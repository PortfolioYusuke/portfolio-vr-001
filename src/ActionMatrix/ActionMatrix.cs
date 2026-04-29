using UnityEngine;
using System.Collections.Generic;
using Sirenix.OdinInspector;

/// <summary>
/// 状態遷移マトリクス。各行が1つの「アクション状態」を表し、ランタイムは
/// このマトリクスを再生する。Odin Inspector の TableList で表形式編集UXを提供。
///
/// 設計のポイント：
/// - データは ScriptableObject に外出し、コードはエンジンとデータ構造のみ
/// - ランタイムでは <see cref="CopyFrom"/> でディープコピーしてから編集 → 共有SO汚染を回避
/// - 状態遷移は重み付きランダム（次状態への nextWeight + 任意の branches 分岐）
/// </summary>
[CreateAssetMenu(fileName = "NewActionMatrix", menuName = "ActionSystem/ActionMatrix")]
public class ActionMatrix : ScriptableObject
{
    [Title("Matrix Data")]
    [Searchable]
    [TableList(ShowIndexLabels = true)]
    public List<MotionStateData> states = new List<MotionStateData>();

    /// <summary>全データの Duration を一括で倍率変更する Editor ユーティリティ。</summary>
    [Button("Multiply All Durations"), ButtonGroup("Tools")]
    [GUIColor(0.6f, 1f, 0.6f)]
    private void MultiplyDuration(float multiplier)
    {
        foreach (var state in states)
        {
            state.durationRange *= multiplier;
        }
    }

    /// <summary>
    /// このマトリクスの内容を別のマトリクスから**ディープコピー**する。
    /// AnimationCurve も新規インスタンスでラップし、ランタイム編集が
    /// 元のアセットに伝搬しないようにする（教訓レコード #029：共有SO汚染防止）。
    /// </summary>
    public void CopyFrom(ActionMatrix source)
    {
        states.Clear();
        foreach (var src in source.states)
        {
            var copy = new MotionStateData
            {
                motionPair = src.motionPair,
                durationRange = src.durationRange,
                speedCurve = new AnimationCurve(src.speedCurve.keys),
                useOverrideBPM = src.useOverrideBPM,
                overrideBPM = src.overrideBPM,
                nextWeight = src.nextWeight,
                branches = new List<TransitionBranch>()
            };
            foreach (var b in src.branches)
                copy.branches.Add(new TransitionBranch
                {
                    targetStateIndex = b.targetStateIndex,
                    weight = b.weight
                });
            states.Add(copy);
        }
    }
}
