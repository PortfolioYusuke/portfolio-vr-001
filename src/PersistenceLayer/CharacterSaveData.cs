using System;

/// <summary>
/// キャラクター設定の保存データ。髪・服・メッシュ ON/OFF・目の色・体型 BlendShape・
/// トーン値。<see cref="UnityEngine.JsonUtility"/> でシリアライズ可能な構造。
///
/// 新フィールド追加時、JsonUtility は古い JSON に**存在しないキーを無視**し、
/// **新フィールドはデフォルト値で初期化**してくれる。これにより
/// バージョン跨ぎの後方互換が手間なく成立する。
/// </summary>
[Serializable]
public class CharacterSaveData
{
    /// <summary>空スロットかどうか。</summary>
    public bool isEmpty = true;
    /// <summary>保存日時（表示用）。</summary>
    public string savedAt;
    /// <summary>スロット名（ユーザーが自由に設定）。</summary>
    public string slotName;

    // --- 髪型 ---
    public string hairItemName;
    public int hairColorIndex;

    // --- 服装 ---
    public string clothingItemName;

    // --- メッシュ ON/OFF ---
    public string[] disabledMeshNames = Array.Empty<string>();

    // --- 目の色 ---
    public string eyeGroupName;
    public int eyeTextureIndex;

    // --- 体型 BlendShape ---
    public BlendShapeEntry[] blendShapes = Array.Empty<BlendShapeEntry>();

    // --- トーンスライダー（v1.0.2 で追加） ---
    /// <summary>主トーンスライダー値（0-1）。</summary>
    public float accentTone = 0f;
    /// <summary>ハイライトトーンスライダー値（0-1）。</summary>
    public float highlightTone = 0f;
    /// <summary>レイヤー強度スライダー値（0-1）。デフォルトはマテリアル初期値に合わせる。</summary>
    public float layerIntensity = 0.621f;
}

[Serializable]
public class BlendShapeEntry
{
    public string shapeName;
    public float value;
}

/// <summary>キャラ保存スロットの配列ラッパー（JsonUtility 用）。</summary>
[Serializable]
public class CharacterSaveSlots
{
    public CharacterSaveData[] slots;

    public CharacterSaveSlots() { }
    public CharacterSaveSlots(int count)
    {
        slots = new CharacterSaveData[count];
        for (int i = 0; i < count; i++)
            slots[i] = new CharacterSaveData();
    }
}
