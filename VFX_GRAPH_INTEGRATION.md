# VFX Graph 連携ガイド

**作成日**: 2025-11-25

GeometrySync のインスタンスデータを Unity VFX Graph に渡して、リアルタイムでパーティクルエフェクトを生成する方法を説明します。

---

## 概要

VFX Graph 連携により、Blender の Geometry Nodes で生成されたインスタンス位置に基づいて、Unity でパーティクルエフェクトをリアルタイムに生成できます。

### ユースケース
- **パーティクル配置**: Geometry Nodes の Instance on Points をパーティクル発生源として使用
- **エフェクトプレビュー**: Blender で配置をリアルタイム調整しながら Unity でエフェクトを確認
- **大規模エフェクト**: 10,000+ のパーティクル発生源をリアルタイムで制御
- **プロシージャルエフェクト**: Geometry Nodes の手続き的な配置を VFX に活用

---

## アーキテクチャ

```
Blender (Geometry Nodes)
  ↓ Instance on Points
GeometrySync (Streaming)
  ↓ TCP
Unity: GPUInstanceRenderer
  ↓ Instance transforms (Matrix4x4[])
Unity: VFXGraphBridge
  ↓ Extract positions → GraphicsBuffer
Unity: VFX Graph
  ↓ Read PositionBuffer
GPU: Particle spawning at instance positions
```

---

## セットアップ手順

### 1. Blender 側の準備

#### Geometry Nodes でインスタンスを作成
```
Geometry Nodes:
  Input Geometry
    ↓
  Distribute Points on Faces (または Instance on Points)
    ↓ Points
  Instance on Points
    ↓ Instance: 小さいメッシュ (例: Cube)
    ↓
  Realize Instances (オフ - インスタンスのまま送信)
```

#### GeometrySync サーバー起動
1. Blender で GeometrySync アドオンを有効化
2. サイドパネル (N キー) → GeometrySync タブ
3. "Start Server" をクリック
4. Port: 8080 (デフォルト)

### 2. Unity 側の準備

#### VFX Graph Asset を作成

1. **Project ウィンドウで右クリック**
   - Create → Visual Effects → Visual Effect Graph
   - 名前: `GeometrySyncParticles.vfx`

2. **VFX Graph を開いて設定**

##### Exposed Properties を追加
VFX Graph ウィンドウで "Blackboard" を開き、以下のプロパティを追加:

**Texture2D モード (推奨)**:
```
- PositionTexture (Texture2D)
- InstanceCount (int)
- TextureWidth (int)
- SpawnRate (float)
```

**GraphicsBuffer モード (レガシー)**:
```
- PositionBuffer (GraphicsBuffer, Structured)
- InstanceCount (int)
- SpawnRate (float)
```

##### Context を設定

**注意**: VFX Graph 17.0.4では、GraphicsBufferからの読み取り方法が制限されています。以下のいずれかの方法を使用してください:

**方法 1: Texture2D を使用 (推奨 - VFX Graph 17.0.4+)**

VFXGraphBridge コンポーネントで `Use Texture2D` にチェックを入れます (デフォルトで有効)。

VFX Graph の Blackboard で以下のプロパティを追加:
```
- PositionTexture (Texture2D)
- InstanceCount (int)
- TextureWidth (int)
- SpawnRate (float)
```

VFX Graph の Initialize Context (推奨設定):

### オプション A: Attribute Map を使用 (最も簡単)

**ステップ 1: Spawn Context を設定**
```
Spawn Context
  → Set Spawn Event Attribute: position (Vector3)
     Source: Custom
     Channels: Position from PositionTexture
```

**重要**: この方法では、VFXGraphBridge が Spawn Event を直接トリガーする必要があります。現在の実装では未対応のため、**オプション B** を使用してください。

---

### オプション B: Set Position with Attribute (推奨)

VFX Graph 17.0.4 では、Inline 式が使えない場合があります。代わりに **Attribute** を使用します:

**ステップ 1: Initialize Particle Context に Set Position ブロックを追加**
- 右クリック → Blocks → Set Position

**ステップ 2: Position を Attribute にバインド**
- Set Position ブロックの Position フィールドの右側にある **小さな丸 (◉)** をクリック
- **Use Attribute** を選択
- Attribute 名: `position` と入力

