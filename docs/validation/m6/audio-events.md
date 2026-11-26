# M6 Unity音イベント検証

2026-11-26、Unity 6000.3.21f1のGameビュー390×844で取得した。`ManualTime=false`、通常時間で成功・失敗・ミュートの演出を再生し、AudioServiceと生成済みAudioMixerの実状態を読み出した。

これは **AudioSource.Playの呼び出しとDSP時刻・ソース設定・Mixer配線の記録** である。スピーカー出力、ループバック音声、Compressor後の波形ピークや歪みを測定したものではない。`EffectiveGain`も、ソースゲインに保存音量を掛けた指令値であり、出力音圧や波形振幅の測定値ではない。

## 元の記録と検証範囲

| 記録 | 演出用スコア | ON/OFF・保存音量 | 全イベント | AudioSource.Playに対応する記録 | 終了後のactiveVoices |
|---|---:|---|---:|---:|---:|
| [audio_success.json](audio_success.json) | 18,420 | ON・50% | 66 | 59 | 0 |
| [audio_failure.json](audio_failure.json) | 7,350（成功基準18,420） | ON・50% | 49 | 44 | 0 |
| [audio_muted.json](audio_muted.json) | 18,420 | OFF・50% | 58 | 0 | 0 |
| [settings_off_50.json](settings_off_50.json) | Home設定 | OFF・50% | 0 | 0 | 0 |

3つの演出記録の `source` は `artwork-preview`。モックの成功18,420／失敗7,350の音とレベル変化を比較するための演出fixtureで、通常プレイの精算値を書き換えていない。JSON内の `round/result` は背後に残るIGameServerの状態、`effect` と `audioEvents` が今回の演出の測定対象である。

`Played=true` は成功65件・失敗48件だが、それぞれドローンのレベル設定変更6件・4件を含む。上表のPlay件数は `drone / beat / hit / level / finale` に絞った件数。ドローン停止は `Played=false` の停止記録として1件ずつ残る。3ケースとも `stale-*` は0件だった。

取得時のConsoleは音の3記録で **エラー0・警告1**、設定記録で **エラー0・警告0**。警告1は取得セッション中に一度発生したDynamicFontのNativeFormatImporter一貫性警告として親検証側で調査対象になっている。このJSONの警告件数は改変していない。最終再コンパイルのエラー・警告0確認は別の最終検証記録で扱う。

## 成功版の時刻・音源・音程

時刻は演出開始からの秒。元JSONにはミリ秒とUnityの `DspTimeSeconds` を保持している。

| 時刻 | 記録 | pitch／その他の実指定 |
|---:|---|---|
| 0.300 | `bgm_drone_loop`開始、最初の`sfx_beat` | pitch 1、ローパス260Hz。DSP時刻16606.165333秒 |
| 1.400 | 最初の`sfx_hit` | pitch 1.122462。Gain 0.8、50%適用の指令Gain 0.4 |
| 3.193083 | `sfx_levelup_L1` | pitch 1 |
| 4.053888 | `sfx_levelup_L2` | pitch 1 |
| 4.340 | 最初の`sfx_beat_hihat` | Lv.2以降へ切り替わった次のビート |
| 4.978576 | `sfx_levelup_L3` | pitch 1 |
| 5.943494 | `sfx_levelup_L4` | pitch 1 |
| 6.978131 | `sfx_levelup_L5` | pitch 1 |
| 7.582388 | `sfx_finale_success` | pitch 1、Gain 0.9、50%適用の指令Gain 0.45。DSP時刻16613.440000秒 |
| 8.783663 | ドローン停止 | Gain 0、DSP時刻16614.634667秒 |

成功時のビートは19回（通常8・ハイハット11）、着地音33回、レベル音5回、完成音1回。完成からドローン停止まで **1,201.275ms** で、設定1,200ms後の最初の更新で停止している。

ドローンのレベル設定変更を実ソースから記録した値は以下のとおり。

| レベル | 記録時刻（秒） | pitch | ローパスHz | Source Gain |
|---:|---:|---:|---:|---:|
| 0 | 0.301204 | 1.000000 | 260 | 0.000602（立ち上がり中） |
| 1 | 3.194299 | 1.122462 | 520 | 0.48 |
| 2 | 4.057052 | 1.259921 | 780 | 0.56 |
| 3 | 4.978735 | 1.414214 | 1040 | 0.64 |
| 4 | 5.946327 | 1.587401 | 1300 | 0.72 |
| 5 | 6.980019 | 1.781797 | 1560 | 0.80 |

