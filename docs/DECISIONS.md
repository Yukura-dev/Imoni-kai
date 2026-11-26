# 決定事項・仮決定・解釈メモ

`SPEC_v1.0.md`（仕様書）に書かれていない点や「仮」になっている点について、モック作成時に決めた内容をまとめる。
**仕様書とこのファイルが食い違う場合は仕様書を優先し、実装を止めずに `docs/QUESTIONS.md` に質問を追記すること。**
数値はすべて `unity/Assets/Imoni/Config/GameConfig.json` に入っている。コードに直書きしない。

## 1. 技術・範囲

| 項目 | 決定 |
|---|---|
| エンジン | **Unity 6.3 LTS**、C#（オーナー確定） |
| 画面 | スマートフォン縦画面。基準解像度 **390×844**（モックの px と一致させる） |
| UI | **UI Toolkit**（UXML/USS）。uGUI は使わない |
| マップ | Tilemap ＋ SpriteRenderer（ワールド座標）。UIではない |
| バックエンド | 今回の範囲外（仕様 Phase 4）。サーバーで確定すべき処理は `IGameServer` インターフェースの後ろに置き、今は **ローカル実装 `LocalGameServer`** で動かす。後で AWS（以前の仕様 v2.0 で EC2＋PostgreSQL 案）に差し替える |
| 他プレイヤー | Phase 1〜3 では、チームメイト2人を **ボット**（一定間隔で採集する）で再現する。モックと同じ |
| 数値型 | 数量・資金・ポイントは `System.Numerics.BigInteger`。倍率は固定小数点（例: 1e6 倍した `long`）で計算し、表示時だけ丸める（仕様 6.5） |
| 乱数 | シード付き乱数（例: xorshift / PCG）。同じシード・同じ操作列なら同じ結果になること（受け入れテスト 7） |

## 2. ゲームの数値（仮）

| 項目 | 値 | 根拠 |
|---|---|---|
| 初期資金 | 1,000G、通貨単位「G」 | 仕様 2.1 |
| 強化Lv | 0 から。価格 = 150 × 1.5^Lv | 仕様 8.2（基本価格 150 はモックの仮値） |
| 強化効果 | 移動 ×(1+0.25Lv) / 採集時間 1.6秒÷(1+0.25Lv) / 獲得量 ×(1+0.5Lv) / 採集運は下記 | モック |
| 最高品質の確率 | pTop = min(1, 0.08 × (1 + 0.6 × 採集運Lv))。残りの確率を他の品質の weight で按分 | モック。上限なし（仕様 5.2） |
| 最高品質 | **そのラウンドで解放済みの最も高い品質**（R1は「希少」） | 仕様 6.1 の解釈 |
| 連続倍率 | M = 2^(n/10)。数量は floor(基本 × 獲得量倍率 × M)、ポイントにも M | 仕様 6 |
| 資源 | 1ノードに採集3回分の在庫。尽きたら切り株になり40秒で再生 | モック |
| ショップ購入価格 | 売却単価 × 3。購入品は「普通」品質 | 仮（仕様に価格の定義なし） |
| ボーナス段階 | 100%:×1 / 150%:×2 / 200%:×4 / 300%:×8、100%未満は×0 | 仕様 9.4 |
| 成功ランク | S:ボーナス300%以上 / A:150%以上 / B:100%以上 / C:それ以外（成功時のみ） | **仮**（仕様に定義なし） |
| R1の目標（モック固定） | 必須: 薪60・里芋20・ねぎ15 / ボーナス: キノコ30 | 本番は 9.2 の生成アルゴリズムで決める |

## 3. 画面・UXで決めたこと

- **二つ名**：「前半」＋「後半」の2パーツを集めて組み合わせる（オーナー要望）。初期値は「芋煮会の」＋「初心者」。前半は深緑、後半はえんじのラベル。未入手は「???」。
- **画面遷移**：`docs/UI_SPEC.md` の遷移図を正とする。
  - 成功: メインゲーム → 芋煮会演出（成功）→ 結果
  - 失敗: メインゲーム → 芋煮会演出（失敗）→ ゲームオーバー
