# アーキテクチャ

## 設計思想

本プロジェクトでは以下の5原則を採用している。

1. **SSOT (Single Source of Truth)** — 状態の真実はロジック層、UIは映す窓に徹する
2. **ScriptableObject によるデータ駆動** — 設定とロジックを分離、AIアセスタント・非エンジニアでも調整可能
3. **イベント駆動の疎結合** — システム間は直接参照ではなく `static Action` を介して通信
4. **VR特有の制約への配慮** — フレームレート維持を最優先、`FindObjectOfType` の頻度制御
5. **教訓の体系化** — ハマったバグ・設計判断を `~/.claude/lessons/` にナレッジベース化

これらの原則は、開発初期に発生した複数のバグ（UIが状態を書き戻して値が固定される、
共有マテリアルがランタイムで汚染される、`Animator Write Defaults=ON` で
BlendShape値が毎フレーム0にリセットされる、等）から逆算して言語化されたものである。

---

## システム構成

レイヤー構成と各レイヤーに属する主要モジュール：

- **Input / External**
  - XR Input Manager（XR Interaction Toolkit）
  - External Device Bridge（WebSocket protocol）
- **Event Bus (Pub/Sub)**
  - GameEventHub（`static Action<T>` の中央ディスパッチャ）
- **State Layer**
  - ActionEngine（FSM driver）
  - ActionMatrix（SO：states + branches）
  - ParameterStateManager（Singleton）
- **Visualization Layer**
  - AutonomyHub（8 Tick modules）
  - ExpressionController（DOTween cascade）
  - OutfitManager（bone reparent）
- **Persistence**
  - SaveDataManager（JsonUtility）
  - Excel Pipeline（ExcelDataReader）
- **UI Layer**
  - AdminPanel（World Space Canvas）
  - QuickMenu（World Space）

主要な依存方向：

- Input / External → GameEventHub（イベント発火）
- GameEventHub → ActionEngine（状態遷移要求）
- ActionEngine → ActionMatrix（再生対象を読み取る）
- ActionEngine → GameEventHub（状態変化を通知）
- GameEventHub → AutonomyHub / ExpressionController（状態変化を購読して可視化）
- ParameterStateManager → AutonomyHub / ExpressionController（**参照のみ**、書き戻し禁止）
- OutfitManager → AutonomyHub（装着時に SMR 再キャッシュ通知）
- SaveDataManager ↔ ActionEngine / ParameterStateManager / OutfitManager（双方向）
- Excel Pipeline ↔ ActionMatrix（インポート/エクスポート）
- AdminPanel / QuickMenu → GameEventHub（操作要求発行のみ、State には直接書き込まない）

---

## レイヤー間の依存ルール

- **Input / External → Event Bus**
  - 経路：直接参照（publish）
  - 備考：入力 → イベント発火
- **Event Bus → State Layer**
  - 経路：static Action 購読
  - 備考：状態変更要求を受信
- **State Layer → Event Bus**
  - 経路：直接参照（publish）
  - 備考：状態変化を通知
- **Event Bus → Visualization**
  - 経路：static Action 購読
  - 備考：UIや表情・自律挙動が反応
- **Visualization → State Layer**
  - 経路：**読み取りのみ**
  - 備考：UIが状態を書き戻すことは禁止（SSOT）
- **Persistence → State / Visualization**
  - 経路：双方向
  - 備考：起動時ロード・任意保存
- **UI → Event Bus**
  - 経路：publish のみ
  - 備考：UIから直接 State を書き換えない

**禁則：**
- UI Layer から State Layer への直接書き込み
- Visualization Layer から State Layer への書き込み
- 共有 ScriptableObject アセットの **ランタイム書き換え**（共通マテリアルや設定SO）

---

## 主要な設計判断

### 1. なぜイベント駆動を選んだか

**問題**: モジュール数が10を超えた時点で、相互参照によるスパゲッティ化が顕在化した。
各モジュールが他モジュールを `FindObjectOfType` で取得→直接呼び出し、という構造は
- テスト時にモック化困難
- システム削除/差し替え時の波及範囲が予測不能
- VR で `FindObjectOfType` の多用がフレームレート低下を招く