**ステップ 3: Spawn Context で Attribute を設定**
```
Spawn Context
  ↓
Set Attribute: position
  Source: Custom
  Value: Sample Texture2D を使用 (以下参照)
```

---

### オプション C: 簡略化アプローチ - 直接 particleIndex を使用 (最も簡単)

VFX Graph 17.0.4 で確実に動作する最も簡単な方法:

**ステップ 1: Spawn Context を Burst で設定**
```
Spawn Context
  → Constant Spawn Rate
     Rate: InstanceCount (1回だけスポーン)
```

**ステップ 2: Initialize Particle で位置を計算**

1. **particleIndex から UV を直接計算**
   - VFX Graph では、数式ノードを使用して UV を計算します

2. **必要なノード接続** (簡略版):
   ```
   [Attribute: particleIndex]  [Property: TextureWidth]
         ↓                              ↓
   [Cast to Float]                      |
         ↓                              ↓
   +-----[Modulo]----------------------+ → U (0〜TextureWidth-1)
         ↓                              ↓
   +-----[Divide]----------------------+ → V_raw
         ↓
   +-----[Floor]------------------------+ → V (整数)

   [U] [V]
    ↓   ↓
   [新しい Float2 を作成]
    ↓
   [Divide by TextureWidth] → UV (0〜1)
    ↓
   [Sample Texture2D: PositionTexture]
    ↓
   [Extract XYZ] → Position
    ↓
   [Set Position]
   ```

**ステップ 3: 具体的なノード操作**

1. **Initialize Particle Context** を開く
2. 右クリック → **Blocks** → **Set Position** を追加
3. **Position** フィールドを以下の手順で接続:
   - VFX Graph ウィンドウの空白部分で右クリック
   - **Operator** → **Sequential → particleIndex** を選択
   - さらに演算ノードを追加:
     - Modulo (particleIndex % TextureWidth)
     - Divide (particleIndex / TextureWidth)
     - Floor
   - これらを **Sample Texture2D** ノードに接続

**重要な注意**:
- VFX Graph 17.0.4 では、オペレーター名が異なる場合があります
- `Combine` の代わりに、**2つの Float を直接 Sample Texture2D の UV に接続**できます
- Sample Texture2D ノードは UV 入力を自動的に Float2 として認識します

---

### オプション D: シンプルな代替案 - Position Buffer を使用

もし Texture2D アプローチが複雑すぎる場合、**GraphicsBuffer モード**に切り替えることもできます:

**VFXGraphBridge 設定**:
```
Use Texture2D: OFF (チェックを外す)
```

**VFX Graph 設定**:
```
Blackboard:
- PositionBuffer (GraphicsBuffer)
- InstanceCount (int)

Initialize Particle:
  Set Position
    Position: PositionBuffer[particleIndex % InstanceCount]
```

ただし、VFX Graph 17.0.4 では GraphicsBuffer のサポートが制限されているため、**オプション C を推奨**します。

---

**トラブルシューティング**:

もしノード接続が複雑すぎる場合、以下の**最もシンプルな方法**を試してください:

1. **VFX Graph で Output Particle Context のみ使用**
2. **Initialize Particle Context で Set Position を削除**
3. **代わりに、C# スクリプト側から VFX Graph に位置を直接送信**

この場合、VFXGraphBridge.cs を拡張して、**Spawn Event** で位置データを送信する必要があります（将来の実装）。

**方法 2: Custom HLSL Block を使用 (高度、非推奨)**

**⚠️ 警告**: Custom HLSL は複雑で、`SamplerState` の管理が必要です。**通常は方法 1 の Inline 式を使用してください。**

VFX Graph で "Custom HLSL" ブロックを追加する場合の例:

```hlsl
// Custom HLSL Block - VFX Graph 17.0.4 format
// 注意: SamplerState を手動で管理する必要があり、エラーが発生しやすい
float3 GetPositionFromTexture(in Texture2D posTexture, in SamplerState samplerState, in float particleId, in float texWidth)
{
    float2 uv = float2(particleId % texWidth, floor(particleId / texWidth)) / texWidth;
    float4 color = posTexture.SampleLevel(samplerState, uv, 0);
    return color.xyz;
}
```

**重要な注意点**:
- Custom HLSL では関数形式で記述が必須
- `SamplerState` を関数パラメータとして受け取る必要がある
- VFX Graph での `SamplerState` の受け渡しはエラーが発生しやすい
- **SampleLevel エラーが発生する場合は、方法 1 の Inline 式を使用してください**

