# Unity 実装ガイド（設計方針）

## 1. 前提
- **Unity 6.3 LTS**。テンプレートは「Universal 2D」。
- 縦画面で、基準解像度は 390×844。
- 追加するパッケージ（`Packages/manifest.json`）：
  - `com.unity.nuget.newtonsoft-json`（JSON 読み込み）
  - `com.unity.test-framework`（テンプレートに入っていなければ追加）
  - `com.unity.2d.tilemap`（2D テンプレートに含まれる）

## 2. フォルダ構成（この形を守る）
```
unity/Assets/Imoni/
  Config/GameConfig.json         … 調整値(唯一の数値ソース)
  Fonts/*.ttf                    … 元フォント(OFL)。Generated/ に FontAsset を生成
  Art/Sprites/{UI,Icons,Game,Tiles}/  Art/Backgrounds/  Art/Logo/
  Audio/SFX/*.wav
  UI/Theme.uss                   … 共通スタイル
  Resources/UI/*.uxml            … 画面(Resources.Load で読む)
  Resources/UI/*.uss             … 画面ごとのスタイル(必要なら)
  Scripts/Core/                  … ★UnityEngine を参照しない純C#(ゲームのルール)
     Imoni.Core.asmdef (noEngineReferences: true)
  Scripts/Game/                  … MonoBehaviour(表示・入力・音・演出)
     Imoni.Game.asmdef (Imoni.Core を参照)
  Editor/                        … ImoniSetup.cs(セットアップメニュー), ImoniUISpritePostprocessor.cs
  Tests/EditMode/                … Unity Test Framework(Core のテストを流用可)
tools/CoreTests/                 … dotnet test 用のプロジェクト(Core のソースをリンクして Unity なしでテスト)
```

## 3. 層の分け方
```
[Scripts/Core]  ルールだけ。決定的(シード付き乱数)。UnityEngine 禁止。dotnet test で検証できる
   GameConfig(POCO) / Rng / QualityRoller / StreakCalc / Inventory(MaterialLot, Lock) / Market(売買)
   Upgrades / Objectives(生成・判定) / Scoring / RoundStateMachine / WorldGen(シード→タイル・資源)
   Pathfinding(A*) / ResourceNode(成長) / Bots(チームメイト) / IGameServer + LocalGameServer
        ↑ 操作コマンドを送る / 確定した状態とイベントを受け取る
[Scripts/Game]  Unity 側
   Bootstrap(起動・DI) / ScreenRouter(UXML差し替え) / Screens/*Screen.cs(各画面)
   WorldView(Tilemap・Sprite・キャラ移動の補間) / HudView / Panels(倉庫・ショップ・強化・メニュー)
   ImoniEffect(演出) / AudioService / SaveData(PlayerPrefs または JSON ファイル)
```
- **サーバーを正にする処理**（仕様 11.2）は、必ず `IGameServer` を通す。
  - 今は `LocalGameServer` が同じプロセス内で処理する。後でネットワーク版に差し替えられるよう、UI から直接倉庫などを書き換えない。
- **時間**：`LocalGameServer.Tick(dtSeconds)` で進める。開発用の倍速（`round.mockTimeScale`）は Unity 側で掛ける。

## 4. 画面（UI Toolkit）
- 起動シーンは1つ（`Boot.unity`）。中身は次のとおり。
  - `UIDocument`：PanelSettings は Scale With Screen Size、390×844、Match 0、Sort Order 10。
  - ワールド用の Camera（Orthographic）と Grid/Tilemap。
- `ScreenRouter.Show(name)`：`Resources.Load<VisualTreeAsset>("UI/" + name)` で読み、`rootVisualElement` の中身を入れ替える。各画面の C# クラスがイベントを結び付ける。
- **ワールドとUIの重ね方**：ゲーム画面の UXML ではマップ部分を空の要素にし、`picking-mode: Ignore` を付ける。
  - タップは、HUD 全体の `PointerDownEvent` で受けて座標を取る。
  - パネルやボタンの上でなければ、`Camera.ScreenToWorldPoint` でワールド座標に変換して資源を探す。
  - UI Toolkit の座標は左上が原点で、Unity の Screen 座標は左下が原点。`RuntimePanelUtils.ScreenToPanel` などで変換する。
- **Safe Area**：`Screen.safeArea` から余白を計算し、ルート要素の padding に入れる。

### CSS → USS の置き換え表
| モック（CSS） | USS / C# |
|---|---|
| `display:flex; gap:12px` | `flex-direction` ＋ 子の `margin`（USS に gap はない） |
| `display:grid; repeat(3,1fr)` | 横並びの行 ＋ 子に `flex-grow:1; flex-basis:0` |
| `border-radius:999px` | 高さの半分の値 |
| `box-shadow: 0 5px 0 色` | `border-bottom-width:5px; border-bottom-color` |
| `linear-gradient` | 画像。または C# で `Texture2D` を作って `style.backgroundImage` に入れる |
| `text-shadow` | USS の `text-shadow` が使える |
| `@keyframes`、アニメーション | 簡単なもの：USS の `transition`。複雑なもの：C# の `schedule.Execute(...).Every(16)` か `experimental.animation` |
| SVG アイコン | `Art/Sprites/Icons/icon_*.png`（白）を `-unity-background-image-tint-color` で着色 |
| 点線の枠 | USS は点線に非対応。画像にするか、実線で薄い色にする |

