using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// キャラのボーン階層を抽象化するハブ。<see cref="BoneTarget"/> 列挙値で
/// 「Head ボーンが欲しい」と問い合わせれば、そのキャラのボーン階層から
/// 該当 Transform を返す。
///
/// 命名規則の差（`Head`, `Mannequin_Head`, `Bone.Head` 等）に対応するため、
/// **完全一致を優先しつつ末尾サフィックスでフォールバック**する2パス検索を採用。
/// </summary>
public class CharacterProfile : MonoBehaviour
{
    public enum BoneTarget
    {
        FullBody, Head, Hips, Chest, Spine, LeftHand, RightHand, LeftLowerLeg, RightLowerLeg
    }

    /// <summary>ボーン名 → BoneTarget のマッピング（完全一致用）。</summary>
    private static readonly Dictionary<string, BoneTarget> _exactNameMap = new Dictionary<string, BoneTarget>
    {
        { "Hips", BoneTarget.Hips },
        { "Head", BoneTarget.Head },
        { "Chest", BoneTarget.Chest },
        { "Spine", BoneTarget.Spine },
        { "Hand_L", BoneTarget.LeftHand },
        { "Hand_R", BoneTarget.RightHand },
    };

    /// <summary>EndsWith フォールバック用。</summary>
    private static readonly Dictionary<string, BoneTarget> _suffixMap = new Dictionary<string, BoneTarget>
    {
        { "Hips", BoneTarget.Hips },
        { "Head", BoneTarget.Head },
        { "Chest", BoneTarget.Chest },
        { "Spine", BoneTarget.Spine },
        { "LeftHand", BoneTarget.LeftHand },
        { "RightHand", BoneTarget.RightHand },
    };

    private Dictionary<BoneTarget, Transform> _boneMap = new Dictionary<BoneTarget, Transform>();

    void Awake() => BuildBoneMap();

    public void BuildBoneMap()
    {
        _boneMap.Clear();
        Transform[] allChildren = GetComponentsInChildren<Transform>(true);

        // パス1: 完全一致（最優先。"Head" は "Head" にだけマッチし、"Mannequin_Head" にはマッチしない）
        foreach (var t in allChildren)
        {
            if (_exactNameMap.TryGetValue(t.name, out BoneTarget target))
            {
                if (!_boneMap.ContainsKey(target))
                    _boneMap[target] = t;
            }
        }

        // パス2: EndsWith フォールバック（完全一致で見つからなかった BoneTarget だけ）
        foreach (var t in allChildren)
        {
            foreach (var kvp in _suffixMap)
            {
                if (!_boneMap.ContainsKey(kvp.Value) && t.name.EndsWith(kvp.Key))
                {
                    _boneMap[kvp.Value] = t;
                    break;
                }
            }
        }
    }

    public Transform GetBone(BoneTarget target)
    {
        if (_boneMap.Count == 0) BuildBoneMap();
        return _boneMap.GetValueOrDefault(target);
    }

    /// <summary>全ボーンの名前 → Transform 辞書を構築（FullBody リマップ用）。</summary>
    public Dictionary<string, Transform> GetAllBonesDict()
    {
        var dict = new Dictionary<string, Transform>();
        foreach (var t in GetComponentsInChildren<Transform>(true))
        {
            if (!dict.ContainsKey(t.name)) dict.Add(t.name, t);
        }
        return dict;
    }
}
