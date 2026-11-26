# M7 仕上げ

M7の実装を統合し、Coreは **410件成功・失敗0・スキップ0**。iPhone 12／OnePlus 6Tの全80状態、390×844の比較画像32枚、MainからR2への無音MP4を検証した。最終画面のConsoleはエラー0・警告0。**Android APKの生成も成功（エラー0・警告0）**。物理端末での起動・性能確認は行っていない。

作業先は `D:/UnityProjects/Imoni-kai`、ブランチは `rebuild/m7-polish`。開始時にmainを `git pull --ff-only` した時点のorigin/mainはM5の `7d7e037` だったため、mainから作業ブランチを作成し、承認済みM6 `ad965ae` をfast-forwardで取り込んでいる。mainは変更・pushしていない。この経緯とM7の区切りはAGENTS.mdにも記載した。

## 実装した内容

| 対象 | 最終実装の概要 | 詳細 |
|---|---|---|
| 目標数量 | 現在の共有資金の50%で購入可能な見込み強化を、人間メンバーへ実価格で仮配分。見込みLvを目標と達成可能な供給の両方に反映し、実資金・Lv・履歴を変更しない | [バランス記録](validation/m7/balance.md) |
| 実績・保存 | 二つ名全種類と6バッジの条件をConfig化。本人の採集・到達・成功から付与し、結果再表示で再加算しない。旧プロフィールの所有/選択も移行 | [条件一覧](M7_PROGRESSION.md) |
| ローカルランキング | 通常/エンドレス×共通/自由シードの4部門。通常R5成功と、エンドレスR6以降の成功結果から明示終了した時だけ保存。失敗・途中中断は新規登録しない | [登録条件と保存](M7_PROGRESSION.md#ランキング) |
| 未作成画面 | 端末内プロフィールを使うLogin、ClearからR6へ引き継ぐEndless移行を既存UI部品で追加。通常Clear・音量設定を継続利用 | [操作手順](M7_PROGRESSION.md#補完した画面と操作) |
| 地形境界 | 森林と未開放領域で共通のマスク描画を使い、既存タイル画像の角と縁をなじませる。Coreの地形・通行・橋・資源は維持 | [表示の仕上げ](M7_PRESENTATION.md) |
| 再接続 | 自分の切断中は再接続オーバーレイ、半透明表示、操作抑止。チームの時間と仲間は継続。開発用デモは3秒後に同じ状態へ復帰 | [再接続APIと確認](M7_PRESENTATION.md#再接続の表示と確認入口) |
| 見出し・注記 | 成功/失敗演出の提供フォントを維持して前景の太さを補正。Lobbyやショップの開発用注記を除去 | [表示の仕上げ](M7_PRESENTATION.md) |
| Safe Area | `UnityEngine.Device.Screen.safeArea` を使い、Simulatorにも対応。既存デザインの上/下余白を先に消費し、ノッチ用余白との二重加算を避ける。ロビー／ランキングの先頭描画欠けと会員証の圧縮も修正 | [最終画面レビュー](validation/m7/visual-review.md) |
| フォント | 全4字体の既知UI文字をSetupで事前生成し、未知名向けDynamicを保持。「秭」は提供Delaへフォールバック。ビルド時も事前生成データを維持 | [フォント対策](M7_PRESENTATION.md) |
| Android | Setupに縦画面、IL2CPP/ARM64、アプリ識別子、SDK値、既存鍋素材からの仮アイコン生成を追加。APK用のEditorメニューを用意 | [ビルド成功記録](validation/m7/android-build.json) |

UIの判定・計算はCoreに置き、画面は既存のCard、ImoniButton、StatCard、MemberCard、RankingListなどへ値を渡す。詳細は [COMPONENTS.md](COMPONENTS.md)。音量設定はM6の保存/AudioMixer経路を継続使用する。

## バランス検証

```text
dotnet run --project tools/BalanceReview -- . docs/validation/m7
```

共通シード202647、同じ通常IGameServer操作で3方針を比較した。強化ありのQAは各ラウンド開始後に資金の50%以内で購入する。

| QAの方針 | M7の結果 |
|---|---|
| 無強化 | R1/R2成功、R3失敗 |
| 各項目+1Lv/Rまでの計画的強化 | R1〜R3成功、R4失敗 |
| 予算内で回数制限なしの強化 | R1〜R5成功。R5の必須達成106.602022秒 |

M6で同じ予算内無制限試行のR5が12.478390秒だったところを、約1分46秒へ改善した。共有資金が増えても目標が購入前Lvだけを参照し、投資直後に簡単になる問題へ対応している。投資しない方針や品質の下振れでも必勝になる保証ではなく、失敗した試行もそのまま保存した。採集式・連続倍率・価格・報酬・強化上限は変更していない。全素材の目標、期待供給、見込み/実購入Lv、正確な到達時刻は [balance.json](validation/m7/balance.json) にある。

## 手動でたどる手順

1. Unity Hubから正本を開き、Playを止めて `Imoni > Setup Project`。`Assets/Imoni/Scenes/Boot.unity` を使い、`Imoni > Open App` でMainから開始する。
2. Main→Login→初回Tutorial1〜3→Home。登録済みLoginは「この会員で続ける」でHomeへ進む。端末内プロフィールである説明を確認する。
3. Home右上の設定で音ON/OFFと音量を変更する。「会員プロフィール」からLoginへ戻れる。Memberで二つ名・配色と会員証裏のバッジを確認し、再起動後も保存値が残ることを確認する。
4. Home「プレイ開始」→Lobbyでシード選択→チーム作成→Waitingのボット参加→Loading→Game。資源タップ→移動→採集と、4パネルの通常操作を確認する。森林・未開放境界の見た目を確認する。
5. ラウンド成功→TimeUp→演出→Result。「ラウンド2へ」で同じワールドを引き継ぐ。同時に二つ名を複数獲得した欄は先頭1件と「ほかn件」の2行になり、下の操作が画面内に残ることを確認する。
6. 通常R5成功→Clearで通常記録が保存される。「エンドレスへ」→引継ぎ確認→R6。成功結果で継続するか「記録してホームへ」を選べる。Homeから新規エンドレスを選んだ場合は初期状態のR1から開始する。
7. Rankingのモードとシードを切り替え、実記録と空状態を確認する。失敗した挑戦は新規登録されず、過去の記録は残る。エンドレスの再挑戦は同じモードで新シードR1へ戻る。
8. Editor/DevelopmentのGameで `BeginReconnectDemo()` を実行し、自分の表示と操作が止まる間もタイマーと仲間が続くこと、3秒後に同じワールド・資金・倉庫・強化で戻ることを確認する。仲間の確認には `BeginReconnectDemo("player2")` を使う。

短縮して確認する場合だけ `Imoni > Development > Short Rounds (GameConfig)` を使う。ゲーム時計は10倍、演出時計は実時間のまま。通常Releaseは短縮しない。

## Simulator・Android・録画

実際のDevice Simulatorで **iPhone 12** と **OnePlus 6T** を選択し、各40状態、計80状態を保存した。全JSONでviewInsideSafeArea=true、outsideButtons空、Consoleエラー0・警告0。Simulatorは端末画面形状の確認であり、物理端末での性能／起動確認とは区別する。

Home、Tutorial、Game、パネル、Result/Clear、GameOver、Login/Endlessに加え、ロビーの各状態、ランキング4部門、再接続、設定を確認した。Resultで二つ名4件を同時獲得した欄は先頭1件＋「ほか3件」の2行へ収まり、R2ボタンが見える。会員証の色選択は44pxを維持して全8色が収まる。ロビー／ランキングは矩形が正常でも先頭が描画されなかったため、該当ScrollViewのGroupTransform最適化だけを外し、標準のスクロールとクリップを保持して再確認した。長いロビーのチーム画面は下へスクロールして操作する。画像と範囲は [visual-review.md](validation/m7/visual-review.md) に記録した。

Androidの手順は、Playを止めて `Imoni > Build > Android APK`。Setupと `BuildPipeline.BuildPlayer` を使い、Bootのみを含む `Builds/Android/Imoni.apk` を生成する。設定元は `setup.android` で、アプリID `jp.yukura.imoni`、minimum API26、target API36、IL2CPP/ARM64、縦画面。仮アイコンは提供 `pot_campfire` をEditor APIで合成する。ロビースクロール修正を含む最終再ビルドは **Succeeded、エラー0・警告0、32.344秒**。実APKは **62,699,410 bytes（約59.80 MiB）**で、存在とSHA-256を [android-build.json](validation/m7/android-build.json) に記録した。同JSONの`bytes`はBuildReportの集計値で、実APKのサイズは`apkBytes`を使う。APKはローカルのビルド成果物であり、実機へのインストール／起動は未検証。

通し録画は `Imoni > Development > Record Main to Round 2`。隔離プロフィールで通常のUIボタンをたどり、実際のBootstrap.TickとIGameServer操作でR2へ進む。**Gameのみ10倍速、演出・Waiting・Loadingは実時間**。記録した実時計は77.9913535秒・元画像1936枚で、実フレーム間隔を維持して合成したMP4は78.12秒・1953フレーム・25fps。全編デコード、24時点の抽出画像、遷移と通常操作ログを照合し、黒画面中断やResultからHomeへ戻る誤遷移は見つからなかった。最後はR2で、録画時Consoleは0/0。**音声ストリームはなく、この動画で効果音の内容や同期は検証していない**。詳しい確認範囲は [flow-review.md](validation/m7/flow-review.md)。

## テスト・証跡一覧

```text
dotnet test tools/CoreTests --logger "trx;LogFileName=m7.trx" --results-directory docs/validation/m7/TestResults
```

記録された結果は **410件成功 / 失敗0 / スキップ0**。二つ名・ランキング23ケース、見込み強化15ケース、再接続とエンドレス表示/演出経路などを追加している。テストファイル上書きの通知はTRX出力先に関するもので、Unity Consoleの警告件数ではない。

| 証跡 | 状態・内容 |
|---|---|
| [dotnet-test.txt](validation/m7/dotnet-test.txt) | 記録済み。全410件のコンソール結果 |
| [TestResults/m7.trx](validation/m7/TestResults/m7.trx) | 記録済み。テスト名ごとの結果 |
| [balance.md](validation/m7/balance.md) / [balance.json](validation/m7/balance.json) | 記録済み。3方針、目標/供給/見込みLv/実購入/到達時刻 |
| [title_to_round2.mp4](validation/m7/title_to_round2.mp4) / [flow-review.md](validation/m7/flow-review.md) | 390×844・78.12秒の無音動画。全編デコードと24代表時点の目視／遷移照合を実施 |
| [flow-recording.json](validation/m7/flow-recording.json) | 録画のUIクリック、画面遷移、元1936画像の実時刻、通常IGameServer操作。R2到着、録画時Consoleはエラー0/警告0 |
| [画面レビュー](validation/m7/visual-review.md) / [検証資料の索引](validation/m7/README.md) | `compare_*.png` は左=参照、右=Unity。Clearの左だけM5のUnity画像。新規Login/Endlessは単独証跡 |
| [iPhone 12一覧](validation/m7/simulator/contact_iPhone12.png) / [OnePlus 6T一覧](validation/m7/simulator/contact_OnePlus6T.png) / [座標監査](validation/m7/safe-area-audit.json) | 各40状態。範囲外0、Consoleエラー0/警告0。目視3件とスクロール1件を修正・再撮影済み |
| [ロビー下端](validation/m7/proof_Lobby_scrolled.png) / [実測レイアウト](validation/m7/proof_Lobby_scrolled_layout.json) / [取消後](validation/m7/proof_Lobby_cancelled.png) / [Loading遷移](validation/m7/proof_Lobby_to_Loading.png) | 実スワイプで下部2ボタン全体が見える。取消→再準備→開始の通常ハンドラも確認。OSクリック成功の証明とは区別 |
| [Setup冪等性](validation/m7/setup-idempotence.json) | 無変更で2回目のSetupを実行した際、4字体のFontAssetハッシュが不変 |
| `autoplay_R1.json`〜`autoplay_R6.json` / [保存プロフィール](validation/m7/proof_saved_profile.json) | Unityでの通常サーバー操作によるR1〜R6成功と、実績／通常・Endless記録の保存 |
| [再接続前](validation/m7/reconnect_before.json) / [切断中](validation/m7/reconnect_during.json) / [復帰後](validation/m7/reconnect_after.json) | 同じワールドに復帰し、切断中も仲間が進行する実時間の検証 |
| [Androidビルドレポート](validation/m7/android-build.json) | Succeeded、エラー0/警告0、実APK 62,699,410 bytes。IL2CPP/ARM64・Portrait・API26→36・SHA-256を記録 |
| [最終Unity Console](validation/m7/unity-final-console.json) | 最終画面の再コンパイル・再撮影後、エラー0/警告0。Androidの最終ビルド結果は別記録 |

各実装の判断は [QUESTIONS.md](QUESTIONS.md) と [DECISIONS.md](DECISIONS.md) のM7節、保存条件の全一覧は [M7_PROGRESSION.md](M7_PROGRESSION.md)、描画・再接続・録画の実装詳細は [M7_PRESENTATION.md](M7_PRESENTATION.md) を参照する。M7の成果物を `rebuild/m7-polish` へコミット・pushし、このマイルストーンで停止する。
