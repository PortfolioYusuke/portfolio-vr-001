using UnityEngine;
using System.Collections.Generic;
using System.IO;
using System.Text;
using ExcelDataReader;

/// <summary>
/// <c>MotionPresets/*.xlsx</c> を読み込んで <see cref="ActionMatrix"/> に変換する
/// ランタイムローダー。
///
/// フォルダ構成（exe 横に配置）:
/// <code>
///   {exe}/MotionPresets/             ← プレイヤーのプリセット + テンプレート
///   {exe}/MotionPresets/サンプル/    ← 開発者プリセット（参考、読み込み対象外）
/// </code>
///
/// このファイルはポートフォリオ用にコア部分のみ抜粋した版です。
/// </summary>
public static class ExcelMotionPresetLoader
{
    public struct LoadedPreset
    {
        public string name;      // ファイル名（拡張子なし）= プリセット表示名
        public string filePath;  // フルパス
        public ActionMatrix matrix;
    }

    /// <summary>exe 横の MotionPresets フォルダ（Editor 時はプロジェクトルート横）。</summary>
    private static readonly string PresetsRoot = Path.Combine(
        Path.GetDirectoryName(Application.dataPath), "MotionPresets");

    public static string RootPath => PresetsRoot;

    /// <summary>必要ディレクトリの作成 + テンプレートの初回コピー。</summary>
    public static void EnsureDirectories()
    {
        Directory.CreateDirectory(PresetsRoot);
        var templateSrc = Path.Combine(Application.streamingAssetsPath, "MotionPresets", "_Template.xlsx");
        var templateDst = Path.Combine(PresetsRoot, "_Template.xlsx");
        if (File.Exists(templateSrc) && !File.Exists(templateDst))
        {
            File.Copy(templateSrc, templateDst);
        }
    }

    /// <summary>MotionPresets/*.xlsx を全部読み込み（サブフォルダは対象外）。</summary>
    public static List<LoadedPreset> LoadAll(IReadOnlyList<MotionPair> motionPairs)
    {
        var result = new List<LoadedPreset>();
        EnsureDirectories();
        if (!Directory.Exists(PresetsRoot)) return result;

        var files = Directory.GetFiles(PresetsRoot, "*.xlsx");
        foreach (var file in files)
        {
            var fileName = Path.GetFileNameWithoutExtension(file);
            if (fileName.StartsWith("_") || fileName.StartsWith("~$")) continue; // テンプレ・一時

            try
            {
                var matrix = LoadFile(file, motionPairs);
                if (matrix != null)
                    result.Add(new LoadedPreset { name = fileName, filePath = file, matrix = matrix });
            }
            catch (System.Exception ex)
            {
                Debug.LogWarning($"[ExcelLoader] Failed to load {fileName}: {ex.Message}");
            }
        }
        return result;
    }

    /// <summary>1ファイル読み込んで ActionMatrix を生成。</summary>
    public static ActionMatrix LoadFile(string path, IReadOnlyList<MotionPair> motionPairs)
    {
        // .NET Core 環境で Shift-JIS を扱うため CodePagesEncodingProvider を登録
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);

        using var stream = File.Open(path, FileMode.Open, FileAccess.Read);
        using var reader = ExcelReaderFactory.CreateReader(stream);
        var dataset = reader.AsDataSet();

        var sequenceTable = dataset.Tables["Sequence"];
        if (sequenceTable == null) return null;

        var matrix = ScriptableObject.CreateInstance<ActionMatrix>();
        var pairLookup = BuildStableIdLookup(motionPairs);

        // Sequence シートの各行を 1 状態としてパース
        for (int i = 1; i < sequenceTable.Rows.Count; i++) // 1行目はヘッダ
        {
            var row = sequenceTable.Rows[i];
            string stableId = row[0]?.ToString();
            if (string.IsNullOrEmpty(stableId)) continue;

            if (!pairLookup.TryGetValue(stableId, out var motionPair)) continue;

            var state = new MotionStateData
            {
                motionPair = motionPair,
                durationRange = ParseVector2(row[1]?.ToString(), 10f, 20f),
                speedCurve = AnimationCurve.Linear(0, 1, 1, 1),
                useOverrideBPM = ParseBool(row[2]?.ToString()),
                overrideBPM = ParseFloat(row[3]?.ToString(), 0f),
                nextWeight = ParseFloat(row[4]?.ToString(), 100f),
                branches = ParseBranches(row[5]?.ToString())
            };
            matrix.states.Add(state);
        }

        return matrix;
    }

    private static Dictionary<string, MotionPair> BuildStableIdLookup(IReadOnlyList<MotionPair> pairs)
    {
        var dict = new Dictionary<string, MotionPair>();
        if (pairs == null) return dict;
        foreach (var p in pairs)
        {
            if (p == null || string.IsNullOrEmpty(p.StableId)) continue;
            dict[p.StableId] = p;
        }
        return dict;
    }

    // ---- パーサ補助（簡略化） ----
    private static Vector2 ParseVector2(string s, float defMin, float defMax) =>
        new Vector2(defMin, defMax); // 元実装: "min,max" 形式を解析

    private static bool ParseBool(string s) => s == "1" || s == "TRUE" || s == "ON";
    private static float ParseFloat(string s, float def) => float.TryParse(s, out var v) ? v : def;
    private static List<TransitionBranch> ParseBranches(string s) => new List<TransitionBranch>();
}
