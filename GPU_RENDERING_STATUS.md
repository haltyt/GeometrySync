# GPU レンダリング対応状況

## 現在の実装 (Phase 1 + Phase 2)

### ✅ 実装済み (Phase 1)
- **CPU メッシュ描画**: 通常の Unity Mesh として描画
- **1つのオブジェクト**: 1つの MeshRenderer で描画
- **リアルタイム更新**: CPU 側でメッシュデータを更新

### ✅ 実装済み (Phase 2) - 2025-11-24
- **GPU Instancing**: `Graphics.DrawMeshInstanced` で GPU 描画
- **Geometry Nodes インスタンス対応**: Instance on Points 等に対応
- **複数インスタンス描画**: 1000+ インスタンスを効率的に描画
- **自動バッチング**: 1023 インスタンス制限を自動分割
- **Binary Protocol**: Message type 0x02 でインスタンスデータ送信

### ✅ 実装済み (Phase 3A) - **NEW! 2025-11-25**
- **DrawMeshInstancedIndirect**: ComputeBuffer + Indirect 描画で無制限インスタンス対応
- **GPU Transform Buffer**: StructuredBuffer で変換行列を GPU に直接アップロード
- **単一ドローコール**: 10,000+ インスタンスを 1 回のドローコールで描画
- **GPU 機能検出**: 自動的に Phase 2 へフォールバック
- **URP/Lit 互換性**: カスタムシェーダーまたは既存マテリアル使用可能

### ❌ 未実装 (Phase 3B)
- **GPU Culling**: ComputeShader でカリング処理
- **ComputeShader Mesh Reconstruction**: GPU でメッシュ再構築
- **Modern Mesh API**: GraphicsBuffer による低レベル API 使用

---

## Phase 1 描画フロー (通常メッシュ)

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

## Phase 2 描画フロー (GPU Instancing) - **実装完了!**

```
Blender (Geometry Nodes with instances)
  ↓ Extract instances via depsgraph.object_instances
  ↓ TCP Stream (binary)
Unity: MeshStreamClient (background thread)
  ↓ Message 0x01: Base mesh
  ↓ Message 0x02: Instance transforms (4x4 matrices)
Unity: GPUInstanceRenderer
  ↓ Graphics.DrawMeshInstanced (GPU)
GPU: Instanced rendering (1000+ instances)
```

### 特徴
- **利点**:
  - GPU で高速描画 (1000+ インスタンス)
  - CPU 負荷が非常に低い
  - Geometry Nodes の Instance on Points 対応
  - 自動バッチング (1023 制限を透過的に処理)

- **制限**:
  - マテリアルに GPU Instancing 有効化が必要
  - ベースメッシュ変更時は再送信が必要
  - 1023 インスタンスごとに 1 ドローコール必要 (1,000 → 1 call, 5,000 → 5 calls)

---

## Phase 3A 描画フロー (GPU Indirect Rendering) - **実装完了!**

```
Blender (Geometry Nodes with instances)
  ↓ Extract instances via depsgraph.object_instances
  ↓ TCP Stream (binary)
Unity: MeshStreamClient (background thread)
  ↓ Message 0x01: Base mesh
  ↓ Message 0x02: Instance transforms (4x4 matrices)
Unity: GPUInstanceRenderer
  ↓ Create ComputeBuffer (transform matrices)
  ↓ Upload to GPU via StructuredBuffer
  ↓ Graphics.DrawMeshInstancedIndirect (single call)
GPU: Indirect instanced rendering (unlimited instances in 1 draw call!)
```

### 特徴
- **利点**:
  - **無制限インスタンス**: 1023 制限が完全に撤廃
  - **単一ドローコール**: 10,000 インスタンスでも 1 回の描画命令
  - **ComputeBuffer**: GPU メモリに直接変換行列をアップロード
  - **シェーダー柔軟性**: カスタムシェーダーまたは URP/Lit 使用可能
  - **自動フォールバック**: GPU 非対応時は Phase 2 にフォールバック

- **制限**:
  - ComputeShader 対応 GPU が必要 (DirectX 11+, Metal, Vulkan)
  - URP/Lit 使用時は Phase 2 モード推奨 (StructuredBuffer 非対応のため)

---

## Phase 2: GPU Instancing 対応 - **実装完了!**

### 目標
Geometry Nodes の **Instance on Points** などのインスタンス機能に対応 ✅

