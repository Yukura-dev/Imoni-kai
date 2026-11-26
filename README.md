# みんなで芋煮会

正本は **D:/UnityProjects/Imoni-kai**。このフォルダをUnity Hubから開く。別のhandoffフォルダで並行開発しない。

現在は **M7（仕上げ）まで実装・検証済み**。見込み強化を反映した目標数量、二つ名・バッジ、端末内ランキング、エンドレス、ローカルLogin、再接続表示、地形境界、Safe Area、Android向けSetupを実装した。Coreは **410件成功・失敗0**。iPhone 12／OnePlus 6Tの計80状態、390×844比較画像32枚、タイトルからR2までの78.12秒の無音動画を検証済み。最終画面とAndroidビルドのUnityエラー・警告は0。**Android APK生成成功（IL2CPP/ARM64、約59.80 MiB）**。物理端末での起動・性能確認は未実施。

実装概要・手動確認・証跡一覧は [docs/M7_POLISH.md](docs/M7_POLISH.md)、獲得条件と記録は [docs/M7_PROGRESSION.md](docs/M7_PROGRESSION.md)、表示と録画は [docs/M7_PRESENTATION.md](docs/M7_PRESENTATION.md)。3方針の自動プレイ記録は [M7 balance.md](docs/validation/m7/balance.md)。作業指示は [AGENTS.md](AGENTS.md)、共通部品は [docs/COMPONENTS.md](docs/COMPONENTS.md) を参照。

- 新しい実装：`Assets/Imoni/`。起動シーンは `Assets/Imoni/Scenes/Boot.unity`。
- 旧プロトタイプ：`Assets/Scenes/`、`Assets/Scripts/`、`Assets/Prefabs/`。採取の操作感と画面を比較するため保存。
- 見た目の基準：`design/screenshots/`、`mock/`。芋煮会演出は `docs/IMONI_EFFECT_SPEC.md` と `design/videos/`。
- 数値：`Assets/Imoni/Config/GameConfig.json`。純C#テスト：`dotnet test tools/CoreTests`。

初回はPlay Modeを止めて `Imoni > Setup Project` を実行する。`Imoni > Open App`でMain→端末内Login→初回チュートリアル→Homeから開始し、`Imoni > Open Component Gallery`で共通部品を確認できる。LoadingからGameへ進み、資源をタップすると移動・採集する。Home右上で音のON/OFFと音量を変更でき、再起動後も保持する。通常クリア後はエンドレスを選べる。ランキングはこの端末の成功記録だけを表示する。

Android APK生成は `Imoni > Build > Android APK`、通し録画は `Imoni > Development > Record Main to Round 2`。ビルド結果と録画の最終確認状況は [M7の証跡一覧](docs/M7_POLISH.md#テスト証跡一覧) を参照。音の実装・検証は [docs/M6_AUDIO_BALANCE.md](docs/M6_AUDIO_BALANCE.md)、基本の演出・ラウンド遷移は [docs/M5_ROUND_FLOW.md](docs/M5_ROUND_FLOW.md)。

作り直し前の全ソースはコミット `30046d7`（`backup/pre-rebuild-20261125`）と `D:/UnityProjects-Backups/Imoni-kai-before-rebuild-20261125-175526` に保存。復元手順はバックアップ内の `RESTORE.txt` を参照。

M0の確認記録は [docs/M0_VALIDATION.md](docs/M0_VALIDATION.md)、M1の実装・テスト一覧は [docs/M1_CORE.md](docs/M1_CORE.md)。マイルストーン完了時はコミット・報告して停止する。
