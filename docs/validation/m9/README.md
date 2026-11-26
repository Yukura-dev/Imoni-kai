# M9 検証

390×844のGameビューで撮影。`compare_*.png` は左が受領モック、右がUnity。Help 50〜54の5枚に加え、Home・Tutorial2・Tutorial3・Gameメニューの変更後を比較した。`unity_Settings.png`は設定内の入口、`M2A-HelpPageCard*.png`は共通部品のGallery表示。

Helpのカード・タブ・本文・注意ラベル・5ページの送り/戻りを確認。上位3品質は???で隠し、点線をPainter2Dで描く。Home/メニュー背景のマップ、プレイヤーの状態、目標数量などは以前に実装したCoreの値なのでモックの固定データとは異なる。M8で指定された新鍋・4レイヤーアバターもそのまま維持した。

| 確認 | 結果/証跡 |
|---|---|
| dotnet Core | 548成功 / 0失敗 / 0スキップ。追加BGM56ケース。`core-tests.json` / `core-tests.trx` |
| Help実操作 | 28/28成功。`help-validation.json` |
| BGM実再生 | 25/25成功。約90秒、intro/loop1周/中断復帰/Help/演出/結果/音量。`audio.md` / `audio-events.json` |
| 初回Unity再コンパイルとPlay | エラー0 / 警告0。`unity-console-before-build.json` |
| Gallery | HelpPageCard全5種類の実描画、エラー0 / 警告0。`gallery-result.json` |
| 標準Android APK | 成功、エラー0 / 警告0、IL2CPP ARM64、versionCode 4。68,362,350 bytes |
| ビルド後Boot→Home/Help/Game | エラー0 / 警告0。`unity-console-final.json`。Help28項目も再確認済み |

BGMの検証はEditorのAudioSource・AudioMixer・DSP時計の実測。アプリのバックグラウンド化は同じOnApplicationPause処理を呼ぶ検証で、Android実機の音声出力・OS復帰を実測したものではない。

最終AndroidビルドとConsoleの記録は`android-build.json` / `apk.json` / `unity-console-final.json`。APK本体はリポジトリ外の`Builds/Android/Imoni.apk`。

初回はAPK生成に成功したがUnity Cloudのシンボル送信認証403による警告が1件出た。`android-build-first-attempt.json`に保存済み。SetupからEngineDiagnosticsを無効にした最終再ビルドは警告0。警告の非表示やConsoleの除外フィルタではなく、不要な外部接続を実行しない設定で解決した。
