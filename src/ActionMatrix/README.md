# ActionMatrix — ScriptableObject 駆動の状態遷移FSM

## 役割

キャラクターの「行動状態」をデータとして表現し、ランタイムで再生・遷移させる
カスタム FSM。状態定義は **すべて ScriptableObject アセット**で管理し、
コードは「再生エンジン」と「データ構造定義」だけに留める。

具体的には、1つの `ActionMatrix` が複数の `MotionStateData`（状態行）を持ち、
各行は

- 再生する `MotionPair`（2キャラのペアアニメーション組合せ）
- 滞在時間 `durationRange` (Vector2 = 最小/最大、ランタイムで内挿乱択)
- 段階的速度遷移用の `AnimationCurve`
- BPMオーバーライド設定
- 次状態への重み付き遷移分岐

を保持する。`ActionEngine` はこのマトリクスを起動時に受け取り、
入力イベントとタイマーに従って状態を進める。

---

## 採用したパターン

### 1. ScriptableObject + Odin Inspector TableList

状態定義を `[CreateAssetMenu]` で生成可能なアセットにし、Odin Inspector の
`[TableList]` で**Excelのような表形式編集**を実現した。

```csharp
[CreateAssetMenu(fileName = "NewMatrix", menuName = "ActionSystem/ActionMatrix")]
public class ActionMatrix : ScriptableObject
{
    [Searchable]
    [TableList(ShowIndexLabels = true)]
    public List<MotionStateData> states = new List<MotionStateData>();
}
```

採用理由：
- Inspector 上で**1状態1行**として並べ、列ごとに `motionPair` / `durationRange` /
  `speedCurve` / `branches` を編集できる
- `[Searchable]` で状態数が増えても検索可能
- 非エンジニアでも編集できる（モーション差し替え・速度調整等）

### 2. ランタイムでのディープコピー（共有SO汚染防止）

エンジンは起動時に元の `ActionMatrix` アセットを直接編集せず、
`CopyFrom()` でランタイムインスタンスを複製してから編集する。

```csharp
public void CopyFrom(ActionMatrix source)
{
    states.Clear();
    foreach (var src in source.states)
    {
        var copy = new MotionStateData
        {
            motionPair = src.motionPair,
            durationRange = src.durationRange,
            speedCurve = new AnimationCurve(src.speedCurve.keys),
            useOverrideBPM = src.useOverrideBPM,
            overrideBPM = src.overrideBPM,
            nextWeight = src.nextWeight,
            branches = new List<TransitionBranch>()
        };
        foreach (var b in src.branches)
            copy.branches.Add(new TransitionBranch {
                targetStateIndex = b.targetStateIndex, weight = b.weight });
        states.Add(copy);
    }
}
```

採用理由：
- Editor Play Mode で値を書き換えたとき、**Stop後もアセットに値が残ってしまう**
  Unityの罠（=共有SOの永続汚染）を回避
- `AnimationCurve.keys` も新規 `AnimationCurve` でラップする徹底

これは `~/.claude/lessons/` 配下に**教訓レコード #029**（共有マテリアル
書換問題）として記録した教訓と同じパターンで、**ScriptableObject版**として横展開した設計判断。

### 3. 重み付きランダム遷移（Next Weight + Branches）

各状態は

- `nextWeight` — 「次の状態（インデックス順）に進む」確率の相対値
- `branches: List<TransitionBranch>` — 「特定の状態にジャンプする」確率の相対値とターゲット

を持つ。エンジンは滞在時間が来た時点でこれらを正規化（合計100%）して抽選する。

採用理由：
- 単純な線形遷移だと体験が単調になる
- 「8割で次、2割で前に戻る」のような分岐をコード変更なしで調整可能

### 4. イベント駆動の遷移トリガ

状態遷移は **`GameEventHub` の static Action 経由** で要求される。

```csharp
void OnEnable()
{
    GameEventHub.OnNextStateRequest += GoToNextState;
    GameEventHub.OnStateSelect      += PlayState;
    GameEventHub.OnSpeedChangeRequest += OnSpeedChange;
}
```

採用理由：
- VRコントローラ入力・UI操作・自動進行タイマーの**3経路すべて**から遷移要求が
  来うる。エンジンは「誰が要求したか」を知らずに済む
- 単体テストで「イベント発火→状態が変わる」を検証しやすい

詳細は [GameEventHub README](../GameEventHub/README.md) を参照。

---

## 採用しなかった選択肢

### A. Unity Animator State Machine（AnimatorController）

