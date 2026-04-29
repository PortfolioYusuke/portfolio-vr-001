# OutfitManager — 衣装動的着替え + 物理（クロス）連携

## 役割

VR空間内のキャラに対して、衣装・髪型を**ランタイムで動的に着脱**するシステム。
prefabのインスタンス化、ボーン再ペアレント、MagicaCloth2のクロスシミュレーション
コライダー設定、テクスチャ swap までを一括管理する。

「服を変える → 各所のSMRがキャラのアーマチュアに正しく追従し、髪は揺れ、
ボディシェイプも追従する」を1関数 `Equip(ItemData)` で実現する。

---

## 採用したパターン

### 1. ItemData (SO) によるデータ駆動の着替え定義

```csharp
[CreateAssetMenu(fileName = "Item_New", menuName = "Outfit/ItemData")]
public class ItemData : ScriptableObject
{
    public string itemName;
    public GameObject prefab;
    public ItemCategory category;          // Hair / Clothing / FullBody
    public CharacterProfile.BoneTarget attachTo;  // どのボーンに付けるか
    public string textureProperty;         // 髪色swap用 (例: "_BaseMap")
}
```

UI からは「ItemData の List」を見るだけで切替できる：

```csharp
// 例: ItemSelector の onValueChanged
outfitManager.Equip(items[selectedIndex]);
```

### 2. CharacterProfile によるボーンマップ抽象化

```csharp
public class CharacterProfile : MonoBehaviour
{
    public enum BoneTarget {
        FullBody, Head, Hips, Chest, Spine,
        LeftHand, RightHand, LeftLowerLeg, RightLowerLeg
    }

    public Transform GetBone(BoneTarget target);
}
```

衣装側は「Headボーンに追従させたい」と書けば、`CharacterProfile` が
そのキャラのHead相当ボーンを返す。**完全一致優先 + 末尾サフィックス検索**で
キャラ違い（命名規則の差）にも対応。

### 3. MagicaCloth2 の遅延コライダー設定

クロスシミュレーション (MagicaCloth2) は、**コライダーが先に存在している前提**で
ビルドされる。Instantiate 時に自動ビルドされてしまうと、後からコライダーを
追加しても効かない。

そのため：

```csharp
// 非アクティブでInstantiate → MagicaClothの自動ビルドを防止
bool prefabWasActive = data.prefab.activeSelf;
data.prefab.SetActive(false);
GameObject instance = Instantiate(data.prefab);
data.prefab.SetActive(prefabWasActive);
```

非アクティブで生成 → ボーンマップを作成 → コライダー設定 → 最後に SetActive(true) で
クロスをビルドさせる、という順序を厳守。

### 4. ボーンリーク対策（PortUniqueBones）

衣装FBXにはキャラのアーマチュアと**同名ボーンが含まれる**ことがある。
naive にインスタンス化すると、Unityのボーンマッチングがキャラ側ボーンと
衣装ボーンを混同して、見えない場所に「衣装専用ボーン」がリークする。

対処：衣装ファイル内の **ユニークボーン**（キャラに無いボーン）だけを
キャラのアーマチュアに移植。それ以外のボーンは破棄（捨てて、SkinnedMeshRenderer の
`bones[]` をキャラ側ボーンに付け替える）。

```csharp
private List<Transform> PortUniqueBones(GameObject instance)
{
    // 衣装SMR.bones を走査し、キャラ側に存在しないボーンだけを移植
    var ported = new List<Transform>();
    foreach (var smr in instance.GetComponentsInChildren<SkinnedMeshRenderer>())
    {
        var newBones = new Transform[smr.bones.Length];
        for (int i = 0; i < smr.bones.Length; i++)
        {
            var origName = smr.bones[i].name;
            var charBone = profile.FindBoneByName(origName);
            if (charBone != null) newBones[i] = charBone;          // キャラ側に存在 → 流用
            else { ported.Add(smr.bones[i]); newBones[i] = smr.bones[i]; } // 衣装独自 → 移植
        }
        smr.bones = newBones;
    }
    return ported;
}
```

