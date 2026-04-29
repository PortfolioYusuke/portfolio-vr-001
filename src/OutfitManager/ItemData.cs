using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// 1つの装備アイテム（衣装・髪・アクセサリ等）のデータ。
/// <see cref="OutfitManager.Equip"/> はこの SO を渡すだけで装着できる。
/// </summary>
[CreateAssetMenu(fileName = "NewItemData", menuName = "OutfitSystem/ItemData")]
public class ItemData : ScriptableObject
{
    public string itemName;
    public GameObject prefab;
    public CharacterProfile.BoneTarget attachTo;
    public string rootBoneName;

    [Header("Transform Settings")]
    public Vector3 positionOffset;
    public Vector3 rotationOffset;

    [Header("Mesh Settings")]
    public List<string> ignoreMeshNames;

    [Header("Material Variants")]
    [Tooltip("マテリアルバリエーションのフォルダパス（複数可）")]
    public List<string> materialFolders;

    [Header("表示名設定")]
    [Tooltip("マテリアル名 → 表示名")]
    public List<NamedEntry> materialDisplayNames;

    [Tooltip("メッシュ名 → 表示名")]
    public List<NamedEntry> meshDisplayNames;

    public ItemCategory category;
}

/// <summary>キーと表示名のペア（Inspector で並べて設定可能）。</summary>
[System.Serializable]
public class NamedEntry
{
    [Tooltip("内部名（マテリアル名、メッシュ名等）")]
    public string key;
    [Tooltip("UI 上に表示する名前")]
    public string displayName;
}

public enum ItemCategory
{
    Hair,
    Headwear,
    Top,
    Bottom,
    Innerwear,
    Accessory
}
