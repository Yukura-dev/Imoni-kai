# M8 Android上部ちらつき：対策済み・実機再確認必要

コード上の描画範囲・外周背景・同値再設定への対策を実装した。**Galaxy A53でちらつきが消えたことは、まだ確認していない。** 今回の作業環境ではADBの接続一覧が空で、端末の描画・画面録画・GPUログを採取できていない。Editorの確認とAndroid実機の結果を分けて記録する。

## 報告と原因の確度

報告端末はGalaxy A53。Androidのバージョンは不明。症状は「ゲーム画面の黄色いHUDへ、マップの色の横帯が一瞬だけ、毎回違う高さに出る」。PCでは再現していない。

修正前のコードを確認して分かったこと：

- `GameWorldView.ResizeCamera` は、既にマップ領域から `camera.rect` を計算していた。ただし、同じ値でも毎フレームsetterを呼び、最初のレイアウトが成立するまでの間はBootのカメラ状態が残った。
- `SafeAreaAdapter` は100msごとの確認で、値が変わらなくてもpaddingのsetterを呼んでいた。
- Game表示中の `ScreenHost` はマップを見せるため透明になり、Safe Area外周に専用の不透明背景がなかった。
- HUDの黄色と下ボタン領域のクリームは、修正前から不透明な色だった。

以上はコード上の事実であり、実機の横帯との因果関係を確定する計測結果ではない。特定GPU・Vulkan・Frame Pacing・Galaxy A53固有の問題とは断定しない。今回、不要な再設定と未確定レイアウトでの描画を抑え、マップ外を明示的に覆う対策を行った。

## 実施した対策

| 対象 | 実装内容 | 主なファイル |
|---|---|---|
| マップ描画範囲 | HUDと下ボタンの間からviewportを算出。画面の内側へclampし、物理ピクセル境界で内向きに丸める | `Assets/Imoni/Scripts/Game/World/GameWorldView.cs` |
| 初回・無効レイアウト | Game生成時はカメラを無効化。幅・高さが0、不正な値、描画面が無効な間も無効化し、地色の不透明な仮背景を表示。GeometryChangedで有効なマップ領域が確定したらカメラを有効化し、仮背景を外す | 同上 |
| カメラの再設定 | viewportとorthographicSizeが同じならsetterを呼ばない | 同上 |
| 画面とワールドの変換 | 丸めた実際の `camera.rect` を `CameraPanelBounds` でpanel座標に戻し、タップ・名札・ポップアップ等の変換にも共用する | 同上 |
| 外周の背景 | `SafeTopCover` / `SafeBottomCover` / `SafeLeftCover` / `SafeRightCover` を追加。背景alpha・opacityは1。Gameの下端は下ボタンと同じクリーム、その他は黄色 | `Bootstrap.cs`、`Resources/UI/ScreenLayout.uss` |
| Safe Area | 100msの確認自体は継続するが、同じpadding・覆い寸法・クラスを再設定しない。装飾用paddingの調整も同値なら書かない | `Scripts/Game/UI/SafeAreaAdapter.cs` |

HUDと下ボタンの既存不透明背景を維持する。Gameでマップを見せる開口部は透明のまま、Safe Area外周を個別に塗る。芋煮会演出はこれらの覆いより後に描画し、光・虹・白フラッシュ・失敗時の覆いを画面全体へ広げる。

## Editorでの確認

`ImoniM8ReviewJob` の `stable` は、`SafeArea.Refresh()` とGameの表示更新を**同期的に100回**呼び、paddingとviewportのsetter回数の差分を記録する。100フレームの動画や、実機での10秒間の観測ではない。

| 条件 | 呼び出し回数 | padding書き込み差分 | viewport書き込み差分 | 記録 |
|---|---:|---:|---:|---|
| 通常Gameビュー390×844 | 100 | 0 | 0 | [render-native-stable.json](render-native-stable.json) |
| Device Simulator / Galaxy Note10 | 100 | 0 | 0 | [Note10/stable.json](simulator/Note10/stable.json) |
| Device Simulator / iPhone 12 | 100 | 0 | 0 | [iPhone12/stable.json](simulator/iPhone12/stable.json) |

初回GeometryChangedでviewportが確定した後の取得。100回の更新でもviewportとSafe Areaのpadding再設定はともに0回だった。

