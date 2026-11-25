# M3a — ワールド・採集のCore

作業ブランチ：`rebuild/m3a-world-core`。実装は `Assets/Imoni/Scripts/Core/` の純C#（C# 9）に限定。UI・Tilemap・Game画面はM3bで接続する。

## 実装と入口

| 項目 | API・動作 |
|---|---|
| ワールド生成 | `WorldGen.Generate(config, seed)`。64×64、16×16チャンク、中心拠点、シード付き補間ノイズ、平野/森林/河川/山岳/湿地（既存ID `swamp`）。河川を橋で横断できる |
| 解放範囲 | `WorldMap.IsUnlocked(position, round)`。拠点からの同心円。R1〜5半径10/17/24/32/46。R5で全域を含む |
| 経路 | `Pathfinding.FindPath` / `FindPathToResource`。上下左右のA*、水と未開放を回避。`Reachable=false`、空の`Steps`で選択不能を表す |
| 資源への到着 | 資源の上下左右の隣接タイルで止まる。魚は岸か橋から採集。生成時に全ノードの到達可能性を確認する |
| 在庫・成長 | `ResourceNode`。3回で尽き、40秒で再生。切り株→苗木→若木→成木→採集可能。資源の変更メソッドはinternal |
| 状態取得 | `IGameServer.Snapshot()`。プレイヤーの現在/次タイル、状態、対象、残り時間/全時間、接続状態、資源状態のコピーを返す |
| 選択 | `CanSelectResource(actorId,nodeId)` / `TrySelectResource(...)`。未開放、経路なし、成長中、切断中、時間切れでは選べない |
| 時間進行 | `Tick(decimal seconds)`。タイル移動→到着→採集時間→確定→在庫があれば継続。画面のフレーム数に依存しない |
| 中断・接続 | `CancelAction(actorId)`、`SetConnected(actorId,bool)`。未完了の採集作業を破棄。再接続後は再選択可能 |
| ボット | 先頭以外のメンバー（標準2人）が設定間隔で最寄りの採集可能資源を選択。同じ`IGameServer`コマンドと時間進行を通る |
| 目標生成 | `ObjectiveGenerator.GeneratePlan`。必須3種類とボーナス1種類を重複なしで選ぶ。`SimulatedCapacities`で数量の根拠を取得できる |
| 品質 | `QualityRoller`にバイオーム・素材・ノード補正を追加。採集運係数は`upgrades.items`の`luck.effectPerLevel`のみ |

`new LocalGameServer(config, seed)`でワールド・R1目標・ボットを生成する。`StartNextRound()`もそのラウンドの到達可能な資源・現在の資源状態・強化から目標を生成し、`round1Mock`を使わない。テストや別ルールのため明示目標・`enableBots:false`を渡せるが、即時取得の抜け道はない。`Gather`を直接呼んでも、Tick内の完了認可なしでは拒否される。

```csharp
IGameServer server = new LocalGameServer(config, seed);
if (server.CanSelectResource("player1", resourceId))
    server.TrySelectResource("player1", resourceId);
server.Tick(elapsedSeconds);
GameSnapshot state = server.Snapshot();
```

## 時間・再現性

- 乱数は既存のSplitMix64。地形・目標・採集は別の乱数列とし、生成が採集抽選の乱数を消費しない。同時完了はコンストラクタに渡したメンバー順、同距離のボット候補は資源ID順。
- 5:00と採集完了が同時なら時間切れを先に扱う。入手履歴には確定したシミュレーション時刻が入る。
- 移動・採集速度の強化は次のタイル/次の採集作業から反映。採集量・運は確定時に参照。
- 成功時は同じWorldと資源残量・成長経過・プレイヤー位置を保持。結果待ちでは時間を進めず、次ラウンドで成長を再開する。失敗は挑戦状態を初期化し、`Retry(newSeed)`で新しいマップを作る。
- 人同士は障害物にしない。資源本体も経路の障害物ではなく、採集の到着点を隣接タイルに限定する。

## 目標数量の根拠（仕様9.2）

到達可能な素材候補をシード付きで並べ替え、必要数を選ぶ。チーム全体のラウンド時間を必須・ボーナスで等分し、各区間内で「現在位置から拠点への帰還、拠点と資源の往復、強化後の採集、共有在庫、再生待ち」を保守的にシミュレーションする。素材ごとに5分を重複配分しない。ボーナス区間は公開時刻より前に開始しない。