- **芋煮会演出**：**どんな条件でも10秒以内**に結果ボタンを出す（オーナー要望）。スコアの増え方は時間で割り付ける（`GameConfig.imoniEffect`）。
  - 文言に「JACKPOT」は使わない（オーナー指摘）。最後は「大成功!! 芋煮 完成!!」。
- **ランキング**：
  - 通常／エンドレス × **共通シード部門／自由シード部門**の両方を作る（オーナー要望）。
  - 表示はチーム人数部門（3人）、クリア状況、シード、記録日時。
  - 失敗した挑戦は登録しないので、自分のチームは「未登録」と表示する。
- **メンバー待ち画面**と**読み込み中（ワールド生成）画面**を追加した（オーナー要望）。
- **倉庫**：表示は「素材×品質」でまとめる。内部ではポイント倍率ごとのロットを保持する（仕様 7.2）。
  - 売却は古いロットから消費する（仕様 7.5）。
- **操作履歴**：`[m:ss] 実行者が○○×数量(品質)を売却 +nG` の形式で、ラウンド経過時間を付ける。
- **切断表示**：
  - メニューのメンバー一覧に「切断中・再接続待ち」と表示する。
  - マップ上のキャラは半透明にする。
  - 切断中のメンバーは採集しない。
- **音**：効果音は `Assets/Imoni/Audio/SFX/` の WAV を使う。
  - 演出中は低音のうなり（ループ）と太鼓のビートが鳴り、レベルで速く・高くなる。詳しくは UI_SPEC の演出の節。

## 4. 仕様書の版について
オーナーからは v2.0（Unity＋AWS構成の詳細を含む）と v1.0（実装前レビュー版）を受け取っている。
**ゲームルールは v1.0 を正とする**。v1.0 で未定の技術選定は、本ファイル 1章の決定に従う。

## 芋煮会演出の修正（2026-11-25）

- `GameConfig.imoniEffect.visual` にIMONI_EFFECT_SPECの画面座標・速度・光・揺れ・拡大の定数、`layout` に390×844上のHUD／テキスト／画像矩形、`audio` に提供WAVの音量・ピッチ・フェード、`effectReview` に録画解像度390×844・25fps・12.6秒・参照スコアとseed40410を追加。ゲームルールの数値は変更しない。元画像を加工せず使用し、旧演出のUnity生成アセットは `Imoni > Legacy > Setup Effect` から生成する（M0統合後のメニュー名）。


## M0統合（2026-11-25）

- 作業先を `D:/UnityProjects/Imoni-kai` に固定。`unity/Assets/` は `Assets/` と読み替える。
- M0の生成値は既存 `GameConfig.setup` を使用する：Panel390×844・Match0・Sort10、Dynamic Font90pt／Padding9／Atlas1024×1024、Orthographic Camera8／位置(0,0,-10)／Clip0.3〜1000、Grid(1,1,0)。今回、新しいゲームバランス値は追加・変更しない。
- `Imoni > Setup Project` はBootの土台を生成する。旧uGUIプロトタイプは比較用に保存し、新Bootには組み込まない。次の実装はM1。


## M1 Core（2026-11-25）

- `numeric.fixedScale=1000000` を追加。連続倍率は整数n乗根で切り捨て、ポイント中間値は整数の固定小数、スコアと報酬は合算後に切り捨て。強化価格はモックと同じ正数の四捨五入。採集時間の下限は固定小数の最小表現単位1/scale秒。
- `economy.purchasedQualityId=normal`、`economy.bonusFundsPerPoint=0.1` を追加。成功時の資金報酬はボーナスポイント×段階倍率×係数を切り捨て。失敗時は0。購入のポイント倍率は1で、採集の連続記録を変えない。
- `format.unitBase=10000`、`format.decimalPlaces=2`、`format.units`（空単位・万・億・兆から無量大数まで）を追加。`Format.Big` は整数のまま最大2桁の小数を切り捨て表示する。
- 乱数はSplitMix64＋棄却抽選。アルゴリズム定数・整数/百分率/10進表現の単位はコード、ゲームの調整値はGameConfigで管理する。
- シミュレーション時間はdecimal秒。`round.mockTimeScale` はCoreでは適用せず、表示・開発側で実時間に掛ける。
- M1は外部注入の目標と採集完了コマンドを用意。マップ由来の目標生成・経路・資源・ボットはM3で接続する。判断の詳細はQUESTIONSのM1節を参照。

