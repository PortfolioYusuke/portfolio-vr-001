# GameEventHub — 静的 Pub/Sub と SSOT パターン

## 役割

複数のシステム（入力 / 状態エンジン / UI / 自律挙動 / 永続化）が
**互いの存在を知らずに連携**できる、中央イベントディスパッチャ。

加えて、ゲーム横断の状態（視点追従モード、キャラのトーン値など）に対しては
SSOT (Single Source of Truth) パターンを徹底し、UIから状態への
**書き戻しを禁止**するアーキテクチャ規律を採用している。

このREADMEでは

1. 中央 EventHub の設計
2. SSOT 状態クラスの設計（`ViewFollowModeState` / `AccentToneState` の2例）
3. UI が状態を「映す窓」に徹するための実装規約

の3点を解説する。

---

## 1. 中央 EventHub

### 採用：`static class` + `static Action<T>` フィールド

```csharp
public static class GameEventHub
{
    // 状態遷移系
    public static Action OnNextStateRequest;
    public static Action<int> OnStateSelect;
    public static Action<int> OnMotionChanged;

    // 入力系
    public static Action<float> OnSpeedChangeRequest;
    public static Action<float> OnSpeedMultiplierChanged;
    public static Action OnSpeedResetRequest;
    public static Action OnViewResetRequest;

    // 外部デバイス連携
    public static Action<float> OnStrokeUpdate;

    // UI
    public static Action<bool> OnToggleUI;
    public static Action<bool, Transform> OnQuickMenuToggle;

    // ピーク状態（特殊演出用）
    public static Action<PeakTrigger> OnPeakStarted;
    public static Action OnPeakReached;
}
```

各システムは `OnEnable` で購読、`OnDisable` で解除する。

```csharp
void OnEnable()
{
    GameEventHub.OnNextStateRequest += GoToNextState;
    GameEventHub.OnSpeedChangeRequest += OnSpeedChange;
}

void OnDisable()
{
    GameEventHub.OnNextStateRequest -= GoToNextState;
    GameEventHub.OnSpeedChangeRequest -= OnSpeedChange;
}
```

### 採用しなかった選択肢

- **Zenject / VContainer (DIコンテナ)** — 単一シーン構成のVRアプリでは過剰。学習コスト > 恩恵
- **UnityEvent + Inspector結線** — 依存関係が Inspector を見ないと辿れない。リファクタ困難
- **MessagePipe / UniRx ReactiveProperty** — 良い選択肢だが、本作では「シンプルな pub/sub があれば十分」と判断し外部依存を増やさなかった
- **enum + delegate Dictionary** — コンパイル時の型安全性を捨てる必要あり、`Action<T>` 直接定義の方が IDE 補完が効く

### トレードオフ

- **タイポはコンパイル時に検出される**（`Action` フィールド名のミスは即時エラー）
- ただし**購読忘れ**は実行時まで気付けない → コードレビュー＋ペアプロで対処
- イベントの **発火元が複数ある場合の追跡**には Find References が必要（IDE機能で対応）

---

## 2. SSOT 状態クラス

複数のUIや内部システムから「同じ値」を参照する状態は、
**専用の static class** に値を集約し、変更は必ずSetter経由で行わせる。

### 例1: `ViewFollowModeState`

「VR視点が体位変更時に追従するか」のグローバル設定。
パラメータタブのトグルと QuickMenu のボタンの両方から操作可能。

```csharp
public static class ViewFollowModeState
{
    private static bool _enabled = false;

    public static bool Enabled => _enabled;

    public static event Action<bool> OnChanged;

    public static void Set(bool value)
    {
        if (_enabled == value) return;
        _enabled = value;
        OnChanged?.Invoke(value);
    }

    public static void Toggle() => Set(!_enabled);
}
```

ポイント：
- **値は private**、外部からは Setter 経由のみで変更可能
- **同じ値を Set した時は OnChanged を発火しない**（無限ループ防止）
- 各UIは `SetIsOnWithoutNotify` で UI 表示だけ更新し、`OnChanged` で Set する

UI側の実装規約：

```csharp
// パラメータタブ初期化
viewFollowToggle.SetIsOnWithoutNotify(ViewFollowModeState.Enabled);
viewFollowToggle.onValueChanged.AddListener(ViewFollowModeState.Set);
ViewFollowModeState.OnChanged += b => viewFollowToggle.SetIsOnWithoutNotify(b);
```

これで「QuickMenuでON→パラメータタブのトグルも自動でON」が成立する。

### 例2: `AccentToneState`

キャラの色調スライダー値（主トーン / ハイライト / レイヤー強度）。Gradient で
マテリアル色を決める方式なので、マテリアルから t 値を逆算できない。
そのため**値の真実をここに置く**。

