using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// 管理パネルのタブ切替・表示制御を担うコントローラ。
///
/// 設計のポイント：
/// - 表示時は <see cref="AdminPanelBase.OnRefresh"/> で値の再同期のみ
///   （重い初期化は <see cref="AdminPanelBase.OnOpen"/> で1度だけ）
/// - 非表示時は <c>gameObject.SetActive(false)</c> で完全停止
///   → <c>Update()</c> が走らないため、Slider 値で state を上書きする事故を防止
/// </summary>
public class AdminPanelController : MonoBehaviour
{
    [SerializeField] private CanvasGroup canvasGroup;
    [SerializeField] private List<AdminPanelBase> pages = new List<AdminPanelBase>();
    [SerializeField] private RectTransform tabContainer;

    private int _currentIndex = -1;
    private bool _isVisible = false;

    void Start()
    {
        // 全ページの GameObject は閉じた状態で開始
        foreach (var page in pages)
        {
            if (page != null) page.gameObject.SetActive(false);
        }

        // 最初のページを開いておく
        if (pages.Count > 0) ShowPage(0);

        // 起動時はパネル全体は非表示
        ToggleVisibility(false);
    }

    /// <summary>外部（QuickMenu等）からパネル全体の表示切替を要求する。</summary>
    public void TogglePanel()
    {
        ToggleVisibility(!_isVisible);
    }

    /// <summary>指定インデックスのタブを表示する。</summary>
    public void ShowPage(int index)
    {
        if (index < 0 || index >= pages.Count) return;

        // 体験版モードでロックされているタブはスキップ
        if (IsTabLocked(index)) return;

        // 旧ページを閉じる
        if (_currentIndex >= 0 && _currentIndex < pages.Count)
        {
            var oldPage = pages[_currentIndex];
            if (oldPage != null) oldPage.OnClose();
        }

        _currentIndex = index;
        var newPage = pages[_currentIndex];
        if (newPage != null) newPage.OnOpen();
    }

    /// <summary>体験版モードで指定タブがロックされているかを判定。</summary>
    private bool IsTabLocked(int index)
    {
        if (!DemoConfig.IsDemo) return false;
        var cfg = DemoConfig.Instance;
        if (cfg == null) return false;

        var name = pages[index].GetType().Name;
        if (name.Contains("Character") && cfg.lockCharacterTab) return true;
        if (name.Contains("Motion") && cfg.lockMotionTab) return true;
        return false;
    }

    private void ToggleVisibility(bool show)
    {
        _isVisible = show;
        if (canvasGroup == null) return;

        canvasGroup.alpha = show ? 1f : 0f;
        canvasGroup.blocksRaycasts = show;
        canvasGroup.interactable = show;

        // 表示時: 値の同期のみ行う（リスナー再登録はしない）
        // OnOpen ではなく OnRefresh を呼ぶことで、背景再生成や状態リセットを防ぐ
        if (show)
        {
            if (_currentIndex >= 0 && _currentIndex < pages.Count)
            {
                pages[_currentIndex].gameObject.SetActive(true);
                pages[_currentIndex].OnRefresh();
            }
            UnityEngine.UI.LayoutRebuilder.ForceRebuildLayoutImmediate(tabContainer);
        }
        else
        {
            // 非表示時: ページを非アクティブにして Update() を停止する
            // これにより、状態をUI値で上書きし続けるのを防ぐ
            if (_currentIndex >= 0 && _currentIndex < pages.Count)
            {
                pages[_currentIndex].gameObject.SetActive(false);
            }
        }
    }
}
