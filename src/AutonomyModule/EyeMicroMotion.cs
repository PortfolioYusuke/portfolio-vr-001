using UnityEngine;

/// <summary>
/// 目の微振動（サッケード）を再現する <see cref="AutonomyModule"/>。
/// 眼球ボーンに Perlin Noise を加算し、人間らしい微細な眼球運動を生成する。
///
/// パラメータ値が高いほど振幅が増えるため、状況に応じた
/// 「目が落ち着かない」表現になる。
/// </summary>
public class EyeMicroMotion : AutonomyModule
{
    [Header("ボーン参照（未設定なら自動検索）")]
    [SerializeField] private Transform leftEyeBone;
    [SerializeField] private Transform rightEyeBone;

    [Header("サッケード設定")]
    [Tooltip("基本振幅（度）")]
    [SerializeField] private float baseAmplitude = 0.18f;
    [Tooltip("パラメータ値最大時の振幅倍率")]
    [SerializeField] private float peakAmplitudeMultiplier = 5.0f;
    [Tooltip("ノイズの速度")]
    [SerializeField] private float noiseSpeed = 3.0f;

    private float _seedLX, _seedLY, _seedRX, _seedRY;

    /// <summary>外部から左右目の独立ノイズを一時停止できるフラグ。</summary>
    [HideInInspector] public bool suppress = false;

    void OnEnable() { } // Inspector チェックボックス表示用

    void Awake()
    {
        // 各軸のノイズシードをずらして同期しないようにする
        _seedLX = Random.Range(0f, 100f);
        _seedLY = Random.Range(100f, 200f);
        _seedRX = Random.Range(200f, 300f);
        _seedRY = Random.Range(300f, 400f);
    }

    public override void Initialize(AutonomyHub parentHub)
    {
        base.Initialize(parentHub);
        AutoFindBones();
    }

    private void AutoFindBones()
    {
        // 自分の GameObject 配下から検索（同シーンに別キャラがいる場合に
        // 別キャラのボーンを拾わないため、検索範囲を限定）
        if (leftEyeBone == null) leftEyeBone = FindBoneRecursive(transform, "LeftEye");
        if (rightEyeBone == null) rightEyeBone = FindBoneRecursive(transform, "RightEye");

        if (leftEyeBone == null || rightEyeBone == null)
            Debug.LogWarning("[EyeMicroMotion] Eye bone が見つかりません。Inspector で手動設定してください。");
    }

    public override void Tick(float parameterLevel, float autonomy, float fatigue)
    {
        if (leftEyeBone == null || rightEyeBone == null) return;
        if (autonomy <= 0f || suppress) return;

        float amplitude = baseAmplitude * Mathf.Lerp(1f, peakAmplitudeMultiplier, parameterLevel) * autonomy;
        float time = Time.time * noiseSpeed;

        // 左目
        float lx = (Mathf.PerlinNoise(time + _seedLX, 0f) - 0.5f) * 2f * amplitude;
        float ly = (Mathf.PerlinNoise(0f, time + _seedLY) - 0.5f) * 2f * amplitude;
        leftEyeBone.localRotation = leftEyeBone.localRotation * Quaternion.Euler(ly, lx, 0f);

        // 右目
        float rx = (Mathf.PerlinNoise(time + _seedRX, 0f) - 0.5f) * 2f * amplitude;
        float ry = (Mathf.PerlinNoise(0f, time + _seedRY) - 0.5f) * 2f * amplitude;
        rightEyeBone.localRotation = rightEyeBone.localRotation * Quaternion.Euler(ry, rx, 0f);
    }

    public static Transform FindBoneRecursive(Transform root, string boneName)
    {
        if (root.name == boneName) return root;
        for (int i = 0; i < root.childCount; i++)
        {
            var result = FindBoneRecursive(root.GetChild(i), boneName);
            if (result != null) return result;
        }
        return null;
    }
}
