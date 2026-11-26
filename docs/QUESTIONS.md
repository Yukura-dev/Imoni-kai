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
- 対応済み（M7）：森林・未開放エリアの階段状境界は、既存Tile画像とセル中心マスクの双線形補間・4pxの縁で滑らかにした。Coreの地形・開放・衝突は変更しない。詳細は下記「M7 表示・再接続の判断」と `docs/M7_PRESENTATION.md`。
- 対応済み（M6/M7の定量確認）：R1が2:50時点で倍近く達成できる懸念を受け、M6で目標と供給予測を調整し、M7で共有資金から購入可能な強化を見込むようにした。シード202647のM7実測は、R1の全必須到達が無強化4:17、計画的／予算内無制限強化2:42。R5の予算内無制限強化はM6の約12秒からM7の1:46となった。3方針の成功・失敗をともに `docs/validation/m6/balance.md` と `docs/validation/m7/balance.md` に残す。これは同一シードの自動操作比較であり、全プレイ方針の難易度保証ではない。
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

## M7 フォント事前生成の拡張（2026-11-26）

- 質問：M6のHome/設定124字の事前生成後も、冷起動・Androidビルド後のR5結果でBlack、アウトゲームでBold/Delaの動的Atlas再Import警告が再発する。
  - 判断：全UXML属性・Core/Game文字列リテラル・Config文字列から既知UI文字を集め、全4字体で不足分だけ事前生成する。最終集合は594字で、Zen3種は「秭」以外の各593字、Delaは全角「＋」以外の593字。複数Atlasは配列容量ではなくatlasTextureCountの実使用枚数だけを保存・検証し、文字・設定に変化がないSetupはフォント保存・Importを省く。Dynamicは未知名のため維持し、生成済みフォントは意図した成果物として保持する。
  - 判断：「秭」はZen3種に未収録だが提供Delaに収録されている。Zen→Delaの明示的なフォールバックとDelaでの事前生成を追加し、AndroidのOSフォントに依存させない。Delaのフォールバックは空で循環を避ける。Delaにない全角「＋」はZen Blackの倉庫Smallボタン専用なのでDela集合からだけ除外し、その他の未収録字はエラーにする。M6節の「既存フォールバックを使う」はこの明示設定に置き換える。
  - 検証：全4字体版でSetup2回目の4種FontAssetハッシュ不変・エラー0/警告0。再Play後の両Device SimulatorでMainからLoadingまでのアウトゲーム各状態、GameOver、成功／失敗演出の指定時刻を再撮影し、全件0/0・outsideButtons空。先行Black版の新規プロフィールR1→R5→R6成功・各結果表示とResult/Clear/Endlessの確認も0/0。範囲と証跡はM7_PRESENTATION.md。Android実機確認ではない。
  - 該当：ImoniFontWarmup.cs、ImoniSetup.cs、Fonts/Generated/、docs/M7_PRESENTATION.md。
- 質問：Editorの事前生成が通っても、Androidビルド後にFontAssetが空の状態へ戻る。
  - 判断：TextCoreのClear Dynamic Data on Buildが既定でtrueだったため、SetupのSerializedObject経由で4字体の `m_ClearDynamicDataOnBuild=false` を保存する。Unity 6000.3には公開setterがない。Dynamicは維持し、既知文字・AtlasをAPKへ保持する。最終APKはこの設定で再生成する。
  - 該当：ImoniSetup.EnsureFont、Fonts/Generated/、docs/M7_PRESENTATION.md。

## M7 表示・再接続の判断（2026-11-26）

- 質問：森林・未開放の階段状境界を滑らかにするためにCore地形を変えるか。
  - 判断：Coreは維持し、既存Tile画像を描く表示メッシュとセル中心マスクで境界だけ丸める。双線形補間後の4pxの縁をなじませ、各セル中心の判定を保持する。森林下地は既存平野、未開放は既存半透明画像と鍵ラベルを使う。
  - 該当：WorldBoundarySurface.cs、WorldBoundary.shader、GameWorldView.cs、LockedAreaOverlayView.cs。
- 質問：ローカル版で再接続をどう再現し、進行中の挑戦をどう戻すか。
  - 判断：Editor/Developmentの入口から既存SetConnectedを呼び、3秒実時間後に同じServerへ再接続する。自分の操作だけを遮断し、チームの時間・仲間は継続する。有効な採集先だけ元の選択APIで再開する。通常プレイへ切断をランダム発生させない。
  - 該当：GameScreen.BeginReconnectDemo、ReconnectNotice、ReconnectionTests。
