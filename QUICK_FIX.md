# 🚀 Quick Fix - 接続成功だがメッシュが表示されない

Unity と Blender の接続は成功しているが、メッシュが表示されない場合の解決法

---

## 📋 現状確認

### ✅ 動作している部分
- Blender サーバー起動済み
- Unity 接続成功 (Console: "Connected to Blender server")
- Status: Connected 表示

### ❌ 動作していない部分
- Unity にメッシュが表示されない
- Unity Console に mesh data 受信ログがない
- Blender からデータが送信されていない

---

## 🔧 解決手順

### 手順1: Blender アドオンをリロード

Debug ログを追加したので、アドオンをリロードする必要があります。

#### 方法A: アドオンの再有効化
1. Blender で `Ctrl+,` (Preferences)
2. **Add-ons** タブ
3. 検索: `GeometrySync`
4. ✅ **チェックボックスを外す** (無効化)
5. 2秒待つ
6. ✅ **チェックボックスを入れる** (有効化)
7. Preferences を閉じる

#### 方法B: Blender 再起動
1. Blender を完全に閉じる
2. Blender を起動
3. ファイルを開く

---

### 手順2: サーバーを再起動

1. GeometrySync パネル (N キー) で **"Stop Server"**
2. 2秒待つ
3. **"Start Server"**
4. Status が **"Connected"** になるまで待つ

---

### 手順3: Unity を再起動

1. Unity で **Play ボタンを押して停止**
2. 2秒待つ
3. **Play ボタンを押して開始**
4. Console 確認: `Connected to Blender server at 127.0.0.1:8080`

---

### 手順4: テストスクリプトを実行

Blender でメッシュ送信をテストします。

#### Scripting ワークスペースで:

1. **Scripting** タブに切り替え
2. **Text Editor** で **Open** → `E:\GeometrySync\Blender\test_streaming.py`
3. **▶ Run Script** ボタンをクリック

#### 期待される出力 (System Console):
```
============================================================
Testing GeometrySync Streaming
============================================================

1. Server running: True
   Server connected: True

2. Scheduler enabled: True
   Target FPS: 30

3. Extracting mesh from Cube...
   ✓ Extracted: 36 vertices, 12 triangles

4. Serializing mesh data...
   ✓ Serialized to 1576 bytes

5. Sending to Unity...
   ✓ Successfully sent mesh to Unity!

============================================================
SUCCESS! Check Unity Console for received data logs.
============================================================
```

#### Unity Console で確認:
```
[MeshStreamClient] Received mesh data: 1576 bytes
[MeshStreamClient] Deserialized: 36 vertices, 36 indices
[GeometrySyncManager] Got mesh from queue: 36 vertices
```

---

### 手順5: リアルタイムテスト

テストスクリプトが成功したら、リアルタイム更新をテストします。

1. **Blender:** Cube を選択
2. **Blender:** `G` キー (移動開始)
3. **Blender:** マウスを動かす
4. **Blender:** 左クリックで確定

#### Blender System Console で確認:
```
[GeometrySync] Depsgraph update: Cube geometry changed
[GeometrySync] Processing 1 dirty objects: {'Cube'}
Streamed Cube: 36 vertices, 12 triangles
```

#### Unity Console で確認:
```
[MeshStreamClient] Received mesh data: 1576 bytes
[MeshStreamClient] Deserialized: 36 vertices, 36 indices
[GeometrySyncManager] Got mesh from queue: 36 vertices
```

#### Unity Scene View で確認:
- Cube が表示されるはず!

---

## 🔍 トラブルシューティング

### ログが何も出ない場合

#### Blender 側
```python
# Blender Python Console で実行
from geometry_sync import handlers
scheduler = handlers.get_scheduler()
print(f"Enabled: {scheduler.enabled}")
print(f"FPS: {scheduler.target_fps}")
```

**期待値:**
```
Enabled: True
FPS: 30
```

**もし False なら:**
- サーバーを Stop → Start

#### Unity 側
- Play mode が有効か確認
- Console の Clear をクリックして再テスト

---

### Geometry Nodes の場合

もし Geometry Nodes を使っている場合:

1. **Group Output** ノードに **Geometry** ソケットが接続されているか確認
2. Modifier が **有効** (目のアイコンがオン) か確認
3. **ノードパラメータを変更**して update をトリガー

---

### Edit Mode でテスト

確実に update をトリガーする方法:

1. Cube を選択
2. `Tab` キー → Edit Mode
3. 頂点を選択 (`1` キー)
4. `G` キー → 移動
5. `Tab` キー → Object Mode

→ これで必ず depsgraph update が発生します

---

## ✅ 成功の確認

### Blender System Console
```
[GeometrySync] Depsgraph update: Cube geometry changed
[GeometrySync] Processing 1 dirty objects: {'Cube'}
Streamed Cube: 36 vertices, 12 triangles
```

### Unity Console
```
[MeshStreamClient] Received mesh data: 1576 bytes
[MeshStreamClient] Deserialized: 36 vertices, 36 indices
[GeometrySyncManager] Got mesh from queue: 36 vertices
```

### Unity Scene View
- メッシュが表示される
- Blender で動かすと Unity でリアルタイムに更新される

---

## 📚 関連ドキュメント

- [BLENDER_TROUBLESHOOTING.md](BLENDER_TROUBLESHOOTING.md) - 詳細なトラブルシューティング
- [RELOAD_ADDON.md](Blender/RELOAD_ADDON.md) - アドオンのリロード方法
- [QUICKSTART.md](QUICKSTART.md) - クイックスタートガイド

---

**最終更新:** 2025-11-24
