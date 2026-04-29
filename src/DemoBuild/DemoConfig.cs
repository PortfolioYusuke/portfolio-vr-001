using UnityEngine;
using TMPro;

/// <summary>
/// 体験版モード設定。Resources/DemoConfig.asset として配置し、
/// 各システム（背景マネージャ / アクションエンジン / UI コントローラ等）が
/// シングルトン経由で参照する。製品版ビルド時は isDemoMode=false にしたアセットに差し替える。
///
/// 体験版判定の二段階：
/// - エディタでは SO のチェックボックスでホットスワップ
/// - ビルド時は `DEMO_BUILD` コンパイル定義で固定（アセット差替バイパス不可）
/// </summary>
[CreateAssetMenu(fileName = "DemoConfig", menuName = "Demo/Demo Config")]
public class DemoConfig : ScriptableObject
{
    [Header("モード")]
    [Tooltip("true で体験版モードが有効。製品版ビルド時は false に")]
    public bool isDemoMode = false;

    [Header("フォント")]
    [Tooltip("体験版コントローラが動的生成する UI で使う日本語フォント")]
    public TMP_FontAsset japaneseFont;

    [Header("背景制限")]
    [Tooltip("体験版で許可する背景ラベル（背景マネージャの背景リストの label と一致させる）")]
    public string allowedBackgroundLabel = "Lobby";

    [Header("UI封印")]
    [Tooltip("Character タブを封印")]
    public bool lockCharacterTab = true;
    [Tooltip("MotionEditor タブを封印")]
    public bool lockMotionTab = true;
    [Tooltip("環境タブの照明UI（カラー・明るさ）を封印")]
    public bool lockLightingControl = true;
    [Tooltip("環境タブの背景選択UIを封印")]
    public bool lockBackgroundSelection = true;
    [Tooltip("環境タブのプリセット保存/復元UIを封印")]
    public bool lockEnvironmentPreset = true;

    [Header("スプラッシュ")]
    [Tooltip("起動時スプラッシュの表示時間（秒）")]
    public float splashDurationSeconds = 3f;
    [TextArea(2, 4)]
    public string splashMessage = "これは体験版です";

    [Header("時間制限")]
    [Tooltip("連続プレイ可能時間（秒）。900 = 15分")]
    public float demoDurationSeconds = 900f;

    [Header("終了画面")]
    [TextArea(5, 15)]
    public string endScreenText =
        "━━━━━━━━━━━━━━━━━━━\n　　　体験版終了\n━━━━━━━━━━━━━━━━━━━\n\n　ご体験いただき\n　　　ありがとうございました。\n\n　続きは製品版にて\n　　　お待ちしております。";

    [Header("封印UIの見た目")]
    [Tooltip("封印された時のUI色（がっつり暗いグレー推奨）")]
    public Color lockedColor = new Color(0.15f, 0.15f, 0.15f, 0.85f);

    // === Singleton（Resources/からロード）===

    private static DemoConfig _instance;
    public static DemoConfig Instance
    {
        get
        {
            if (_instance == null)
                _instance = Resources.Load<DemoConfig>("DemoConfig");
            return _instance;
        }
    }

    /// <summary>
    /// 体験版モードか。
    /// - ビルド時: <c>DEMO_BUILD</c> コンパイル定義で決定（アセット差替バイパス不可）
    /// - エディタ: SO の <see cref="isDemoMode"/> フィールドで切替可能
    /// </summary>
    public static bool IsDemo =>
#if UNITY_EDITOR
        Instance != null && Instance.isDemoMode;
#elif DEMO_BUILD
        true;
#else
        false;
#endif
}