**強く推奨**: Custom HLSL は複雑なので、**方法 1 の Inline 式** (`SampleTexture2D()`) を使用することを推奨します。Inline 式は VFX Graph 17.0.4 で完全にサポートされており、`SamplerState` の管理も自動的に行われます。

**Update Context**:
```
Update Particle
  ↓
(通常のパーティクル更新処理)
  - Gravity
  - Drag
  - Size over lifetime
  - etc.
```

**Output Context**:
```
Output Particle Quad (or Mesh)
  ↓
Set Color
Set Size
etc.
```

**Spawn Context**:
```
Spawn
  - Rate: SpawnRate
```

#### Unity Scene を作成

1. **新規シーン作成**
   - File → New Scene → Basic (URP)
   - 名前: `VFXIntegrationDemo`

2. **GeometrySyncManager GameObject を配置**
   ```
   Hierarchy で右クリック
   → Create Empty
   → 名前: "GeometrySyncManager"
   ```

   **コンポーネント追加**:
   - Add Component → GeometrySync → GeometrySyncManager
   - Add Component → MeshFilter
   - Add Component → MeshRenderer

   **Inspector で設定**:
   - Host: 127.0.0.1
   - Port: 8080
   - Auto Connect: ✓
   - Show Debug Info: ✓

   **Material 設定**:
   - GeometrySyncMat を MeshRenderer に割り当て
   - Material の "Enable GPU Instancing" にチェック

3. **VFX GameObject を配置**
   ```
   Hierarchy で右クリック
   → Visual Effects → Visual Effect
   → 名前: "GeometrySyncVFX"
   ```

   **コンポーネント設定**:
   - Visual Effect:
     - Asset Template: GeometrySyncParticles.vfx

   **VFXGraphBridge を追加**:
   - Add Component → GeometrySync → VFXGraphBridge
   - Instance Renderer: GeometrySyncManager の GPUInstanceRenderer をドラッグ
   - Update Interval: 1
   - Max Particles: 10000
   - Spawn Rate: 1.0
   - Log Updates: ✓ (デバッグ用)

---

## 使用方法

### 1. 基本的な使い方

1. **Unity でプレイモード開始**
   - Play ボタンをクリック
   - GeometrySyncManager が Blender に自動接続

2. **Blender でオブジェクトを編集**
   - Geometry Nodes modifier を持つオブジェクトを選択
   - インスタンス数やパターンを変更
   - 変更がリアルタイムで Unity に反映

3. **VFX が自動的に更新**
   - インスタンス位置でパーティクルが発生
   - Blender での変更が即座に VFX に反映

### 2. パラメータ調整

#### VFXGraphBridge の設定

- **Update Interval**: VFX 更新頻度
  - 0: 毎フレーム更新 (高負荷)
  - 1: 1 フレームおき (推奨)
  - 5: 5 フレームおき (低負荷)

- **Max Particles**: 最大パーティクル発生源数
  - 大量のインスタンスがある場合の制限
  - 例: 50,000 インスタンス → 10,000 に制限

- **Spawn Rate**: パーティクル発生レート倍率
  - 1.0: デフォルト
  - 2.0: 2 倍の発生レート
  - 0.5: 半分の発生レート

#### VFX Graph の設定

VFX Graph Asset を編集して、パーティクルの見た目や挙動をカスタマイズ:

- **Spawn Rate**: パーティクル発生頻度
- **Lifetime**: パーティクル寿命
- **Size**: パーティクルサイズ
- **Color**: パーティクルカラー
- **Velocity**: 初速度 (例: ランダム方向に飛ばす)

---

## サンプル VFX Graph 設定 (VFX Graph 17.0.4)

### 例 1: シンプルなスパーク

**Blackboard プロパティ**:
- PositionTexture (Texture2D)
- InstanceCount (int)
- TextureWidth (int)
- SpawnRate (float)

**Initialize Particle Context**:
```
Initialize Particle
  ↓
Set Position (Inline)
  Position: SampleTexture2D(PositionTexture,
                            float2(particleId % TextureWidth, particleId / TextureWidth) / float(TextureWidth)).xyz
  ↓
Set Velocity
  Velocity: Random Direction in Cone
    Direction: (0, 1, 0)
    Cone Angle: 30
    Speed: Random(1.0, 3.0)
  ↓
Set Lifetime
  Lifetime: Random(0.5, 1.5)
  ↓
Set Size
  Size: 0.1
```

