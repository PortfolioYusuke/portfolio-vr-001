# PersistenceLayer — JSON 永続化と複数スロット管理

## 役割

ゲームの進行データ（キャラ設定 / 環境プリセット）を、JSON ファイルとして
ローカルに保存・読み込むレイヤー。

- **5キャラスロット**（固定）でキャラクター設定を保持
- **環境プリセット N スロット**（動的増加可、最大10）で雰囲気・パラメータ・
  座標までを丸ごと保存／復元
- 復元時は SSOT (`AccentToneState` 等) 経由で UI を自動同期

「APK 更新時にもセーブが消えない」「製品版・体験版で別パス（衝突回避）」を
要件として満たす。

---

## 採用したパターン

### 1. JsonUtility による軽量シリアライズ

```csharp
using UnityEngine;
using System.IO;

public class SaveDataManager : MonoBehaviour
{
    private const string ENVIRONMENT_FILE = "environment_presets.json";

    public void SaveEnvironment(int slot, EnvironmentPresetData data)
    {
        var json = JsonUtility.ToJson(data, prettyPrint: false);
        var path = Path.Combine(Application.persistentDataPath, ENVIRONMENT_FILE);
        // ... slot index に書き込み
    }

    public EnvironmentPresetData LoadEnvironment(int slot)
    {
        // ...
        return JsonUtility.FromJson<EnvironmentPresetData>(json);
    }
}
```

採用理由：
- **Newtonsoft.Json** は IL2CPP で AOT問題が起きやすく、AOTGenerationConfig 必要
- **MessagePack-CSharp** は高速だが、デバッグでJSON可視化が困難
- **JsonUtility** は Unity 公式、`[System.Serializable]` で動く、Quest3 で安定

### 2. `[System.Serializable]` DTO による型固定

```csharp
[System.Serializable]
public class EnvironmentPresetData
{
    // 雰囲気系
    public int backgroundIndex = -1;
    public int lightingPresetIndex = -1;
    public float brightness = 1f;
    public float masterVolume, seVolume, voiceVolume;
    public int qualityPresetIndex = -1;
    public bool magicaClothEnabled = true;

    // パラメータ系
    public bool parameterLock;
    public float parameterValue, sensitivity, autonomy, flushLevel;
    public float bodyTransparency;
    public bool subElementVisible = true;
    public bool viewFollowMode;

    // 環境系
    public float strokeMin, strokeMax;
    public float characterScale = 1f;
    public bool uiPitchFollow;

    // 座標系
    public bool hasCoordinates;
    public Vector3 xrOriginPosition, worldRootPosition;
    public Quaternion xrOriginRotation, worldRootRotation;

    // メタ
    public string savedAt;
}
```

採用理由：
- フィールド名が**そのまま JSON キー**になる → デバッグ・人間可読
- `Vector3` / `Quaternion` も Unity が標準でシリアライズ可能
- 新フィールド追加時、既存セーブは**デフォルト値でロード**（互換性維持）

### 3. 環境プリセットの動的スロット数

```csharp
[System.Serializable]
public class EnvironmentSlotsRoot
{
    public List<EnvironmentPresetData> slots = new List<EnvironmentPresetData>();
}

public void AddEnvironmentPresetSlot()
{
    var root = LoadEnvironmentRoot();
    if (root.slots.Count >= MAX_SLOTS) return;
    root.slots.Add(new EnvironmentPresetData { savedAt = "" });
    SaveEnvironmentRoot(root);
}
```

ユーザーが「+ スロットを追加」ボタンを押すと、JSON 配列に空エントリが
追加される。**5枠固定 → 動的伸縮**に変更したのは v1.0.2 から。

### 4. SSOTと連動した復元

復元時は SSOT クラスの `RestoreFromSave` で値を反映：

```csharp
// 例: アクセントトーンの復元
AccentToneState.RestoreFromSave(data.accentTone, data.highlightTone, data.layerIntensity);
ApplyAllTones();  // SSOT変更通知 → 各UI/マテリアルが自動更新
```

UI は `OnChanged` を購読しているので、**ロード後に明示的なUI同期コードを書く必要が無い**。

詳細は [GameEventHub README](../GameEventHub/README.md) の SSOT パターン参照。

