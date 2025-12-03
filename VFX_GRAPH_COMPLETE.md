# VFX Graph 連携実装完了レポート

**実装日**: 2025-11-25
**実装内容**: GeometrySync と Unity VFX Graph の連携機能

---

## 概要

VFX Graph 連携により、Blender の Geometry Nodes で生成されたインスタンス位置を Unity VFX Graph にリアルタイムで転送し、パーティクルエフェクトの発生源として使用できるようになりました。

### 主な成果
- ✅ **VFX Graph 17.0.4 完全対応**: Texture2D ベースのデータ転送
- ✅ **リアルタイム連携**: Blender の変更が即座に VFX に反映
- ✅ **大規模対応**: 10,000+ インスタンスをサポート
- ✅ **デュアルモード**: Texture2D (推奨) と GraphicsBuffer (レガシー) の両対応
- ✅ **詳細なドキュメント**: 完全なセットアップガイドとサンプル集

---

## 技術詳細

### アーキテクチャ

```
Blender (Geometry Nodes)
  ↓ Instance on Points
GeometrySync Server (TCP:8080)
  ↓ Binary stream
Unity: GeometrySyncManager
  ↓ Mesh deserialize
Unity: GPUInstanceRenderer
  ↓ Instance transforms (Matrix4x4[])
Unity: VFXGraphBridge
  ↓ Extract positions → Texture2D / GraphicsBuffer
Unity: VFX Graph
  ↓ SampleTexture2D() / Sample GraphicsBuffer
GPU: Particle spawning
```

### 実装ファイル

#### 1. VFXGraphBridge.cs
**役割**: GPUInstanceRenderer からインスタンス位置を抽出し、VFX Graph に転送

**主な機能**:
- デュアルモード対応 (Texture2D / GraphicsBuffer)
- リフレクションによる GPUInstanceRenderer の内部データアクセス
- 自動バッファ管理 (作成・更新・解放)
- フレーム間隔調整機能 (パフォーマンス最適化)
- 最大パーティクル数制限

**コア実装 - Texture2D モード**:
```csharp
private void UpdateVFXWithTexture2D(int instanceCount)
{
    // Square texture layout: √n × √n
    int texWidth = Mathf.CeilToInt(Mathf.Sqrt(instanceCount));
    int texHeight = texWidth;

    // RGBAFloat texture (128-bit per pixel)
    _positionTexture = new Texture2D(texWidth, texHeight, TextureFormat.RGBAFloat, false);
    _positionTexture.filterMode = FilterMode.Point;
    _positionTexture.wrapMode = TextureWrapMode.Clamp;

    // Pack Vector3 positions into Color array
    Color[] colors = new Color[texWidth * texHeight];
    for (int i = 0; i < instanceCount; i++)
    {
        colors[i] = new Color(
            _positions[i].x,  // R channel
            _positions[i].y,  // G channel
            _positions[i].z,  // B channel
            1.0f              // A channel (unused)
        );
    }

    _positionTexture.SetPixels(colors);
    _positionTexture.Apply();

    // Set VFX Graph properties
    _vfx.SetTexture(PropPositionTexture, _positionTexture);
    _vfx.SetInt(PropTextureWidth, texWidth);
}
```

**コア実装 - リフレクション**:
```csharp
private uint[] GetInstanceMeshIds()
{
    var instanceTransformsField = typeof(GPUInstanceRenderer)
        .GetField("_instanceTransforms",
                  System.Reflection.BindingFlags.NonPublic |
                  System.Reflection.BindingFlags.Instance);

    var instanceTransforms = instanceTransformsField.GetValue(instanceRenderer)
        as System.Collections.Generic.Dictionary<uint, Matrix4x4[]>;

    var keys = new uint[instanceTransforms.Count];
    instanceTransforms.Keys.CopyTo(keys, 0);
    return keys;
}
```

#### 2. VFX_GRAPH_INTEGRATION.md
**役割**: VFX Graph 連携の完全なドキュメント

**内容**:
- セットアップ手順 (Blender + Unity)
- VFX Graph 17.0.4 対応の実装方法
- 3 つのサンプル VFX (Spark, Floating, Explosion)
- パフォーマンス最適化ガイド
- トラブルシューティング

---

## VFX Graph 17.0.4 互換性対応

### 対応した問題

