# わたあめメーカー

ざらめを入れてスイッチオン。棒をくるくる回して、自分だけのかたちのわたあめを作る 3D ゲームです。

- エンジン: Unity 6 (6000.5.4f1)
- 3D モデル: Blender 5.2 (Python スクリプトで生成)
- CI/CD: GitHub Actions + [GameCI](https://game.ci)

## あそびかた

1. **ざらめを えらぶ** — 左のパネルで量 (5〜30 g) と色を選んで「ざらめを いれる」。色ちがいを続けて入れると、層になったわたあめができます
2. **スイッチ** — 下のボタン (または機械のスイッチを直接クリック) で ON。ヒーターが温まると糸が出はじめます
3. **棒で まきとる** — 棒をボウルの中に入れて回すと、ヘッドから飛んできた糸が少しずつ巻きつきます。回し方・動かし方・傾け方でかたちが変わります
4. **できあがり！** — 名前・重さ・大きさ・まんまる度を評価します

| 操作 | マウス | キーボード |
| --- | --- | --- |
| 棒を水平に動かす | 左ドラッグ | WASD / 矢印 |
| 棒を上げ下げ | ホイール | R / F |
| 棒を回す | 右ドラッグ (左右) | Q / E |
| 棒を傾ける | 右ドラッグ (上下) | Z / C |
| 自動でくるくる | — | スペース |
| カメラを回す | 中ドラッグ / Alt + 左ドラッグ | — |

## しくみ

- **わたあめの成長**: 棒のまわりに 3D の密度グリッド (`CandyField`) を持ち、ヘッドから飛ばした糸 (レイ) がわたあめの表面か棒に当たった位置に密度を積みます。ヘッド側だけが育つので、回したり動かしたりすると形が変わります
- **メッシュ化**: 密度グリッドを Naive Surface Nets (`SurfaceNets`) で毎秒十数回メッシュにし、シェル (毛皮) 描画のシェーダーで糸のふわふわ感を出しています
- **色**: 積もった位置ごとに色も記録しているので、途中で色を変えると縞やまだらになります
- **レンダリング**: 自前ライティングのシェーダーだけで描くので、Built-in / URP どちらでも同じ見た目になります
- **入力**: IMGUI のイベントから読むので、Input Manager / Input System どちらの設定でも動きます
- **シーン**: `WataameGame` が実行時にカメラ・機械・棒・UI をすべてコードで組み立てます

```
Assets/
  Resources/Models/   Blender から書き出した FBX (WataameMachine / Stick)
  Resources/Shaders/  Wataame/Lit, CottonCandy, Particle, Backdrop
  Resources/Fonts/    M PLUS Rounded 1c (OFL)
  Scenes/Main.unity   WataameGame だけを置いたシーン
  Scripts/Runtime/    ゲーム本体
  Scripts/Editor/     ビルドスクリプト、モデルのインポート設定
  Tests/EditMode/     ユニットテスト + わたあめ作りのシミュレーション
Blender/
  generate_models.py  モデル生成スクリプト
  wataame_models.blend 生成結果 (確認・手直し用)
```

## 開発

### Unity で開く

Unity Hub でこのフォルダを開き、`Assets/Scenes/Main.unity` を再生します (シーンを開かずに再生しても自動で起動します)。

メニュー **Wataame** から:

- `Setup Main Scene` — シーンとビルド設定を作り直す
- `Build/Windows`, `Build/WebGL` — ローカルビルド (`Builds/` に出力)
- `Regenerate Models (Blender)` — Blender でモデルを作り直す (見つからないときは環境変数 `BLENDER_PATH` を設定)

### モデルを作り直す

```bash
blender -b --factory-startup --python Blender/generate_models.py -- --out Assets/Resources/Models --blend Blender/wataame_models.blend
```

寸法は `Assets/Scripts/Runtime/GameConfig.cs` の定数と合わせています。

### テスト

Unity の Test Runner (EditMode) か、コマンドラインで:

```bash
Unity -batchmode -projectPath . -runTests -testPlatform EditMode -testResults Logs/results.xml
```

### 自動操縦で動作確認

ビルドした実行ファイルに引数をつけると、ざらめ投入 → スイッチ → 棒を回してわたあめを作り、スクリーンショットを保存して終了します。

```bash
Builds/Windows/Wataame.exe -wataame-autopilot -wataame-screenshot Logs/shots
```

## CI/CD の設定

`.github/workflows/ci.yml` が次を行います。

| ジョブ | タイミング | 内容 |
| --- | --- | --- |
| Blender models | すべて | Blender (Linux) でモデルを生成し、ビルドに使う |
| Unity EditMode tests | すべて | ユニットテスト (結果は Checks に表示) |
| Build | すべて | WebGL / Windows をビルドして Artifact に保存 |
| Deploy to GitHub Pages | `main` へのプッシュ | WebGL 版を GitHub Pages に公開 |
| GitHub Release | `v*` タグのプッシュ | Windows / WebGL の zip をリリースに添付 |

### 必要な準備

1. **Unity ライセンスを Secrets に登録** (Settings → Secrets and variables → Actions)
   - `UNITY_LICENSE` — Unity Personal のライセンスファイル (`.ulf`) の中身。Unity Hub でサインイン・ライセンス有効化をした PC の
     - Windows: `C:\ProgramData\Unity\Unity_lic.ulf`
     - macOS: `/Library/Application Support/Unity/Unity_lic.ulf`
     にあります。詳しくは [GameCI のアクティベーション手順](https://game.ci/docs/github/activation) を参照
   - `UNITY_EMAIL` / `UNITY_PASSWORD` — Unity アカウント
2. **GitHub Pages を有効化** — Settings → Pages → Build and deployment の Source を **GitHub Actions** にする

Secrets が無い場合 (フォークからの PR など) は Unity のジョブを警告つきでスキップし、Blender のジョブだけ実行します。

### リリース

```bash
git tag v1.0.0
git push origin v1.0.0
```

## ライセンス

フォントのライセンスは [THIRD_PARTY_NOTICES.md](THIRD_PARTY_NOTICES.md) を参照してください。
