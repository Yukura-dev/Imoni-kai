# 質問・仮決定ログ

実装中に仕様が曖昧だった点を、ここに追記していく（エージェント・人間どちらも）。
形式: `- [日付] 質問 / 仮に決めた内容 / 該当ファイル`

- [2026-11-25] 成功ランク(S/A/B/C)の基準が仕様にない / ボーナス達成率で S:300% A:150% B:100% C:それ以外 とした / GameConfig.json rankGrades
- [2026-11-25] ショップの購入価格が仕様にない / 売却単価×3、購入品は普通品質とした / GameConfig.json economy.buyPriceMultiplier
- [2026-11-25] 二つ名パーツ・バッジの獲得条件が未定 / M7 で仮の条件を決めてここに書く / —

## 2026-11-25 — 芋煮会演出の修正

- 質問：前回M0を作ったhandoffと、現在表示されているゲームのどちらに反映するか。
  - 判断：見た目が崩れている実装は `D:/UnityProjects/Imoni-kai` にあるため、こちらの演出を修正する。前回の独立handoffフォルダにはゲーム本体を移さない。ユーザーにも両者が別であったことを説明した。
  - 該当：`Assets/Imoni/`、`Assets/Scripts/UI/RoundResultController.cs`。配布仕様の `unity/Assets/` は、この既存Unityプロジェクトでは `Assets/` に対応する。
- 質問：既存ゲームはuGUIだが、新仕様はUI Toolkit専用である。
  - 判断：今回作り直す芋煮会演出はUI Toolkitとする。採取、移動、カメラ、手のモーション、確定済み結果画面は既存処理に接続し、ゲーム全体の置換は行わない。
  - 該当：`Assets/Imoni/UI/ImoniEffect.uxml`、`Assets/Imoni/Scripts/Game/ImoniEffectView.cs`、`RoundResultController.cs`。
- 質問：失敗スナップショットには「成功した場合の最終スコア」が保存されていない。
  - 判断：演出の閾値用の仮基準を、実際の失敗スコア÷0.4（切り上げ、最低1）として渡す。確定スコア・報酬は変更しない。0.4は仕様3章の約40%に従い `effectReview.failureScoreRatio` に置く。録画用の固定入力は成功18,420／失敗7,350、共通基準18,420。実ゲームの不足素材名はスナップショットから取得する。
  - 該当：`RoundResultController.Replay()`、`GameConfig.json`。
- 質問：参照動画のランダム配置・フレームごとの発生時刻を完全再現できるか。
  - 判断：原本の食材生成には非シードMath.randomが使われているため、指定の式・速度・段階を守り、シード固定で再現可能にする。録画は共通seed40410（背景配置seed7）で比較する。原本の冒頭空白は成功0.76秒／失敗0.24秒だけ除去し、演出開始を合わせる。比較動画でそれ以外の時間変更や補正は行わない。
  - 該当：`ImoniEffectTimeline.cs`、`GameConfig.json`、`tools/EffectReview/compare_videos.py`。
- 質問：失敗動画の完全なグレースケールと、仕様6章の半透明の覆いは同じ色になるか。
  - 判断：新しい実装仕様を優先して指定された茶灰色と黒の2枚を使う。動画のCSSフィルターとは残る色に差があることを比較報告に明記する。
  - 該当：`ImoniEffect.uxml`、`ImoniEffect.uss`。


## 2026-11-25 — 作り直しのM0統合（ユーザー承認済み）

- 質問：既存Unityと独立handoffのどちらを正本にするか。
  - 決定：ユーザーの「全部作り直していい」「はい」を受け、今開いている `D:/UnityProjects/Imoni-kai` に統一する。別のUnityプロジェクトは作らない。旧handoffは仕様・素材・M0コードの受け取り元とする。
  - 該当：`AGENTS.md`、`README.md`、`docs/SETUP.md`。