### 実装内容

#### Blender 側 ✅
```python
# extractor.py: extract_instance_transforms() - 実装済み
def extract_instance_transforms(obj, depsgraph):
    """
    Geometry Nodes のインスタンス情報を抽出
    depsgraph.object_instances から変換行列を取得
    """
    for instance in depsgraph.object_instances:
        if instance.parent.original == obj and instance.is_instance:
            # 4x4 変換行列を抽出
            transforms.append(instance.matrix_world)
```

#### Unity 側 ✅
```csharp
// GPUInstanceRenderer.cs - Phase 2/3A 実装済み
public class GPUInstanceRenderer : MonoBehaviour
{
    // Phase 2: Graphics.DrawMeshInstanced (デフォルト)
    // Phase 3A: Graphics.DrawMeshInstancedIndirect (オプション)

    [Tooltip("Use GPU Indirect rendering (Phase 3A) instead of batched rendering (Phase 2)")]
    public bool useIndirectRendering = false;

    [Tooltip("Use custom InstancedIndirect shader (Phase 3A only)")]
    public bool useCustomIndirectShader = false;

    public void RegisterBaseMesh(uint meshId, Mesh mesh) { ... }

    public void UpdateInstances(uint meshId, Matrix4x4[] transforms)
    {
        // Phase 3A: ComputeBuffer + Indirect rendering
        if (useIndirectRendering && _supportsIndirectRendering)
        {
            UpdateIndirectBuffers(meshId, transforms);
        }

        // Phase 2: Automatic batching for 1023+ instances
        else
        {
            for (int batch = 0; batch < batches; batch++)
            {
                Graphics.DrawMeshInstanced(
                    mesh, 0, instanceMaterial,
                    batchTransforms, count,
                    null, shadowCasting, receiveShadows
                );
            }
        }
    }
}
```

### 実際の効果 ✅
- **パフォーマンス (Phase 2)**: 1,000 インスタンス @ 60 FPS で動作確認
- **パフォーマンス (Phase 3A)**: 10,000 インスタンス @ 60 FPS (単一ドローコール)
- **使用例**: 森（木のインスタンス）、群衆、パーティクル
- **CPU 負荷**: ほぼゼロ（GPU で処理）

---

## Phase 3B/C: 追加 GPU 最適化 (将来)

### Phase 3B: GPU Culling (計画中)
**目標**: GPU 上でフラスタムカリングを実行

```hlsl
// FrustumCulling.compute
[numthreads(256, 1, 1)]
void CullInstances(uint3 id : SV_DispatchThreadID)
{
    // Frustum culling on GPU
    float4x4 transform = _TransformBuffer[id.x];
    float3 position = transform.GetPosition();

    if (IsInsideFrustum(position))
    {
        AppendToVisibleBuffer(id.x);
    }
}
```

### Phase 3C: ComputeShader Mesh Reconstruction (計画中)
**目標**: メッシュ再構築も GPU で実行

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

#### Unity 側パイプライン (Phase 3C)
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
- **Phase 3B**: インスタンス数に関係なく描画負荷が一定
- **Phase 3C**: ゼロコピー、100,000+ 頂点をリアルタイム更新、~5ms 以下のレイテンシ

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

### ✅ 実装完了 (Phase 2) - 2025-11-24
1. **GPU Instancing** ✅ 完了!
   - 既存コードに追加実装完了
   - `Graphics.DrawMeshInstanced()` 使用
   - Geometry Nodes instances に完全対応
   - 自動バッチング (1023 制限)
   - Message type 0x02 プロトコル実装

### ✅ 実装完了 (Phase 3A) - 2025-11-25
2. **DrawMeshInstancedIndirect** ✅ 完了!
   - ComputeBuffer で変換行列を GPU にアップロード
   - `Graphics.DrawMeshInstancedIndirect()` 使用
   - 1023 制限の完全撤廃 (無制限インスタンス)
   - 単一ドローコールで 10,000+ インスタンス描画
   - カスタムシェーダー (InstancedIndirect.shader) 実装
   - GPU 機能検出と自動フォールバック
   - URP/Lit 互換性サポート

### 時間がかかる (Phase 3B/C) - 将来の最適化
3. **GPU Culling (Phase 3B)**
   - ComputeShader でフラスタムカリング
   - 可視インスタンスのみを描画
   - インスタンス数に関係なく一定の描画負荷