## M2a 共通UI（2026-11-25）

- `GameConfig.ui` に、表示用更新間隔16ms／Safe Area再確認100ms、コピー表示1.6秒・チェック回転0.8秒・カウントアップ0.6秒・トースト2.2秒を集約。鍋の上下移動（Small 1.6秒/6px、Large 1.4秒/10px/±4度）と湯気（Small 1.8秒/20px、Large 1.6秒/26px）、バナーpop0.45秒・drop0.4秒・fade1.4秒・peak1.15もモックから移す。M5の演出進行は今回実装しない。
- UIの色・フォント・寸法はユーザー指示に従いThemeとモックのstyleをUSSへ移す。Galleryのサンプル数量・名前は表示確認のfixtureであり、ゲーム状態・バランスには使わない。GameConfigのルール値は変更しない。
- RuntimeAssetsの参照はSetupで生成する。UXMLの埋込StyleSheetとのResources名競合を避け、GalleryのUSSはGalleryLayoutとする。Safe Areaは端末のScreen.safeAreaから換算し、画面デザイン固有の余白とは別に適用する。

## M2b アウトゲーム画面（2026-11-25）

- `GameConfig.ui`へタイトル読み込み80msごと4%、名前上限10文字を追加。`outGame.lobby`へIMO-482/IMO-777、入力上限7、ボット参加2.5/5.2秒、点滅0.5秒・参加pop0.45秒・空枠pulse1.4秒を追加。`outGame.loading`は表示用3秒、step閾値10/38/62/80/100%、同期82/90/100%、ヒント2.6秒。ゲームのラウンドルールは変更しない。
- `outGame.profile/ranking`はモックの初期プロフィール、取得済みパーツ/バッジ、最高R3とランキング表示用ダミーを集約。プロフィール保存はpersistentDataPath/imoni-profile.json、検証は別のLibrary内ファイルを使う。
- disabled色はThemeの専用変数（大: #EFD38A/#5C4518、小: #E9D5AE/#6B4A3A）、opacity1。画面ルートScreenHostでThemeと部品USSを一度読み、部品はattach時に親のシートがない場合だけフォールバックする。
- M2b湯気画像の複製位相差: `ui.smallSteamPhaseDelaySeconds=0.6`・`ui.largeSteamPhaseDelaySeconds=0.5`秒。モックの2本／3本の遅延をAnimatedPotで再利用する。


## M3a ワールド・採集Core（2026-11-25）

- `world`を追加：64×64、chunk16、tileSizePx32、開始点は中央、開放半径10/17/24/32/46、エンドレス半径増分7、拠点空白半径2。ノイズcell8・分解能10000・閾値plain0.35/forest0.62/swamp0.78/mountain1。河川幅2・拠点から5・蛇行±2・4行ごと変化・橋12行ごと。配置確率0.12、各素材最低3ノード、R1はwood/negi/taro/mushroomを保証。
- 在庫3・再生40秒は既存値を使用。`gathering.growthStageFractions=[0.25,0.5,0.75,1]`、ボット思考間隔`botIntervalSec=2`を追加。移動時間は既存140px/秒とタイル32pxから計算。時間精度は既存numeric.fixedScale、ゼロ時間を避ける最小単位は1/scale秒。
- `objectiveGeneration`を追加：必須3、最低0.4、最高0.7。チーム3人×5分の時間を4素材で等分し、往復移動・強化・共有在庫・再生待ちを含む保守的な採集量を算出。品質と連続の上振れを見込まず、下限は切り上げ、上限は切り捨て。不適切な素材を除いて有限回で再選択し、4種類揃わない設定ではエラーにする。
- 品質補正（品質/効率）はplain1/1、forest1.05/1、river1.1/1、mountain1.2/1.15、swamp1.15/0.9。素材はsansaiの品質1.1以外すべて1。ノード固有補正は中立値1で生成し、Coreモデルは個別補正も扱える。元確率へ総品質係数の品質順位乗を掛けて正規化する。
- `qualityRoll.luckPerLevel`を削除。`upgrades.items[luck].effectPerLevel=0.6`を唯一の採集運係数として品質抽選も参照する。
- 生成・目標・採集の乱数列は分離し、目標のseedはworld seed+round（整数のunchecked加算）。A*は上下左右・同距離の順序固定。採集同時完了はメンバー順、5:00は採集完了より先に確定する。

