# VR個人開発ポートフォリオ — Unity / XR Interaction Toolkit / Meta XR SDK

Meta Quest 3 (PCVR) 向け VR アプリケーションの個人開発プロジェクトです。
本ポートフォリオは**技術設計の言語化**を主目的に、コードベース
（約100ファイル / 約 24,000 行）から設計判断が見える部分を整理したものです。

> **本作は IT学科の学生による「初リリース作品」**です。
> 業界経験ゼロ・Unity独学からスタートし、約1ヶ月で v1.0.0 をリリースしました。
> 未熟な実装箇所もありますが、認識した負債は本ドキュメント内で正直に開示しています。

---

## プロジェクト概要

- **プラットフォーム**：Meta Quest 3 / PCVR (SteamVR / Quest Link / Virtual Desktop)
- **エンジン**：Unity 2022.3 LTS (2022.3.62f3)
- **言語**：C# 9 / .NET Standard 2.1
- **開発体制**：個人開発（独学・AI駆動開発）
- **開発スタイル**：短期集中開発・継続リリース体制
- **開発者の経験**：**IT学科の学生・本作が初リリース**（業界経験ゼロ・Unity独学）

## 実績

- **リリース日**：2026年4月18日 (v1.0.0)
- **リリース後10日間の販売数**：**約700本**
- **リリース後10日間の売上（卸手取り換算）**：**約 90万円**
- **リリース後アップデート**：v1.0.1 (SteamVR互換性修正等) → v1.0.2 (環境プリセット・トーンスライダー等、開発中)

この売上は、**業界経験ゼロの学生が初リリース作品で達成した数字**です。

---

## 技術スタック（manifest.json から実測）

### コア
- **Unity 2022.3 LTS** (2022.3.62f3) — VR向け安定LTS
- **XR Interaction Toolkit 3.1.2** — VR入力・GrabInteractable
- **Meta XR SDK Core 76.0.1** — Quest 3 連携・OpenXR ランタイム
- **OpenXR 1.14.3** — クロスベンダ XR ランタイム
- **Universal Render Pipeline 14.0.12** — URP
- **Animation Rigging 1.2.1** — IK拡張

### 補助ライブラリ
- **MagicaCloth2** — 布・髪のクロスシミュレーション（GPU高速）
- **lilToon** — トゥーンシェーダー（無料・OSS）
- **Bakery** — ライトマップベイクツール
- **DOTween (Demigiant)** — Tween/Sequence
- **Odin Inspector (Sirenix)** — Editor拡張
- **FinalIK (RootMotion)** — Look-At / IK 補助
- **ExcelDataReader** — `.xlsx` パーサ（プレイヤーカスタムプリセット用）

### 開発ツール
- **Claude Code (Maxプラン)** — 主力AI駆動開発ツール
- **Anthropic Claude (Opus系最新モデル)** — Claude Code のプラン内で随時利用
- 開発初期は **Google AI Studio (Gemini)** を併用していたが、ファイルの直接操作・閲覧が
  できないボトルネックを抱えていた。詳細は `docs/ai-driven-development.md` を参照

---

## アーキテクチャ概観

レイヤー構成（**入力 → イベントバス → 状態 → 可視化 → 永続化 → UI**）：

- **Input Layer**：XR Input Manager / External Device Bridge
- **Event Bus**：GameEventHub（静的 Action Pub/Sub の中央ディスパッチャ）
- **State Layer**：ActionEngine / ActionMatrix（SO） / ParameterStateManager
- **Visualization Layer**：AutonomyHub（8 modules） / ExpressionController / OutfitManager
- **Persistence**：SaveDataManager / Excel Preset I/O
- **UI Layer**：AdminPanel（VR World Space Canvas） / QuickMenu（手追従）

主要な依存方向：

- 入力（XR / 外部デバイス）→ GameEventHub
- GameEventHub → ActionEngine（状態遷移要求）/ AutonomyHub・ExpressionController（可視化）
- ActionEngine → ActionMatrix（再生対象を読み出す）/ GameEventHub（状態変化を通知）
- ParameterStateManager → AutonomyHub・ExpressionController（参照のみ、書き戻しなし）
- OutfitManager → AutonomyHub（衣装変更時の SMR 再キャッシュ通知）
- SaveDataManager ↔ ActionEngine / ParameterStateManager / OutfitManager（双方向）
- Excel Preset I/O ↔ ActionMatrix（インポート/エクスポート）
- AdminPanel / QuickMenu → GameEventHub（操作要求発行のみ、State には直接書き込まない）

**禁則**：UI Layer から State Layer への直接書き込み、Visualization Layer から State Layer への書き戻し（SSOT 原則）。

詳細：[docs/architecture.md](docs/architecture.md)

---

## 主要システム

