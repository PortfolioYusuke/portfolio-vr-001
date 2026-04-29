# ExpressionCascade — DOTween による部位別カスケード遷移と部分ロック

## 役割

キャラの表情を、パラメータ値に応じて自動的にプリセット間で遷移させるシステム。
単純な「全 BlendShape を一括 LerpToTarget」ではなく、

- **部位別（目・眉・口・頬）にずらして遷移**することで自然な印象を演出
- 各 BlendShape の遷移時間に**ランダム揺らぎ**を加えて生き物らしさを演出
- 一部の部位を **外部システムから一時ロック**できる仕組みで他システム（接触演出・特殊状態演出等）と協調

を実現している。`AutonomyModule` を継承し、`AutonomyHub` から Tick される。

---

## 採用したパターン

### 1. 部位別カスケード（cascadeDelay 段階遷移）

```csharp
[SerializeField] private float cascadeDelay = 0.5f;     // 部位ごとの遅延
[SerializeField] private float tweenDuration = 8f;      // 遷移時間
[SerializeField] private float tweenDurationJitter = 1f; // ランダム幅（±秒）
[SerializeField] private Ease tweenEase = Ease.InOutSine;
```

遷移開始時、部位の順序を `Eyes → Eyebrows → Mouth → Cheeks` で並べ、
各部位の遷移開始時刻を `cascadeDelay × index` だけずらす。

採用理由：
- 顔のすべての BlendShape が**同時に動く**と機械的に見える
- 人間は表情変化時、目の動きが先行し、口や頬は遅れて追従する
- DOTween の `Sequence` で時間軸を組み立てると簡潔

### 2. ジッタ付き遷移時間

各 BlendShape の遷移時間は

```
duration = tweenDuration + Random.Range(-tweenDurationJitter, tweenDurationJitter)
```

で個別に決まる。同じ部位内でも shape ごとにわずかに速度が違う。

採用理由：
- 機械的な「全 BlendShape が同じ秒数で動く」を回避
- 視覚的に自然な遷移感

### 3. ScriptableObject による表情プロファイル

```csharp
public class ExpressionProfile : ScriptableObject
{
    public ExpressionMapping[] mappings;

    [System.Serializable]
    public class ExpressionMapping
    {
        public string label;
        public float parameterMin;          // 0.4 等
        public float parameterMax = 1f;     // 0.6 等
        public ExpressionPreset[] candidates; // 候補（ランダム選択）
        public float switchInterval = 15f;
    }
}

public class ExpressionPreset : ScriptableObject
{
    public string sourceAnimName;
    public string category;
    public BlendShapeEntry[] shapes;

    public enum FacePartGroup { Eye, Eyebrow, Mouth, Cheek, Other }

    [System.Serializable]
    public class BlendShapeEntry
    {
        public string shapeKey;
        public float baseValue;
        public float randomRange;
        public FacePartGroup partGroup;
    }
}
```

各 `ExpressionMapping` は「パラメータ値 0.4〜0.6 の時にどの候補プリセットを採用するか」を
保持。**candidates 配列にプリセットが複数ある場合はランダム選択**。
プリセット内の各 BlendShape は **partGroup** 属性を持ち、カスケード対象。

### 4. 部位別ロック機構

```csharp
private readonly HashSet<ExpressionPreset.FacePartGroup> _lockedGroups
    = new HashSet<ExpressionPreset.FacePartGroup>();

public void LockGroup(ExpressionPreset.FacePartGroup group) => _lockedGroups.Add(group);
public void UnlockGroup(ExpressionPreset.FacePartGroup group) => _lockedGroups.Remove(group);
public bool IsGroupLocked(ExpressionPreset.FacePartGroup group) => _lockedGroups.Contains(group);
```

例えば接触演出中は **`Mouth` グループだけロック**して、演出用 BlendShape を
維持しつつ目・眉・頬は通常の自動遷移を続ける、といった協調が可能。

外部システム（接触演出コントローラ等）が `LockGroup(Mouth)` し、解除時に `UnlockGroup` する。

### 5. 瞬きレイヤーの動的抑制

Animator の "Blink" レイヤーの weight をパラメータ値に応じて減らす：

```csharp
// パラメータ値0.4以下: weight=1（通常）
// パラメータ値0.4〜0.9: 線形に減衰
// パラメータ値0.9以上: weight=blinkMinWeight（例: 0.2）
float t = Mathf.InverseLerp(blinkSuppressStart, blinkSuppressMax, parameterLevel);
float weight = Mathf.Lerp(1f, blinkMinWeight, t);
_animator.SetLayerWeight(_blinkLayerIdx, weight);
```

