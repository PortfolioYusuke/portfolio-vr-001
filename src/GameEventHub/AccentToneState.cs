using System;
using UnityEngine;

/// <summary>
/// キャラクターの色調スライダー値（Gradient で評価する補助色）の SSOT。
/// マテリアル色は Gradient.Evaluate(t) で決まるため、マテリアルの色から
/// t 値を逆算できない。そのため、t 値の真実をこの static class に置く。
///
/// 上記 <see cref="ViewFollowModeState"/> が「単一 bool の SSOT」例であるのに対し、
/// こちらは「複数 float + セーブ復元用 API つき」の例。
///
/// 設計のポイント：
/// - 値は private、Setter で <see cref="Mathf.Clamp01"/> 強制
/// - 「変化がなかったら通知しない」で UI 側のリスナーループを防止
/// - <see cref="RestoreFromSave"/> で全値を一括反映してから OnChanged 1発（部分更新の中間状態を防ぐ）
/// </summary>
public static class AccentToneState
{
    private static float _accentTone = 0f;       // 主トーン（0-1）
    private static float _highlightTone = 0f;    // ハイライト（0-1）
    private static float _layerIntensity = 0.621f; // レイヤー強度（0-1、デフォルト値はマテリアル初期値）

    public static float AccentTone => _accentTone;
    public static float HighlightTone => _highlightTone;
    public static float LayerIntensity => _layerIntensity;

    /// <summary>値変更通知（変更時のみ。ハンドラは必要な値を Getter で読む）。</summary>
    public static event Action OnChanged;

    public static void SetAccentTone(float v)
    {
        v = Mathf.Clamp01(v);
        if (Mathf.Approximately(_accentTone, v)) return;
        _accentTone = v;
        OnChanged?.Invoke();
    }

    public static void SetHighlightTone(float v)
    {
        v = Mathf.Clamp01(v);
        if (Mathf.Approximately(_highlightTone, v)) return;
        _highlightTone = v;
        OnChanged?.Invoke();
    }

    public static void SetLayerIntensity(float v)
    {
        v = Mathf.Clamp01(v);
        if (Mathf.Approximately(_layerIntensity, v)) return;
        _layerIntensity = v;
        OnChanged?.Invoke();
    }

    /// <summary>セーブデータからの一括復元（OnChanged は1回だけ発火）。</summary>
    public static void RestoreFromSave(float accent, float highlight, float layer)
    {
        _accentTone = Mathf.Clamp01(accent);
        _highlightTone = Mathf.Clamp01(highlight);
        _layerIntensity = Mathf.Clamp01(layer);
        OnChanged?.Invoke();
    }
}