**Update Particle Context**:
```
Update Particle
  ↓
Add Gravity
  Gravity: (0, -9.81, 0)
  ↓
Linear Drag
  Drag: 0.5
```

**Output Particle Context**:
```
Output Particle Quad
  ↓
Set Color (over Lifetime)
  Gradient: White → Orange → Black
  ↓
Blend Mode: Additive
```

**Spawn Context**:
```
Constant Spawn Rate
  Rate: SpawnRate
```

---

### 例 2: 浮遊パーティクル

**Initialize Particle Context**:
```
Initialize Particle
  ↓
Set Position (Inline)
  Position: SampleTexture2D(PositionTexture,
                            float2(particleId % TextureWidth, particleId / TextureWidth) / float(TextureWidth)).xyz
  ↓
Set Velocity
  Velocity: Random Direction in Sphere
    Speed: Random(0.05, 0.15)
  ↓
Set Lifetime
  Lifetime: Random(2.0, 5.0)
  ↓
Set Size
  Size: Random(0.05, 0.2)
```

**Update Particle Context**:
```
Update Particle
  ↓
Turbulence (Noise)
  Intensity: 0.5
  Frequency: 1.0
  Octaves: 3
  ↓
Linear Drag
  Drag: 1.0
```

**Output Particle Context**:
```
Output Particle Quad
  ↓
Set Color
  Gradient: Cyan → Blue → Purple
  ↓
Blend Mode: Alpha Blend
```

---

### 例 3: 爆発エフェクト

**Initialize Particle Context**:
```
Initialize Particle
  ↓
Set Position (Inline)
  Position: SampleTexture2D(PositionTexture,
                            float2(particleId % TextureWidth, particleId / TextureWidth) / float(TextureWidth)).xyz
  ↓
Set Velocity
  Velocity: Random Direction
    Speed: Random(3.0, 7.0)
  ↓
Set Lifetime
  Lifetime: Random(0.2, 0.8)
  ↓
Set Size
  Size: 0.3
```

**Update Particle Context**:
```
Update Particle
  ↓
Add Gravity
  Gravity: (0, -9.81, 0)
  ↓
Set Size (over Lifetime)
  Size: Lerp(0.3, 0.0, age / lifetime)
```

**Output Particle Context**:
```
Output Particle Quad
  ↓
Set Color (over Lifetime)
  Gradient: Yellow → Orange → Red → Black
  ↓
Blend Mode: Additive
```

---

### 例 4: トレイル (軌跡表示) - Sample Buffer 使用

**概要**: GraphicsBuffer と Sample Buffer ノードを使用して、パーティクルの移動軌跡を可視化します。

**重要**: VFX Graph 17.0.4 では **Output ParticleStrip Quad** を使用します。

#### ステップ 1: VFXGraphBridge の設定

1. **VFXGraphBridge コンポーネント**を選択
2. **Use Texture2D**: **OFF** (チェックを外す) → GraphicsBuffer モードになります

#### ステップ 2: VFX Graph の Blackboard プロパティ

Blackboard に以下のプロパティを追加:

```
- PositionBuffer (Type: GraphicsBuffer)
- InstanceCount (Type: int)
```

#### ステップ 3: Particle Strip System を作成

1. **VFX Graph ウィンドウの空白部分を右クリック**
2. **Create System** → **Particle Strip System** を選択

自動的に以下が作成されます:
- Initialize Particle Strip Context
- Update Particle Strip Context
- Output ParticleStrip Quad Context

#### ステップ 4: Spawn Context の設定

```
Spawn Context
  ↓
Single Burst
  Count: InstanceCount (Blackboard から)
  Delay: 0
  Loop: Infinite
  Loop Duration: 999999
```

#### ステップ 5: Initialize Particle Strip - Sample Buffer 使用

**Sample Buffer ノードの追加方法**:

1. **Initialize Particle Strip Context** に **Set Position** ブロックを追加
2. Position フィールドの横にある **小さな丸 (◉)** をクリック → **Create Operator**
3. **Operator** → **Sampling** → **Sample Buffer** を選択
4. **Sample Buffer ノードの右上の ⚙️ アイコン**をクリック
5. **Type** を **Vector3** に設定

