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


## M3で対応するメモ（2026-11-25、M3aで対応）

- 対応済み：R1を含め、目標省略時は毎ラウンドWorldからObjectiveGeneratorで生成する。round1Mockはモック表示・明示的なルールテストfixtureにだけ残す。
- 対応済み：QualityRollerにバイオームと資源の品質補正を追加し、LocalGameServerの採集確定時に適用する（仕様5.2）。
- 対応済み：採集運の係数はupgrades.itemsのluck.effectPerLevelに集約。qualityRoll.luckPerLevelは削除し、GameConfig.LuckPerLevelも同じ定義を読む。

## M2aの表示上の判断（2026-11-25）

- 質問：モックのMemberRowの単色の二つ名・円形プレースホルダーとCOMPONENTSの指定が異なる。
  - 仮決定：指定どおりTitleChipsと4レイヤーAvatarを再利用し、画面ごとの描き直しを避ける。対象：ProfileControls.cs、docs/M2A_COMPONENTS.md。
- 質問：CSSの999px角丸・line-heightのままではUnityの形と行間が一致しない。
  - 仮決定：pillは高さの半分相当の半径、ResultBannerはCSSのline-heightに相当する高さの単行Labelを2段にする。対象：Data.uss、Profile.uss、EffectsControls.cs、Effects.uss。

## M2bの仮決定（2026-11-25）

- 質問：ワールド未実装のLoadingをどう進めるか。
  - 仮決定：モック相当の3秒で進む決定的な表示用進捗とし、Gameは空画面にする。M3で実際のワールド生成・同期進捗へ接続する。対象：LobbyPresentation.cs、LobbyScreens.cs、Game.uxml。
  - M3b更新：Loadingに入る時点でCoreワールドを同期生成し、資源数には生成結果の実数を表示する。ローカルボット構成のため、進捗バー・同期ステップは既存の3秒演出を維持する。Gameの空画面は実ワールド表示へ置き換えた。
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


## M3aの仮決定（2026-11-25）

- 質問：mainへマージ済みとの依頼と、取得したGit履歴が一致しない。
  - 判断：origin/mainとローカルmainは053c6e6のままで、レビュー済みM2bはdacbdc9だった。mainがM2bの祖先であることを確認し、ローカルmainだけをM2bまでfast-forwardしてからrebuild/m3a-world-coreを作成した。リモートmainは変更しない。
  - 該当：Gitブランチ履歴。本タスクの差分の基点はdacbdc9。
- 質問：水上の魚と、資源へ重ならずに到達する条件をどう扱うか。
  - 仮決定：資源タイルの上下左右いずれかの歩行可能タイルまでA*で到達すれば採集可能。水上へ移動せず、魚は岸または橋から採る。資源自体も解放範囲内であることを要求する。人同士は移動の障害物にしない。
  - 該当：WorldMap.cs、Pathfinding.cs、LocalGameServer.cs。
- 質問：旧M1の即時Gatherコマンドを残すか。
  - 仮決定：本番APIは移動・採集時間・在庫を検証し、時間進行による完了時だけ確定する。M1テストも資源選択とTickを経て採集する。ルール単独テストは明示目標とボット無効をテスト側から渡し、本番の抜け道は作らない。
  - 該当：LocalGameServer.cs、LocalGameServerTests.cs、GatheringSimulationTests.cs。
- 質問：M2bのLoadingとワールド表示をこの段階で接続するか。
  - 判断：今回の指定はCoreのみ。M3bでLoadingとGameへ接続する。画面、Tilemap、タップ操作、カメラ・手の演出は変更しない。
  - 該当：docs/M3A_WORLD_CORE.md。

- 質問：仕様9.2の簡易シミュレーションで、複数素材に同じ5分を使ってよいか。
  - 仮決定：ラウンド時間を必須とボーナスで等分し、現在位置からの帰還と各採集の往復も負担する。実数量の保守的な上限を使い、品質・連続の上振れは数えない。人数分の作業と資源在庫を共有シミュレーションする。目標種類不足や時間不足は候補を除外して再抽選し、固定の必須数を満たせなければエラーにする。
  - 該当：ObjectiveGenerator.cs、ObjectiveGenerationConfig.cs、GatheringTiming.cs。
