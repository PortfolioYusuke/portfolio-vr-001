# DemoBuild — 体験版 / 製品版の二重ビルド設計

## 役割

同一コードベースから **体験版（無料配布）と製品版（有料）の2バリアント**を
ビルドする仕組み。コンパイル時定義 `DEMO_BUILD` とランタイム参照される
`DemoConfig` ScriptableObject の組合せで、

- 機能制限（一部タブ封印、特定背景のみ許可、主パラメータ上限0.84）
- 時間制限（15分でエンド画面）
- スプラッシュ表示

を統一的に制御する。**製品版コードベースに何も足さずに体験版を成立させる**
のが設計目標。

---

## 採用したパターン

### 1. 二段階の判定（コンパイル定義 + SOフラグ）

```csharp
public static bool IsDemo =>
#if UNITY_EDITOR
    Instance != null && Instance.isDemoMode;     // エディタはSOフラグ
#elif DEMO_BUILD
    true;                                        // 体験版ビルドは常にtrue
#else
    false;                                       // 製品版ビルドは常にfalse
#endif
```

- **エディタ**: `DemoConfig.asset` の `isDemoMode` チェックボックスで
  両モードをホットスワップ可能（開発・QA時の利便性）
- **ビルド時**: `Player Settings → Scripting Define Symbols` に
  `DEMO_BUILD` を入れると、SOの値に関わらずデモモード固定
- → **ビルド成果物のアセット差替で体験版バイパス不可**（セキュリティ）

### 2. SOによる「何を制限するか」のデータ駆動

```csharp
[CreateAssetMenu(fileName = "DemoConfig", menuName = "Demo/Demo Config")]
public class DemoConfig : ScriptableObject
{
    public bool isDemoMode = false;

    [Header("背景制限")]
    public string allowedBackgroundLabel = "Lobby";

    [Header("UI封印")]
    public bool lockCharacterTab = true;
    public bool lockMotionTab = true;
    public bool lockLightingControl = true;
    public bool lockBackgroundSelection = true;
    public bool lockEnvironmentPreset = true;

    [Header("時間制限")]
    public float demoDurationSeconds = 900f;     // 15分

    [Header("スプラッシュ")]
    public string splashMessage = "これは体験版です";
    public float splashDurationSeconds = 3f;

    [Header("終了画面")]
    [TextArea(5, 15)]
    public string endScreenText = "...";
}
```

「何を制限するか」が**SOで設定可能**。例えば「特定の背景のみ許可」を
別な背景に切り替えたければ SO の `allowedBackgroundLabel` を書き換えるだけ。
コード変更不要。

### 3. 共通ロック UI コンポーネント

```csharp
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
        // 1. CanvasGroup で子のSelectable全部を無効化
        var cg = GetComponent<CanvasGroup>() ?? gameObject.AddComponent<CanvasGroup>();
        cg.interactable = false;
        cg.blocksRaycasts = true;

        // 2. フルサイズの暗いオーバーレイを最前面に追加
        var overlay = new GameObject("_DemoLockOverlay");
        overlay.transform.SetParent(transform, false);
        var rect = overlay.AddComponent<RectTransform>();
        rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero; rect.offsetMax = Vector2.zero;
        var img = overlay.AddComponent<Image>();
        img.color = DemoConfig.Instance.lockedColor;
        img.raycastTarget = true;  // クリックを吸収（多重防衛）
    }
}
```

- `gameObject.AddComponent<DemoLockableUI>()` で**動的に**封印を貼る
- 製品版では何もしないので、製品版ビルドへの侵襲ゼロ
- **3重ガード**: CanvasGroup 無効化 + 暗いオーバーレイ + raycastTarget吸収

### 4. プリセット復元の二重ロック

体験版の「環境プリセット」UIを単に隠すだけでは、製品版のセーブデータを
体験版に持ち込まれた場合に、**ApplyPresetToScene** 経由で照明・明るさが
バイパスされうる。

そのためアプリ起動時に：
- **UIごと SetActive(false)** で機能起動経路を断つ
- かつ **背景マネージャ側で `backgrounds` リストを許可ラベル1個に絞る**

