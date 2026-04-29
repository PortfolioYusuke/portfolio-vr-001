using UnityEngine;

/// <summary>
/// 自律挙動モジュールの基底クラス。<see cref="AutonomyHub"/> が毎フレーム
/// <see cref="Tick"/> を呼び出す。新しいモジュールはこのクラスを継承し、
/// GameObject にアタッチするだけで自動登録される。
///
/// Template Method パターン：基底が共通参照（hub / context）を保持し、
/// サブクラスは Tick だけ実装すれば良い。
/// </summary>
public abstract class AutonomyModule : MonoBehaviour
{
    /// <summary><see cref="AutonomyHub"/> から自動注入される。</summary>
    protected AutonomyHub hub;

    /// <summary>
    /// キャラ単位のコンテキスト。<see cref="UnityEngine.Object.FindObjectOfType{T}()"/> の代わりにこれを使う
    /// （マルチキャラ対応のため、参照のスコープをキャラ単位に閉じる）。
    /// </summary>
    protected CharacterContext context;

    /// <summary>
    /// AutonomyHub から毎フレーム呼ばれる（LateUpdate 相当）。
    /// </summary>
    /// <param name="parameterLevel">主パラメータの値 0-1</param>
    /// <param name="autonomy">自律挙動全体の強度 0-1（UIスライダー）</param>
    /// <param name="fatigue">疲労度 0-1（時間と運動量で蓄積、休息で回復）</param>
    public abstract void Tick(float parameterLevel, float autonomy, float fatigue);

    /// <summary>Hub から呼ばれる初期化。サブクラスで override して追加初期化可能。</summary>
    public virtual void Initialize(AutonomyHub parentHub)
    {
        hub = parentHub;
        context = parentHub.Context;
    }

    // Inspector の enable/disable チェックボックスを表示するために必要。
    void OnEnable() { }
}