#### 問題 1: Sample GraphicsBuffer オペレーターが存在しない
**症状**: ドキュメントで参照していた `Sample GraphicsBuffer` オペレーターが VFX Graph 17.0.4 に存在しない

**解決策**:
- Texture2D ベースのアプローチに変更
- `SampleTexture2D()` 関数を使用
- VFXGraphBridge に `useTexture2D` フラグを追加 (デフォルト: true)

**VFX Graph での実装**:
```hlsl
// Set Position (Inline)
SampleTexture2D(PositionTexture,
    float2(particleId % TextureWidth, particleId / TextureWidth) / float(TextureWidth)
).xyz
```

#### 問題 2: Construct Vector2 オペレーターが存在しない
**症状**: UV 座標計算で `Construct Vector2` オペレーターが存在しない

**解決策**:
- 複雑なオペレーターチェーンを単一の Inline 式に簡略化
- `float2()` コンストラクタを直接使用

**変更前 (動作しない)**:
```
[Operator] Multiply (particleId, 1.0 / TextureWidth)
  ↓
[Operator] Construct Vector2
  ↓
Set Position from Map
```

**変更後 (動作する)**:
```
Set Position (Inline)
  Position: SampleTexture2D(PositionTexture,
      float2(particleId % TextureWidth, particleId / TextureWidth) / float(TextureWidth)
  ).xyz
```

#### 問題 3: Custom HLSL の記述形式が不正
**症状**: Custom HLSL ブロックで変数宣言形式を使用していたが、VFX Graph 17.0.4 では関数形式が必須

**解決策**:
- 関数ベースの構文に変更
- 入出力パラメータを関数シグネチャで定義

**変更前 (動作しない)**:
```hlsl
// Custom HLSL Block (変数宣言形式)
StructuredBuffer<float3> PositionBuffer;

[out] float3 position;

uint index = particleId % InstanceCount;
position = PositionBuffer[index];
```

**変更後 (動作する)**:
```hlsl
// Custom HLSL Block (関数形式)
float3 GetPositionFromTexture(in Texture2D posTexture, in float particleId, in float texWidth)
{
    float2 uv = float2(particleId % texWidth, floor(particleId / texWidth)) / texWidth;
    float4 color = posTexture.SampleLevel(sampler_posTexture, uv, 0);
    return color.xyz;
}
```

---

## 使用方法

### 基本セットアップ

#### 1. Unity Scene 準備

**GeometrySyncManager GameObject**:
```
Components:
- GeometrySyncManager (Auto Connect: ✓)
- MeshFilter
- MeshRenderer (Material: GeometrySyncMat)
- GPUInstanceRenderer (Use Indirect Rendering: ✓)
```

**VFX GameObject**:
```
Components:
- VisualEffect (Asset: GeometrySyncParticles.vfx)
- VFXGraphBridge
  - Instance Renderer: GeometrySyncManager の GPUInstanceRenderer
  - Use Texture2D: ✓ (推奨)
  - Update Interval: 1
  - Max Particles: 10000
  - Spawn Rate: 1.0
```

#### 2. VFX Graph 設定

**Blackboard Properties**:
```
- PositionTexture (Texture2D)
- InstanceCount (int)
- TextureWidth (int)
- SpawnRate (float)
```

**Initialize Particle Context**:
```
Set Position (Inline)
  Position: SampleTexture2D(PositionTexture,
      float2(particleId % TextureWidth, particleId / TextureWidth) / float(TextureWidth)
  ).xyz
```

#### 3. Blender 準備

**Geometry Nodes**:
```
Input Geometry
  ↓
Distribute Points on Faces
  ↓
Instance on Points
  ↓ Instance: Small mesh (e.g., Cube)
  ↓
(Realize Instances: OFF - keep as instances)
```

**GeometrySync Server**:
- Port: 8080
- "Start Server" をクリック

---

## サンプル VFX

### 例 1: スパーク

**効果**: インスタンス位置から上方向にスパークが飛び散る

**設定**:
```
Initialize:
  - Position: SampleTexture2D(...)
  - Velocity: Random Direction in Cone (Y-up, 30°, Speed: 1-3)
  - Lifetime: 0.5-1.5s
  - Size: 0.1

Update:
  - Gravity: (0, -9.81, 0)
  - Linear Drag: 0.5

Output:
  - Color Gradient: White → Orange → Black
  - Blend Mode: Additive
```