**ノード接続**:

```
[Attribute: particleIndex]  [Property: InstanceCount]
      ↓                              ↓
      +-----[Modulo]------------------+ → Index (0 〜 InstanceCount-1)
                ↓
         [Sample Buffer]
           Buffer: PositionBuffer (Blackboard)
           Index: 上記の Modulo の結果
           Stride: 12 (Vector3 = 4 bytes × 3)
                ↓
           [Extract XYZ] → Position
                ↓
          [Set Position]
```

**重要**: Sample Buffer の **Stride** は **12** に設定してください (Vector3 = float × 3 = 4 bytes × 3 = 12 bytes)

#### ステップ 6: Initialize Particle Strip の完全な設定

```
Initialize Particle Strip
  ↓
Set Position
  Position: Sample Buffer (上記参照)
  ↓
Set Lifetime
  Lifetime: 999999 (ほぼ無限)
  ↓
Set Size
  Size: 0.5
```

#### ステップ 7: Update Particle Strip Context

```
Update Particle Strip
  ↓
Set Strip Size (over Strip Index)
  Size: Curve (start: 0.2 → end: 0.0)
  (トレイルの先端が細くなる)
```

#### ステップ 8: Output ParticleStrip Quad Context

```
Output ParticleStrip Quad
  ↓
Set Color
  Color: Cyan
  ↓
Blend Mode: Additive
  ↓
Tiling Mode: Stretch
```

#### トレイルの応用例

**注意**: 以下の例は全て **Particle Strip System** + **Sample Buffer** を使用します。

**例 A: 静的トレイル (インスタンス位置に固定)**

インスタンス位置にトレイルが固定され、Blender で位置を変更するとリアルタイムで更新されます。

```
Spawn Context:
  - Single Burst (Count: InstanceCount, Loop: Infinite, Duration: 999999)

Initialize Particle Strip:
  - Set Position: Sample Buffer (Stride: 12)
  - Set Lifetime: 999999
  - Set Size: 0.3

Update Particle Strip:
  - Set Strip Size (over Strip Index): 0.2 → 0.0

Output ParticleStrip Quad:
  - Color: Cyan
  - Blend Mode: Additive
```

**例 B: 動的トレイル (パーティクルが移動)**

インスタンス位置からパーティクルが放出され、移動しながら軌跡を残します。

この場合、通常のパーティクルシステムから Particle Strip を発生させます:

```
[通常のパーティクルシステム]
Spawn Context:
  - Single Burst (Count: InstanceCount)

Initialize Particle:
  - Set Position: Sample Buffer (Stride: 12)
  - Set Velocity: Random Direction (Speed: 2-5)
  - Set Lifetime: 3.0

Update Particle:
  - Add Gravity: (0, -9.81, 0)
  - Trigger Event Rate: 60

[Particle Strip System - GPU Event から]
Initialize Particle Strip (from GPU Event):
  - Inherit Source Position: ON (親パーティクルの位置を継承)
  - Set Size: 0.1

Update Particle Strip:
  - Set Strip Size (over Strip Index): 0.15 → 0.0

Output ParticleStrip Quad:
  - Color Gradient: Yellow → Orange → Red → Black
  - Blend Mode: Additive
```

**例 C: 蛍のような軌跡**

```
Spawn Context:
  - Single Burst (Count: InstanceCount, Loop: Infinite, Duration: 999999)

Initialize Particle Strip:
  - Set Position: Sample Buffer (Stride: 12)
  - Set Lifetime: 999999
  - Set Size: 0.15

Update Particle Strip:
  - Turbulence (Intensity: 0.1, Frequency: 0.3)
  - Set Strip Size (over Strip Index): Constant 0.08

Output ParticleStrip Quad:
  - Color: Bright Green (RGB: 0.5, 1.0, 0.3)
  - Blend Mode: Additive
```

#### Particle Strip のパラメータ解説

**Particle Per Strip Count (ストリップ解像度)**:
- Initialize Particle Strip Context の Capacity 設定
- 1本のトレイルを構成するポイント数
- 値が大きいほど滑らかだが、パフォーマンス負荷が高い
- 推奨値: 32-128

**Strip Capacity (総容量)**:
- 同時に存在できるストリップの総数
- InstanceCount と同じか、それ以上に設定
- 例: InstanceCount が 100 なら、Strip Capacity も 100 以上