## 5. フォント
- `ImoniSetup` で、`FontAsset.CreateFontAsset(Font)` を使って各 TTF から**ダイナミックな** FontAsset を作る。
  - 保存先：`Fonts/Generated/<名前> SDF.asset`（Theme.uss のパスと一致させる）。
  - 日本語はダイナミックにしておけば、名前入力などで想定外の文字が来ても表示できる。
- 対応表：
  - Dela Gothic One：見出し・大ボタン・大きい数字
  - Zen Maru Gothic Bold：本文
  - Zen Maru Gothic Black：強調
  - Zen Maru Gothic Medium：細めの文字
- **ライセンス**：OFL。`Fonts/OFL.txt` をクレジット画面に表示できるようにする。

## 6. スプライト
- **9スライス**：`Art/Sprites/UI/` の画像は、`Editor/ImoniUISpritePostprocessor.cs` が取り込み時に境界を自動設定する。
  - USS で背景に使う場合は `-unity-slice-left` などを指定する。
  - 境界の値（3倍サイズの px）は Postprocessor の中の表を参照。
- **ゲーム用**（`Art/Sprites/Game/`）：3倍サイズで書き出してある。Pixels Per Unit は、タイル1マス＝32px（2倍書き出しなので画像上は 64px）に合わせて設定する。
- **アイコン**（`Art/Sprites/Icons/`）：24px のアイコンを4倍（96px）で、白で書き出してある。着色して使う。
- **アバター**：4枚のレイヤーを重ねる（UI_SPEC 9章）。

## 7. マップ生成（WorldGen・Core）
- **入力**：シード、ラウンド、チーム人数。**出力**：タイル種別の2次元配列、資源ノードの一覧、開始地点、ラウンドごとの解放範囲（同心円状に広げる）。
- **最低限の実装（Phase 1〜2）**
  - 64×64 マス、チャンクは 16×16。
  - バイオームはノイズの閾値で決める：平野・森林・河川（橋を必ず置く）・山岳・湿地。
  - 資源はバイオームの表（`GameConfig.materials[].biomes`）に従って置く。
- **保証**（仕様 3.3・9.2）
  - 開始地点から A* で到達できない資源は作らない（作ってしまったら置き直す）。
  - そのラウンドの必須素材は、解放範囲に必ず置く。
- **目標生成**（仕様 9.2）
  - 簡易シミュレーションで「チーム人数×平均採集量×5分」の上限を出し、その 40〜70% の範囲からランダムに選ぶ（仮）。
  - 必須とボーナスで同じ素材は選ばない。

## 8. 芋煮会演出
- `mock/Imoni.dc.html` の JS ロジック（`step()` と `renderVals()`）を、そのまま C# の `ImoniEffect` に移すのが一番早い。
- **作り方**：UI Toolkit の絶対配置の要素（食材・星・ライン・キラキラ）を毎フレーム更新する。
  - 虹色の背景は、縦グラデーションの `Texture2D` を実行時に作り、UV をずらして流す（RawImage 相当の背景を自前で描く。`generateVisualContent` でもよい）。
- **入力**：最終スコア、成否、不足素材名。どんな場合も10秒以内に結果ボタンを出す（`GameConfig.imoniEffect`）。
- **音**：UI_SPEC 12章の表に従う。

## 9. 保存
- `PlayerPrefs` か `Application.persistentDataPath` の JSON に次を保存する。
  - プロフィール（名前、二つ名パーツ、キャラの色）
  - 取得済みバッジと二つ名パーツ
  - 最高到達ラウンド
  - 音の設定
  - ランキング（ローカル版）
- 挑戦中の状態は保存しない（失敗したら全部リセットなので）。

## 10. テスト
- **Core**：`tools/CoreTests` で `dotnet test`（NUnit）。Unity の EditMode テストでも同じテストを動かせるよう、NUnit の書き方にそろえる。
- **必須テスト**（仕様 15 のうちオフラインで確かめられるもの）
  - 保護数量を下回る売却がエラーになる（15-5）
  - 同じシード・同じ操作なら品質と連続の結果が同じになる（15-7）
  - 2:00 にボーナス公開のイベントが出る（15-8）
  - 5:00 に採集を止めて判定する（15-9）
  - 必須素材が不足したら、R1 に戻って全て初期化される（15-10）
  - 5ラウンド連続成功で通常クリアになる（15-11）
  - 売却済み素材がスコアに入らない（15-12）
  - 品質換算数量・連続倍率・強化価格・ボーナス段階・スコア式が、GameConfig の値どおりに計算される
  - 演出のタイムラインが、どんな最終スコアでも10秒以内に終わる
