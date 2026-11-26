# AGENTS.md — みんなで芋煮会（Unity）実装指示書

あなた（コーディングエージェント）の仕事は、このリポジトリにあるモックと仕様をもとに、**Unity 6.3 LTS で遊べるゲームを作ること**。
見た目・画面・文言はすでに決まっている。**新しくデザインを考えず、モックを忠実に移す**こと。

## 0. 最初に読む順番
1. `docs/SPEC_v1.0.md` … ゲーム仕様（原文）
2. `docs/DECISIONS.md` … 仕様の仮の部分を決めた内容・数値・範囲
3. `docs/UI_SPEC.md` … 画面ごとの仕様と遷移。`design/screenshots/*.png` が正解の見た目
4. `docs/UNITY_GUIDE.md` … 設計方針・フォルダ構成・CSS→USS の置き換え
5. `Assets/Imoni/Config/GameConfig.json` … すべての数値
6. `docs/COMPONENTS.md` … 部品カタログ。**2画面以上で使う見た目は必ず部品にする**（UI はカスタムコントロール、ワールドはプレハブ）
7. `docs/IMONI_EFFECT_SPEC.md` … 芋煮会演出の詳細（M5 の前に必ず読む）
8. `mock/*.dc.html` … 動くモック。`<x-dc>` 内がレイアウト、`<script data-dc-script>` 内がロジック
   - `cd mock && python3 -m http.server 8000` で開ける（`mock/README.md`）
   - 細かい px や挙動は、ここに書かれている数値が正

## 1. 守るルール
- **Unity エディタを開けない前提で進める。**
  - `.meta` / `.unity` / `.prefab` / `.asset` / `ProjectSettings` を**手書きしない**。
  - これらが必要な作業は、すべて Editor スクリプト `Assets/Imoni/Editor/ImoniSetup.cs` の「**Imoni > Setup Project**」メニューに書く。人間がエディタでそのメニューを1回押せば揃うようにする。
  - 作るもの：Boot シーン、PanelSettings、FontAsset、Tilemap 用 Tile、AudioMixer など。冪等にして、何度実行しても壊れないようにする。
- **ゲームのルールは `Assets/Imoni/Scripts/Core/` に純C#で書く**（`UnityEngine` 禁止、`noEngineReferences: true`）。
  - `tools/CoreTests` の `dotnet test` で検証してから次へ進む。
  - Unity の C# バージョンに合わせて **C# 9 まで**の構文を使う（`LangVersion 9.0`）。
- **数値を直書きしない**。`GameConfig.json` から読む。足りない値は GameConfig に追加し、`DECISIONS.md` に1行書く。
- **UI は UI Toolkit（UXML/USS）だけ**。uGUI・TextMeshPro の Canvas は使わない。
- **部品を再利用する**：`docs/COMPONENTS.md` の部品を `[UxmlElement]` のカスタムコントロールとして作り、画面はそれを並べて組み立てる。ボタン・カード・二つ名・アバター・メンバー行・進捗バーなどを画面ごとにベタ書きしない。
  - ワールドの物（資源・キャラ・ポップアップ）は `ImoniSetup` がコードからプレハブを生成し、同じプレハブを使い回す。
  - 色・フォント・部品は `Assets/Imoni/UI/Theme.uss` の変数とクラスを使う。
- **文言**：画面の日本語はモックと一字一句同じにする（「JACKPOT」は使わない）。
- **乱数**：シード付き。同じシード・同じ操作列なら同じ結果になること。
- **数値型**：数量・資金・ポイントは `BigInteger`。表示は「万・億・兆」形式の `Format.Big()` を作って使う。
- **サーバーで確定する処理**は `IGameServer` を通す。今は `LocalGameServer` で、チームメイト2人はボット。オンライン化（仕様 Phase 4）は今回やらない。
- **わからないとき**：止まらずに一番自然な案で進める。その判断を `docs/QUESTIONS.md` に「質問・仮に決めた内容・該当ファイル」として追記する。
- **1マイルストーンごと**：コミットし、下の「完了条件」を満たしたことを報告する。
  - 報告に含めるもの：実行したテストのコマンドと結果、人間がエディタで確認すべき手順。

## 2. マイルストーン（この順番で。1つ終えたら報告して止まる）

### M0 土台
- `Packages/manifest.json` への追加内容を `docs/SETUP.md` に書く（newtonsoft-json, test-framework）。Unity プロジェクトそのものは人間が作る。
- `Assets/Imoni/Scripts/Core/Imoni.Core.asmdef`、`Scripts/Game/Imoni.Game.asmdef`、`Editor/Imoni.Editor.asmdef`、`Tests/EditMode/Imoni.Tests.asmdef` を作る。
- `tools/CoreTests/Imoni.Core.Tests.csproj` を使える状態にする（Core のソースをリンク済み）。
- `ImoniSetup.cs` に「Imoni > Setup Project」を作る。中身は次のとおり。
  - フォントから FontAsset を生成する（`Fonts/Generated/`、名前は Theme.uss と一致させる）
  - PanelSettings を作る（390×844 / Match 0）
  - `Boot.unity` を作る（UIDocument、Camera、Grid＋Tilemap、Bootstrap）
  - Build Settings に Boot シーンを登録する
  - 縦画面に固定する
