# みんなで芋煮会

正本は **D:/UnityProjects/Imoni-kai**。このフォルダをUnity Hubから開く。別のhandoffフォルダで並行開発しない。

現在の到達点は **M6（目標インフレ・整数表示・演出音・音量保存）**。ワールド採集、倉庫・ショップ・強化、5ラウンドの成功／失敗／通常クリアに加え、品質・強化に応じた目標数量とAudioMixer経由の演出音を実装した。操作・検証は [docs/M6_AUDIO_BALANCE.md](docs/M6_AUDIO_BALANCE.md)、自動プレイの目標と到達時刻は [balance.md](docs/validation/m6/balance.md)。作業指示は [AGENTS.md](AGENTS.md)、アウトゲームの遷移は [docs/M2B_SCREENS.md](docs/M2B_SCREENS.md)、共通部品は [docs/COMPONENTS.md](docs/COMPONENTS.md) を参照。M7へは進んでいない。

- 新しい実装：`Assets/Imoni/`。起動シーンは `Assets/Imoni/Scenes/Boot.unity`。
- 旧プロトタイプ：`Assets/Scenes/`、`Assets/Scripts/`、`Assets/Prefabs/`。採取の操作感と画面を比較するため保存。
- 見た目の基準：`design/screenshots/`、`mock/`。芋煮会演出は `docs/IMONI_EFFECT_SPEC.md` と `design/videos/`。
- 数値：`Assets/Imoni/Config/GameConfig.json`。純C#テスト：`dotnet test tools/CoreTests`。

初回はPlay Modeを止めて `Imoni > Setup Project` を実行する。`Imoni > Open App`でMainから開始し、`Imoni > Open Component Gallery`で共通部品を確認できる。LoadingからGameへ進み、資源をタップすると移動・採集する。Home右上の設定で音のON/OFFと音量を変更でき、再起動後も保持する。演出・ラウンド遷移の詳細は [docs/M5_ROUND_FLOW.md](docs/M5_ROUND_FLOW.md)。

作り直し前の全ソースはコミット `30046d7`（`backup/pre-rebuild-20261125`）と `D:/UnityProjects-Backups/Imoni-kai-before-rebuild-20261125-175526` に保存。復元手順はバックアップ内の `RESTORE.txt` を参照。

M0の確認記録は [docs/M0_VALIDATION.md](docs/M0_VALIDATION.md)、M1の実装・テスト一覧は [docs/M1_CORE.md](docs/M1_CORE.md)。マイルストーン完了時はコミット・報告して停止する。
