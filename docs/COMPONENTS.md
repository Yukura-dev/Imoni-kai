# 部品カタログ（再利用のルール）

**方針：2つ以上の画面に出てくるものは、必ず部品にして使い回す。** 画面の UXML には部品を並べるだけにし、見た目の細部を画面ごとに書かない。

## 1. 作り方

### UI（UI Toolkit）＝「カスタムコントロール」
UI Toolkit にはプレハブがない。代わりに次の形で部品を作る。
```csharp
// Assets/Imoni/Scripts/Game/UI/Components/TitleChips.cs
using UnityEngine.UIElements;
[UxmlElement]
public partial class TitleChips : VisualElement
{
    [UxmlAttribute] public string First  { get => _first.text;  set => _first.text  = value; }
    [UxmlAttribute] public string Second { get => _second.text; set => _second.text = value; }
    readonly Label _first = new Label(), _second = new Label();
    public TitleChips()
    {
        AddToClassList("title-chips");
        _first.AddToClassList("chip"); _first.AddToClassList("chip--title-first");
        _second.AddToClassList("chip"); _second.AddToClassList("chip--title-second");
        Add(_first); Add(_second);
    }
}
```
- 画面の UXML からは `<TitleChips first="芋煮会の" second="初心者" />` と書くだけで使える。UI Builder にも出る。
- 部品の中身が大きいものは、`Resources/UI/Components/<名前>.uxml` を読み込んで組み立ててよい。
- 部品のスタイルは `Resources/UI/Components/<名前>.uss`（共通の色・フォントは Theme.uss の変数を使う）。
- **見た目の種類**は `variant` 属性（enum）で切り替える。例：`<ImoniButton variant="Primary" text="次へ" />`。
- **データの受け渡し**は `Bind(モデル)` メソッドでまとめて行う。画面側は値を流し込むだけにする。

### ワールド・演出（GameObject）＝「プレハブ」
- `ImoniSetup`（Imoni > Setup Project）の中で、`PrefabUtility.SaveAsPrefabAsset` を使って **コードから生成** する。
- **プレハブの YAML を手書きしない。**
- 置き場所は `Assets/Imoni/Prefabs/`。Setup をもう一度実行したら作り直されるようにする（冪等）。

