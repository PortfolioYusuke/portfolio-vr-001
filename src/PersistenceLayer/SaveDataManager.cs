using UnityEngine;
using System.IO;

/// <summary>
/// セーブデータの中央 I/O ハブ。<see cref="Application.persistentDataPath"/> 配下に
/// JSON ファイルとして保存・読み込みする Singleton。
///
/// 永続パスは productName 依存なので、製品版・体験版で productName を変えれば
/// 自動的にセーブパスが分離される（クロスビルドでデータが混ざる事故を防止）。
/// </summary>
public class SaveDataManager : MonoBehaviour
{
    private static SaveDataManager _instance;
    public static SaveDataManager Instance
    {
        get
        {
            if (_instance == null)
            {
                var go = new GameObject("_SaveDataManager");
                _instance = go.AddComponent<SaveDataManager>();
                DontDestroyOnLoad(go);
            }
            return _instance;
        }
    }

    private const int CHARACTER_SLOT_COUNT = 5;
    private const int MAX_ENVIRONMENT_SLOTS = 10;

    private const string CHARACTER_FILE = "characters.json";
    private const string ENVIRONMENT_FILE = "environment_presets.json";

    void Awake()
    {
        if (_instance != null && _instance != this) { Destroy(gameObject); return; }
        _instance = this;
    }

    // ===========================
    // 1. キャラクターセーブ（5固定スロット）
    // ===========================

    public void SaveCharacter(int slot, CharacterSaveData data)
    {
        if (slot < 0 || slot >= CHARACTER_SLOT_COUNT) return;
        var root = LoadCharacterRoot();
        data.savedAt = System.DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
        data.isEmpty = false;
        root.slots[slot] = data;
        SaveCharacterRoot(root);
    }

    public CharacterSaveData LoadCharacter(int slot)
    {
        if (slot < 0 || slot >= CHARACTER_SLOT_COUNT) return null;
        var root = LoadCharacterRoot();
        return root.slots[slot];
    }

    public CharacterSaveData[] CharacterSlots
    {
        get
        {
            var root = LoadCharacterRoot();
            return root.slots;
        }
    }

    private CharacterSaveSlots LoadCharacterRoot()
    {
        var path = Path.Combine(Application.persistentDataPath, CHARACTER_FILE);
        if (!File.Exists(path)) return new CharacterSaveSlots(CHARACTER_SLOT_COUNT);

        try
        {
            var json = File.ReadAllText(path);
            var root = JsonUtility.FromJson<CharacterSaveSlots>(json);
            // スロット数の前方互換: 古いデータが少なければ追加、多ければ切り詰め
            if (root.slots == null || root.slots.Length != CHARACTER_SLOT_COUNT)
            {
                var fresh = new CharacterSaveSlots(CHARACTER_SLOT_COUNT);
                if (root.slots != null)
                {
                    int copy = Mathf.Min(root.slots.Length, CHARACTER_SLOT_COUNT);
                    for (int i = 0; i < copy; i++) fresh.slots[i] = root.slots[i];
                }
                return fresh;
            }
            return root;
        }
        catch (System.Exception ex)
        {
            Debug.LogWarning($"[SaveDataManager] Failed to load character save: {ex.Message}");
            return new CharacterSaveSlots(CHARACTER_SLOT_COUNT);
        }
    }

    private void SaveCharacterRoot(CharacterSaveSlots root)
    {
        var path = Path.Combine(Application.persistentDataPath, CHARACTER_FILE);
        var json = JsonUtility.ToJson(root, prettyPrint: true);
        File.WriteAllText(path, json);
    }

    // ===========================
    // 2. 環境プリセット（動的スロット数、最大10）
    // ===========================

    public EnvironmentPresetData[] EnvironmentSlots => LoadEnvironmentRoot().slots;
    public int EnvironmentSlotCount => LoadEnvironmentRoot().slots.Length;

    public void SaveEnvironmentPreset(int slot, EnvironmentPresetData data)
    {
        var root = LoadEnvironmentRoot();
        if (slot < 0 || slot >= root.slots.Length) return;
        data.savedAt = System.DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
        data.isEmpty = false;
        root.slots[slot] = data;
        SaveEnvironmentRoot(root);
    }

    public EnvironmentPresetData LoadEnvironmentPreset(int slot)
    {
        var root = LoadEnvironmentRoot();
        if (slot < 0 || slot >= root.slots.Length) return null;
        return root.slots[slot];
    }

    public void DeleteEnvironmentPreset(int slot)
    {
        var root = LoadEnvironmentRoot();
        if (slot < 0 || slot >= root.slots.Length) return;
        root.slots[slot] = new EnvironmentPresetData();
        SaveEnvironmentRoot(root);
    }

    public bool AddEnvironmentPresetSlot()
    {
        var root = LoadEnvironmentRoot();
        if (root.slots.Length >= MAX_ENVIRONMENT_SLOTS) return false;

        var newArray = new EnvironmentPresetData[root.slots.Length + 1];
        System.Array.Copy(root.slots, newArray, root.slots.Length);
        newArray[root.slots.Length] = new EnvironmentPresetData();
        root.slots = newArray;
        SaveEnvironmentRoot(root);
        return true;
    }

    private EnvironmentPresetRoot LoadEnvironmentRoot()
    {
        var path = Path.Combine(Application.persistentDataPath, ENVIRONMENT_FILE);
        if (!File.Exists(path)) return new EnvironmentPresetRoot { slots = new EnvironmentPresetData[5] };

        try
        {
            var json = File.ReadAllText(path);
            var root = JsonUtility.FromJson<EnvironmentPresetRoot>(json);
            if (root.slots == null || root.slots.Length == 0)
                root.slots = new EnvironmentPresetData[5];
            return root;
        }
        catch (System.Exception ex)
        {
            Debug.LogWarning($"[SaveDataManager] Failed to load environment presets: {ex.Message}");
            return new EnvironmentPresetRoot { slots = new EnvironmentPresetData[5] };
        }
    }

    private void SaveEnvironmentRoot(EnvironmentPresetRoot root)
    {
        var path = Path.Combine(Application.persistentDataPath, ENVIRONMENT_FILE);
        var json = JsonUtility.ToJson(root, prettyPrint: true);
        File.WriteAllText(path, json);
    }
}
