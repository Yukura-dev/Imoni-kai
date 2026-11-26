# M3b Game画面の接続

Game画面は `Assets/Imoni/Resources/UI/Game.uxml` と `GameScreen.uss` で構成する。`FundsPill`、`MaterialGoalChip`、`ImoniButton`（Action）、`Toast`、`ImoniText` はM2a/M2bの共通部品を再利用する。倉庫・ショップ・強化・メニューの4ボタンは外観のみで、パネルと操作はM4に残す。

## 起動と時間の所有者

1. Loading画面が `ScreenContext.PrepareGame()` を呼び、GameConfigとシードから `LocalGameServer` を作る。共通シードは `outGame.lobby.commonSeed`、自由シードは開始時のUTC ticksから決める。生成後のワールド・目標・採集抽選はCoreのシード付き乱数のみを使う。
2. 「ラウンド1 スタート!」でRouterのGameへ進む。Loadingの資源数は生成したワールドの実数を表示する。
3. `Bootstrap.Update` がGame画面の `Advance(Time.unscaledDeltaTime)` を1回呼ぶ。`Advance` が唯一の自動Tick呼び出し元で、Coreを進めてからWorldとHUDを更新する。
4. ラウンド終了後はTickを止め、Game画面に留まる。失敗後にCoreが初期化されても、タイマーは0:00、目標は `LastResult` の最終達成量を示す。演出への遷移はM5で接続する。

## Worldとの境界

`GameScreen` はUIパネルにAttachされた後、次を生成する。

```csharp
new GameWorldView(server, bootstrap, mapViewport, profile)
```

`GameWorldView.Present(snapshot, deltaSeconds)` は表示のみを担当し、Tickを呼ばない。画面破棄時に `Dispose()` する。World側が資源・キャラ・採集バー・成長ラベル・名札・獲得ポップアップ・鍵表示・ワールド入力を担当する。HUD側はタイマー・共有資金・必須・ボーナス・最高品質の連続表示・履歴Toastを担当する。

マップ要素と重ねる表示要素は `PickingMode.Ignore`。World側はUIDocumentのルートでPointerDownを受け、マップの矩形内だけをワールド座標へ変換する。HUDと下ボタンの操作はワールドへ渡さない。Game中はThemeルートを透明にしてTilemapを透過表示し、画面を離れると元へ戻す。

## モック対応

| 表示 | 共通部品／データ | モック対応 |
|---|---|---|
| 上部200pxのHUD | ImoniText / FundsPill | Game.dc.html header（46px上余白、ラウンド・34pxタイマー・共有資金） |
| 必須素材 | MaterialGoalChip | Game.dc.html 必須ラベル＋3列、達成時の緑色 |
| ボーナス | ImoniText | Game.dc.html クリームの34px枠。2分未満は発表待ち、公開後は素材・数量・達成率・報酬倍率 |
| 連続表示 | ImoniText | Game.dc.html マップ左上10pxの茶色ピル |
| 採集履歴 | Toast | Game.dc.html マップ下12pxのクリーム枠 |
| 下部84px | ImoniButton Action | Game.dc.html 倉庫・ショップ・強化・メニュー |

UIの計算は `Core/GameHudProjection.cs` に集約し、品質換算数量は `QualityDefinition.Multiplier`、目標表示は `DisplayProjection.Goal`、連続倍率は `StreakCalc`、巨大数は `Format.Big` を使う。ポイント倍率を数量へ二重計上しない。表示精度はGameConfigのformat、タイマーの赤色閾値は `ui.timerUrgentSeconds`、Toastの時間は `ui.toastSeconds` から読む。

## 検証用の公開API

`GameScreen.Server`、`Snapshot`、`Hud`、`WorldView`、`LocalActorId` は読み取り専用。Editorの `ReviewPaused = true` は自動Advanceを止める。記録用には `Server.Tick(seconds)` の後で `RefreshSnapshot(visualDeltaSeconds)` を呼ぶ。これは追加のTickをせず、同じ時刻の表示だけを更新する。Bootstrapがない独立したUI検証ではWorldを作らずHUDを表示する。

`GameHudProjectionTests` は品質換算・ボーナス公開境界・倍率の境界・巨大数・残り時間・個人の連続倍率・履歴文言・失敗リセット後の0:00を検証する。13件。
