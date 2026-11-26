# M9 BGM 実再生記録

Unity Editorの実時間 89.881 秒で 25 項目すべて成功。
イントロ/ループを実際のAudioSourceで再生し、DSP時刻・timeSamples・isPlaying・ミキサー音量を観測した。
Android実機での聴取結果ではない。背景停止はOnApplicationPause(true/false)へ同じ処理を送ったEditor検証。

ゲームは最新UI_SPEC17章の指定曲、ホームは和風/バンド。ゲームから開くHelpはゲーム曲を継続する。

| 開始から秒 | 画面/文脈 | イベント | 曲 | フェード秒 | BGM音量 |
|---:|---|---|---|---:|---:|
| 0.000 | Tutorial3 | stop-immediate | 停止 | 0 | 100% |
| 0.004 | Home | crossfade | bgm_home_loop | 0.5 | 100% |
| 0.005 | Home | continue | bgm_home_loop | 0.5 | 100% |
| 0.056 | Home | loop-start | bgm_home_loop | 0 | 100% |
| 0.493 | Home | fade-complete | bgm_home_loop | 0 | 100% |
| 0.707 | Home | crossfade | bgm_home_loop_band | 0.5 | 100% |
| 0.747 | Home | loop-start | bgm_home_loop_band | 0 | 100% |
| 1.205 | Home | fade-complete | bgm_home_loop_band | 0 | 100% |
| 1.408 | Home | crossfade | bgm_home_loop | 0.5 | 100% |
| 1.459 | Home | loop-start | bgm_home_loop | 0 | 100% |
| 1.907 | Home | fade-complete | bgm_home_loop | 0 | 100% |
| 2.427 | Game | crossfade | bgm_game_main | 0.5 | 100% |
| 2.490 | Game | intro-start | bgm_game_main | 0 | 100% |
| 2.607 | Game | fade-complete | bgm_game_main | 0 | 100% |
| 3.128 | Game | application-pause | bgm_game_main | 0 | 100% |
| 4.129 | Game | application-resume | bgm_game_main | 0 | 100% |
| 16.118 | Game | loop-start | bgm_game_main | 0 | 100% |
| 81.624 | Game | loop-repeat | bgm_game_main | 0 | 100% |
| 81.846 | Help from Game | continue | bgm_game_main | 0.5 | 100% |
| 82.057 | Game | continue | bgm_game_main | 0.5 | 100% |
| 82.265 | Game | application-pause | bgm_game_main | 0 | 100% |
| 83.267 | Game | application-resume | bgm_game_main | 0 | 100% |
| 83.711 | Imoni | fade-out | 停止 | 0.3 | 100% |
| 83.766 | Imoni | fade-complete | 停止 | 0 | 100% |
| 84.236 | Result | crossfade | bgm_home_loop | 0.5 | 100% |
| 84.280 | Result | loop-start | bgm_home_loop | 0 | 100% |
| 84.711 | Result | fade-complete | bgm_home_loop | 0 | 100% |
| 85.294 | Game | crossfade | bgm_game_main | 0.5 | 100% |
| 85.348 | Game | intro-start | bgm_game_main | 0 | 100% |
| 85.436 | Game | fade-complete | bgm_game_main | 0 | 100% |
| 86.207 | ImoniFail | fade-out | 停止 | 0.3 | 100% |
| 86.295 | ImoniFail | fade-complete | 停止 | 0 | 100% |
| 86.722 | GameOver | crossfade | bgm_home_loop | 0.5 | 100% |
| 86.773 | GameOver | loop-start | bgm_home_loop | 0 | 100% |
| 87.207 | GameOver | fade-complete | bgm_home_loop | 0 | 100% |
| 87.423 | GameOver | crossfade | bgm_home_loop_band | 0.5 | 100% |
| 87.466 | GameOver | loop-start | bgm_home_loop_band | 0 | 100% |
| 87.922 | GameOver | fade-complete | bgm_home_loop_band | 0 | 100% |
| 88.124 | GameOver | crossfade | bgm_home_loop | 0.5 | 100% |
| 88.175 | GameOver | crossfade | bgm_home_loop_band | 0.5 | 100% |
| 88.178 | GameOver | loop-start | bgm_home_loop | 0 | 100% |
| 88.674 | GameOver | fade-complete | bgm_home_loop_band | 0 | 100% |
| 88.876 | GameOver | preferences | bgm_home_loop_band | 0 | 10% |
| 89.076 | GameOver | preferences | bgm_home_loop_band | 0 | 0% |
| 89.278 | GameOver | preferences | bgm_home_loop_band | 0 | 100% |
| 89.480 | GameOver | preferences | bgm_home_loop_band | 0 | 0% |
| 89.680 | GameOver | preferences | bgm_home_loop_band | 0 | 100% |

## 検証項目

| 完了秒 | 項目 | 結果 |
|---:|---|---|
| 0.703 | home-wa | 成功 |
| 1.405 | home-band-crossfade | 成功 |
| 2.106 | home-wa-crossfade | 成功 |
| 3.125 | game-intro | 成功 |
| 4.126 | background-pause-during-intro | 成功 |
| 4.327 | background-resume-during-intro | 成功 |
| 16.316 | game-intro-to-loop | 成功 |
| 81.841 | game-loop-repeat | 成功 |
| 82.052 | help-from-game-keeps-track | 成功 |
| 82.263 | help-return-keeps-track | 成功 |
| 83.264 | background-pause | 成功 |
| 83.465 | background-resume | 成功 |
| 84.210 | imoni-fade-stop | 成功 |
| 84.935 | result-home-restart | 成功 |
| 85.993 | next-game-new-intro | 成功 |
| 86.706 | imoni-fail-fade-stop | 成功 |
| 87.420 | game-over-home-restart | 成功 |
| 88.121 | time-scale-zero-crossfade | 成功 |
| 88.172 | retarget-mid-crossfade | 成功 |
| 88.873 | retarget-completed | 成功 |
| 89.074 | bgm-volume-independent | 成功 |
| 89.275 | bgm-zero-keeps-effects | 成功 |
| 89.477 | effects-zero-keeps-bgm | 成功 |
| 89.678 | sound-off | 成功 |
| 89.879 | sound-on | 成功 |

詳細な予約DSP時刻・実測サンプル位置・各Voiceのgainは`audio-events.json`、件数は`audio-result.json`。
音量の永続化は既存のProfileData/LocalAccountsを使用し、今回のCore全548件にその回帰テストも含む。