## M3a バランス調整（2026-11-25）

- `materials[].spawnWeight` は薪40・里芋20・ねぎ20・キノコ10・魚6・山菜4。配置密度からマップ全体のノード数を求め、重みで個数を配分してから、その素材に適したバイオームへ配置する。最低供給保証は重みより優先する。
- `world.biomeTargets` は平野35%・森林30%・山岳15%・湿地12%・河川8%。補間ノイズの順位で地形を割り当てる。川の幅は目標割合から算出し、蛇行の接続部分で生じる小さな差を許容する。
- `world.startingPlainFraction=0.6`、`world.minimumStartingRequiredNodes=6`。R1の平野を60%以上とし、R1必須候補は `objectiveGeneration.firstRoundRequiredMaterials` の薪・里芋・ねぎ、各6か所以上を保証する。R2以降の必須候補は従来どおり解放範囲にある全素材。

## M3b ワールド表示（2026-11-25）

- `ui.worldView` にHUD200px・下ボタン84px、資源38×40px/タップ48px、自分48×48px/仲間40×38px（提供4レイヤーPNGの余白を含むキャンバス寸法）、切断opacity0.45、歩行上下2.2px/周期0.28秒、丸い手8px/内側0.7倍/横11px/高さ13px/伸長0.9/採集2往復、接近の表示補正5px、獲得表示1.3秒/上昇18px/高さ58px、成長表示倍率0.35/0.45/0.65/0.85/1、鍵ラベルは実測幅で端へ収める、拠点52×52px/枠3px/中央鍋28×28px、拠点ラベル横補正-30px、ラベル余白3pxを集約。1タイル32pxはCoreのworld.tileSizePxを共用する。
- `ui.timerUrgentSeconds=30`、`outGame.lobby.commonSeed=202647`を追加。共通seedの表示#2026-47と対応させる。実プレイは実時間でTickし、旧round.mockTimeScaleは適用しない。

## M4 パネル（2026-11-26）

- `ui.worldView.growthLabelDistanceTiles=2`。成長札は近距離または選択資源のみ表示する。獲得表示の連続数は最高品質かつ1以上に限る。
- `ui.panels`：保護増減5、売却割合25/50/75/100%、初期割合50%、購入数量10/50、ショップ素材wood/taro/negi/mushroom、履歴3件、効果の表示精度は移動/採集速度2桁・採集量/採集運1桁。BottomSheet高さは倉庫620/ショップ540/強化560/メニュー440px、上の影8px（モック）。採集運はバイオーム・資源補正前の最高品質基本確率を表示し、現場の補正は採集時に適用する。

## M5 ラウンドの流れと演出（2026-11-26）

