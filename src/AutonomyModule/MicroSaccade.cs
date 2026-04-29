using UnityEngine;
using Sirenix.OdinInspector;

/// <summary>
/// 視線制御モジュール。Eye bone を直接回転させて以下の挙動を合成する：
///
/// 1. アイドルサイクル — 目を見る → 口を見る → 目に戻る → 横に逸らす → 目に戻る
/// 2. 左右目交互注視 — プレイヤーの左目⇔右目を 0.5-2 秒で切替
/// 3. 注視点シフト — 無意識的な微小ズレ（人間らしい不安定さ）
/// 4. 微小揺らぎ — Perlin ノイズによるふとした目線の動き
/// 5. サッカード — 高速微振動
/// 6. 目線逸らし — パラメータ値に応じた伏し目／横逸らし
///
/// <see cref="EyeMicroMotion"/> と共存可能（後段の EyeMicroMotion が更に微振動を加算）。
///
/// このファイルはポートフォリオ用に**主要構造とフィールド宣言を抜粋**した版です。
/// 実装版は約 460 行で、各サブ機能（avoidance / fixation shift / saccade）の
/// 詳細状態管理ロジックを内包しています。
/// </summary>
public class MicroSaccade : AutonomyModule
{
    [Title("ボーン参照（未設定なら自動検索）")]
    [SerializeField] private Transform leftEyeBone;
    [SerializeField] private Transform rightEyeBone;
    [SerializeField] private Transform headBone;

    [Title("目線ターゲット設定")]
    [Tooltip("目線のベースターゲット（未設定ならカメラを使用）")]
    [SerializeField] private Transform gazeTarget;
    [Tooltip("視線方向の上下オフセット（メートル）。負の値で目標を下に補正")]
    [SerializeField] private float upwardGazeOffset = -0.08f;

    [Title("アイドルサイクル")]
    [SerializeField] private bool enableIdleCycle = true;
    [SerializeField] private float mouthOffsetY = -0.08f;
    [SerializeField] private float sideGlanceOffset = 0.12f;

    [Title("左右目交互注視")]
    [SerializeField] private float eyeSpacing = 0.032f;
    [SerializeField] private float eyeSwitchMinInterval = 0.5f;
    [SerializeField] private float eyeSwitchMaxInterval = 2.0f;

    [Title("目線逸らし（恥じらい）")]
    [SerializeField] private float avoidanceBaseRate = 0.15f;
    [SerializeField] private float avoidancePeakMultiplier = 0.2f;
    [SerializeField] private Vector2 avoidanceDurationRange = new Vector2(0.8f, 2.5f);
    [SerializeField] private Vector2 avoidanceAngleRange = new Vector2(15f, 35f);

    [Title("注視点シフト（Fixation Shift）")]
    [SerializeField] private float fixationShiftMaxAngle = 10f;
    [SerializeField] private float fixationShiftRate = 0.8f;
    [SerializeField] private float fixationShiftHoldTime = 0.8f;
    [SerializeField] private float fixationReturnSpeed = 2f;

    [Title("微小揺らぎ + サッカード")]
    [SerializeField] private float microJitterAmplitude = 1.0f;
    [SerializeField] private float microJitterSpeed = 2.0f;
    [SerializeField] private float saccadeAmplitude = 2.5f;
    [SerializeField] private float saccadePeakMultiplier = 2.0f;
    [SerializeField] private float saccadeSpeed = 8.0f;

    [Title("回転制限")]
    [SerializeField] private float maxEyeRotation = 25f;

    /// <summary>外部から振幅倍率を上乗せする（特殊状態時等）。1.0 = 通常。</summary>
    [HideInInspector] public float overrideAmplitudeMultiplier = 1.0f;

    /// <summary>視線をプレイヤーに固定し、サッカード以外の目の動きを停止する。</summary>
    [HideInInspector] public bool peakGazeLock = false;

    /// <summary>視線の垂直オフセット（負 = 上向き）。Hub 経由で制御。</summary>
    [HideInInspector] public float peakVerticalOffset = 0f;

    /// <summary>ON にすると目線逸らし・サイクル・注視シフトを停止する。</summary>
    [HideInInspector] public bool suppressIdleMovements = false;

    /// <summary>アイドルサイクルの状態種別。</summary>
    private enum CyclePhase { LookingEye, LookingMouth, ReturningToEye, GlancingSide, ReturningFromSide }

    private CyclePhase _cyclePhase = CyclePhase.LookingEye;
    private float _cycleTimer;
    private bool _gazeAtLeftEye = true;
    private float _eyeSwitchTimer;
    private bool _isAvoiding;
    private float _avoidanceTimer;
    private Vector3 _currentFixationShift;
    private float _fixationShiftTimer;
    private float _noiseSeedX, _noiseSeedY;

    void OnEnable() { } // Inspector チェックボックス表示用

    void Awake()
    {
        _noiseSeedX = Random.Range(0f, 100f);
        _noiseSeedY = Random.Range(100f, 200f);
    }

    public override void Initialize(AutonomyHub parentHub)
    {
        base.Initialize(parentHub);
        if (gazeTarget == null && Camera.main != null) gazeTarget = Camera.main.transform;
        AutoFindBones();
    }

    private void AutoFindBones()
    {
        if (leftEyeBone == null && context != null) leftEyeBone = context.FindBone("LeftEye");
        if (rightEyeBone == null && context != null) rightEyeBone = context.FindBone("RightEye");
        if (headBone == null && context != null) headBone = context.FindBone("Head");
    }

