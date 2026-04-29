using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections.Generic;

/// <summary>
/// UI Dropdown と <see cref="OutfitManager"/> の橋渡し。
/// アイテム候補リストから1つを Dropdown で選ばせ、選択された ItemData を
/// <see cref="OutfitManager.Equip"/> に渡す。
/// </summary>
[RequireComponent(typeof(TMP_Dropdown))]
public class ItemSelector : MonoBehaviour
{
    [SerializeField] private OutfitManager outfitManager;
    [SerializeField] private List<ItemData> items = new List<ItemData>();
    [SerializeField] private string captionLabel = "アイテム";

    [Header("フォントサイズ")]
    [SerializeField] private int captionFontSize = 32;
    [SerializeField] private int itemFontSize = 28;

    private TMP_Dropdown _dropdown;

    void Awake()
    {
        _dropdown = GetComponent<TMP_Dropdown>();
    }

    void Start()
    {
        RefreshUI();
        _dropdown.onValueChanged.AddListener(OnSelectionChanged);
    }

    /// <summary>Dropdown のオプション一覧を items から再構築する。</summary>
    public void RefreshUI()
    {
        _dropdown.ClearOptions();
        var options = new List<TMP_Dropdown.OptionData>();
        options.Add(new TMP_Dropdown.OptionData(captionLabel + " — 未装着"));
        foreach (var item in items)
        {
            if (item == null) continue;
            options.Add(new TMP_Dropdown.OptionData(item.itemName));
        }
        _dropdown.AddOptions(options);

        // フォントサイズを明示上書き（プレハブ依存だと小さいため）
        var captionTmp = _dropdown.captionText;
        if (captionTmp != null) captionTmp.fontSize = captionFontSize;
        if (_dropdown.itemText != null) _dropdown.itemText.fontSize = itemFontSize;
    }

    private void OnSelectionChanged(int index)
    {
        if (outfitManager == null) return;
        if (index <= 0)
        {
            // 「未装着」 → カテゴリの装着を全解除（先頭の items[0] のカテゴリを参照）
            if (items.Count > 0 && items[0] != null)
                outfitManager.Unequip(items[0].category);
            return;
        }

        int itemIndex = index - 1; // captionLabel の分シフト
        if (itemIndex < items.Count && items[itemIndex] != null)
        {
            outfitManager.Equip(items[itemIndex]);
        }
    }
}
