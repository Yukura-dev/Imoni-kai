# M1 Core 完了記録 — 2026-11-25

正本：`D:/UnityProjects/Imoni-kai`。ブランチ：`rebuild/m1-core`。M2には未着手。

## 実装

| ファイル | 役割 |
|---|---|
| `GameConfig.cs` | GameConfig.jsonの型付き読み込み・入力検証・読み取り専用の設定 |
| `Rng.cs` / `QualityRoller.cs` | シード再現可能な乱数、解放済み品質のみの抽選、採集運の反映 |
| `GameMath.cs` / `StreakCalc.cs` | BigIntegerの整数比・n乗根、連続倍率、獲得数量 |
| `Inventory.cs` | 取得時倍率を保持するロット、種類/品質別の保護数量、FIFO売却 |
| `UpgradeRules.cs` | 強化価格の四捨五入、移動/採集速度/採集量の設定駆動計算 |
| `Objectives.cs` / `Scoring.cs` | 品質換算数量、必須判定、ボーナス段階、売却済み除外、スコアと報酬 |
| `LocalGameServer.cs` | IGameServer、採集・購入・売却・保護・強化の確定、時間境界、履歴、不変スナップショット |
| `Format.cs` | 巨大整数を万・億・兆などで表示 |

上記はすべて `Assets/Imoni/Scripts/Core/`。UnityEngineを参照せず、C#9でコンパイルする。UIはサーバーのスナップショットを読み、変更はIGameServerのコマンドで行う構造。

## 状態と接続範囲

- `new LocalGameServer(config, seed)` がR1を開始し、開始イベントと必須目標を公開する。
- `Tick(decimal秒)` が2:00でボーナス公開、5:00で採集/売買を止めて判定する。時間を大きく進めても境界イベントは順番どおり1回ずつ。
- 成功は在庫/保護/連続を消費・リセットして結果を保持。資金/強化/seed/累積スコアを引き継ぎ、`StartNextRound` で次へ進む。5回目の成功でCleared。
- 失敗はR1/初期資金/強化0/連続0/在庫0/保護0/累積0へ即時初期化。Failed状態では操作を拒否し、LastResultに結果を保持。`Retry(newSeed)` で再開。
- 目標は注入可能。未指定ではGameConfigのround1Mockを使用する。到達可能な資源からの目標生成、タップ移動、資源残量、ボットはM3。
- M1のGatherは採集完了の確定入口。ワールド側の作業時間・到達判定はM3で接続する。BootにUIやゲーム操作を接続する作業はしていない。

新しい調整値・端数処理・報酬の仮決定は [DECISIONS.md](DECISIONS.md) と [QUESTIONS.md](QUESTIONS.md) に記録。更新されたCOMPONENTS/演出仕様を読んだ上で、部品実装や録画照合は該当マイルストーンへ残す。配布の演出画像・動画・UI_SPEC・IMONI_EFFECT_SPECは既存と同一内容だったため保持した。

## 検証結果

実行：

```powershell
Set-Location -LiteralPath 'D:/UnityProjects/Imoni-kai'
dotnet test tools/CoreTests --nologo --logger 'trx;LogFileName=m1.trx' --results-directory Library/M1Tests
```

**116件成功、0件失敗、0件スキップ。追加101件、既存15件。** 正確な全ケースは [validation/m1/tests.json](validation/m1/tests.json) に保存。

| テストクラス | ケース数 | 区分 |
|---|---:|---|
| RulesMathTests | 43 | 追加 |
| InventoryScoringTests | 30 | 追加 |
| LocalGameServerTests | 28 | 追加 |
| SmokeTests | 1 | 既存 |
| ImoniEffectTests | 14 | 既存 |

- Unity 6000.3.21f1のNewtonsoft DLLを参照し、netstandard2.1/C#9でCoreをビルド：警告0・エラー0。
- 実際のUnityでRefreshし、Core/Game/Editor/Testsの再コンパイルと新規.meta生成を確認。Consoleはエラー0・警告0。
- Theme.ussの先頭タブは `.tabbar__item--first`。非対応の構造擬似クラスをUSS全体から検索し、該当なし。

## UNITY_GUIDE 10章との対応

