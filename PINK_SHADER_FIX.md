# ピンク色のメッシュを修正

## ✅ 成功！メッシュが表示されました！

キューブが Unity に表示されていますが、ピンク/マゼンタ色になっています。
これはシェーダーの問題を示しています。

---

## 🔧 修正方法

### 手順1: シェーダーのコンパイルエラーを確認

1. **Unity Console** を開く
2. **赤いエラー**を探す
3. シェーダー関連のエラーがあるか確認

### 手順2: マテリアルのシェーダーを確認

1. **Project ビュー** で `Assets/GeometrySync/Materials/GeometrySyncMat` をクリック
2. **Inspector** で **Shader** を確認
3. もし **"Missing"** や **"Error"** と表示されている場合:
   - Shader ドロップダウンをクリック
   - `GeometrySync → Basic URP` を選択

### 手順3: URP Asset を確認

Unity 6000 は URP (Universal Render Pipeline) が必要です。

1. **Edit → Project Settings → Graphics**
2. **Scriptable Render Pipeline Settings** が設定されているか確認
3. もし **None** の場合:
   - `Assets/Settings/UniversalRenderPipelineAsset` を割り当て
   - または新規作成: **Assets → Create → Rendering → URP Asset (with Universal Renderer)**

---

## 📝 代替案: Built-in シェーダーを使用

一時的な解決策として、Unity の標準シェーダーを使用できます:

1. **Project ビュー** で `GeometrySyncMat` を選択
2. **Inspector** で **Shader** を変更:
   - `Universal Render Pipeline → Lit` (URP の場合)
   - または `Standard` (Built-in の場合)

---

## 🎨 期待される結果

修正後:
- メッシュが **白色またはグレー色** で表示される
- ライティングが正しく適用される
- Blender でオブジェクトを動かすとリアルタイムに更新される

---

## 🚀 次のステップ

### リアルタイム更新をテスト

1. **Blender** で Cube を選択
2. **G キー** (移動)
3. マウスを動かす
4. **Unity Scene ビュー**でリアルタイムに更新されることを確認！

### Geometry Nodes をテスト

1. **Blender** で Geometry Nodes modifier を追加
2. ノードのパラメータを変更
3. Unity でリアルタイムに反映されることを確認

---

**現在の状態:** ✅ 基本的なストリーミングは動作中！シェーダーの設定のみが必要

**最終更新:** 2025-11-24
