# M8 実機プレイテスト修正の検証

2026-11-26、Unity 6000.3.21f1。最新main `3c69e6f` から `rebuild/m8-playtest-fixes` を作成。実機報告はGalaxy A53（Android版は不明）。ADB接続がないため、実機のちらつき解消・API別の実行結果は未確認。コード・Editor・ビルドの結果と混同しない。

## 追加レビュー

追加の画面・札・設定修正は[extra/README.md](extra/README.md)。最新APKはversionCode 3、Coreテスト492件成功。以下の447件とversionCode 2の記録は最初のM8実装時の証跡として残す。

## テスト

`dotnet test tools/CoreTests --logger 'trx;LogFileName=m8-core.trx'`

- **447件成功、失敗0、スキップ0**。所要時間約64秒。[TRX](dotnet-test.trx)
- M7の410件から37ケース追加。移動20ケース（8方向・水/未開放の角抜け・LOS端点・4近傍と同じ到達性・連続位置・途中リタップ/停止・個人速度強化の即反映）、数値17ケース（上下単位・巨大整数・Compact境界・Config検証・正確な達成判定）。詳細は [移動](../../M8_MOVEMENT.md) と [表示](../../M8_NUMBERS.md)。
- 既存の目標生成・ボット・採集・再生・ラウンド・演出の10秒保証・保存/実績/ランキングも全件成功。
- `dotnet build tools/BalanceReview --no-restore`：成功、エラー0・警告0。自動プレイの資源距離比較も新しい実距離へ揃えた。今回はバランス係数の再調整や3方針の再計測は行っていない。
- Unity再コンパイルと撮影時のConsoleは **エラー0・警告0**：[unity-console.json](unity-console.json)。

## 演出・共通鍋の画像

すべてGameビュー390×844。比較は左が `design/screenshots`、右が実際のUnityキャプチャ。演出は既存のEditor用プレビューで成功18,420/失敗7,350を指定し、絶対時刻で停止した。Coreの確定結果を書き換える操作ではない。

| 対象 | 時刻 | 比較画像 |
|---|---:|---|
| 成功・開始 | 1.5秒 | [40_Imoni_start](compare_40_Imoni_start.png) |
| 成功・途中 | 4.6秒 | [41_Imoni_mid](compare_41_Imoni_mid.png) |
| 成功・完了 | 9.0秒 | [42_Imoni_finale](compare_42_Imoni_finale.png) |
| 失敗・途中 | 4.0秒 | [44_ImoniFail_mid](compare_44_ImoniFail_mid.png) |
| 失敗・終了 | 9.0秒 | [45_ImoniFail_end](compare_45_ImoniFail_end.png) |
| ホーム | 静止レイアウト | [10_Home](compare_10_Home.png) |
| メンバー待ち | 1人待機 | [15_Waiting](compare_15_Waiting.png) |
| 読み込み | 進行中 | [17_Loading](compare_17_Loading.png) |

確認所見：鍋は指定された取っ手・里芋・たき火付きの原画へ変わった。汁面と拡大中心は設計座標(195,536)。食材が汁面へ入り、成功/失敗の見出しと結果ボタンを遮らない。ホームはたき火付き、待機/読み込みは本体のみの同じ原画を使う。元画像の切り出しはSetupで生成し、再生成可能。

今回変更していない箇所の差も比較に残している。途中のレベル/スコア/飛行中食材は既存Coreタイムラインと固定モック静止画で異なり、最終スコア18,420はM6以降の `Format.Big` により「1万」。見出しの内部線はM7のフォント表現を維持。読み込み率・参加行アニメーション・音の保存状態は撮影時点に依存する。これらを新しい鍋の描画欠けとは扱わない。

## 全画面背景と描画境界

[Galaxy Note10一覧](simulator/contact_Note10.png) / [iPhone 12一覧](simulator/contact_iPhone12.png)。両SimulatorでGame・成功終盤・失敗終盤を撮影し、元解像度のPNGと矩形JSONを各フォルダに保存した。背景がSafe Area外へ広がり、黄色い帯が演出の上端に残らないことを画像で確認。HUD・文字・ボタンのSafe Area配置は維持される。

初回撮影でレビュー停止中のGameカメラがまだ無効のままになる例を発見し、GeometryChangedで初期描画を確定するよう修正して再撮影した。最終の全Gameキャプチャではカメラが有効で、マップが表示されている。

同じ状態での更新100回は、通常/Note10/iPhone 12すべて **padding書き込み0・viewport書き込み0**。HUD・下ボタン・Safe Area外周の背景alpha/opacityは1。原因候補・具体的対策・実機での比較手順は [flicker.md](flicker.md)。

## 斜め移動

[7枚の一覧](diagonal_contact.png) / [入力と経路](diagonal-tap.json) / 元画像と状態は `movement/`。

シード482、R1、Lv0で地面(34,33)を、画面座標(259,512)から実際の `TryTapPanelPoint` 経路へ渡した。地面選択が成功し、資源選択にはならないことも記録。CoreのTickを0.1秒刻みで進め、0〜0.6秒を7枚撮影した。OSのタッチ入力を自動注入した証跡ではない。

| 秒 | 連続X | 連続Y | 状態 |
|---:|---:|---:|---|
| 0.0 | 32.000 | 32.000 | Moving |
| 0.1 | 32.391 | 32.196 | Moving |
| 0.2 | 32.783 | 32.391 | Moving |
| 0.3 | 33.174 | 32.587 | Moving |
| 0.4 | 33.565 | 32.783 | Moving |
| 0.5 | 33.957 | 32.978 | Moving |
| 0.6 | 34.000 | 33.000 | Idle |

A*のセル列は(32,32)→(33,33)→(34,33)、LOS後は始点と終点の2点のみ。距離√5、所要時間0.51110253秒。カメラ追従により本人の画面中心は全コマ(195,480)のまま、周囲の地形が斜めに動く。L字の折れはない。

## 再現

1. `Imoni > Setup Project`。`tools/Invoke-M7Review.ps1` の `open` で隔離プロフィールのBootを起動する。
2. `tools/Capture-M8.ps1` でプレビュー・移動・Simulatorの証跡を再取得。
3. `tools/Compare-M8.ps1`、`tools/Build-M8ContactSheet.ps1` で比較・Simulator一覧を生成する。画像を目視して確認する。
4. Androidは `Imoni > Build > Android APK`。GLES3 On/Off版は同じBuildメニューの各項目。実機ではまず標準APKを試し、残る場合は [flicker.md](flicker.md) の同条件比較を行う。

APK本体は既存 `.gitignore` に従ってコミットせず、`Builds/Android/` に出力する。ビルド結果・サイズ・SHA256をこのフォルダのAndroid検証記録に保存する。

## Androidビルド結果

全版 `Succeeded`、エラー0・警告0。ARM64 / IL2CPP / Portrait、versionCode 2。

| APK | 実ファイルのbytes | 所要時間 | 証跡 |
|---|---:|---:|---|
| Imoni.apk（標準） | 63,500,628 | 352.126秒 | [android-default.json](android-default.json) |
| Imoni-gles3-on.apk | 62,272,492 | 42.272秒 | [android-gles3-on.json](android-gles3-on.json) |
| Imoni-gles3-off.apk | 62,192,249 | 34.628秒 | [android-gles3-off.json](android-gles3-off.json) |

実機インストールとちらつきの再確認は人間が行う。まず標準版、残る場合はGLES3 ON→OFFの順で同じ操作を比較する。
