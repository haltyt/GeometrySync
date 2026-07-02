# Vision Pro Metal GPU シェーダー対応

visionOS クライアント(`VisionPro/GeometrySyncVisionPro/`)に、
**LowLevelMesh(visionOS 2.0+)+ Metal コンピュートシェーダー**による GPU パイプラインを実装した。
Unity 側の `MeshReconstructor.cs`(NativeArray ゼロGC)+ `InstancedIndirect.shader`(GPUインスタンシング)に相当する visionOS ネイティブ実装である。

## 背景: visionOS のシェーダー制約

- visionOS の RealityKit は **CustomMaterial(Metal サーフェスシェーダー)非対応**
  (iOS/macOS 専用)。マテリアルをコードから Metal で書くことはできず、
  ShaderGraphMaterial(Reality Composer Pro / MaterialX)のみ
- 一方 **LowLevelMesh(visionOS 2.0+)** により、メッシュの頂点/インデックスバッファを
  Metal バッファとして直接操作できる。プロジェクトのデプロイターゲットは 2.0 なので全面的に利用可能
- 本対応の「Metal シェーダー」は**コンピュートシェーダーによるジオメトリパイプライン**を指す

## アーキテクチャ

### 従来 (CPU パス — フォールバックとして維持)

```
TCP受信 → 全頂点をSwiftでパース(配列生成) → MeshDescriptor構築
        → MeshResource.generate(CPU) → replace
インスタンス: ModelEntity プール N 個の Transform を毎フレーム CPU 更新
```

### 新規 (Metal GPU パス — 既定)

```
TCP受信 → ヘッダ検証のみ(RawMeshData: ゼロコピーslice)
        → Metal ステージングバッファへ memcpy
        → コンピュートカーネルが LowLevelMesh の GPU バッファへ直接書き込み
インスタンス: transforms(64B×N)をアップロード → GPU が全インスタンスを
             1つのマージ済み LowLevelMesh にベイク(単一Entity・単一ドロー)
```

頂点ワイヤフォーマット(pos:12 + normal:12 + uv:8 = 32B interleaved)を
LowLevelMesh の頂点レイアウトにそのまま採用したため、**CPU側の頂点パースが完全に消える**
(ネットワークペイロード → GPU バッファの memcpy のみ)。

## 追加・変更ファイル

| ファイル | 内容 |
|---|---|
| `Rendering/Metal/GeometrySyncKernels.metal` | **新規** — コンピュートカーネル5本: 頂点コピー / 巻き順反転(i0,i2,i1) / 端数インデックスコピー / インスタンス頂点ベイク / インスタンスインデックスベイク |
| `Rendering/Metal/MetalMeshBuilder.swift` | **新規** — LowLevelMesh 管理(2の冪で容量拡張)、ステージングバッファ、カーネルディスパッチ。`MetalContext`(device/queue/pipeline共有)もここ |
| `Rendering/Metal/MetalInstanceRenderer.swift` | **新規** — ベースメッシュ登録(MTLBuffer保持)+ GPU インスタンスベイク。ベイク上限 200万頂点(超過分はクランプ+警告) |
| `Protocol/MeshProtocol.swift` | `RawMeshData` 追加(検証済み・未パースのペイロードslice) |
| `Protocol/MeshDeserializer.swift` | `rawMesh(from:)`(ヘッダ検証+ゼロコピーslice)と `expand(_:)`(CPUフォールバック用展開)に分離 |
| `Network/MeshStreamClient.swift` | meshStream を `AsyncStream<RawMeshData>` 化(受信スレッドでのパース廃止) |
| `App/ImmersiveView.swift` | Metal 利用可否で GPU/CPU パスを自動選択 |

Xcode プロジェクトは filesystem-synchronized group(objectVersion 77)のため、
新規ファイルは自動的にターゲットへ含まれ、`.metal` は `default.metallib` にコンパイルされる。
**pbxproj の編集は不要。**

## 設計上のポイント

- **巻き順反転を GPU 化**: Blender→Unity の左手系巻き順 (i0,i1,i2) を RealityKit の
  右手系 (i0,i2,i1) に変換する処理を `gs_flipWinding` カーネルで実行(1スレッド=1三角形)
- **インスタンスベイク**: RealityKit にはハードウェアインスタンシング API がないため、
  `gs_bakeInstanceVertices` がベースメッシュ×N transform を1つのマージメッシュへ変換
  (WWDC24 の spatial drawing サンプルと同じ手法)。CPU エンティティプール比で
  Transform 書き込み N 回 → コンピュートディスパッチ1回になる
- **法線変換**: transform の上位3×3で変換し正規化(非一様スケールの厳密性より速度優先。
  Unity のインスタンスシェーダーと同等の近似)
- **ステージング再利用の同期**: コマンドバッファ完了を `await` してから次フレームの
  CPU 書き込みを行う(`gsCommitAndWait`)。GPU コピーはサブms級なので 30FPS に影響なし
- **容量拡張**: 頂点/インデックスとも2の冪で成長(Unity 側 NativeArray と同じ戦略)。
  拡張時のみ LowLevelMesh / MeshResource を再生成し、Entity へ再アサイン
- **バウンディングボックス**: メインメッシュは受信データから AABB を算出。
  マージメッシュは「ベースメッシュの外接球 × 各 transform の最大スケール」の合成で近似
- **フォールバック**: Metal デバイス/ライブラリが取れない環境では従来の
  MeshDescriptor + エンティティプール経路に自動で切り替わる

## マテリアルについて

現状は従来どおり `SimpleMaterial`(青)固定。visionOS で見た目をカスタムする場合は
Reality Composer Pro の ShaderGraphMaterial を作成して `ModelEntity` に適用する
(Metal でのサーフェスシェーダー記述は visionOS では不可)。
頂点変形・ジオメトリ加工は本対応のコンピュートカーネルに追加する形で拡張できる
(delta 適用(0x03)のフックポイントも `gs_copyVertices` に用意済み)。

## 検証

- 本変更はコンテナ環境(Linux)で作成しており **Xcode ビルドは未実施**。
  visionOS simulator での確認手順:
  1. `VisionPro/GeometrySyncVisionPro/GeometrySyncVisionPro.xcodeproj` を Xcode 16+ で開く
  2. visionOS 2.0+ simulator でビルド・実行
  3. Blender 側ストリーミング開始 → Connect → Open Immersive View
  4. Xcode コンソールに `Using Metal GPU pipeline (LowLevelMesh + compute shaders)` が出ることを確認
- 性能比較はコンソールの FPS 表示(ContentView)と Instruments の
  RealityKit trace / Metal System Trace で確認する