着地音の実効pitch最大は **4.756828**。高音8件では `sfx_hit (presentation octave)` を使用し、全ソースへの実指定 `SourcePitch` は最大 **2.996614** に収まった。例として6.978131秒では実効4.756828、周波数2倍コピーへのSourcePitch 2.378414となる。元のWAVは変更していない。

## 失敗版

失敗版のビートは16回（通常11・ハイハット5）、着地音23回、レベル音はL1〜L3の3回。L4／L5のレベル音は出ていない。

- ドローン開始と最初のビートは0.300秒。
- L1は4.053888秒、L2は5.464672秒、L3は7.246414秒。ハイハットは5.840秒から。
- 最終ドローン設定はpitch **1.414214**、ローパス **1040Hz**。
- `sfx_finale_fail` は **7.650603秒**、Gain 0.9、50%適用の指令Gain 0.45。DSP時刻16629.162667秒。
- ドローン停止は **8.451235秒**。完成から **800.632ms** で、設定800ms後の最初の更新で停止。
- 高音コピーは2件。全ソースのSourcePitch最大は **2.996614**。

## 仕様式との照合

保存した実イベントの順番からレベルとレベル内着地数を再構成し、Configの `2^(2L/12)`、着地音のペンタトニック式、レベル別 `beatIntervalMs` と照合した。

| ケース | 着地pitch式の不一致 | 最大浮動小数差 | ビート間隔の不一致 |
|---|---:|---:|---:|
| 成功 | 0 | 2.23×10^-16未満 | 0 |
| 失敗 | 0 | 2.23×10^-16未満 | 0 |
| ミュート（再生せず計算した指令値） | 0 | 2.23×10^-16未満 | 0 |

## ON/OFF・音量・dB

Homeの設定記録は `enabled=false / volume=0.5 / savedEnabled=false / savedVolume=0.5`。ミュート演出でも同じ保存値が保持され、58イベントすべて `Played=false / EffectiveGain=0`、ドローン開始記録なし、終了後のActiveVoicesは0だった。ミュート時のSourcePitchは未適用の予定値なので、ソース上限確認にはONの成功・失敗記録を用いた。

ONでは保存音量50%を各指令ゲインへ反映している。ビート・着地・レベル音は0.8→0.4、完成音は0.9→0.45。

これら取得時点のJSONにはMasterの `GetFloat` 実読出し値は含まれない。以下は保存音量と実装式からの換算であり、Mixer実測dBとしては扱わない。

- Master 50%: `20×log10(0.5)` = **−6.0206dB**。OFF時の設定は**−80dB**に加えソースミュート。
- 通常効果音: Source 0.8 = −1.9382dB、50%適用の指令Gain 0.4 = −7.9588dB。
- 完成音: Source 0.9 = −0.9151dB、50%適用の指令Gain 0.45 = −6.9357dB。

## 実Mixer配線・コンプレッサー

4記録とも `allVoicesRouted=true`。検証コードは全効果音Voiceとドローンの `outputAudioMixerGroup` が、UIRuntimeAssetsのMaster参照と同じであることを確認している。

`ImoniAudioSetup.Describe` がUnityの生成済みオブジェクトから読んだ値:

| 項目 | 結果 |
|---|---|
| Mixer | `Assets/Imoni/Audio/ImoniMaster.mixer` |
| Master参照 | `ready=true`、グループ名`Master` |
| 効果 | `Attenuation` → `Compressor` |
| bypass | 両効果ともfalse |
| Compressor Threshold | −18dB |
| Attack | 20ms |
| Release | 120ms |
| Make up gain | 0dB |
| 公開パラメーター | `ImoniMasterVolume` |

Unity内蔵CompressorはこのバージョンではRatioを公開しないため、設定していない。上表はConfigを転記しただけではなく実Mixerの値。コンプレッサーによる音量変化の波形測定は本記録の対象外。

後続の `proof_mixer_runtime.json` には `AudioMixer.GetFloat` の実読出しも追加した。保存音量50%・ONで `masterDecibels=-6.0206`、`allVoicesRouted=true` を確認している。設定をOFF・50%で保存し、Play Mode再起動後もOFF・50%を復元、その後ONへ戻した画像は `proof_settings_*.png/json`。

## フォント修正後の最終再検証

SetupでBlackの既知文字を事前生成する修正後、cold PlayからHome設定と全5ラウンドを経由して再度実時間の音を記録した。[成功の最終記録](audio_success_final.json)・[失敗の最終記録](audio_failure_final.json)は、いずれも **エラー0・警告0**、全ソースMaster接続、音量50%のMaster実読出し−6.0206dB、再生終了後activeVoices=0。着地/ビート/レベル/完成の音源・音程・予定時刻を上の初期記録と全件照合し、不一致0だった。初期の警告を含む原本は調査記録として保持している。