## 2. UI部品の一覧（M2 で最初に作る）
| 部品 | 使う画面 | 主な属性・メソッド | モックの該当箇所 |
|---|---|---|---|
| `ImoniButton` | ほぼ全画面 | `variant`: Primary / Danger / Yellow / Outline / Small / Icon / Action、`icon`、`disabled`、押すと沈む | 各画面の大ボタン、Game の下ボタン |
| `Card` | ほぼ全画面 | `variant`: Cream / White / Dark / Dashed / Shadow、`header`（焦げ茶の見出し帯） | 枠付きの角丸カード全般 |
| `ScreenHeader` | Lobby, Waiting | 戻るボタン、タイトル、右側の任意要素 | 「< ロビー」「< キャンセル」 |
| `StepTitle` | Tutorial1〜3 | `step`（1/3）、見出し | 「会員登録しよう!(1/3)」 |
| `PagerDots` | Tutorial1〜3 | `count`, `index` | 下のページ送りの点 |
| `TabBar` | Home, Ranking, Member | `selected`: Home / Ranking / Member。押すと Router で遷移 | 下タブ |
| `SegmentedControl` | Ranking（モード・部門）, Lobby（シード） | `options`, `selected`, `changed` イベント | 通常/エンドレス、共通/自由シード |
| `TitleChips` | Tutorial1, Home, Member, Lobby, Waiting, Result | `first`, `second`, `size` | 二つ名（前半＝深緑、後半＝えんじ） |
| `TitlePartPicker` | Member | 所持パーツ一覧、選択中、未入手は「???」 | 二つ名をくみあわせる |
| `Avatar` | Home, Member, Lobby, Waiting, Loading, Game | `size`, `bandColor`, `outfitColor`, `photo`, `dimmed`（切断中）。4レイヤーを重ねる | アバターの丸・キャラ |
| `AvatarEditor` | Member | はちまき・服の `ColorSwatch` の列 | 自分のキャラの編集 |
| `ColorSwatch` | Member | `color`, `selected`（二重の輪） | 44px の色ボタン |
| `MemberCard` | Tutorial1, Member | 表／裏の切り替え、`editableName`、`Bind(Profile)` | 会員証 |
| `BadgeGrid` | Member（会員証の裏） | 取得済み・未取得 | 達成したバッジ |
| `MemberRow` | Lobby, Waiting, Game（メニュー） | `Avatar`＋`TitleChips`＋名前＋`StatusBadge` | メンバーの行 |
| `StatusBadge` | Lobby, Waiting, Loading, Game | `state`: Ready / Waiting / Host / Joined / Offline / Collecting | 準備OK・準備中・切断中 |
| `InviteCodeCard` | Lobby（チーム）, Waiting | `code`、コピー（「コピーしました!」）、共有 | 招待コードの帯・カード |
| `StatCard` | Home, Result, GameOver | `label`, `value` | 最高到達・通常クリア など |
| `ProgressBar` | Main, Loading, Game, Result, GameOver, Tutorial3 | `value`（0〜1）、`variant`: Thick / Thin / Rainbow、`fillColor` | 進捗バー全般 |
| `ChecklistStep` | Loading | `state`: Done / Active / Pending、`label`, `note` | ワールド生成のステップ |
| `QualityChip` | Tutorial3, 倉庫, 採集ポップアップ | `quality`（色は GameConfig.qualities） | 普通・良質・希少… |
| `MaterialGoalChip` | Game HUD, Result, GameOver | `material`, `current`, `required`, 達成で色が変わる | 必須素材のチップ・行 |
| `BonusTierChips` | Game HUD, Result | 達成率、段階（×1/×2/×4/×8） | ボーナスの段階 |
| `BigNumberLabel` | Game, Result, Ranking, Imoni | `BigInteger` の値を `Format.Big()` で表示、カウントアップ | スコア・資金 |
| `FundsPill` | Game HUD, 各パネル | コインアイコン＋金額 | 共有資金 |
| `BottomSheet` | 倉庫, ショップ, 強化, メニュー | `title`、右側の資金表示、閉じる、中身のスロット | 下から出るパネル |
| `InventoryRow` / `ShopRow` / `UpgradeRow` / `RankingRow` | 各パネル, Ranking | `Bind(データ)` | 一覧の行 |
| `Toast` | Game | `Show(text, seconds)` | 下の履歴トースト |
| `ErrorMessage` | Lobby（以後の入力全般） | `text` | えんじ枠のエラー |
| `AnimatedPot` | Home, Waiting, Loading, Tutorial2 | `variant`: Campfire / Small / Large、湯気・揺れの有無 | 湯気の出る鍋の絵 |
| `PatternBackground` | Main, Loading | 里芋柄のタイル | 背景柄 |
| `ResultBanner` | Imoni, ImoniFail | `variant`: LevelUp / Success / Fail。縁取りの文字と出方のアニメーション | LEVEL UP!・大成功!!・未完成… |

**確認用の画面**：`ComponentGallery` 画面（開発ビルドだけ）を作り、上の部品を全種類・全バリエーション並べる。人間はここで見た目をまとめて確認する。

## 3. プレハブの一覧（M3・M5 で `ImoniSetup` から生成する）
| プレハブ | 中身 | 使う場所 |
|---|---|---|
| `ResourceNode.prefab` | SpriteRenderer（種類・成長段階で差し替え）、タップ判定、`ResourceNodeView`（採集バー・「成長中 n秒」の表示） | マップ上の資源（薪・里芋・ねぎ・キノコ…すべて同じプレハブ） |
| `Character.prefab` | SpriteRenderer（体の白レイヤーに色を付ける）、名札、`CharacterView`（経路に沿った移動の補間・切断中の半透明） | 自分とチームメイト（同じプレハブで、色と名前だけ変える） |
| `GatherPopup.prefab` | 「+3 薪(良質) 連続4」のポップアップ（品質の色） | 採集したとき |
| `LockedAreaOverlay.prefab` | 灰色の覆い＋鍵アイコン＋「R2で探索範囲が拡大」 | 未開放エリア |
| `TileSet.asset` | `Tiles/` の画像から作った Tile 一式 | Tilemap |