- 質問：品質補正の具体的な式・成長段階の時間・ボット間隔が未定。
  - 仮決定：品質順位rの抽選重みに総補正係数のr乗を掛けて正規化。成長は40秒の25/50/75/100%で区切り、ボットは2秒ごとに最寄りを探す。採用値はDECISIONSのM3a節とGameConfigへ集約した。
  - 該当：QualityRoller.cs、ResourceNode.cs、Bots.cs、GameConfig.json。
- 質問：ラウンド間の待ち時間と同時完了をどう扱うか。
  - 仮決定：Collecting中だけ成長を進め、成功画面では停止して次ラウンドへ引き継ぐ。同時完了はメンバー順、5:00の期限が採集完了より優先。移動/速度の強化は次のタイル/採集作業から、数量/運は採集確定時に適用する。
  - 該当：LocalGameServer.cs、GatheringSimulationTests.cs。

## M3aバランス調整の判断（2026-11-25）

- 質問：「R1で必須になりうる素材（薪・里芋・ねぎ）」と既存の全素材抽選をどう合わせるか。
  - 仮決定：R1だけ必須候補を設定の3種類へ限定し、ボーナスは重複しない入手可能素材から選ぶ。数量は固定目標に戻さず、従来の採集シミュレーションから毎回生成する。各素材6か所保証は、この設定の候補すべてに適用する。
  - 該当：ObjectiveGenerationConfig.cs、ObjectiveGenerator.cs、WorldGen.cs、GameConfig.json。
- 質問：バイオーム内抽選と全体の出現重みのどちらを優先するか。
  - 仮決定：マップ全体の素材数を重みで配分する。バイオームごとの独立抽選だと、専用の地形が広い素材が過剰になるため。地形は滑らかなノイズの順位から割合を割り当て、R1平野と最低供給を優先した補修を認める。
  - 該当：WorldGen.cs、WorldConfig.cs、docs/validation/m3a/。

## M3bの判断（2026-11-25）

- 質問：M3aには資源選択しかなく、以前の地面タップ移動をどう維持するか。
  - 仮決定：`IGameServer.CanMoveTo/TryMoveTo`を追加し、同じA*とTickで地面へ移動、到着後はIdleにする。人同士は重なってよい。カメラは自分の補間位置に追従し、HUDと下ボタンを除くマップ領域の中央に置く。
  - 該当：LocalGameServer.cs、MoveCommandTests.cs、GameWorldView.cs。
- 質問：専用の魚・山菜・山岳・湿地の画像が提供されていない。
  - 仮決定：既存素材を再利用する。魚は水面と「魚」の表示、山菜は里芋画像の濃緑色、山岳・湿地は既存タイルの色違いとする。専用画像が追加されたらSetupの参照対応だけで交換できる。
  - 該当：ImoniWorldSetup.cs、WorldRuntimeAssets.cs、WorldTileSet.cs。
- 質問：モックの固定地形・固定数量とシード生成後のGameをどのように比較するか。
  - 仮決定：HUD寸法、フォント、色、部品、マップの画素密度と採集状態の見せ方を比較する。地形、資源座標、目標素材・数量、採集結果は実際のCoreを表示するためモックの固定値と異なる。比較画像のためにゲーム状態を偽装しない。
  - 該当：docs/M3B_WORLD_VIEW.md、docs/validation/m3b/。
- 質問：今回作らないパネルと終了演出への入口をどう扱うか。
  - 仮決定：下の4ボタンは既存Actionボタンで表示のみ。ラウンド終了時はCoreで入力・時間を停止して0:00を表示し、Homeへ戻さない。パネルはM4、結果・次ラウンドへの遷移はM5で接続する。
  - 該当：Game.uxml、GameScreens.cs、GameHudProjection.cs。