**棄却理由**:
- Animator State は「状態名（文字列キー）」「遷移条件」「Tween」が前提で、
  本作のような「状態に紐づく長尺メタデータ（durationRange / speedCurve /
  branches[] 等）」を載せにくい
- Animator のグラフは AnimatorController アセット内に閉じていて、
  外部からの**ランタイム編集・Excel入出力が困難**
- AnimatorState 数が増えるとグラフがスパゲッティ化する

### B. ハードコードの enum + switch

**棄却理由**:
- 状態を1つ追加するたびに enum / switch / 遷移条件を書き換える必要
- バランス調整のたびに**コードビルド**が必要 → 試行錯誤コストが高い

### C. 純粋な JSON / Excel（Inspectorなし）

**棄却理由**:
- `MotionPair` の **GUID参照** を JSON で保持すると、リネームに弱い
  （Unity の SerializableField + Inspector ドラッグの方が GUID追従が確実）
- 編集 UX が Notepad 級になり、`AnimationCurve` の編集が困難

ただし**Excel I/O は補助機能として実装**してある（プレイヤーが自作のシーケンスを
書ける）。Inspector編集とExcel編集の二重化については
[ExcelPipeline README](../ExcelPipeline/README.md) を参照。

### D. Behavior Tree (NodeCanvas, Behavior Designer 等)

**棄却理由**:
- 本作の遷移はほぼ「タイマー満了 → 次選択」+ 「ランダム分岐」の単純な構造
- Behavior Tree の階層的構造は過剰、学習コストに見合わない
- 同様の理由で **Bolt / Visual Scripting** も不採用

---

## 解決した課題

### バランス調整のスループット
状態あたり10+のパラメータを Inspector 表で並列編集できるため、
1日に数十状態のバランス調整が可能になった。コード変更を伴わないので
ビルド時間も発生しない。

### AIアシスタントへのデータ修正委譲
Claude Code に「フェードカーブを 0→1→0 から 0→1→1→0 に変えて」のような
指示を出すと、SOアセットの YAML を直接編集してくれる。
コードに埋め込んだ定数だと AI に渡せる粒度が粗くなる。

### Editor Play Mode 中の試行錯誤の安全性
ディープコピー方式により、再生中にパラメータを書き換えても
**元のアセットが汚染されない**。試行錯誤後にいい値だったらコピーバック、悪ければ
そのまま破棄。

---

## コード構成

- **`ActionMatrix.cs`** — SO本体。`List<MotionStateData>` を保持。`CopyFrom()` でディープコピー
- **`MotionStateData.cs`** — 1状態の全フィールド。Odin の `[VerticalGroup]` / `[TableColumnWidth]` で表編集UX
- **`TransitionBranch.cs`** — 重み付き遷移分岐 (`targetStateIndex`, `weight`)
- **`MotionPair.cs`** — 2キャラのアニメーション状態ペア + メタデータ（BPM、タグ、`stableId` GUID）
- **`ActionEngine.cs`** — ランタイム再生エンジン。状態タイマー / 速度補間 / 分岐抽選 / Animator駆動

---

## 既知の制約・トレードオフ

- **Odin Inspector 必須**
  - 内容：`[TableList]` 等は Sirenix 製
  - 対処：Odinなしの環境では `[CustomEditor]` を別途書く必要
- **SO の永続汚染リスク**
  - 内容：Editor で SO を直接編集すると残る
  - 対処：`CopyFrom` ディープコピーで対処済み（[教訓レコード #029](../../docs/architecture.md#lessons-学習)）
- **遷移グラフの可視化なし**
  - 内容：`branches[].targetStateIndex` を人間が追跡
  - 対処：今後 GraphView ベースの Visualizer を作る案あり（v1.1.x候補）
- **Animator との同期**
  - 内容：Animator のステート名を文字列でハードコード
  - 対処：`MotionPair.animatorStateName` をSerializeField化済み

---

## 関連システム

- **[GameEventHub](../GameEventHub/README.md)** — 状態遷移のトリガ経路（依存元）
- **[ExcelPipeline](../ExcelPipeline/README.md)** — マトリクスのExcel入出力（双方向）
- **[PersistenceLayer](../PersistenceLayer/README.md)** — 現在のマトリクス選択をセーブ
- **[AutonomyModule](../AutonomyModule/README.md)** — `ActionEngine.GetCurrentSpeed()` を読んで自律挙動を変調

---

## 抜粋コード

- `ActionMatrix.cs`
- `MotionStateData.cs` (`[System.Serializable]` クラス)
- `TransitionBranch.cs`
- `ActionEngine.cs` 抜粋（状態管理コア部分のみ）
