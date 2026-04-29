# AutonomyModule — Tickベースの自律挙動システム

## 役割

スクリプトされた動き（モーションキャプチャ・アニメーター駆動）に**重ね合わせる
形で**、キャラクターに微小な自律挙動（瞬き、視線の揺れ、呼吸、微表情、
口の開閉等）を生成するモジュラーシステム。

各モジュールは「1つの挙動だけ」を担当し、`AutonomyHub` が一括 Tick する。
モジュールは**追加・削除がプラグアンドプレイ**で行え、Inspector上で
個別 enable/disable できる。

---

## 採用したパターン

### 1. Template Method + 抽象基底クラス

```csharp
public abstract class AutonomyModule : MonoBehaviour
{
    protected AutonomyHub hub;
    protected CharacterContext context;

    public abstract void Tick(float parameterLevel, float autonomy, float fatigue);

    public virtual void Initialize(AutonomyHub parentHub)
    {
        hub = parentHub;
        context = parentHub.Context;
    }

    void OnEnable() { } // Inspector に enable/disable チェックボックスを表示するため
}
```

各モジュールは `Tick(parameterLevel, autonomy, fatigue)` を実装するだけ。
共通の参照（`hub`, `context`）は基底クラスから注入される。

### 2. Hub による中央オーケストレーション

```csharp
[DefaultExecutionOrder(20000)]
public class AutonomyHub : MonoBehaviour
{
    private List<AutonomyModule> _modules = new List<AutonomyModule>();

    void Start()
    {
        CollectModules();   // 子階層から AutonomyModule を全部集める
        InitializeAll();    // 各モジュールに self を注入
    }

    void LateUpdate()
    {
        var p = parameterManager.CurrentValue;
        UpdateFatigue();
        foreach (var m in _modules)
            if (m.enabled) m.Tick(p, autonomyLevel, Fatigue);
    }
}
```

**実行順序**:
- `[DefaultExecutionOrder(20000)]` で **FinalIK の Look-At より後**に走らせる
- AutonomyHub の Tick は LateUpdate 相当（IKで決まったポーズに微調整を上乗せする）

### 3. Tick 引数の3次元（parameterLevel / autonomy / fatigue）

すべてのモジュールが共通して受ける入力：

- **`parameterLevel`**
  - 範囲：0-1
  - 意味：主パラメータの正規化値
- **`autonomy`**
  - 範囲：0-1
  - 意味：自律挙動全体の強度（UIスライダー、0でほぼ静止）
- **`fatigue`**
  - 範囲：0-1
  - 意味：疲労（時間と運動量で蓄積、休息で回復）

これにより各モジュールは「パラメータが高い局面では強く、疲れたら抑制、ユーザー設定で全体トーン調整」
という共通の応答性をデフォルトで持つ。

### 4. CharacterContext 経由の参照（FindObjectOfType 排除）

```csharp
public override void Initialize(AutonomyHub parentHub)
{
    base.Initialize(parentHub);
    _smr = context.BodySMR;          // CharacterContext経由
    _animator = context.Animator;
}
```

各モジュールは `FindObjectOfType<...>` を使わない。
`CharacterContext` がキャラ単位で参照を集約しているため、
**マルチキャラ対応**の余地がある（同じシーンに2キャラ並べたとき、各々の
AutonomyHubは自分の Context だけを見る）。

### 5. 8モジュールの責務分離

- **`MotionJitter`** — Animator.speed に Perlin Noise を加えて自然な速度ゆらぎ
- **`EyeMicroMotion`** — 眼球ボーンに微小な X/Y 回転 (Perlin)
- **`MicroSaccade`** — 視線のサッカード・凝視・回避（複雑な視線アルゴリズム）
- **`MicroExpression`** — BlendShape の微小オシレーション（瞬き等）
- **`LipSyncModule`** — 口の開閉を音声振幅で駆動（リップシンク）
- **`EmissionFadeModule`** — lilToon `_Emission2ndColor` をパラメータ値で変調
- **`BreathSway`** — 脊椎ボーンを Sin で揺らす（呼吸）
- **`ExpressionController`** — パラメータ値→表情プリセット選択（[ExpressionCascade](../ExpressionCascade/README.md) 参照）

> 本ポートフォリオでは設計判断の代表例として `AutonomyModule` / `AutonomyHub` /
> `EyeMicroMotion` / `MicroSaccade` / `BreathSway` の5本のみ抜粋掲載している。
> 他モジュールは責務一覧として記載のみ。

---

## 採用しなかった選択肢

### A. 1個の `AutonomyController.cs` で全部書く

