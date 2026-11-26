# M5 検証記録（2026-11-26）

Unity 6000.3.21f1、Gameビュー390×844。ブランチは `rebuild/m5-round-flow`。操作手順・追加テスト一覧は [M5_ROUND_FLOW.md](../../M5_ROUND_FLOW.md)。

## 撮影と比較

比較PNGは左が `design/screenshots` のモック、右がUnity（各390×844）。Unityの原本画像自体に加工はしていない。

| 比較画像 | 演出開始からの時刻／状態 |
|---|---|
| `compare_40_Imoni_start.png` | 成功1.5秒 |
| `compare_41_Imoni_mid.png` | 成功4.6秒 |
| `compare_42_Imoni_finale.png` | 成功9.0秒 |
| `compare_44_ImoniFail_mid.png` | 失敗4.0秒 |
| `compare_45_ImoniFail_end.png` | 失敗9.0秒 |
| `compare_43_Result.png` | Coreで実際に成功したR1の結果 |
| `compare_46_GameOver.png` | Coreで実際に失敗したR3の結果 |

演出5枚は見た目を比較する専用プレビューで、既存 `effectReview` の成功18,420・失敗7,350・不足「魚」・シード40410を入力している。これは `source: artwork-preview` と明示し、プレイ中の確定結果を書き換えない。実際のCore結果からの成功・失敗演出は別の `proof_actual_*_effect.png/json` に記録した。結果／GameOverの数量・目標・ランク・スコアは実際のプレイ内容なので、モックの固定値とは異なる。

`unity_imoni_success.mp4` と `unity_imoni_fail.mp4` は、同じ実行時描画経路を25fpsで315フレーム撮影した12.6秒の動画。音声は含まない。`compare_imoni_*.mp4` は提供された参考動画を左、Unity動画を右に並べたもの。各フレームの時刻・スコア・レベル・完成時刻・ボタン表示時刻は `imoni_*-telemetry.json` に残している。

`compare_aligned_imoni_*.mp4` は参考動画の導入（成功0.76秒、失敗0.24秒）だけを除いて開始を合わせた比較。導入の計測値は `reference-intro-measurement.json`。

静止見本と参考動画は同じ時刻の状態ではなく、動画には冒頭の白画面もある。指定撮影時刻を変更して見本へ合わせることはしていない。差の根拠と仕様8章の照合は [effect-comparison.md](effect-comparison.md) を参照。

## 実際のゲーム進行

Editor専用ヘルパーは、通常UIのクリックコールバックと `IGameServer` の操作・Tickを使用する。確定結果・資金・設定値へ直接代入しない。レビュー用プロフィールは `Library/M5-profile.json` に分離し、通常セーブは変更しない。

- `proof_timeup`：5:00で停止してダイアログ表示。強化を購入後、ボットが採集してR1成功。
- `proof_actual_success_effect`：Coreの最終スコア1,078を演出に表示。
- `proof_next_round`：資金926G、採集速度Lv.1、ワールド識別ハッシュと全資源成長状態ハッシュが直前の結果と一致。倉庫・保護0、全員の連続0、解放半径10→17。
- `proof_round2_timeup`：R2も生成された目標で成功。
- `proof_actual_fail_effect`：R3で実際に不足した「希少な山菜・里芋」とスコア4,190を表示。
- `proof_retry_loading` / `proof_retry_round1`：再挑戦ボタンで新しいシードのLoadingへ進み、開始するとR1・1000G・全強化0・空倉庫・連続0。
- `proof_short_round_timeup`：短縮ONの10倍速でCoreが300秒に到達し停止。確認後OFFへ戻した。
- `proof_replay` / `proof_skip_result`：もう一回見る／スキップでスコア・資金・累積を再加算しない。
- `proof_sound_off` / `proof_sound_restored`：音OFFをプロフィールへ保存し、演出画面を作り直してもOFF。確認後ONへ戻した。
- `proof_normal_clear` / `proof_clear_home`：通常の選択・採集・購入・Tickで5回成功し、Clear→Homeへ遷移。各ラウンドのCore結果は `M5-autoplay_round1.json`〜`M5-autoplay_round5.json`。
- `proof_gameover_home`：GameOverのホームボタンでHomeへ遷移。

比較画像と各操作検証は別セッションを含む。`tools/Validate-M5Evidence.ps1` で引継ぎ・初期化・撮影時刻・動画315フレーム・Console件数など23項目成功、0失敗（`round-flow-validation.json`）。

Unity再コンパイル、Setup、フォント生成物をコミット済みの状態へ戻した後の再import、5連続成功／失敗／演出全フレームを確認し、エラー0・警告0。初回検証で見つかったAudioListener不足と音源の初期化順を修正した。動的フォントの初回import警告は停止・再import後に解消し、警告の抑制はしていない。動的フォントキャッシュと自動生成slnxの差分はコミットに含めない。

## テストと再実行

`dotnet test tools/CoreTests --no-restore --logger 'console;verbosity=minimal'`：全300件成功、失敗0、スキップ0。M5追加26ケースの一覧はM5資料に記載。1／18,420／10^15の最終スコアで成功・失敗とも10秒以内のボタン表示を検証した。

`tools/Invoke-M5Review.ps1` からEditor専用 `ImoniM5ReviewJob` を呼ぶ。`open` がSetup・390×844・レビュー用保存先を準備する。`prepare`／`step`／`click` は実ゲーム進行、`preview`／`sample` は比較用演出、`capture`／`record` は画面と状態の保存。`tools/Compare-M5.ps1 -Videos -Ffmpeg <ffmpegの実行ファイル>` で比較画像と動画を生成できる。