品質・連続ボーナスを加算しない普通品質の入手可能量を基準に、`ceil(cap×0.4)`〜`floor(cap×0.7)`の整数を抽選する。成長中の資源も再生待ち込みで扱い、実際のノードを変更しない。不適切な候補は除外して選び直し、必要な種類を確保できない設定では明確なエラーを返す。無限の再抽選はしない。これは適切な操作を前提とした目標生成で、最寄りを採るボットが自動で目標を達成する保証ではない。

## 調整する場所

数値はすべて `Assets/Imoni/Config/GameConfig.json`。

| 設定 | 内容 |
|---|---|
| `world` | サイズ、チャンク、タイル32px、解放半径、ノイズ、河川、橋、配置確率、初期最低供給 |
| `materials[].biomes` | 配置可能バイオーム |
| `gathering.nodeStockPerCycle/regrowSec/growthStageFractions` | 在庫3、40秒再生、成長境界25/50/75/100% |
| `gathering.botIntervalSec` | ボットの思考間隔2秒 |
| `gathering.biomeModifiers` / `materials[].qualityMultiplier,efficiencyMultiplier` | 品質と採集効率の補正 |
| `objectiveGeneration` | 必須数3、数量の下限40%・上限70% |
| `upgrades.items`の`luck.effectPerLevel` | 採集運の唯一の係数0.6 |

品質抽選はM1のpTopと下位重みを中立分布とし、品質順位r（普通=0）の重みへ `(バイオーム係数×素材係数×ノード係数)^r` を掛けて正規化する。整数比とBigIntegerを使い、未解放品質を含めない。100%未満の人為的上限は追加しない。

## 検証と3シードのマップ

```powershell
dotnet test tools/CoreTests --verbosity minimal
```

2026-11-25の最終結果：**200件成功、失敗0、スキップ0（追加58件）**。実行結果と全テスト一覧は `validation/m3a/dotnet-test.trx`、追加テストの全名称は `validation/m3a/added-tests.txt`、集計とマップのハッシュは `validation/m3a/validation.json`。Unity 6000.3.21f1の再コンパイルもエラー0・警告0を確認した。新しい.metaはUnityのインポーターが生成し、手書きしていない。

3シードの出力：`validation/m3a/map_seed_482.txt`、`map_seed_777.txt`、`map_seed_202647.txt`。各ファイルに64×64の地形、資源、ラウンド解放範囲の3面と、各ラウンドに到達できる素材別ノード数がある。`@`が拠点、`=`が橋、`~`が川。資源記号はファイル冒頭の凡例を参照。

再出力：

```powershell
$env:IMONI_MAP_EXPORT_DIR = Join-Path (Get-Location) 'docs/validation/m3a'
dotnet test tools/CoreTests --filter FullyQualifiedName~WorldMapExportTests
Remove-Item Env:IMONI_MAP_EXPORT_DIR
```

通常のテスト実行ではファイルを出力しない。環境変数を指定したときだけ指定ディレクトリに書き出す。

追加テストの範囲：

| テストクラス（追加件数） | 検証内容 |
|---|---|
| `WorldGenTests`（23件） | 同シード一致・別シード差、全資源到達、配置バイオーム、初期供給、疎な配置の補修、全バイオーム・橋・チャンク、同心円、A*最短路/水/未開放/到達不能/岸からの魚/決定的な経路 |
| `GatheringSimulationTests`（16件） | 在庫・全成長段階・設定変更、移動後の採集、強化反映、同時採集、中断と切断、ボット、操作再現、Tick分割の一致、5:00境界、ラウンド越しの状態・目標 |
| `WorldObjectiveTests`（11件） | R1〜5の必須・ボーナス供給と重複なし、40〜70%、シード再現、共有時間上限、成長中・部分在庫・再生待ち、人数/強化、設定変更と不可能な候補 |
| `WorldQualityTests`（5件） | 中立確率の維持、3種類の補正、未解放品質の除外、決定性、採集運の単一ソース、上限なし、無効設定の拒否 |
| `WorldMapExportTests`（3件） | 3シードの64×64出力とラウンドごとの到達確認 |

M1の既存サーバーテストも実際の選択・移動・採集完了経由に更新し、売却/保護/資金/連続/ラウンド処理の回帰を確認する。

## Unityでの確認

M3aはCoreのみなので、新しいゲーム画面の確認操作はまだない。Unityの再読み込み後、必要ならWindow > General > Test RunnerのEditModeで上記Coreテストを実行できる。BootのGameはM2bの空画面のままで正常。Tilemap・タップ入力・カメラ・歩行と採取演出への接続はM3bで扱う。
