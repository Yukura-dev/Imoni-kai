# みんなで芋煮会

正本は **D:/UnityProjects/Imoni-kai**。このフォルダをUnity Hubから開く。別のhandoffフォルダで並行開発しない。

現在の到達点は **M3a（ワールド・採集Core）**。64×64のワールド、A*、資源成長、目標生成、移動・採集Tick、ボットを実装した。使い方と検証は [docs/M3A_WORLD_CORE.md](docs/M3A_WORLD_CORE.md)。画面側はM2bまでで、M2aの共通UI部品を使い、Main、Tutorial1〜3、Home、Lobby、Waiting、Loading、Member、Rankingとプロフィール・音設定の保存を実装した。作業指示は [AGENTS.md](AGENTS.md)、起動・遷移・比較画像の案内は [docs/M2B_SCREENS.md](docs/M2B_SCREENS.md)、部品のモック対応表は [docs/M2A_COMPONENTS.md](docs/M2A_COMPONENTS.md)。

- 新しい実装：`Assets/Imoni/`。起動シーンは `Assets/Imoni/Scenes/Boot.unity`。
- 旧プロトタイプ：`Assets/Scenes/`、`Assets/Scripts/`、`Assets/Prefabs/`。採取の操作感と画面を比較するため保存。
- 見た目の基準：`design/screenshots/`、`mock/`。芋煮会演出は `docs/IMONI_EFFECT_SPEC.md` と `design/videos/`。
- 数値：`Assets/Imoni/Config/GameConfig.json`。純C#テスト：`dotnet test tools/CoreTests`。

`Imoni > Open App`でMainから開始し、`Imoni > Open Component Gallery`で全部品を確認できる。Loadingの先のGameはM3bまで空の仮画面。ワールド表示・タップ操作・採取の演出はM3b、芋煮会演出の録画照合はM5で扱う。旧演出コードが残っていることは、M5完了を意味しない。

作り直し前の全ソースはコミット `30046d7`（`backup/pre-rebuild-20261125`）と `D:/UnityProjects-Backups/Imoni-kai-before-rebuild-20261125-175526` に保存。復元手順はバックアップ内の `RESTORE.txt` を参照。

M0の確認記録は [docs/M0_VALIDATION.md](docs/M0_VALIDATION.md)、M1の実装・テスト一覧は [docs/M1_CORE.md](docs/M1_CORE.md)。マイルストーン完了時はコミット・報告して停止する。
