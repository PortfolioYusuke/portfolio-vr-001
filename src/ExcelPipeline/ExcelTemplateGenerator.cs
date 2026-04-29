#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

/// <summary>
/// プレイヤー向け Excel テンプレートを生成する Editor 拡張。
/// 全 <see cref="MotionPair"/> の stableId / 名前 / BPM 等を <c>_Lookup</c> シートに
/// 書き出し、<c>Sequence</c> シートでは VLOOKUP 数式で参照される。
///
/// プレイヤーは Sequence シートに stableId を入力するだけで、
/// 他の列が自動補完される（Excel UX）。
/// </summary>
public static class ExcelTemplateGenerator
{
    private const string TemplateFileName = "_Template.xlsx";

    [MenuItem("Tools/MotionPair/Generate Excel Template")]
    public static void GenerateTemplate()
    {
        // StreamingAssets/MotionPresets/_Template.xlsx に書き出す
        // 配布物に含まれて、初回起動時に exe 横にコピーされる
        string outDir = Path.Combine(Application.streamingAssetsPath, "MotionPresets");
        Directory.CreateDirectory(outDir);
        string outPath = Path.Combine(outDir, TemplateFileName);

        var allPairs = LoadAllMotionPairs();
        WriteTemplate(outPath, allPairs);

        AssetDatabase.Refresh();
        Debug.Log($"[ExcelTemplateGenerator] Template generated: {outPath}");
    }

    private static List<MotionPair> LoadAllMotionPairs()
    {
        var list = new List<MotionPair>();
        string[] guids = AssetDatabase.FindAssets("t:MotionPair");
        foreach (var guid in guids)
        {
            var p = AssetDatabase.LoadAssetAtPath<MotionPair>(AssetDatabase.GUIDToAssetPath(guid));
            if (p != null && !string.IsNullOrEmpty(p.StableId)) list.Add(p);
        }
        return list;
    }

    /// <summary>
    /// テンプレート xlsx を組み立てる。シート構成：
    /// <list type="number">
    /// <item><c>Sequence</c> — プレイヤーが編集する状態リスト。各列に VLOOKUP 数式を埋め込む</item>
    /// <item><c>_Lookup</c> — 全 MotionPair の stableId / pairLabel / BaseBPM 等のメタ表</item>
    /// <item><c>_使い方</c> — テンプレの使い方ヘルプ</item>
    /// </list>
    /// 詳細実装はポートフォリオでは省略（<see cref="ExcelMotionPresetWriter"/> と同じ
    /// xlsx 直接組み立て方式）。
    /// </summary>
    private static void WriteTemplate(string path, List<MotionPair> pairs)
    {
        // Sequence の VLOOKUP 数式の例：
        //
        //   stableId 列  | pairLabel 列                       | BaseBPM 列
        //   -------------+-------------------------------------+--------------------
        //   "abc123…"    | =VLOOKUP(A2, _Lookup!A:C, 2, FALSE) | =VLOOKUP(A2, _Lookup!A:C, 3, FALSE)
        //
        // プレイヤーは A 列に stableId を貼るだけで、他列が自動補完される。
    }
}
#endif
