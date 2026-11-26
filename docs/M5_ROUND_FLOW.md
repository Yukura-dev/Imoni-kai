# M5 ラウンドの流れと演出

ブランチはレビュー済み `rebuild/m4-panels` の `aed4468` から作成した `rebuild/m5-round-flow`。M5はタイムアップから演出・結果・次ラウンド／再挑戦までを接続し、5連続成功で通常クリアする。M6のAudioService／AudioMixer統合、M7のランキング保存・バッジ条件には進まない。

## 開き方と遷移

1. Play Modeを終了し、`Imoni > Setup Project` を実行する。Bootの参照に演出UXML・USS・既存Effect画像・既存の演出音源が登録される。
2. `Imoni > Open App` からHome → Lobby → Waiting → Loadingへ進み、「ラウンド1 スタート!」を押す。
3. Gameで採集・購入・売却などを行い、5:00まで進める。Coreが期限で操作を止め、評価・報酬・累積スコアを一度だけ確定する。パネルが開いていれば閉じ、タイムアップダイアログを表示する。
4. ダイアログのボタンから芋煮会演出へ進む。成功・失敗は確定済みの `RoundResult` で分岐する。演出の「スキップ」は該当の結果画面へ進み、「もう一回見る」は同じ結果・シードの演出を再生する。再評価・再支給は行わない。
5. R1〜R4の成功はResult → 「ラウンドnへ」→ Game。失敗はGameOver → 「R1から再挑戦」→ Loading → Game。GameOverからHomeへ戻ることもできる。
6. R5成功はClearへ進み、「ホームへ戻る」でHomeへ戻る。ClearはResultと同じ部品を使った通常クリア表示で、R6へは進まない。

| 条件 | ルート | 次の操作 |
|---|---|---|
| R1〜R4成功 | Gameのタイムアップ → Imoni → Result | `IGameServer.StartNextRound()` → Game |
| 失敗 | Gameのタイムアップ → ImoniFail → GameOver | `IGameServer.Retry(newSeed)` → Loading → Game、またはHome |
| R5成功 | Gameのタイムアップ → Imoni → Clear | Home |

次ラウンドでは共有資金、個人強化、同じワールド、資源の在庫・再生成長、メンバー位置・接続状態を引き継ぎ、解放範囲を広げる。在庫と保護、連続数は成功確定時に消費・リセットされる。演出・結果表示中は時間と資源成長を停止する。

失敗時は進行中の挑戦を初期化するが、終了したラウンド番号・到達数量・不足素材・累積スコア・履歴は `LastResult` のコピーに残す。GameOverと演出はこのコピーを読み、新しいR1の空の倉庫から表示値を作らない。再挑戦は直前と異なるシードを選び、資金・強化・在庫・保護・連続・累積スコアを初期状態にする。

## 開発用の短縮確認

Unityの `Imoni > Development > Short Rounds (GameConfig)` で切り替える。既定はOFF。ONのときだけ、既存の `round.mockTimeScale=10` をGameのCore時間進行へ適用する。5分のラウンドを実時間約30秒で確認でき、移動・採集・ボット・資源成長も同じ倍率で進む。演出とアウトゲームの読み込みは実時間のまま。

Editorの切り替えは `SessionState` に保持する。Development Buildでは起動引数 `--imoni-short-rounds` を指定した場合にだけ有効になる。通常ビルドはコンパイル条件で無効にし、設定値や起動引数にかかわらず常に1倍で動く。確認後はメニューのチェックを外す。

## Coreの結果と共通部品

`RoundResultProjection` が必須素材の実績、ボーナス達成率と段階、ランク、R/B/Tの内訳、最終スコア、獲得資金、累積スコア、不足素材、次の操作と二つ名報酬を返す。UIはこの値を既存のCard／MaterialGoalChip／ProgressBar／BonusTierRow／ResultSummary／ImoniButtonなどへ渡す。ResultとClearは共通のResultScreenを使う。

ランクは既存 `rankGrades` のS:300%／A:150%／B:100%／C:それ以外。判定はBigIntegerの整数比較で行い、丸めた表示百分率を使わない。R/Bの端数も確定前の固定小数値を保持して表示する。スコアは既存の `0.5R + 0.5B×T` を合算後に一度だけ切り捨て、売却済みロットは除外する。数量・資金・スコアをfloatへ変換しない。

暫定の二つ名条件を `titles.unlockRules` に置く。初成功で後半「鍋奉行」、5連続成功で前半「伝説の」を獲得する。既存の所持パーツは維持し、取得済みのパーツは重複付与しない。旧保存データには初期所持を補い、新しい所持配列を既存プロフィールファイルへ保存する。Memberの選択肢にも反映する。モックで初期所持になっている「連続の」は維持し、報酬に再利用しない。全パーツ・バッジの条件整理はM7に残す。

## 演出

座標・速度・閾値は `IMONI_EFFECT_SPEC.md` の画面座標式と `GameConfig.imoniEffect` を使い、提供済みの `Art/Sprites/Effect/` 画像を再利用する。食材・星・輪・流線・きらめきは部品を事前生成してプールし、再生中に要素を増やさない。プール容量は最短発生間隔・最長寿命・最大段階からCoreで求める。

演出は確定済みスコアを時間に配分する。フレームを飛ばしてもイベント時刻と表示値が一致し、成功・失敗とも10秒以内に結果ボタンを出す。ボタンが表示される時点ではカウントアップ値も最終スコアに一致する。スコア1／18,420／10^15に加え、失敗スコア0も検証している。

