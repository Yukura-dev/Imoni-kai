# M8 追加の実機プレイテスト修正

2026-11-26。M8の既存コミット`ae862cb`を修正前として、同じ`rebuild/m8-playtest-fixes`で追加対応。以下はUnity Editorでの検証であり、Galaxy A53実機での再検証結果ではない。

## 変更と画像

比較は**左=修正前、右=修正後**。各入力はGameビュー390×844、比較は780×844。元画像は`before/`と`after/`。自動撮影はEditor専用レビュー操作を使い、画像を描き直したものではない。読み込み進捗や鍋の湯気などのアニメーション位相は撮影時刻で異なる。

| 対象 | 確認した変更 | 比較 |
|---|---|---|
| タイトル進行中 | ロゴ右の湯気をクリップ、設定ボタン追加、塗りの左端 | [Main_loading](compare_Main_loading.png) |
| タイトル完了 | 100%で枠の内側両端まで塗る | [Main_ready](compare_Main_ready.png) |
| タイトル設定 | Homeと同じSettingsDialog | [Main_settings](compare_Main_settings.png) |
| ホーム | アバター/名前のブロック/設定ボタンの縦中心、アイコン中央 | [Home](compare_Home.png) |
| 設定 | BGM/効果音の独立音量、保存、共通ON/OFF | [Settings](compare_Settings.png) |
| 会員切替 | 独立Login画面を起動から除外し、設定の中へ移動 | [Account_switch](compare_Account_switch.png) |
| チュートリアル2 | 「すべて」を削除 | [Tutorial2](compare_Tutorial2.png) |
| チュートリアル3 | 神話/超越/究極をグレーの???、倍率非表示 | [Tutorial3](compare_Tutorial3.png) |
| 読み込み | 共通ProgressBarの端を確認 | [Loading](compare_Loading.png) |
| ゲーム | 重なっていたボット名札を別位置に配置 | [Game_labels](compare_Game_labels.png) |
| ゲームオーバー | 指定された記録保持の注記行を削除 | [GameOver](compare_GameOver.png) |

ロゴは原画PNGの文字を保つ非破壊のTitleLogo部品。原画978pxの左780pxを描画し、離れた湯気だけを除外した。新しい文字を生成したり、原画の影を加工したりしていない。

Homeの実レイアウトは[home-layout.json](home-layout.json)。アバター(18,52,54,54)、名前と二つ名のブロック(84,54,232,50)、設定枠(328,57,44,44)はすべて中心Y=79。設定アイコン(339,68,22,22)の中心(350,79)も枠中心と一致。44pxのタップ領域を維持。

ProgressBarはThick/Thin/Rainbowの共通部品を修正。[Gallery前半](progress-gallery.png)・[後半](progress-gallery-02.png)で0/50/100%、読み込み画面とGameOverの素材行でも確認。満タンの端にクリームの三日月形の隙間が残らない。

## 採取札の確認

[近く](labels/near.png) / [少し離れた位置](labels/away.png)、それぞれのJSONに予約矩形と実際のLabel.worldBoundを記録。

- シード202647、R1、強化なし。ボット2人を切断表示にして、resource-33-27を実際の画面座標→TryTapPanelPointから3回採取。
- 5.8秒時点で在庫0、残り再生時間39.94秒。自分との距離1タイルなので選択中でも成長札は隠れ、芽の表示のみ。獲得「+3 薪(希少) 連続1」と「あなた」は分離。
- 地面(31,27)へタップし0.5秒移動後、資源との距離1.9567タイル。成長札が再表示され、名札・獲得popup・成長札が同時に見える。
- 両状態とも予約矩形の交差0、実描画矩形の交差0、近接除外違反0。
- Game_labelsの比較はシード482/25秒。古い撮影はpopupが残った停止状態、新しい撮影は25秒のまとめTickでpopup寿命も進んだ状態。popupの消失を改善の証拠には用いず、上の近接/離隔2枚と実矩形で検証した。

## テストと音

`dotnet test tools/CoreTests --logger 'trx;LogFileName=m8-extra-core.trx'`

**492件成功 / 失敗0 / スキップ0**（約56秒）。[TRX](dotnet-test.trx)。従来447件に以下45ケースを追加。

- AudioChannelSettingsTests 10件：旧SoundVolume移行、独立保存・変更、欠損値、境界値、全体muteで音量保持。
- LocalAccountsTests 35件：旧単一保存移行、3会員の再起動、未保存変更を含む往復切替、新規初期値、無効/未知/重複IDの非破壊、返却値/保存値の参照分離、破損active ID、復旧用ID衝突時の既存会員保持。

