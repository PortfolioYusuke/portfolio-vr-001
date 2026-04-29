using UnityEngine;
using System.Collections.Generic;
using Sirenix.OdinInspector;
using DG.Tweening;

/// <summary>
/// 表情制御モジュール（<see cref="AutonomyModule"/> 派生）。
/// <see cref="ExpressionProfile"/> に基づきパラメータ値レンジに応じた表情を自動切替する。
/// 部位グループ別のロック機構により、外部システム（接触演出・特殊状態演出等）と
/// 干渉せずに協調する。DOTween で部位別カスケード遷移（目→眉→口→頬）を行う。
///
/// このファイルはポートフォリオ用にコア部分のみ抜粋した版です。
/// 元実装には加えて、瞬きレイヤーのパラメータ連動抑制、特殊状態時の強制遷移等が
/// 含まれます。
/// </summary>
public class ExpressionController : AutonomyModule
{
    [Title("参照")]
    [SerializeField] private ExpressionProfile profile;
    [SerializeField] private SkinnedMeshRenderer faceMesh;

    [Title("遷移設定")]
    [Tooltip("部位別カスケード遅延（秒）。目→眉→口→頬 の順に遅れて遷移開始")]
    [SerializeField] private float cascadeDelay = 0.5f;

    [Tooltip("各 BlendShape の遷移時間（秒）。長いほどゆっくり変化")]
    [SerializeField] private float tweenDuration = 8f;

    [Tooltip("遷移時間のランダム揺らぎ（±秒）")]
    [SerializeField] private float tweenDurationJitter = 1f;

    [Tooltip("遷移のイージング")]
    [SerializeField] private Ease tweenEase = Ease.InOutSine;

    [Title("デバッグ")]
    [ShowInInspector, ReadOnly] private string _currentPresetName = "(なし)";
    [ShowInInspector, ReadOnly] private string _currentRange = "";
    [ShowInInspector, ReadOnly] private float _nextSwitchTime;

    /// <summary>外部から部位別にロックする。ロック中の部位は表情遷移の対象から外れる。</summary>
    private readonly HashSet<ExpressionPreset.FacePartGroup> _lockedGroups
        = new HashSet<ExpressionPreset.FacePartGroup>();

    private Dictionary<string, int> _shapeIndexCache = new Dictionary<string, int>();
    private ExpressionPreset _currentPreset;
    private ExpressionProfile.ExpressionMapping _currentMapping;
    private Sequence _activeSequence;

    public override void Initialize(AutonomyHub parentHub)
    {
        base.Initialize(parentHub);
        AutoFindFaceMesh();
        BuildShapeIndexCache();
    }

    private void AutoFindFaceMesh()
    {
        if (faceMesh != null) return;
        faceMesh = context != null ? context.FaceSMR : GetComponentInChildren<SkinnedMeshRenderer>();
    }

    private void BuildShapeIndexCache()
    {
        _shapeIndexCache.Clear();
        if (faceMesh == null || faceMesh.sharedMesh == null) return;
        int count = faceMesh.sharedMesh.blendShapeCount;
        for (int i = 0; i < count; i++)
            _shapeIndexCache[faceMesh.sharedMesh.GetBlendShapeName(i)] = i;
    }

    public override void Tick(float parameterLevel, float autonomy, float fatigue)
    {
        if (profile == null || faceMesh == null) return;

        // パラメータレンジの判定
        var mapping = profile.GetMapping(parameterLevel);
        if (mapping == null) return;

        // レンジが切り替わった or 切替時刻が来た → 新規プリセットを抽選して遷移
        bool rangeChanged = _currentMapping != mapping;
        bool timerExpired = Time.time >= _nextSwitchTime;

        if (rangeChanged || timerExpired)
        {
            _currentMapping = mapping;
            _currentRange = mapping.label;

            if (mapping.candidates != null && mapping.candidates.Length > 0)
            {
                var preset = mapping.candidates[Random.Range(0, mapping.candidates.Length)];
                if (preset != null && preset != _currentPreset)
                {
                    TransitionTo(preset);
                }
            }
            _nextSwitchTime = Time.time + mapping.switchInterval;
        }
    }

    /// <summary>
    /// 指定プリセットへ部位別カスケードで遷移する。
    /// DOTween Sequence で複数 Tween を同時管理し、Kill() で一括中断可能。
    /// </summary>
    public void TransitionTo(ExpressionPreset preset)
    {
        if (preset == null || faceMesh == null) return;

        _activeSequence?.Kill();
        _activeSequence = DOTween.Sequence();
        _currentPreset = preset;
        _currentPresetName = preset.name;

        foreach (var entry in preset.shapes)
        {
            // ロック中の部位はスキップ
            if (_lockedGroups.Contains(entry.partGroup)) continue;

            int idx;
            if (!_shapeIndexCache.TryGetValue(entry.shapeKey, out idx)) continue;

            float startValue = faceMesh.GetBlendShapeWeight(idx);
            float targetValue = entry.baseValue + Random.Range(-entry.randomRange, entry.randomRange);
            targetValue = Mathf.Clamp(targetValue, 0f, 100f);

            float delay = GetCascadeDelay(entry.partGroup);
            float dur = tweenDuration + Random.Range(-tweenDurationJitter, tweenDurationJitter);
            int capturedIdx = idx; // closure 用キャプチャ

            // 各部位に遅延をつけて Tween を Sequence に挿入
            _activeSequence.Insert(delay,
                DOTween.To(
                    () => faceMesh.GetBlendShapeWeight(capturedIdx),
                    v => faceMesh.SetBlendShapeWeight(capturedIdx, v),
                    targetValue,
                    dur).SetEase(tweenEase));
        }
    }

    /// <summary>部位順序に従ったカスケード遅延を返す。</summary>
    private float GetCascadeDelay(ExpressionPreset.FacePartGroup group)
    {
        switch (group)
        {
            case ExpressionPreset.FacePartGroup.Eye: return 0f;
            case ExpressionPreset.FacePartGroup.Eyebrow: return cascadeDelay;
            case ExpressionPreset.FacePartGroup.Mouth: return cascadeDelay * 2f;
            case ExpressionPreset.FacePartGroup.Cheek: return cascadeDelay * 3f;
            default: return 0f;
        }
    }

    /// <summary>外部システムが特定部位だけ占有する（解除は <see cref="UnlockGroup"/>）。</summary>
    public void LockGroup(ExpressionPreset.FacePartGroup group) => _lockedGroups.Add(group);
    public void UnlockGroup(ExpressionPreset.FacePartGroup group) => _lockedGroups.Remove(group);
    public bool IsGroupLocked(ExpressionPreset.FacePartGroup group) => _lockedGroups.Contains(group);
}
