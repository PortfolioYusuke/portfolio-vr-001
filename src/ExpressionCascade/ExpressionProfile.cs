using UnityEngine;
using Sirenix.OdinInspector;

/// <summary>
/// キャラ1体分の表情マッピング。
/// パラメータ値レンジごとに候補プリセットを設定し、<see cref="ExpressionController"/> が参照する。
/// </summary>
[CreateAssetMenu(fileName = "NewExpressionProfile", menuName = "ExpressionSystem/Expression Profile")]
public class ExpressionProfile : ScriptableObject
{
    [Title("表情マッピング")]
    [Tooltip("パラメータ値レンジごとの表情候補。上から順に評価され、最初にマッチしたものが使われる")]
    [InfoBox("Min/Max が重なる場合、上の行が優先されます")]
    public ExpressionMapping[] mappings = new ExpressionMapping[0];

    [System.Serializable]
    public class ExpressionMapping
    {
        [Tooltip("この帯の名前（表示用）")]
        public string label = "";

        [Tooltip("パラメータ値の下限（0-1）")]
        [Range(0f, 1f)]
        [LabelText("Min")]
        public float parameterMin;

        [Tooltip("パラメータ値の上限（0-1）")]
        [Range(0f, 1f)]
        [LabelText("Max")]
        public float parameterMax = 1f;

        [Tooltip("このレンジで使用する表情候補（ランダム抽選）")]
        [LabelText("表情候補")]
        public ExpressionPreset[] candidates = new ExpressionPreset[0];

        [Tooltip("表情の切替間隔（秒）。この間隔で candidates から再抽選する")]
        [Range(3f, 60f)]
        [LabelText("切替間隔（秒）")]
        public float switchInterval = 15f;
    }

    /// <summary>指定パラメータ値に該当するマッピングを返す。見つからなければ null。</summary>
    public ExpressionMapping GetMapping(float parameterLevel)
    {
        for (int i = 0; i < mappings.Length; i++)
        {
            if (parameterLevel >= mappings[i].parameterMin && parameterLevel <= mappings[i].parameterMax)
                return mappings[i];
        }
        return null;
    }

    /// <summary>ラベル名でマッピングを検索する（特殊状態時の強制遷移等）。</summary>
    public ExpressionMapping GetMappingByLabel(string label)
    {
        for (int i = 0; i < mappings.Length; i++)
        {
            if (mappings[i].label == label)
                return mappings[i];
        }
        return null;
    }
}
