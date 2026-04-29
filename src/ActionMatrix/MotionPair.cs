using System;
using UnityEngine;
using Sirenix.OdinInspector;

/// <summary>
/// 1つのアクション状態に紐づくアニメーション情報を保持する ScriptableObject。
/// 2つのキャラクター（A/B）のアニメーション状態名 + メタデータ（BPM、タグ、補正値）を持つ。
/// stableId による永続参照で、Excel I/O や外部参照のリネーム耐性を確保する。
/// </summary>
[CreateAssetMenu(fileName = "NewMotionPair", menuName = "ActionSystem/MotionPair")]
public class MotionPair : ScriptableObject
{
    [BoxGroup("Basic Info")]
    [LabelText("Stable ID")]
    [ReadOnly]
    [SerializeField] private string stableId;

    /// <summary>リネーム耐性のある永続ID。Excel参照解決等に使用。</summary>
    public string StableId => stableId;

#if UNITY_EDITOR
    /// <summary>Editor専用: stableIdが空なら自動付与する（GUID 12文字）。</summary>
    public void EnsureStableId()
    {
        if (string.IsNullOrEmpty(stableId))
        {
            stableId = System.Guid.NewGuid().ToString("N").Substring(0, 12);
            UnityEditor.EditorUtility.SetDirty(this);
        }
    }
#endif

    [BoxGroup("Basic Info")]
    [LabelWidth(120)]
    public string pairLabel;

    [BoxGroup("Basic Info")]
    [EnumToggleButtons]
    public MotionTag tag;

    [BoxGroup("Animation States")]
    public string actorAStateName;
    [BoxGroup("Animation States")]
    public string actorBStateName;

    [BoxGroup("Basic Info")]
    [EnumToggleButtons]
    public MotionGroup motionGroup;

    [BoxGroup("Basic Info")]
    [LabelText("1ループの長さ（秒）")]
    [Tooltip("速度倍率1.0時の1ループの秒数。音声BPMマッチングに使用。0=未設定")]
    public float loopDuration;

    [BoxGroup("Basic Info")]
    [LabelText("最大速度倍率")]
    [Tooltip("このモーションの速度倍率上限。0=制限なし")]
    [Range(0f, 10f)]
    public float maxSpeedMultiplier = 0f;

    /// <summary>1ループ秒数からBPMを算出する（60/秒）</summary>
    public float BaseBPM => loopDuration > 0f ? 60f / loopDuration : 0f;

    /// <summary>このモーションの最大BPMを返す。0=制限なし</summary>
    public float MaxBPM => (maxSpeedMultiplier > 0f && BaseBPM > 0f) ? BaseBPM * maxSpeedMultiplier : 0f;

    // --- 詳細補正値（Inspector上は折りたたみ） ---

    [FoldoutGroup("Adjustments")]
    [Title("Actor A Root Alignment", "Anchor からのズレ補正")]
    [InfoBox("基準点 (Anchor) から、このモーションの再生位置をどれだけズラすか")]
    public Vector3 actorARootPositionOffset;

    [FoldoutGroup("Adjustments")]
    public Vector3 actorARootRotationOffset;

    [FoldoutGroup("Adjustments")]
    [Title("Actor B Body Adjustment")]
    public Vector3 actorBBodyPositionOffset;
    [FoldoutGroup("Adjustments")]
    public Vector3 actorBBodyRotationOffset;
}

/// <summary>
/// モーションのグループ識別。アンカーオフセットの大別に使う。
/// </summary>
public enum MotionGroup
{
    Group_A = 0,
    Group_B = 1,
    Group_C = 2,
    Group_D = 3,
    Group_E = 4,
}

/// <summary>
/// モーションのタグ（ビットフラグ）。アクション分類・能力・終端種別の3カテゴリを束ねる。
/// </summary>
[Flags]
public enum MotionTag
{
    None = 0,

    // アクション分類（重複可能）
    Position_TypeA = 1 << 0,
    Position_TypeB = 1 << 1,
    Position_TypeC = 1 << 2,
    Position_TypeD = 1 << 3,
    Position_TypeE = 1 << 4,
    Position_TypeF = 1 << 5,
    Position_TypeG = 1 << 6,
    Position_VariantH = 1 << 7,
    Position_VariantI = 1 << 8,
    Position_VariantJ = 1 << 9,
    Position_TypeK = 1 << 16,

    // 能力フラグ（重ね合わせ可能）
    Capability_Reactive = 1 << 11,         // 反応可能（パラメータが上がる）
    Capability_FacialExpression = 1 << 12, // 表情変化が可能
    Capability_GazeMove = 1 << 13,         // 視線移動が可能
    Capability_TorsoRotation = 1 << 14,    // 体幹回転が可能
    Capability_AutoSync = 1 << 15,         // 自動同期演出が可能

    // 終端種別
    Terminal_TypeA = 1 << 20,
    Terminal_TypeB = 1 << 21,
}

/// <summary>MotionTag のよく使う組み合わせ定数。</summary>
public static class MotionTagSets
{
    public const MotionTag AnyPosition =
        MotionTag.Position_TypeA | MotionTag.Position_TypeB
        | MotionTag.Position_TypeC | MotionTag.Position_TypeD
        | MotionTag.Position_TypeE | MotionTag.Position_TypeF
        | MotionTag.Position_TypeG | MotionTag.Position_TypeK
        | MotionTag.Position_VariantH | MotionTag.Position_VariantI
        | MotionTag.Position_VariantJ;

    /// <summary>反応可能なメイン分類（ピーク演出が連動する分類）</summary>
    public const MotionTag MainReactive =
        MotionTag.Position_TypeA | MotionTag.Position_TypeB
        | MotionTag.Position_TypeC | MotionTag.Position_TypeD
        | MotionTag.Position_TypeE | MotionTag.Position_TypeF
        | MotionTag.Position_TypeG | MotionTag.Position_TypeK;

    public const MotionTag VariantSet =
        MotionTag.Position_VariantH | MotionTag.Position_VariantI | MotionTag.Position_VariantJ;
}