採用理由：パラメータが高い局面で瞬きが減るのは自然な反応。表情とは別レイヤーの
仕組みで実現することで、Tween と独立して制御できる。

### 6. DOTween Sequence による単一 Tween 管理

```csharp
private Sequence _activeSequence;

public void TransitionTo(ExpressionPreset preset)
{
    _activeSequence?.Kill();   // 前の遷移を中断
    _activeSequence = DOTween.Sequence();
    foreach (var entry in preset.shapes)
    {
        if (_lockedGroups.Contains(entry.group)) continue;
        float delay = GetCascadeDelay(entry.group);
        float dur = tweenDuration + Random.Range(-jitter, jitter);
        _activeSequence.Insert(delay, DOTween.To(...).SetEase(tweenEase));
    }
}
```

採用理由：DOTween の `Sequence.Kill()` で**複数同時 Tween を一括中断**
できる。手動で `coroutine.StopCoroutine` を集めるより簡潔。

---

## 採用しなかった選択肢

### A. Animator の Blend Tree

**棄却理由**:
- BlendTree は数値パラメータ→アニメーション補間に最適化されており、
  個別の BlendShape 値の指定には不向き
- 部位別ロックや shape 別ジッタが困難

### B. AnimationClip による表情アニメ

**棄却理由**:
- パラメータ値→表情マッピングが固定化、調整時に AnimationClip を作り直す必要
- ランタイムでの ScriptableObject による値変更が効かない

### C. Unity の Coroutine ベース手書き Tween

**棄却理由**:
- 部位別ジッタ・カスケード遅延・Sequence 中断を Coroutine で書くと
  100行以上のステートマシン化
- DOTween で 30行以下に圧縮可能

### D. 顔リグ＋Blendshape Driver パターン（VTuber系ツール）

**棄却理由**:
- 本作は「パラメータ値→表情を自動切替」が中心。VTuber 用の Blendshape Driver は
  「外部入力（マイク・カメラ）→Blendshape」が前提でユースケース不一致

---

## 解決した課題

### 「マネキン顔」感の解消

単純に BlendShape を瞬時に切り替えると、表情が**カクッと**変わって不自然。
カスケード + ジッタにより、視覚的な滑らかさを担保。

### システム間の協調（接触系・特殊状態演出）

部位別ロック機構により、接触演出システムが口だけ占有しても、目や頬は
ExpressionController の自動制御が継続する。**システム間の干渉を避けつつ
独立した制御**が可能。

### パラメータ値の連続的反映

`ExpressionProfile.mappings` に複数の `parameterMin/parameterMax` 帯を定義することで、
パラメータ値の連続値に対応した表情切替を実現。ハードコード分岐より調整が容易。

---

## コード構成

- **`ExpressionController.cs`** — `AutonomyModule` 派生。Tween駆動・ロック管理
- **`ExpressionProfile.cs`** — SO: パラメータ値→表情マッピング
- **`ExpressionPreset.cs`** — SO: 1表情のBlendShape値セット + 部位グループ

---

## 既知の制約・トレードオフ

- **DOTween 必須**
  - 内容：Sequence/Tween に DOTween を使用
  - 対処：標準ライブラリではないが、無料版でカバー範囲十分
- **BlendShape 名のハードコード**
  - 内容：`eye_close` 等の固有名がコード内にある
  - 対処：多モデル対応時に再考必要
- **オーバーレイ系（涙、頬染め等）の除外リスト**
  - 内容：別系統のBlendShapeをスキップする `EXCLUDE_NAMES` に依存
  - 対処：命名規約で半自動的に分離（option_*プレフィックス + 日本語複合マクロ）
- **遷移中のパラメータ値急変**
  - 内容：Tween 中に範囲が変わると次の遷移が遅延
  - 対処：`_nextSwitchTime` で最低保持時間を担保

---

## 関連システム

- **[AutonomyModule](../AutonomyModule/README.md)** — 継承元
- 接触演出コントローラ（非掲載）— `LockGroup(Mouth)` を呼ぶ外部システム
- `AutonomyHub` — ピーク状態時に表情オーバーライド要求

---

## 抜粋コード

- `ExpressionController.cs`
- `ExpressionProfile.cs`
- `ExpressionPreset.cs`
