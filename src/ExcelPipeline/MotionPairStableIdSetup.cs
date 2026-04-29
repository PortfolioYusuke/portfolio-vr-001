#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

/// <summary>
/// 全 <see cref="MotionPair"/> アセットに <c>stableId</c> を一括採番する Editor ツール。
/// 採番済みのアセットは触らないため、何度実行しても安全。
///
/// stableId は <see cref="System.Guid.NewGuid"/> から取得した 12 文字の short GUID。
/// MotionPair をリネームしても変わらないため、Excel I/O 等の永続参照に使える。
/// </summary>
public static class MotionPairStableIdSetup
{
    [MenuItem("Tools/MotionPair/Assign StableIds (All)")]
    public static void AssignAll()
    {
        string[] guids = AssetDatabase.FindAssets("t:MotionPair");
        int updated = 0;

        foreach (var guid in guids)
        {
            var path = AssetDatabase.GUIDToAssetPath(guid);
            var pair = AssetDatabase.LoadAssetAtPath<MotionPair>(path);
            if (pair == null) continue;

            // stableId が空の場合のみ採番（既存値は触らない）
            if (string.IsNullOrEmpty(pair.StableId))
            {
                pair.EnsureStableId();
                updated++;
            }
        }

        AssetDatabase.SaveAssets();
        Debug.Log($"[MotionPairStableIdSetup] StableId assigned: {updated} / {guids.Length}");
    }
}
#endif
