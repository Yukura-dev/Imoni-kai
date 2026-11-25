# みんなで芋煮会

正本は **D:/UnityProjects/Imoni-kai**。このフォルダをUnity Hubから開く。別のhandoffフォルダで並行開発しない。

現在の到達点は **M1（ルール・Core）**。M0の土台に、設定読み込み・決定的な採集計算・在庫/売買・目標/スコア・ラウンド進行を追加した。画面はM2から実装する。作業指示は [AGENTS.md](AGENTS.md)、開き方と確認手順は [docs/SETUP.md](docs/SETUP.md)。

- 新しい実装：`Assets/Imoni/`。起動シーンは `Assets/Imoni/Scenes/Boot.unity`。
- 旧プロトタイプ：`Assets/Scenes/`、`Assets/Scripts/`、`Assets/Prefabs/`。採取の操作感と画面を比較するため保存。
- 見た目の基準：`design/screenshots/`、`mock/`。芋煮会演出は `docs/IMONI_EFFECT_SPEC.md` と `design/videos/`。
- 数値：`Assets/Imoni/Config/GameConfig.json`。純C#テスト：`dotnet test tools/CoreTests`。

BootはM1でも空画面。ホームなどのUIはM2、ワールド・採取はM3、芋煮会演出の録画照合はM5で扱う。旧演出コードが残っていることは、M5完了を意味しない。

作り直し前の全ソースはコミット `30046d7`（`backup/pre-rebuild-20261125`）と `D:/UnityProjects-Backups/Imoni-kai-before-rebuild-20261125-175526` に保存。復元手順はバックアップ内の `RESTORE.txt` を参照。

M0の確認記録は [docs/M0_VALIDATION.md](docs/M0_VALIDATION.md)、M1の実装・テスト一覧は [docs/M1_CORE.md](docs/M1_CORE.md)。マイルストーン完了時はコミット・報告して停止する。