**演出（Imoni）**：UI Toolkit の部品（`FallingItem`, `StarBurst`, `GlowRing`, `StreakLine`, `Sparkle`）として作る。同じ部品を使い回し、毎回 new せずに**プールして再利用**する。

## 4. ルール（AGENTS.md と同じ）
- 同じ見た目を2か所に書いたら、それは部品にする合図。
- 部品は「表示だけ」を担当する。ルール（計算・判定）は Core に置き、部品には結果だけを渡す。
- 部品の属性名・バリエーション名は、この表の名前に合わせる（増やすのはよい。ここに追記すること）。


## M2aで追加した表示属性

カタログの基本属性に加え、モックの画面差を共通部品内で表す。各値の対応箇所は `M2A_COMPONENTS.md` に記載する。

- `Card.header`、`StepTitle.stacked`、`ImoniButton.selected`。
- `TitleChips.size`: Compact / Regular / Large。
- `SegmentedControl.variant`: Pill / Tabs / Seed。`InviteCodeCard.variant`: Compact / Large。
- `StatCard.variant`: Home / Result / GameOver。`MaterialGoalChip.variant`: Chip / Row / FailureRow。
- `RankingRow.rankStyle`: First / Podium / Normal、`alternating`。
- `PatternBackground.variant`: Yellow / Transparent。`AnimatedPot.steam / sway`。
- `QualityChip.showMultiplier`、`ResultBanner.subtitle / missingMaterial`。
- `BottomSheet.inline`: Gallery内で通常のレイアウトフローに並べるための開発用属性。

## USSの制約（ユーザー指示、2026-11-25）

`:first-child`、`:last-child`、`:nth-child`など、USS非対応の構造擬似クラスは使わない。TabBarの先頭タブには `tabbar__item--first` クラスを付ける。共通Themeは `.tabbar__item--first` で先頭の左罫線を消す。

## M2bで追加した構成部品

| カスタムコントロール | 表示・再利用する内容 |
|---|---|
| `ImoniText` | Themeを継承する共通画面文言 |
| `Artwork` | 配布画像を素材キーで配置する装飾 |
| `InputEntry` | Themeの入力欄。`placeholder`属性 |
| `TutorialTimelineStep` | 時刻・見出し・説明。`tone`: Start / Bonus / Finish |
| `TutorialInfo` | Card内の見出し・説明。`tone`: Green / Red / Compact |
| `UpgradeHint` | 強化の見出し・説明 |
| `WaitingMemberSlot` | MemberRowと空の待機枠。`filled`と待機点の表示 |
| `LoadingMember` | Avatar・名前・同期状態 |
| `RankingList` | ScrollView内にRankingRowを配置。順位計算はしない |

ScreenHostがThemeと全部品USSを共有し、個々の部品には祖先に未読込の場合だけUSSを追加する。M2bの画面・部品対応と使い方は `M2B_SCREENS.md` に記載。

## M4で追加した構成部品・表示属性

| 部品 | 用途 |
|---|---|
| `InventoryTableHeader` | 倉庫の素材・所持・保護・売却可・単価の列見出し |
| `InventorySelectionControls` | 選択行の保護操作・割合・数量入力・即売却。操作意図だけを通知し、計算はCoreの投影結果を受け取る |
| `OperationHistory` | Coreが整形した直近の履歴を表示 |
| `UpgradeRow.inlineEffects` | 現在→次の効果を横並びにするGameパネル用属性 |
| `MemberRow.BindConnection` | 接続状態の点・名前・状態文言を表示するGameメニュー用形式 |