- タイムアップは既存のCore期限300秒で一度だけ確定し、`RoundResult` を演出・結果画面へ渡す。R1〜R4成功はResultから `StartNextRound`、失敗はGameOverから `Retry(newSeed)` とLoading、5成功は共通の結果部品を使ったClearからHomeへ進む。結果表示中はワールドの時間を進めない。
- ランクは既存 `rankGrades` の300/150/100/0%を使用する。`RoundEvaluation` にR/Bの固定小数の中間値を保持し、内訳表示でも精度を失わない。スコア・資金報酬の計算式は変更しない。
- `titles.unlockRules` を追加：`second/鍋奉行` は `minimumRound=1, minimumStreak=0, requiresClear=false`、`first/伝説の` は `minimumRound=5, minimumStreak=0, requiresClear=true`。成功結果だけを対象に未所持パーツを追加し、プロフィールの所持配列に保存する。初期所持・選択・音設定を保持し、同じ結果の再表示では重複付与しない。
- 失敗演出の成功基準は、現在のR/Bを保ち、不足必須素材の品質換算数量×基本ポイントをRへ加えた仮の成功スコア（整数比で計算後に切り捨て）。基準は実スコア以上かつ最低1。旧プロトタイプの実スコア÷0.4はBootのM5経路には適用しない。表示する最終スコアと失敗報酬0は確定結果のまま。
- 開発用短縮は既存 `round.mockTimeScale=10` を再利用する。Editorメニュー `Imoni > Development > Short Rounds (GameConfig)` の既定はOFF。Development Buildは `--imoni-short-rounds` を明示した場合だけ有効。Releaseでは無効・常時1倍。演出の時計は短縮対象外。
- 演出部品のプール数は `imoniEffect` の発生間隔・寿命・段階数から算出し、独立した固定個数は追加しない。結果ボタン表示時にスコア表示を確定値へ合わせ、巨大スコアの指数補間の残りを表示し続けない。演出の座標・光画像・時間上限は既存IMONI_EFFECT_SPECのまま。

## M6 目標インフレ（2026-11-26）

- `objectiveGeneration.baseAmounts` はwood240/taro160/negi160/mushroom80/fish60/sansai40。`minimumFraction=0.9, maximumFraction=1.1` はこの基準量の抽選幅とし、M3の「供給量の40〜70%」から役割を変更した。`roundGrowth=1.35` は抽選幅の最大比率を上回り、`highestQualityWeight=0.35` は `1+0.35×(当該R最高品質倍率/R1最高品質倍率−1)` として使う。
- チーム補正は `[1+Σ(チーム平均Lv×upgradeWeights)]×[1+upgradeSupplyWeight×max(0,実強化期待供給/同条件の無強化期待供給−1)]`。`upgradeWeights={move:0.1,speed:0.15,amount:0.35,luck:0.12}`、`upgradeSupplyWeight=1`。数値はBigIntegerと整数比で計算し、数量をfloatへ変換しない。
- `simulationGathersPerVisit=3` は1ノードの在庫をまとめて採って戻る予定。全員の5分を必須＋ボーナスで等分し、品質分布は実QualityRollerと同じ、連続は有限回数の状態確率から期待量を求める。各素材段階の連続は0開始、固定小数の小さい確率の端数は0連続へ戻して上振れを過大評価しない。最終要求は式の結果と `maximumCapacityFraction=0.85` ×期待供給の小さい方。共有在庫・再生・A*往復・採集時間を検証できる予定を公開する。
- QA専用 `balanceReview` はseed202647、upgradeBudgetFraction0.5、plannedLevelsPerRound1、priority=[amount,luck,speed,move]。無強化、計画的、予算内無制限の3試行を通常IGameServer操作で比較する。目標生成後の強化によって確定目標は再計算しない。供給・目標の係数とQAポリシーを分け、正確な達成時刻は履歴から再構築する。
- 実測では無強化R1の必須達成2:36、R4失敗。計画的強化はR1約2:03→R5約1:55となり、後半の要求が数十のままになる問題を解消した。予算内無制限投資はR5約12秒で達成するため、その限界も残す。採集式、連続倍率、強化上限、資金報酬は変更しない。

## M6 整数表示・失敗演出（2026-11-26）