**Set Strip Size (ストリップの太さ)**:
- Update Particle Strip Context で設定
- over Strip Index でカーブを設定可能
- 推奨: 始点が太く、終点が細い (0.2 → 0.0)

**Tiling Mode (UV モード)**:
- Output ParticleStrip Quad の設定
- **Stretch**: テクスチャがストリップ全体に引き延ばされる (デフォルト)
- **Repeat Per Segment**: 各セグメントごとにテクスチャが繰り返される
- **Custom**: 手動で UV を制御

**Sample Buffer の Stride**:
- **非常に重要**: Vector3 の場合は **12** に設定
- 計算式: sizeof(float) × 3 = 4 bytes × 3 = 12 bytes
- 間違えるとデータが正しく読み取れない

#### トラブルシューティング

**問題 1: トレイルが表示されない**
- **原因 A**: Output ParticleStrip Quad ではなく Output Particle Quad を使用している
- **解決**: Particle Strip System を作成し、Output ParticleStrip Quad を使用

- **原因 B**: VFXGraphBridge が Texture2D モードになっている
- **解決**: VFXGraphBridge の Use Texture2D を **OFF** にする

- **原因 C**: PositionBuffer が VFX Graph に設定されていない
- **解決**: Blackboard に PositionBuffer (GraphicsBuffer 型) を追加

**問題 2: トレイルが正しい位置に表示されない**
- **原因**: Sample Buffer の Stride が間違っている
- **解決**: Stride を **12** に設定 (Vector3 = 12 bytes)

**問題 3: トレイルが途切れ途切れ**
- **原因**: Particle Per Strip Count が低すぎる
- **解決**: 32 または 64 に増やす

**問題 4: トレイルが太すぎる/細すぎる**
- **原因**: Set Strip Size の値が適切でない
- **解決**: Set Strip Size ブロックで値を調整 (推奨: 0.05-0.3)

**問題 5: 一部のインスタンスでトレイルが表示されない**
- **原因**: Strip Capacity が InstanceCount より小さい
- **解決**: Strip Capacity を InstanceCount 以上に設定

**問題 6: パフォーマンスが低い**
- **原因**: Particle Per Strip Count が高すぎる、またはパーティクル数が多すぎる
- **解決**:
  - Particle Per Strip Count を 16-32 に下げる
  - VFXGraphBridge の Max Particles を下げる
  - Update Interval を上げる (例: 2-5)

**問題 7: Sample Buffer ノードが見つからない**
- **原因**: VFX Graph のバージョンが古い可能性
- **解決**: Operator → Sampling → Sample Buffer を探す
- または: Operator → StructuredBuffer → Sample Structured Buffer を試す

---

## パフォーマンス最適化

### GraphicsBuffer のメモリ管理

VFXGraphBridge は自動的に以下を管理します:
- インスタンス数変化時に GraphicsBuffer を再作成
- 不要になった GraphicsBuffer を自動解放
- 位置データを毎フレーム GPU にアップロード

### 推奨設定

#### 小規模 (< 1,000 インスタンス)
```
Update Interval: 0 (毎フレーム)
Max Particles: 1000
Spawn Rate: 1.0
```

#### 中規模 (1,000 - 5,000 インスタンス)
```
Update Interval: 1 (1 フレームおき)
Max Particles: 5000
Spawn Rate: 0.5
```

#### 大規模 (5,000+ インスタンス)
```
Update Interval: 2-5 (数フレームおき)
Max Particles: 10000
Spawn Rate: 0.2
```

---

## トラブルシューティング

### パーティクルが表示されない

**原因 1**: VFX Graph のプロパティが正しく設定されていない

**解決**:
- VFX Graph の Blackboard で以下を確認:
  - PositionBuffer (GraphicsBuffer)
  - InstanceCount (int)
  - SpawnRate (float)
- プロパティ名が VFXGraphBridge と一致しているか確認

**原因 2**: GPUInstanceRenderer にインスタンスデータがない

**解決**:
- Unity Console で `[VFXGraphBridge] No instances available` を確認
- GeometrySyncManager が Blender に接続されているか確認
- Blender でインスタンスを持つオブジェクトが選択されているか確認

### パーティクルの位置がおかしい

**原因**: Blender と Unity の座標系変換

