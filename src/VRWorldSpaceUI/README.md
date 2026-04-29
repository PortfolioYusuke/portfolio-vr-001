# VRWorldSpaceUI — VR空間内のWorld Space CanvasベースUI

## 役割

VRヘッドセット内に**3次元空間に浮かぶ**UIパネルを配置し、コントローラの
レーザーポインタや手の指で操作可能にするレイヤー。

タブ切替型のメニュー（環境設定・パラメータ調整等）と、手の上に表示される
QuickMenu の2形態を提供する。

---

## 採用したパターン

### 1. World Space Canvas + TrackedDeviceGraphicRaycaster

```csharp
// Canvas component
canvas.renderMode = RenderMode.WorldSpace;
canvas.sortingOrder = 0;

// 必須: VRコントローラのレイで操作可能にする
var raycaster = canvasGo.AddComponent<TrackedDeviceGraphicRaycaster>();
```

採用理由：
- VR では **Screen Space Overlay は使えない**（HMDのスクリーンとは別空間）
- `TrackedDeviceGraphicRaycaster` (XR Interaction Toolkit) が VR コントローラ
  の Pointer Event を Canvas に流す唯一の標準パス

### 2. AdminPanelBase + ページ別実装

```csharp
public abstract class AdminPanelBase : MonoBehaviour
{
    public virtual void OnOpen()    { gameObject.SetActive(true); }
    public virtual void OnClose()   { gameObject.SetActive(false); }
    public virtual void OnRefresh() { /* タブ切替時の値同期 */ }
}

public class Page_Parameters : AdminPanelBase { /* ... */ }
public class Page_Environment : AdminPanelBase { /* ... */ }
```

ページは抽象基底を継承し、`AdminPanelController` がタブクリックで
ページの `OnOpen` / `OnClose` を切替える。

### 3. 非active時にUpdate停止して状態書き戻しを防止

```csharp
// AdminPanelController
private void ToggleVisibility(bool show)
{
    canvasGroup.alpha = show ? 1f : 0f;
    canvasGroup.blocksRaycasts = show;

    if (show)
    {
        currentPage.gameObject.SetActive(true);
        currentPage.OnRefresh();   // 値を再同期
    }
    else
    {
        // 非表示時はGameObjectごとSetActive(false)
        // → Update() が走らない → スライダー値で state を上書きする事故防止
        currentPage.gameObject.SetActive(false);
    }
}
```