- **完了条件**：`dotnet test tools/CoreTests` が通る（空のテスト1件で可）。人間向けのセットアップ手順が `docs/SETUP.md` にある。

### M1 ルール（Core）
- 作るもの
  - GameConfig の読み込み
  - Rng（シード付き）
  - 品質抽選（pTop の式。上限なし。解放済み品質だけ）
  - 連続倍率
  - MaterialLot と Inventory（ロットごとのポイント倍率、古い順に消費、保護数量）
  - 売却・購入
  - 強化（価格 150×1.5^Lv）
  - 目標の判定（品質換算数量）
  - ボーナス段階
  - スコア（0.5R＋0.5B×T、売却分は除く）
  - ラウンドの状態遷移（0:00 開始 / 2:00 ボーナス公開イベント / 5:00 停止・判定 / 成功で引き継ぎ / 失敗で全初期化 / 5連続でクリア）
  - 操作履歴（実行者・種類・数量・時刻）
- **完了条件**：`docs/UNITY_GUIDE.md` 10章の必須テストがすべて `dotnet test` で通る。

### M2 画面の枠組み＋アウトゲーム
- **最初に** `docs/COMPONENTS.md` 2章の UI 部品を作り、全部品を並べた `ComponentGallery` 画面（開発用）を用意する。
- 作るもの：Theme.uss の読み込み、`ScreenRouter`、Safe Area 対応。
- UXML と C# を作る画面：Main、Tutorial1〜3、Home、Lobby、Waiting、Loading、Member、Ranking。
- 動きとして作るもの
  - ローディングバー
  - チュートリアルの名前入力
  - ロビーのコード入力とエラー
  - メンバー待ちのボット参加
  - 読み込みのステップ表示
  - 会員証の表裏・二つ名の組み合わせ・キャラの色変え（保存する）
  - ランキングの切り替え（データはローカルのダミーで可）
- **完了条件**：
  - 画面の UXML が部品の組み合わせだけでできている（同じ見た目のベタ書きがない）。
  - `ComponentGallery` で全部品を確認できる。
  - 各画面が `design/screenshots/` と並べて見分けがつかない程度に一致する（人間が確認）。
  - モックと同じボタンで同じ画面へ進む。

### M3 ワールドと採集
- **Core**：
  - WorldGen（シード、64×64、バイオーム、橋、到達できることの保証、ラウンドごとの解放範囲）
  - A*
  - ResourceNode（在庫3回分、切り株、40秒で再生、段階成長）
  - Bots
- **Game**：
  - `ImoniSetup` に、プレハブ（ResourceNode / Character / GatherPopup / LockedAreaOverlay）と TileSet の生成を追加する（COMPONENTS 3章）。
  - Tilemap 描画、資源のスプライト、未開放エリアの覆い
  - タップ → 経路に沿って移動 → 採集バー → 獲得ポップアップ
  - 連続表示、チームメイト表示（切断中の半透明表示を含む）、トースト
  - HUD の表示更新（タイマー・資金・必須・ボーナス）
- **完了条件**：
  - Core のテストに WorldGen（同じシードなら同じマップ、全資源に到達できる）と A* を追加して通る。
  - 人間がエディタで「タップすると歩いて採集し、倉庫の数が増える」を確認できる。

### M4 パネル
- 倉庫（選択・保護の増減・割合売却・数量入力・即売却・まとめて売却・履歴）、ショップ、強化、メニュー（接続状態）。
- **完了条件**：
  - 操作がすべて `IGameServer` を通っている。
  - 保護数量を割る売却ができない。
  - 資金が足りない購入・強化のボタンは押せない。

### M5 ラウンドの流れと演出
- 5:00 のタイムアップ画面 → 芋煮会演出（成功／失敗）→ 結果 または ゲームオーバー → 次のラウンド または R1 から再挑戦。
- 5連続成功したら通常クリア画面（モックにないので、結果画面と同じスタイルで作る）。
- 演出は **`docs/IMONI_EFFECT_SPEC.md` に従う**（座標は画面基準に変換済み、光の表現は `Art/Sprites/Effect/` の画像を使う）。**どんな最終スコアでも10秒以内に結果ボタンが出る**。
- **完了条件**：
  - 演出の時間を Core のテストで検証する（最終スコア 1 / 18,420 / 10^15 で10秒以内）。
  - 失敗版で「不足した素材名」が出る。
  - Unity の録画を `design/videos/imoni_success.mp4` と `imoni_fail.mp4` に並べ、IMONI_EFFECT_SPEC 8章の項目が一致する（人間が確認）。

