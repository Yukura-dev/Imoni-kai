# M8 移動経路と連続座標

## 決定

- A* は上下左右に加えて斜め4方向を探索する。移動コストは縦横1・斜め√2、許容的ヒューリスティックは octile distance。同点は推定総距離、残り推定距離、挿入順で決める。近傍挿入順は北・東・南・西・北東・南東・南西・北西。
- 斜め方向は、その方向に隣接する縦横両方のセルが通行可能なときだけ許可する。水と未開放セルに同じ判定を使う。したがって、4方向で連結していなかった領域へ斜め移動だけで侵入することはない。
- A* の経路は見通せる最遠の点まで直線で結ぶ。見通し判定は線分が触れるセルをすべて検査し、タイルの角に触れる場合は縦横両セルも検査する。水・未開放の角を平滑化で抜けることもない。
- 資源に対しては以前と同じく上下左右いずれかの隣接セルで止まり、到着後に採集時間を進める。資源・他の人間は地形障害物に追加しない。
- 直線区間の時間は「区間のユークリッド距離 × 既存の1タイル移動時間」。速度・強化係数は従来どおり GameConfig を参照する。√2、セル境界の0.5、直線の距離は幾何学上の定数で、ゲームバランス用パラメーターではない。
- 途中で別地点をタップしたり、キャンセル・切断・タイムアップした場合も、その瞬間の連続座標を保持する。長い直線区間の始点に戻らない。移動終了は端点へ正確に確定する。
- 移動中の移動速度強化は購入時点で即時反映し、現在位置を保持して次の折れ点までの残距離だけを新速度で再計時する。SPEC 7.1/8.2どおり資金は共有・強化Lvは個人なので、指定actor以外の移動は再計時しない。人間・ボットどちらのactorにも同じ処理を使う。
- 見通し判定は始点・終点がセルの辺や角にある場合も、端点に接する全セルを検査する。線分方向を逆にしても水・未開放への接触判定は変わらない。

## API と利用箇所

- `PathResult.Steps`: A* が得た8近傍のセル列。開始セルと到着セルを含む。到達性・生経路の診断用。
- `PathResult.Waypoints`: 見通し確認後の直線経路。実際の連続開始位置と到着位置を含む。
- `PathResult.DistanceTiles`: Waypoints に沿った合計距離。移動所要時間や近さの比較で Steps.Count を使わない。
- `PlayerSnapshot.Location`: Core の Tick が確定する連続座標。セル中心は整数座標(セル32の中心は32)。描画側はこの値を既存のセル→ワールド座標式へ渡し、さらに Position/NextPosition で補間しない。
- `PlayerSnapshot.Position`: 現在の連続座標を含むセル。`NextPosition` は移動中の次の直線区間の終点セル。
- サーバー・ボットの資源選択・目標生成の到達可能量シミュレーションを実距離へ変更。ボット候補の枝刈り下限は「資源までの直線距離−採集到達距離1」に変更。
- 見込み強化・未強化比較の Snapshot も同じ Location を引き継ぎ、ラウンド境界で途中の位置をセル中央に丸めない。

## 回帰テスト

追加・変更した主要ケース:

| テスト | 確認内容 |
|---|---|
| EightDirectionSearchUsesDiagonalCostAndSmoothsOpenGroundToOneStraightSegment | 斜め√2で最短経路、空地では始点→目的地の直線1区間 |
| DiagonalSearchAndSmoothingCannotCutEitherWaterCorner (3通り) | 縦横どちらか、または両方が水なら角抜け不可 |
| DiagonalDoesNotEscapeATileSurroundedByWater | 四方が水の場所から斜めだけで脱出不可 |
| DiagonalDoesNotCutAnUnopenedCorner | 未開放セルの角抜け不可 |
| EightDirectionReachabilityMatchesOriginalCardinalConnectedComponent (R1/R3/R5) | シード482の格子サンプルで従来の4近傍BFS連結成分と到達判定が一致 |
| OpenGroundTapMovesContinuouslyOnTheStraightLineAtConstantSpeed | Core Tick の途中座標が直線上を一定速度で移動 |
| RetapDuringMovementStartsFromActualLocationWithoutSnappingBack | 途中リタップで巻き戻らず、実座標から戻る |
| CancelAndDisconnectPreserveTheContinuousPosition | キャンセル・切断でも連続位置を保持 |
| MovementUpgradeImmediatelyRetimesOnlyThePurchasingActorsRemainingStraightLeg (player1/player2) | 長い直線の途中での強化を即反映し、同時に移動中の他メンバーは変更しない |
| InFlightMovementUpgradeProducesTheSameArrivalForWholeOrPartitionedTicks | 強化前後を1回/複数回のTickで進めても途中位置と到着が一致 |
| LineOfSightChecksEveryTileTouchedByACornerEndpointInBothDirections (3通り) | 角の端点に接する水を全方向で拒否 |
| LineOfSightChecksBothSidesOfAnEdgeEndpointInBothDirections (2通り) | 辺の端点に接する水を両方向で拒否 |
| LineOfSightAllowsWalkableCornerAndEdgeEndpointsInBothDirections | 障害のない角・辺の端点は両方向で到達可能 |

橋経由・到着後のみ採集・資源在庫/再生・ボット採集・決定性・Tick分割不変・R1〜R5目標の達成可能スケジュールなど既存テストを、移動時間について実距離へ更新した。

初回の関連テスト実行: `dotnet test tools/CoreTests --no-restore --filter "FullyQualifiedName~WorldGenTests|FullyQualifiedName~MoveCommandTests|FullyQualifiedName~GatheringSimulationTests|FullyQualifiedName~ObjectiveInflationTests|FullyQualifiedName~ObjectiveUpgradeForecastTests|FullyQualifiedName~RoundFlowTests"` → **129件成功 / 失敗0 / 29.218秒**。全体の最終実行結果は M8 検証記録を参照。

## 連番撮影の再現条件

1. シード482、R1、プレイヤーの移動強化Lv0で開始。開始地点は (32,32)。
2. 地面 (34,33) に移動指示する。経路は始点と目的地だけの2点で、水・未開放領域を通らない。
3. 0.0、0.1、0.2、0.3、0.4、0.5、0.6秒を撮影する。総所要時間は約0.511103秒で、最終コマは停止している。撮影時の補間座標は PlayerSnapshot.Location に従う。
4. 表示は斜めの直線で、横移動→縦移動の折り返しがないことを確認する。

## 仕様判断の記録用メモ

途中リタップで「セル中央へ瞬間移動して経路を引き直す」ことは避け、Core に連続座標を追加した。既存の人間同士の重なり可、資源への隣接停止、採集時間、共有倉庫の確定経路は維持する。目標シミュレーションも同じ距離を使うため、平滑化で実移動だけ短くなって達成可能量の計算と食い違うことを防ぐ。
