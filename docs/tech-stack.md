# 技術選定の判断

各技術について「なぜ採用したか / 棄却した代替案 / トレードオフ」を記録する。

依存関係は `Packages/manifest.json` および `Assets/Plugins/` を直接読み取って
記載しているため、ドキュメント上の記述と実プロジェクトに齟齬はない。

---

## エンジン: Unity 2022.3 LTS (2022.3.62f3)

### 選定理由
- **LTS**（Long Term Support）で長期保守が保証されている
- VR開発で安定性が確認されている枯れたバージョン
- `XR Interaction Toolkit 3.x` の対応が成熟している

### 棄却した選択肢
- **Unity 6**: 本作の開発開始時はリリース直後で不具合リスク。VR周りの挙動変化が
  未検証だった
- **Unity 2021 LTS**: サポート終了が近い、新しい XR API が使えない

### トレードオフ
- 最新機能（VisionOS対応など）は使えない
- ただし本作は Quest 3 + PCVR 単機種ターゲットのため影響なし

---

## VR フレームワーク

### com.unity.xr.interaction.toolkit 3.1.2

#### 選定理由
- **Unity 公式の VR フレームワーク**で、OpenXR に統合済み
- レーザーポインタ / UI 操作 / Grab Interactable がワンパッケージ
- 本作の UI 操作は XR Interaction Toolkit の `TrackedDeviceGraphicRaycaster`
  必須（World Space Canvas のクリック判定）

### com.meta.xr.sdk.core 76.0.1

#### 選定理由
- Quest 3 専用最適化（FFR, ASW, Multiview Rendering）が必要
- Meta側公式の OpenXR ランタイム連携
- Mixed Reality 機能（パススルー）も将来活用可

### com.unity.xr.openxr 1.14.3

#### 選定理由
- **クロスベンダ XR ランタイム**（Quest, SteamVR, Virtual Desktop で動く）
- OpenXR を介することで「Steam版」「Quest単体版」を同じビルドで対応

### 棄却した選択肢

- **HurricaneVR** (有料)
  - 棄却理由：物理ベースのVRハンド表現が魅力的だが、本作は手の物理表現より自律挙動の方が体験価値の中心。$70 のコストに対して ROI が低い
- **VRTK**
  - 棄却理由：開発活発度が落ちている、最新 Unity への追従が遅い
- **Auto Hand**
  - 棄却理由：機能不足、カスタマイズ性が低い
- **Oculus Integration（OVR）**
  - 棄却理由：Meta XR SDK Core への移行で deprecated。本作も初期は OVR で開発開始したが途中で完全移行

### トレードオフ
- XR Interaction Toolkit と Meta XR SDK の併用で**学習コスト二重**
- ただし両者の責任分担は明確（XRIT=入力統合、Meta XR SDK=Quest 固有最適化）

---

## レンダリング: URP 14.0.12

### 選定理由
- **Quest 3 のVR で性能を出すには URP がほぼ必須**（Built-in は重い）
- Forward+ Renderer で多光源を実用的なFPSで処理可能
- `lilToon` の URP 対応版が利用可能

### 棄却した選択肢

- **Built-in Render Pipeline**
  - 棄却理由：Quest 3 でフレームレート維持が困難、最新シェーダーアセットが URP / HDRP に移行
- **HDRP**
  - 棄却理由：Quest3 では非現実的（重すぎる）

### トレードオフ
- Built-in 用のシェーダーアセットの一部が使えない
- 特に過去の Asset Store 資産で URP対応していないものは除外する必要

---

## トゥーンシェーダー: lilToon

### 選定理由
- **無料・OSS**（MIT）で商用利用可
- VRChat 由来で日本のキャラクター作成者に最も普及
- URP / Built-in 両対応
- 機能が極めて豊富（Color2nd/Color3rd レイヤー、Emission2nd、輪郭線、Shadow ON/OFF 等）

### 棄却した選択肢