失敗版の「成功した場合の最終スコア」は実ゲームに一意の値がないため、不足する必須素材の品質換算数量だけを、普通品質・連続倍率なしで補った仮の成功スコアを閾値の基準にする。取得済み必須ポイント・ボーナスポイント・段階倍率は保持し、基準は実スコア以上かつ最低1にする。実際に表示するスコアと報酬は変更しない。これは旧プロトタイプの「失敗スコア÷0.4」という仮定をM5で置き換える判断である。失敗の段階上限は設定どおりLv.3、不足素材名は確定結果から取得する。

既存の演出音源と音ON/OFF保存をBootの演出へ接続した。全ゲームのAudioService／AudioMixer化はM6の対象。

## Coreテスト

実行コマンド：

```powershell
dotnet test tools/CoreTests --no-restore --logger 'console;verbosity=minimal'
```

全300件成功、失敗0、スキップ0（コンソール記録、テスト期間29秒）。M4の274件に26ケースを追加した。`RoundFlowTests` は17ケース、`ImoniEffectResultTests` は4ケース、既存 `ImoniEffectTests` への追加は5ケース。既存の10秒上限テストには最終表示スコアの一致確認も加えた。演出だけの `--filter 'FullyQualifiedName~ImoniEffect'` も23件すべて成功した。

| テスト | ケース数 | 確認内容 |
|---|---:|---|
| `RoundFlowTests.ReplayingOrReadingSettledResultNeverPaysScoreOrFundsAgain` | 1 | 結果の再表示・時間進行で報酬と累積スコアを再加算しない |
| `DeadlineSnapshotConsumesAllLotsAndExcludesSoldStockFromRAndB` | 1 | 保護・無関係素材も終了時に消費し、売却分をR/Bから除く |
| `NextRoundPreservesWorldGrowthMoneyUpgradesAndExpandsAreaWhileClearingRunStock` | 1 | 同じワールド・成長・資金・強化を引き継ぎ、解放拡大と在庫／保護／連続リセットを行う |
| `FailureProjectionKeepsHistoricalRoundAndCumulativeEvenAfterActiveResetAndRetry` | 1 | 失敗後の初期化・別シード再挑戦を経ても確定結果が変わらない |
| `FiveSuccessesExposeClearWithoutR6AndOnlyCompletedRoundScoresAccumulate` | 1 | 5成功の合算とClear、R6／失敗用Retryの拒否 |
| `ResultRankUsesConfiguredExactBonusThreshold` | 6 | 99/100/149/150/299/300%のランク境界 |
| `ResultRankDoesNotRoundUpBelowThresholdAndReadsChangedGradeConfig` | 1 | 設定変更を読み、境界未満を丸め上げない |
| `FractionalLotPointsRemainVisibleAndFinalScoreStillUsesOneFinalFloor` | 1 | R/Bの端数表示とスコアの最後の切り捨て |
| `HugeResultProjectionDoesNotConvertScoresOrBonusRatesThroughFloatingPoint` | 1 | 10^100の結果を整数で扱う |
| `SuccessfulTitleRewardIsPersistedIdempotentlyWithoutRemovingExistingOwnership` | 1 | 報酬保存・再読込・二重付与防止・旧所持維持 |
| `InvalidOrDuplicateSavedTitlePartsAreRemovedAndOldSavesKeepInitialOwnership` | 1 | 不正／重複パーツの除去と旧セーブ互換 |
| `TitleStreakEligibilityUsesTheSelectedActorsCurrentRoundAndRejectsFailure` | 1 | 条件が指定プレイヤー・当該ラウンドに限定され、失敗では付与しない |
| `ImoniEffectResultTests.SuccessfulEffectUsesSettledScoreAndRoutesWithoutAnotherSettlement` | 2 | R1→Result、R5→Clear、実スコアとシードの受け渡し |
| `FailureReferenceAddsOnlyRequiredDeficitsAndKeepsActualScoreAndMissingNames` | 1 | 不足分だけを補う参照値、実スコア・素材名を保持 |
| `FailurePresentationSurvivesAutomaticRunResetAndDoesNotMutateServer` | 1 | 失敗初期化後も同じ演出結果を読み、サーバーを変更しない |
| `ImoniEffectTests.EveryScoreShowsResultWithinTenSeconds` 追加ケース | 1 | 失敗18,420も10秒以内、最終表示値まで一致 |
| `EveryPresentedFrameIsDeterministicWhenFramesAreSkipped` | 2 | 成功／失敗のイベント・光・揺れ・ゲージ・スコアがフレーム間隔に依存しない |
| `ConfigDerivedPoolsCoverAllLiveItemsBurstsAndRings` | 2 | 成功／失敗、5シードで同時表示数がプール容量以内 |

この表の修飾名省略行は、直前に示したテストクラスに属する。

## Unityでの確認結果

Unity再コンパイルとSetup、390×844の実画面で成功→次R／失敗→新シードのLoading→R1／5成功→Clear→Homeを確認し、エラー0・警告0。保存した状態と動画の23項目検証も全件成功した。比較画像7枚、成功・失敗の録画、参考動画を左右に並べた比較を `docs/validation/m5/` に保存した。

演出の結果ボタンは比較シードで成功8.582秒・失敗8.951秒に表示される。仕様の座標・レベルアップ式・10秒上限を維持している。指定時刻の静止見本と参考動画には状態の差があるため、同一フレームとは扱わず、参考動画冒頭の白画面を除いた比較も添付した。詳しくは [検証記録](validation/m5/README.md) と [演出の照合](validation/m5/effect-comparison.md) を参照。
