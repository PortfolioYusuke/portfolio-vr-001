using UnityEngine;
using TMPro;
using System.Collections;

/// <summary>
/// 体験版モードの統合管理: スプラッシュ → 制限プレイ → 時間満了で終了画面の流れ。
/// シーンロード後に <see cref="RuntimeInitializeOnLoadMethodAttribute"/> で
/// 自動生成される。製品版モード時 (<see cref="DemoConfig.IsDemo"/> == false) は何もしない。
///
/// VR対応: World Space Canvas を Camera.main の子として配置し、HMD の動きに追従する
/// （詳細な Canvas 生成コードはポートフォリオでは省略）。
/// </summary>
public class DemoController : MonoBehaviour
{
    private static DemoController _instance;
    public static DemoController Instance => _instance;

    private float _startTime;
    private bool _ended = false;
    private bool _splashing = true;

    // World Space Canvas 群（生成コードは省略）
    private Canvas _splashCanvas;
    private Canvas _hudCanvas;
    private TextMeshProUGUI _hudText;
    private Canvas _mainTimerCanvas;
    private TextMeshProUGUI _mainTimerText;
    private Canvas _endCanvas;

    /// <summary>残り時間がこの秒数以下になったら HUD を出す。</summary>
    private const float HUD_WARN_THRESHOLD = 180f;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void AutoInitialize()
    {
        if (!DemoConfig.IsDemo) return;
        if (_instance != null) return;
        var go = new GameObject("_DemoController");
        go.AddComponent<DemoController>();
        DontDestroyOnLoad(go);
    }

    void Awake()
    {
        if (_instance != null && _instance != this) { Destroy(gameObject); return; }
        _instance = this;
    }

    void Start()
    {
        if (!DemoConfig.IsDemo) { gameObject.SetActive(false); return; }

        _startTime = Time.time;
        CreateAllCanvases();
        StartCoroutine(SplashRoutine());

        GameEventHub.OnToggleUI += OnAdminPanelToggle;
    }

    void OnDestroy()
    {
        GameEventHub.OnToggleUI -= OnAdminPanelToggle;
    }

    void Update()
    {
        if (_ended || _splashing) return;

        var cfg = DemoConfig.Instance;
        float elapsed = Time.time - _startTime;
        float remaining = Mathf.Max(0f, cfg.demoDurationSeconds - elapsed);

        // メインタイマー（管理パネル開時のみ表示）
        if (_mainTimerCanvas != null && _mainTimerCanvas.gameObject.activeSelf && _mainTimerText != null)
            _mainTimerText.text = "体験版残り " + FormatTime(remaining);

        // 警告HUD（残り3分以下で表示）
        bool showHud = remaining <= HUD_WARN_THRESHOLD;
        if (_hudCanvas != null && _hudCanvas.gameObject.activeSelf != showHud)
            _hudCanvas.gameObject.SetActive(showHud);
        if (showHud && _hudText != null)
            _hudText.text = "残り " + FormatTime(remaining);

        // 時間切れ判定
        if (remaining <= 0f)
        {
            _ended = true;
            ShowEndScreen();
        }
    }

    private void OnAdminPanelToggle(bool open)
    {
        if (_mainTimerCanvas != null && !_ended && !_splashing)
            _mainTimerCanvas.gameObject.SetActive(open);
    }

    private static string FormatTime(float seconds)
    {
        int m = Mathf.FloorToInt(seconds / 60f);
        int s = Mathf.FloorToInt(seconds % 60f);
        return string.Format("{0:00}:{1:00}", m, s);
    }

    private IEnumerator SplashRoutine()
    {
        yield return new WaitForSeconds(DemoConfig.Instance.splashDurationSeconds);
        if (_splashCanvas != null) Destroy(_splashCanvas.gameObject);
        _splashing = false;
    }

    /// <summary>時間切れ時に終了画面 Canvas を表示し、ゲームをロックする。</summary>
    private void ShowEndScreen()
    {
        // 既存 Canvas を全部隠す（プレイ続行不可にする）
        if (_mainTimerCanvas != null) _mainTimerCanvas.gameObject.SetActive(false);
        if (_hudCanvas != null) _hudCanvas.gameObject.SetActive(false);

        // 終了画面 Canvas を表示（生成は CreateAllCanvases 側で済んでいる前提）
        if (_endCanvas != null) _endCanvas.gameObject.SetActive(true);

        // ゲーム時間を停止（必要に応じて）
        Time.timeScale = 0f;
    }

    /// <summary>
    /// World Space Canvas 群（スプラッシュ / HUD / メインタイマー / 終了画面）を生成する。
    /// 詳細実装はポートフォリオでは省略。
    /// </summary>
    private void CreateAllCanvases()
    {
        // 元実装ではここで4つの Canvas を Camera.main 配下に配置：
        //
        //   _splashCanvas      — 中央 (Vector3(0, 0, 1.5f))、起動時のみ
        //   _hudCanvas         — 視界右下、残り3分以下で表示（Raycaster なし＝操作不要）
        //   _mainTimerCanvas   — 管理パネル開時のみ表示
        //   _endCanvas         — 時間切れ時のみ表示
        //
        // それぞれ TextMeshProUGUI で残り時間 / メッセージを描画。
    }
}
