# M0 確認記録 — 2026-11-25

対象：`D:/UnityProjects/Imoni-kai`、Unity `6000.3.21f1`、ブランチ `rebuild/m0-foundation`。

## 結果

| 確認 | 結果 |
|---|---|
| `dotnet test tools/CoreTests --nologo` | 15成功、0失敗、0スキップ（M0 Smoke 1件＋保持した演出Core 14件） |
| Unityでのスクリプトコンパイル | 成功。最後のBoot起動後のConsoleはエラー0・警告0 |
| `Imoni > Setup Project` | Unity Editor APIで生成完了 |
| `Imoni > Validate M0` | 成功。保存済みシーンの参照と生成アセットを検証 |
| Setupをもう一度実行してValidate | 初回と2回目のJSONが完全一致。GUID・FontサブアセットID・シーンのオブジェクト／コンポーネント構成・参照が維持された |
| Boot Play Mode | 起動・停止成功。実際の `Screen.width=390`、`Screen.height=844` |
| GameビューのPNG | 実行中のScreenCaptureで390×844、目視確認済み |
| 旧採取処理・旧シーン・Prefab | M0の差分では内容変更なし。比較用に保持 |

生成物の検証には、UIDocument・Camera・Grid/Tilemap・Bootstrap、Missing Scriptなし、Panel390×844/Match0/Sort10、4つのDynamic/Multi Atlas TextCore FontAssetと永続Material/Atlas、4 asmdefs、Build Settings先頭のBoot、縦画面固定を含む。

## 証跡

- [保存済みアセットの検証結果](validation/m0/saved-assets.json)
- [再実行時の一致確認](validation/m0/idempotence.json)
- [Play Modeの画面サイズ確認](validation/m0/play.json)
- [実際のGameビュー390×844](validation/m0/Boot390x844.png)

`play.json` の `screenshot` は実行時の保存先を示す。コミットに含める画像は同じファイルを `docs/validation/m0/Boot390x844.png` にコピーしたもの。

M0のBootは空の土台なので青い背景のみ。ホームなどはM2で接続する。この確認は演出の見た目・採取全体の再検証ではない。演出の動画比較と仕様8章の照合はM5で行う。

## 再確認する手順

1. 正本のプロジェクトをUnityで開き、Playを停止する。
2. `Imoni > Setup Project`、続けて `Imoni > Validate M0` を実行する。
3. GameビューをFixed Resolution 390×844にしてPlay。Consoleに新しいエラーがないことを確認して停止する。
4. 詳細は [SETUP.md](SETUP.md) を参照。次はM1であり、このコミットでM0を終了する。

## バックアップ

作り直し直前の505ファイルを `D:/UnityProjects-Backups/Imoni-kai-before-rebuild-20261125-175526/project.zip` に保存し、全ファイルのSHA-256一致を検証済み。Git履歴は `history.bundle`。復元方法は同フォルダの `RESTORE.txt`。

保存用コミットは `30046d7`、保存用ブランチは `backup/pre-rebuild-20261125`。
