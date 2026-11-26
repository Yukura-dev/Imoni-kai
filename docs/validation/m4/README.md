# M4 検証記録（2026-11-26）

Unity 6000.3.21f1、Gameビュー390×844。ブランチは `rebuild/m4-panels`（M3bの5153ecbから作成）。起動・操作手順は [M4_PANELS.md](../../M4_PANELS.md)。

## 結果

- `dotnet test tools/CoreTests --no-restore --verbosity minimal --logger "trx;LogFileName=dotnet-test.trx" --results-directory docs/validation/m4`：274件成功、失敗0、スキップ0。新規38件の名前は `added-tests.txt`、実行記録は `dotnet-test.trx`。
- Unityの再コンパイル、Setup、Play Modeでのパネル表示・操作後：エラー0、警告0。
- パネル統合検証18項目成功：`panel-integration.json`。保護±5・全部保護/解除、4割合、数量指定、即売却、複数行の一括売却、履歴3件、強化対象、継続Tick、トースト抑止、入力復帰、Home遷移を確認。
- 既存画面の回帰検証成功：`ui-regression.json`。既存7遷移フロー、UXML読込、ルートでのUSS共有（部品の個別追加0）を確認。
- 各PNGと同名のJSONには、その時点のCore在庫・資金・ボタン可否・履歴・Console件数を記録。

## モック比較

左がモック、右がUnity。比較画像は780×844、Unity原本は390×844。元モックを390×844へ縮小し、Unityの画像自体は加工していない。

| ファイル | 比較した内容 |
|---|---|
| `compare_32_Game_shop.png` | ショップ4素材、×10/×50、価格、共有資金、BottomSheet |
| `compare_34_GameMid_menu.png` | メンバー、採集状態、切断中・再接続待ち、Homeへ戻る |
| `compare_35_GameWarehouse.png` | 素材×品質、列、選択、保護操作、割合、数量入力、売却、履歴 |
| `compare_36_GameUpgrade.png` | 4強化、Lv、現在→次、価格、共有資金 |

生成マップ・目標・在庫・資金・プレイヤー名は実Coreの状態なので、モックの固定値とは異なる。検証のための値注入は行わず、通常の購入・保護・Tick・接続変更コマンドで状態を作った。レイアウト、フォント、色、部品構成を目視確認した。

## 保護の連番

各操作段階で撮影。購入・保護増加・売却および無効な売却ボタンはWindows上のGameビューで実際にクリックした。行選択・数量欄変更・100%ボタンは同じUIコールバック経由で操作した。

| 画像 | 操作と結果 |
|---|---|
| `proof_protection_00.png` | 薪50個購入後。所持50、保護0、資金700G |
| `proof_protection_01.png` | ＋をクリック。保護5、売却可45 |
| `proof_protection_02.png` | 数量46を入力。超過表示、売却ボタン無効 |
| `proof_protection_03.png` | 無効ボタンをクリック。在庫・保護・資金・履歴が変わらない |
| `proof_protection_04.png` | 100%を選択。保護を除いた45個になる |
| `proof_protection_05.png` | 45個を即売却。所持5、保護5、売却可0、資金790G。一括売却も無効 |

## 資金不足の連番

購入・無効購入・無効強化ボタンをGameビューで実際にクリックした。

| 画像 | 操作と結果 |
|---|---|
| `proof_funds_00.png` | 初期資金1000G |
| `proof_funds_01.png` | キノコ×10を900Gで購入。残高100G |
| `proof_funds_02.png` | 無効な里芋×10（300G）をクリック。在庫・残高・履歴が変わらない |
| `proof_funds_03.png` | 強化を開く。4項目とも150G、全ボタン無効 |
| `proof_funds_04.png` | 無効な移動強化をクリック。全員のLvと100Gの残高が変わらない |

## 継続動作・M3b修正

- `proof_panel_running.png`：倉庫を開いたまま実時間で116.696秒経過し、ボットの採集履歴が増加。マップ入力・トーストは無効。
- `proof_panel_closed.png`：閉じるとマップ入力が復帰。抑止していた古いトーストは再表示されない。
- `proof_growth_labels.png`：再生中の遠方資源は芽アイコンのみ。距離は `ui.worldView.growthLabelDistanceTiles=2`。成長札は近距離または選択対象に限定し、名札・ポップアップを避ける。
- 最高品質かつ連続1以上だけに連続数を付ける条件は、ラウンドごとの最高品質の違いも含め10件の追加テストで検証。

## フォントの再読込確認

長時間の検証中、動的フォントのNativeFormatImporter整合性警告が1回発生した。Playを停止してSetup・再import後に再実行し、0件を確認した。その後、生成フォント3ファイルをコミット済みの元データに戻して再importし、Setup→起動→全4パネル表示→購入/保護→実時間Tickを再確認した（`proof_font_baseline.png/json`、エラー0・警告0）。フォント設定・フォント生成物の変更や警告抑制はコミットに含めていない。

## 再実行用ツール

`ImoniM4ReviewJob` はEditor専用。`tools/Invoke-M4Review.ps1` が `Library/M4Review.request` の処理完了を待つ。`tools/Validate-M4Panels.ps1` は通常のパネルコールバックとIGameServerを用いた18項目の統合検証。`tools/Compare-M4.ps1` は撮影済み原本を左右に並べる。検証セッションは通常プロフィールの保存内容を書き換えない。

M5のタイムアップ演出・結果画面は今回の対象外。地形境界とR1の難易度調整は `QUESTIONS.md` にメモのみ残した。
