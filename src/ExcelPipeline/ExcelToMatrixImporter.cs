#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Editor 内で Excel ファイルを読み込み、<see cref="ActionMatrix"/> アセットとして
/// プロジェクトに保存する Editor 拡張。プレイヤーが作った Excel をそのまま
/// 開発者プリセット化できる。
/// </summary>
public static class ExcelToMatrixImporter
{
    [MenuItem("Tools/MotionPair/Import xlsx → ActionMatrix asset")]
    public static void ImportSelected()
    {
        string path = EditorUtility.OpenFilePanel("Select xlsx", "MotionPresets", "xlsx");
        if (string.IsNullOrEmpty(path)) return;

        // 全 MotionPair を集めて stableId 解決用辞書のソースにする
        var allPairs = LoadAllMotionPairs();

        var matrix = ExcelMotionPresetLoader.LoadFile(path, allPairs);
        if (matrix == null)
        {
            Debug.LogError($"[ExcelToMatrixImporter] Failed to load: {path}");
            return;
        }

        // 保存先パスを選ばせて、SO アセットとして書き出す
        string savePath = EditorUtility.SaveFilePanelInProject(
            "Save ActionMatrix",
            Path.GetFileNameWithoutExtension(path),
            "asset",
            "Save imported ActionMatrix asset");
        if (string.IsNullOrEmpty(savePath)) return;

        AssetDatabase.CreateAsset(matrix, savePath);
        AssetDatabase.SaveAssets();
        Debug.Log($"[ExcelToMatrixImporter] Saved: {savePath}");
    }

    private static List<MotionPair> LoadAllMotionPairs()
    {
        var list = new List<MotionPair>();
        string[] guids = AssetDatabase.FindAssets("t:MotionPair");
        foreach (var guid in guids)
        {
            var p = AssetDatabase.LoadAssetAtPath<MotionPair>(AssetDatabase.GUIDToAssetPath(guid));
            if (p != null) list.Add(p);
        }
        return list;
    }
}
#endif