**採用**: `static class GameEventHub` に `Action<T>` イベントを集約。各モジュールは
イベントを購読/発火するだけで、相手の存在を知らない設計に統一。

**棄却した代替案**:
- **DI コンテナ** (Zenject 等): VRアプリの単一シーンでは過剰。学習コストに対して恩恵小。
- **Singleton 並び**: テスト容易性とライフサイクル管理が困難。残ったSingletonは2〜3個に絞り、
  それ以外は EventHub で疎結合化。
- **UnityEvent**: Inspector で結線できる利点があるが、コード上の依存関係が
  Inspector を見ないとわからない、シリアライズ依存でランタイム差し替え困難。

**トレードオフ**: イベント名のタイポをコンパイル時に検出できない（リフレクションでは
ないが、購読側の型ミスマッチは実行時まで検出されない）。これは命名規約と
コードレビューで対処。

### 2. なぜ ScriptableObject 駆動か

**問題**: 状態遷移グラフ・バランス値・ボイスバンクをコード内ハードコードすると、
- パラメータ調整に毎回 Unity ビルドが必要
- 非エンジニア（自分自身も "デザイナーモード"）の調整作業がブロックされる
- AI（Claude Code）にデータ修正を任せられない

**採用**: **ほぼ全データを ScriptableObject に外出し**。
- `ActionMatrix` (状態遷移グラフ)
- `MotionPair` (1状態のアニメーション+メタデータ)
- `ParameterConfig` (バランス値)
- 各種 `*Database` (Hair / Eye / Clothing / BodyShape)
- `LightingPresetData` / `BackgroundLightingData`

**棄却した代替案**:
- **JSON ファイル**: Unity の SerializedField/Inspector との親和性が低く、
  GUID参照が使えない。
- **コード内定数**: ビルド・調整コスト増。

