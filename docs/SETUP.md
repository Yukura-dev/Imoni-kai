# M0 セットアップ

正本は **`D:\UnityProjects\Imoni-kai`** です。この既存プロジェクトを Unity Hub から開きます。別の Unity プロジェクトや、入れ子の `unity/` フォルダを作る必要はありません。

今回の範囲は **M0（土台）** です。Boot シーンには起動に必要な参照を用意します。ホーム画面などの画面実装は M2、採集は M3、ラウンド演出の統合は M5 で進めます。M0 の完了後はコミットして停止します。

## 使用する環境

| 項目 | 設定 |
|---|---|
| Unity | Unity 6.3 LTS / **6000.3.21f1** |
| プロジェクト | `D:\UnityProjects\Imoni-kai` |
| 作業ブランチ | `rebuild/m0-foundation` |
| Core テスト | .NET SDK 8.0 以上（対象は `net8.0`） |
| C# | Unity と共通で C# 9.0 まで |

この文書の相対パスはすべてプロジェクトのルートを基準にしています。受け取り元の仕様書に `unity/Assets/` と書かれている場合は、このプロジェクトの `Assets/` と読み替えてください。

## パッケージ

`Packages/manifest.json` の `dependencies` には、M0 で使用する次のパッケージが設定済みです。

```json
{
  "com.unity.nuget.newtonsoft-json": "3.2.2",
  "com.unity.test-framework": "1.6.0",
  "com.unity.2d.tilemap": "1.0.0"
}
```

これは必要な項目の抜粋です。既存の `manifest.json` 全体をこの内容で置き換えないでください。URP `17.3.0`、Input System `1.20.0`、uGUI `2.0.0` など、旧プロトタイプが利用する既存の依存関係も保持しています。新実装の UI は UI Toolkit を使います。

## Unity での手順

1. Unity Hub で `D:\UnityProjects\Imoni-kai` を選び、**6000.3.21f1** で開きます。
2. パッケージの解決、アセットの取り込み、スクリプトのコンパイルが終わるまで待ちます。Console にコンパイルエラーがないことを確認します。
3. Play Mode を終了し、編集中のシーンを保存します。
4. メニューの **Imoni > Setup Project** を実行します。シーンの保存確認が出た場合は、必要な変更を保存して続行します。
5. 開かれた `Assets/Imoni/Scenes/Boot.unity` の Hierarchy に、次の構成があることを確認します。

```text
Main Camera             Camera（Orthographic / MainCamera）
UI Document             UIDocument
World Grid              Grid
└─ World Tilemap        Tilemap / TilemapRenderer
Bootstrap               Imoni.Game.Bootstrap
```

6. Bootstrap の Inspector で、UIDocument、Camera、Grid、Tilemap の参照がそれぞれ上記のオブジェクトに設定されていることを確認します。
7. **Imoni > Validate M0** を実行します。成功すると Console に `Imoni M0 validation passed` が表示され、`Library/M0Validation.json` に結果が保存されます。
8. Game ビューの解像度メニューから **Fixed Resolution / 390 × 844** を選択します。項目がなければ `+` から追加します。Play を押し、Boot が起動して Console に新しいエラーが出ないことを確認します。確認後は Play を停止します。

**M0 の Boot はゲーム画面をまだ表示しないため、Game ビューは空の状態です。** ホームやロビーが表示されないことは、この段階では想定どおりです。UIDocument に各画面を接続し、画面遷移を作るのは M2 の範囲です。

## Setup Project が生成・設定するもの

| 対象 | 内容 |
|---|---|
| `Assets/Imoni/Fonts/Generated/` | Dela Gothic One、Zen Maru Gothic Bold / Medium / Black の TextCore FontAsset |
| FontAsset の名前 | `DelaGothicOne SDF`、`ZenMaruGothic-Bold SDF`、`ZenMaruGothic-Medium SDF`、`ZenMaruGothic-Black SDF`。`Theme.uss` の参照と一致 |
| FontAsset の設定 | Dynamic、Multi Atlas 有効。Material と Atlas Texture を同じアセットのサブアセットとして保存 |
| `Assets/Imoni/UI/ImoniPanelSettings.asset` | Scale With Screen Size、基準解像度 390 × 844、Match Width Or Height / Match 0、Sorting Order 10 |
| `Assets/Imoni/UI/UnityDefaultRuntimeTheme.tss` | Unity 標準ランタイムテーマの取り込み |
| `Assets/Imoni/Scenes/Boot.unity` | UIDocument、Camera、Grid、Tilemap、Bootstrap と相互参照 |
| Build Settings / Build Profiles の Scene List | Boot を先頭の有効シーンとして登録。既存のシーン項目は保持 |
| Player Settings | Portrait、既定画面サイズ 390 × 844。横向き・上下逆さの自動回転を無効化 |

