# M2a: 共通UI部品とComponentGallery

M2aはThemeの読み込み、画面の切り替え先を管理するScreenRouter、Safe Area、`COMPONENTS.md` 2章の35部品、および部品確認用のComponentGalleryを対象とする。Main・Tutorial・Home・Lobbyなどの実画面、採集操作、ゲーム進行、会員情報の保存は今回の対象に含めない。画面の組み立てと画面間の遷移接続はM2b以降で行う。

## 1. 開き方

1. 正本 `D:/UnityProjects/Imoni-kai` をUnity 6000.3.21f1で開く。
2. Play Modeを終了し、`Imoni > Setup Project` を実行する。Boot、PanelSettings、FontAsset、Theme・Config・画像への参照を持つUIRuntimeAssetsをEditor APIで生成・更新する。
3. `Imoni > Open Component Gallery` を実行する。Bootを開いてPlay Modeに入り、ComponentGalleryを表示する。すでにBootを再生中の場合も同じメニューで開ける。
4. Gameビューを390×844にする。全サンプルは1つの縦ScrollViewに並ぶ。上部の部品選択PopupFieldから対象部品へ移動できる。
5. ボタンの押下、タブ・選択肢、会員証の表裏、色変更、数値カウント、通知、バナーの再生を確認する。サンプルの操作は表示とイベントの確認用で、ゲームデータを変更しない。

Bootの`Bootstrap`には開発用の`showComponentGallery`（InspectorではShow Component Gallery）を用意している。EditorまたはDevelopment Buildで有効になり、通常のリリースビルドではGalleryルートを登録しない。M2a時点ではGallery以外の実画面ルートは未登録である。

`Imoni > Validate Component Gallery` は生成リソース、UXMLでの部品登録、35種類と列挙型バリエーションの収録、基本的な表示・イベントを検証する。結果は `Library/M2AValidation.json` に出力する。画面を目視する確認とあわせて使う。

## 2. モックとの対応・全バリエーション

基準は `mock/*.dc.html` のinline styleと `Assets/Imoni/UI/Theme.uss`。下表のセレクタは**HTMLモックを読むための目印**であり、USSとして移植するものではない。HTMLに固有IDがない箇所は表示文言・`sc-for`/`sc-if`の名前で特定する。

Galleryでは次の35種類を独立した見出しで並べ、列挙型の全値と主な状態を確認する。サイズ・任意の文言・金額など連続値を取る属性は代表値を表示する。