採用理由：
- UI が `Update()` 内で「state ← UI値」を毎フレーム書き戻す設計箇所がある
  ([Page_Parameters](../GameEventHub/README.md#3-ui-が映す窓に徹するための実装規約))
- 非active 時に `SetActive(false)` で **Update() を完全に止める**ことで
  プリセット復元時の値踏み潰しを防止

### 4. QuickMenu の手追従配置

```csharp
public class QuickMenuController : MonoBehaviour
{
    [SerializeField] private Transform leftHand, rightHand;
    private Transform _attachedHand;

    private void Update()
    {
        if (_attachedHand != null)
        {
            transform.position = _attachedHand.position + offset;
            transform.rotation = _attachedHand.rotation * rotationOffset;
        }
    }
}
```

QuickMenu はトリガで開かれた手の上に表示。常時固定ではなく、開かれた瞬間の
手の位置を追従する形。`GameEventHub.OnQuickMenuToggle(bool show, Transform hand)`
イベントで制御。

### 5. World Canvas の sortingOrder ガード

VR の World Space Canvas で `sortingOrder` を 32767 等の大きな値にすると、
**深度テストをbypass** してすべてのオブジェクトより手前に描画される。
これは VR コントローラのレーザーより前に出てしまい、操作不能になる。

```csharp
// 確認ダイアログ等のオーバーレイUIでも sortingOrder=1 程度に抑える
dialogCanvas.sortingOrder = 1;
```

教訓 [教訓レコード #036](../../docs/architecture.md#lessons-学習) として記録。

---

## 採用しなかった選択肢

### A. UI Toolkit (UIElements)

**棄却理由**:
- VR 用の World Space 描画で UI Toolkit はまだ実用的でなかった (Unity 2022 LTS時点)
- World Space Canvas + uGUI の方が VR 対応情報が豊富

### B. 1つの巨大 Canvas

**棄却理由**:
- 全ページを単一 Canvas に詰めると、再描画範囲が大きくなりVRで重い
- ページ単位に Canvas を分けることで、active時のみ再計算

### C. Unity 標準の `IPointerClickHandler` のみで実装

**棄却理由**:
- VR コントローラは「ポイント＋トリガ」で操作するため、
  `TrackedDeviceGraphicRaycaster` 経由でないとイベントが飛ばない

### D. OVRRaycaster (Meta XR SDK の独自Raycaster)

**棄却理由**:
- XR Interaction Toolkit の `TrackedDeviceGraphicRaycaster` の方が標準寄り
- OVR系は古くなりつつある（Meta XR SDK Core への移行を完了済み）

---

## 解決した課題

### VR でのUI操作の遅延・誤操作防止

`canvasGroup.blocksRaycasts` を `false` にして非表示時にレイを通さない、
かつ `gameObject.SetActive(false)` で完全停止することで、隣のページのUIを
誤クリックする事故を防ぐ。

### 各ページ独立性

`AdminPanelBase` の virtual API（OnOpen/OnClose/OnRefresh）により、
各ページは他のページの存在を知らずに動作する。
追加・削除も `AdminPanelController.pages` リストに足すだけ。

### モーダルダイアログのレンダリング順問題

「ゲーム終了確認ダイアログ」を VR Canvas に出す際：
- 親の SpatialPanel Scroll より手前に出したい
- かつ VR コントローラのレーザーより**後ろ**に居てほしい
  （レーザー透過、深度テスト維持）

`sortingOrder = 1` + `TrackedDeviceGraphicRaycaster` + 最上位 Canvas 配下
配置の三重保険で解決。

---

## コード構成

- **`AdminPanelBase.cs`** — ページ抽象基底
- **`AdminPanelController.cs`** — タブ切替・表示制御
- **`TabButton.cs`** — タブUI（クリック→ShowPage通知）
- **`QuickMenuController.cs`** — 手追従メニュー（6ボタン）
- **`Page_Parameters.cs`（抜粋）** — ページ実装の代表例（短く読みやすい）

---

## 既知の制約・トレードオフ

- **`Page_Environment.cs` の肥大**
  - 内容:約1,350行、責務多すぎ
  - 対処:機能別パーシャルクラス分割が望ましい（v1.0.2リファクタ候補）
- **VR独自フォントサイズ**
  - 内容:通常UIより大きめ（24-28pt）でないと読めない
  - 対処:規約として 22pt 以上に統一
- **TrackedDeviceGraphicRaycaster の依存**
  - 内容:XR Interaction Toolkit前提
  - 対処:OpenXRランタイムの差異を内部で吸収
- **sortingOrder の罠**
  - 内容:高い値で深度テストbypass
  - 対処:コードレビュー時に sortingOrder 変更を必ず確認（教訓レコード #036）

---

## 関連システム

- **[GameEventHub](../GameEventHub/README.md)** — UIイベントの発行先 / 状態購読元
- **[DemoBuild](../DemoBuild/README.md)** — `DemoLockableUI` を AddComponent
- **[PersistenceLayer](../PersistenceLayer/README.md)** — タブ再表示時の値再同期 (`OnRefresh`)

---

## 抜粋コード

- `AdminPanelBase.cs`, `AdminPanelController.cs`, `TabButton.cs`
- `QuickMenuController.cs`
- `Page_Parameters.cs` 抜粋（公開API中心、UI構築コードは省略）
