using UnityEngine;
using Sirenix.OdinInspector;

/// <summary>
/// 1表情 = 1アセット。BlendShape 値のセットを保持する。
/// アニメーションクリップ (.anim) からインポートして生成する。
/// </summary>
[CreateAssetMenu(fileName = "NewExpression", menuName = "ExpressionSystem/Expression Preset")]
public class ExpressionPreset : ScriptableObject
{
    [Title("表情データ")]
    [Tooltip("元の .anim ファイル名（参照用）")]
    [LabelText("元アニメーション名")]
    public string sourceAnimName;

    [Tooltip("カテゴリ（フォルダ名）")]
    [LabelText("カテゴリ")]
    public string category;

    [Tooltip("BlendShape エントリ一覧")]
    [LabelText("BlendShape一覧")]
    public BlendShapeEntry[] shapes = new BlendShapeEntry[0];

    [System.Serializable]
    public class BlendShapeEntry
    {
        [Tooltip("BlendShape 名（例: eye_close）")]
        [LabelText("キー名")]
        public string shapeKey;

        [Tooltip("基本値（0-100）")]
        [Range(0f, 100f)]
        [LabelText("基本値")]
        public float baseValue;

        [Tooltip("揺らぎ幅（±この値の範囲でランダム変動）")]
        [Range(0f, 20f)]
        [LabelText("揺らぎ幅")]
        public float randomRange;

        [Tooltip("部位グループ（カスケード遅延・部分ロックの単位）")]
        [LabelText("部位")]
        public FacePartGroup partGroup;
    }

    /// <summary>
    /// 部位グループ。外部システムが特定部位だけロックしたいときの単位。
    /// </summary>
    public enum FacePartGroup
    {
        [LabelText("目")] Eye,
        [LabelText("眉")] Eyebrow,
        [LabelText("口")] Mouth,
        [LabelText("頬")] Cheek,
        [LabelText("その他")] Other
    }

    /// <summary>BlendShape 名から部位グループを自動判定する。</summary>
    public static FacePartGroup ClassifyPartGroup(string shapeName)
    {
        string lower = shapeName.ToLower();

        if (lower.Contains("eye") && !lower.Contains("eyebrow") && !lower.Contains("brow"))
            return FacePartGroup.Eye;
        if (lower.Contains("brow") || lower.Contains("eyebrow"))
            return FacePartGroup.Eyebrow;
        if (lower.Contains("mouth") || lower.Contains("tongue") || lower.Contains("lip"))
            return FacePartGroup.Mouth;
        if (lower.Contains("cheek"))
            return FacePartGroup.Cheek;
        return FacePartGroup.Other;
    }
}
