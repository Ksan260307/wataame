# わたあめメーカー

ざらめを入れてスイッチオン。棒をくるくる回して、自分だけのかたちのわたあめを作る 3D ゲームです。

- エンジン: Unity 6 (6000.5.4f1)
- 3D モデル: Blender 5.2 (Python スクリプトで生成)
- CI/CD: GitHub Actions + [GameCI](https://game.ci)

## あそびかた

ブラウザで遊べます: https://ksan260307.github.io/wataame/ (PC・スマホ・タブレット対応)

1. **ざらめを えらぶ** — 量 (5〜30 g) と色を選んで「ざらめを いれる」。色ちがいを続けて入れると、層になったわたあめができます
2. **スイッチ** — ボタン (または機械のスイッチを直接クリック / タップ) で ON。ヒーターが温まると糸が出はじめます
3. **棒で まきとる** — 「ボウルに いれる」で棒をボウルへ。緑のリング (ベストゾーン) の上でくるくる回すと、糸がどんどん巻きつきます。「おまかせ」なら自動で円を描いて回ります
4. **できあがり！** — 名前・重さ・大きさ・まんまる度と、次に作るときのコツが出ます

| 操作 | マウス・キーボード | タッチ |
| --- | --- | --- |
| 棒を動かす | どこでもドラッグ (棒が指の下についてくる) / WASD・矢印 | 1 本指でドラッグ / タップでそこへ移動 |
| 棒の高さ | 「たかさ」スライダー / ホイール / R・F | 「たかさ」スライダー |
| 棒を回す | 自動くるくる / 右ドラッグ / Q・E | 自動くるくる / 2 本指でひねる |
| 傾ける | 右ドラッグ (上下) / Z・C / ◀ ▶ | ◀ ▶ |
| ボウルに入れる / おまかせ | B / T | ボタン |
| カメラ | 中ドラッグ・Alt + ドラッグ / Ctrl + ホイールでズーム / V で視点 | 2 本指ドラッグ / ピンチ |

Shift を押しながら動かすと精密に操作できます。操作まわりの改善点の一覧は [docs/CONTROLS.md](docs/CONTROLS.md) にあります。

## しくみ

- **わたあめの成長**: 棒のまわりに 3D の密度グリッド (`CandyField`) を持ち、ヘッドから飛ばした糸 (レイ) がわたあめの表面か棒に当たった位置に密度を積みます。ヘッド側だけが育つので、回したり動かしたりすると形が変わります
- **メッシュ化**: 密度グリッドを Naive Surface Nets (`SurfaceNets`) で毎秒十数回メッシュにし、シェル (毛皮) 描画のシェーダーで糸のふわふわ感を出しています
- **色**: 積もった位置ごとに色も記録しているので、途中で色を変えると縞やまだらになります
- **レンダリング**: 自前ライティングのシェーダーだけで描くので、Built-in / URP どちらでも同じ見た目になります
- **入力**: マウスとキーは IMGUI のイベント、タッチは `Input.touches` から読み、`GameInput` にまとめています。棒の操作は指の位置を棒の高さの水平面に投影する直接操作です
- **画面**: 横長 / スマホ横向き / 縦向きの 3 つのレイアウトを自動で切り替えます。WebGL は画面いっぱいに表示する専用テンプレート (`Assets/WebGLTemplates/Wataame`) を使います
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
Publish/WebGL/        GitHub Pages に公開するローカルビルド
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
- `Build/WebGL (Publish to GitHub Pages)` — 公開用の WebGL ビルド (`Publish/WebGL` に出力。コミットすると Pages に公開される)
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
| Deploy to GitHub Pages | `main` へのプッシュ | WebGL 版を GitHub Pages に公開 (ライセンスが無ければ `Publish/WebGL` を公開) |
| GitHub Release | `v*` タグのプッシュ | Windows / WebGL の zip をリリースに添付 |

### Secrets なしで使う (ローカルビルドを公開)

Unity のライセンスを Secrets に登録しなくても、ローカルでビルドした WebGL 版を GitHub Pages に公開できます。

1. **GitHub Pages を有効化** — Settings → Pages → Build and deployment の Source を **GitHub Actions** にする (最初の 1 回だけ)
2. Unity のメニュー **Wataame → Build → WebGL (Publish to GitHub Pages)** でビルド (コマンドラインなら下記)
3. `Publish/WebGL` をコミットして `main` にプッシュ

```bash
Unity -batchmode -quit -projectPath . -executeMethod Wataame.Editor.BuildScript.BuildWebGLForPages
```

このとき Unity のテストとビルドのジョブは警告つきでスキップされ、Blender のモデル生成と Pages 公開だけが動きます。

### CI で Unity もビルドする場合

1. **Unity ライセンスを Secrets に登録** (Settings → Secrets and variables → Actions)
   - `UNITY_LICENSE` — Unity Personal のライセンスファイル (`.ulf`) の中身。Unity Hub でサインイン・ライセンス有効化をした PC の
     - Windows: `C:\ProgramData\Unity\Unity_lic.ulf`
     - macOS: `/Library/Application Support/Unity/Unity_lic.ulf`
     にあります。詳しくは [GameCI のアクティベーション手順](https://game.ci/docs/github/activation) を参照
   - `UNITY_EMAIL` / `UNITY_PASSWORD` — Unity アカウント
2. **GitHub Pages を有効化** (上と同じ)

ライセンスがあるときは CI でビルドした WebGL 版を公開します。

### リリース

```bash
git tag v1.0.0
git push origin v1.0.0
```

## ライセンス

フォントのライセンスは [THIRD_PARTY_NOTICES.md](THIRD_PARTY_NOTICES.md) を参照してください。