    public override void Tick(float parameterLevel, float autonomy, float fatigue)
    {
        if (leftEyeBone == null || rightEyeBone == null || gazeTarget == null) return;
        if (autonomy <= 0f) return;

        // 1. ベース注視点の決定
        Vector3 basePoint = ComputeBaseGazePoint(parameterLevel);

        // 2. アイドルサイクル / 逸らし / 注視シフトの更新（ピーク時は停止）
        if (!suppressIdleMovements && !peakGazeLock)
        {
            UpdateIdleCycle(parameterLevel);
            UpdateAvoidance(parameterLevel);
            UpdateFixationShift();
        }

        // 3. 注視点に視線オフセットを適用
        Vector3 finalGaze = ApplyGazeOffsets(basePoint);

        // 4. eye bone を回転
        ApplyEyeRotation(leftEyeBone, finalGaze, parameterLevel);
        ApplyEyeRotation(rightEyeBone, finalGaze, parameterLevel);

        // 5. サッカード（高速微振動）と microJitter を eye bone に加算
        ApplyMicroJitterAndSaccade(parameterLevel);
    }

    // ---- 以下、各サブ機能のスタブ（元実装は約100行ずつ） ----

    private Vector3 ComputeBaseGazePoint(float parameterLevel)
    {
        // 左右目交互注視タイマーを進めて、プレイヤーの左目/右目どちらを見るか決める
        _eyeSwitchTimer -= Time.deltaTime;
        if (_eyeSwitchTimer <= 0f)
        {
            _gazeAtLeftEye = !_gazeAtLeftEye;
            _eyeSwitchTimer = Random.Range(eyeSwitchMinInterval, eyeSwitchMaxInterval);
        }
        Vector3 lateralOffset = gazeTarget.right * (eyeSpacing * (_gazeAtLeftEye ? -1 : 1));

        // 視線方向の上下オフセット（パラメータ値が高いほど補正量が増える）
        Vector3 vertical = gazeTarget.up * (upwardGazeOffset * parameterLevel + peakVerticalOffset);

        return gazeTarget.position + lateralOffset + vertical;
    }

    private void UpdateIdleCycle(float parameterLevel)
    {
        if (!enableIdleCycle) return;
        // 元実装: phase ごとのタイマーで LookingEye → LookingMouth → ReturningToEye → ...
        // のサイクルを回す。タイマー満了で次フェーズへ
    }

    private void UpdateAvoidance(float parameterLevel)
    {
        // 元実装: 抽選確率 = baseRate * Lerp(1, peakMultiplier, parameterLevel)
        // 当たれば avoidanceDurationRange の秒数だけ視線を逸らす
    }

    private void UpdateFixationShift()
    {
        // 元実装: fixationShiftRate / 秒の頻度で抽選し、ランダムに視線をズラす
        // ズレた状態を fixationShiftHoldTime 秒保持してから fixationReturnSpeed で戻す
    }

    private Vector3 ApplyGazeOffsets(Vector3 basePoint)
    {
        // 逸らし状態 → アイドルサイクルのフェーズ別オフセット → 注視点シフトを順に加算
        return basePoint + _currentFixationShift;
    }

    private void ApplyEyeRotation(Transform eye, Vector3 worldTarget, float parameterLevel)
    {
        if (eye == null) return;
        Vector3 localTarget = eye.parent.InverseTransformPoint(worldTarget);
        Quaternion rotation = Quaternion.LookRotation(localTarget);
        // 制限角度内にクランプ
        Vector3 euler = rotation.eulerAngles;
        if (euler.x > 180f) euler.x -= 360f;
        if (euler.y > 180f) euler.y -= 360f;
        euler.x = Mathf.Clamp(euler.x, -maxEyeRotation, maxEyeRotation);
        euler.y = Mathf.Clamp(euler.y, -maxEyeRotation, maxEyeRotation);
        eye.localRotation = Quaternion.Euler(euler);
    }

    private void ApplyMicroJitterAndSaccade(float parameterLevel)
    {
        float t = Time.time;
        float jitterAmp = microJitterAmplitude * overrideAmplitudeMultiplier;
        float saccadeAmp = saccadeAmplitude * Mathf.Lerp(1f, saccadePeakMultiplier, parameterLevel) * overrideAmplitudeMultiplier;

        float jx = (Mathf.PerlinNoise(t * microJitterSpeed + _noiseSeedX, 0f) - 0.5f) * 2f * jitterAmp;
        float jy = (Mathf.PerlinNoise(0f, t * microJitterSpeed + _noiseSeedY) - 0.5f) * 2f * jitterAmp;
        float sx = (Mathf.PerlinNoise(t * saccadeSpeed + _noiseSeedX, 1f) - 0.5f) * 2f * saccadeAmp;
        float sy = (Mathf.PerlinNoise(1f, t * saccadeSpeed + _noiseSeedY) - 0.5f) * 2f * saccadeAmp;

        if (leftEyeBone != null)
            leftEyeBone.localRotation = leftEyeBone.localRotation * Quaternion.Euler(jy + sy, jx + sx, 0f);
        if (rightEyeBone != null)
            rightEyeBone.localRotation = rightEyeBone.localRotation * Quaternion.Euler(jy + sy, jx + sx, 0f);
    }
}