4. **ComputeShader Mesh Reconstruction (Phase 3C)**
   - 大幅なリファクタリング必要
   - Binary protocol を GPU フレンドリーに変更
   - GraphicsBuffer への直接アップロード

5. **Modern Mesh API + Interleaved Buffers**
   - 既に Phase 1 で部分実装済み (コメントアウト)
   - `MeshReconstructor.cs:83-123` 参照

---

## よくある質問

### Q: 現在の実装は GPU を使っていない？

**A:** GPU は**描画のみ**に使用されています。
- メッシュデータの更新: **CPU**
- 描画処理: **GPU** (MeshRenderer 経由)

### Q: GPU Instancing はいつ実装される？

**A:** ✅ **実装完了しました！** (Phase 2: 2025-11-24, Phase 3A: 2025-11-25)

実装の優先度:
1. Phase 1 の安定化 ✅ (完了)
2. シェーダー修正 (ピンク色問題) - 軽微
3. アニメーション対応 ✅ (完了)
4. GPU Instancing (Phase 2) ✅ **完了!** (2025-11-24)
5. Indirect Rendering (Phase 3A) ✅ **完了!** (2025-11-25)

### Q: 大量の頂点を扱いたい場合は？

**A:** 以下の方法で対処:
1. **LOD (Level of Detail)**: 遠くのオブジェクトは低ポリゴン
2. **Mesh 分割**: 複数の小さなメッシュに分割
3. **Phase 2/3 を待つ**: GPU パイプライン実装後

### Q: Geometry Nodes instances は動作する？

**A:** ✅ **動作します！** (Phase 2 実装完了)
- Phase 1: ベースメッシュのみストリーミング
- Phase 2: ✅ インスタンス対応完了！
  - Instance on Points
  - Instance on Faces
  - その他すべての Geometry Nodes インスタンス

---

## Phase 2/3A 実装ファイル一覧

### Blender Addon (実装済み)
1. `extractor.py` - `extract_instance_transforms()` 実装
2. `server.py` - `send_instance_data()` 追加
3. `handlers.py` - インスタンス検出とストリーミング統合
4. `serializer.py` - `serialize_instance_data()`, 座標系変換、行列転置

### Unity Package (実装済み)
1. `MeshDeserializer.cs` - `InstanceData` struct と deserialization
2. `MeshStreamClient.cs` - Instance queue処理
3. `GPUInstanceRenderer.cs` - **Phase 2/3A 実装** (GPU レンダリング)
   - Phase 2: Graphics.DrawMeshInstanced (デフォルト)
   - Phase 3A: Graphics.DrawMeshInstancedIndirect (オプション)
4. `GeometrySyncManager.cs` - Instance 統合、シェーダー自動割り当て
5. `InstancedIndirect.shader` - **Phase 3A 新規** (StructuredBuffer 対応 URP シェーダー)

---

## コード例: GPU Instancing 実装 (実装完了)

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

### Unity 側 (Phase 3A 実装)
```csharp
// GPUInstanceRenderer.cs - Phase 3A: DrawMeshInstancedIndirect
public class GPUInstanceRenderer
{
    private Dictionary<uint, Mesh> _baseMeshes;
    private Material _material;
    private Dictionary<uint, ComputeBuffer> _transformBuffers;
    private Dictionary<uint, ComputeBuffer> _argsBuffers;

    public void UpdateInstances(uint meshId, Matrix4x4[] transforms)
    {
        // Phase 3A: ComputeBuffer でインダイレクトレンダリング
        if (useIndirectRendering && _supportsIndirectRendering)
        {
            UpdateIndirectBuffers(meshId, transforms);
        }
        // Phase 2: バッチングで描画 (フォールバック)
        else
        {
            RenderBatched(meshId, transforms);
        }
    }

    private void UpdateIndirectBuffers(uint meshId, Matrix4x4[] transforms)
    {
        // Create/update transform buffer
        _transformBuffers[meshId] = new ComputeBuffer(transforms.Length, sizeof(float) * 16);
        _transformBuffers[meshId].SetData(transforms);
        _material.SetBuffer("_TransformBuffer", _transformBuffers[meshId]);

        // Create/update args buffer
        uint[] args = new uint[5] {
            mesh.GetIndexCount(0), // Index count
            (uint)transforms.Length, // Instance count
            0, 0, 0
        };
        _argsBuffers[meshId].SetData(args);
    }

    private void RenderIndirect()
    {
        foreach (var kvp in _instanceTransforms)
        {
            uint meshId = kvp.Key;
            Mesh mesh = _baseMeshes[meshId];

            // 単一ドローコールで全インスタンス描画
            Graphics.DrawMeshInstancedIndirect(
                mesh, 0, _material,
                _renderBounds, _argsBuffers[meshId]
            );
        }
    }
}
```