**解決**:
- GeometrySync は自動的に座標系を変換 (Blender: Y-up → Unity: Y-up)
- VFXGraphBridge は変換済みの位置を使用
- 問題が続く場合は GeometrySyncManager の Transform をリセット

### パフォーマンスが低い

**原因 1**: 更新頻度が高すぎる

**解決**:
- VFXGraphBridge の Update Interval を増やす (例: 0 → 2)

**原因 2**: パーティクル数が多すぎる

**解決**:
- VFXGraphBridge の Max Particles を減らす
- VFX Graph の Spawn Rate を下げる

---

## 高度な使用例

### 例 1: インスタンスごとに異なるパーティクル

VFX Graph で particleId を使用して、位置ごとに異なるパーティクルを生成:

```
Initialize Particle:
  - Set Position from PositionBuffer[particleId % InstanceCount]
  - Set Color: Gradient Sample(particleId / InstanceCount)
  - Set Size: 0.1 + 0.1 * (particleId % 10)
```

### 例 2: 時間で変化するエフェクト

VFX Graph で Time を使用:

```
Initialize Particle:
  - Set Position from PositionBuffer[particleId % InstanceCount]
  - Set Velocity: Random Direction * sin(Time)
```

### 例 3: 複数の VFX を連携

同じ GPUInstanceRenderer から複数の VFXGraphBridge を使用:

```
GameObject: GeometrySyncManager
  ↓
GameObject: VFX_Sparks (VFXGraphBridge → Spark VFX)
GameObject: VFX_Smoke (VFXGraphBridge → Smoke VFX)
GameObject: VFX_Glow (VFXGraphBridge → Glow VFX)
```

---

## 技術詳細

### GraphicsBuffer フォーマット

```csharp
GraphicsBuffer.Target.Structured
Stride: sizeof(float) * 3  // Vector3
Data: position.x, position.y, position.z (float)
```

### VFX Graph での読み取り

```
Sample GraphicsBuffer:
  - Buffer: PositionBuffer (GraphicsBuffer)
  - Index: particleId % InstanceCount
  - Output: Vector3 (position)
```

### リフレクション使用について

VFXGraphBridge は GPUInstanceRenderer の private フィールド `_instanceTransforms` にアクセスするため、リフレクションを使用しています:

```csharp
var field = typeof(GPUInstanceRenderer)
    .GetField("_instanceTransforms",
              BindingFlags.NonPublic | BindingFlags.Instance);
var instanceTransforms = field.GetValue(instanceRenderer)
    as Dictionary<uint, Matrix4x4[]>;
```

**将来の改善案**: GPUInstanceRenderer に public API を追加して、リフレクション不要にする

---

## 制限事項

1. **VFX Graph 必須**: Unity の VFX Graph パッケージがインストールされている必要があります
2. **GraphicsBuffer 対応 GPU**: DirectX 11+, Metal, Vulkan が必要
3. **位置のみ**: 現在はインスタンスの位置のみを VFX に渡します (回転・スケールは未対応)
4. **リフレクション使用**: GPUInstanceRenderer の内部データにアクセスするためリフレクションを使用

---

## まとめ

VFX Graph 連携により、Blender の Geometry Nodes で生成したインスタンス配置を、Unity のパーティクルエフェクトにリアルタイムで反映できます。

### 主な利点
- **リアルタイムプレビュー**: Blender での変更が即座に VFX に反映
- **大規模対応**: 10,000+ のパーティクル発生源をサポート
- **柔軟性**: VFX Graph で自由にエフェクトをカスタマイズ可能
- **パフォーマンス**: GPU で効率的に処理

### 今後の拡張案
- インスタンスの回転・スケールデータの転送
- カスタム属性の転送 (色、速度など)
- 複数メッシュ ID のサポート
- VFX Graph テンプレート集の提供

---

**関連ドキュメント**:
- [GPU_RENDERING_STATUS.md](GPU_RENDERING_STATUS.md) - GPU レンダリングステータス
- [PHASE3A_COMPLETE.md](PHASE3A_COMPLETE.md) - Phase 3A 実装レポート
- [PHASE2_COMPLETE.md](PHASE2_COMPLETE.md) - Phase 2 実装レポート

**実装ファイル**:
- `VFXGraphBridge.cs` - VFX Graph 連携ブリッジコンポーネント