`Unequip` 時には `_portedBones[category]` で**そのカテゴリで移植したボーンだけ**を
追跡しているので、複数衣装を着脱しても他カテゴリのボーンを誤って消さない。

### 5. テクスチャプロパティ swap（髪色変更）

```csharp
public void SwapTexture(ItemCategory category, Texture2D newTexture)
{
    var item = _equippedItems[category];
    var smr = item.GetComponentInChildren<SkinnedMeshRenderer>();
    var data = _equippedData[category];
    if (!string.IsNullOrEmpty(data.textureProperty))
        smr.material.SetTexture(data.textureProperty, newTexture);
}
```

`smr.material` は自動でinstance化されるため、共有マテリアルを汚染しない
（[教訓レコード #029](../../docs/architecture.md#lessons-学習) 対策）。

---

## 採用しなかった選択肢

### A. SMRの bones を再構築せず、各衣装ボーンをキャラ側にコピーで階層構築

**棄却理由**:
- bones[] 配列を再構築する方が**メモリ・GC的に軽い**（Transform複製は重い）
- ボーン階層を変えると Animation Rigging 等の参照が壊れる

### B. AssetBundle で衣装をDLC化

**棄却理由**:
- 個人開発で AssetBundle 配信インフラ構築は重い
- 全衣装を内蔵する形で実装

### C. Unity の Avatar 機能（HumanDescription）を使った着替え

**棄却理由**:
- Avatar Mask + Animator のリターゲットは、衣装の SkinnedMeshRenderer に
  bones[] を再配線する場面では恩恵が薄い
- ボーンの**名前一致**ベースの方が、衣装作家の自由度を維持できる

### D. 静的に全衣装をシーンに配置して enable/disable

**棄却理由**:
- 衣装数が増えるとロード時間とメモリ消費が線形増加
- ランタイム Instantiate なら必要時のみメモリ展開

---

## 解決した課題

### ボーンリークによる「謎ボーン残骸」

開発初期、衣装を着替えるたびにキャラの足元・腰回りに**透明ボーンが蓄積**して
動作が遅くなる現象に遭遇。原因は前述のボーン同名衝突。

`PortUniqueBones` + `_portedBones` 追跡で完全解決。
教訓レコードとして記録。

### MagicaCloth の「揺れない髪」問題

MagicaCloth は **prefab activate 時に内部状態をbuild** する。コライダー追加が
ビルド前に間に合わないと、衣装は表示されるが**クロスが無反応**。

非アクティブInstantiate → セットアップ → アクティブ化、の順序固定で解消。

### 共有マテリアル汚染回避

`smr.material` ゲッター経由で自動 instance 化。
共有 `.mat` アセットを書き換えるバグを回避。

---

## コード構成

- **`OutfitManager.cs`** — 中核。Equip/Unequip/SwapTexture
- **`CharacterProfile.cs`** — ボーンマップ抽象化
- **`ItemData.cs`** — SO: 着替え単位のデータ
- **`ItemSelector.cs`** — UI Dropdown ⇔ Equip の橋渡し

---

## 既知の制約・トレードオフ

- **MagicaCloth2 への依存**
  - 内容:物理シミュは MagicaCloth2 前提
  - 対処:代替: Unity Cloth + Magica で同等の挙動を再現可能だが調整工数大
- **衣装 prefab の構造規約**
  - 内容:bones の命名がキャラ側に従う必要
  - 対処:テンプレ prefab を提供
- **Unequip 時のフレーム遅延**
  - 内容:MagicaCloth の Destroy が即時でない
  - 対処:1フレーム待ちで Equip 直後の二重ビルドを回避

---

## 関連システム

- **[ActionMatrix](../ActionMatrix/README.md)** — 着替え時に Animator 状態を保持
- **[AutonomyModule](../AutonomyModule/README.md)** — 衣装変更時に SMR 参照更新が必要
- **[GameEventHub](../GameEventHub/README.md)** — `OnEquipChanged` イベント発火（自律挙動の SMR 再キャッシュ用）

---

## 抜粋コード

- `OutfitManager.cs`
- `CharacterProfile.cs`
- `ItemData.cs`, `ItemSelector.cs`
