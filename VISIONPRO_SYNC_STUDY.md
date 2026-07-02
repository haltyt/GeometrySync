# Blender → Vision Pro リアルタイム同期方式の検討

参考: [SouthwestAir/Vision-Pro-Blender-Live-Streamer](https://github.com/SouthwestAir/Vision-Pro-Blender-Live-Streamer)

本ドキュメントは、Blender から Apple Vision Pro へジオメトリをリアルタイム同期する方式について、
SouthwestAir の公開実装と GeometrySync の既存アーキテクチャ(`VisionPro/GeometrySyncVisionPro/`)を
比較・検討した結果をまとめたものである。

---

## 1. 前提: 本リポジトリの現状

GeometrySync には既に visionOS ネイティブクライアントの実装が存在する
(`VisionPro/GeometrySyncVisionPro/`、Swift 約1,000行)。

| ファイル | 役割 |
|---|---|
| `Network/MeshStreamClient.swift` | NWConnection ベースの TCP クライアント。Unity 版 `MeshStreamClient.cs` の移植。自動再接続・AsyncStream 配信 |
| `Protocol/MeshDeserializer.swift` | バイナリパース。RealityKit(右手系)向けに巻き順 (i0,i2,i1) 反転済み |
| `Protocol/MeshProtocol.swift` | 0x01=mesh / 0x02=instance / 0x03=delta(予約) |
| `Rendering/MeshBuilder.swift` | MeshResource の生成・更新 |
| `Rendering/InstanceManager.swift` | インスタンス Entity 管理 |
| `App/ImmersiveView.swift` | RealityView + AsyncStream 消費(インスタンス更新は15FPSに間引き) |

つまり「Unity と同じバイナリプロトコル(`[Type:1B][Length:4B][Payload]`、頂点32B stride)を
visionOS でも受信する」方式が既に動く形で存在しており、検討の主眼は
**SouthwestAir 方式(USDZ ストリーミング)に乗り換える・要素を取り込む価値があるか**になる。

## 2. SouthwestAir 方式の分析

### アーキテクチャ

```
[Vision Pro]                                [Blender]
NetService(Bonjour)で                        python-zeroconf で
_visionpro_blender._tcp を広告  ◀──発見────  サービスをブラウズ
NWListener :8080 (TCPサーバー)  ◀──接続────  TCPクライアント
                                            depsgraph handler で編集検知
RealityKit Entity を丸ごと差替  ◀──USDZ────  シーン全体を USDZ エクスポート
                                            (4バイト big-endian 長ヘッダ付き)
```

- **接続方向が GeometrySync と逆**: Vision Pro がサーバーとして待ち受け、Blender がクライアントとして接続する
- **転送単位はシーン全体の USDZ ファイル**: 変更のたびに一時ファイルへエクスポートして全量送信
- **更新レート**: 1〜60 FPS 設定可(既定30)だが、実質はUSDZエクスポート時間で律速
- **アイドル制御**: 編集していない間は送信停止(既定2秒の無操作しきい値)
- **制約(README 記載)**: アニメーション無効、単一接続のみ、高ポリ・複雑マテリアルではエクスポート遅延が顕著

### 長所・短所

| | SouthwestAir (USDZ全量) | GeometrySync (頂点バイナリ) |
|---|---|---|
| マテリアル/テクスチャ | ○ USDZ に埋め込みで完全同期 | × 位置/法線/UVのみ(マテリアルは受信側固定) |
| レイテンシ | △ エクスポート数百ms〜数秒(ポリ数依存) | ○ NumPy 抽出+ソケット送信のみ(ms オーダー) |
| 更新の粒度 | シーン全体を Entity ごと差し替え | メッシュ単位で MeshResource をインプレース更新 |
| 実効フレームレート | 低ポリで数FPS程度が実用限界 | 30FPS ターゲット(既存 Unity 実績と同等) |
| インスタンス(Geometry Nodes) | × 全部ベイクされて肥大化 | ○ 0x02/0x03 で transform のみ送信 |
| デバイス発見 | ○ Bonjour で自動発見(IP入力不要) | × ホストIPを手入力 |
| 一時ファイル I/O | あり(毎送信) | なし(オンメモリ) |
| 実装の単純さ | ○ RealityKit の USDZ ローダに丸投げ | △ プロトコル/デシリアライザを自前維持 |

### 結論(方式選定)

**USDZ 全量ストリーミングへの乗り換えは行わない。** Geometry Nodes の出力を毎フレーム級で
流すという GeometrySync の目的に対して、USDZ エクスポートは律速要因そのものであり、
インスタンスデータも活かせない。既存のバイナリプロトコル方式を Vision Pro でも主軸とする。

ただし SouthwestAir 実装から**取り込む価値がある要素が3点**ある(次節)。

## 3. 取り込むべき要素

### 3.1 Bonjour / Zeroconf によるデバイス発見(優先度: 高)

現状の visionOS クライアントは `host` の既定値が `127.0.0.1` で、これは **simulator
(Mac 上で Blender と同居)でしか機能しない**。実機では Mac の LAN IP を手入力する必要がある。

SouthwestAir と同じ Bonjour を、**接続方向はそのまま**(Blender=サーバー、Vision Pro=クライアント)で導入する:

- **Blender 側**: `python-zeroconf` で `_geometrysync._tcp.local.` をポート8080で広告
  (`server.py` は既に `0.0.0.0` bind 済みなので追加はサービス登録のみ)。
  SouthwestAir 同様、アドオンから zeroconf を自動 pip インストールするユーティリティを用意する
- **visionOS 側**: `NWBrowser` でブラウズし、発見したエンドポイントを
  `NWConnection(to: endpoint)` にそのまま渡す(Network.framework は Bonjour endpoint を直接受け付けるため、
  既存 `MeshStreamClient` の変更は endpoint 生成部のみ)
- **Info.plist**: `NSBonjourServices`(`_geometrysync._tcp`)と
  `NSLocalNetworkUsageDescription` の追加が必須(visionOS のローカルネットワーク権限)

### 3.2 アイドル時の送信停止(優先度: 中)

「編集中のみストリーム、無操作2秒で停止」という SouthwestAir の制御は、
Wi-Fi 越しの実機運用では帯域・バッテリー面で有効。`handlers.py` の FPS スロットリングに
最終 depsgraph 更新時刻ベースの idle 判定を足すだけで実現できる(Unity 側にも同時に効く)。

### 3.3 マテリアルのワンショット USDZ 転送(優先度: 低・将来)

「ジオメトリはバイナリで毎フレーム、マテリアル/テクスチャは変更時のみ USDZ(または独自メッセージ型
0x04)で低頻度転送」というハイブリッドは両方式の良所取りになる。現状 visionOS 側は
`MeshBuilder.getMaterial()` の固定マテリアルなので、需要が出た段階で検討する。

## 4. 実機運用の注意点(帯域試算)

simulator(localhost)では問題にならないが、実機は Wi-Fi 越しになるため帯域が支配的:

```
100k 頂点 × 32B = 3.2MB/メッシュ更新
30FPS → 96MB/s ≈ 770Mbps   ← Wi-Fi 5/6 の実効帯域を超過
10FPS →  32MB/s ≈ 256Mbps  ← Wi-Fi 6 なら現実的な上限付近
```

対策の優先順:
1. **インスタンス活用**: ベースメッシュは変更時のみ、毎フレームは 0x02(transform 64B/個)に寄せる
   — Geometry Nodes のスキャッタ系ならこれだけで数百分の一になる
2. **メッシュ更新の間引き**: 実機向けプリセットとして Blender 側 FPS 上限を 10〜15 に
3. **0x03 delta 実装**(visionOS 側は現状 warning のみ)+ LZ4 圧縮 — トポロジ不変時に位置差分のみ送る
4. USB-C 経由の有線接続(visionOS 2 の開発者ストラップ)も選択肢

その他:
- macOS ファイアウォールで Blender のポート8080受信許可が必要
- Mac と Vision Pro は同一サブネット必須(SouthwestAir も同条件)
- 座標系: serializer.py の `(x,y,z)→(x,z,-y)` は Unity(左手系Y-up)向け。RealityKit は右手系Y-up
  のため、visionOS 側デシリアライザで巻き順反転により対応済み(追加変換不要)

## 5. 推奨ロードマップ

| フェーズ | 内容 | 変更箇所 |
|---|---|---|
| A. 実機疎通(現状構成) | Mac の LAN IP を手入力して実機テスト。Info.plist にローカルネットワーク権限追加 | `Info.plist`, `ContentView.swift`(IP入力UI確認) |
| B. Bonjour 発見 | Blender 側 zeroconf 広告 + visionOS 側 NWBrowser | `server.py`(+ zeroconf), `MeshStreamClient.swift`, `Info.plist` |
| C. 帯域最適化 | idle 停止、実機用 FPS プリセット、delta(0x03)+圧縮 | `handlers.py`, `serializer.py`, `MeshDeserializer.swift` |
| D. マテリアル同期(任意) | 0x04 マテリアルメッセージ or USDZ ワンショット | 両側プロトコル拡張 |

## 6. まとめ

- SouthwestAir 方式(USDZ 全量転送)は「シーンプレビューの空間確認」用途であり、
  GeometrySync が目指す毎フレーム級のプロシージャルジオメトリ同期には不適
- 既存の `VisionPro/GeometrySyncVisionPro/` バイナリ方式を主軸として継続する
- 取り込むのは **Bonjour 自動発見・アイドル送信停止・(将来)マテリアルのワンショット転送**の3点
- 実機化の最大の課題はプロトコルではなく **Wi-Fi 帯域**。インスタンス活用と delta 実装が鍵
