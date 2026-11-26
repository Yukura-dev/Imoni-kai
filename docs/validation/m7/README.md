# M7 検証資料の索引

このフォルダはCoreの自動検証、実際のサーバー操作、Unityの画面撮影を分けて保存する。数値のモック固定値への差し替えは行わない。2026-11-26の最終表示修正、410件の再テスト、Android再ビルドを反映した索引。

## Core・バランス

- [dotnet-test.txt](dotnet-test.txt) / [TestResults/m7.trx](TestResults/m7.trx)：410件成功、失敗0、スキップ0。M6の368件から42件増加。見込み強化15件、進行・ランキング23件、再接続1件、Endless HUD1件、演出ルート2件の純増。
- [balance.md](balance.md) / [balance.json](balance.json)：無強化・各項目+1Lv/R・予算内無制限投資の3試行。シード202647。実績から求めた正確な目標到達時刻、開始資金、見込み購入、実購入、期待供給を保存。予算内無制限投資のR5必須達成はM6の12.478390秒から106.602022秒へ。
- `autoplay_R1.json`〜`autoplay_R6.json`：Unity側の通常IGameServer操作による連続プレイ。Coreの予測表とは別の実測資料。
- `proof_saved_profile.json`：獲得パーツ、バッジ、到達記録、端末内ランキングを含む検証用プロフィール。通常プレイヤーの保存を置き換えるものではない。

再実行：

```powershell
dotnet test tools/CoreTests
dotnet run --project tools/BalanceReview -- . docs/validation/m7
```

## 画面・セーフエリア

- `compare_*.png`：32枚。左が対応モック、右がUnityの390×844表示。`compare_Clear.png`の左だけは専用モックがないためM5のUnity通常クリア画像。
- `unity_*.png/json`：390×844の単独撮影とその状態・Console記録。
- `simulator/iPhone12/` / `simulator/OnePlus6T/`：端末形状ごとの各40状態。対応JSONは合計80件で、セーフエリア外ボタン0、Consoleエラー0・警告0を確認した。
- [safe-area-audit.json](safe-area-audit.json)：矩形監査。矩形が範囲内でもScrollView内部や色選択チップの可視性までは証明しないため、[visual-review.md](visual-review.md)の目視結果を併用する。
- `proof_Login*` / `proof_Endless*`：専用の同名モックがない追加画面・Endless遷移の証跡。
- `reconnect_before/during/after.json`と`proof_Reconnect*`：一時切断中の仲間の継続、同じワールド・資金・強化・倉庫への復帰。
- [setup-idempotence.json](setup-idempotence.json)：Setup反復時のフォント生成物の安定性を確認した記録。
- [unity-final-console.json](unity-final-console.json)：最終Unityエラー0・警告0。
- `proof_Lobby_scrolled.png/json`と`proof_Lobby_scrolled_layout.json`：実スワイプ後に、準備取消と開始の全体がSafe Area内に見える。`proof_Lobby_cancelled*`→`proof_Lobby_to_Loading*`は通常ハンドラで取消→再準備→開始を確認した記録。OSクリック成功の証拠とは区別する。

目視の3件（ロビー見出し、ランキングの保存行、会員証のキャラ編集）と追加実操作の1件（ロビー下端へスクロールできない）を修正・再撮影した。最終の両40状態一覧、個別画像、スクロール証拠で解消を確認した。判断と限界は [visual-review.md](visual-review.md)。

## MainからR2までの録画

[title_to_round2.mp4](title_to_round2.mp4) はMain→Login→Tutorial1〜3→Home→Lobby→Waiting→Loading→R1採集→芋煮会演出→Result→R2を通して録画したもの。[flow-recording.json](flow-recording.json) にクリック、強化購入、自動操作、画面遷移、各フレーム時刻を保存する。

元の撮影画像は1,936枚、最終キャプチャ時刻77.9913535秒。GameのCore時計のみ既存の開発用10倍速、待機・Loading・演出は実時間。通常のBootstrap.TickとIGameServer操作を使い、撮影間隔の実測に沿って動画化している。MP4は25fps・1,953フレーム・78.12秒。撮影終了時はR2の採集中、Consoleエラー0・警告0。画面を切り貼りして成功を作った動画ではない。

[flow-review.md](flow-review.md)に全編デコード、24代表時点の目視と遷移照合を記録した。[前半一覧](flow-contact-1.png)／[後半一覧](flow-contact-2.png)も参照。動画に音声ストリームはなく、効果音の内容や同期はこの映像で評価しない。

## Android・生成物

`Imoni > Setup Project`で縦画面、ARM64/IL2CPP、API設定、提供画像由来の仮アイコン、フォントの事前生成を再現する。APK作成は `Imoni > Build > Android APK`、出力は `Builds/Android/Imoni.apk`。Editorのビルド結果は `Library/M7-android-build.json` に書き出される。

最終APKは、Coreの表示文字列を含む4字体の事前生成と全UI修正を反映して再ビルド済み。[android-build.json](android-build.json)は **Succeeded、エラー0・警告0、32.3442474秒**。実APKは **62,699,410 bytes（約59.80 MiB）**、ARM64/IL2CPP・Portrait・minimum API26／target API36で、SHA-256も保存している。BuildReportの`bytes`集計値ではなく、`apkBytes`が実APKのサイズ。

ビルド成功とDevice Simulatorの表示確認は、Android物理端末での起動・描画・性能確認とは別に扱う。物理端末へのインストールは行っていない。

実装・操作手順の詳細は [M7_PROGRESSION.md](../../M7_PROGRESSION.md)、[M7_PRESENTATION.md](../../M7_PRESENTATION.md)、数値の判断は [QUESTIONS.md](../../QUESTIONS.md) / [DECISIONS.md](../../DECISIONS.md) を参照する。