- 質問：再生中の資源が密集し、成長ラベルがキャラクターを隠す。
  - 仮決定：キャラクター本体・名札・採集対象とバーを避けて近傍に配置し、最後にタップした資源と近い資源を優先する。ラベル同士も重ねず、置き場所がない場合はそのラベルだけ非表示にする。
  - 該当：GameWorldView.cs、ResourceNodeView.cs。

## M4での判断・後続作業メモ（2026-11-26）

- 質問：遠くの成長札も表示され、名札やポップアップと重なる。
  - 判断：自分から2タイル以内、または選択した資源だけに札を表示する。距離は `ui.worldView.growthLabelDistanceTiles`。遠方の再生中資源には既存の芽画像を使う。
  - 該当：GameWorldView.cs、ResourceNodeView.cs、GameConfig.json。
- 質問：ショップの「素材ごと」は全資源6種か。
  - 仮決定：UI_SPEC 11章とモックの4種（薪・里芋・ねぎ・キノコ）を扱う。対象は `ui.panels.shopMaterials` で変更できる。倉庫は全素材・品質に対応する。
- 後続メモ（今は対応しない）：森林・未開放エリアの境目が四角いタイルの階段状に見える。M7でオートタイル等による滑らかな境界を検討する。
- 後続メモ（今は対応しない）：R1の目標が2:50経過時点で倍近く達成でき、簡単すぎる可能性がある。プレイテストで目標上限の比率を調整する。
- 質問：数量入力が売却可能数を超える場合、モックのように内部で上限へ丸めるか。
  - 判断：入力した数量を維持して売却を無効にする。今回の「保護を超える売却ができない」を明確にし、意図しない数量の即売却を避ける。割合ボタンは保護を除く数量から計算する。サーバーも過大な売却を拒否する。
  - 該当：GamePanelProjection.SelectSale、GamePanels.SellSelected、GamePanelTests。

## M5 ラウンドの流れと演出の判断（2026-11-26）

- 質問：実プレイの失敗に対する「成功時の最終スコア」はどう求めるか。
  - 仮決定：不足する必須素材の品質換算数量だけを普通品質・連続倍率なしで補った場合のスコアを使う。取得済みの必須／ボーナスポイントとボーナス段階は保持し、実スコア以上・最低1とする。演出の閾値だけに使い、表示スコアや報酬は変えない。前の「芋煮会演出の修正」節にある旧プロトタイプの失敗スコア÷0.4という仮定は、M5のBoot経路ではこの判断に置き換える。
  - 該当：ImoniEffectResultProjection.cs、ImoniEffectResultTests.cs、docs/M5_ROUND_FLOW.md。
- 質問：二つ名の全条件がM7予定だが、M5の結果報酬欄はどう動かすか。
  - 仮決定：初成功で後半「鍋奉行」、5連続成功で前半「伝説の」を付与する。`titles.unlockRules` に条件を集約し、既存の初期所持を保ってプロフィールへ追加保存する。モックの初期所持「連続の」はそのまま残す。全二つ名・バッジの条件整理はM7。
  - 該当：GameConfig.json、RoundResultRules.cs、RoundResultProjection.cs、ProfileData.cs、ProfileScreens.cs。
- 質問：モックにない5連続成功の通常クリア画面をどう作るか。
  - 仮決定：ResultScreenと同じ部品・スコア内訳を使い、通常クリアの見出しとHomeボタンに切り替える。通常モードにR6は作らない。
  - 該当：RoundScreens.cs、ScreenCatalog.cs、RoundResultProjection.cs。
- 質問：5分の動作確認を短縮するため、ゲームの制限時間を変えるか。
  - 仮決定：制限時間は既存の300秒を維持する。Editor／Development Buildのみ、既定OFFの切り替えで `round.mockTimeScale=10` をCoreの時間進行へ適用する。通常ビルドは常に1倍、芋煮会演出も実時間のまま。M3bの「旧mockTimeScaleは適用しない」は通常プレイについて維持し、開発用の明示切り替えだけ追加する。
  - 該当：DevelopmentRoundClock.cs、ImoniDevelopmentMenus.cs、Bootstrap.cs。
