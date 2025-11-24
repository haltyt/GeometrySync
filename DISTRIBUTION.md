# GeometrySync - Distribution Guide

配布・共有用ガイド（Git、パッケージング、クリーンアップ済み）

## ✅ クリーンアップ完了

プロジェクトは配布可能な状態にクリーンアップされています：

### 削除済みの不要ファイル
- ❌ Unity/Library/ (自動生成キャッシュ)
- ❌ Unity/Temp/ (一時ファイル)
- ❌ Unity/Logs/ (ログファイル)
- ❌ Unity/UserSettings/ (個人設定)
- ❌ Unity/*.csproj (Visual Studio 自動生成)
- ❌ Unity/*.sln (ソリューションファイル)
- ❌ __pycache__/ (Python キャッシュ)

### 保持されているファイル
- ✅ ソースコード (Python, C#, Shader)
- ✅ プロジェクト設定 (ProjectSettings/)
- ✅ パッケージ定義 (Packages/manifest.json)
- ✅ デモシーン (.unity)
- ✅ ドキュメント (.md)
- ✅ .gitignore (Git 管理用)

## 📦 配布用パッケージ

### 完全版（すべて含む）

**推奨：** GitHub リポジトリまたは ZIP アーカイブ

```
GeometrySync/
├── Blender/addons/geometry_sync.zip    # Blender アドオン
├── Unity/GeometrySync/                 # Unity プロジェクト
└── *.md                                # ドキュメント
```

**サイズ:** 約 100KB（ソースコードのみ）
**用途:** 開発者、カスタマイズしたいユーザー

### Blender アドオンのみ

```
配布ファイル:
Blender/addons/geometry_sync.zip (約 8KB)

インストール方法:
Blender → Edit → Preferences → Add-ons → Install
→ geometry_sync.zip を選択
```

### Unity パッケージのみ

**方法1: Unity プロジェクト全体**
```
配布フォルダ:
Unity/GeometrySync/

使用方法:
Unity Hub → Add Project → GeometrySync フォルダを選択
```

**方法2: ポータブルパッケージ**
```
配布フォルダ:
Unity/GeometrySync/Assets/GeometrySync/

使用方法:
既存プロジェクトの Assets/ にコピー
```

## 🔒 Git リポジトリ管理

### .gitignore の配置

プロジェクトには 2 つの .gitignore が含まれています：

**1. ルート .gitignore** (`E:\GeometrySync\.gitignore`)
- Unity 自動生成ファイルを除外
- Blender キャッシュを除外
- OS 固有ファイルを除外

**2. Unity プロジェクト .gitignore** (`Unity/GeometrySync/.gitignore`)
- Unity 固有の詳細設定

### Git で管理されるファイル

```bash
# Git に含まれるファイル（例）
Blender/addons/geometry_sync/*.py          # Python ソース
Unity/GeometrySync/Assets/GeometrySync/    # Unity ソース
Unity/GeometrySync/ProjectSettings/        # プロジェクト設定
Unity/GeometrySync/Packages/manifest.json  # パッケージ定義
*.md                                       # ドキュメント
LICENSE                                    # ライセンス
```

### Git で除外されるファイル

```bash
# Git から除外されるファイル
Unity/GeometrySync/Library/     # Unity キャッシュ
Unity/GeometrySync/Temp/        # 一時ファイル
Unity/GeometrySync/Logs/        # ログ
Unity/GeometrySync/*.csproj     # VS 自動生成
Blender/**/__pycache__/         # Python キャッシュ
```

## 📤 GitHub へのアップロード

### 初回セットアップ

```bash
cd E:\GeometrySync

# Git リポジトリ初期化
git init

# ファイルを追加
git add .

# 初回コミット
git commit -m "Initial commit: GeometrySync v1.0.0

- Blender addon with TCP server
- Unity package with modern Mesh API
- Complete documentation
- Phase 1 implementation complete"

# リモートリポジトリを追加
git remote add origin https://github.com/yourname/GeometrySync.git

# プッシュ
git push -u origin main
```

### 更新の反映

```bash
# 変更を追加
git add .

# コミット
git commit -m "Add feature X / Fix bug Y"

# プッシュ
git push
```

## 📦 ZIP アーカイブ作成

### Windows PowerShell

```powershell
# プロジェクト全体を圧縮
Compress-Archive -Path "E:\GeometrySync\*" -DestinationPath "E:\GeometrySync-v1.0.0.zip"

# Blender アドオンのみ
Compress-Archive -Path "E:\GeometrySync\Blender\addons\geometry_sync" -DestinationPath "E:\geometry_sync.zip"
```

### Linux / macOS

```bash
# プロジェクト全体を圧縮
cd E:\GeometrySync
zip -r GeometrySync-v1.0.0.zip . -x "*.git*" "Unity/GeometrySync/Library/*"

