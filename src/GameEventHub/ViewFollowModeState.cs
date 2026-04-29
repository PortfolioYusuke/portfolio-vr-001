using System;

/// <summary>
/// 視点追従モードの状態を保持する SSOT (Single Source of Truth)。
/// 複数のUI（パラメータタブのトグル / QuickMenu のボタン）と、ロジック側の
/// VR視点コントローラがこの static class を参照・購読する。
///
/// 設計のポイント：
/// - 値は private、Setter 経由のみで変更可能
/// - 同じ値を Set しても OnChanged を発火しない（無限ループ防止）
/// - セッション単位の状態（永続化なし）
/// </summary>
public static class ViewFollowModeState
{
    private static bool _enabled = false;

    public static bool Enabled => _enabled;

    /// <summary>状態変更通知（変更があった時のみ発火）。</summary>
    public static event Action<bool> OnChanged;

    public static void Set(bool value)
    {
        if (_enabled == value) return;
        _enabled = value;
        OnChanged?.Invoke(value);
    }

    public static void Toggle() => Set(!_enabled);
}
