# Blender マテリアル適用方式の検討

Blender 側のマテリアルをストリーミング先(Unity / Vision Pro)へ同期・適用する方式の検討。

> **実装状況**: Phase M1(Principled BSDF パラメータの 0x04 同期)は**実装済み**。
> - Blender: `extractor.extract_material_params()` / `serializer.serialize_material()` /
>   `server.send_material()` / `handlers._sync_materials()`(Material 更新検知+ハッシュ差分送信)
> - Unity: `MeshDeserializer.DeserializeMaterialData()` → `GeometrySyncManager.ApplyMaterial()`
>   (ランタイムマテリアルインスタンスへ適用、アセット非破壊。インスタンス描画と共有)
> - visionOS: `MeshDeserializer.deserializeMaterial()` → `MaterialData.makeMaterial()`
>   (PhysicallyBasedMaterial、linear→sRGB変換込み)→ 両パイプラインの全Entityへ適用
> M2(テクスチャ)以降は未実装。

## 1. 現状

マテリアル情報はプロトコル上いっさい転送していない(頂点 pos/normal/uv のみ)。

| 側 | 現状のマテリアル |
|---|---|
| Blender (`extractor.py`) | 抽出対象外。`material_index` も読んでいない |
| Unity (`GeometrySyncManager.cs`) | MeshRenderer に**手動設定**したマテリアルを流用。インスタンス描画は `MaterialPropertyBlock` の仕組みが既にある(`GPUInstanceRenderer._propertyBlocks`) |
| visionOS (`MetalMeshBuilder` 等) | `SimpleMaterial(color: .blue)` 固定 |

## 2. Blender 側で抽出できるもの

### 2.1 Principled BSDF パラメータ(コスト: ほぼゼロ)

```python
def extract_material_params(mat: bpy.types.Material) -> dict:
    if mat.use_nodes:
        bsdf = next((n for n in mat.node_tree.nodes
                     if n.type == 'BSDF_PRINCIPLED'), None)
        if bsdf:
            i = bsdf.inputs
            return {
                'base_color': tuple(i['Base Color'].default_value),   # RGBA
                'metallic':   i['Metallic'].default_value,
                'roughness':  i['Roughness'].default_value,
                'emission':   tuple(i['Emission Color'].default_value),
                'emission_strength': i['Emission Strength'].default_value,
                'alpha':      i['Alpha'].default_value,
            }
    # use_nodes=False またはBSDFなし → ビューポート表示値で代用
    return {'base_color': tuple(mat.diffuse_color),
            'metallic': mat.metallic, 'roughness': mat.roughness, ...}
```

- Geometry Nodes の Set Material で付いたマテリアルも `obj.evaluated_get(depsgraph)` 後の
  `mesh_eval.materials` に現れるため同じ方法で取れる
- 注意: 入力ソケットに**ノードが接続されている場合** `default_value` は無意味
  (接続の有無は `input.is_linked` で判定 → テクスチャ or ベイクへ)

### 2.2 画像テクスチャ(コスト: 中)

Base Color 等に `TEX_IMAGE` ノードが直結されている場合は画像を転送できる:

- 取得: `image.pixels` は遅いので `image.pixels.foreach_get(np_buffer)`(NumPy一括)
- または `image.save()` / packed_file からPNGバイトを直接取得(エンコード済みで帯域に有利)
- ピクセルはリニアfloat RGBA。sRGB変換と8bit化はBlender側で行ってから送るのが受信側実装を最小にする

### 2.3 プロシージャルノードグラフ(コスト: 大)

Noise/ColorRamp 等のノードグラフは受信側で再現不可能。唯一の汎用解は**ベイク**
(`bpy.ops.object.bake`, Cycles必須, UV必須, 数秒〜数十秒/枚)。
リアルタイム編集ループとは両立しないため、**自動同期の対象外とし手動トリガーに分離**するべき。

### 2.4 マルチマテリアル

`mesh_eval.loop_triangles.foreach_get('material_index', ...)` で三角形ごとのマテリアル番号が
取れる。現行プロトコルはインデックスバッファ1本のためsubmesh非対応 —
複数マテナルを反映するにはメッシュメッセージの拡張が必要(§4)。

## 3. 受信側の適用方法

### 3.1 Unity(URP/Lit)

ランタイムでの適用は素直:

- 単体メッシュ: `renderer.material.SetColor("_BaseColor", c)` / `_Metallic` /
  `_Smoothness`(**= 1 − roughness**)/ `_EmissionColor`
- テクスチャ: `Texture2D.LoadImage(pngBytes)`(PNG)または `LoadRawTextureData`(RGBA8)
- インスタンス描画: 既存の `MaterialPropertyBlock` 経路にそのまま乗る
- InstancedIndirect.shader を使う場合はシェーダー側にプロパティ追加が必要な点だけ注意

### 3.2 visionOS(RealityKit)

**`PhysicallyBasedMaterial` はコードから生成・更新でき、visionOS でも利用可能。**
(VISIONPRO_METAL_GPU.md の「Metal サーフェスシェーダー不可 → ShaderGraphMaterial のみ」は
*カスタムシェーダー記述*の話であり、PBRパラメータ/テクスチャの動的適用は問題なくできる)

