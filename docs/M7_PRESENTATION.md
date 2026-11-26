# M7 表示の仕上げ

## 地形と未開放境界

森林と未開放の覆いは `WorldBoundarySurface` を共用する。既存の `tile_forest.png` / `tile_locked_overlay.png` を描く4頂点のメッシュに、Coreの64×64セルから作ったマスクを渡す。マスクの双線形補間とsmoothstepで角を丸め、`ui.worldView.boundaryFeatherPx=4` の範囲で縁をなじませる。

Coreの地形、橋、資源、A*、開放範囲は変更しない。マスクの各セル中心はCoreの森林／未開放判定と一致する。森林セルのTilemap下地だけ既存の平野画像に置き換え、森林画像をその上へ重ねる。河川や橋は従来のTilemapで描画する。鍵ラベルも従来の部品を使う。

メッシュ・マテリアル・64×64のマスクは表示作成時に確保し、森林は生成時だけ、未開放はラウンド変更時だけ更新する。画面を離れる際にすべて解放する。元画像や生成地形を書き換えない。`Imoni > Setup Project` が `WorldRuntimeAssets.worldBoundaryShader` を設定する。

## 再接続の表示と確認入口

`ReconnectNotice` は既存のCard、ImoniButton、Themeを使う共通コントロール。自分の `PlayerSnapshot.Connected` がfalseの間だけ、Game上に「再接続中…」「接続を確認しています。チームの挑戦は続いています。」を表示する。マップ・下ボタン・パネル操作と履歴トーストを止めるが、サーバーのTickと仲間の行動は続ける。「ホームへ戻る」は `IGameServer.AbortChallenge` を経由する。

Editor / Development Buildの確認API：

```csharp
GameScreen game = /* 現在の Game 画面 */;
game.BeginReconnectDemo();          // 自分を一時切断
game.BeginReconnectDemo("player2"); // 仲間の切断表示を確認
// game.IsReconnecting / ReconnectDemoPending / ReconnectDialog で状態を読める
```

`IGameServer.SetConnected(false)` の後、`ui.reconnectDemoSeconds=3` 秒の実時間でtrueへ戻す。ワールドや挑戦を作り直さない。自分の以前の採集先がまだ有効なら同じ選択APIで再開し、仲間は既存のボット処理で復帰する。開発用入口は通常の画面ボタンには出さない。外部サーバーが同じConnected状態を通知した場合も表示部品を再利用できる。

確認手順：Gameで採集先を選択→上記APIを実行→自分が半透明・採集停止、仲間とタイマーが継続していることを確認→3秒後に同じラウンド・倉庫・資金・強化・ワールドで操作が戻ることを確認する。仲間のデモではメニューの「切断中・再接続待ち」とワールドの半透明を確認する。

## 演出見出しを画像にしない判断

提供Dela Gothic Oneを継続使用する。モックのブラウザーとTextCoreの描画差だけを補うため、「芋煮」「完成!!／未完成…」に同じ文字・同じ座標の前景ラベルを1枚ずつ追加し、文字と同色の0.1pxストロークを付ける。調整値は `imoniEffect.layout.finaleFillStrokePx`。実画像QAで前景の1pxやTextCoreの合成BoldはSDFの字の穴を潰したため、最終版の追加前景は元フォントのNormalと0.1pxを使う。Dela自体の太い字形を維持し、前景へ追加の合成Boldは使わない。元の外枠ラベルのBold、えんじ／焦げ茶の縁取り、成功58px・失敗52px、光彩、出現時刻、拡大率は保持する。

固定PNGにしない理由は、元フォントと変更可能な文言を正本として維持し、端末解像度で画像を拡大した時のぼけや文言アセットの重複を増やさないため。追加ラベルは再生開始時の2枚だけで、フレームごとには生成しない。最終の太さは成功・失敗のモック比較画像で確認する。

## フォントの事前生成とAndroidの大数単位

M6のHome/設定124字だけでは、後半ラウンドの報酬や新しい画面でBlackフォントのAtlasが追加され、TextCoreの描画後再ImportでNativeFormatImporterの一貫性警告が再発した。Black対策後のアウトゲーム確認ではBoldとDelaでも同じ警告が出たため、最終版は全4字体を対象にする。`ImoniFontWarmup` が全UXMLの属性、Gameスクリプトの文字列リテラル、GameConfigの文字列を読み、既知UI文字をSetupで事前生成する。最終の集合は594字で、Zen3種は「秭」以外の各593字、Delaは全角「＋」以外の593字を用意する。文字集合は固定の手書き一覧にせず、資料・コードの更新を次のSetupで取り込む。

提供Zen3種には「秭」がなく、提供Delaには収録されていることをTTFのcmapで確認した。Zen3種のFontAssetからDelaへ明示的なフォールバックを設定し、Dela側はフォールバックを空にして循環を作らない。R5で自然に表示される大数単位を、AndroidのOSフォントの有無に依存させないため。Delaにない全角「＋」は、Zen Blackを使う倉庫の保護増加Smallボタン専用なので、Delaの事前集合からだけ除外する。この2字以外の未収録文字はエラーとして検出する。フォントの追加ダウンロードや元TTFの変更は行わない。未知のプレイヤー名に対応できるようDynamicは維持する。