```csharp
public static class AccentToneState
{
    private static float _accentTone = 0f;
    private static float _highlightTone = 0f;
    private static float _layerIntensity = 0.621f;

    public static float AccentTone => _accentTone;
    public static float HighlightTone => _highlightTone;
    public static float LayerIntensity => _layerIntensity;

    public static event Action OnChanged;

    public static void SetAccentTone(float v) { /* clamp + set + 通知 */ }

    /// <summary>セーブデータからの一括復元（通知は1回だけ）</summary>
    public static void RestoreFromSave(float accent, float highlight, float layer)
    {
        _accentTone = Mathf.Clamp01(accent);
        _highlightTone = Mathf.Clamp01(highlight);
        _layerIntensity = Mathf.Clamp01(layer);
        OnChanged?.Invoke();
    }
}
```

ポイント：
- セーブ復元時は3値をまとめて反映してから OnChanged 1発
- `Mathf.Clamp01` でClamp（不正値防止）
- 「変化がなかったら通知しない」で UI 側のリスナーループを防止

---

## 3. UI が「映す窓」に徹するための実装規約

設計原則として、`docs/architecture.md` に書いた通り：

> UI Layer から State Layer への**直接書き込みは禁止**。
> UI は状態を読む / 変更要求イベントを発行するだけ。

これを徹底するため、Sliderの初期化等で以下のパターンを採用する。

### 規約1: 初期表示は `SetValueWithoutNotify`

```csharp
// 良い例
slider.SetValueWithoutNotify(currentValue);
slider.onValueChanged.AddListener(v => state.SetValue(v));

// 悪い例（onValueChangedが発火して状態を上書きする）
slider.value = currentValue;
slider.onValueChanged.AddListener(v => state.SetValue(v));
```

### 規約2: タブ再表示時の値は **状態から読み戻す**

```csharp
public override void OnRefresh()
{
    // ✅ 状態の現在値で UI を更新
    if (parameterSlider != null)
        parameterSlider.SetValueWithoutNotify(_manager.CurrentValue);
}
```

### 規約3: `Update()` で UI→状態を毎フレーム書き戻さない

過去にこのパターンで「外部から状態を変更しても、次のフレームで
スライダー値で踏み潰される」というバグを踏んだ。
詳細は `~/.claude/lessons/` 配下の**教訓レコード #011**（UIはゲーム状態を映す窓）に記録済。

---

## 解決した課題

### モジュール数増加によるスパゲッティ化の防止

このプロジェクトで `GameEventHub` を導入する前は、VR入力ハンドラが `ActionEngine`
を直接呼び、`AudioDirector` が `ActionEngine` を直接見て、`ParameterStateManager`
が UI を直接更新する……という相互参照地獄になっていた。

EventHub 導入後：
- 入力ハンドラ → `OnSpeedChangeRequest(delta)` だけ発火
- `ActionEngine` → `OnSpeedChangeRequest` を購読してタイマー更新
- UI → 状態クラスの `OnChanged` を購読して表示更新

各モジュールは「自分の責務」だけ知ればよくなった。

### プリセット復元時の整合性

環境プリセット復元では `state.RestoreFromSave(...)` で値を復元してから
`OnChanged` を1発発火するパターンを徹底。
**部分更新による中間状態の表示**を防いでいる。

### マルチUI同期

「QuickMenuのトグル」と「パラメータタブのトグル」が同じ状態を共有する場合、
SSOT クラスの `OnChanged` 経由で両方が自動同期する。
明示的なクロス参照（QuickMenu→パラメータタブ）が不要。

---

## コード構成

- **`GameEventHub.cs`** — 中央 static Pub/Sub。`Action<T>` フィールド群
- **`ViewFollowModeState.cs`** — bool型 SSOT の例
- **`AccentToneState.cs`** — 複数 float の SSOT、セーブ復元用 API つき
- **`InputManager.cs` 抜粋** — XR入力 → EventHub 発行の例（予定）

---

## 既知の制約・トレードオフ

- **static class はテスト時にリセットしにくい**
  - 内容：UnitTest で値が前のテストに引きずられる
  - 対処：`[SetUp]` で各テスト前にリセット呼び出し
- **static event 購読忘れリーク**
  - 内容：`OnDisable` で解除し忘れるとリーク
  - 対処：コードレビュー + パターン化
- **複数キャラへの拡張時**
  - 内容：現在は1キャラ前提。複数キャラなら `EventHub` を `instance` 化が必要
  - 対処：将来 v2.x で `CharacterContext` 経由に変更想定
- **イベント名の重複**
  - 内容：`OnSomething` という名前が複数の意味で使われる懸念
  - 対処：プレフィックス命名（`OnState*` / `OnInput*` 等）で対処

---

## 関連システム

- **[ActionMatrix](../ActionMatrix/README.md)** — 状態遷移要求を EventHub 経由で受信
- **[AutonomyModule](../AutonomyModule/README.md)** — 状態通知を購読
- **[VRWorldSpaceUI](../VRWorldSpaceUI/README.md)** — UI からの操作を EventHub に発行
- **[PersistenceLayer](../PersistenceLayer/README.md)** — `RestoreFromSave` で SSOT を復元

---

## 抜粋コード

- `GameEventHub.cs`
- `ViewFollowModeState.cs`
- `AccentToneState.cs`