```swift
var pbr = PhysicallyBasedMaterial()
pbr.baseColor = .init(tint: UIColor(red: r, green: g, blue: b, alpha: a))
pbr.roughness = .init(floatLiteral: roughness)
pbr.metallic  = .init(floatLiteral: metallic)
pbr.emissiveColor = .init(color: emission)
// テクスチャ: CGImage → TextureResource.generate(from:options:)
//   baseColor は options.semantic = .color (sRGB)、データ系マップは .raw
entity.model?.materials = [pbr]
```

roughness はBlenderと同義(変換不要)。Unityのみ smoothness 反転が必要。

## 4. プロトコル設計案

既存タイプ(0x01/0x02/0x03)は変更せず、新規タイプを追加する:

### 0x04 = マテリアルパラメータ(毎回送っても ~100B、ただし変更時のみ送信)

```
materialId     uint32
baseColor      float32 × 4 (RGBA, linear)
metallic       float32
roughness      float32
emission       float32 × 3 (RGB) + strength float32
alpha          float32
textureFlags   uint32 (bit0=baseColor, bit1=normal, bit2=roughness...)
textureIds     uint32 × 3 (0 = なし)
```

### 0x05 = テクスチャ(初回・変更時のみ)

```
textureId  uint32
width      uint32
height     uint32
format     uint8 (0=RGBA8 raw, 1=PNG, 2=raw+zlib)
payload    NB
```

- `textureId` は**画像内容のハッシュ**(例: xxhash64 の下位32bit)にすると
  受信側キャッシュと再送抑制が自動的に成立する
- 1024² RGBA8 raw = 4MB / PNG なら通常数百KB。PNG推奨(Blender側でエンコード済みが取れる)

### 0x06 = submesh 対応メッシュ(Phase M3、マルチマテリアル用)

```
vertexCount    uint32
indexCount     uint32
submeshCount   uint32
per submesh:   indexOffset uint32, indexCount uint32, materialId uint32
vertexData     (現行と同一の32B stride)
indexData      uint32 × indexCount (submesh順にソート済み)
```

- 0x01 は残し、マテリアルスロットが1つの間は 0x01 + 0x04 で済ませる
- 受信側は submesh を Unity では `Mesh.SetSubMesh`、visionOS では
  `LowLevelMesh.Part` を複数並べて `materialIndex` を割り当てるだけ
  (**LowLevelMesh は Part ごとの materialIndex を最初からサポートしており、
  現行GPUパイプラインの拡張コストは小さい**)

### 送信トリガー

`depsgraph_update_post` の `update.id` が `bpy.types.Material` のときのみ
パラメータを再抽出し、直前送信値のハッシュと違う場合だけ送る
(ジオメトリ30FPSの流れとは独立した低頻度パス。`handlers.py` に追加)。

## 5. 段階的ロードマップ

| フェーズ | 内容 | 主な変更 | 効果/コスト |
|---|---|---|---|
| M1 | Principled BSDF スカラー/カラーのみ (0x04) | extractor/serializer/handlers + 両受信側 ~各100行 | 色・粗さ・金属感・発光が同期。**費用対効果最大** |
| M2 | 画像テクスチャ (0x05 + ハッシュキャッシュ) | 画像エンコード、Unity `LoadImage` / RealityKit `TextureResource` | UV済みテクスチャが反映。初回転送のみ重い |
| M3 | マルチマテリアル (0x06 submesh) | extractor の material_index 分割、`LowLevelMesh.Part` 複数化、`Mesh.SetSubMesh` | GN Set Material の塗り分けが反映 |
| M4 | プロシージャルベイク(手動) | UI「Bake & Sync Materials」オペレーター、Cycles bake → 0x05 | ノードグラフ全般を反映。編集ループ外の操作 |

## 6. 技術的な注意点

- **色空間**: Blender の値はリニア。パラメータはリニアのまま送り、
  Unity(Linear色空間プロジェクト)はそのまま、RealityKit は UIColor 生成時に注意。
  テクスチャは「baseColor=sRGB、normal/roughness=リニア」を format メタデータで区別
- **normal map**: Blender(OpenGL, Y+)と Unity/RealityKit の規約差(UnityはY+だがDirectX系
  アセットはY−)。自前ベイクならY+で統一すれば両者そのまま使える
- **Emission強度**: RealityKit の emissiveColor は HDR 強度の扱いが弱い。
  strength はカラーへ乗算してから送る簡易対応で開始
- **インスタンスのマテリアル**: 0x02/0x03 のベースメッシュにも materialId を紐付ける場合、
  visionOS のベイク方式(単一マージメッシュ)はマテリアル1種のみ。
  複数種のインスタンスマテリアルは meshId 分割で対応(現行設計と整合)
- **SouthwestAir 方式(USDZ)との関係**: VISIONPRO_SYNC_STUDY.md §3.3 の
  「マテリアルのみUSDZワンショット」案は、本検討の 0x04/0x05 が Unity と visionOS の
  **両方**に効くのに対し USDZ は visionOS 専用になるため、優先度を下げる

## 7. 結論

- **推奨は M1(BSDF パラメータの 0x04 同期)から着手**。実装量が小さく、
  Unity は既存 MaterialPropertyBlock 経路・visionOS は PhysicallyBasedMaterial で
  受け皿が揃っており、「Blenderで色を変えたら即反映」という体験価値が最も大きい
- テクスチャ(M2)はハッシュキャッシュ前提で追加。プロシージャルは自動同期せず
  手動ベイク(M4)に分離する
- マルチマテリアル(M3)はプロトコル拡張(0x06)を伴うため、
  M1/M2 の運用で需要を確認してから実装する