| 必須項目 | 主なテスト |
|---|---|
| 保護数量を下回る売却の拒否（15-5） | `SaleBelowProtectionIsRejectedWithoutChangingAnyLot` / `SaleBelowProtectionRejectsAtomicallyAcrossSharedActors` |
| 同seed/同操作の品質と連続（15-7） | `SameSeedAndOperationSequenceReproduceQualityStreakQuantityAndPoints` |
| 2:00の公開イベント（15-8） | `BonusIsHiddenUntilExactlyTwoMinutesAndRevealedOnlyOnce` |
| 5:00で採集停止・判定（15-9） | `FiveMinutesStopsGatheringAndEvaluatesTheInventory` / `EveryInventoryOrFundsCommandIsRejectedAfterTimeUp` |
| 不足時R1へ戻り全初期化（15-10） | `FailedSecondRoundResetsRunAndKeepsDetachedFailureResult` |
| 5連続成功でクリア（15-11） | `FiveConsecutiveSuccessesClearAndSumOnlyCompletedRoundScores` |
| 売却済み素材をスコアから除外（15-12） | `SoldMaterialsAreExcludedFromRequiredAndBonusScore` / `SoldBonusMaterialCannotReturnInTheFinalScore` |
| 品質換算/連続/強化/段階/スコア式 | `QualityConversionFollowsChangedConfiguration` / `FractionalStreakIsFloorOfTheExactIntegerRoot` / `UpgradePriceMatchesMockHalfUpRounding` / `BonusTiersUseExactInclusiveThresholds` / `ScoringAndSalePricesFollowChangedConfiguration` |
| 演出が10秒以内 | 既存 `EveryScoreShowsResultWithinTenSeconds`（成功1/18,420/10^15、失敗1/7,350/10^15/0） |

## エディタで再確認する手順

1. 正本をUnity 6000.3.21f1で開き、取り込み後のConsoleにコンパイルエラーがないことを確認。
2. `Window > General > Test Runner` のEditModeでImoni.Testsを選び、Run Allを実行する（同じテストをUnityから実行可能）。今回の完了判定に使った結果は上記dotnet test。
3. Bootは引き続き土台のみで空画面。M1では新規シーン・Prefab・UIを生成していないため、Setupの再実行は不要。
4. M2開始時はCOMPONENTSに従って共通部品とComponentGalleryから着手し、TabBarの先頭タブへ `tabbar__item--first` を付ける。

## 追加テスト一覧（パラメータ展開後）

### RulesMathTests（43件）

- `BigNumberFormattingUsesJapaneseUnits("-1234567890123","-1.23兆")`
- `BigNumberFormattingUsesJapaneseUnits("0","0")`
- `BigNumberFormattingUsesJapaneseUnits("10000","1万")`
- `BigNumberFormattingUsesJapaneseUnits("100000000","1億")`
- `BigNumberFormattingUsesJapaneseUnits("1000000000000","1兆")`
- `BigNumberFormattingUsesJapaneseUnits("123456","12.34万")`
- `BigNumberFormattingUsesJapaneseUnits("9999","9,999")`
- `ConfigLoadsBigIntegerAmountsWithoutDoublePrecisionLoss`
- `DuplicateIdsAndUnknownPurchasedQualityAreRejected`
- `FormatterUsesConfigAndKeepsVeryLargeIntegers`
- `FractionalStreakIsFloorOfTheExactIntegerRoot`
- `GatherQuantityFloorsOnlyAfterBothMultipliers`
- `InvalidConfigFailsBeforeStartingGame("economy.startFunds",-1)`
- `InvalidConfigFailsBeforeStartingGame("format.decimalPlaces",-1)`
- `InvalidConfigFailsBeforeStartingGame("format.unitBase",1)`
- `InvalidConfigFailsBeforeStartingGame("numeric.fixedScale",0)`
- `InvalidConfigFailsBeforeStartingGame("qualityRoll.luckPerLevel",-1)`
- `InvalidConfigFailsBeforeStartingGame("qualityRoll.topBaseChance",2)`
- `InvalidConfigFailsBeforeStartingGame("round.bonusRevealSec",300)`
- `InvalidConfigFailsBeforeStartingGame("round.durationSec",0)`
- `InvalidConfigFailsBeforeStartingGame("streak.divisor",0)`
- `LargeStreakAndYieldNeverPassThroughFloatingPoint`
- `MaxLuckAlwaysChoosesHighestUnlockedQuality(1,"rare")`
- `MaxLuckAlwaysChoosesHighestUnlockedQuality(2,"legend")`
- `MaxLuckAlwaysChoosesHighestUnlockedQuality(3,"myth")`
- `MaxLuckAlwaysChoosesHighestUnlockedQuality(4,"transcend")`
- `MaxLuckAlwaysChoosesHighestUnlockedQuality(5,"ultimate")`
- `MissingNumericValueDoesNotSilentlyUseADefault`
- `NegativeLevelsAndStreaksAreRejected`
- `QualityProbabilityUsesConfigAndLowerWeights`
- `RandomSamplingHandlesNegativeSeedsAndBigIntegerBounds`
- `RandomSequenceHasStableGoldenValues`
- `SameSeedAndInputsRepeatTheWholeQualitySequence`
- `StreakUsesConfiguredFractionalExponent(0,"1000000")`
- `StreakUsesConfiguredFractionalExponent(1,"1071773")`
- `StreakUsesConfiguredFractionalExponent(10,"2000000")`
- `StreakUsesConfiguredFractionalExponent(20,"4000000")`
- `UnlimitedSpeedRetainsAPositiveRepresentableDuration`
- `UpgradePriceAndEffectsFollowChangedConfigAtHugeLevels`
- `UpgradePriceMatchesMockHalfUpRounding(0,"150")`
- `UpgradePriceMatchesMockHalfUpRounding(1,"225")`
- `UpgradePriceMatchesMockHalfUpRounding(2,"338")`
- `UpgradePriceMatchesMockHalfUpRounding(3,"506")`

