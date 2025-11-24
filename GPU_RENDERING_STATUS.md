# GPU レンダリング対応状況

## 現在の実装 (Phase 1)

### ✅ 実装済み
- **CPU メッシュ描画**: 通常の Unity Mesh として描画
- **1つのオブジェクト**: 1つの MeshRenderer で描画
- **リアルタイム更新**: CPU 側でメッシュデータを更新

### ❌ 未実装
- **GPU Instancing**: 複数のインスタンスを GPU で描画
- **ComputeShader**: GPU でメッシュ再構築
- **Indirect Drawing**: GPU 側で描画コマンド発行

---

## 現在の描画フロー

```
Blender
  ↓ TCP Stream (binary)
Unity: MeshStreamClient (background thread)
  ↓ Deserialize
Unity: MeshReconstructor
  ↓ mesh.vertices = ... (CPU)
Unity: MeshRenderer
  ↓ Standard rendering
GPU: Draw calls
```

### 特徴
- **利点**:
  - シンプルで安定
  - すべての Unity 機能と互換性あり
  - デバッグが簡単

- **制限**:
  - CPU でメッシュ更新 (遅い)
  - 1オブジェクトのみ
  - 大量の頂点では遅延が発生

---

## Phase 2: GPU Instancing 対応 (計画中)

### 目標
Geometry Nodes の **Instance on Points** などのインスタンス機能に対応

### 実装内容

#### Blender 側
```python
# extractor.py に追加
def extract_instance_transforms(obj, depsgraph):
    """
    Geometry Nodes のインスタンス情報を抽出

    Returns:
        - base_mesh_id: ベースメッシュの ID
        - transforms: 4x4 変換行列の配列 (N, 4, 4)
    """
    # Geometry Nodes の出力からインスタンス情報を取得
    # depsgraph.object_instances を使用
```

#### Unity 側
```csharp
// GPUInstanceRenderer.cs (新規)
public class GPUInstanceRenderer
{
    private ComputeBuffer _transformBuffer;
    private Material _instanceMaterial;

    public void UpdateInstances(Matrix4x4[] transforms)
    {
        _transformBuffer.SetData(transforms);
        Graphics.DrawMeshInstancedIndirect(
            baseMesh,
            0,
            _instanceMaterial,
            bounds,
            argsBuffer
        );
    }
}
```

### 期待される効果
- **パフォーマンス**: 10,000+ インスタンスを GPU で高速描画
- **使用例**: 森（木のインスタンス）、群衆、パーティクル

---

## Phase 3: フル GPU パイプライン (将来)

### 目標
すべての処理を GPU で実行

### 実装内容

#### ComputeShader でメッシュ再構築
```hlsl
// MeshReconstruction.compute
[numthreads(256, 1, 1)]
void ReconstructMesh(uint3 id : SV_DispatchThreadID)
{
    // バイナリデータから頂点を GPU で直接構築
    float3 position = ReadVector3(vertexBuffer, id.x * vertexStride);
    float3 normal = ReadVector3(normalBuffer, id.x * vertexStride);

    outputVertices[id.x] = position;
    outputNormals[id.x] = normal;
}
```

#### Unity 側パイプライン
```
TCP Stream → GPU Buffer (direct upload)
  ↓
ComputeShader: Deserialize & Reconstruct
  ↓
GPU Mesh (GraphicsBuffer)
  ↓
DrawProceduralIndirect
```

### 期待される効果
- **ゼロコピー**: CPU を介さずに GPU へ直接転送
- **超高速**: 100,000+ 頂点をリアルタイム更新
- **低レイテンシ**: ~5ms 以下

---

## 現在の性能

### Phase 1 実測値
```
テスト環境: Windows 11, Unity 6000, Blender 4.5

小規模メッシュ (2,304 vertices):
- Latency: 16ms (60 FPS)
- CPU Usage: ~5%
- Memory: 82 KB/frame
✅ 快適に動作

中規模メッシュ (10,000 vertices):
- Latency: ~30ms (33 FPS)
- CPU Usage: ~15%
- Memory: 360 KB/frame
⚠️ やや重い

大規模メッシュ (50,000+ vertices):
- Latency: 100ms+ (<10 FPS)
- CPU Usage: 40%+
- Memory: 1.8 MB/frame
❌ 実用的でない
```

