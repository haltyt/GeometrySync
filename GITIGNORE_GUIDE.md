# GeometrySync - .gitignore 構成ガイド

プロジェクトの .gitignore ファイルの構成と役割説明。

## 📁 .gitignore ファイルの配置

```
GeometrySync/
├── .gitignore                           # ルート - プロジェクト全体の共通設定
├── Blender/
│   └── .gitignore                       # Blender 固有の設定
└── Unity/
    └── GeometrySync/
        └── .gitignore                   # Unity 固有の設定
```

## 🎯 役割分担

### 1. ルート .gitignore (`E:\GeometrySync\.gitignore`)

**役割:** プロジェクト全体に共通する設定

**除外対象:**
- OS 生成ファイル（.DS_Store, Thumbs.db など）
- IDE/エディタ設定（.vscode/, .idea/ など）
- 一時ファイル（*.tmp, *.bak など）
- Claude Code 設定（.claude/）

**内容:**
```gitignore
# OS generated files
.DS_Store
.DS_Store?
._*
Thumbs.db
Desktop.ini
ehthumbs.db

# IDE and editors
.vscode/
.idea/
*.swp
*.swo
*~
*.sublime-project
*.sublime-workspace

# Temporary files
*.tmp
*.bak
*.log
*~

# Claude Code
.claude/
```

**特徴:**
- ✅ プラットフォーム非依存の汎用設定
- ✅ すべてのサブディレクトリに適用
- ✅ 開発環境に依存しない

---

### 2. Blender .gitignore (`Blender/.gitignore`)

**役割:** Blender 固有の自動生成ファイルを除外

**除外対象:**
- Python キャッシュ（__pycache__/, *.pyc）
- Blender バックアップファイル（*.blend1, *.blend1~）
- Blender 一時ファイル（*.autosave）

**内容:**
```gitignore
# Python cache
__pycache__/
*.pyc
*.pyo
*.pyd

# Blender backup files
*.blend1
*.blend1~
*.blend2
*.blend@

# Blender temporary files
*.autosave

# OS files (if not caught by root .gitignore)
.DS_Store
Thumbs.db
```

**特徴:**
- ✅ Blender と Python に特化
- ✅ アドオン開発時の不要ファイルを除外
- ✅ .blend ファイル自体は除外しない（test_scenes 用）

---

### 3. Unity .gitignore (`Unity/GeometrySync/.gitignore`)

**役割:** Unity 固有の自動生成ファイルを除外

**除外対象:**
- Unity キャッシュ（Library/, Temp/, Logs/ など）
- Visual Studio 自動生成（*.csproj, *.sln など）
- Unity デバッグファイル（*.pdb, *.mdb など）

**内容:**
```gitignore
# Unity generated files
[Ll]ibrary/
[Tt]emp/
[Oo]bj/
[Bb]uild/
[Bb]uilds/
[Ll]ogs/
[Mm]emoryCaptures/
[Uu]serSettings/

# Visual Studio / Rider generated
.vs/
.idea/
*.csproj
*.sln
*.suo
*.user
*.userosscache
*.sln.docstates
*.userprefs
ExportedObj/
*.unityproj
*.booproj

# Unity meta files for generated files
*.pidb.meta
*.pdb.meta
*.mdb.meta

# Debug files
*.pdb
*.mdb
*.opendb
*.VC.db

# Unity Asset Store Tools
AssetStoreTools/

# Unity crash logs
sysinfo.txt
*.stackdump

# Crashlytics
crashlytics-build.properties
```

**特徴:**
- ✅ Unity に特化した詳細設定
- ✅ 大文字/小文字を区別しないパターン使用
- ✅ Library/ フォルダを除外（サイズ大）

---

## 📊 Git 管理対象ファイル

### ✅ Git に含まれるファイル

**ドキュメント:**
```
README.md
QUICKSTART.md
INSTALL.md
INDEX.md
QUICK_REFERENCE.md
SETUP_GUIDE.md
PROJECT_STRUCTURE.md
PROJECT_LAYOUT.md
IMPLEMENTATION_SUMMARY.md
DISTRIBUTION.md
FINAL_STATUS.md
GITIGNORE_GUIDE.md
LICENSE
```

**Blender:**
```
Blender/addons/geometry_sync/
├── __init__.py
├── server.py
├── extractor.py
├── serializer.py
├── handlers.py
└── ui.py

Blender/addons/geometry_sync.zip  # 配布用
```

**Unity:**
```
Unity/GeometrySync/
├── Assets/
│   ├── GeometrySync/
│   │   ├── Runtime/*.cs
│   │   └── Shaders/*.shader
│   └── Scenes/*.unity
├── Packages/
│   ├── manifest.json
│   └── packages-lock.json
└── ProjectSettings/
    ├── ProjectVersion.txt
    └── ProjectSettings.asset
```

**設定:**
```
.gitignore (3 ファイル)
Unity/GeometrySync/.gitignore
Blender/.gitignore
```

---

### ❌ Git から除外されるファイル

**Unity 自動生成:**
```
Unity/GeometrySync/Library/      # ~500 MB
Unity/GeometrySync/Temp/
Unity/GeometrySync/Logs/
Unity/GeometrySync/UserSettings/
Unity/GeometrySync/*.csproj
Unity/GeometrySync/*.sln
Unity/GeometrySync/.vs/
```

