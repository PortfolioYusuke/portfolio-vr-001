using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// パラメータ調整タブ。<see cref="AdminPanelBase"/> 派生で、
/// 主要 Slider と View Follow Mode トグルを管理する。
///
/// このページは <see cref="AdminPanelBase"/> の3段階ライフサイクル
/// （OnOpen / OnRefresh / OnClose）と SSOT パターンの実装例として配置している。
/// 実装版ではこのほかにも複数の Slider / Toggle / 状態テキストが連動する。
/// </summary>
public class Page_Parameters : AdminPanelBase
{
    [Header("Sliders")]
    [SerializeField] private Slider parameterSlider;        // 主パラメータ
    [SerializeField] private Slider sensitivitySlider;
    [SerializeField] private Slider autonomySlider;
    [SerializeField] private Slider bodyTransparencySlider;

    [Header("Toggles")]
    [SerializeField] private Toggle viewFollowToggle;       // SSOT 共有

    [Header("Status")]
    [SerializeField] private TextMeshProUGUI statusText;

    private ParameterStateManager _manager;
    private AutonomyHub _autonomyHub;

    // ===========================
    // 1. ライフサイクル
    // ===========================

    public override void OnOpen()
    {
        base.OnOpen();

        _manager = SessionManager.Instance?.ActiveParameter;
        _autonomyHub = FindObjectOfType<AutonomyHub>(includeInactive: true);

        // Slider 初期表示は SetValueWithoutNotify（onValueChanged を発火させない）
        if (parameterSlider != null)
        {
            parameterSlider.maxValue = DemoConfig.IsDemo ? 0.84f : 1f;
            parameterSlider.SetValueWithoutNotify(_manager != null ? _manager.CurrentValue : 0.5f);
        }

        if (sensitivitySlider != null)
            sensitivitySlider.SetValueWithoutNotify(_manager != null ? _manager.Sensitivity : 0.75f);

        if (autonomySlider != null && _autonomyHub != null)
            autonomySlider.SetValueWithoutNotify(_autonomyHub.autonomyLevel);

        // 視点追従モード（SSOT: ViewFollowModeState）
        if (viewFollowToggle != null)
        {
            viewFollowToggle.SetIsOnWithoutNotify(ViewFollowModeState.Enabled);
            viewFollowToggle.onValueChanged.RemoveAllListeners();
            viewFollowToggle.onValueChanged.AddListener(ViewFollowModeState.Set);
        }
        ViewFollowModeState.OnChanged -= OnViewFollowModeChanged;
        ViewFollowModeState.OnChanged += OnViewFollowModeChanged;
    }

    public override void OnClose()
    {
        base.OnClose();
        ViewFollowModeState.OnChanged -= OnViewFollowModeChanged;
    }

    /// <summary>UI 再表示時：外部で変更された値をリスナー発火なしに同期する。</summary>
    public override void OnRefresh()
    {
        if (_manager != null && parameterSlider != null)
            parameterSlider.SetValueWithoutNotify(_manager.CurrentValue);
        if (_manager != null && sensitivitySlider != null)
            sensitivitySlider.SetValueWithoutNotify(_manager.Sensitivity);
        if (_autonomyHub != null && autonomySlider != null)
            autonomySlider.SetValueWithoutNotify(_autonomyHub.autonomyLevel);
        if (viewFollowToggle != null)
            viewFollowToggle.SetIsOnWithoutNotify(ViewFollowModeState.Enabled);
    }

    // ===========================
    // 2. SSOT 連動
    // ===========================

    private void OnViewFollowModeChanged(bool enabled)
    {
        // 他UI（QuickMenu等）から状態が変わった時、自分のトグルも自動同期
        if (viewFollowToggle != null)
            viewFollowToggle.SetIsOnWithoutNotify(enabled);
    }

    // ===========================
    // 3. プリセット復元用 公開API
    // ===========================

    public void SetBodyTransparency(float t)
    {
        float v = Mathf.Clamp01(t);
        if (bodyTransparencySlider != null)
            bodyTransparencySlider.SetValueWithoutNotify(v);
        // タブが非アクティブでも即時にマテリアルへ反映する
        ApplyBodyTransparencyToMaterial(v);
    }

    private void ApplyBodyTransparencyToMaterial(float transparency)
    {
        // 詳細実装はポートフォリオでは省略。
        // MaterialPropertyBlock 経由で SMR 単位に当てる方式で、共有マテリアルを汚染しない。
    }
}