- 質問：演出の完成見出しをロゴのようなPNGへ変換するか。
  - 判断：画像化せず提供Delaを保持する。2行の前景の塗りに同色0.1pxストロークを足して補正し、元の外枠・寸法・座標・タイムラインを維持する。前景の1pxとTextCoreの合成BoldはSDFの字の穴を潰したため、追加前景は元フォントのNormalと0.1pxとする。既存の外枠ラベルのBoldは保持する。文言の正本と複数解像度での描画品質を保てるため。
  - 該当：ImoniEffectFinale、ImoniEffectView.SetupFinaleFill、docs/M7_PRESENTATION.md。


## M7 共有資金からの見込み強化（2026-11-26）

- 質問：M6ではR5開始後の大量投資に目標生成が追いつかず、予算50%の一括強化で約12秒で必須を集められた。未購入の強化をどの程度見込むか。
  - 仮決定：目標生成時の共有資金の50%を、同じUpgradeRulesによる実購入価格の安い順に配分した見込みLvを計算する。係数は `objectiveGeneration.anticipatedUpgradeBudgetFraction=0.5`。同額は `anticipatedUpgradePriority=[amount,luck,speed,move]`、次にactor順で決める。人間（IsBot=false）だけを対象にし、ボットは購入を行わない現行ルールに合わせ現在Lvのままとする。複数人間の場合も予算を各人に重複計上しない。
  - 該当：ObjectiveUpgradeForecast.cs、ObjectiveGenerationConfig.cs、GameConfig.json。
- 質問：見込みLvを目標だけに使うと「達成可能」の根拠がずれる。
  - 仮決定：目標のチーム平均Lv補正・実効供給補正と、A*移動／共有在庫／再生／採集時間／品質／有限連続の供給予定を、すべて同一の見込みLvで計算する。見込み購入はその場の共有資金内で実行可能。実際の資金・Lv・履歴・乱数には触らず、ラウンド開始後は目標を変更しない。Retryは再挑戦の開始資金・初期Lv・元の人間/ボット構成で再生成する。
  - 該当：ObjectiveGenerator.GeneratePlan、ObjectiveGenerationPlan.UpgradeForecast、LocalGameServer、ObjectiveUpgradeForecastTests。
- 質問：見込み投資をしない方針の扱いと、50%の根拠は何か。
  - 仮決定：共有資金の半分を素材購入等に残し、半分を成長に使う自然な初期案を採用。基準量・成長率1.35・品質重み0.35・供給上限85%などM6の係数は維持する。全員が購入可能な能力と適切な操作を使った期待供給に収まる保証であり、投資しない方針やすべての品質乱数列での成功保証ではない。
  - 実測：同じシード202647・同じ3操作方針を再実行。予算内無制限投資はR1 162.057144秒、R2 129.120006秒、R3 145.371428秒、R4 192.914286秒、R5 106.602022秒で全必須達成。M6のR5 12.478390秒から改善。無強化はR3で失敗、各項目+1Lv/Rの計画的強化はR4で失敗し、その記録も残す。資金を100%使う／別の集中投資／連続上振れによる短縮まで一律に防ぐ制限は加えない。
  - 該当：tools/BalanceReview、docs/validation/m7/balance.md、balance.json。正確な時刻は確定OperationRecordの品質換算数量を順に集計して求める。

## M7 二つ名・バッジ・ローカル記録（2026-11-26）

- 質問：全二つ名・バッジの獲得条件が未定。
  - 仮決定：`progression.titleRules/badgeRules` の条件表を採用し、一覧を `M7_PROGRESSION.md` に記載する。二つ名は成功結果で付与、本人の採集・連続・品質バッジは失敗後も残す。購入とボット採集は本人の採集実績に含めない。R3到達と最高到達はラウンド開始時に保存する。
  - 該当：PlayerProgression.cs、ProfileData.cs、GameConfig.json、RoundScreens.cs。
