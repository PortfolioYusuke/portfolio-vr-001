using UnityEngine;
using System.Collections.Generic;
using Sirenix.OdinInspector;

/// <summary>
/// 自律挙動システムの中枢。キャラのルートに1つ配置する。
/// 子階層の <see cref="AutonomyModule"/> を自動収集し、LateUpdate で一括 Tick する。
///
/// <see cref="DefaultExecutionOrderAttribute"/> で IK（FinalIK等）よりも後に実行する：
/// IK で決まったポーズに対して、自律挙動による微小な上書きを最後に乗せるため。
///
/// このファイルはポートフォリオ用にコア部分のみを抜粋している。
/// 実装版にはこのほか「ピーク状態演出」（一時的に各モジュールへ強制値を流すロジック）が含まれる。
/// </summary>
[DefaultExecutionOrder(20000)]
public class AutonomyHub : MonoBehaviour
{
    [Title("コンテキスト")]
    [Tooltip("キャラ単位の参照コンテキスト（同オブジェクトまたは親から自動取得）")]
    [SerializeField] private CharacterContext _context;
    public CharacterContext Context => _context;

    [Title("パラメータ参照")]
    [Tooltip("主パラメータの状態管理（自動取得も可）")]
    [SerializeField] private ParameterStateManager parameterManager;

    [Title("自律挙動パラメータ")]
    [Tooltip("自律挙動レベル。UIスライダーで制御。0=無効、1=最大")]
    [Range(0f, 1f)]
    public float autonomyLevel = 0.75f;

    [Tooltip("疲労度。時間経過・激しい動作で蓄積される。")]
    [ShowInInspector, ReadOnly]
    public float Fatigue { get; private set; }

    [Title("疲労設定")]
    [Tooltip("疲労蓄積係数（速度倍率 × deltaTime × この値）")]
    [SerializeField] private float fatigueAccumulationRate = 0.02f;

    [Tooltip("疲労回復速度（/秒）。穏やかな状態・停止中に適用")]
    [SerializeField] private float fatigueRecoveryRate = 0.01f;

    [Tooltip("この速度以下なら疲労回復モードとみなす")]
    [SerializeField] private float restSpeedThreshold = 0.3f;

    [Title("デバッグ")]
    [ShowInInspector, ReadOnly]
    private int _moduleCount;

    private List<AutonomyModule> _modules = new List<AutonomyModule>();
    private float _currentSpeed;

    void Start()
    {
        // CharacterContext取得（同オブジェクト → 親 → フォールバック）
        if (_context == null) _context = GetComponent<CharacterContext>();
        if (_context == null) _context = GetComponentInParent<CharacterContext>();

        // ParameterStateManager未設定なら Context 経由で自動取得
        if (parameterManager == null)
            parameterManager = _context != null ? _context.ParameterManager : ParameterStateManager.Instance;

        // 子階層から全モジュールを自動収集
        CollectModules();

        // 速度変更イベントを購読（疲労蓄積に使う）
        GameEventHub.OnSpeedMultiplierChanged += OnSpeedChanged;
    }

    void OnDestroy()
    {
        GameEventHub.OnSpeedMultiplierChanged -= OnSpeedChanged;
    }

    private void CollectModules()
    {
        _modules.Clear();
        GetComponentsInChildren<AutonomyModule>(true, _modules);
        foreach (var m in _modules) m.Initialize(this);
        _moduleCount = _modules.Count;
    }

    private void OnSpeedChanged(float multiplier)
    {
        _currentSpeed = multiplier;
    }

    void LateUpdate()
    {
        // 主パラメータと自律レベルを取得
        float parameterLevel = parameterManager != null ? parameterManager.CurrentValue : 0.5f;

        // 疲労を進める
        UpdateFatigue();

        // 各モジュールに同じ3引数を流す（モジュール側はその値で内部状態を変調）
        foreach (var m in _modules)
        {
            if (m == null || !m.enabled) continue;
            m.Tick(parameterLevel, autonomyLevel, Fatigue);
        }
    }

    private void UpdateFatigue()
    {
        float dt = Time.deltaTime;
        if (_currentSpeed > restSpeedThreshold)
        {
            Fatigue = Mathf.Min(1f, Fatigue + _currentSpeed * dt * fatigueAccumulationRate);
        }
        else
        {
            Fatigue = Mathf.Max(0f, Fatigue - dt * fatigueRecoveryRate);
        }
    }
}