### 5. Path 設計（製品版/体験版を別パス化）

セーブ先パスは `%APPDATA%\..\LocalLow\<CompanyName>\<ProductName>\` 配下：

- `environment_presets.json`（環境プリセット全スロット）
- `characters/slot_0.json`, `characters/slot_1.json`, ...（キャラ5スロット）

`Application.persistentDataPath` は productName ベースで決まる。
**製品版と体験版で productName を別文字列**にすることで、
セーブデータが完全分離される。

これにより：
- 製品版で保存したデータが体験版に「持ち込まれる」事故を防止
- 体験版から製品版への移行時、どちらのセーブデータも維持

---

## 採用しなかった選択肢

### A. Newtonsoft.Json

**棄却理由**:
- IL2CPP（Quest3）で AOT 関連で動作不良が起きやすい
- 機能過剰、Quest3 で動かすには `link.xml` 等の調整が必要
- JsonUtility で十分

### B. PlayerPrefs

**棄却理由**:
- 1キーあたり1MB制限
- 暗号化なし（平文）の上に削除も簡単で「ユーザーの編集」を防げない（むしろ不便）
- 多階層の構造化データに不向き

### C. SQLite

**棄却理由**:
- VR セッション内の 5 キャラ + 10プリセット程度のデータ量に過剰
- SQLite-net は AOT 対応で追加設定が必要
- フラットJSONで十分

### D. Cloud Save (Unity Cloud Save / Firebase)

**棄却理由**:
- 個人開発でユーザー認証・サーバー運用は重い
- オフラインで動作しない
- 本作はローカル完結が要件

---

## 解決した課題

### APK 更新時のセーブ消失防止

`Application.persistentDataPath` は APK 上書きインストール時にも保持される
パス（`/storage/emulated/0/Android/data/<package>/files/`）。
v1.0.0 → v1.0.1 → v1.0.2 のアップデートを跨いでセーブが残る。

### 既存セーブの後方互換

新フィールド追加時、`JsonUtility.FromJson` は **存在しないキーを無視**して
**新フィールドはデフォルト値で初期化**してくれる。
v1.0.2 で `accentTone` 等のトーンスライダーフィールドを追加したが、v1.0.1 のセーブもクラッシュせず
ロード可能。

### 部分復元の整合性

「環境プリセット復元時、Page_Parameters タブが非active だと UI 同期が走らず
古い値が残る」というバグを v1.0.2 で発見。
**直接 state を書き換えてから OnRefresh を発火**するパターンに統一して解消。

---

## コード構成

- **`SaveDataManager.cs`** — 中核。JSON I/O、スロット管理、Singleton
- **`CharacterSaveData.cs`** — キャラ DTO（hair/clothing/eye/blendshapes/tones）
- **`EnvironmentPresetData.cs`** — 環境プリセット DTO（24フィールド）

---

## 既知の制約・トレードオフ

- **JsonUtility は private フィールドを扱えない**
  - 内容:DTO は public フィールドのみ
  - 対処:DTO はあくまで**シリアライズ専用クラス**と割り切る
- **暗号化なし**
  - 内容:プレイヤーが手動でJSONを書き換え可能
  - 対処:チート対策ではなくバックアップ容易性を優先
- **環境プリセット最大10**
  - 内容:ハードコード上限
  - 対処:必要なら拡張可能、UI/UX 上は十分
- **マイグレーション機能なし**
  - 内容:フォーマット大変更時の旧データ変換は手動
  - 対処:現状フィールド追加のみで対応可能

---

## 関連システム

- **[GameEventHub](../GameEventHub/README.md)** — `SSOT.RestoreFromSave` 経由で UI 自動同期
- **[ActionMatrix](../ActionMatrix/README.md)** — `ActionMatrix.CopyFrom` でランタイム編集セーブ
- **[OutfitManager](../OutfitManager/README.md)** — `Equip(itemData)` で衣装を復元
- **[DemoBuild](../DemoBuild/README.md)** — productName 切替によるセーブデータ分離

---

## 抜粋コード

- `SaveDataManager.cs`
- `CharacterSaveData.cs`
- `EnvironmentPresetData.cs`