主要9システム + メタ情報3本。各 `README.md` で
**「採用したパターン / 採用しなかった選択肢 / 解決した課題 / 既知のトレードオフ」**
を言語化しています。

- **[ActionMatrix](src/ActionMatrix/README.md)** — ScriptableObject駆動の状態遷移FSM（データ駆動・SO Facade）
- **[GameEventHub](src/GameEventHub/README.md)** — 静的 Pub/Sub + SSOT原則（static `Action`、`SetValueWithoutNotify`）
- **[AutonomyModule](src/AutonomyModule/README.md)** — Tickベースの自律挙動8モジュール（Template Method・責務分離）
- **[ExpressionCascade](src/ExpressionCascade/README.md)** — DOTweenによる表情カスケード（部位別ロック・段階遷移）
- **[DemoBuild](src/DemoBuild/README.md)** — 体験版 / 製品版二重ビルド設計（コンパイル定義・SO切替）
- **[OutfitManager](src/OutfitManager/README.md)** — 衣装動的着替え + 物理連携（ボーン再ペアレント・遅延コライダー）
- **[ExcelPipeline](src/ExcelPipeline/README.md)** — 外部Excelによるデータ駆動（StableId採番・VLOOKUP生成）
- **[PersistenceLayer](src/PersistenceLayer/README.md)** — JSON永続化と複数スロット（persistentDataPath・バージョン互換）
- **[VRWorldSpaceUI](src/VRWorldSpaceUI/README.md)** — World Space CanvasのVR UI（TrackedDeviceGraphicRaycaster）

メタ情報：
- [docs/architecture.md](docs/architecture.md) — システム全体の依存関係と設計原則
- [docs/tech-stack.md](docs/tech-stack.md) — 各技術の選定理由と棄却した選択肢
- [docs/ai-driven-development.md](docs/ai-driven-development.md) — AI駆動開発のワークフロー

---

## 設計上の主要原則

### 1. SSOT (Single Source of Truth)
ゲーム状態は**ロジック層が唯一の真実**で、UIはそれを映す窓に徹する。
UIが状態を毎フレーム書き戻す設計は禁止。詳細は
[GameEventHub README](src/GameEventHub/README.md)。

### 2. ScriptableObject によるデータ駆動
バランス値・遷移グラフ・プリセットは **すべてSOアセット**。
コード中にハードコードしない。詳細は
[ActionMatrix README](src/ActionMatrix/README.md)。

### 3. インターフェース経由の疎結合
モジュール間は直接呼び出しを避け、`GameEventHub` の static Action でPub/Sub。

### 4. ランタイムでの共有マテリアル汚染の禁止
`mat.SetColor()` は共有マテリアルアセットを永続書き換えする。
ランタイム着色は **MaterialPropertyBlock 経由** に統一。

### 5. 教訓の体系化
ハマったバグ・設計判断は `~/.claude/lessons/` に LES-XXX として
ナレッジベース化し、次セッション開始時に自動表示する仕組みを構築。
詳細は [docs/ai-driven-development.md](docs/ai-driven-development.md)。

---

## ディレクトリ構成

ルート直下：

- `README.md` — このファイル

`docs/` 配下：

- `architecture.md` — システム全体設計
- `tech-stack.md` — 技術選定の判断記録
- `ai-driven-development.md` — AI駆動開発のワークフロー

`src/` 配下（1システム1ディレクトリ、各 `README.md` + 抜粋 .cs）：

- `ActionMatrix/` — ScriptableObject 駆動 FSM
- `GameEventHub/` — 静的 Pub/Sub + SSOT
- `AutonomyModule/` — Tick ベース自律挙動 8モジュール
- `ExpressionCascade/` — DOTween 表情カスケード
- `DemoBuild/` — 体験版 / 製品版二重ビルド
- `OutfitManager/` — 衣装動的着替え
- `ExcelPipeline/` — Excel データ駆動
- `PersistenceLayer/` — JSON 永続化
- `VRWorldSpaceUI/` — VR UI 基底

---

## このポートフォリオを読む順序（推奨）

**20分で全体像を把握できる**ことを目標に構成しています。

1. このREADME（5分）— プロジェクト全体像
2. [docs/architecture.md](docs/architecture.md)（5分）— 設計思想と依存関係
3. [docs/ai-driven-development.md](docs/ai-driven-development.md)（5分）— AI協働の運用論
4. 興味のあるシステムREADMEを1〜2本（5分）— 設計判断の深掘り

時間がある場合：
- [docs/tech-stack.md](docs/tech-stack.md) — 各技術の選定理由
- [src/](src/) 配下の実コード — 抽象化済みC#コード

---

## 連絡先

カジュアル面談・選考でのご連絡を希望される場合は、本リポジトリを発見いただいた
経路（採用媒体・スカウト・直接応募等）からのご連絡をお願いいたします。

公開リポジトリ運用上、ここには直接の連絡先を掲載していません。