# Blender アドオンのみ
cd Blender/addons
zip -r geometry_sync.zip geometry_sync/
```

## 📋 配布チェックリスト

### リリース前の確認

- [ ] すべてのドキュメントが最新
- [ ] バージョン番号が正しい（README.md、__init__.py）
- [ ] LICENSE ファイルが含まれている
- [ ] .gitignore が正しく設定されている
- [ ] Unity プロジェクトが開ける（Library/ なしでも）
- [ ] Blender アドオンが .zip で提供されている
- [ ] デモシーンが動作する
- [ ] 不要なファイルが除外されている

### 配布ファイル

**必須ファイル:**
- [ ] README.md (プロジェクト概要)
- [ ] QUICKSTART.md (クイックスタート)
- [ ] INSTALL.md (インストール手順)
- [ ] LICENSE (ライセンス)
- [ ] Blender/addons/geometry_sync.zip
- [ ] Unity/GeometrySync/ (プロジェクト全体)

**推奨ファイル:**
- [ ] すべての .md ドキュメント
- [ ] .gitignore
- [ ] Unity/README.md

## 🌐 GitHub Releases

### リリースの作成

1. **GitHub でタグを作成:**
```bash
git tag -a v1.0.0 -m "Version 1.0.0 - Phase 1 Complete"
git push origin v1.0.0
```

2. **GitHub Releases ページ:**
   - Releases → Create a new release
   - Tag: v1.0.0
   - Title: GeometrySync v1.0.0
   - Description: リリースノートを記載

3. **アセットを添付:**
   - `geometry_sync.zip` (Blender アドオン)
   - `GeometrySync-Unity.unitypackage` (オプション)
   - `GeometrySync-v1.0.0-Full.zip` (完全版)

### リリースノート例

```markdown
# GeometrySync v1.0.0 - Phase 1 Complete

## 🎉 Features

- Real-time Blender Geometry Nodes streaming to Unity
- 30-60 FPS @ 50k+ vertices
- Modern Unity Mesh API integration
- URP shader support
- Complete documentation

## 📦 Downloads

- **Blender Addon:** geometry_sync.zip
- **Unity Project:** Clone repository or download source

## 🚀 Quick Start

See [QUICKSTART.md](QUICKSTART.md) for 5-minute setup guide.

## 📖 Documentation

Complete documentation at [INDEX.md](INDEX.md)

## 🐛 Known Issues

See [IMPLEMENTATION_SUMMARY.md](IMPLEMENTATION_SUMMARY.md)

## 🗺️ Roadmap

- Phase 2: Geometry Nodes instances
- Phase 3: Optimization (delta encoding)
- Phase 4: Multi-object support
```

## 📊 ファイルサイズ目安

| コンポーネント | サイズ | 含まれるもの |
|--------------|--------|------------|
| Blender アドオン (.zip) | ~8 KB | Python スクリプトのみ |
| Unity ソース | ~50 KB | C# + Shader |
| ドキュメント | ~100 KB | すべての .md ファイル |
| **合計（ソースのみ）** | **~160 KB** | Git で管理 |
| Unity Library (生成後) | ~500 MB | .gitignore で除外 |

## 🎯 ベストプラクティス

### 開発時

1. **Unity を開く前:**
   - Library/ フォルダが自動生成される
   - .gitignore で除外されているので問題なし

2. **コミット前:**
   - `git status` で不要なファイルが含まれていないか確認
   - `git add .` で .gitignore が正しく機能しているか確認

3. **プッシュ前:**
   - README.md のバージョン番号を更新
   - IMPLEMENTATION_SUMMARY.md を更新

### 配布時

1. **クリーンな状態で配布:**
   - 新しいクローンで動作確認
   - Library/ なしで Unity が開けることを確認

2. **ドキュメント:**
   - QUICKSTART.md で 5 分以内にセットアップできるか確認
   - INSTALL.md で全プラットフォームの手順が正しいか確認

3. **バージョン管理:**
   - セマンティックバージョニング使用 (v1.0.0, v1.1.0, v2.0.0)
   - CHANGELOG.md で変更履歴を管理（推奨）

## 🆘 トラブルシューティング

### "Library フォルダが大きすぎる"

**原因:** Unity の自動生成キャッシュ

**解決策:**
```bash
# Git には含まれていない（.gitignore で除外済み）
# 配布する必要なし
```

### "Git に不要なファイルが含まれている"

**確認:**
```bash
git status --ignored
```

**修正:**
```bash
# .gitignore を更新
git rm -r --cached Unity/GeometrySync/Library
git commit -m "Remove Unity Library from Git"
```

### "ダウンロード後に Unity が開けない"

**原因:** Library フォルダがない

**解決策:** これは正常です！
```
1. Unity Hub でプロジェクトを開く
2. Unity が自動的に Library/ を生成
3. 数分待つ（初回のみ）
4. 完了！
```

## 📝 まとめ

✅ **配布可能な状態:**
- 不要なファイルは除外済み
- .gitignore 設定済み
- ドキュメント完備

✅ **配布方法:**
- GitHub リポジトリ（推奨）
- ZIP アーカイブ
- 個別パッケージ

✅ **サイズ:**
- ソースコードのみ: ~160 KB
- 非常に軽量で共有しやすい

---

**準備完了！** GitHub や配布サイトにアップロード可能です 🚀
