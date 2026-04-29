using Sirenix.OdinInspector;

/// <summary>
/// 状態遷移の分岐定義。<see cref="MotionStateData.branches"/> に積まれ、
/// 重み付きランダムで遷移先を抽選する。
/// </summary>
[System.Serializable]
public class TransitionBranch
{
    [HorizontalGroup("row")]
    [LabelText("To State ID")]
    [LabelWidth(70)]
    public int targetStateIndex;

    [HorizontalGroup("row")]
    [LabelText("Weight")]
    [LabelWidth(50)]
    public float weight;
}