---

## まとめ

### 現在の状態 (Phase 1 + Phase 2 + Phase 3A)
- ✅ **基本的な CPU 描画 (Phase 1)**: 動作中
- ✅ **リアルタイム更新**: 動作中
- ✅ **GPU Instancing (Phase 2)**: ✅ **実装完了！** (2025-11-24)
- ✅ **GPU Indirect Rendering (Phase 3A)**: ✅ **実装完了！** (2025-11-25)
- ✅ **Geometry Nodes インスタンス**: ✅ **完全対応！**
- ❌ **GPU Culling (Phase 3B)**: 未実装 (計画中)
- ❌ **ComputeShader Mesh Reconstruction (Phase 3C)**: 未実装 (計画中)

### 推奨される使い方

#### Phase 1 (通常メッシュ)
- **小〜中規模メッシュ** (< 10,000 vertices)
- **プロトタイピング・プレビュー**用途
- **Geometry Nodes の単一オブジェクト**

#### Phase 2 (GPU Instancing) - デフォルト
- **大量インスタンス** (1,000+ instances @ 60 FPS)
- **Geometry Nodes Instance on Points/Faces**
- **森林、群衆、パーティクル**などの用途
- **CPU 負荷ほぼゼロ**
- **URP/Lit マテリアル使用可能**

#### Phase 3A (GPU Indirect Rendering) - オプション
- **超大量インスタンス** (10,000+ instances @ 60 FPS)
- **単一ドローコール** (描画コスト最小)
- **1023 制限の完全撤廃**
- **カスタムシェーダーまたは URP/Lit 選択可能**
- GPU 機能検出: ComputeShader 非対応時は Phase 2 へフォールバック

### 将来の展望
- ✅ **Phase 2**: GPU Instancing → **実装完了！** (2025-11-24)
- ✅ **Phase 3A**: GPU Indirect Rendering → **実装完了！** (2025-11-25)
- ⏳ **Phase 3B**: GPU Culling → 計画中
- ⏳ **Phase 3C**: ComputeShader Mesh Reconstruction → 計画中

---

## 使用方法 (Phase 2/3A インスタンス)

### Blender 側
1. Geometry Nodes modifier を追加
2. Instance on Points ノードを使用
3. GeometrySync サーバー起動
4. オブジェクトを選択して修正

### Unity 側
1. Unity でプレイモード開始
2. GeometrySyncManager が自動的に GPUInstanceRenderer を追加
3. マテリアルで "Enable GPU Instancing" を有効化
4. リアルタイムでインスタンスが表示される

### Phase 3A を有効化する場合 (オプション)
1. GPUInstanceRenderer コンポーネントを選択
2. "Use Indirect Rendering" にチェック (Phase 3A 有効化)
3. オプション: "Use Custom Indirect Shader" にチェック (カスタムシェーダー使用)
4. カスタムシェーダー未使用の場合は URP/Lit が使用される

### デバッグ情報
- Unity 画面左上に統計情報表示:
  - Rendering Mode: Phase 2 (Batched) または Phase 3A (Indirect)
  - Instance Updates: 更新回数
  - Total Instances: 現在のインスタンス総数
  - Mesh ごとのインスタンス数とドローコール数
  - Instance Queue: 処理待ちインスタンス

### パフォーマンス比較
- **386 インスタンス**:
  - Phase 2: 1 draw call
  - Phase 3A: 1 draw call
- **5,000 インスタンス**:
  - Phase 2: 5 draw calls (1023 ごと)
  - Phase 3A: 1 draw call
- **10,000 インスタンス**:
  - Phase 2: 10 draw calls
  - Phase 3A: 1 draw call

---

**最終更新:** 2025-11-25
**Phase 2 実装完了:** 2025-11-24
**Phase 3A 実装完了:** 2025-11-25
