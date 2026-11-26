# M8 数量・資金の表示

## 単位の組み合わせ

`Format.Big` は `GameConfig.format.unitBase` と `format.units` の階層に従い、上位の整数部分と、その**ひとつ下の階層の余り**を表示する。既定の表示グループ数は引き続き2。数量・資金・目標の保存値や計算には `BigInteger` を使い続け、表示で丸めた値を判定へ戻さない。

| 入力 | `Format.Big` | `Format.Compact` |
|---|---|---|
| 1,234 | 1,234 | 1,234 |
| 1,234,567,890 | 12億3,456万 | 12億 |
| 28,199,999,999 | 281億9,999万 | 281億 |
| 3,200,012,345,678 | 3兆2,000億 | 3兆 |
| 7804 × 10^28 + 8528 × 10^24 | 7,804穣8,528秭 | 7,804穣 |

`Format.Big` 内の「最上位の単位」と「各余りの単位」を明示的に分離し、単位と除数を同時に一段ずつ下げる。`format.units` に同名や空白の単位を設定すると、Config読み込み時に拒否する。

設定済みの最大単位（標準では無量大数）を超える値も、最上位の係数を `BigInteger` の整数として表示する。最大単位を下位の余りにも再使用したり、下位の単位へ循環させたりしない。指数表記や小数への変更は行わない。

## 実機報告の調査範囲

M8開始時のmain (`3c69e6f`) の `Format.Big` とConfigでは、上記の対象値は既に `7,804穣8,528秭` に分解され、同じ単位を2回返す現象は再現していない。Galaxy A53で報告された表示の元の整数値・画面画像は今回の作業資料に含まれないため、その表示の原因は確定していない。今回の変更は階層対応を明示し、全隣接単位の回帰テストとConfig検証を追加するもの。

フォントも確認した。Zen Maru Gothicには「秭」がないが、生成済みのfallback参照はDela Gothic Oneを指している。Delaの生成FontAssetでは「秭」(U+79ED) は glyph 5099、「穣」(U+7A63) は glyph 5139 で異なる。元TTFの描画でも別の字形だった。これをもって実機でのフォント描画異常がなかったと断定はしない。新APKの端末確認では、2単位表示の詳細欄と1単位表示のHUDを区別して確認する。

## 部品の再利用と表示選択（COMPONENTS補足）

| 場所・部品 | 表示 | 省略前の情報 |
|---|---|---|
| `MaterialGoalChip` の `Chip` | `CurrentCompactText` / `RequiredCompactText`、各最上位1単位 | ルートのtooltipに素材名と `ExactCountsText` |
| `MaterialGoalChip` の `Row` / `FailureRow` | 従来の `CurrentText` / `RequiredText`、設定グループ数 | 同じ正確なtooltip |
| `FundsPill` | 内部の `BigNumberLabel.Compact = true` | ルートのtooltipにカンマ付き省略なし資金＋通貨単位 |
| HUDボーナス数量 | `GameHudProjection` から1単位の `BonusText` | `BonusExactText` をtooltipへ結線 |
| HUDボーナス達成率・報酬倍率 | `Achievement(..., compact: true)` と `Format.Compact` | Coreの判定は正確な整数のまま |
| 通常の `BigNumberLabel`、結果・クリア等の詳細 | 既定は従来どおり `Format.Big` | `BigNumberLabel` は省略なし値をtooltipにも保持 |

`BigNumberLabel` に `[UxmlAttribute] Compact` を追加。画面側に別の数値表示部品を作らず、同じ部品の表示オプションとして利用する。`MaterialGoalChip` はCoreの `MaterialGoalView` から表示済み文字列・達成判定を受け取るだけで、UI側で目標の比較や単位計算をしない。

たとえば現在値28,199,999,998・目標28,199,999,999は、HUDでは両方 `281億` だが**未達成のまま**。正確な1差はtooltipに残し、進捗・達成色はCoreの結果を使う。`Format.Compact` は繰り上げず、小数を表示しない。

## 回帰テスト

既存の `IntegerDisplayTests.cs` / `GameHudProjectionTests.cs` に17ケース追加。

- 報告された穣→秭の組み合わせ（正負・Compact）。
- 全隣接単位の上下の対応（最終単位まで）。
- 10^1000の係数でも上位と下位が混ざらず、整数を保持。
- Compactの0・9,999・万/億の境界・281億・負数・兆（8ケース）。
- Configで単位基数・単位名・表示グループ数を変更した場合。
- 空文字・空白・重複の単位を拒否（3ケース）。
- 省略表示が同じでも1不足なら未達成、正確なtooltipを保持。
- HUDの必須・ボーナス・倍率の短縮と、倉庫内の正確な数量維持。

dotnet全件の実行結果およびUnity/Android確認は `docs/validation/m8/` の最終検証記録にまとめる。