- 質問：通常クリア後のエンドレス移行と、失敗したエンドレスの記録をどう扱うか。
  - 仮決定：Clearから同じワールドのR6へ継続。Homeで解放済みエンドレスを新規選択した場合は初期状態のR1から始める。エンドレスR6以降の成功結果で「記録してホームへ」を選んだ場合だけ正式登録し、失敗・途中中断は未登録。すでに成功した通常R5の記録はエンドレスの失敗で消さない。失敗後のRetryは選択モードを維持して新シードR1へ戻す。
  - 該当：LocalGameServer.cs、LocalRanking.cs、ScreenContext.cs、ProgressionScreens.cs。
- 質問：UI_SPEC 15章のログインは今回のオフライン範囲で何を作るか。
  - 仮決定：端末内の会員プロフィールを確認して続けるLogin画面を追加する。外部アカウント認証とは表示せず、初回はチュートリアルへ、登録済みはHomeへ進む。通常Clearと音量設定は既存画面を利用する。
  - 該当：Login.uxml、ProgressionScreens.cs、OnboardingScreens.cs。
- 質問：旧モックの所有パーツ・バッジ・最高R・ランキングを新しい実績へ移すか。
  - 仮決定：保存済みの所有と選択パーツは保持する。所有配列がなかった旧形式では選択されたカタログ内パーツだけ所有へ移行する。新規プロフィールは初期の1組だけ。保存されていなかったダミーバッジ・最高R3・ランキングは実績へ移さない。端末内ランキングであることを表示する。
  - 該当：ProfilePersistence.Load、ProfileScreens.cs、LocalRanking.cs。

## M7 端末表示とAndroid生成の判断（2026-11-26）

- 質問：390×844の上下余白をノッチの内側にも重ねると、Tutorial・Resultの下部が狭くなる。
  - 判断：`UnityEngine.Device.Screen.safeArea`を使い、ノッチありでは既存の装飾用上下paddingからinset分を先に消費する。通常390×844ではUSSの元のpaddingを維持する。演出はIMONI_EFFECT_SPECの390×844座標を変えず、構図全体をSafe Area内へ等比で収める。
  - 該当：SafeAreaAdapter.cs、Tutorial1〜3.uxml、Result.uxml、GameOver.uxml、ImoniEffectScreen.cs。
- 質問：Androidのパッケージ名・SDK・仮アイコンが未指定。
  - 仮決定：`setup.android`に `jp.yukura.imoni` / versionCode 1 / min API 26 / target API 36 / 512px を置く。縦固定・IL2CPP・ARM64、既存の鍋画像とTheme系の黄色から仮アイコンをEditor APIで生成する。APKは `Builds/Android/Imoni.apk`、実機へのインストールは人間が行う。
  - 該当：ImoniAndroidSetup.cs、ImoniSetup.cs、GameConfig.json。アイコンは非圧縮で取り込み、Androidの任意モジュールはビルド時だけ現在のDebugSymbols APIを解決してSymbolTableのzipを生成する。
- 質問：Unityを開いたままAndroidモジュールを追加すると、ビルド結果がSucceededでもAPKが出なかった。
  - 対応：Editorを再起動して追加モジュールを読み込み直す。ビルド完了判定はresultに加え、totalErrorsと実APKの存在も検証する。初回の未生成結果を成功証跡に使わない。
  - 該当：ImoniAndroidSetup.BuildAndroid、docs/validation/m7/android-build.json。

## M7 ScrollViewと会員証の実画面補正（2026-11-26）

- 質問：Safe Area内に正しい矩形を持つロビー見出し・ランキング先頭行が、Device Simulatorでは描画されない。ConsoleやButton範囲の監査は0件でも検出できなかった。
  - 判断：該当する2つのScrollViewだけ、パネルに追加される前にcontentContainerのUsageHints.GroupTransformを外す。行のレイアウト・通常のスクロール・標準クリップは維持する。viewportのoverflowを変える案では直らず撤回し、GroupTransform解除後のiPhone 12実画像で見出し・順位行の復帰を確認した。内部Unity APIや毎フレームの再構築は使わない。
  - 該当：LobbyScreenコンストラクタ、RankingListコンストラクタ、docs/validation/m7/visual-review.md。
- 質問：iPhone 12で会員証のキャラ編集領域が縮み、見出しが前のカードに重なり、服の色選択がナビの下へ隠れる。
  - 判断：member-mainにも既存safe-design-paddingを適用して装飾余白とSafe Areaの二重加算を避ける。insetありの場合だけAvatarEditor内の見出し・ラベル・色行の下余白を8pxから4pxにし、44pxの色選択ターゲットを保持する。insetなしの390×844では元の寸法を維持する。
  - 該当：Member.uxml、ProfileScreens.uss。
