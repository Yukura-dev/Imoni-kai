# M9 遊び方とBGM

作業ブランチ：`rebuild/m9-help-bgm`。mainを`git pull --ff-only`で188e4d8へ更新して作成した。mainに含まれていなかったレビュー承認済みM8追加分（89b7888）を734c209でmergeし、その上にM9を実装した。

## 受領資料

訂正された[共有ZIP](https://drive.google.com/file/d/1tbE7JQiVsCdy7kQqfmdGjNBdz-m0A9z-/view)は`imoni_m9_update.zip`、48,144,561 bytes、SHA-256 `DF585C6414F294FA80A457564112992AB1C45C4333160A4C8E43C79BA1A0E06A`。

mock・比較用画像・BGMを取り込み、UI_SPECは追加16・17章を反映。既存章はM8で承認された仕様を保持した。新しい17章を優先し、ゲームは指定のmain曲、ホームはwa/bandとした。判断はQUESTIONS.mdに記載。

## 確認手順

1. `Imoni > Setup Project`、続いて`Imoni > Open App`。初回は名前入力を経てホームへ進む。
2. ホーム右上の`?`を開く。5タブと前へ/次へで各項目を読む。最後の`わかった!`または上の戻るでホームへ戻る。
3. 設定→遊び方→戻るで開いていた設定へ復帰する。音量とアカウント入力は維持する。
4. Game→メニュー→遊び方を見る→戻るで元のメニューへ復帰する。倉庫右上`?`は倉庫の説明から始まり、戻ると行選択・入力数量を維持する。説明を開いている間もゲームの時間は進む。
5. ゲーム用BGMは冒頭約12.634秒のイントロから約65.524秒のループへ移る。Help往復では同じ再生位置を維持する。
6. 芋煮会演出に進むと0.3秒でBGM停止。結果/ゲームオーバーでホーム曲を再開、次のGameはイントロから始まる。
7. 設定でBGM/効果音を別々に0〜100%へ変更し再起動する。保存された値を維持する。和風/バンドは`GameConfig.audio.bgmStyle`で切り替える。

## 主な実装

| 対象 | 実装 |
|---|---|
| 文言/図形 | HelpContent.json、Export-HelpContent.pyでmockのSECTIONSとSVGパスを抽出 |
| 共通部品 | HelpTabs / HelpStepRow / HelpPageCard / HelpPager。ComponentGalleryにも登録 |
| 表示/復帰 | Help.uxml、HelpScreen、ScreenRouterのoverlay。元画面を破棄せず背面入力を無効化 |
| 選曲/時間 | CoreのBgmPlan / BgmTransition / BgmLoopPlan |
| 音の再生 | ScreenBgmController → AudioService → BgmPlaybackController。AudioMixerのBGMバスを利用 |
| 素材登録 | ImoniBgmSetupをSetupへ統合。Config catalogからUIRuntimeAssetsを生成 |
| 追加資料の小修正 | Tutorial2通常クリア文言、Tutorial3未解放品質の点線。既存QualityChipを利用 |

数値・曲キー・パス・音量はGameConfigに集約。インポートはVorbis品質0.7、Preload/DecompressOnLoad。DSP予約で整数サンプル長を使用し、Updateからループ再生を開始しない。アプリ休止時はDSPとフェードの両方を止める。原曲全体のWAVは資料として残し、実行時は分割イントロ/ループのみ参照する。

## 検証の再実行

- Core：`dotnet test tools/CoreTests --logger "trx;LogFileName=m9-core.trx" --results-directory Library/M9-tests`
- UI：隔離プロフィールでBootを開き、`Library/M9HelpReview.request`へ`run`を書き込む。結果は`Library/M9-help-validation.json`。
- BGM：同じBootで`Library/M9Audio.request`へ`{"command":"run","includeLoopRepeat":true}`を書き込む。実時間でループ1周まで観測する。完了まで他の画面操作をしない。結果は`Library/M9-audio-events.json`。
- 撮影：Gameビュー390×844で`tools/Capture-M9.json`を`Library/M2BReview.request`へコピー。メニュー撮影後、`tools/Compare-M9.ps1`で左モック/右Unityを出力する。
- 標準APK：`tools/Invoke-M7Review.ps1 -RequestJson '{"command":"build"}' -TimeoutSeconds 1200`。実機インストールは人間が行う。

最終結果と比較画像は`validation/m9/`を参照。Editorでの音源/ミキサー/DSP観測を記録し、Android実機で聴取した結果とは区別する。

完了結果：Core 548/548（追加56ケース）、Help実操作28/28、BGM実再生25/25。Unity最終エラー0・警告0、標準APKビルド成功（エラー0・警告0、versionCode4、68,362,350 bytes）。比較9枚を保存。APKのSHA-256は`63CE972257F7466F6B5CF92C46A48E3497141550A7F25E3A7C037DC22B333B10`。
