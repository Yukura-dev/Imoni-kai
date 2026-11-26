# M8: BGM と効果音の設定

タイトルとホームは共通の `SettingsDialog` を開く。音の ON/OFF、BGM 音量、効果音音量、アカウント切り替え、閉じるを表示し、操作意図を画面側に通知する。カード、ボタン、音量バーは既存部品を使う。`AccountContent` はアカウント一覧の差し込み先で、`ShowAccount(true)` により同じ設定カード内で切り替える。

## 保存と移行

- `ProfileData.BgmVolume` と `EffectsVolume` を別々に保存する。範囲、初期値、刻みは従来の `GameConfig.audio.minVolume / maxVolume / defaultVolume / volumeStep` に従う。
- 旧セーブに `SoundVolume` だけがある場合、未設定の各チャンネルへ同じ値を引き継ぐ。片方だけが保存済みなら、その値は上書きしない。旧 `SoundVolume` フィールドは読込互換のため保持する。
- `SoundEnabled` は両方のチャンネルに共通の ON/OFF。OFF にしてもそれぞれの音量は残す。

## 再生と Mixer

`Imoni > Setup Project` が Unity Editor API を通して次の構成を冪等に生成する。Mixer の YAML は手書きしない。

```text
Master (既存の軽い Compressor、全体の ON/OFF)
├─ BGM (ImoniBgmVolume) ← 背景曲用のループ AudioSource
└─ SFX (ImoniEffectsVolume) ← 演出ドローンと効果音用 AudioSource
```

各チャンネルの音量を各グループへ適用し、Master では音量を二重に掛けない。音を切ると各 AudioSource も mute する。背景曲は mute 中も再生位置を進める。演出の開始・終了・リプレイでは `StopEffects()` のみを呼び、背景曲を止めない。

BGM 曲は未作成なので `audio.bgmClipPath` は空欄にする。`UIRuntimeAssets.backgroundMusic` は null のまま正常起動し、背景曲は鳴らない。後日 AudioClip のアセットパスを `audio.bgmClipPath` へ設定し Setup Project を実行すれば、起動時に 2D ループ再生する。実行時の差し替えは `AudioService.SetBgmClip(clip)`、停止は `StopBgm()`、再開は `PlayBgm()`。null を渡すと安全に無音へ戻る。

Mixer グループ名、公開パラメーター名、背景曲の優先度は `audio.bgmGroupName / effectsGroupName / bgmVolumeParameter / effectsVolumeParameter / bgmPriority` から読む。

## 判断した点

提供済みの `bgm_drone_loop.wav` は、ファイル名に bgm が含まれるが、UI_SPEC 12章の芋煮会演出に合わせて開始・音程変化・フェードする演出音なので **効果音** に分類する。ビート、食材着地、レベルアップ、成功・失敗音も SFX。未作成の背景曲と区別するため、提供音を仮の BGM として流用しない。

## 検証

`AudioChannelSettingsTests` は 10 ケースを追加する。

| テスト | 確認内容 |
|---|---|
| LegacySoundVolumeMigratesToBothChannelsWithoutChangingMute (3 ケース) | 旧音量の 0 / 0.3 / 1 と OFF を移行 |
| ChannelVolumesPersistIndependentlyAcrossRestart | 二つの音量を別々に保存・再読込 |
| PartiallyMigratedSaveOnlyInheritsVolumeForMissingChannel | 保存済みの片方を維持 |
| MissingAllVolumeFieldsUsesConfiguredDefaultForBothChannels | 音量未設定時の Config 初期値 |
| ChannelBoundsAndInvalidValuesUseGameConfig | Config 上下限と非有限値の補正 |
| GlobalMuteRetainsBothSavedChannelLevels | OFF/ON で音量が消えない |
| SteppingOneChannelDoesNotChangeOtherSavedChannel (2 ケース) | 片方を操作しても他方は変わらない |

Unity の検証では Setup 後に `ImoniAudioSetup.Describe` の Master 子グループと公開パラメーターを確認する。BGM 未登録の無音起動、Settings の二つの音量の保存・再起動、効果音を下げた際の演出音の変化を確認する。背景曲の配線検証には一時的なテスト用 AudioClip を `SetBgmClip` へ渡し、BGM バス・ループ・独立 mute・演出開始で止まらないことを確認し、検証後は null に戻す。実際の検証結果は `docs/validation/m8/` に記録する。

Editor の `ImoniM8ExtraValidation.Run()` は明示呼び出し専用で、自動実行しない。Boot の実画面を切り替えず、非表示の付加 UI と Library 内の一時セーブを使い、Main/Home の設定ボタン、二系統の音量、ON/OFF、チュートリアル導線、設定内のアカウント作成・切り替えを実際のハンドラ経由で検証する。一時 AudioService と生成した無音 PCM クリップで Mixer の音量・ループ・独立 mute/停止を確認し、終了時は元 AudioService と AudioListener を復元する。結果は `Library/M8-extra-validation.json` に保存し、実セーブのバイト一致と一時セーブ削除も記録する。これは入力ハンドラと再生状態の検証であり、指によるヒット判定や音質の試聴の代わりではない。
