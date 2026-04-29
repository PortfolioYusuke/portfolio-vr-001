using System.Collections.Generic;
using UnityEngine;
using Sirenix.OdinInspector;

/// <summary>
/// 1つの「アクション状態」の全フィールド。<see cref="ActionMatrix.states"/> の1行に対応する。
/// Odin Inspector の `[VerticalGroup]` / `[TableColumnWidth]` で
/// 表形式編集UXを実現している。
/// </summary>
[System.Serializable]
public class MotionStateData
{
    // --- 1. モーション参照 ---
    [VerticalGroup("Basic")]
    [TableColumnWidth(250, Resizable = false)]
    [HideLabel]
    [AssetSelector(Paths = "Assets")]
    public MotionPair motionPair;

    // --- 2. 滞在時間 ---
    [VerticalGroup("Timing")]
    [TableColumnWidth(180, Resizable = false)]
    [MinMaxSlider(0, 60, ShowFields = true)]
    [LabelText("Duration (s)")]
    public Vector2 durationRange = new Vector2(10, 20);

    // --- 3. 速度カーブ ---
    [VerticalGroup("Timing")]
    [HideLabel]
    public AnimationCurve speedCurve = AnimationCurve.Linear(0, 1, 1, 1);

    // --- 4. BPMオーバーライド ---
    [VerticalGroup("BPM")]
    [LabelText("BPM設定ON")]
    [Tooltip("ONの場合、このステートに遷移したときにBPMを指定値に変更する")]
    public bool useOverrideBPM = false;

    [VerticalGroup("BPM")]
    [LabelText("目標BPM")]
    [Tooltip("ステート遷移時に設定するBPM（0=停止）")]
    [ShowIf("useOverrideBPM")]
    [Range(0f, 300f)]
    public float overrideBPM = 0f;

    // --- 5. 遷移設定 ---
    [VerticalGroup("Transition")]
    [TableColumnWidth(250)]
    [LabelText("Next Weight")]
    [LabelWidth(80)]
    public float nextWeight = 10f;

    [VerticalGroup("Transition")]
    [ListDrawerSettings(Expanded = false, NumberOfItemsPerPage = 3)]
    [LabelText("Branches")]
    public List<TransitionBranch> branches = new List<TransitionBranch>();
}