の二重防衛を採用した。詳細は同ディレクトリの抜粋コードを参照。

---

## 採用しなかった選択肢

### A. アセット差替方式（DemoConfig.asset を体験版用に差替）

製品版コードはそのまま、`DemoConfig.asset` の `isDemoMode = true` 版を
体験版用フォルダに置き、ビルド時に上書きする方式。

**棄却理由**:
- ビルド成果物の SO アセットを書き換えれば体験版バイパスが可能
  （ZIP内のアセットを差し替えれば製品版相当に）
- Unity の `Resources.Load` は **アセット内容そのもの**をビルド時固定するわけではなく、
  ファイルとしてバンドルされる
- `#if DEMO_BUILD` のコンパイル定義方式は**ビルド時に分岐コードが消える**ため、
  製品版バイナリには `IsDemo = true` のパスが**そもそも存在しない**

### B. 別プロジェクトに分割

体験版用のリポジトリと製品版用のリポジトリを別々に持つ案。

**棄却理由**:
- 製品版のバグ修正を体験版にも反映する手間が発生する
- コードの逐次比較が困難
- セーブデータ等の互換性管理が複雑化

### C. AssetBundle で機能をDLC化

体験版は基本機能のみ、製品版で AssetBundle 配信。

**棄却理由**:
- 個人開発で AssetBundle のホスティング・配信インフラを持つコストが高い
- 体験版/製品版を「別バイナリ配信」するシンプルさを優先

---

## 解決した課題

### 製品版コードへの侵襲ゼロ

`DemoConfig.IsDemo` で分岐するコードは数箇所のみ：

```csharp
// 主パラメータ上限
parameterSlider.maxValue = DemoConfig.IsDemo ? 0.84f : 1f;

// 背景フィルタ
if (DemoConfig.IsDemo)
{
    var allowed = DemoConfig.Instance.allowedBackgroundLabel;
    backgrounds = backgrounds.Where(b => b.label == allowed).ToList();
}

// UI封印
if (DemoConfig.IsDemo && cfg.lockEnvironmentPreset)
    presetContainer.gameObject.SetActive(false);
```

製品版機能本体には1行も体験版コードが入らない。
**製品版を作る → DemoConfig分岐を足す → 体験版完成**という追加方式。

### バイパス困難な体験版

ビルド時のコンパイル分岐により、製品版バイナリには「体験版モード」コードが
**そもそも含まれない**。SO差替では破れない。

### QA 効率

エディタでは `DemoConfig.asset` のチェックボックスで瞬時にモード切替できるため、
1日に何度も体験版/製品版の動作確認をするテストフローが軽快。

---

## コード構成

- **`DemoConfig.cs`** — SOアセット + `IsDemo` static判定
- **`DemoLockableUI.cs`** — UI動的封印コンポーネント
- **`DemoController.cs`** — スプラッシュ・15分タイマー・終了画面の統合制御（`[RuntimeInitializeOnLoadMethod]` で自動生成）

---

## 既知の制約・トレードオフ

- **`DemoConfig.IsDemo` がコードベース全体に散在**
  - 内容：制限ロジックが `if (IsDemo)` で点在
  - 対処：影響範囲が分かるよう grep 監査を CI に組み込み済（手動運用）
- **エディタ操作中のモード混乱**
  - 内容：`isDemoMode` チェック忘れで「動作確認したつもりが製品版モード」
  - 対処：エディタ起動時にコンソールにログ出力
- **新機能追加時の体験版考慮漏れ**
  - 内容：機能追加時、体験版でロックすべきか判断必要
  - 対処：リリース前に「体験版QA監査」を1パスとして必須化（v1.0.2 で発見した照明バイパス経路）

---

## 関連システム

- **[VRWorldSpaceUI](../VRWorldSpaceUI/README.md)** — `DemoLockableUI` をUI要素に動的アタッチ
- **[GameEventHub](../GameEventHub/README.md)** — タイマー満了時に `OnDemoEnded` 発火（予定）

---

## 抜粋コード

- `DemoConfig.cs`
- `DemoLockableUI.cs`
- `DemoController.cs` 抜粋（タイマー＋終了画面部分のみ）
