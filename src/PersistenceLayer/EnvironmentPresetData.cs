using System;
using UnityEngine;

/// <summary>
/// 環境プリセット保存データ（24項目）。雰囲気 / パラメータ / 環境 / 座標の
/// 4 グループからなる。1 スロット 1 インスタンスでシリアライズされる。
///
/// 設計のポイント：
/// - <see cref="UnityEngine.JsonUtility"/> 互換のため public フィールドのみ
/// - <see cref="Vector3"/> / <see cref="Quaternion"/> も Unity が標準でシリアライズ可能
/// - 新フィールド追加は既存セーブと**後方互換**（古い JSON はデフォルト値で読まれる）
/// </summary>
[Serializable]
public class EnvironmentPresetData
{
    // --- メタ ---
    public bool isEmpty = true;
    public string savedAt;        // 保存日時（表示用）
    public string slotName;       // スロット名

    // --- 雰囲気系 ---
    public int backgroundIndex = -1;
    public int lightingPresetIndex = -1;
    public float brightness = 1f;
    public float masterVolume;
    public float seVolume;
    public float voiceVolume;
    public int qualityPresetIndex = -1;
    public bool magicaClothEnabled = true;

    // --- パラメータ系 ---
    public bool parameterLock;
    public float parameterValue;
    public float sensitivity;
    public float autonomy;
    public float flushLevel;
    public float bodyTransparency;
    public bool subElementVisible = true;
    public bool viewFollowMode;

    // --- 環境系 ---
    public float strokeMin;
    public float strokeMax;
    public float characterScale = 1f;
    public bool uiPitchFollow;

    // --- 座標系（VR 視点が大きく動く可能性があるため、復元時はフェード演出を入れる） ---
    public bool hasCoordinates;
    public Vector3 xrOriginPosition;
    public Quaternion xrOriginRotation;
    public Vector3 worldRootPosition;
    public Quaternion worldRootRotation;
}

/// <summary>環境プリセットスロットの配列ラッパー（JsonUtility 用）。</summary>
[Serializable]
public class EnvironmentPresetRoot
{
    public EnvironmentPresetData[] slots = Array.Empty<EnvironmentPresetData>();
}