**トレードオフ**:
- SO アセットの**ランタイム書き換えは共有アセットを永続汚染する**という
  Unityの罠がある。これは [教訓レコード #029](#lessons-学習) として記録し、
  ランタイム書き換え時は `MaterialPropertyBlock` を使う / SOを `Instantiate` で
  コピーしてから書き換える、というパターンに統一。

### 3. なぜ Singleton を残したか

**残した Singleton**:
- `SaveDataManager` (JSON I/O、シーン横断的なファイルパス管理)
- `BackgroundManager` (シーンに1つだけ存在する背景制御)
- `LightingManager` (同上)
- `ExternalDeviceBridge` (WebSocket接続、シーン横断)
- `SessionManager` (アクティブキャラの参照ハブ)

これらは**シーン内に複数存在する意義がない**ものに限定。それ以外は EventHub で代替。

### 4. なぜ FinalIK を採用したか（Animation Rigging との関係）

- **FinalIK** (RootMotion) — 視線・首の追従の中心。`LookAtIK` の補正パラメータが
  Animation Rigging より細かいため採用
- **Animation Rigging** (Unity公式) — より単純な拘束で十分な部位の補完用に併用

両者は**競合せず補完関係**。視線・首の追従は FinalIK 一択、その他の補助拘束は
Animation Rigging という棲み分け。

### 5. なぜ XR Interaction Toolkit + Meta XR SDK か（HurricaneVR ではなく）

**採用**: XR Interaction Toolkit 3.1.2 + Meta XR SDK Core 76.0.1

**棄却した HurricaneVR**:
- 物理ベースのVRハンド表現が魅力的だが、本作は手の物理表現より
  顔・表情・視線の自律挙動の方が体験価値の中心
- XR Interaction Toolkit のレーザーポインタとUI連携が成熟している
- Quest 3 ターゲットでは Meta XR SDK のFFR (Fixed Foveated Rendering) や
  ASW (Application SpaceWarp) が必須で、Meta XR SDK との統合が前提

**トレードオフ**: 物理ベースのハンドジェスチャ等の細かい表現は犠牲に。

---

## 主要なシステム間連携

### ActionEngine と ParameterStateManager
- ActionEngine が `ParameterStateManager.SetValue(value)` を呼ぶことはない
- 代わりに、入力やタイマーから `ParameterStateManager` 自身が値を更新
- ActionEngine は `ParameterStateManager.CurrentValue` を **読むだけ**
- → 互いを直接呼ばず、`GameEventHub.OnSomethingHappened` 経由で連携

### AutonomyHub と ParameterStateManager
- `AutonomyHub.Tick(parameterLevel, autonomy, fatigue)` で各モジュールに値を渡す
- 各モジュール（`EyeMicroMotion` 等）は `ParameterStateManager` を**知らない**
- → AutonomyHub だけが `ParameterStateManager` を参照する仲介役

### OutfitManager と AutonomyHub
- 衣装変更時、`OutfitManager` は新しい衣装の `BlendShape` ターゲットを設定
- `AutonomyHub` 配下の `BreathSway` 等は新しいターゲットを発見する必要がある
- → `OutfitManager.OnEquipChanged` イベント → 各モジュールが再初期化

---

## 既知の構造的負債

ポートフォリオ作成時点で認識している構造的負債もオープンにしておく。

- **`Page_Environment.cs` の肥大化**
  - 内容：約1,350行、責務多すぎ
  - 対処方針：機能別パーシャルクラス分割が望ましい。リリース後リファクタ予定
- **`ParameterStateManager.Update()` のUI→state書き戻し**
  - 内容：教訓レコード #011 違反だがタブ非active時SetActive(false)で実害なし
  - 対処方針：OnRefresh+listener方式へ段階移行
- **Slider初期化順序**
  - 内容：RemoveAllListeners が value 代入の後に来る箇所あり
  - 対処方針：`SetValueWithoutNotify` への統一が望ましい

これらは v1.0.2 リファクタ候補として内部TODOに記録している
（透明性のためポートフォリオでも開示）。

---

## 教訓 (Lessons) {#lessons-学習}

開発中に踏んだ「**この罠は次回避けたい**」というパターンを
`~/.claude/lessons/` ディレクトリに `LES-XXX` 形式で蓄積している。
これは Claude Code が次セッション開始時に自動表示する仕組みと連動している。

代表的な教訓レコード（内部ファイル名の `LES-XXX` が ID）：

- **教訓レコード #011**
  - カテゴリ：unity-ui
  - 内容：UIはゲーム状態を映す窓。ゲーム状態がSSOT
- **教訓レコード #012**
  - カテゴリ：unity-vr
  - 内容：SteamVR OpenXRランタイムは非ASCII exe名で接続失敗
- **教訓レコード #013**
  - カテゴリ：debugging
  - 内容：「動くもの」との比較解析を最初にやれ
- **教訓レコード #014**
  - カテゴリ：debugging
  - 内容：同じ環境で他作品が動くならプロジェクト側を疑え（AIの「環境のせい」逃げを許さない）
- **教訓レコード #029**
  - カテゴリ：unity-general
  - 内容：共有マテリアル直書きでアセット永続汚染
- **教訓レコード #032**
  - カテゴリ：debugging
  - 内容：動いているコードを「理論的違反」だけを理由に触るな
- **教訓レコード #035**
  - カテゴリ：unity-general
  - 内容：FindObjectOfType はinactive を除外。`(true)`明示が必要
- **教訓レコード #036**
  - カテゴリ：unity-vr
  - 内容：World Canvas の sortingOrder=32767 は深度テストをbypass
- **教訓レコード #038**
  - カテゴリ：unity-vr
  - 内容：LazyFollow の RotationFollowMode 選択基準

詳細とこのナレッジベース運用の意義は
[docs/ai-driven-development.md](ai-driven-development.md) に記載。

---

## 補足：個別システムへのリンク

各システムの設計判断詳細は以下のREADMEを参照：

- [ActionMatrix](../src/ActionMatrix/README.md)
- [GameEventHub](../src/GameEventHub/README.md)
- [AutonomyModule](../src/AutonomyModule/README.md)
- [ExpressionCascade](../src/ExpressionCascade/README.md)
- [DemoBuild](../src/DemoBuild/README.md)
- [OutfitManager](../src/OutfitManager/README.md)
- [ExcelPipeline](../src/ExcelPipeline/README.md)
- [PersistenceLayer](../src/PersistenceLayer/README.md)
- [VRWorldSpaceUI](../src/VRWorldSpaceUI/README.md)
