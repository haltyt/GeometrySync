# 🔧 シーンが表示されない場合の対処法

## 問題

Project ビューの Scenes フォルダに **GeometrySyncDemo** が表示されない。

## 原因

Unity がシーンファイルをまだ認識していない（キャッシュの問題）。

---

## ✅ 解決方法（簡単な順）

### 方法1: Assets をリフレッシュ

**Unity エディタで:**
```
1. Project ビューで Assets フォルダを右クリック
2. 「Refresh」を選択

または

キーボードで: Ctrl + R
```

**結果:**
- Unity がすべてのアセットを再スキャン
- Scenes フォルダに GeometrySyncDemo が表示される

---

### 方法2: Reimport All

**Unity エディタで:**
```
Unity メニュー
→ Assets
→ Reimport All
→ 確認ダイアログで「Reimport」をクリック
```

**待機時間:** 1-3分

**結果:**
- すべてのアセットを再インポート
- .meta ファイルも再生成
- 確実にシーンが認識される

---

### 方法3: Unity を再起動

**手順:**
```
1. Unity エディタを完全に閉じる
2. Unity Hub から再度プロジェクトを開く
3. インポート完了を待つ
```

**結果:**
- 完全に再読み込み
- すべてのキャッシュがクリア

---

### 方法4: Library フォルダを削除（最終手段）

**Unity を閉じてから:**
```bash
# Windows PowerShell または Git Bash で実行
cd E:\GeometrySync\Unity\GeometrySync
rm -rf Library/
```

**その後:**
```
1. Unity Hub からプロジェクトを開く
2. 完全再インポート（3-5分）
3. すべてのアセットが認識される
```

⚠️ **注意:** Library フォルダは ~500 MB あるため、削除と再生成に時間がかかります。

---

## 🎯 推奨手順

**まず試す:**
1. ✅ **Ctrl + R** (Refresh) - 5秒
2. ✅ **Assets → Reimport All** - 1-3分
3. ✅ **Unity 再起動** - 2-3分
4. ⚠️ **Library 削除** - 5分（最終手段）

---

## 📋 確認方法

### Project ビューで確認

```
Project ビュー
├── Assets
│   ├── GeometrySync
│   │   ├── Runtime (フォルダ)
│   │   └── Shaders (フォルダ)
│   └── Scenes (フォルダ)  ← ここをクリック
│       └── GeometrySyncDemo  ← これが表示されるはず
```

### Scenes フォルダの場所

**Project ビューで:**
```
Assets → Scenes
```

**ファイルパス:**
```
Assets/Scenes/GeometrySyncDemo.unity
```

---

## 🔍 追加の確認

### ファイルが実際に存在するか確認

**Windows エクスプローラーで:**
```
E:\GeometrySync\Unity\GeometrySync\Assets\Scenes\
```

**確認するファイル:**
- ✓ `GeometrySyncDemo.unity` (10 KB)
- ✓ `GeometrySyncDemo.unity.meta` (152 bytes)

両方存在していれば問題ありません。

### Console でエラー確認

**Unity エディタで:**
```
Window → General → Console
または
Ctrl + Shift + C
```

**確認事項:**
- ❌ 赤いエラーがないか
- ⚠️ 黄色い警告は無視可能

---

## 💡 よくある質問

### Q: Scenes フォルダ自体が表示されない

**A: Assets をリフレッシュしてください**
```
Project ビューで Assets 右クリック → Refresh
```

### Q: シーンを開いたが空っぽ

**A: Hierarchy を確認してください**
```
Hierarchy ビューに以下が表示されるはず:
- Main Camera
- Directional Light
- BlenderMesh
```

### Q: GeometrySyncManager が見つからない

**A: スクリプトが認識されていません**
```
1. Console でコンパイルエラーを確認
2. Assets → Reimport All
3. Unity 再起動
```

---

## 🚀 シーンを開く

### 正しい手順

```
1. Project ビュー → Assets → Scenes
2. 「GeometrySyncDemo」をダブルクリック
3. Scene ビューに内容が表示される
4. Hierarchy に GameObject が表示される
```

### Hierarchy の内容

```
GeometrySyncDemo (シーン名)
├── Main Camera
├── Directional Light
└── BlenderMesh
    ├── MeshFilter
    ├── MeshRenderer
    └── GeometrySyncManager (Script)
```

---

## ✅ 成功の確認

**以下がすべて表示されていれば成功:**
- ✓ Project ビューに Scenes フォルダが表示
- ✓ Scenes フォルダに GeometrySyncDemo が表示
- ✓ ダブルクリックでシーンが開く
- ✓ Hierarchy に 3 つの GameObject が表示
- ✓ BlenderMesh に GeometrySyncManager がアタッチ

---

## 🎮 次のステップ

シーンが正しく表示されたら:

1. **Play ボタンをクリック（▶）**
2. **Game ビューを確認**
3. **Blender でサーバー起動**
4. **接続テスト！**

---

**最も簡単な解決法:** `Ctrl + R` キーでリフレッシュ！

---

**最終更新:** 2025-11-24