- 質問：演出の再生中に同時に必要になる部品数をどう決めるか。
  - 仮決定：最短の発生間隔・ジッター下限・二重発生、最大落下時間、星の寿命と段階数から上限を計算し、表示前に部品をプールする。リプレイとフレーム飛びでも再利用する。
  - 該当：ImoniEffectPoolCapacity.cs、ImoniEffectView.cs、ImoniEffectParts.cs、ImoniEffectTests.cs。
- 質問：指定撮影時刻と静止見本・参考動画の段階が一致しない場合はどれを基準にするか。
  - 仮決定：指定された演出開始からの時刻とIMONI_EFFECT_SPECの式を優先する。参考動画には成功0.76秒・失敗0.24秒の導入画面があり、静止41は演出4.6秒の状態ではない。元動画の並列比較を残したうえで、導入を除いた開始合わせ比較も保存する。乱数の違いを隠すためにタイムラインやスクリーンショットの時刻を変更しない。
  - 該当：docs/validation/m5/effect-comparison.md、tools/Compare-M5.ps1。

## M6 目標インフレ・検証の判断（2026-11-26）

- 質問：最高品質と連続数量を無視したM3の供給予測では、R5の要求がR1より小さく、数十万の採集量に見合わない。
  - 仮決定：素材別基準量×ラウンド成長×最高品質補正×チーム強化補正で要求を増やす。基準は薪240・里芋160・ねぎ160・キノコ80・魚60・山菜40の90〜110%を抽選し、成長率1.35、最高品質補正の重み0.35とする。1.35は抽選幅1.1/0.9より大きく、同素材の基準量の振れだけで次Rの要求が逆転しない。最高品質の全倍率をそのまま平均採集量とは見なさず、実際の品質分布による供給予測で上限を確認する。
  - 該当：ObjectiveGenerationConfig.cs、ObjectiveGenerator.cs、GameConfig.objectiveGeneration。
- 質問：「チームの強化度合い」はLvの合計だけで十分か。
  - 仮決定：チーム平均Lvの重み（移動0.1・速度0.15・量0.35・運0.12）に加え、同じ位置・在庫・ラウンドでの「実強化の期待供給÷無強化の期待供給」を掛ける（upgradeSupplyWeight=1）。これにより品質抽選と連続の非線形な増加も反映する。生成時の目標を、そのラウンド途中の強化で上げ下げしない。
  - 該当：ObjectiveGenerator.TargetAmount、GeneratePlan、GatherYieldExpectation.cs。
- 質問：期待品質と連続を含めても「達成可能」と言えるか。
  - 仮決定：5分を素材ごとの重複しない作業時間に分け、A*往復移動・1訪問最大3採集・強化速度と量・共有在庫・再生時間を守る作業予定を構築する。実QualityRoller分布と有限回数の連続状態の期待値を加え、その品質換算供給の85%を上限にする。3回まとめて採るのは在庫3回分の自然な操作であり、以前の1採集ごとの拠点往復を過剰に見積もらないため。保証は想定能力と適切な操作での期待供給に収まることで、すべての品質抽選列・放置・無強化での成功保証ではない。
  - 該当：ObjectiveGenerationPlan.Visits、ObjectiveInflationTests、docs/validation/m6/balance.md。各素材の段階開始時の連続分布は0から計算し、前の素材から続く連続の上振れを保証に使わない。
- 質問：素材抽選や資源上限がある中で単調増加をどう検証するか。
  - 仮決定：同素材・同シード・同じ位置／資源状態で、R1〜R5の上限適用後の目標も厳密増加を確認する。強化は非減少、全4強化では全目標の厳密増加を確認する。移動／速度単独の強化が在庫・再生の上限に達する場合だけ同値を許容する。物理的な供給を超えて増やすことはしない。
  - 該当：ObjectiveInflationTests.FinalGeneratedTargetsAlsoIncreaseForEachRepeatedMaterialAfterCapacityClamp、RaisingUpgradesNeverReducesFinalTargetsAndReadsActualSupply。