[settings-audio-validation.json](settings-audio-validation.json)はUnity内の実コントロールのクリック/入力ハンドラから検証。初回Main→Tutorial1→2→3→Home、登録済みMain→Home、両画面の共通設定、2つの音量保存、設定内の新規会員と2会員切替、再読込が成功。元のセーブのbyte一致と設定復元も確認。OSのタッチ注入や実機での試聴を代替するものではない。

MixerはMaster下にBGM/SFX、Masterに既存Compressor。未作成のBGMはClip未登録で正常に無音。一時的な無音PCMのAudioClipを使い、BGM再生継続/SFX停止、逆方向、各mute、各Mixer値、全体OFF→ONを確認。演出ドローンはSFX分類。背景曲の追加方法は[音設定](../../../M8_AUDIO_SETTINGS.md)。

## Simulator

`simulator/`にNote10/iPhone12を適用したGameビューの設定/会員切替画像。Safe Area内にカードと操作が収まる。これはGalaxy A53固有のちらつき改善やAndroidキーボードの実測ではない。

## 再現手順

1. UnityでImoni > Setup Projectを実行し、Bootを再生。
2. タイトル右上で設定を開く。BGMだけを下げ、効果音が独立していることを確認。閉じて開始し、初回のみチュートリアルを経由する。
3. Homeの設定で同じ音量を確認。設定のアカウント切り替えから名前を入れて新規作成し、チュートリアル後に元の会員へ切り替える。元の記録が残る。
4. Playを停止して再起動し、選択中の会員と両音量が保持されることを確認。
5. ゲームで採取して資源を空にし、近くでは成長札が隠れ、少し離れると他の札を避けて表示されることを確認。
6. Imoni > Open Component GalleryからProgressBarを選び、Thick/Thin/Rainbowの0/50/100%を確認。

APKと最終Consoleの記録は同フォルダのandroid-build-*.json / apk-manifest.json / unity-console.json。

既存アウトゲーム統合検証も現在の獲得/ローカルランキング仕様に合わせ、[outgame-validation.json](outgame-validation.json)の7系列すべて成功。M2b当時の複数初期二つ名・ダミーランキング前提が残っていたため、製品仕様を変えず、検証用の所有パーツと4部門の記録を明示的に用意した。

## 最終ビルド

3種類ともSucceeded、エラー0・警告0。IL2CPP / ARM64 / 縦固定 / min API26 / target API36 / jp.yukura.imoni。aaptで各APKのversionCode 3を確認。

| APK | API / Frame Pacing | 実ファイルサイズ | ビルド秒 |
|---|---|---:|---:|
| Builds/Android/Imoni.apk | Vulkan優先、GLES3 fallback / On | 63,515,460 bytes | 210.8 |
| Builds/Android/Imoni-gles3-on.apk | GLES3 / On | 62,287,768 bytes | 37.1 |
| Builds/Android/Imoni-gles3-off.apk | GLES3 / Off | 62,207,525 bytes | 34.0 |

[APKハッシュとメタデータ](apk-manifest.json)。BuildReportのbytesはシンボル等を含む総出力なので、表は実APKのLengthを使用。APK本体は従来どおりgit対象外のBuilds/Androidに置き、ソースと検証記録をコミットする。Unity Consoleの最終再コンパイル/Boot確認はエラー0・警告0（下記のビルド後Editor警告とは分けて記録）。ADB端末一覧は空であり、実機再試験は未実施。

### Editorへ戻ったときのUnity同梱シェーダー警告

APKの3つのBuildReportはすべてエラー0・警告0だった。一方、ビルド後にEditorへ戻った時点のConsoleには、Unityパッケージ `com.unity.render-pipelines.core` の `TraceVirtualOffset`（D3D11、Fibonacci/Hammersley/Samplingのmin16floatへの型変換）から9件の警告が出た。[その時点のConsole](unity-console-after-build.json)と[9件の全文](unity-package-shader-warnings.txt)を保持している。パッケージキャッシュのソースやシェーダー警告のフィルターは変更していない。

この記録を保存後にConsoleをリセットし、最終スクリプトを再コンパイル、Boot→Home/設定→Gameを再起動して検証したものが[unity-console.json](unity-console.json)。これをビルド中を通して警告が一度も出なかったという意味にはしない。