- `format.maxDisplayGroups=2`、`format.achievementMultipleThresholdPercent=1000` を追加。`Format.Big` は整数の単位グループで表示し、1万以上の値は万未満を省略する。M1の「大数を小数で表示」はこの仕様に置き換える。`format.decimalPlaces` は連続倍率などの係数表示で引き続き使う。スコアの固定小数計算・最終切り捨ては変更しない。
- `ui.numericLabelMinimumFontSize=10`。共通数値ラベルは実測幅に応じて元のUSSサイズから縮小する。長いGame目標は枠の高さを変えず複数行にし、Result/Clearの目標行は数値列を可変幅にする。入力欄・ログ・検証JSONは省略せず正確な値を保持する。
- `imoniEffect.layout.grayOpacity=0.65`、`darkOpacity=0.30`。既存の700ms灰色化に合わせ、Worldの画像自体にもRec.709輝度（0.2126/0.7152/0.0722）への補間を適用する。これらの係数は色変換の定義でありバランス値ではない。透過alphaと元画像を保持し、GPUコピーは初回のみ確保、Gray変化時だけ再描画、再生終了後の同じGray値では再描画しない。画面破棄・再構築時にRenderTextureとMaterialを解放する。
- 灰色の覆いは元の暖色の輝度に近い中立色rgb(117,117,117)とし、画像の下にあるUSSの画面背景色も同じGray値で無彩色化する。HUD・操作部品の固有色は保持する。結果の極端な大数でボーナス見出しと達成率が最低文字サイズでも収まらない場合は、共通ResultBonusCardの見出しだけを2段にする。
- 不足文のかぎかっこは `必須素材「魚」が足りなかった` の形に統一する。素材名は確定したCore結果から取得する。

## M6 音と設定の生成物（2026-11-26）

- `audio` に保存音量の初期値1、範囲0〜1、刻み0.1、Master下限−80dB、振幅のdB換算20log10を追加。Masterの内蔵CompressorはThreshold−18dB/Attack20ms/Release120ms/Make up gain0dB。Unityが公開しないRatioは設定せず、実在するパラメーターだけをEditor APIで設定する。
- Home設定で発生した動的Atlasの再Import警告を避けるため、`ImoniFontWarmup` でBlackフォントの既知124字をSetup時に生成する。対象はHome/設定と共通タブ・初期プロフィール・大数単位。未収録の「秭」は単位の事前生成集合からだけ除外する。未知名のためDynamicは保持する。生成済み文字が再度要求された時のAPI戻り値に依存せず、生成後の文字辞書で実在を確認する。

## M7 フォントの再Import対策（2026-11-26）

- M6のBlack124字を、全UXML属性・Core/Game文字列リテラル・GameConfig文字列由来の既知UI集合へ拡張し、全4字体を事前生成する。最終集合594字からZen3種は「秭」以外の各593字、Delaは全角「＋」以外の593字を登録。未知名のためDynamicは維持する。
- Zen3種は提供Delaを明示的なフォールバックにし、Delaはフォールバックなし。Zenにない「秭」をAndroidのOSフォントに依存せず表示する。Delaにない全角「＋」はZen Blackの倉庫Smallボタン専用で、Dela集合からだけ除外する。既存TTFだけを再利用し、生成した4種のFontAssetを保持する。
- Androidビルドが事前生成データを消す既定動作を避けるため、4字体の `m_ClearDynamicDataOnBuild=false` をSetupのSerializedObject APIで保存する。Unity 6000.3では公開setterがないためのEditor設定であり、YAMLの手編集は行わない。Dynamic自体は維持する。
- 未登録文字だけを追加し、無変更Setupではフォント保存・再Importを省く。Atlas配列の未使用nullは実使用枚数atlasTextureCountで除外する。全4字体版のSetup2回目で4フォントのファイルハッシュ不変・エラー0/警告0を確認。再Play後の両Device Simulatorでアウトゲーム各状態・GameOver・成功失敗演出指定時刻を再撮影し、全件0/0・outsideButtons空。先行Black版の新規プロフィールR1→R5→R6成功・各結果表示も0/0。詳細と範囲はM7_PRESENTATION.md。

## M7 表示・再接続（2026-11-26）