`GamePanelParts.uss` もScreenHostで一度だけ読み込む。4つのパネルは `GamePanels.uxml` に既存の `BottomSheet`、行部品と上記部品を配置して構成する。

## M5で追加した芋煮会演出の部品

`Assets/Imoni/Scripts/Game/UI/Components/ImoniEffectParts.cs` の `[UxmlElement]` 部品を、成功・失敗の両方で共用する。専用の座標・光彩表現は `UI/ImoniEffect.uss` と `GameConfig.imoniEffect.layout / visual` に従う。画面UXMLにはスコア・音アイコン・バナー内部のラベルを直接記述しない。

| 部品 | 再利用する内容・入力 | モック／仕様との対応 |
|---|---|---|
| `ImoniEffectSound` | `ImoniButton` のOutlineを継承し、音アイコン・文言を内包。クリックの意図を画面へ通知 | Imoni / ImoniFail左上の音ボタン |
| `ImoniEffectScore` | `Card` を継承し、スコア見出し・レベル・数値・虹のゲージを内包。Card内部の本文をinset 0に固定して座標原点を維持 | 両演出の上部スコアカード |
| `ImoniEffectBanner` | `ResultBanner` のLevelUp構造を再利用。時刻による変形は `ImoniEffectTimeline` の結果を受け取る | LEVEL UP! とレベル名、仕様4章のy=300 |
| `ImoniEffectFinale` | `ResultBanner` の見出し・2行の本文・補足構造を再利用。成功／失敗・不足素材名を投影結果から表示 | 大成功!!／芋煮完成!!、あれ…?／未完成…、仕様5・6章 |
| `FallingItem` | 配布済み `ingredient_*_glow` を表示。Coreの `ImoniIngredientPose` の位置・角度・倍率を反映 | 食材の落下、仕様4章 |
| `StarBurst` | 星群を1単位として保持。`Prepare` で子の星画像を事前生成し、`SetStar` で `ImoniStarPose`、`HideFrom` で未使用分を反映 | 着地の小さな星群、レベルアップ・完成の14個の星 |
| `GlowRing` | `fx_ring_glow` の直径・中心・不透明度を反映 | レベルアップ・完成の光の輪 |
| `StreakLine` | 初期シードで決まる位置・幅・色・周期を持つ縦の光 | 仕様4章の光のライン |
| `Sparkle` | 配布済み `fx_sparkle` の色・点滅・漂いを反映 | 仕様4章のキラキラ |

`ImoniEffectView.Begin` でプールを一度確保し、再生中の着地や `Replay` ではVisualElementを追加・削除せず、表示・座標・画像を更新して使い回す。容量は固定の魔法数ではなく、Coreの `ImoniEffectPoolCapacity.From(config)` が最短出現間隔、最大落下時間、星の寿命、レベル数から保守的に計算する。現在の設定では食材28、星群38（各14個の星）、輪6、光のライン46、キラキラ50。星群の外側と未使用の星は非表示にする。

画面は `ImoniEffectScreen` の専用Hostへマウントし、ScreenHost全体を消さない。通常の結果・リプレイ・スキップには既存 `ImoniButton` を使う。スコア・成否・不足素材・遷移先は確定済み `RoundResult` のCore投影だけから受け取り、部品内では精算・目標判定をしない。

## M5で追加したラウンド結果の部品

`RoundFlowParts.cs` の `[UxmlElement]` 部品をResult/Clear/GameOver/タイムアップで共用する。`Resources/UI/Components/RoundFlow.uss` はScreenHostが一度だけ読み込み、単独表示時だけフォールバックで追加する。

| 部品 | 入力・再利用 | モック対応 |
|---|---|---|
| `RoundHeader` | ラウンド番号と成功文言の帯 | Result / GameOver上部 |
| `RankHero` | ランク丸と2行の見出し | Result / Clear |
| `RequiredGoalsCard` | 既存MaterialGoalChipのRow/FailureRow。長い名称がある場合だけ名称列を拡張 | Result / GameOver必須素材 |
| `ResultBonusCard` | ボーナス達成状況と既存BonusTierChips | Resultボーナス欄 |
| `ScoreBreakdownCard` | Coreが計算したR/B/Tと重み適用後の値 | Resultスコア内訳 |
| `ResultSummary` | CardとStatCardの2列表示 | スコア・資金・累積・二つ名 |
| `FailureResetCard` | Cardと共有のベクター点線枠 | GameOverリセット対象 |
| `RoundTimeUpDialog` | 時刻と既存ImoniButton、Card、半透明背景 | 37_Game_timeup |

