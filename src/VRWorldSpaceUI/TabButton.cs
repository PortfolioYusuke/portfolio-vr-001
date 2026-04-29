using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// タブUI 1個分。クリックされると <see cref="AdminPanelController.ShowPage"/> を呼んで
/// 対応ページを表示させる。
/// </summary>
[RequireComponent(typeof(Button))]
public class TabButton : MonoBehaviour
{
    [SerializeField] private AdminPanelController controller;
    [SerializeField] private int pageIndex;
    [SerializeField] private TextMeshProUGUI label;

    [Header("選択状態")]
    [SerializeField] private Image background;
    [SerializeField] private Color selectedColor = new Color(0.3f, 0.5f, 0.8f, 1f);
    [SerializeField] private Color normalColor = new Color(0.2f, 0.2f, 0.25f, 0.7f);

    void Start()
    {
        var btn = GetComponent<Button>();
        btn.onClick.AddListener(OnClick);
    }

    private void OnClick()
    {
        if (controller != null) controller.ShowPage(pageIndex);
    }

    /// <summary>選択状態（背景色）を切替える。AdminPanelController から呼ばれる想定。</summary>
    public void SetSelected(bool selected)
    {
        if (background != null)
            background.color = selected ? selectedColor : normalColor;
    }

    /// <summary>ラベル文字列を更新する（言語切替等に対応）。</summary>
    public void SetLabel(string text)
    {
        if (label != null) label.text = text;
    }
}
