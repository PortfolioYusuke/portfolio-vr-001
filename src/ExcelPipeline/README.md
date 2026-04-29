# ExcelPipeline — Excel駆動のデータ管理（プレイヤーカスタマイズ）

## 役割

ゲームの「行動シーケンス」（[ActionMatrix](../ActionMatrix/README.md) のSO定義）を、
**Excel ファイル（`.xlsx`）として外部編集可能**にするためのパイプライン。

プレイヤーが `StreamingAssets/inbox/` にExcelを置くと自動読込、
編集中のシーケンスは `outbox/` にエクスポートされる。
SOアセットの GUID 参照を残しつつ、データだけを Excel に切り出す独自フォーマット。

このパイプラインは **配布製品にも組み込まれている**ため、ユーザーが
オリジナルの行動シーケンスを作って共有する余地がある。

---

## 採用したパターン

### 1. ExcelDataReader による IL2CPP 互換 .xlsx パーサ

```csharp
using ExcelDataReader;
using System.IO;
using System.Text;

public static class ExcelMotionPresetLoader
{
    public static ActionMatrix LoadFile(string path)
    {
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance); // .NET CoreでShift-JIS対応

        using var stream = File.Open(path, FileMode.Open, FileAccess.Read);
        using var reader = ExcelReaderFactory.CreateReader(stream);
        var dataset = reader.AsDataSet();
        // dataset.Tables["Sequence"] → 各行が1状態
        return BuildMatrix(dataset);
    }
}
```

採用理由：
- **EPPlus** や **OfficeOpenXml** はライセンスが商用無料でない（2018年以降は有料化）
- **NPOI** は機能十分だが、IL2CPP（Quest用ビルド）でリフレクション関連の問題あり
- **ExcelDataReader** はシンプル、ライセンスがゆるい (Apache-2.0)、IL2CPP 動作確認済

### 2. StableId による GUID 参照解決

`MotionPair` SOアセットに以下のフィールドを追加：

```csharp
public class MotionPair : ScriptableObject
{
    [SerializeField, ReadOnly]
    private string stableId;  // GUID (アセット作成時に1度だけ採番)

    public string StableId => stableId;
}
```

Editor拡張で **既存全 MotionPair に StableId を一括採番**。Excel側は
`stableId` 文字列で MotionPair を参照する。

採用理由：
- アセット名（`MotionPair_Slow.asset` 等）でExcel参照すると**リネーム時に壊れる**
- Unity の AssetGUID は AssetDatabase アクセスが必要で、StreamingAssets では使えない
- 専用 stableId フィールドを `[ReadOnly]` で内部固定 → リネーム耐性あり

### 3. Excel テンプレート生成（VLOOKUP方式）

```csharp
public class ExcelTemplateGenerator
{
    public static void GenerateTemplate(List<MotionPair> motions)
    {
        // _Lookup シート: stableId, name, BPM等のメタを書き出し
        // Sequence シート: VLOOKUP で _Lookup を参照する数式を埋め込み
        // _使い方 シート: ユーザー向けヘルプ
    }
}
```

ユーザーは `Sequence` シートで `MotionId` 列に stableId を入れるだけで、
他の列が VLOOKUP で自動補完される（Excel的UX）。

### 4. 衝突時の自動リネーム + .bak バックアップ

書き出し時、同名ファイルが存在する場合：

```csharp
public string WriteWithAutoRename(string baseName, ActionMatrix matrix)
{
    string path = Path.Combine(outboxDir, baseName + ".xlsx");
    if (File.Exists(path))
    {
        // 1. 既存を .bak にバックアップ
        File.Move(path, path + $".bak.{DateTime.Now:yyyyMMddHHmmss}");
        // 2. 新ファイルを連番で書き出し
        path = GetUniquePath(baseName);
    }
    Write(path, matrix);
    return path;
}
```