- 質問：ロビー準備完了の画面下部へスクロールしても「準備を取り消す」まで届かない。見た目の描画を直した後の実操作で、ページ763pxに内容が縮み、ReadyActionsの高さが0になって子ボタンだけが外へはみ出していた。
  - 判断：ScrollView内のpage、stack、team、ReadyActionsにflex-shrink:0を指定して内容の自然な高さを測る。余った空間を受け持つspacerだけは縮められるままとし、390×844の既存paddingやボタン寸法は変えない。スクロール可能範囲に下部操作を含める。
  - 該当：Lobby.uxml、LobbyScreens.uss。
- 補足：会員証の青い無地写真枠、Waiting/Loadingの無地の丸アバターは19_Member・15_Waiting・18_Loading_doneのモックどおり。カメラアイコンはTutorialの写真編集時だけ表示し、これらを描画欠けとして変更しない。

## M8 実機プレイテスト修正（2026-11-26）

- 質問：Galaxy A53のHUDに横帯が出る原因は何か。
  - 判断：元実装もviewportを限定していたため「全面描画が原因」とは断定しない。毎フレームのviewport再設定・0.1秒ごとのpadding設定・透明なSafe Area外周を修正する。初期レイアウトが未確定の間はマップの地色を表示し、GeometryChangedでカメラを確定する。実機がADB接続されておらずAndroid版も未確認のため、GPU/API/Frame Pacing由来かは未確定。標準版とGLES3 On/Off版で再確認できるようにする。
  - 該当：GameWorldView、SafeAreaAdapter、Bootstrap、ImoniAndroidSetup、docs/validation/m8/flicker.md。
- 質問：新しい鍋画像は元画像の透明余白と湯気を含めて引き伸ばすか。
  - 判断：同じ原画から本体＋たき火、本体のみ、湯気3片をSetupで切り出し、縦横比を保って配置する。演出は汁面(195,536)を拡大中心にする。前景はSafe Area内、背景だけ画面全体。モックの旧鍋との差はユーザー指定による意図した変更。
  - 該当：setup.potArtwork、imoniEffect.layout、ImoniPotSetup、AnimatedPot、IMONI_EFFECT_SPEC 9章。
- 質問：直線化後、途中で別地点をタップするとどこから経路を引くか。
  - 判断：Coreの現在の連続座標から引く。セル中心へ戻さない。資源には以前どおり隣接停止し、人間同士の重なりは許可する。速度強化は個人Lvなので購入者の残区間だけ新速度で再計時する。
  - 該当：Pathfinding、PlayerSnapshot.Location、LocalGameServer、M8_MOVEMENT.md。
- 質問：「7,804穣8,528穣」の再現がmainでは得られない。
  - 判断：既存コードは「7,804穣8,528秭」、Delaの秭/穣も別glyphで、原因は未確定。上下単位の添字と余り処理を明示し、全階層の回帰テスト・Config重複単位検証を追加。狭いHUDは1単位へ統一する。
  - 該当：Format、IntegerDisplayTests、DataControls、M8_NUMBERS.md。

## M8 追加実機レビュー（2026-11-26）

- 質問：アカウント切り替えの認証方式は未定。仮決定：オンライン化は行わず、設定内から端末内の会員を選択/新規作成する。既存の会員証・二つ名・実績・ランキング・音設定を会員ごとに残す。旧単一保存を自動移行し、1ファイルを原子的に置換する。初回はTutorial1、登録済みはHomeへ直接進む。該当：LocalAccounts、ScreenContext、SettingsDialog、AccountPicker。
- 質問：元画像のロゴ右側の湯気だけを消す方法。判断：TitleLogoの表示領域を原画978pxのうち左780pxに制限し、文字と影の画素を保持する。原画PNGは変更せず、UI部品として湯気なし表示に統一。該当：TitleLogo、ui.titleLogoVisibleWidthFraction。
- 質問：bgm_drone_loop.wavはBGM音量の対象か。判断：UI_SPEC12章の演出ドローンは効果音に分類。未作成の背景曲は独立した任意Clip/ループSourceを用意し、空でも正常動作。Masterの軽いコンプレッサーは共通、下流のBGM/SFXで独立音量。該当：AudioService、ImoniAudioSetup、GameConfig.audio。
- 質問：札を置ける空間が足りない場合。判断：名札（自分優先）→新しい獲得popup→成長札の順。実際に計測した矩形で回避し、場所がなければ低優先札を隠す。自分から1.5タイル以内は選択中でも成長札を隠して芽のみ。該当：WorldAnnotationLayout、GameWorldView、ui.worldView.growthLabelSelfExclusionTiles。