### 例 2: 浮遊パーティクル

**効果**: インスタンス位置周辺をゆっくり浮遊

**設定**:
```
Initialize:
  - Position: SampleTexture2D(...)
  - Velocity: Random Direction in Sphere (Speed: 0.05-0.15)
  - Lifetime: 2-5s
  - Size: 0.05-0.2

Update:
  - Turbulence (Intensity: 0.5, Frequency: 1.0, Octaves: 3)
  - Linear Drag: 1.0

Output:
  - Color: Cyan → Blue → Purple
  - Blend Mode: Alpha Blend
```

### 例 3: 爆発

**効果**: インスタンス位置で爆発エフェクト

**設定**:
```
Initialize:
  - Position: SampleTexture2D(...)
  - Velocity: Random Direction (Speed: 3-7)
  - Lifetime: 0.2-0.8s
  - Size: 0.3

Update:
  - Gravity: (0, -9.81, 0)
  - Size over Lifetime: 0.3 → 0.0

Output:
  - Color Gradient: Yellow → Orange → Red → Black
  - Blend Mode: Additive
```

### 例 4: トレイル (軌跡表示) - Sample Buffer 使用

**効果**: GraphicsBuffer と Sample Buffer ノードを使用してパーティクルの軌跡を可視化

**VFXGraphBridge 設定**:
- Use Texture2D: **OFF** (GraphicsBuffer モード)

**VFX Graph Blackboard**:
```
- PositionBuffer (GraphicsBuffer)
- InstanceCount (int)
```

**設定**:
```
Spawn:
  - Single Burst (Count: InstanceCount, Loop: Infinite, Duration: 999999)

Initialize Particle Strip:
  - Position: Sample Buffer (Buffer: PositionBuffer, Index: particleIndex % InstanceCount, Stride: 12)
  - Lifetime: 999999
  - Size: 0.3

Update Particle Strip:
  - Set Strip Size (over Strip Index): 0.2 → 0.0

Output ParticleStrip Quad:
  - Color: Cyan
  - Blend Mode: Additive
  - Tiling Mode: Stretch
```

**重要**:
- **Particle Strip System** を使用 (Create System → Particle Strip System)
- **Output ParticleStrip Quad** を使用 (VFX Graph 17.0.4)
- Sample Buffer の **Stride は 12** (Vector3 = 12 bytes)

詳細は [VFX_GRAPH_INTEGRATION.md](VFX_GRAPH_INTEGRATION.md) の「例 4: トレイル - Sample Buffer 使用」セクションを参照してください。

---

## パフォーマンス

### メモリ使用量

**Texture2D モード**:
```
1,000 instances:
  Texture: 32×32 pixels × 16 bytes (RGBAFloat) = 16 KB

10,000 instances:
  Texture: 100×100 pixels × 16 bytes = 160 KB
```

**GraphicsBuffer モード**:
```
1,000 instances:
  Buffer: 1,000 × 12 bytes (Vector3) = 12 KB

10,000 instances:
  Buffer: 10,000 × 12 bytes = 120 KB
```

### 推奨設定

| インスタンス数 | Update Interval | Max Particles | Spawn Rate |
|-------------|-----------------|---------------|------------|
| < 1,000     | 0 (毎フレーム)    | 1,000         | 1.0        |
| 1,000-5,000 | 1 (1フレームおき)  | 5,000         | 0.5        |
| 5,000+      | 2-5             | 10,000        | 0.2        |

---

## 制限事項と今後の展望

### 現在の制限

1. **位置のみ対応**: 回転・スケールデータは転送されない
2. **カスタム属性非対応**: Geometry Nodes のカスタム属性 (色、速度等) は未対応
3. **リフレクション使用**: GPUInstanceRenderer の内部データにアクセスするため、パフォーマンスオーバーヘッドあり
4. **VFX Graph パッケージ必須**: Unity VFX Graph パッケージのインストールが必要

### 今後の拡張案

#### Phase 1: 回転・スケール対応
```csharp
// Matrix4x4 から完全な TRS を抽出
Vector3 position = matrix.GetPosition();
Quaternion rotation = matrix.rotation;
Vector3 scale = matrix.lossyScale;

// Texture2D に複数チャンネルでエンコード
// Channel 0 (RGB): Position
// Channel 1 (RGBA): Rotation (quaternion)
// Channel 2 (RGB): Scale
```