| # | 部品 | モック画面・該当する要素 | バリエーション・確認する状態 |
|---|---|---|---|
| 1 | `ImoniButton` | Tutorial1の「登録する」、Lobbyの「準備完了」「ワールド生成してスタート」「参加する」「招待コードをコピー」、Gameの`sc-for[list="{{actions}}"]`とパネル内の小ボタン | `Primary / Danger / Yellow / Outline / Small / Icon / Action`。通常・disabled、アイコン、Actionの選択、押下時の沈み |
| 2 | `Card` | Lobbyの`section[aria-labelledby="create-h"]`・`join-h`・`mem`、Tutorial1の会員証と二つ名説明枠 | `Cream / White / Dark / Dashed / Shadow`。見出し帯あり・なし、子要素スロット |
| 3 | `ScreenHeader` | Lobbyの`a[aria-label="ホームに戻る"]`と「ロビー」、Waiting上部「キャンセル」と右側シード表示 | タイトル、戻る文言、`RightContent`の有無、戻る要求イベント |
| 4 | `StepTitle` | Tutorial1の「会員登録しよう! (1/3)」、Tutorial2の段番号と複数行見出し | 横並び・`Stacked`、`Step / Count / Title` |
| 5 | `PagerDots` | Tutorial1〜3の下部、8pxの丸と24pxの選択マーカー | `Count`、先頭・中間・末尾の`Index` |
| 6 | `TabBar` | Home・Ranking・Memberの`nav`内「ホーム／ランキング／会員証」 | `Selected = Home / Ranking / Member`。先頭には`tabbar__item--first` |
| 7 | `SegmentedControl` | Rankingの`[role="tablist"][aria-label="モード"]`・`[aria-label="部門"]`、Lobbyの`section[aria-labelledby="mode"]` | `Pill / Tabs / Seed`。選択・非選択、変更イベント |
| 8 | `TitleChips` | Homeヘッダーの二つ名、Member会員証内、Tutorial1の「二つ名」欄 | `Compact / Regular / Large`（11 / 12 / 13px）、前半は緑・後半はえんじ |
| 9 | `TitlePartPicker` | Memberの`section[aria-labelledby="title-edit"]`、`sc-for[list="{{firsts}}"]`・`{{seconds}}` | 前半・後半、所持・選択中・未取得`???`。未取得は押せない |
| 10 | `Avatar` | Homeの48px丸枠、Lobby・Waitingの44px丸枠、Memberのキャラクタープレビュー | サイズ、はちまき4色・服4色、Photo指定、`Dimmed`。配布された4レイヤーを合成 |
| 11 | `AvatarEditor` | Memberの`section[aria-labelledby="chara-edit"]` | 110×120pxプレビュー、はちまき・服の各4色、選択変更 |
| 12 | `ColorSwatch` | Memberの`sc-for[list="{{bands}}"]`・`{{outfits}}`内44pxボタン | 通常・`Selected`。選択時はクリームと緑の二重輪 |
| 13 | `MemberCard` | Memberの`button[aria-label="{{flipLabel}}"]`、Tutorial1の「リモート芋煮会 会員証」枠 | 表・`BackFacing`・`EditableName`、名前入力、写真選択要求 |
| 14 | `BadgeGrid` | Member会員証裏の`sc-for[list="{{badges}}"]` | 取得済み黄色・未取得`???`。3列の配置 |
| 15 | `MemberRow` | Lobbyの`section[aria-labelledby="mem"]`、Waitingの`section[aria-labelledby="slot-h"]`内の参加済み行、Gameの`section[aria-label="メニュー"]` | Avatar・TitleChips・名前・StatusBadge。通常・切断中表示 |
| 16 | `StatusBadge` | Lobbyの`{{m.status}}`、Waitingの`{{s.tag}}`、Gameメニューのメンバー状態 | `Ready / Waiting / Host / Joined / Offline / Collecting`。「準備OK／準備中／ホスト／参加!／切断中／採集中」 |
| 17 | `InviteCodeCard` | Lobbyの`sc-if[value="{{inTeam}}"]`内の焦げ茶コード帯、Waitingの`section[aria-labelledby="code-h"]` | `Compact / Large`。通常・コピー完了表示、コピー・共有要求 |
| 18 | `StatCard` | Homeの「最高到達／通常クリア」、Resultの「獲得スコア」欄、GameOverの「今回の到達／累積スコア」 | `Home / Result / GameOver`。`Label / Value` |
| 19 | `ProgressBar` | Mainの`[role="progressbar"][aria-label="読み込み中"]`、Loadingの`[aria-label="読み込み"]`、Gameの必須素材、Result・GameOverの素材行、Imoniスコア下ゲージ | `Thick / Thin / Rainbow`。0・途中・100%、`FillColor`。丸い外枠で平坦な塗りをクリップ |
| 20 | `ChecklistStep` | Loadingの`section[aria-label="進み具合"]`内`sc-for[list="{{steps}}"]` | `Done / Active / Pending`。チェック・回転中・待機、ラベルと補足 |
| 21 | `QualityChip` | Tutorial3の`sc-for[list="{{qualities}}"]`、Game倉庫行の品質名 | `normal / good / rare / legend / myth / transcend / ultimate`の7品質。小型・`ShowMultiplier`表示 |
| 22 | `MaterialGoalChip` | Game HUDの`sc-for[list="{{reqs}}"]`、Resultの`section[aria-labelledby="req-h"]`、GameOverの`section[aria-labelledby="miss"]` | `Chip / Row / FailureRow`。未達成・達成・不足表示。判定済みのCoreモデルをBind |
| 23 | `BonusTierChips` | Resultの`section[aria-labelledby="bonus-h"]`内`sc-for[list="{{stages}}"]`、Gameのボーナス表示 | 未到達・到達・現在段階。100%×1／150%×2／200%×4／300%×8はConfigから取得 |
| 24 | `BigNumberLabel` | Imoniヘッダーの`{{scoreText}}`、Resultの`{{score}}`、Rankingの`{{r.score}}`、Gameの数量表示 | 静止表示・`CountTo`。万・億・兆を含む大きな整数の表示 |
| 25 | `FundsPill` | Game HUD上部のコインアイコンと共有資金 | コイン＋金額＋通貨単位。小額・大きな金額 |
| 26 | `BottomSheet` | Gameの`section[aria-label="倉庫"]`・`ショップ`・`個人強化`・`メニュー` | `IsOpen`の開閉、任意の資金表示、子要素スロット。Gallery用の`Inline` |
| 27 | `InventoryRow` | Gameの`section[aria-label="倉庫"]`内`sc-for[list="{{rows}}"]` | 通常・選択中、素材／品質／所持／保護／売却可能／単価 |
| 28 | `ShopRow` | Gameの`section[aria-label="ショップ"]`内`sc-for[list="{{shopItems}}"]` | 購入選択肢の有効・無効、数量と価格。押すと選択肢IDを通知 |
| 29 | `UpgradeRow` | Gameの`section[aria-label="個人強化"]`内`sc-for[list="{{ups}}"]` | 購入可能・不可能、名前／Lv／現在効果／次効果／価格 |
| 30 | `RankingRow` | Rankingの`sc-for[list="{{rows}}"]` | `First / Podium / Normal`、`Alternating`。順位・チーム・補足・スコア |
| 31 | `Toast` | Gameの`sc-if[value="{{hasToast}}"]`、マップ下部の履歴通知 | `Show(text, seconds)`で表示、指定時間後に非表示 |
| 32 | `ErrorMessage` | Lobbyの`p[role="alert"]` | エラー文言あり・空。えんじ枠、薄赤背景 |
| 33 | `AnimatedPot` | Home・Tutorial2の焚き火鍋、Waiting上部の64×58px鍋、Loadingの大きな鍋 | `Campfire / Small / Large`、`Steam`・`Sway`の有無 |
| 34 | `PatternBackground` | Mainの`pattern#taro`、Loadingの`pattern#taroL` | `Yellow / Transparent`。配布された里芋柄画像を96×104pxで繰り返す |
| 35 | `ResultBanner` | Imoniの`sc-if[value="{{showBanner}}"]`・`{{finale}}`・`{{failFinale}}`。ImoniFailはImoniを失敗設定で読み込む | `LevelUp / Success / Fail`。縁取り文字、サブタイトル・不足素材表示、`Play()`による出現 |

