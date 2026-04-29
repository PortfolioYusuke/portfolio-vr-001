using UnityEngine;
using System.Collections;
using System.Collections.Generic;

/// <summary>
/// <see cref="ActionMatrix"/> を再生するランタイムエンジン。状態タイマー、速度補間、
/// 重み付きランダム分岐、Animator 駆動を担う。
/// このファイルはポートフォリオ用に**コア部分のみ抜粋**したものであり、
/// 実装版ではこれに加えて IK 連動・装着物座標補正・特殊状態演出等が連携する。
///
/// 設計のポイント：
/// - 状態遷移要求は <see cref="GameEventHub"/> 経由（疎結合）
/// - 状態タイマー満了で <see cref="GoToNextState"/> を自発的に呼び、重み付き抽選
/// - 速度倍率は線形補間でスムージング（speedSmoothRate で減衰係数を制御）
/// </summary>
public class ActionEngine : MonoBehaviour
{
    [Header("Basic Settings")]
    [SerializeField] private ActionMatrix currentMatrix;
    [SerializeField] private Animator actorAAnimator;
    [SerializeField] private Animator actorBAnimator;

    [Header("Speed Settings")]
    [SerializeField] private float minMultiplier = 0.0f;
    [SerializeField] private float maxMultiplier = 5.0f;
    [SerializeField] private float changeSensitivity = 1.5f;

    [Tooltip("速度スムージングの速さ（大きいほど即座に追従。5=約0.2秒で追従）")]
    [SerializeField] private float speedSmoothRate = 5f;

    [Tooltip("目標BPMの上限。これ以上にはならない")]
    [SerializeField] private float maxTargetBPM = 160f;

    [Header("Alignment Settings")]
    [SerializeField] private bool enableAutoAlign = true;
    [SerializeField] private Transform alignmentAnchor;
    [SerializeField] private Transform characterRoot;

    private float _currentMultiplier = 1.0f;
    private float _targetBPM = 0f;
    private float _lastNotifiedMultiplier = -1f;

    private MotionStateData currentData;
    private int currentStateIndex = 0;
    private float stateTimer = 0f;
    private float currentMaxDuration = 0f;
    private float _currentBaseSpeed = 1f;

    private Coroutine _alignCoroutine;
    private MotionPair _lastPlayedPair;

    private bool _peakLocked;     // ピーク演出中の状態変更封印

    // ===========================
    // 1. ライフサイクル
    // ===========================

    void OnEnable()
    {
        GameEventHub.OnNextStateRequest += GoToNextState;
        GameEventHub.OnStateSelect += PlayState;
        GameEventHub.OnSpeedChangeRequest += OnSpeedChange;
        GameEventHub.OnSpeedResetRequest += ResetSpeed;
        GameEventHub.OnPeakStarted += OnPeakStart;
    }

    void OnDisable()
    {
        GameEventHub.OnNextStateRequest -= GoToNextState;
        GameEventHub.OnStateSelect -= PlayState;
        GameEventHub.OnSpeedChangeRequest -= OnSpeedChange;
        GameEventHub.OnSpeedResetRequest -= ResetSpeed;
        GameEventHub.OnPeakStarted -= OnPeakStart;
    }

    // ===========================
    // 2. メインループ
    // ===========================

    void Update()
    {
        if (currentMatrix == null || currentMatrix.states.Count == 0) return;

        // 速度倍率を targetBPM に合わせて線形補間
        float baseBPM = currentData?.motionPair?.BaseBPM ?? 0f;
        if (baseBPM > 0f)
        {
            float targetMul = _targetBPM / baseBPM;
            _currentMultiplier = Mathf.Lerp(_currentMultiplier, targetMul, Time.deltaTime * speedSmoothRate);
        }

        // 速度カーブの値を Animator に流す
        if (currentData != null)
        {
            float t = currentMaxDuration > 0 ? Mathf.Clamp01(stateTimer / currentMaxDuration) : 0f;
            _currentBaseSpeed = currentData.speedCurve.Evaluate(t);
            float finalSpeed = _currentBaseSpeed * _currentMultiplier;
            if (actorAAnimator) actorAAnimator.speed = finalSpeed;
            if (actorBAnimator) actorBAnimator.speed = finalSpeed;
        }

        // タイマー満了で次状態へ
        stateTimer += Time.deltaTime;
        if (!_peakLocked && stateTimer >= currentMaxDuration)
        {
            GoToNextState();
        }

        // 速度倍率変化通知（外部システムが BPM 連動するため）
        if (Mathf.Abs(_currentMultiplier - _lastNotifiedMultiplier) > 0.01f)
        {
            _lastNotifiedMultiplier = _currentMultiplier;
            GameEventHub.OnSpeedMultiplierChanged?.Invoke(_currentMultiplier);
        }
    }

    // ===========================
    // 3. 状態遷移
    // ===========================

