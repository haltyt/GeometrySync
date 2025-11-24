# 🚀 Unity プロジェクト初回起動ガイド

## ⚠️ 重要：初回起動時の注意

このプロジェクトには .meta ファイルが含まれていません。
**これは正常です** - Unity が自動的に生成します。

---

## 📋 初回起動手順

### 1️⃣ Unity Hub でプロジェクトを開く

```
Unity Hub を起動
↓
「Add」または「Open」をクリック
↓
フォルダを選択：
E:\GeometrySync\Unity\GeometrySync\
↓
「Select Folder」をクリック
```

### 2️⃣ Unity が自動処理（3-5分）

Unity が以下を自動実行します：

```
✓ すべてのアセットをスキャン
✓ .meta ファイルを自動生成
✓ GUID を割り当て
✓ Library/ フォルダを作成
✓ スクリプトをコンパイル
```

**画面に表示される内容:**
- "Importing..." プログレスバー
- "Compiling Scripts..."
- "Importing Assets..."

**待機時間:** 3-5分（初回のみ）

### 3️⃣ 完了確認

**成功のサイン:**
- ✅ Console にエラーがない
- ✅ Project ビューにファイルが表示される
- ✅ "Importing..." が消える

**Project ビューの内容:**
```
Assets/
├── GeometrySync/
│   ├── Runtime/
│   │   ├── MeshStreamClient
│   │   ├── MeshDeserializer
│   │   ├── MeshReconstructor
│   │   └── GeometrySyncManager
│   └── Shaders/
│       └── GeometrySyncBasic
└── Scenes/
    └── GeometrySyncDemo
```

---

## 🎮 デモシーンを開く

### シーンを開く

```
Project ビュー
↓
Assets → Scenes
↓
「GeometrySyncDemo」をダブルクリック
```

### Hierarchy の内容

```
GeometrySyncDemo
├── Main Camera
├── Directional Light
└── BlenderMesh  ← GeometrySyncManager 設定済み
```

---

## ⚡ すぐにテスト

### Blender 側

```
1. Blender を起動
2. GeometrySync パネルを開く（N キー）
3. 「Start Server」をクリック
4. "Waiting for Unity client..." 表示を確認
```

### Unity 側

```
1. GeometrySyncDemo シーンを開く
2. Play ボタンをクリック（▶）
3. Game ビューに "Status: Connected" 表示
4. Blender で Geometry Nodes を編集
5. Unity でリアルタイム更新を確認！
```

---

## 🐛 トラブルシューティング

### "YAML Parsing error" が表示される

**これは無視してください！**

Unity が .meta ファイルを再生成中です。
数秒待つと自動的に解決します。

### "シーンが見つからない"

**解決策:**
```
Unity メニュー
→ Assets → Reimport All
→ 再インポート完了を待つ
```

### "スクリプトがコンパイルされない"

**確認:**
```
Console ビューを開く（Ctrl + Shift + C）
→ エラーメッセージを確認
→ 赤いエラーがなければ OK
```

**解決策:**
```
Unity メニュー
→ Assets → Refresh (Ctrl + R)
```

### "Library フォルダが大きすぎる"

**これは正常です！**

Library/ フォルダは自動生成され、~500 MB になります。
.gitignore で除外されているので Git には含まれません。

---

## 📊 初回起動チェックリスト

- [ ] Unity Hub でプロジェクトを追加
- [ ] Unity でプロジェクトを開く
- [ ] "Importing..." 完了を待つ（3-5分）
- [ ] Console にエラーがないことを確認
- [ ] Project ビューに Assets が表示される
- [ ] Scenes/GeometrySyncDemo が存在する
- [ ] デモシーンを開ける
- [ ] Hierarchy に BlenderMesh がある
- [ ] Play モードに入れる

**すべて ✓ なら成功です！** 🎉

---

## 📚 次のステップ

初回起動が完了したら：

1. **[QUICKSTART.md](../../QUICKSTART.md)** - 5分セットアップガイド
2. **[README.md](../../README.md)** - プロジェクト概要
3. **Blender 側セットアップ** - アドオンインストール

---

## 💡 ヒント

### エディタレイアウト

推奨レイアウト：
```
上段: Scene ビュー | Game ビュー
下段: Project ビュー | Console ビュー
右側: Inspector ビュー
```

### ショートカット

| キー | 機能 |
|------|------|
| `Ctrl + P` | Play モード切替 |
| `Ctrl + Shift + C` | Console 開く |
| `Ctrl + R` | Refresh |
| `F` | 選択オブジェクトにフォーカス |

---

**🎉 準備完了！Unity で GeometrySync を楽しんでください！**

---

**最終更新:** 2025-11-24
**バージョン:** 1.0.0