生成設定は `Assets/Imoni/Config/GameConfig.json` の `setup` にあります。Camera、Grid、フォント生成などの調整値もここから読みます。

`Imoni > Setup Project` は再実行できます。同じ設定なら、Boot のオブジェクト、生成アセット、Build Settings の Boot 項目を重複させず、既存の参照を維持します。確認する場合は、初回実行後と再実行後に `Imoni > Validate M0` を実行し、生成物の GUID・Boot の構成・参照が変わらないことを確認してください。Editor コードからは `Imoni.Editor.ImoniM0Validation.Snapshot()` の戻り値で、同じ内容を比較できます。

`.meta`、`.unity`、`.prefab`、`.asset`、`ProjectSettings` は手書きしません。生成内容を変更するときは `ImoniSetup.cs` と設定値を修正し、Unity の Editor API を通して再生成します。Unity によって生成された `.meta` は、対応するアセットと一緒に管理します。

## コードの配置とテスト

| 配置 | Assembly | 役割 |
|---|---|---|
| `Assets/Imoni/Scripts/Core/` | `Imoni.Core` | UnityEngine に依存しない純 C#。`noEngineReferences: true` |
| `Assets/Imoni/Scripts/Game/` | `Imoni.Game` | Unity の表示・入力との接続。M0 では Bootstrap の参照を保持 |
| `Assets/Imoni/Editor/` | `Imoni.Editor` | Editor 専用。Setup Project と M0 検証 |
| `Assets/Imoni/Tests/EditMode/` | `Imoni.Tests` | Editor 専用の NUnit テスト |
| `tools/CoreTests/Imoni.Core.Tests.csproj` | .NET テストプロジェクト | Core と EditMode テストのソースをリンクし、Unity を起動せず検証 |

PowerShell で次を実行します。

```powershell
Set-Location -LiteralPath 'D:\UnityProjects\Imoni-kai'
dotnet test tools/CoreTests
```

M0 の検証結果は **15 件成功、失敗 0、スキップ 0** です。M0 の Smoke テスト 1 件と、保持した既存の演出 Core テスト 14 件を含みます。既存テストの成功は、M1 以降の全機能や M5 の見た目の完成を意味しません。

`Imoni > Validate M0` は、保存済み Boot の参照、Missing Script の有無、PanelSettings、フォントの永続サブアセット、Theme の参照、4 つの asmdef、Scene List、縦画面設定を検証します。実際の Game ビューの表示と Play Mode の Console は、上の Unity 手順でも確認します。`Library/` の検証結果は再生成できる作業データです。

## 旧プロトタイプとバックアップ

旧プロトタイプの `Assets/Scenes/`、Prefab、スクリプト、素材は比較用として保持しています。旧 Game シーンなどを直接開くと、以前の操作や見た目を参照できます。新実装は `Assets/Imoni/` に集約し、M0 の起点は `Assets/Imoni/Scenes/Boot.unity` とします。

旧演出と旧画面をつなぐ Editor コードは `Assets/Editor/` に分けています。これにより、新しい `Imoni.Editor` から旧 `Assembly-CSharp` を直接参照せずに、プロトタイプを保存できます。

作り直し前の復旧用情報は次のとおりです。

| 項目 | 場所・値 |
|---|---|
| バックアップ | `D:\UnityProjects-Backups\Imoni-kai-before-rebuild-20261125-175526` |
| プロジェクトの保存 | 上記の `project.zip` |
| Git 履歴の保存 | 上記の `history.bundle` |
| 復元方法 | 上記の `RESTORE.txt` |
| プロトタイプを保持した基準コミット | `30046d7`（`chore: preserve prototype before M0 rebuild`） |
| M0 の作業ブランチ | `rebuild/m0-foundation` |

バックアップにはプロジェクトのファイルと Git 履歴があり、再生成可能な `Library/` やキャッシュは含めていません。以前の `imoni_codex_handoff` フォルダと ZIP は仕様・素材・実装の参照元として扱います。今後の編集と Unity での確認は、正本の **`D:\UnityProjects\Imoni-kai`** で行ってください。
