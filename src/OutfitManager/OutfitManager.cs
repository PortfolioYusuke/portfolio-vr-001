using UnityEngine;
using System.Collections.Generic;
using MagicaCloth2;

/// <summary>
/// 衣装・髪型をランタイムで動的に着脱するシステム。
/// prefab のインスタンス化、ボーン再ペアレント、MagicaCloth2 のコライダーセットアップ、
/// テクスチャ swap までを <see cref="Equip"/> 一発で実現する。
///
/// 重要な設計ポイント：
/// - **MagicaCloth2 の遅延コライダー対応**: prefab を非アクティブで Instantiate
///   → コライダー設定 → 最後にアクティブ化、の順序を厳守
///   （アクティブ化で MagicaCloth が内部状態をビルドするため、コライダーが事前に必要）
/// - **ボーンリーク対策**: 衣装 prefab にキャラと同名ボーンが含まれる場合、
///   ユニークボーンだけを移植し、それ以外は SkinnedMeshRenderer.bones[] を
///   キャラ側 Transform に再配線する
/// - **共有マテリアル汚染回避**: <c>renderer.material</c> 経由で自動 instance 化
///
/// このファイルはポートフォリオ用にコア部分のみ抜粋した版です。
/// </summary>
public class OutfitManager : MonoBehaviour
{
    [SerializeField] private CharacterProfile profile;
    [SerializeField] private List<ColliderComponent> characterColliders;

    /// <summary>装着中のアイテムを ItemCategory 単位で追跡。</summary>
    private Dictionary<ItemCategory, GameObject> _equippedItems = new Dictionary<ItemCategory, GameObject>();

    /// <summary>カテゴリごとに移植したボーンを追跡（Unequip時にだけそのカテゴリ分を掃除する）。</summary>
    private Dictionary<ItemCategory, List<Transform>> _portedBones = new Dictionary<ItemCategory, List<Transform>>();

    public delegate void EquipChangedHandler(ItemCategory category);
    public event EquipChangedHandler OnEquipChanged;

    // ===========================
    // 1. 装着
    // ===========================

    public void Equip(ItemData data)
    {
        Unequip(data.category);
        if (data.prefab == null) return;

        // === MagicaCloth2 の遅延コライダー対応：非アクティブで Instantiate ===
        bool prefabWasActive = data.prefab.activeSelf;
        data.prefab.SetActive(false);
        GameObject instance = Instantiate(data.prefab);
        data.prefab.SetActive(prefabWasActive);
        instance.name = $"Temp_{data.itemName}";

        GameObject container = new GameObject($"Container_{data.itemName}");
        container.transform.SetParent(this.transform);

        if (data.attachTo == CharacterProfile.BoneTarget.FullBody)
        {
            // FullBody は SMR の bones[] をキャラ側に付け替える
            var ported = PortUniqueBones(instance);
            _portedBones[data.category] = ported;
            RemapFullBody(instance, container, data);
            _equippedItems[data.category] = container;

            SetupMagicaColliders(container, true);
            foreach (var root in ported)
                if (root != null) SetupMagicaColliders(root.gameObject, true);

            // FullBody: container をアクティブ化してから instance を破棄
            container.SetActive(true);
            Destroy(instance);
        }
        else
        {
            // 部分装着（髪・帽子等）：指定ボーンに付ける
            var reparentedRoot = AttachAsPart(instance, container, data);
            _equippedItems[data.category] = instance;
            container.transform.SetParent(instance.transform);
            if (reparentedRoot != null)
                _portedBones[data.category] = new List<Transform> { reparentedRoot };

            SetupMagicaColliders(instance, true);
            instance.SetActive(true);
        }

        OnEquipChanged?.Invoke(data.category);
    }

