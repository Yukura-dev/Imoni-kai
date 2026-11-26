# M4 パネル

ブランチは `rebuild/m3b-world-view` の `5153ecb` から作成した `rebuild/m4-panels`。M5の終了演出には進まない。

## 開き方と操作

1. Play Modeを終了し、`Imoni > Setup Project` を実行する。
2. `Imoni > Open App` からHome → Lobby → Waiting → Loadingへ進み、「ラウンド1 スタート!」を押す。
3. Game下部の倉庫・ショップ・強化・メニューからパネルを開く。右上の×で閉じる。
4. 倉庫では素材×品質の行を選ぶ。「−／＋」で5ずつ保護、全部保護／保護解除で切り替える。割合は売却可能数を基準に計算する。数量欄も編集でき、売却可能数を超えた場合は売却を無効にする。
5. えんじの売却ボタンは確認なしで即時確定。「保護以外をまとめて売却」は全素材・全品質の保護を残す。
6. ショップは4素材を普通品質で×10／×50購入。強化は自分の4項目にだけ適用される。どちらも共有資金不足のボタンは無効。
7. メニューは待機・移動・採集・切断（再接続待ち）をCoreの状態から表示する。「ホームへ戻る(挑戦は中断)」で明示中断し、GameのWorldと入力を破棄する。

パネル中も時間とボットの採集は進む。マップへの新規入力とトーストを抑止し、閉じた直後に古いトーストを再表示しない。

## 部品とCoreの接続

| 内容 | 表示 | 確定・計算 |
|---|---|---|
| パネル枠 | 既存BottomSheet、倉庫620／店540／強化560／メニュー440px | GameConfig.ui.panels |
| 倉庫 | InventoryRow、QualityChip、InventorySelectionControls、OperationHistory | GamePanelProjection、SetProtection、Sell、SellAllUnprotected |
| ショップ | ShopRow、ImoniButton Small | GamePanelProjection.Shop、IGameServer.Buy |
| 強化 | UpgradeRow、ImoniButton Small | GamePanelProjection.Upgrades、IGameServer.Upgrade |
| メニュー | MemberRowの接続形式、ImoniButton Outline | GamePanelProjection.Members、IGameServer.AbortChallenge |

表示用の数量・可否・価格・履歴は純C#のCoreで生成する。数量と資金はBigIntegerで扱い、UIでfloatへ変換しない。コマンド直前に最新のSnapshotで再確認し、サーバーも保護・残高・接続・ラウンド状態を検証する。まとめ売却は一つのサーバー操作として原子的に確定する。

数値はGameConfigの `ui.panels`、既存economy/upgrades/materialsから読み込む。新しい部品USSもScreenHostで共有し、未読込時のみフォールバックする。

## M3bの小修正

成長札は `ui.worldView.growthLabelDistanceTiles`（2タイル）以内か、最後に選択した資源だけ表示する。遠方の再生中資源は芽画像にし、札はキャラクター・名札・採集バー・獲得ポップアップを避ける。

獲得ポップアップの連続数は、確定時のラウンドで最高品質を引き、連続が1以上のときだけ表示する。「連続0」は付けない。地形の境界とR1難易度はQUESTIONSへ後続メモを残した。

## 検証

`dotnet test tools/CoreTests --no-restore --verbosity minimal --logger "trx;LogFileName=dotnet-test.trx" --results-directory docs/validation/m4`

274件成功、失敗0、スキップ0。既存236件に、M4パネルCore28件・ポップアップ10件を追加。検証画像・連番・実行結果は `docs/validation/m4/`、追加したテスト名は同ディレクトリの `added-tests.txt` を参照する。

Unity 6000.3.21f1の再コンパイル・Setup・実行後にエラー0、警告0を確認した。パネル統合検証18項目と既存画面の回帰検証も成功。モック比較4枚、保護の連番6枚、資金不足の連番5枚、成長札・パネル中の時間進行の補足画像を保存した。実操作と各画像の対応は [検証記録](validation/m4/README.md) に記載する。

比較画像は左がモック、右が390×844のUnity。生成地形・Coreの実際の資金や数量・プレイヤー名はモックの固定値と異なる。Unityの検証セッションは通常のIGameServerコマンドで作り、画面用のダミー値は注入しない。

Editor専用の `ImoniM4ReviewJob` は `Library/M4Review.request` のJSONで実セッションの表示・状態を記録する。`tools/Invoke-M4Review.ps1` はその完了を待ち、`tools/Compare-M4.ps1` は比較画像を作る。`prepare`で検証用セッションを開始し、`open`／`click`／`input`は通常パネルのハンドラーを通す。無効ボタンは呼び出さない。保存プロフィールは書き換えない。