    /// <summary>次の状態を重み付きランダムで選んで遷移する。</summary>
    public void GoToNextState()
    {
        if (_peakLocked) return;
        if (currentMatrix == null || currentMatrix.states.Count == 0) return;
        if (currentStateIndex >= currentMatrix.states.Count) currentStateIndex = 0;

        var data = currentMatrix.states[currentStateIndex];

        // nextWeight + branches の合計重みで抽選
        float branchSum = 0f;
        foreach (var b in data.branches) branchSum += b.weight;
        float totalWeight = (data.nextWeight > 0.5f)
            ? data.nextWeight + branchSum
            : 100f; // パーセンテージ方式

        float diceRoll = Random.Range(0f, totalWeight);
        float cumulative = 0f;
        int nextIndex = -1;

        foreach (var b in data.branches)
        {
            cumulative += b.weight;
            if (diceRoll < cumulative)
            {
                nextIndex = b.targetStateIndex;
                break;
            }
        }

        if (nextIndex == -1) nextIndex = currentStateIndex + 1;
        if (nextIndex >= currentMatrix.states.Count) nextIndex = 0;

        PlayState(nextIndex);
    }

    /// <summary>指定インデックスの状態を再生開始。</summary>
    public void PlayState(int index)
    {
        if (currentMatrix == null || currentMatrix.states.Count == 0) return;
        if (index < 0 || index >= currentMatrix.states.Count) index = 0;

        currentStateIndex = index;
        stateTimer = 0;
        currentData = currentMatrix.states[index];
        currentMaxDuration = Random.Range(currentData.durationRange.x, currentData.durationRange.y);

        GameEventHub.OnMotionChanged?.Invoke(index);

        var nextPair = currentData.motionPair;

        // 同じ MotionGroup 内の遷移は滑らか、グループを跨ぐ遷移はカット
        float transitionDuration = 0.5f;
        if (_lastPlayedPair != null && nextPair != null
            && _lastPlayedPair.motionGroup != nextPair.motionGroup)
        {
            transitionDuration = 0f;
        }
        _lastPlayedPair = nextPair;

        // Animator 切替
        if (nextPair != null)
        {
            if (actorAAnimator) actorAAnimator.CrossFadeInFixedTime(nextPair.actorAStateName, transitionDuration);
            if (actorBAnimator) actorBAnimator.CrossFadeInFixedTime(nextPair.actorBStateName, transitionDuration);

            if (enableAutoAlign && alignmentAnchor != null && characterRoot != null)
            {
                if (_alignCoroutine != null) StopCoroutine(_alignCoroutine);
                _alignCoroutine = StartCoroutine(AlignRoot(nextPair, transitionDuration));
            }
        }

        // BPMオーバーライド指定があれば即時反映
        if (currentData.useOverrideBPM)
        {
            float clamped = Mathf.Min(currentData.overrideBPM, maxTargetBPM);
            float maxFromMotion = nextPair != null ? nextPair.MaxBPM : 0f;
            if (maxFromMotion > 0f) clamped = Mathf.Min(clamped, maxFromMotion);
            _targetBPM = clamped;
        }

        _lastNotifiedMultiplier = -1f; // 次フレームで必ず通知
    }

    // ===========================
    // 4. 位置合わせ（補間）
    // ===========================

    private IEnumerator AlignRoot(MotionPair pair, float duration)
    {
        Vector3 anchorPos = alignmentAnchor.position;
        Quaternion anchorRot = Quaternion.Euler(0, alignmentAnchor.eulerAngles.y, 0);

        Quaternion targetRot = anchorRot * Quaternion.Euler(pair.actorARootRotationOffset);
        Vector3 targetPos = anchorPos + (anchorRot * pair.actorARootPositionOffset);

        if (duration <= 0.01f)
        {
            characterRoot.position = targetPos;
            characterRoot.rotation = targetRot;
            yield break;
        }

        float timer = 0f;
        Vector3 startPos = characterRoot.position;
        Quaternion startRot = characterRoot.rotation;

        while (timer < duration)
        {
            timer += Time.deltaTime;
            float t = timer / duration;
            float smoothT = Mathf.SmoothStep(0f, 1f, t);
            characterRoot.position = Vector3.Lerp(startPos, targetPos, smoothT);
            characterRoot.rotation = Quaternion.Lerp(startRot, targetRot, smoothT);
            yield return null;
        }
        characterRoot.position = targetPos;
        characterRoot.rotation = targetRot;
    }

    // ===========================
    // 5. 速度入力ハンドラ
    // ===========================

    private void OnSpeedChange(float delta)
    {
        if (_peakLocked) return;
        float baseBPM = currentData?.motionPair?.BaseBPM ?? 60f;
        _targetBPM = Mathf.Clamp(_targetBPM + delta * changeSensitivity, 0f, maxTargetBPM);
    }

    private void ResetSpeed()
    {
        _targetBPM = 0f;
    }

    // ===========================
    // 6. ピーク演出フック
    // ===========================

    private void OnPeakStart(PeakTrigger trigger)
    {
        // ピーク中は状態遷移を封じる（ピーク演出の安定のため）
        _peakLocked = true;
        // 演出終了は外部システムが OnPeakReached 後に解除する
    }

    public void ReleasePeakLock() => _peakLocked = false;

    // ===========================
    // 7. 公開API
    // ===========================

    public ActionMatrix GetCurrentMatrix() => currentMatrix;
    public int GetCurrentStateIndex() => currentStateIndex;
    public float GetCurrentMultiplier() => _currentMultiplier;
    public Transform GetCharacterRoot() => characterRoot;

    /// <summary>ランタイムでマトリクスを差し替える（プリセット切替用）。</summary>
    public void SetMatrix(ActionMatrix matrix, bool playFirst = true)
    {
        currentMatrix = matrix;
        if (playFirst)
        {
            currentStateIndex = 0;
            if (matrix != null && matrix.states.Count > 0)
                PlayState(0);
        }
    }
}