- 追加：アクティブ会員IDが破損し、既存local-defaultと復旧先が衝突する場合は未使用の復旧IDを作る。既存会員を上書きせず、トップレベルの最新プロフィールも保持する。LocalAccountsTestsで再保存・再読込まで確認。

## M9 遊び方・BGM（2026-11-26）

- 質問：最初の依頼のゲームBGM（和風/バンド×4/4・7/8）と、追加資料のUI_SPEC17章が異なる。
  - 仮決定：最新資料を優先し、ゲームは指定曲bgm_game_mainのイントロ→ループ、ホームだけ和風/バンドを切り替える。旧ゲーム曲は新資料にも含まれず、参照しない。この前提を作業中にユーザーへ明示した。差し替えはGameConfig.audioの選曲表・catalogに集約する。
  - 該当：BgmPlan、BgmLoopPlan、ScreenBgmController、ImoniBgmSetup、UI_SPEC17章。
- 質問：ゲーム中の遊び方を閉じると、採集中の位置やパネルを作り直すか。
  - 判断：ScreenRouterのoverlayとして開く。元のGameScreen/Server/パネルを保持し、時間・ボット採集は継続。背面のタップ入力を止める。閉じると同じ設定ダイアログ/メニュー/倉庫へ戻り、倉庫からは第3項目を開く。
  - 該当：ScreenRouter、ScreenContext、HelpScreen、GameScreens。
- 質問：追加資料のUI_SPEC全体と既存M8の記述に差がある。
  - 判断：新規16・17章を取り込み、以前の章は承認済みM8の鍋・座標・ログイン撤去・数値表示を保持。新しいmock/画像は参照資料として受領し、Tutorial2の通常クリア文言とTutorial3未解放チップの点線も合わせる。
- 質問：Vorbisのデコード待ちでイントロの継ぎ目が遅れないか。
  - 判断：登録する4クリップをVorbis品質0.7・DecompressOnLoad・Preloadに統一。予約は0.05秒先、境界は丸めた秒数でなく557149/44100秒と実クリップのsamplesを使用する。全曲原本は参照資料として残すが実行catalogからは参照しない。
  - 該当：GameConfig.audio、ImoniBgmSetup、BgmPlaybackController。
- 質問：標準APKの初回ビルドで、Unity Cloudのシンボル送信認証が403になり警告1件が出た。
  - 判断：ローカル/オフライン範囲では不要なEngine Diagnosticsをsetup.android.engineDiagnosticsEnabled=falseにし、Setupが公開Editor APIで適用する。ローカルSymbolTableのzip生成は維持する。最初の警告もandroid-build-first-attempt.jsonに保存し、再ビルドで確認する。
  - 参考：[Unity 6.3 EngineDiagnosticsSettings](https://docs.unity.com/en-us/engine/6000.3/script-reference/unityeditor/enginediagnostics/enginediagnosticssettings)。設定ファイルの手書きは行わない。

## iPhone起動の準備（2026-11-26）

- 質問：iPhoneで起動したいが、利用できるMacは学校にある。
  - 判断：M9からrebuild/ios-device-buildを作り、iOS設定をSetupへ追加する。学校のMacで同じUnity+iOS Build SupportからXcodeプロジェクトを生成し、本人のApple Account/Teamで実機へ入れる。Macの機種/macOSとiPhoneのiOSは未確認なので、利用可能なXcodeの確定と署名・実機テストはMacが手元にあるときに行う。
  - 該当：ImoniIosSetup.cs、GameConfig.setup.ios、IOS_DEVICE_SETUP.md。Windowsでの設定確認をiOSコンパイル成功と混同しない。
- M9のBGM選曲はユーザーから「新仕様の指定曲を使う」と回答済み。上の仮決定（最新17章のmainイントロ→ループ）は確定。
