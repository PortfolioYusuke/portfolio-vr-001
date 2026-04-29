using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 体験版モード時にこの GameObject の UI を封印する。
/// - <see cref="CanvasGroup"/> で interactable=false（子の Selectable 全部を無効化）
/// - フルサイズの暗いオーバーレイ <see cref="Image"/> を最前面に追加（がっつりグレーアウト）
/// - クリックを吸収して下の UI に通さない（多重防衛）
///
/// <c>AddComponent</c> で動的に貼ることを想定。製品版モードでは何もしない。
/// </summary>
[RequireComponent(typeof(RectTransform))]
public class DemoLockableUI : MonoBehaviour
{
    void Start()
    {
        if (!DemoConfig.IsDemo) return;
        ApplyLock();
    }

    private void ApplyLock()
    {
        // 1. CanvasGroup で子の Selectable を全部無効化
        var cg = GetComponent<CanvasGroup>();
        if (cg == null) cg = gameObject.AddComponent<CanvasGroup>();
        cg.interactable = false;
        cg.blocksRaycasts = true;

        // 2. フルサイズの暗いオーバーレイを最前面に追加
        var overlayGO = new GameObject("_DemoLockOverlay");
        overlayGO.transform.SetParent(transform, false);
        var rect = overlayGO.AddComponent<RectTransform>();
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
        var img = overlayGO.AddComponent<Image>();
        img.color = DemoConfig.Instance.lockedColor;
        img.raycastTarget = true; // クリックを吸収
    }
}