- 質問：自動操作の強化方針で結果が大きく違う。
  - 仮決定：無強化・計画的強化・予算内無制限投資の3試行を保存する。強化はplayer1のみ、各R開始直後の所持金50%以内、低Lvからamount→luck→speed→move順。計画的試行は各項目+1Lv/Rまで。これはQA専用のbalanceReview設定で、ゲームの強化上限ではない。到達時刻は確定OperationRecordから算出する。
  - 結果と限界：シード202647では無強化がR4で失敗、計画的強化のR5必須達成は約1:55、予算内無制限投資のR5は約12秒。開始後の大量強化を生成済み目標が予見できない問題は残す。高速クリアを隠すための試行除外、採集／連続／資金報酬の変更や強化上限は追加しない。
  - 該当：tools/BalanceReview、GameConfig.balanceReview、docs/validation/m6/balance.json、balance.md。

## M6 整数表示・失敗演出の判断（2026-11-26）

- 質問：整数の「万・億・兆」表示で、最小桁や0のグループをどう扱うか。
  - 判断：1万未満は3桁区切りの整数。1万以上は最高単位から最大2つの隣接単位を表示し、0のグループと万未満の端数は出さない。例は `12,345 → 1万`、`88,701,234 → 8,870万`、`1,234,567,890 → 12億3,456万`、`3,200,012,345,678 → 3兆2,000億`。数量・資金・ポイント・品質倍率・ランキングの表示を共通化する。入力欄と検証用JSONには正確な整数を保持する。
  - 該当：Format.cs、DisplayProjection.cs、RoundResultProjection.cs、GameHudProjection.cs、ProfileScreens.cs、IntegerDisplayTests.cs。
- 質問：スコア内訳を整数表示すると、表示した小計の和と確定スコアが一致しない場合がある。
  - 判断：内部の固定小数精度と最後に一度だけ切り捨てる計算を保持し、各表示項目だけ端数を切り捨てる。内訳に「表示は端数切り捨て」と記載する。強化効果や連続倍率の小数は数量ではないため維持する。達成率1000%以上は整数の「目標の○倍」に切り替える。
  - 該当：RoundResultProjection.cs、RoundFlowParts.cs、DisplayProjection.cs。
- 質問：2単位の長い数値が、従来の固定幅に収まらない。
  - 判断：StatCard・結果内訳は同じラベルを実測して必要時だけ縮小する。結果の目標行は数値列を内容に合わせて広げ、バーが残り幅を使う。Gameの目標チップは名前と数値が同じ行に収まらない場合だけ、同じ枠内の「名前／現在／目標」の配置へ切り替える。結果のボーナス見出しと達成率は最低文字サイズでも収まらない場合だけ2段にする。短い値の配置は保持する。
  - 該当：NumericLabelFitter.cs、DataControls.cs、RoundFlowParts.cs、Data.uss。
- 質問：失敗演出を灰色の半透明レイヤーだけで表すと、食材や鍋に元の色が強く残る。
  - 判断：元画像は変更せず、World内の画像を実行時のシェーダーで段階的に無彩色へ変換する。700msの既存Gray進行を使い、透明度は保持する。線・光粒・画面下地の色も同じ輝度変換を適用する。灰色の覆いは中立色rgb(117,117,117)へ変更し、暖色が戻らないようにする。HUD・操作部品の固有色は対象外。描画用Textureは元画像ごとに再利用し、画面破棄時に解放する。
  - 該当：ImoniEffectGrayscale.cs、ImoniEffectGrayscale.shader、ImoniEffectView.cs、ImoniEffectParts.cs。

## M6 フォントの事前生成（2026-11-26）

- 質問：Home設定を初めて開いた時、Unityの動的Atlas保存でNativeFormatImporter警告が出る。
  - 判断：SetupでBlackフォントのHome/設定・初期プロフィール・大数単位の既知文字を生成し、AtlasをEditor APIで保存・同期Importする。Dynamicは未知の名前のため維持し、警告抑制は行わない。提供フォントにない「秭」は単位の事前生成集合からだけ除外し、既存フォールバックを使う。
  - 該当：ImoniFontWarmup.cs、ImoniSetup.cs、生成済みZenMaruGothic-Black SDF.asset。
