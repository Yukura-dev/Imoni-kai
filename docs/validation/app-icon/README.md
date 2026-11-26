# アプリアイコン更新（2026-11-26）

旧`pot_campfire`の簡略図から、ホーム・演出で使用中の`home_pot_campfire`（取っ手・里芋・たき火付き）へ変更した。

- 元画像：`Assets/ImoniUI/Home/home_pot.png`。既存のSetupが生成する鍋部分を再利用。
- `setup.android.iconTexture = home_pot_campfire`、`iconSize = 1024`、`iconContentScale = 0.82`。縦横比を維持して中央へ配置し、背景色は既存の黄色を維持。
- 共通default iconを更新。Androidの個別アイコン枠は空、iOS固有の上書きもないため、両プラットフォームで同じ絵を使用する構成。
- Android versionCode / iOS buildNumberは5。

| 修正前 | 修正後 |
|---|---|
| ![旧アイコン](before.png) | ![新アイコン](after.png) |

## 検証

- Unity 6000.3.21f1で再コンパイル、Setup、標準Androidビルド成功。エラー0・警告0（`console.json`、`android-build.json`）。
- `dotnet test tools/CoreTests`：548件成功、失敗0、スキップ0（`dotnet-test.json`、元TRXは`Library/app-icon-tests/app-icon.trx`）。ゲームルールの変更はない。
- aaptでAPKのpackage `jp.yukura.imoni`、versionCode5を確認。実際のAPKから640dpiのランチャー画像`res/oj.png`を取り出し、新しい鍋を目視確認（`apk-icon.png`）。
- APK：`Builds/Android/Imoni.apk`。実ファイル68,680,210 bytes、SHA256は`apk.json`。BuildReportのbytesはシンボル等を含むため、APK単体サイズとは異なる。
- Android端末での更新インストールは未実施。iOSの署名・実機起動も引き続き未確認。

端末では新しいAPKを更新インストールしてアイコンを確認する。iPhone版は`docs/IOS_DEVICE_SETUP.md`に従ってMacで出力する。
