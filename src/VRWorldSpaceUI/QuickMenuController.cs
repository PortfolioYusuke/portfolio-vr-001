using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// 手の上に表示される VR ショートカットメニュー。
/// グリップ等のトリガで開かれた瞬間の手の位置に追従する（常時固定ではなく、
/// 開かれた手の上に出てくる形）。
///
/// 設計のポイント：
/// - <see cref="GameEventHub.OnQuickMenuToggle"/> で「どの手で開いたか」を受け取る
/// - 開かれた手を <c>_attachedHand</c> として記憶し、Update で追従
/// - ボタンの ON/OFF 状態は SSOT（<see cref="ViewFollowModeState"/> 等）から読み取る
/// </summary>
public class QuickMenuController : MonoBehaviour
{
    [SerializeField] private Transform leftHand;
    [SerializeField] private Transform rightHand;

    [Header("追従オフセット")]
    [SerializeField] private Vector3 offset = new Vector3(0, 0.05f, 0.1f);
    [SerializeField] private Vector3 rotationOffset;

    [Header("ボタン")]
    [SerializeField] private Button[] toggleButtons; // 6個

    private Transform _attachedHand;
    private bool _isVisible = false;

    void Start()
    {
        gameObject.SetActive(false);

        // ボタンクリックイベント
        for (int i = 0; i < toggleButtons.Length; i++)
        {
            int captured = i;
            if (toggleButtons[i] != null)
                toggleButtons[i].onClick.AddListener(() => OnToggleClicked(captured));
        }

        // EventHub 経由の表示要求
        GameEventHub.OnQuickMenuToggle += HandleToggleRequest;
        // SSOT 状態変化の監視（UI 表示と同期）
        ViewFollowModeState.OnChanged += OnViewFollowModeChanged;
    }

    void OnDestroy()
    {
        GameEventHub.OnQuickMenuToggle -= HandleToggleRequest;
        ViewFollowModeState.OnChanged -= OnViewFollowModeChanged;
    }

    void Update()
    {
        if (!_isVisible || _attachedHand == null) return;
        // 開かれた手の上に追従
        transform.position = _attachedHand.position + _attachedHand.TransformDirection(offset);
        transform.rotation = _attachedHand.rotation * Quaternion.Euler(rotationOffset);
    }

    private void HandleToggleRequest(bool show, Transform hand)
    {
        _isVisible = show;
        _attachedHand = hand;
        gameObject.SetActive(show);
        if (show) RefreshButtonStates();
    }

    /// <summary>ボタンの選択状態を SSOT から読み戻す。</summary>
    private void RefreshButtonStates()
    {
        // 例: 視点追従モードボタンの ON/OFF 表示
        // toggleButtons[5] が「視点追従」だとすると：
        if (toggleButtons.Length > 5 && toggleButtons[5] != null)
            UpdateButtonVisual(toggleButtons[5], ViewFollowModeState.Enabled);
    }

    private void UpdateButtonVisual(Button btn, bool isOn)
    {
        var img = btn.GetComponent<Image>();
        if (img != null)
            img.color = isOn ? new Color(0.3f, 0.6f, 0.9f) : new Color(0.3f, 0.3f, 0.35f);
    }

    private void OnToggleClicked(int index)
    {
        switch (index)
        {
            case 0: GameEventHub.OnViewResetRequest?.Invoke(); break;
            case 1: GameEventHub.OnNextStateRequest?.Invoke(); break;
            case 2: GameEventHub.OnSpeedResetRequest?.Invoke(); break;
            case 3: GameEventHub.OnToggleUI?.Invoke(true); break;
            // case 4: 他機能
            case 5: ViewFollowModeState.Toggle(); break;
        }
    }

    private void OnViewFollowModeChanged(bool isOn)
    {
        if (toggleButtons.Length > 5 && toggleButtons[5] != null)
            UpdateButtonVisual(toggleButtons[5], isOn);
    }
}