追加するのは未登録の文字だけ。すべて登録済みならTryAddCharactersを呼ばず、フォントに変更がなければ保存・再Importもしない。複数Atlasの配列は未使用末尾にnullを持ち得るため、保存と検証は `atlasTextureCount` の実使用枚数に限定する。生成した4種のFontAssetとAtlas・フォールバック参照は意図した生成物として保持する。

Androidビルド時、TextCoreの既定の「Clear Dynamic Data on Build」が事前生成した文字とAtlasを消すことも確認した。Setupで4字体の `m_ClearDynamicDataOnBuild=false` を設定し、既知文字をビルドにも保持する。Unity 6000.3ではこの設定の公開setterがないため、SerializedObject/SerializedPropertyのEditor APIを使う。FontAssetのYAMLは手編集せず、AtlasPopulationMode.Dynamicは維持する。最終APKはこの設定で再生成する。

全4字体版の最終Unity検証ではSetupを2回続けて実行し、2回目の前後で4種すべてのFontAssetのファイルハッシュが不変、エラー0・警告0を確認した。Playを入り直し、iPhone 12とOnePlus 6TのDevice SimulatorでMain、Login、Tutorial1〜3、Homeと設定、Member表裏、Ranking4種、Lobbyの選択・IMO-777エラー・IMO-482参加・準備完了、Waitingの空／満員、Loading途中／完了、GameOverを確認した。成功演出1.5／4.6／9秒、失敗演出4／9秒も含め、全撮影でエラー0・警告0、outsideButtonsは空で、NativeFormatImporter警告の再発なし。証跡は `docs/validation/m7/simulator/iPhone12/` と `OnePlus6T/`。

先行するBlack対策段階でも、新規プロフィールで通常のサーバー操作によるR1→R5成功→R6成功と各結果表示、両SimulatorのResult・Clear・Endlessはエラー0・警告0を確認済み。上記はいずれもEditor/Simulatorの確認であり、Android実機での描画検証とは区別する。

## その他

- ショップの開発注記「購入価格は仮」を除き、操作判断に役立つ「購入価格は売却単価の○倍」だけ残す。
- エンドレスではGame HUDを「エンドレス Rn」にする。R6以降の成功演出は通常Clearへ戻さずResultへ進む。

## 対象テスト

```powershell
dotnet test tools/CoreTests --no-restore --filter 'FullyQualifiedName~ReconnectionTests|FullyQualifiedName~ImoniEffectResultTests|FullyQualifiedName~GameHudProjectionTests|FullyQualifiedName~GatheringSimulationTests.CancelAndDisconnect|FullyQualifiedName~GatheringSimulationTests.TwoBotsGather' --logger 'console;verbosity=minimal'
```

結果：26件成功、失敗0、スキップ0。追加内容は再接続前後の同一ワールド・倉庫・資金・保護・強化保持と仲間の継続、R6/R12の演出経路、エンドレスHUD。既存の切断時採集停止とボット復帰も再実行した。

Unity再コンパイル、390×844の境界・再接続・見出し比較は統合検証で実施する。証跡と最終結果は `docs/validation/m7/` とM7の統合報告を参照。

## MainからR2までの通し録画

Bootシーンを開いてPlayを止め、`Imoni > Development > Record Main to Round 2`、またはEditor API `ImoniM7FlowRecording.Start()` を実行する。新しいLibrary内の隔離プロフィールでPlayを開始し、既存のClickableを使ってMain→Login→Tutorial1〜3→Home→Lobby作成→Waiting→Loading→Game R1→TimeUpResults→Imoni→Result→R2をたどる。

Waitingの2.5秒・5.2秒の参加やLoading、演出は実時間で待つ。Gameだけ既存のshort clock=10を使い、実際のBootstrap.Update/Tickに任せる。自動操作はM6の予算内強化と資源選択を再利用し、2ゲーム秒間隔で意図を送り、末尾の不足購入も所持金の範囲内で行う。Coreの数量・位置・結果を直接代入せず、Tickを直接呼ばない。通常共通seedが `balanceReview.seed` と一致することを開始時に検証する。

`Library/M7-flow-frames/frame_00000.png` から連番を保存する。FPSは `effectReview.frameRate`、画面の滞在は `ui.toastSeconds`、実時間上限は `flowReview.maxSeconds=180`。各画像要求の実時間を `frames.jsonl` / `recording.json` に記録し、実際の間隔を使った `frames.ffconcat` も生成する。間隔が遅れたフレームを25fpsと偽って再生速度を変えない。R2到着後は撮影を終え、以前のshort clock設定を復元する。

`ImoniM7FlowRecording.IsRunning` / `Status()` または `Library/M7-flow-status.json` で状態を確認できる。中止は `Stop()` または `Imoni > Development > Stop Flow Recording`。途中失敗でも撮影済みフレームを残し、成功した結果へ置き換えない。動画の合成と最終証跡の保存は統合検証で行う。

端末切替で描画領域が一瞬0になる場合は、`GameWorldView.ResizeCamera` が実カメラのpixelWidth/pixelHeightを検証し、座標変換と地面入力だけを次の有効フレームまで待つ。CoreのTickは継続する。実時間の再接続デモとSimulator切替を併用して、0.38秒で本人切断・4.09秒で復帰、仲間2人がGatheringへ進んだ状態を撮影した。前後のworldIdentityHashは一致し、エラー/警告0。`proof_Reconnect*` と `reconnect_before/during/after.json` に記録する。