### 推奨使用範囲
- **リアルタイムプレビュー**: ~10,000 vertices まで
- **アニメーション**: ~5,000 vertices まで
- **複雑な Geometry Nodes**: Phase 2/3 を待つ

---

## GPU 対応への移行パス

### すぐに実装可能 (Phase 2)
1. **GPU Instancing**
   - 既存コードを大きく変更せずに追加
   - `Graphics.DrawMeshInstanced()` 使用
   - Geometry Nodes instances に対応

### 時間がかかる (Phase 3)
2. **ComputeShader 再構築**
   - 大幅なリファクタリング必要
   - Binary protocol を GPU フレンドリーに変更
   - GraphicsBuffer への直接アップロード

3. **Modern Mesh API + Interleaved Buffers**
   - 既に Phase 1 で部分実装済み (コメントアウト)
   - `MeshReconstructor.cs:83-123` 参照

---

## よくある質問

### Q: 現在の実装は GPU を使っていない？

**A:** GPU は**描画のみ**に使用されています。
- メッシュデータの更新: **CPU**
- 描画処理: **GPU** (MeshRenderer 経由)

### Q: GPU Instancing はいつ実装される？

**A:** Phase 2 として計画中。需要があれば優先的に実装可能。

実装の優先度:
1. Phase 1 の安定化 ✅ (完了)
2. シェーダー修正 (ピンク色問題)
3. アニメーション対応 ✅ (完了)
4. GPU Instancing (Phase 2)

### Q: 大量の頂点を扱いたい場合は？

**A:** 以下の方法で対処:
1. **LOD (Level of Detail)**: 遠くのオブジェクトは低ポリゴン
2. **Mesh 分割**: 複数の小さなメッシュに分割
3. **Phase 2/3 を待つ**: GPU パイプライン実装後

### Q: Geometry Nodes instances は動作する？

**A:** 現在は**動作しません**。
- 現在: ベースメッシュのみストリーミング
- Phase 2: インスタンス対応予定

---

## コード例: GPU Instancing 実装 (Phase 2 プレビュー)

### Blender 側 (イメージ)
```python
# handlers.py に追加
def extract_and_stream_instances(obj, depsgraph):
    # ベースメッシュを取得
    base_mesh = extract_mesh_data_fast(obj, depsgraph)

    # インスタンス変換行列を取得
    instance_count = 0
    transforms = []

    for instance in depsgraph.object_instances:
        if instance.is_instance:
            transforms.append(instance.matrix_world)
            instance_count += 1

    # シリアライズして送信
    mesh_data = serializer.serialize_mesh(base_mesh)
    instance_data = serializer.serialize_instance_data(mesh_id=0, transforms=transforms)

    server.send_mesh(mesh_data)
    server.send_instances(instance_data)
```

### Unity 側 (イメージ)
```csharp
// GPUInstanceRenderer.cs
public class GPUInstanceRenderer
{
    private Mesh _baseMesh;
    private Material _material;
    private ComputeBuffer _transformBuffer;
    private ComputeBuffer _argsBuffer;

    public void UpdateInstances(Matrix4x4[] transforms)
    {
        if (_transformBuffer == null || _transformBuffer.count != transforms.Length)
        {
            _transformBuffer?.Release();
            _transformBuffer = new ComputeBuffer(transforms.Length, sizeof(float) * 16);
        }

        _transformBuffer.SetData(transforms);
        _material.SetBuffer("_TransformBuffer", _transformBuffer);

        Graphics.DrawMeshInstancedIndirect(
            _baseMesh,
            0,
            _material,
            new Bounds(Vector3.zero, Vector3.one * 1000),
            _argsBuffer
        );
    }
}
```

---

## まとめ

### 現在の状態 (Phase 1)
- ✅ **基本的な CPU 描画**: 動作中
- ✅ **リアルタイム更新**: 動作中
- ❌ **GPU Instancing**: 未実装
- ❌ **ComputeShader**: 未実装

### 推奨される使い方
- **小〜中規模メッシュ** (< 10,000 vertices)
- **プロトタイピング・プレビュー**用途
- **Geometry Nodes の単一オブジェクト**

### 将来の展望
- **Phase 2**: GPU Instancing → 大量インスタンス対応
- **Phase 3**: フル GPU パイプライン → 超高速・大規模対応

---

**最終更新:** 2025-11-24
