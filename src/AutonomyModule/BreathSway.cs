using UnityEngine;

/// <summary>
/// 呼吸による体の揺れモジュール。胸・肩ボーンに sin + Perlin Noise を加算して
/// 自然な呼吸の上下動を再現する。
///
/// パラメータ値が高いほど呼吸が速く・大きくなる。
/// 別途 <see cref="ActionEngine"/> から effective BPM が得られる場合は
/// それに同期して呼吸速度を決める（モーションと呼吸のリズムを一致させる）。
///
/// このファイルはポートフォリオ用に基本部分のみを抜粋している。
/// 実装版にはこれに加えてピーク状態時の特殊演出（一時的な振幅増加等）が含まれる。
/// </summary>
public class BreathSway : AutonomyModule
{
    [Header("ボーン参照（未設定なら自動検索）")]
    [SerializeField] private Transform chestBone;
    [SerializeField] private Transform leftShoulderBone;
    [SerializeField] private Transform rightShoulderBone;

    [Header("BPM連動設定")]
    [Tooltip("呼吸はモーションBPMの何分の1で動くか（2 = BPMの半分の速さ）")]
    [SerializeField] private float breathDivisor = 2f;

    [Header("フォールバック（外部BPM=0時）")]
    [Tooltip("基本呼吸速度（回/分）")]
    [SerializeField] private float baseBreathRate = 16f;
    [Tooltip("パラメータ値最大時の呼吸速度（回/分）")]
    [SerializeField] private float peakBreathRate = 40f;

    [Header("胸の揺れ")]
    [SerializeField] private float chestBaseAmplitude = 0.3f;
    [SerializeField] private float chestPeakMultiplier = 3f;

    [Header("肩の揺れ")]
    [SerializeField] private float shoulderBaseAmplitude = 0.2f;
    [SerializeField] private float shoulderPeakMultiplier = 2f;

    [Header("ノイズ")]
    [Tooltip("ノイズによる不規則さの強さ")]
    [SerializeField] private float noiseStrength = 0.3f;

    private float _noiseSeed;
    private float _breathPhase;
    private ActionEngine _actionEngine;

    void OnEnable() { } // Inspector チェックボックス表示用

    void Awake()
    {
        _noiseSeed = Random.Range(0f, 100f);
    }

    public override void Initialize(AutonomyHub parentHub)
    {
        base.Initialize(parentHub);
        _actionEngine = context != null ? context.ActionEngine : FindObjectOfType<ActionEngine>();
        AutoFindBones();
    }

    private void AutoFindBones()
    {
        if (chestBone == null && context != null) chestBone = context.FindBone("Chest");
        if (leftShoulderBone == null && context != null) leftShoulderBone = context.FindBone("Shoulder_L");
        if (rightShoulderBone == null && context != null) rightShoulderBone = context.FindBone("Shoulder_R");

        if (chestBone == null)
            Debug.LogWarning("[BreathSway] Chest ボーンが見つかりません。CharacterContext.ModelRoot を確認してください。");
    }

    public override void Tick(float parameterLevel, float autonomy, float fatigue)
    {
        if (chestBone == null || autonomy <= 0f) return;

        float dt = Time.deltaTime;

        // 呼吸速度の決定：ActionEngine から実効 BPM が取れればそれに同期、
        // 取れなければ基本値とパラメータ値の補間値にフォールバック
        float effectiveBPM = _actionEngine != null ? _actionEngine.GetCurrentMultiplier() * 60f : 0f;
        float breathBPM = effectiveBPM > 5f
            ? effectiveBPM / breathDivisor
            : Mathf.Lerp(baseBreathRate, peakBreathRate, parameterLevel);

        float breathRate = breathBPM / 60f;
        _breathPhase += breathRate * dt * Mathf.PI * 2f;

        // sin（規則的な呼吸） + Perlin Noise（不規則さ）
        float breathValue = Mathf.Sin(_breathPhase);
        float noise = (Mathf.PerlinNoise(Time.time * 0.5f + _noiseSeed, 0f) - 0.5f) * 2f * noiseStrength;
        float combined = (breathValue + noise) * autonomy;

        // 胸の上下動（X 軸回転）
        float chestAmp = chestBaseAmplitude * Mathf.Lerp(1f, chestPeakMultiplier, parameterLevel);
        chestBone.localRotation = chestBone.localRotation * Quaternion.Euler(combined * chestAmp, 0f, 0f);

        // 肩の上下動（左右で逆相）
        float shoulderAmp = shoulderBaseAmplitude * Mathf.Lerp(1f, shoulderPeakMultiplier, parameterLevel);
        if (leftShoulderBone != null)
            leftShoulderBone.localRotation = leftShoulderBone.localRotation * Quaternion.Euler(0f, 0f, combined * shoulderAmp);
        if (rightShoulderBone != null)
            rightShoulderBone.localRotation = rightShoulderBone.localRotation * Quaternion.Euler(0f, 0f, -combined * shoulderAmp);
    }
}