### InventoryScoringTests（30件）

- `BonusThresholdDoesNotRoundUpLargeFractionalPercentages`
- `BonusTiersFollowChangedConfiguration`
- `BonusTiersUseExactInclusiveThresholds(0,0)`
- `BonusTiersUseExactInclusiveThresholds(100,1)`
- `BonusTiersUseExactInclusiveThresholds(1000,8)`
- `BonusTiersUseExactInclusiveThresholds(149,1)`
- `BonusTiersUseExactInclusiveThresholds(150,2)`
- `BonusTiersUseExactInclusiveThresholds(199,2)`
- `BonusTiersUseExactInclusiveThresholds(200,4)`
- `BonusTiersUseExactInclusiveThresholds(299,4)`
- `BonusTiersUseExactInclusiveThresholds(300,8)`
- `BonusTiersUseExactInclusiveThresholds(99,0)`
- `BothScoreTermsAreAddedBeforeRounding`
- `DecimalScoreWeightsKeepFullPrecisionWithHugeQuantitiesAndCoarseLotScale`
- `FailureReportsAllMissingMaterialsAndPaysNoReward`
- `FractionalLotPointsAreCombinedBeforeScoreIsRounded`
- `HugeQuantitiesAndProceedsKeepExactIntegerPrecision`
- `InvalidAcquisitionOrSaleDoesNotMutateInventory`
- `InvalidProtectionLeavesPreviousProtectionUnchanged`
- `InventorySnapshotsCannotBeChangedAndDoNotTrackLaterSales`
- `IrrelevantMaterialsDoNotContributeToScore`
- `ObjectivesRejectInvalidQuantitiesDuplicatesAndOverlap`
- `ProtectedMaterialsStillMeetObjectivesAndScoreAndAreClearedForThePot`
- `QualityConversionFollowsChangedConfiguration`
- `QualityEquivalentIgnoresPointMultiplierAndIncludesAllQualities`
- `SaleBelowProtectionIsRejectedWithoutChangingAnyLot`
- `SaleConsumesOldestMatchingLotsAndKeepsHistoricalPointMultipliers`
- `ScoringAndSalePricesFollowChangedConfiguration`
- `SellingDifferentQualityDoesNotConsumeProtectedOrOtherQualityLots`
- `SoldMaterialsAreExcludedFromRequiredAndBonusScore`

### LocalGameServerTests（28件）

- `BonusIsHiddenUntilExactlyTwoMinutesAndRevealedOnlyOnce`
- `ClearCountAndRoundObjectivesCanBeSuppliedFromUpdatedRules`
- `ClientSnapshotsCannotMutateServerOrChangeAfterLaterCommands`
- `ConcurrentSalesCannotSpendTheSameLotTwice`
- `EveryInventoryOrFundsCommandIsRejectedAfterTimeUp(Buy)`
- `EveryInventoryOrFundsCommandIsRejectedAfterTimeUp(Gather)`
- `EveryInventoryOrFundsCommandIsRejectedAfterTimeUp(Sell)`
- `EveryInventoryOrFundsCommandIsRejectedAfterTimeUp(SetProtection)`
- `EveryInventoryOrFundsCommandIsRejectedAfterTimeUp(Upgrade)`
- `FailedSecondRoundResetsRunAndKeepsDetachedFailureResult`
- `FiveConsecutiveSuccessesClearAndSumOnlyCompletedRoundScores`
- `FiveMinutesStopsGatheringAndEvaluatesTheInventory`
- `InvalidPurchaseOrSaleQuantityCannotChangeTheRun(-1)`
- `InvalidPurchaseOrSaleQuantityCannotChangeTheRun(0)`
- `LargeTickEmitsOrderedBoundaryEventsAtTheirExactTimes`
- `LifecycleAndMemberValidationRejectInvalidTransitions`
- `NegativeTickIsRejectedWithoutChangingTime`
- `NewChallengeUsesConfigAndPublishesRequiredGoalsAtZero`
- `NonHighestGatherResetsOnlyThatPlayersStreak`
- `OperationsRecordActorKindQuantityFundsAndSimulationTime`
- `PurchaseAndUpgradeInsufficientFundsLeaveEveryValueUntouched`
- `RejectedCommandsDoNotConsumeRandomnessOrChangeFunds`
- `SaleBelowProtectionRejectsAtomicallyAcrossSharedActors`
- `SameSeedAndOperationSequenceReproduceQualityStreakQuantityAndPoints`
- `SoldBonusMaterialCannotReturnInTheFinalScore`
- `StreakIsPerPlayerSurvivesMaterialChangesAndDoesNotAffectPurchases`
- `SuccessConsumesLockedAndUnrelatedItemsButKeepsFundsUpgradesAndSeed`
- `TimerUsesChangedConfigAndIgnoresDevelopmentTimeScale`