採用理由：
- 書き出しで**ユーザーの編集中ファイルを上書き**する事故を防止
- バックアップ後ろの `.bak.20260418123045` で経時的な戻し可能

### 5. 読込元追跡（上書き保存 vs 別名保存）

ランタイムで Excel 読み込み → SOマトリクスをCopyFrom → 編集 → 保存時に
「最初に開いたパスへ上書きするか、別名保存するか」を選択可能。

```csharp
public enum MatrixSource { InGameOnly, DeveloperPreset, PlayerCustomFile }
private MatrixSource _currentSource;
private string _currentFilePath;
```

UI 上で **読込元タイプ別にボタン色を変更**：
- 開発者プリセット（読み込み専用）→ 「リロード」のみ表示
- プレイヤーカスタム → 「上書き保存 / 別名保存」表示
- ゲーム内のみ（未保存）→ 「Excel書き出し」表示

---

## 採用しなかった選択肢

### A. JSON フォーマット

**棄却理由**:
- 数値・文字列の編集UIが Notepad 級
- VLOOKUP 等の Excel 機能を活用できない
- 表計算アプリでの編集体験が ExcelPipeline の核

### B. Google Spreadsheet API

**棄却理由**:
- ネット接続必須化（オフラインで遊べなくなる）
- API key 配布の問題
- 個人開発でクラウド連携は重い

### C. CSV

**棄却理由**:
- 複数シート（_Lookup, Sequence, _使い方）に分けたいユースケースに不適
- VLOOKUP 等の数式を持てない

### D. EPPlus（有料）

**棄却理由**:
- Apache 2.0 → LGPL に移行（2018）→ 商用利用は有料ライセンス必要
- ExcelDataReader で要件は満たせる

---

## 解決した課題

### バランス調整のスループット爆上げ

100行を超える状態定義を Inspector で1行ずつ編集するより、Excel で
一括コピペ・行挿入・並び替えできる方が**10倍以上高速**。

### プレイヤーコミュニティの育成余地

`StreamingAssets/inbox/` に Excel ファイルを置くだけで動作するため、
ユーザー間で Excel ファイルを共有する文化が育ちうる。

### リネーム耐性

stableId 方式により、SOアセットを Asset/Frequencies/ 等にフォルダ移動しても
Excel 側のリンクが切れない。

---

## コード構成

- **`ExcelMotionPresetLoader.cs`** — Excel → ActionMatrix
- **`ExcelMotionPresetWriter.cs`** — ActionMatrix → Excel + 衝突時リネーム
- **`ExcelTemplateGenerator.cs` (Editor)** — テンプレ生成（_Lookup + Sequence + _使い方）
- **`ExcelToMatrixImporter.cs` (Editor)** — Editor内での Excel → SO アセット変換
- **`MotionPairStableIdSetup.cs` (Editor)** — 全 MotionPair に StableId 一括採番

---

## 既知の制約・トレードオフ

- **ExcelDataReader 依存**
  - 内容:サードパーティ依存
  - 対処:Apache-2.0 ライセンス、安定
- **StreamingAssets パスの Quest3 制約**
  - 内容:Android では一部 read-only
  - 対処:`Application.persistentDataPath` への展開でカバー
- **AnimationCurve のExcel表現困難**
  - 内容:曲線はテキストで表現しにくい
  - 対処:速度カーブを `SpeedCurvePresets` で名前参照する設計に変更
- **大量行の処理速度**
  - 内容:1000行以上で読み込み数百ms
  - 対処:一般プレイヤーは100行以下なので実害なし

---

## 関連システム

- **[ActionMatrix](../ActionMatrix/README.md)** — 入出力対象のSOアセット
- **[PersistenceLayer](../PersistenceLayer/README.md)** — 「現在のExcel読込元パス」を保存

---

## 抜粋コード

- `ExcelMotionPresetLoader.cs`
- `ExcelMotionPresetWriter.cs`
- `ExcelTemplateGenerator.cs`（Editor抜粋、サンプル数式部分）