- `ui.worldView.boundaryFeatherPx=4`。森林と未開放の表示は共通のセル中心マスクを双線形補間し、既存Tile画像で角を丸める。1セル=1Unity単位・32px、Coreの地形と通行判定は維持する。
- `ui.reconnectDemoSeconds=3`。Editor/Developmentのデモは実時間で接続を復帰し、同一Server/Worldの状態を表示する。通常版には故意に切断する画面操作を出さない。切断中も仲間とラウンドタイマーは継続する。
- `imoniEffect.layout.finaleFillStrokePx=0.1`。完成2行はPNG化せず、Delaの文字と同色の前景ストロークで擬似太字の差を補う。実画像QAで1pxは字の穴を潰すため0.1へ調整。元の外枠・光彩・位置・成功58px/失敗52px・時刻は変更しない。


## M7 見込み強化と目標生成（2026-11-26）

- `objectiveGeneration.anticipatedUpgradeBudgetFraction=0.5`、`anticipatedUpgradePriority=[amount,luck,speed,move]` を追加。共有資金の半分を一つの仮予算として、人間だけに実UpgradeRules価格の安い順で配分する。同額は設定順→actor順。ボットは強化を買わない。実際の資金・Lv・操作履歴・乱数を変更せず、結果は読み取り専用の見込みプレイヤーと購入明細で保持する。
- M6のチームLv補正と実効供給比のLvを見込みLvに差し替える。達成可能シミュレーションにも同じLvを使い、目標はその期待供給の85%以内。開始資金で実行可能な購入と時間／共有在庫を守る採集予定の存在を確認する。生成後は実際の投資によって目標を動かさない。再挑戦は開始資金・初期Lvで生成する。
- 見込み割合0.5以外の目標係数はM6から維持。同シード202647の無制限投資R5は12.478390秒→106.602022秒で必須達成。無強化R3失敗・各項目+1Lv/Rの計画的強化R4失敗も保存する。実採集／連続倍率／価格／報酬は変更しない。3試行と明細は `docs/validation/m7/balance.md` / `balance.json`。

## M7 二つ名・バッジ・ローカル記録（2026-11-26）

- `progression.titleRules/badgeRules` に全条件を集約。前半はR2/10連続/通常1クリア/R7/R10、後半は薪30/里芋30/初成功/総採集300/通常3クリア。初期パーツは「芋煮会の」「初心者」のみ。バッジは初成功/R3到達/10連続/通常クリア/成功累積1億/究極品質。詳細表はM7_PROGRESSION.md。
- 最高到達はラウンド開始時、採集実績は確定RoundResultの当該ラウンド・本人Gatherから保存。挑戦ID:ラウンドで再加算を防止する。IDは保存の識別用で、Coreのシード付き乱数に影響しない。既存所有パーツと旧形式の選択パーツは移行する。
- `progression.rankingLimitPerBoard=100`。通常/エンドレス×共通/自由、人数別（共通はシード別）で保持。通常はR5成功時に累積スコアを登録。エンドレスはR6以降の成功結果から明示終了した時だけ記録する。失敗/中断では新しいランキングを登録しない。
- 通常ClearからStartEndlessで同じワールドR6へ進む。Homeから新規エンドレスを選んだ場合と失敗後Retryは初期状態R1から開始し、選択モードは維持する。成功結果からFinishEndlessを実行するとEndlessFinishedへ移り、次ラウンドや二重終了を拒否する。
- UI_SPEC15のLoginは端末内プロフィールを利用する画面とし、外部認証は行わない。通常Clear・音量設定は既存画面、Endlessは既存部品を組み合わせた移行画面で補う。ランキングのM2bダミーは表示せず、空状態と実記録を使う。

## M7 端末・Android設定
- Safe AreaはDevice.Screenを使用。既存の装飾paddingからノッチ分を消費し、演出の390×844構図は等比で内側へ収める。
- `setup.android`: package `jp.yukura.imoni`、versionCode 1、min API 26、target API 36、icon 512px。Editor APIでPortrait/IL2CPP/ARM64と仮アイコンを生成。Android Build Supportは任意導入とし、通常のSetupではAndroid専用型へのコンパイル依存を作らない。
