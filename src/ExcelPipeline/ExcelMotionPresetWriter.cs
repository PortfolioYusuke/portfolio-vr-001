using UnityEngine;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Text;

/// <summary>
/// <see cref="ActionMatrix"/> を <c>.xlsx</c> ファイルに書き出すランタイムライター。
/// 衝突時の自動リネーム、<c>.bak</c> バックアップ付き上書きに対応。
///
/// xlsx は ZIP コンテナの中身（XML 群）を <see cref="ZipArchive"/> で直接組み立てる方式。
/// EPPlus 等の有料ライブラリに依存しない実装。
/// </summary>
public static class ExcelMotionPresetWriter
{
    /// <summary>exe 横の MotionPresets フォルダ（Loader と同じ場所）。</summary>
    private static readonly string PresetsDir = Path.Combine(
        Path.GetDirectoryName(Application.dataPath), "MotionPresets");

    /// <summary>新規書き出し。ファイル名衝突時は (1), (2)... を付与。</summary>
    /// <returns>書き出されたフルパス。</returns>
    public static string ExportNew(ActionMatrix matrix, IReadOnlyList<MotionPair> allMotionPairs)
    {
        Directory.CreateDirectory(PresetsDir);

        string baseName = GenerateAutoName(matrix);
        string path = GetUniqueFilePath(baseName);

        WriteXlsx(path, matrix, allMotionPairs);
        Debug.Log($"[ExcelWriter] Exported: {path}");
        return path;
    }

    /// <summary>上書き保存。元ファイルを .bak にリネームしてから書き出し。</summary>
    public static void Overwrite(string existingPath, ActionMatrix matrix, IReadOnlyList<MotionPair> allMotionPairs)
    {
        if (File.Exists(existingPath))
        {
            string bakPath = existingPath + ".bak";
            if (File.Exists(bakPath)) File.Delete(bakPath);
            File.Move(existingPath, bakPath);
        }

        WriteXlsx(existingPath, matrix, allMotionPairs);
        Debug.Log($"[ExcelWriter] Overwritten: {existingPath}");
    }

    /// <summary>指定パスへ直接書き出し（外部からも呼べる API）。</summary>
    public static void ExportTo(string path, ActionMatrix matrix, IReadOnlyList<MotionPair> allMotionPairs)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path));
        WriteXlsx(path, matrix, allMotionPairs);
    }

    // ===========================
    // ファイル名衝突回避
    // ===========================

    private static string GenerateAutoName(ActionMatrix matrix)
    {
        string ts = System.DateTime.Now.ToString("yyyyMMdd-HHmmss");
        return $"Custom_{ts}";
    }

    private static string GetUniqueFilePath(string baseName)
    {
        string path = Path.Combine(PresetsDir, baseName + ".xlsx");
        int suffix = 1;
        while (File.Exists(path))
        {
            path = Path.Combine(PresetsDir, $"{baseName} ({suffix}).xlsx");
            suffix++;
        }
        return path;
    }

    // ===========================
    // xlsx 生成本体（概念実装）
    // ===========================

    /// <summary>
    /// xlsx ファイル（実体は ZIP コンテナ）を組み立てて書き出す。
    /// 必要な XML パーツ:
    /// <list type="bullet">
    /// <item><c>[Content_Types].xml</c></item>
    /// <item><c>_rels/.rels</c></item>
    /// <item><c>xl/workbook.xml</c></item>
    /// <item><c>xl/_rels/workbook.xml.rels</c></item>
    /// <item><c>xl/worksheets/sheet1.xml</c>（Sequence シート）</item>
    /// <item><c>xl/worksheets/sheet2.xml</c>（_Lookup シート）</item>
    /// <item><c>xl/sharedStrings.xml</c></item>
    /// </list>
    /// 元実装は約 350 行で、各 XML を文字列ビルダで生成して ZipArchive に追加します。
    /// </summary>
    private static void WriteXlsx(string path, ActionMatrix matrix, IReadOnlyList<MotionPair> allMotionPairs)
    {
        using var zipStream = File.Create(path);
        using var archive = new ZipArchive(zipStream, ZipArchiveMode.Create);

        // 各 XML パーツを順次書き出す。詳細はポートフォリオでは省略。
        // - Sequence シート: stableId / Duration / BPM / nextWeight / branches を1行ずつ
        // - _Lookup シート: 全 MotionPair の stableId / pairLabel / BaseBPM 等
        // - sharedStrings: 出現する文字列を集約してインデックス参照
    }
}