**Blender 自動生成:**
```
Blender/**/__pycache__/
Blender/**/*.pyc
Blender/**/*.blend1
```

**OS/IDE:**
```
.DS_Store
Thumbs.db
.vscode/
.idea/
```

**Claude Code:**
```
.claude/
```

---

## 🔍 .gitignore パターン説明

### ワイルドカード

| パターン | 意味 | 例 |
|----------|------|-----|
| `*.tmp` | すべての .tmp ファイル | `file.tmp` |
| `**/*.pyc` | すべてのサブディレクトリの .pyc | `any/path/file.pyc` |
| `[Ll]ibrary/` | Library または library フォルダ | `Library/` or `library/` |
| `!important.tmp` | 例外（除外しない） | `important.tmp` は追跡 |

### Unity の大文字小文字パターン

Unity では OS によってフォルダ名の大文字小文字が異なる場合があるため：

```gitignore
[Ll]ibrary/    # Library/ または library/
[Tt]emp/       # Temp/ または temp/
[Oo]bj/        # Obj/ または obj/
```

---

## 🛠️ .gitignore の確認方法

### 除外されているファイルを確認

```bash
cd E:\GeometrySync

# 除外されているファイルを表示
git status --ignored

# 特定のファイルが除外されているか確認
git check-ignore -v Unity/GeometrySync/Library/metadata
```

### .gitignore が正しく機能しているか確認

```bash
# ステージングエリアに何が追加されるか確認
git add --dry-run .

# 追跡されているファイル一覧
git ls-files
```

---

## 🔧 .gitignore のテスト

### 新しいプロジェクトでテスト

```bash
# 1. 新しいフォルダにクローン
git clone /path/to/GeometrySync test-clone
cd test-clone

# 2. Unity を開く
# Unity Hub → Add Project → test-clone/Unity/GeometrySync

# 3. Library/ が生成される
# 4. git status で Library/ が表示されないことを確認
git status

# 期待結果: "nothing to commit, working tree clean"
```

---

## 📋 .gitignore ベストプラクティス

### ✅ 推奨

1. **階層的に配置**
   - ルート: 共通設定
   - サブディレクトリ: 固有設定

2. **コメントを付ける**
   ```gitignore
   # Unity generated files
   Library/
   ```

3. **グループ化**
   - 関連する項目をまとめる
   - セクションごとに分ける

4. **大文字小文字を考慮**
   - Unity: `[Ll]ibrary/`
   - クロスプラットフォーム対応

### ❌ 避けるべき

1. **重複する設定**
   - ルートと サブディレクトリで同じパターン

2. **過度に広範なパターン**
   - `*.cs` など（必要なファイルまで除外）

3. **絶対パス**
   - 相対パスを使用

---

## 🆘 トラブルシューティング

### "Library/ が Git に含まれている"

**原因:** .gitignore 追加前にコミット済み

**解決:**
```bash
# Git から削除（ファイルは保持）
git rm -r --cached Unity/GeometrySync/Library/

# コミット
git commit -m "Remove Library from Git tracking"
```

### ".gitignore が効かない"

**原因:** すでに追跡されているファイル

**解決:**
```bash
# キャッシュをクリア
git rm -r --cached .

# 再度追加
git add .

# コミット
git commit -m "Update .gitignore"
```

### "特定のファイルが除外されない"

**確認:**
```bash
# どの .gitignore ルールが適用されているか
git check-ignore -v path/to/file
```

---

## 📊 .gitignore 効果の確認

### Git リポジトリサイズ

**Library/ なし（正しい）:**
```bash
du -sh .git
# 期待: ~1-2 MB
```

**Library/ あり（間違い）:**
```bash
du -sh .git
# 問題: ~500+ MB
```

### 追跡ファイル数

```bash
git ls-files | wc -l
# 期待: ~30 ファイル

git ls-files | grep -v "\.meta$" | wc -l
# Unity meta ファイル除外
```

---

## 📝 .gitignore メンテナンス

### 定期的な確認

1. **新しいファイルタイプの追加時**
   - Unity プラグイン追加時
   - 新しいビルドターゲット追加時

2. **Unity バージョンアップ時**
   - 新しい自動生成ファイル確認

3. **プルリクエスト前**
   - `git status --ignored` で確認

### テンプレート

GitHub の公式テンプレートを参考：
- [Unity .gitignore](https://github.com/github/gitignore/blob/main/Unity.gitignore)
- [Python .gitignore](https://github.com/github/gitignore/blob/main/Python.gitignore)

---

## ✅ まとめ

**3 つの .gitignore:**
1. **ルート** - OS/IDE の共通設定
2. **Blender/** - Python/Blender 固有
3. **Unity/GeometrySync/** - Unity 固有

**役割分担:**
- ✅ 各ディレクトリで関連する設定のみ
- ✅ 重複を避ける
- ✅ メンテナンスしやすい

**効果:**
- ✅ リポジトリサイズ: ~1-2 MB（Library なし）
- ✅ 追跡ファイル: ~30 ファイル
- ✅ クローン後すぐに使える

---

**正しく設定されています！** ✅

Git リポジトリに不要なファイルは含まれず、クリーンな状態を維持できます。