各部品は表示を担当し、成否・内訳・報酬・二つ名条件はCoreに置く。Result/Clearは同じ `Result.uxml` を使い、確定済みLastResultを表示する。

## M6の音量部品と長い数値

`AudioVolumeControl` は `[UxmlElement]` で、既存のSmallボタン2個とThinのProgressBarを組み合わせる。Homeの設定CardとComponentGalleryから再利用でき、変更方向だけを通知する。音量の刻み・上下限・保存はCoreの `ImoniAudioPlan` とプロフィールに委ねる。`AudioSettings.uss` はScreenHostで一度だけ読み込む。

大きい数値はCoreの `Format.Big` で整形する。StatCard・目標数量・スコア内訳は共通の `NumericLabelFitter` で実際の文字幅を測り、欄からあふれる場合だけ縮小する。数値を切り捨てて別の値へ書き換えず、極端な長さには完全な文字列のtooltipを保持する。短い数値は従来のモック寸法を保つ。

## M7の表示部品

- `ReconnectNotice`：既存Card/ImoniButton/Themeの再接続オーバーレイ。状態はGameScreenから渡し、ホームへ戻る操作意図だけを通知する。
- `WorldBoundarySurface`：森林とLockedAreaOverlayで共用する描画メッシュ。提供Tile画像とCore由来のセルマスクだけを受け取り、通行・開放・資源の判定を持たない。画面破棄時に描画用Mesh/Material/Textureを解放する。
- `ImoniEffectFinale`：完成2行へ前景ラベルを1枚ずつ事前追加し、同色ストロークでブラウザーの太字との差を補正する。元のResultBanner構造・文言・位置を共用する。
- `RankingList.empty-text`：実記録が0件の部門に空状態を表示する。行は既存RankingRowを使い、並び順・登録可否・モード/シード部門の絞り込みはCoreのLocalRankingへ委ねる。
- `ResultSummary`：複数の二つ名を同時に獲得した場合は、Coreから受け取った「先頭1件＋ほかn件」の2行を既存StatCardへ渡す。全名称はtooltipに保持し、会員証のTitlePartPickerでも確認できる。1件だけの表示はモックのまま。獲得条件や所持更新は部品に置かない。
- `Login` / `Endless`：UI_SPEC15の補完画面。Card・MemberCard・StatCard・AnimatedPot・ImoniText・ImoniButtonを組み合わせ、新しいボタンや会員証の中身は複製しない。Loginは端末内プロフィールの確認、Endlessは通常Clear後の引継ぎ内容と記録条件を表示する。

## M8 追加部品・変更

- **SettingsDialog**（UxmlElement）：Main/Homeで共用。設定Card、全体ON/OFF、BGM/効果音のAudioVolumeControl、アカウント切替、閉じる。操作意図をイベントで通知し、保存はScreenContextが担当。
- **AccountPicker**（UxmlElement）：保存済み会員の一覧・名前入力・新規会員ボタン・音設定へ戻る。LocalAccountsをSettingsPresenterから投影し、UI自体は保存しない。
- **TitleLogo**（UxmlElement）：既存Artworkをクリップする湯気なし版。元PNGと文字の位置を保持。
- **QualityChip.Locked**：グレーの???、倍率非表示。Tutorial3では神話以降に適用。
- **ProgressBar**：外側角丸−枠幅から塗りの角丸を決め、Thick/Thin/Rainbow共通で枠内いっぱいに表示。
- **ImoniButton.Icon**：枠内の50%位置と負の50%移動でアイコン中央を一致させる。
