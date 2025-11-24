# Unity .meta ファイルについて

## 重要：.meta ファイルは Unity が自動生成します

このプロジェクトでは、.meta ファイルを手動で作成していません。
Unity がプロジェクトを開いた時に自動的に生成します。

## 初回起動時の挙動

1. **Unity Hub でプロジェクトを開く**
   ```
   Unity Hub → Add Project
   → E:\GeometrySync\Unity\GeometrySync\
   ```

2. **Unity が自動的に以下を実行:**
   - すべてのアセットをスキャン
   - .meta ファイルを自動生成
   - GUID を割り当て
   - Library/ フォルダを作成

3. **完了まで待つ:**
   - "Importing..." プログレスバーが表示される
   - 数分かかる場合があります
   - 完了後、すべてのファイルが Project ビューに表示される

## .meta ファイルの役割

### GUID (Globally Unique Identifier)
- 各ファイルに一意の ID を割り当て
- ファイル名変更時も参照を維持
- シーン内のコンポーネント参照に使用

### 例
```yaml
fileFormatVersion: 2
guid: 674c91d7e77d0c74eb7ffbecf4c8fd58  # Unity が自動生成
MonoImporter:
  externalObjects: {}
  serializedVersion: 2
  defaultReferences: []
  executionOrder: 0
  icon: {instanceID: 0}
  userData:
  assetBundleName:
  assetBundleVariant:
```

## トラブルシューティング

### "YAML Parsing error" が表示される

**原因:** .meta ファイルのフォーマットが壊れている

**解決策:**
```bash
# 1. Unity を閉じる
# 2. 問題のある .meta ファイルを削除
rm Assets/GeometrySync/Runtime/*.meta

# 3. Unity を再度開く
# → 自動的に正しい .meta ファイルが生成される
```

### "Assembly Definition に関連するスクリプトがない"

**原因:** .asmdef ファイルが Runtime/ フォルダ外にある

**解決策:**
```bash
# .asmdef ファイルを削除
rm Assets/GeometrySync/GeometrySync.asmdef
rm Assets/GeometrySync/GeometrySync.asmdef.meta

# Unity を再起動
```

### "GUID が見つからない"

**解決策:**
```bash
# 1. Unity を閉じる
# 2. Library/ フォルダを削除
rm -rf Unity/GeometrySync/Library/

# 3. Unity を再度開く
# → すべて再インポートされる
```

## ベストプラクティス

### ✅ 推奨

1. **Unity に自動生成させる**
   - .meta ファイルは手動で作成しない
   - Unity がすべて処理する

2. **Git で管理する**
   - .meta ファイルは重要
   - .gitignore から除外しない
   - チーム全体で GUID を共有

3. **削除時は慎重に**
   - .meta を削除すると参照が壊れる
   - Unity 内で削除操作を行う

### ❌ 避けるべき

1. **手動で .meta を編集**
   - YAML フォーマットエラーの原因

2. **Git で .meta を ignore**
   - チームメンバー間で GUID が不一致になる

3. **Library/ を Git に含める**
   - 巨大（~500 MB）
   - 個人ごとに異なる
   - .gitignore で除外必須

## このプロジェクトの方針

**Phase 1（現在）:**
- .meta ファイルは Unity が自動生成
- 手動で作成しない
- Git には含める

**初回セットアップ手順:**
```bash
# 1. プロジェクトをクローン
git clone /path/to/GeometrySync

# 2. Unity Hub でプロジェクトを開く
Unity Hub → Add Project → GeometrySync/Unity/GeometrySync

# 3. Unity が自動的に処理
# - .meta ファイル生成
# - Library/ 作成
# - インポート完了

# 4. すぐに使える！
Assets/Scenes/GeometrySyncDemo.unity を開く
```

## 現在の状態

**✅ 正常:**
- C# スクリプト（4 ファイル）
- Unity シーン（1 ファイル）
- シェーダー（1 ファイル）

**⚠️ Unity が生成:**
- .meta ファイル（自動生成待ち）
- Library/ フォルダ（自動生成待ち）

## 次のステップ

1. **Unity Hub でプロジェクトを開く**
2. **インポート完了を待つ（数分）**
3. **Project ビューですべてのファイルを確認**
4. **デモシーンを開く**

---

**重要:** .meta ファイルは Unity に任せてください！
手動で作成・編集すると YAML エラーが発生します。

**現在のステータス:** ✅ Unity が自動生成する準備完了