    public void Unequip(ItemCategory category)
    {
        if (_equippedItems.TryGetValue(category, out var existing) && existing != null)
        {
            Destroy(existing);
            _equippedItems.Remove(category);
        }

        // そのカテゴリで移植したボーンだけを破棄（他カテゴリ分は残す）
        if (_portedBones.TryGetValue(category, out var ported))
        {
            foreach (var t in ported)
                if (t != null) Destroy(t.gameObject);
            _portedBones.Remove(category);
        }
    }

    // ===========================
    // 2. ボーンリーク対策（PortUniqueBones）
    // ===========================

    /// <summary>
    /// 衣装 prefab 内のユニークボーン（キャラ側に存在しない名前のボーン）だけを
    /// キャラのアーマチュアに移植する。それ以外のボーンは <see cref="SkinnedMeshRenderer.bones"/>
    /// 配列をキャラ側 Transform に再配線する。
    ///
    /// これにより「同名ボーンが衣装着替えのたびに増殖し、キャラの足元・腰回りに
    /// 透明ボーンが蓄積する」現象を防ぐ。
    /// </summary>
    private List<Transform> PortUniqueBones(GameObject instance)
    {
        var ported = new List<Transform>();
        var charBones = profile.GetAllBonesDict();

        foreach (var smr in instance.GetComponentsInChildren<SkinnedMeshRenderer>())
        {
            var newBones = new Transform[smr.bones.Length];
            for (int i = 0; i < smr.bones.Length; i++)
            {
                var origName = smr.bones[i] != null ? smr.bones[i].name : "";
                if (charBones.TryGetValue(origName, out var charBone))
                {
                    newBones[i] = charBone;          // キャラ側に存在 → 流用
                }
                else
                {
                    ported.Add(smr.bones[i]);        // 衣装独自 → 移植対象
                    newBones[i] = smr.bones[i];
                }
            }
            smr.bones = newBones;
        }
        return ported;
    }

    /// <summary>FullBody 装着時のリマップ実装。詳細はポートフォリオでは省略。</summary>
    private void RemapFullBody(GameObject instance, GameObject container, ItemData data)
    {
        // SMR を container に移植し、bones[] をキャラ側に付け替える
        // ignoreMeshNames に該当する SMR はスキップする
    }

    /// <summary>部分装着用の Attach 実装。詳細はポートフォリオでは省略。</summary>
    private Transform AttachAsPart(GameObject instance, GameObject container, ItemData data)
    {
        var anchor = profile.GetBone(data.attachTo);
        if (anchor == null) return null;
        instance.transform.SetParent(anchor, false);
        instance.transform.localPosition = data.positionOffset;
        instance.transform.localEulerAngles = data.rotationOffset;
        return instance.transform;
    }

    // ===========================
    // 3. MagicaCloth コライダー連携
    // ===========================

    /// <summary>
    /// 装着中の衣装に <see cref="MagicaCloth"/> がある場合、キャラ側のコライダーを
    /// 自動的にコリジョンソースに登録する。MagicaCloth はアクティブ化で
    /// 内部状態をビルドするため、ビルド前にこれを完了させる必要がある。
    /// </summary>
    private void SetupMagicaColliders(GameObject targetRoot, bool deferBuild)
    {
        var clothComponents = targetRoot.GetComponentsInChildren<MagicaCloth>(true);
        foreach (var cloth in clothComponents)
        {
            // characterColliders をコライダーリストに追加して再構築
            // 詳細はポートフォリオでは省略
        }
    }

    // ===========================
    // 4. テクスチャ swap（髪色変更等）
    // ===========================

    /// <summary>
    /// 装着中アイテムのマテリアルテクスチャを差し替える。
    /// <c>renderer.material</c> 経由のため自動で instance 化され、共有 .mat アセットを汚染しない。
    /// </summary>
    public void SwapTexture(ItemCategory category, Texture2D newTexture)
    {
        if (!_equippedItems.TryGetValue(category, out var item) || item == null) return;
        var smr = item.GetComponentInChildren<SkinnedMeshRenderer>();
        if (smr != null)
            smr.material.SetTexture("_BaseMap", newTexture);
    }
}