- 質問：作り直しで採取の操作感や既存Prefabをどう扱うか。
  - 仮決定：旧シーン・Prefab・移動／採取コードを比較用に保存し、M3でタップ地点への移動、カメラ追従、歩行の上下動、丸い手、資源の近くで停止して中心へ手を伸ばす動作、人同士の重なり許可を引き継ぐ。新UIは新しいAGENTSに従いUI Toolkitの共通部品とする。
  - 該当：`Assets/Scenes/`、`Assets/Prefabs/`、`Assets/Scripts/`、今後の `Assets/Imoni/Scripts/Game/`。
- 質問：以前の演出修正を先に完成させるか。
  - 決定：今回の区切りはM0のみ。前節の「ゲーム全体の置換は行わない」は今回の再構築承認で更新された。既存演出は保管し、仕様8章の録画比較はM5で行う。M0完了を演出の完成とは扱わない。
  - 該当：`docs/IMONI_EFFECT_REVIEW.md`、`Assets/Editor/ImoniLegacyEffectSetup.cs`。
- 質問：Setupメニューの既存演出用処理と新しいBoot生成をどう分けるか。
  - 仮決定：`Imoni > Setup Project` はM0のBoot生成とし、旧演出の生成は `Imoni > Legacy > Setup Effect` に残す。既存uGUI参照があるEditor処理は `Assets/Editor/`、新しい土台は `Assets/Imoni/Editor/` に置く。
  - 該当：`ImoniSetup.cs`、`ImoniLegacyEffectSetup.cs`、`Imoni.Editor.asmdef`。


## 2026-11-25 — M1 Core

- 質問：端数のある連続倍率と巨大数量をどう確定するか。
  - 仮決定：`numeric.fixedScale=1000000`。連続倍率は `floor(scale × base^(n/divisor))` をBigIntegerの整数n乗根で求め、獲得数量は採集量倍率と連続倍率を両方掛けた後に切り捨てる。ロットのポイント倍率を固定し、スコアはR/Bを整数比の係数で合算した最後に一度だけ切り捨てる。
  - 該当：`GameMath.cs`、`StreakCalc.cs`、`Inventory.cs`、`Scoring.cs`、`GameConfig.json`。
- 質問：強化価格の端数はどう丸めるか。
  - 仮決定：モックの `Math.round` に合わせ、正数の四捨五入。Lv2=338、Lv3=506。価格は元の基本価格と成長率から毎回求め、前の丸めた価格を掛け直さない。
  - 該当：`UpgradeRules.cs`。
- 質問：ボーナスの資金報酬は別式とあるが係数が未定。
  - 仮決定：成功時に `floor(ボーナス素材ポイント × 段階倍率 × economy.bonusFundsPerPoint)` を加算し、係数は0.1。失敗時の資金報酬は0。購入品は `economy.purchasedQualityId=normal`、ポイント倍率1。購入価格は数量×基本売価×品質倍率×buyPriceMultiplierを掛けて切り捨てる。
  - 該当：`GameConfig.json`、`Scoring.cs`、`LocalGameServer.cs`。
- 質問：M1でワールド未実装の目標と採集をどう扱うか。
  - 仮決定：目標は `RoundObjectives` を外部から受け取り、省略時は `round1Mock` を使う。到達可能性を考慮した目標生成・移動・資源在庫・ボットはM3。M1の `Gather` は採集完了を確定するCoreの入口であり、M3でワールドの採集検証を接続する。
  - 該当：`Objectives.cs`、`LocalGameServer.cs`。M2の画面はまだ作らない。
- 質問：失敗直後の初期化と結果表示を両立するには。
  - 仮決定：即座にR1・初期資金・強化0・連続0・在庫/保護/累積スコア0へ初期化し、状態をFailedにして入力を止める。確定結果と履歴のコピーをLastResultへ保持し、診断イベントはRetryまで保持。`Retry(newSeed)` で新挑戦を明示開始する。成功後も `StartNextRound` まで入力を止め、資金・強化・seedを引き継ぐ。
  - 該当：`LocalGameServer.cs`。