### 再利用時の表示上の扱い

- 色・フォントの共通値はThemeを利用し、部品別の寸法はモックの値に合わせたUSSへ集約する。`Dashed`の点線はTheme／UNITY_GUIDEの方針に従う実線代替。影は太い下辺、選択輪は重ねた要素で表現する。
- MemberRowはCOMPONENTSの指定に従い、モックの緑の一行二つ名を共有TitleChipsで組む。Avatarはモックの単色プレースホルダーの代わりに指定の4レイヤーを使用する。
- Rainbowは `Art/Sprites/Effect/fx_rainbow_scroll` の画像を使用する。USSのグラデーションは使わない。M5の演出全体・鍋や光の画面座標・成功失敗の時間制御はM2aの対象外である。
- 禁止構文（構造擬似クラス、gap、grid、box-shadow、グラデーション）は使わない。間隔は明示クラスとmargin、複数列はFlexで配置する。

## 3. UI BuilderとUXMLから使う

35部品はすべて `Imoni.Game.UI.Components` 名前空間の `[UxmlElement]` カスタムコントロール。Unityで再コンパイル後、UI BuilderのLibraryからプロジェクトのカスタムコントロールを選べる。公開属性は `[UxmlAttribute]` で表示する。Setupを先に実行し、ThemeのFontAssetと画像参照を生成しておく。

```xml
<ui:UXML xmlns:ui="UnityEngine.UIElements"
         xmlns:imoni="Imoni.Game.UI.Components">
    <imoni:Card variant="Cream" header="表示サンプル">
        <imoni:TitleChips first="芋煮会の" second="初心者" size="Regular" />
        <imoni:Avatar size="48" band-color="#8B2310" outfit-color="#FFF3D6" />
        <imoni:StatusBadge state="Ready" />
        <imoni:ImoniButton name="next" variant="Primary" text="次へ" />
    </imoni:Card>
</ui:UXML>
```