3件のJSONで、HUD・下ボタン・4辺の覆いの `backgroundAlpha` / `opacity` はすべて1。通常Gameビューのカメラは物理座標 `(x=0, y=84, width=390, height=560)`（カメラは左下原点）で、UIのマップ領域 `(x=0, y=200, width=390, height=560)`（左上原点）と一致する。ノッチ形状についてはNote10とiPhone 12のSimulatorで確認した。Note10は報告端末のGalaxy A53とは異なり、GPUやAndroidの描画経路を再現する確認ではない。

## Androidの比較用設定・APK

`GameConfig.json` の `setup.android` を `ImoniAndroidSetup` から適用した。**標準版と比較用2版すべてビルド成功、各エラー0・警告0**。実機でのAPI別・Frame Pacing ON/OFF比較はすべて未検証。

| 版 | Graphics API設定 | Optimized Frame Pacing | Unityメニュー | 出力先 | ビルド結果 | Galaxy A53のちらつき |
|---|---|---|---|---|---|---|
| 標準 | Vulkan優先、非対応時OpenGLES3 | ON | `Imoni > Build > Android APK` | `Builds/Android/Imoni.apk` | [成功 0/0](android-default.json) | 未検証 |
| GLES3 ON | OpenGLES3のみ | ON | `Imoni > Build > Android GLES3 Frame Pacing On` | `Builds/Android/Imoni-gles3-on.apk` | [成功 0/0](android-gles3-on.json) | 未検証 |
| GLES3 OFF | OpenGLES3のみ | OFF | `Imoni > Build > Android GLES3 Frame Pacing Off` | `Builds/Android/Imoni-gles3-off.apk` | [成功 0/0](android-gles3-off.json) | 未検証 |

各JSONはBuildPipelineの結果に実APKの `apkBytes` とSHA256を付記した。`bytes` はUnityのBuildReport総量（シンボル等を含む）で、APK自体のサイズではない。IL2CPP/ARM64・縦固定、package `jp.yukura.imoni`、versionCode 2、min API 26 / target API 36。比較版を作った後は標準のAPI列とFrame Pacing ONへProjectSettingsを戻す。

各版はAuto Graphics APIをOFFにして列挙順を明示する。UnityはAPIリストの先頭を優先し、端末が対応しなければ後続へフォールバックするため、標準版の実際の使用APIは端末上で確認する必要がある。[Unity 6.3 Android Player settings](https://docs.unity.com/en-us/engine/6000.3/manual/platform-specific/android/getting-started/class-player-settings)

Optimized Frame Pacingはフレームの提示間隔を均等にする設定。今回の不具合を解消する機能と決めつけず、同一APIのON/OFF版で差を見る。[Unity 6.3 PlayerSettings.Android.optimizedFramePacing](https://docs.unity.com/en-us/engine/6000.3/script-reference/unityeditor/playersettings/android/optimizedframepacing)

`renderOutsideSafeArea = true` を維持し、アプリが描画する外周も明示的に背景を塗る。Androidバージョンによって画面端の扱いが異なるため、再確認時はOS版も記録する。[Unity 6.3 Android Player settings — Render outside safe area](https://docs.unity.com/en-us/engine/6000.3/manual/platform-specific/android/getting-started/class-player-settings)、[PlayerSettings.Android.renderOutsideSafeArea](https://docs.unity.com/en-us/engine/6000.3/script-reference/unityeditor/playersettings/android/renderoutsidesafearea)

## 実機での再確認手順

1. Galaxy A53のAndroid/One UIバージョン、画面解像度、リフレッシュレート、ナビゲーション方式を記録する。ビルド版と、可能なら実際のGraphics APIも記録する。
2. まず標準APKを入れ、タイトルからゲームへ進む。マップを静止した状態と、連続してタップ・移動・採集した状態を画面録画する。黄色いHUD・上下の境界・ステータスバー下をコマ送りで見る。
3. 倉庫等のパネル開閉、ホームからゲームへの再入場、バックグラウンドからの復帰でも同じ箇所を確認する。芋煮会演出に入った際、上端に黄色い帯が残らないことも見る。
4. 標準版で残る場合、同じ端末・OS・画面設定・操作でGLES3 ON版を確認する。その後GLES3 OFF版で同じ確認を行う。再現までの時間、帯が出た時刻・高さ・回数を各版で記録する。
5. 録画と使用APKを対応づけて残す。ADBを利用できる場合は端末の接続・使用API・描画エラーも記録する。新しい結果をこの表に追記するまでは「実機で解消済み」と扱わない。

現時点の判定は**対策実装済み、Editorの構造・同値再設定抑止は確認、実機再確認必要**。