### M6 音
- `AudioService` を作り、AudioMixer（Master に軽いコンプレッサー）を通す。
- UI_SPEC 12章の表どおりに鳴らす（ドローンのループと音程、ビート、食材ごとに上がる音程、レベルアップ、成功・失敗）。
- 音のON/OFFを保存する。

### M7 仕上げ
- 二つ名パーツとバッジの獲得条件（仮でよい。QUESTIONS.md に書く）。
- ローカルのランキング保存。
- 設定（音量）。
- 再接続中の表示（ダミー）。
- 未作成画面の補完（UI_SPEC 15章）。

## 3. 完成の定義（今回の範囲）
仕様 15章の受け入れテストのうち、オンラインに関係しない **3, 5, 7, 8, 9, 10, 11, 12** を、ボットのチームメイトでローカルに満たすこと。
1・2・4・6（複数人・別チーム・同期・同時売却）は `IGameServer` の差し替えで対応できる構造になっていればよい（Phase 4）。

## 4. 素材（すでにあるもの。作り直さない）
| 種類 | 場所 |
|---|---|
| フォント | `Assets/Imoni/Fonts/`（Dela Gothic One、Zen Maru Gothic Medium/Bold/Black、OFL） |
| UI の9スライス | `Assets/Imoni/Art/Sprites/UI/`（取り込み設定は `Editor/ImoniUISpritePostprocessor.cs`） |
| アイコン | `Assets/Imoni/Art/Sprites/Icons/`（白。PNG と SVG） |
| ゲーム画像 | `Assets/Imoni/Art/Sprites/Game/`（資源・食材・鍋・キャラ・マスコット・アバターのレイヤー・演出） |
| タイル | `Assets/Imoni/Art/Sprites/Tiles/` |
| 背景柄 | `Assets/Imoni/Art/Backgrounds/`（里芋柄。つなぎ目なく繰り返せる） |
| ロゴ | `Assets/Imoni/Art/Logo/title_logo@3x.png` |
| 演出用の光の画像 | `Assets/Imoni/Art/Sprites/Effect/`（虹グラデーション、放射光、光の柱、虹のにじみ、光彩付きの星・食材、光の輪、汁面の光） |
| 演出の参考動画 | `design/videos/imoni_success.mp4`, `imoni_fail.mp4` |
| 効果音 | `Assets/Imoni/Audio/SFX/`（WAV 44.1kHz・ステレオ） |
| スタイル | `Assets/Imoni/UI/Theme.uss` |
| 数値 | `Assets/Imoni/Config/GameConfig.json` |

## 5. よくある失敗（前回うまくいかなかった点の対策）
- **モックの `.dc.html` を Unity に取り込もうとしない**。あれは見本で、UI Toolkit で作り直す。
- **シーンや Prefab の YAML を手書きしない**。必ず `ImoniSetup` から生成する。
- **画面ごとに同じ見た目をコピペしない**。COMPONENTS.md の部品を使う。
- **ルールを MonoBehaviour の中に書かない**。Core に書いてテストする。
- **USS に gap・grid・box-shadow・グラデーションはない**。`docs/UNITY_GUIDE.md` 4章の置き換え表を使う。
- **タップがUIに吸われてマップに届かない**：マップ部分の要素は `picking-mode="Ignore"` にする。
- **大きい数値が float で崩れる**：BigInteger と固定小数点を使う。
- **芋煮会演出が不格好になる**：モックの座標は20px外側に広げた枠が基準。CSS の光彩・グラデーション・ぼかしは UI Toolkit で再現できないので、`docs/IMONI_EFFECT_SPEC.md` の画面座標の式と `Sprites/Effect/` の画像を使う。

## このリポジトリの作業先と現在の区切り

正本は `D:/UnityProjects/Imoni-kai`。ここがUnityプロジェクトのルートで、`unity/` の入れ子は作らない。旧handoffは資料の受け取り元のみ。既存プロトタイプは比較用に残し、新実装は `Assets/Imoni/` に集約する。仕様書の `unity/Assets/` はこのリポジトリの `Assets/` と読み替える。

2026-11-26：M3bの5153ecbからrebuild/m4-panelsを作成。M4の倉庫・ショップ・強化・メニュー、成長札の距離制限、最高品質のみの連続表示を実装済み。全操作をIGameServer経由とし、dotnet test 274件成功（失敗0）、パネル統合18項目成功、Unityエラー警告0を確認。390×844比較画像4枚、保護と資金不足の連番11枚をdocs/validation/m4に保存した。このM4をコミット・pushして停止する。M5の終了演出には進まない。地形境界とR1目標上限の調整はQUESTIONSにメモのみ残した。操作・検証手順はdocs/M4_PANELS.md。

USS非対応の構造擬似クラス（`:first-child`、`:last-child`、`:nth-child`など）は使わない。`Theme.uss` の先頭タブ用クラスは `.tabbar__item--first`。M2のTabBarは先頭のタブにこのクラスを付ける。