部品は `UiTheme.Apply` でThemeと `Resources/UI/Components/` の対応USSを読み込む。UXMLには部品の組み合わせを置き、内部のラベル・枠・アイコンを画面ごとに複製しない。`Card`と`BottomSheet`に追加した子要素は内容スロットへ入る。`ScreenHeader`の右側は `RightContent` に追加する。

一覧・プロフィールなど複合データは `Bind(モデル)` で渡す。UXML属性の `value/current/required` で整数を表すものは文字列として受け取り、数量をfloatへ変換しない。

```csharp
materialGoalChip.Bind(DisplayProjection.Goal(config, "wood", current, required));
bonusTierChips.Bind(DisplayProjection.BonusTiers(config, current, required));
bigNumberLabel.Bind(score);
bigNumberLabel.CountTo(nextScore); // 既定時間はGameConfigのui.countUpSeconds

shopRow.BuyRequested += optionId => presenter.RequestPurchase(optionId);
memberCard.NameChanged += name => presenter.OnNameEdited(name);
```

進捗・達成判定・段階・巨大整数の補間は純C#の `Core.DisplayProjection` に集約する。倉庫行の保護数量、購入可能かどうか、品質・獲得条件なども計算済みモデルを渡す。部品は表示と入力意図の通知を担当する。クリップボードや共有APIは `InviteCodeCard.CopyRequested / ShareRequested` を受けた画面側で実行し、コピー成功後に `ShowCopied()` を呼ぶ。

はちまきの選択色は赤 `#8B2310`、緑 `#34421C`、青 `#2F5D8A`、白 `#FFFFFF`。服はきなり `#FFF3D6`、わかくさ `#6B9B3A`、つち `#C9A27A`、そら `#8FAFC4`。AvatarEditorは選択表示と変更イベントまでを担当し、永続化しない。

## 4. ScreenRouterとSafe Area

`Bootstrap`はUIDocumentの下に `SafeArea > ScreenHost` を作り、ScreenRouterをScreenHostに接続する。`Register(name, factory)` または `RegisterResource(name, resourcePath)` で登録し、`Show(name)` で表示を置き換える。未登録のルートは `false` を返して現在の表示を維持する。表示中の画面が `IDisposable` の場合は切り替え・終了時に破棄する。

TabBarはルート名を `NavigateRequested` で通知する。M2b以降は登録済み画面に対して `tabBar.NavigateRequested += route => router.Show(route);` を接続する。Gallery内のTabBarは表示・選択イベントの見本として扱う。

SafeAreaAdapterは `Screen.safeArea` の物理ピクセルを `RuntimePanelUtils.ScreenToPanel` でパネル座標に変換し、上下の原点差を補正してSafeArea要素のpaddingへ反映する。画面のGeometryChangedとConfig指定間隔で再計算する。PanelSettingsはConfigの390×844基準とMatch値を使用する。

## 5. 検証記録

2026-11-25、Unity 6000.3.21f1／Gameビュー390×844で確認。

| 確認 | 結果 |
|---|---|
| Unity再コンパイル・USS/UXMLインポート | エラー0・警告0。4部品USS＋GalleryLayout／UXMLを実インポート |
| `Imoni > Setup Project` とBoot起動 | 成功。生成アセットを保存し、BootからGalleryを表示 |
| `Imoni > Validate Component Gallery` | 成功：35種類・列挙型属性52値、リソース参照、先頭タブ、イベント、内容スロット、RouterとSafe Area |
| Gameビュー390×844のGallery目視 | 全35セクションを撮影・目視。色選択のはみ出し／pill端／バナー行間を修正し再確認 |
| `dotnet test tools/CoreTests` | 124件成功、失敗0、スキップ0（既存116＋追加8）。実行：`dotnet test tools/CoreTests --nologo` |
| 禁止USS構文の検査 | 非対応の構造擬似クラス・gap/grid・box-shadow・グラデーションの宣言なし |

今回追加した `DisplayProjectionTests` は8件：巨大数量の比率とConfig表示精度1件、進捗のゼロ／途中／一致／超過4件、巨大なボーナス閾値とConfig変更1件、増加・減少・両端を含む巨大整数補間1件、不正入力1件。既存M1のテストも継続して実行する。

Safe Areaの端末固有確認ではDevice Simulator等でノッチ・ホームインジケーターのある画面を選び、上下左右の安全領域の内側にGalleryが配置されることを確認する。
