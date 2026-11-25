# M3b 検証記録

Unity 6.3 LTS 6000.3.21f1、Gameビュー390×844、共通seed202647で確認した。

## テスト

```powershell
dotnet test tools/CoreTests --verbosity minimal --logger "trx;LogFileName=dotnet-test.trx" --results-directory docs/validation/m3b
```

236件成功、失敗0、スキップ0。M3aバランス調整で追加した11件に加え、M3bで25件追加した。M3bの内訳はHUD投影13件、地面移動11件、採集時の連続数を履歴に保持する回帰テスト1件。全名称は `added-tests.txt`、実行結果は `dotnet-test.trx`。

Unity再コンパイル・Setup・実操作後のConsoleはエラー0、警告0。`unity-console.json` の通常ログ2件はSetupの完了ログ。Setupを再実行してもワールドの4プレハブ・TileSet・WorldRuntimeAssetsのハッシュが変わらないことを `setup-idempotence.json` に記録した。既存M2bの画面遷移・保存・部品検証も成功（`ui-regression.json`）。

## 比較画像

| ファイル | 状態 |
|---|---|
| compare_30_Game_start.png | 4:35、必須3素材と公開前ボーナス |
| compare_31_Game_collecting.png | 4:57、資源選択・採集バー・品質つき獲得表示 |
| compare_33_GameMid_bonus.png | 2:10、魚18/19・達成率94%、player3切断、成長中表示 |

左はモックを390×844に縮小、右は同解像度のUnity実キャプチャ。`unity_*.png` は原画像、`state_*.json` は同時点のCore状態。地形・資源座標・目標数量・採集結果は実際の生成結果であり、モックの固定値とは異なる。上200px、下84px、タイル32px、フォント、色、共通部品と状態表示を比較した。専用画像がない地形・魚・山菜は既存画像を再利用する（QUESTIONS参照）。

## タップから倉庫の増加まで

`sequence_00.png`〜`sequence_07.png` は実際のGameビューで薪をタップして開始した8枚。撮影時だけ自動Tickを停止し、実時間でも約1秒ずつ待ちながらCoreを正確に1秒ずつ進めた。通常のIGameServer命令を通し、在庫や採集結果は直接変更していない。

| 経過秒 | 行動 | 自分の採集確定回数 | 対象の残り在庫 |
|---:|---|---:|---:|
| 0 | 移動 | 0 | 3 |
| 1 | 移動 | 0 | 3 |
| 2 | 採集 | 0 | 3 |
| 3 | 採集 | 1 | 2 |
| 4 | 採集 | 1 | 2 |
| 5 | 採集 | 2 | 1 |
| 6 | 待機・切り株 | 3 | 0 |
| 7 | 待機・成長中 | 3 | 0 |

自分の薪が合計9個増加し、品質換算のHUD数量は24。CoreのLots/Historyと対象の在庫を各JSONで確認できる。倉庫パネル自体はM4のため未実装。`sequence.json` は移動・採集中・採集確定をすべて検出している。成長ラベルのキャラクターとの重なり、ラベル同士の重なりは記録した全フレームで0。

通常の実時間操作でも地面タップで(32,27)から(34,31)へ到着し、追加採集なしでIdleになった（`ground_move.json`）。この操作確認は成長ラベルの表示修正前に記録した。Loadingの「ラウンド1 スタート!」は実クリックでGameへ遷移した。

`timeup.png/json` は300秒でGameに留まり0:00になる確認。結果演出・次ラウンド遷移はM5で接続する。パネル類（M4）には進んでいない。

画像の再配置は `tools/Compare-M3B.ps1`、記録用Editor APIと手動確認手順は `docs/M3B_WORLD_VIEW.md` を参照。
