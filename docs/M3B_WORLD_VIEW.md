# M3b ワールド表示と採集

作業ブランチは `rebuild/m3b-world-view`。Coreのバランス調整コミット `2e14454` から続けて実装した。パネルはM4、終了演出と次ラウンドへの画面遷移はM5の範囲。

## Unityで開く

1. Play Modeを終了し、`Imoni > Setup Project` を実行する。Boot、既存UIの参照に加え、下記ワールド用アセットを生成する。
2. `Imoni > Open App` から、Main → Tutorial1〜3 → Home → Lobbyへ進む。
3. コード `IMO-482` で参加して準備を完了するか、チームを作成してボット2人を待ち、Loadingへ進む。
4. Loadingの「ラウンド1 スタート!」を押すと、生成されたマップをGameで表示する。
5. 資源をタップすると、隣接タイルまで歩いて止まり、採集バーが進む。確定すると品質つき獲得ポップアップとトースト、HUDの品質換算数量が更新される。
6. 同じ資源を3回採り切ると切り株になり、「成長中 n秒」を表示する。40秒後に再び採れる。
7. 空いた地面をタップすると、そこへ移動して止まる。移動だけでは採集しない。カメラは自分を追う。

Gameビューは390×844、Scale 1xを使用する。倉庫・ショップ・強化・メニューのボタンは表示のみで、今回パネルは開かない。倉庫の内容はCoreの `Snapshot().Lots` に保持され、採集によって増える。時間切れ後は0:00で停止し、Homeへ戻らない。

## 生成アセットと責務

| 生成物 | 内容 |
|---|---|
| `Assets/Imoni/Prefabs/World/ResourceNode.prefab` | 全素材共通。画像、タップ範囲、選択枠、採集バー、成長表示 |
| `Assets/Imoni/Prefabs/World/Character.prefab` | 自分・ボット共通。4レイヤー、保存色、丸い手、歩行の上下動、切断表示 |
| `Assets/Imoni/Prefabs/World/GatherPopup.prefab` | 品質色つき獲得量と採集時の連続数。再利用する |
| `Assets/Imoni/Prefabs/World/LockedAreaOverlay.prefab` | 未開放タイルの覆い、鍵、次ラウンドの説明 |
| `Assets/Imoni/Prefabs/World/TileSet.asset` | 提供画像から作るTile一式。既存画像のGUID・切り出し設定を保持する |
| `Assets/Imoni/Resources/World/WorldRuntimeAssets.asset` | プレハブ・TileSet・画像の参照 |

すべて `ImoniSetup` → `ImoniWorldSetup.EnsureAssets` のEditor APIで生成する。シーンやプレハブのYAMLは手書きしない。表示はTilemap/SpriteRendererとUI Toolkitだけを使い、Canvas/TMPは追加しない。

`Bootstrap.Update` → `GameScreen.Advance` だけがCoreのTickを呼ぶ。`GameWorldView`は公開状態を補間し、タップを `IGameServer.TrySelectResource/TryMoveTo` へ渡す。到達・採集時間・在庫・品質・数量はCoreが決定する。HUDの品質換算とボーナス判定も `GameHudProjection` で行う。

カメラの描画範囲はUIのマップ領域から計算する。390×844では上200pxと下84pxを除く390×560が対象で、1タイル32px、カメラのorthographicSizeは8.75になる。Safe Areaが変わった場合も実測領域を使用する。

成長ラベルはキャラクター本体・名札・採集中の資源とバーを避けて配置する。最後にタップした資源、その後は自分に近い資源を優先し、ラベル同士も重ねない。周囲に置き場所がないラベルは隠す。人間同士の重なりは許可し、表示調整によってCoreの移動や採集を変更しない。

## モックとの対応

| 確認画像 | 比較する見た目・動作 |
|---|---|
| `30_Game_start` | 黄色200pxのHUD、タイマー、資金、必須3素材、公開前ボーナス、連続表示、マップ、84pxのActionボタン |
| `31_Game_collecting` | 資源の赤い選択枠、上の採集バー、隣接位置のキャラクター、手の伸縮、獲得表示 |
| `33_GameMid_bonus` | 公開後ボーナスの数量・達成率・報酬倍率、成長中の資源、半透明の切断メンバー |

比較画像は左がモック、右が実際のUnity Gameビュー。モックの固定地図・固定数量と違い、右側は共通seed202647の生成結果を表示する。地形・資源座標、必須の数量、ボーナス素材、在庫、連続数は実際のCore状態のため一致しない。これらを一致させるダミー値は入れない。画面部品の詳細は `M3B_HUD.md` に記載する。

## 検証用の記録方法

`ImoniM3BReviewJob` はEditor専用。`Library/M3BReview.request` のJSONから、現在のBootの表示・状態を記録できる。`inspect` は配置と資源のパネル座標、`capture` はGameビュー、`step` はCoreの時間進行、`arm` は実際の資源タップを待って撮影する。

連番は実タップから開始し、通常のIGameServerへ渡した操作を、シミュレーション上1秒ずつ進めた8枚で記録する。フレーム間は開発用停止を使い、各画像と同時の状態JSONも保存する。採集の前倒しや在庫の直接変更はしない。通常実時間の操作確認と併用する。

検証画像・連番・実行結果は `validation/m3b/` に保存する。最終件数、Unityのコンソール数、撮影時の移動/採集/在庫状態は同ディレクトリの `validation.json` を参照。