**棄却理由**:
- 9つの挙動を1ファイルに書くと**1500行+**のクラスになる
- 個別の挙動だけ無効化したい時（デバッグ・パフォーマンス調整）に困る
- Inspector 上で「視線だけ切る」「呼吸だけ強くする」が困難

### B. ECS / DOTS

**棄却理由**:
- Quest 3 単体ターゲットで、8モジュール × 1キャラだけを処理
- ECS の真価は**大量のエンティティ**を扱う時。本作では過剰
- MonoBehaviour ベースの方が他システム（Animator, FinalIK）との結合が容易

### C. ScriptableObject ベースの「挙動アセット」

例: `[CreateAssetMenu] BlinkBehavior : ScriptableObject` で挙動を SO化

**棄却理由**:
- SO はステートレス前提が望ましい。各モジュールはタイマーや乱数シードを持つ
- 結局 SO + ランタイムインスタンス生成で複雑になる
- MonoBehaviour なら「キャラに付けて Inspector で値調整」が直感的

### D. Unity Job System で並列化

**棄却理由**:
- 8モジュール × 1キャラ × 60FPS = 480 Tick/秒。**並列化の必要性が薄い**
- BlendShape / Bone操作はメインスレッド前提（SkinnedMeshRenderer は MainThread API）

---

## 解決した課題

### 「マネキン感」の解消

スクリプトされたモーションだけだと、キャラが**置物っぽく見える**。
人間は本来、瞬きや微表情、視線の揺れ、呼吸の揺れを常に行っている。
本システムはこれらを「数学的ノイズ + 状態応答」で生成する。

### モジュール単位のチューニング

「キャラ A は呼吸を控えめにしたい、視線は強くしたい」のような
キャラ別調整が、Inspector 上でモジュールごとの enable / 値変更で完結する。

### 疲労システムの自然な減衰

```csharp
private void UpdateFatigue()
{
    float dt = Time.deltaTime;
    if (_currentSpeed > restSpeedThreshold)
        Fatigue = Mathf.Min(1f, Fatigue + _currentSpeed * dt * fatigueAccumulationRate);
    else
        Fatigue = Mathf.Max(0f, Fatigue - dt * fatigueRecoveryRate);
}
```

激しいモーションが続くと疲労が貯まり、各モジュールの応答が
徐々に変化する（呼吸が大きくなる、視線が安定しなくなる等）。
ユーザーには「キャラが**生きている**」感覚を提供。

---

## コード構成

本ポートフォリオで抜粋掲載している5ファイル：

- **`AutonomyModule.cs`** — 抽象基底（Tick + Initialize）
- **`AutonomyHub.cs`** — モジュール収集・Tick駆動・疲労管理・特殊状態（ピーク）演出
- **`EyeMicroMotion.cs`** — 眼球ボーン微小回転
- **`MicroSaccade.cs`** — サッカード・凝視・回避
- **`BreathSway.cs`** — 脊椎ボーン Sin Sway

元プロジェクトには他に `MotionJitter` / `MicroExpression` / `LipSyncModule` / `EmissionFadeModule` も存在するが、設計判断の代表例として上記5本に絞った。

---

## 既知の制約・トレードオフ

- **Hub の参照肥大**
  - 内容：`AutonomyHub` が `_saccade`, `_eyeMicroMotion`, `_faceFlush` 等を直接保持
  - 対処：ピーク状態（特殊演出）でハブが各モジュールに直接命令する設計上、ある程度は許容。さらに分離するなら別ハンドラへ
- **MicroSaccade.cs が 464行と肥大**
  - 内容：視線アルゴリズムが複雑（凝視→回避→上目→jitter）
  - 対処：本作の体験価値の中心なので意図的に手を入れた。アルゴリズム分割は v1.x 候補
- **`OnEnable() {}` の空メソッド**
  - 内容：Inspector のチェックボックスを表示するため必要
  - 対処：コメントで意図を明記
- **Animator Write Defaults との競合**
  - 内容：BlendShape を毎フレーム上書きする側との競合
  - 対処：LateUpdate で確実に最終値を上書き ([教訓レコード #011](../../docs/architecture.md#lessons-学習))

---

## 関連システム

- **[GameEventHub](../GameEventHub/README.md)** — 速度変更通知 / ピーク状態通知の購読元
- **[ExpressionCascade](../ExpressionCascade/README.md)** — 表情モジュールの拡張（`AutonomyModule` を継承）
- `CharacterContext` — モジュールから参照されるキャラ単位コンテキスト

---

## 抜粋コード

- `AutonomyModule.cs`
- `AutonomyHub.cs`（抜粋）
- `EyeMicroMotion.cs` / `BreathSway.cs` / `MicroSaccade.cs`（抜粋）
