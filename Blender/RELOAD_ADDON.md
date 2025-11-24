# Blender Addon を再読み込み

Debug ログを追加したので、アドオンを再読み込みする必要があります。

## 手順

### 方法1: アドオンを無効化→有効化

1. **Edit → Preferences** (または `Ctrl+,`)
2. **Add-ons** タブ
3. 検索: `GeometrySync`
4. **チェックボックスを外す** (無効化)
5. 2秒待つ
6. **チェックボックスを入れる** (有効化)
7. Preferences を閉じる

### 方法2: Blender を再起動

1. Blender を完全に閉じる
2. Blender を再度開く
3. ファイルを開く

### 方法3: Python コンソールでリロード

1. **Scripting** ワークスペースに切り替え
2. Python Console で実行:

```python
import addon_utils
addon_utils.disable("geometry_sync")
addon_utils.enable("geometry_sync")
```

---

## 再読み込み後の確認

1. **N キー** で GeometrySync パネルを開く
2. **Start Server** をクリック
3. Status: **Connected** になるまで待つ

---

## テスト手順

### 1. Cube を移動

- Cube を選択
- **G キー** (移動)
- マウスを動かす
- **左クリック** で確定

### 2. System Console を確認

**Window → Toggle System Console** で開く

**期待される出力:**
```
[GeometrySync] Depsgraph update: Cube geometry changed
[GeometrySync] Processing 1 dirty objects: {'Cube'}
Streamed Cube: 8 vertices, 12 triangles
```

### 3. Unity Console を確認

**期待される出力:**
```
[MeshStreamClient] Received mesh data: XXXX bytes
[MeshStreamClient] Deserialized: 8 vertices, 36 indices
[GeometrySyncManager] Got mesh from queue: 8 vertices
```

---

## もし何も表示されない場合

### Geometry Nodes のアウトプットを確認

Geometry Nodes エディタで:
1. **Group Output** ノードに **Geometry** が接続されているか確認
2. ノードツリーが正しく評価されているか確認

### 簡単なテスト: Edit Mode で頂点を移動

1. Cube を選択
2. **Tab キー** で Edit Mode に入る
3. **G キー** で頂点を移動
4. **Tab キー** で Object Mode に戻る
5. → これで必ず depsgraph update がトリガーされる

---

**最終更新:** 2025-11-24
