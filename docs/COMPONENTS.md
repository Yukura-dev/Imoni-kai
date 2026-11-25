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