#### Phase 2: カスタム属性対応
```csharp
// Blender から属性データを受信
Dictionary<string, float[]> attributes;

// VFX Graph に追加テクスチャとして転送
_vfx.SetTexture("ColorAttribute", colorTexture);
_vfx.SetTexture("VelocityAttribute", velocityTexture);
```

#### Phase 3: Public API 追加
```csharp
// GPUInstanceRenderer に public メソッド追加
public class GPUInstanceRenderer
{
    public uint[] GetMeshIds() { ... }
    public Matrix4x4[] GetTransforms(uint meshId) { ... }
    public int GetTotalInstanceCount() { ... }  // 既に存在
}

// リフレクションを廃止
```

#### Phase 4: ComputeShader 最適化
```csharp
// CPU 側の位置抽出を GPU 側で実行
ComputeShader extractPositions;
extractPositions.SetBuffer(0, "InputTransforms", transformBuffer);
extractPositions.SetBuffer(0, "OutputPositions", positionBuffer);
extractPositions.Dispatch(0, instanceCount / 64, 1, 1);

// VFX Graph に直接転送 (CPU を介さない)
_vfx.SetGraphicsBuffer("PositionBuffer", positionBuffer);
```

---

## トラブルシューティング

### パーティクルが表示されない

**確認項目**:
1. VFX Graph の Blackboard プロパティが正しく設定されているか
2. VFXGraphBridge の `Instance Renderer` フィールドに GPUInstanceRenderer が割り当てられているか
3. GeometrySyncManager が Blender に接続されているか (Console で確認)
4. Blender でインスタンスを持つオブジェクトが選択されているか
5. VFX GameObject が有効化されているか

**Console ログ**:
```
[VFXGraphBridge] Initialized with Texture2D mode
[VFXGraphBridge] Updated VFX with 386 positions (Texture2D mode)
```

### パーティクル位置がおかしい

**原因**: 座標系変換の問題

**解決**:
- GeometrySync は自動的に Blender (Y-up) → Unity (Y-up) 変換を実行
- GeometrySyncManager の Transform が (0, 0, 0) / (0, 0, 0) / (1, 1, 1) になっているか確認
- Blender と Unity のスケールが一致しているか確認 (Blender: 1 unit = Unity: 1 unit)

### VFX Graph エディタでエラー

**症状**: "SampleTexture2D is not defined"

**原因**: VFX Graph 17.0.4 より古いバージョンを使用

**解決**:
- Unity Package Manager で VFX Graph を最新バージョンに更新
- または GraphicsBuffer モードを使用 (`Use Texture2D` のチェックを外す)

---

## まとめ

VFX Graph 連携実装により、GeometrySync は以下を達成しました:

1. **リアルタイム性**: Blender での変更が即座に Unity VFX に反映
2. **スケーラビリティ**: 10,000+ インスタンス対応
3. **互換性**: VFX Graph 17.0.4 完全対応
4. **柔軟性**: Texture2D / GraphicsBuffer デュアルモード
5. **拡張性**: 将来の機能追加に対応可能な設計

この実装は、Blender の Geometry Nodes と Unity VFX Graph をシームレスに連携させ、プロシージャルなパーティクルエフェクトのリアルタイムプレビューを可能にする重要なマイルストーンです。

---

**関連ドキュメント**:
- [VFX_GRAPH_INTEGRATION.md](VFX_GRAPH_INTEGRATION.md) - 詳細なセットアップガイド
- [PHASE3A_COMPLETE.md](PHASE3A_COMPLETE.md) - Phase 3A 実装レポート
- [PHASE2_COMPLETE.md](PHASE2_COMPLETE.md) - Phase 2 実装レポート
- [GPU_RENDERING_STATUS.md](GPU_RENDERING_STATUS.md) - GPU レンダリングステータス

**実装ファイル**:
- `Unity/GeometrySync/Assets/GeometrySync/Runtime/VFXGraphBridge.cs`
- `Unity/GeometrySync/Assets/GeometrySync/Runtime/VFXGraphBridge.cs.meta`

**実装日**: 2025-11-25