- **UTS2/UTS3 (Unity-chan ShaderToon)**
  - 棄却理由：機能が lilToon より少ない
- **MToon (VRM)**
  - 棄却理由：VRMモデル用途以外では機能不足
- **自作シェーダー**
  - 棄却理由：キャラ表現の多様な要求（メイク、頬染め、半透明）を網羅するシェーダー実装は工数大

### トレードオフ
- **lilToon Optimize 機能を実行すると `.shader` ファイルが破壊される**
  という罠あり（実体験、git checkout で復元）。教訓
  [教訓レコード #037](../../docs/architecture.md#lessons-学習) として記録
- マテリアルの `_Color2nd`, `_Color3rd` 等を**ランタイムで `mat.SetColor()` すると
  共有アセットが永続書き換えされる**。MaterialPropertyBlock 経由で対処
  [教訓レコード #029](../../docs/architecture.md#lessons-学習)

---

## 物理（クロス）: MagicaCloth2

### 選定理由
- **GPU 加速で軽い**（Unity 標準 Cloth は CPU 重く Quest3 で実用困難）
- 髪・スカート・布を統一的に扱える
- VRChat 系コミュニティで使い慣れている

### 棄却した選択肢

- **Unity Cloth (Built-in)**
  - 棄却理由：Quest3 で重く、骨ベースシミュレーションでない
- **Dynamic Bone**
  - 棄却理由：機能限定的、コライダーセットアップが煩雑
- **SpringBone**
  - 棄却理由：単純な揺れには使えるが、衣装の布表現には機能不足

### トレードオフ
- prefab を Instantiate した瞬間に内部状態がビルドされるため、コライダー追加は
  **アクティブ化前に**完了している必要がある（[OutfitManager](../src/OutfitManager/README.md) 参照）

---

## ライトマップ: Bakery

### 選定理由
- **Unity 標準 Progressive Lightmapper より大幅に高速**かつ高品質
- VR では事前ベイクで動的ライト数を抑える必要があり、品質の差が体験に直結
- アセット価格に対して工数削減効果が大きい

### 棄却した選択肢
- **Unity Progressive (CPU)**: 遅い
- **Unity Progressive (GPU)**: 改善はされたが Bakery には及ばない

---

## Tween: DOTween (Demigiant)

### 選定理由
- 業界標準的な Tween ライブラリ、無料版で本作の用途には十分
- `DOTween.Sequence()` で**カスケード遅延・複数同時 Tween 中断**が簡潔に書ける
- [ExpressionCascade](../src/ExpressionCascade/README.md) の表情遷移で多用

### 棄却した選択肢

- **iTween**
  - 棄却理由：開発停止
- **LeanTween**
  - 棄却理由：DOTween より機能少ない
- **Coroutine 手書き**
  - 棄却理由：複数同時 Tween + 中断管理を Coroutine で書くと冗長
- **UniTask の DelayFrame**
  - 棄却理由：値補間機能なし、別途 Tween が必要

---

## エディタ拡張: Odin Inspector (Sirenix)

### 選定理由
- **複雑な ScriptableObject の表編集 UX**（`[TableList]`, `[VerticalGroup]`,
  `[ButtonGroup]` 等）が桁違いに使いやすい
- 標準 `[CustomEditor]` を書くより圧倒的に短時間
- 本作のコア機能 [ActionMatrix](../src/ActionMatrix/README.md) の編集UX を成立させている

### 棄却した選択肢
- **`[CustomEditor]` の自前実装**: 1機能ごとに 50-100行、Odin の `[TableList]`
  なら 1属性で済む
- **NaughtyAttributes**（無料代替）: 機能が一部しかない、`[TableList]` 相当なし

### トレードオフ
- 有料アセット（個人 $55）。ただし開発時間短縮効果が大きく ROI 良好

---

## IK: FinalIK (RootMotion)

### 選定理由
- 本作は視線・首の追従を**自然に**見せる必要があり、`LookAtIK` の補正が
  Unity 標準より細かい
- 既に Asset Store で評価が高く、VRChat 系で導入実績多数

### 棄却した選択肢

- **Animation Rigging のみ**
  - 棄却理由：LookAt の補正パラメータが少ない、首の追従が硬い
- **自作 IK**
  - 棄却理由：工数大

### 補完
- **Animation Rigging** も併用（より単純な拘束で十分な部位）

---

## Excel I/O: ExcelDataReader

### 選定理由
- **Apache 2.0 ライセンス**（商用利用可）
- IL2CPP（Quest3）で動作確認済
- `.xlsx` をシンプルに読める

### 棄却した選択肢

- **EPPlus**
  - 棄却理由：2018年から商用有料化
- **NPOI**
  - 棄却理由：IL2CPP で AOT 関連の問題が起きやすい
- **ClosedXML**
  - 棄却理由：.NET Core 依存があり Unity で使いづらい

詳細は [ExcelPipeline README](../src/ExcelPipeline/README.md) 参照。

---

## メッシュ最適化: MeshBaker

### 選定理由
- **DrawCall 削減**が VR で必須
- 静的な背景や複数の同マテリアルメッシュを1つに統合可能

### 棄却した選択肢
- **Unity 標準の Static Batching**: 動的に切替えできるメッシュには使えない
- **手動結合**: 工数大

---

## 開発ツール: Claude Code (Maxプラン)

### 選定理由
- **個人開発で最も生産性が高いAI協働ツール**（複数試した結果）
- ターミナル直結のため Unity Editor 操作とコード編集を並行可能
- カスタムコマンド・skills・hooks による拡張性
- **Anthropic Claude（Opus系最新モデル）** をプラン内で随時利用

### Claude Code 最大の強み：YAML 経由でのインスペクタ・シーン読み取り

Unity プロジェクトのシーン (`.unity`) / プレハブ (`.prefab`) /
ScriptableObject (`.asset`) はすべて **YAML テキスト**として保存される。
Claude Code はこれを直接読めるため、**Inspector で設定した SerializeField の値**や
**シーン階層**まで AI が把握できる。コード以外が原因のバグの特定が劇的に速くなった。

### ワークフロー詳細
[docs/ai-driven-development.md](ai-driven-development.md) を参照。

### 採用しなかった選択肢 / 経緯

- **Google AI Studio (Gemini)**
  - 経緯：開発初期はこれを使っていた。**ファイルを直接操作・閲覧できない**ため、新規チャットを起こすたびに必要コードを全コピペする必要があり、これが最大のボトルネック。Claude Code 移行で大幅にペース改善
- **GitHub Copilot**
  - 経緯：編集補助は速いが、ファイル横断のリファクタや設計議論が弱い
- **Cursor**
  - 経緯：良いツールだが、開発開始時点では Claude Code の CLI 親和性を選択
- **ChatGPT Web UI**
  - 経緯：コンテキスト保持が手動コピペ、Unityファイルへの直接書き込みできない

---

## 補助：Unity MCP（本作では未導入）

Unity Editor との直接連携 MCP（Model Context Protocol）サーバーは存在を把握している程度で、
**本作では導入していない**。次作以降で必要になれば検討する。

---

## まとめ：選定の傾向

本作の技術選定で一貫している判断基準：

1. **無料・OSS を優先**、有料アセットは「Odin Inspector」「FinalIK」「Bakery」「MeshBaker」「MagicaCloth2」のように**確実に時間を節約**するものに限定
2. **Unity 公式 + LTS**を基本とし、新しすぎる機能は採用しない
3. **Quest 3 の制約**（フレームレート、メモリ、IL2CPP）を最優先
4. **個人開発の限界**を踏まえ、サーバー連携・クラウドストレージは避ける
5. **AI協働を加速する選択**（DOTween, Odin など、コードがコンパクトに書ける選択肢）を好む