- 質問：非常に高い採集速度で固定小数の時間が0になる。
  - 仮決定：時間の最小表現単位1/scale秒を下限とする。強化レベル自体にゲーム上の上限は追加しない。
  - 該当：`UpgradeRules.CollectDurationRaw`、`RulesMathTests.UnlimitedSpeedRetainsAPositiveRepresentableDuration`。
- 質問：Theme.ussの先頭タブ警告と今後の部品の指定。
  - 決定：ユーザーの修正内容を反映して `.tabbar__item--first` を採用。非対応の構造擬似クラスは使用しない。M2のTabBarが先頭タブに明示クラスを付ける。
  - 該当：`Theme.uss`、`AGENTS.md`、`docs/COMPONENTS.md`。


## M3で対応するメモ（ユーザー指示、2026-11-25）

- R2以降の目標がR1の固定目標（round1Mock）のままなので、M3でワールドからの目標生成につなぐ。M2aでは変更しない。
- 品質抽選にバイオーム・資源ごとの補正（仕様5.2）を追加する。M2aでは変更しない。
- 採集運の係数がGameConfigのqualityRoll.luckPerLevelとupgradesのluck.effectPerLevelに重複しているので、M3で1か所にまとめる。M2aでは変更しない。

## M2aの表示上の判断（2026-11-25）

- 質問：モックのMemberRowの単色の二つ名・円形プレースホルダーとCOMPONENTSの指定が異なる。
  - 仮決定：指定どおりTitleChipsと4レイヤーAvatarを再利用し、画面ごとの描き直しを避ける。対象：ProfileControls.cs、docs/M2A_COMPONENTS.md。
- 質問：CSSの999px角丸・line-heightのままではUnityの形と行間が一致しない。
  - 仮決定：pillは高さの半分相当の半径、ResultBannerはCSSのline-heightに相当する高さの単行Labelを2段にする。対象：Data.uss、Profile.uss、EffectsControls.cs、Effects.uss。

## M2bの仮決定（2026-11-25）

- 質問：ワールド未実装のLoadingをどう進めるか。
  - 仮決定：モック相当の3秒で進む決定的な表示用進捗とし、Gameは空画面にする。M3で実際のワールド生成・同期進捗へ接続する。対象：LobbyPresentation.cs、LobbyScreens.cs、Game.uxml。
- 質問：ランキングの複合単位（12億3,456万）とM1承認済みFormat.Big（12.34億）の表示が異なる。
  - 仮決定：許可されたローカルダミーに数値scoreとモックのdisplayScoreを併記して表示する。M1の書式・ルールは変更しない。対象：GameConfig.outGame.ranking、ProfileScreens.cs。
- 質問：設定画面とOS共有のモックが未定。
  - 仮決定：Homeの設定は既存CardとImoniButtonで音ON/OFF・閉じるを表示して保存する。共有は招待文のクリップボードコピーまでにし外部送信しない。対象：ProfileScreens.cs、LobbyScreens.cs。
- 質問：チュートリアル完了後の再起動経路。
  - 仮決定：今回指定されたUI_SPEC0章の経路を優先し、Main→Tutorial1〜3→Homeを毎回たどれる。名前・二つ名・色・音の保存値は引き継ぐ。対象：OnboardingScreens.cs、ScreenContext.cs。
- 質問：Lobbyを離れてHomeから入り直したときの参加状態。
  - 仮決定：戻る操作で退出し、再入場はチーム選択から始める。モックの初期modeと合わせ、作成経路へ戻れるようにする。対象：LobbyScreens.cs、ImoniM2BValidation.cs。
- 質問：M2aのAvatar表示とモックの塗り円をM2bでどう合わせるか。
  - 仮決定：同じAvatar部品を継続利用し、Lobby／Waiting／Loadingではレイヤーを隠して枠と単色だけ表示する。Home／Memberのキャラ編集は4レイヤー表示を維持する。対象：LobbyScreens.uss、LobbyScreenData.SetAvatar。
