# M6 音・表示・目標バランス

作業ブランチは、`git pull --ff-only` で更新した `main` の `7d7e037` を起点にした `rebuild/m6-audio-balance`。M6で停止し、M7は実装しない。

## AudioServiceとMasterミキサー

`Scripts/Game/AudioService.cs` が音源・音量・ミュート・再生履歴を管理する。Bootstrapが生成済みの `UIRuntimeAssets` を渡し、すべてのAudioSourceを `audioMaster` へ接続する。旧実装の `AudioListener.volume` は初期化時に1へ戻し、以降の音量操作はMasterと各ソースのミュートへ集約する。

Play Modeを終了して `Imoni > Setup Project` を実行すると、`ImoniAudioSetup.EnsureAssets` がUnityのEditor APIから `Assets/Imoni/Audio/ImoniMaster.mixer` を生成する。MasterにはAttenuationとCompressorを配置し、音量を `ImoniMasterVolume` として公開する。再実行時は同じMixer・Compressor・公開パラメーターを再利用する。Mixerや参照assetのYAMLは手書きしない。

Unity 6000.3.21f1で実際に公開されているCompressorパラメーターは `Threshold / Attack / Release / Make up gain` の4つ。Ratioは公開されていないため指定しない。Configの目標値はThreshold −18dB、Attack 20ms、Release 120ms、Make up gain 0dB。内蔵Compressorの特性を使ってピークを抑え、追加のメイクアップ増幅をかけない。値は `GameConfig.audio.compressor` に置く。

Editor内部APIへのアクセスは `Editor/ImoniAudioSetup.cs` に隔離した。Unityの実パラメーター一覧に存在しない設定は例外にし、無視して生成成功扱いにはしない。`ImoniAudioSetup.Describe(UIRuntimeAssets)` はConfigではなく生成済みMixerを読み、Master参照、実効果名、bypass、実パラメーター値、公開パラメーター名を返す。

## 演出音

`ImoniEffectAudio` が演出と同じ `ImoniEffectTimeline` のイベントを受け取り、純C#の `ImoniAudioPlan` が音源キー・音程・ゲイン・フィルター・フェードを計算する。音の数値は既存の `GameConfig.imoniEffect.audio` と `beatIntervalMs` を使う。

| イベント | 再生内容 |
|---|---|
| 開始0.3秒 | `bgm_drone_loop`をループ。レベルLでpitch=`2^(2L/12)`、ローパス=`260+260L` Hz |
| ビート | Lv.0〜1は`sfx_beat`、Lv.2以降は`sfx_beat_hihat`。間隔はレベルごとのConfig値 |
| 食材着地 | `sfx_hit`。レベル・そのレベルでの着地数・ペンタトニック配列による式を使用し、レベルアップで段階をリセット |
| レベルアップ | `sfx_levelup_L1`〜`L5` |
| 成功／失敗 | `sfx_finale_success`／`sfx_finale_fail`。ドローンは成功1.2秒・失敗0.8秒でフェードアウト |

Unity AudioSourceのpitch上限を超える着地音は、配布WAVのサンプルを周波数2倍のメモリ上のAudioClipにし、ソースpitchを半分にする。元のWAVは変更しない。これにより仕様上の実効音程を保つ。生成したコピーはキャッシュし、サービス破棄時に解放する。

演出をスキップ・終了すると音を停止する。リプレイはタイムラインと再生履歴を初期化する。長いフレーム停止の後に古い音をまとめて再生しないよう、Configの `staleEventMs` を超えたイベントは記録だけ残して再生しない。破棄時の二重停止が次の演出の音へ干渉しないよう、実行中の演出だけが終了処理を行う。

## 音の設定と保存

Home右上の設定 → 「音: ON/OFF」で切り替える。音量は既存 `ImoniButton` と `ProgressBar` を組み合わせた `AudioVolumeControl` の「−」「+」で10%ずつ変更する。0%と100%では対応する方向のボタンを無効にする。音量の上下限・初期値・刻みは `GameConfig.audio` を読む。

プロフィールの `SoundEnabled` と `SoundVolume` を同じJSONへ保存する。再起動後も保持し、演出左上の音ボタンも同じON/OFF設定を更新する。音量のない旧セーブはConfigの初期値100%を使い、不正値は初期値、範囲外は上下限へ補正する。OFF時も選んだ音量は保持する。0%はMixerの最低dBに加えてソースをミュートするので無音になる。

`Resources/UI/Components/AudioSettings.uss` はScreenHostで一度だけ読み込む。UI検証用の名前は `settings`、`sound-toggle`、`volume-decrease`、`volume-increase`、`settings-close`。

## 音の検証API

`AudioService.ClearEventLog()` で履歴を消し、実時間の演出を再生して `EventLog` を取得できる。各記録は次を持つ。

- `AtMs`: 演出タイムライン上の時刻、`DspTimeSeconds`: 呼び出し時のUnity DSP時刻。
- `ClipName`、`Pitch`: 元音源と仕様上の実効音程。
- `SourceClipName`、`SourcePitch`: 高音用コピーを含めたUnityソースへの実指定。
- `Gain`、`EffectiveGain`: ソースゲインと保存音量を掛けた指令ゲイン。Compressor後の波形ピークの測定値ではない。
- `CutoffHz`: ドローンのローパス設定。
- `Played`: 実再生の有無。`stale-*`は遅れたため再生しなかったイベント。

`Mixer`、`MasterGroup`、`DroneSource`、`DroneFilter`、`Voices`、`ActiveVoices`から配線と現在のソースを確認できる。音の検証では `ManualTime=false` の実時間再生を使う。時刻を大きく飛ばす静止画検証では、古い音を再生しない仕様が働く。

## Coreで追加した音テスト

`ImoniAudioPlanTests`の18件を追加した。

- ドローン開始時刻とレベル別ビート間隔。
- Lv.2からのハイハット切り替え。
- 全着地イベントの音程式とレベルごとの着地数リセット。
- Lv.0〜5のドローン音程・ローパス・ゲイン（6件）。
- 成功／失敗の完成音とフェード（2件）。
- 各レベル専用音源の選択。
- Config変更による音程・ゲイン・フェードへの反映。
- MasterのON/OFF・音量とdB変換。
- 音量の保存・旧セーブ互換。
- 音量の上下限補正（2件）。
- 不正値の初期化と10回の増減で正確に0%／100%へ到達すること。

実行コマンド: `dotnet test tools/CoreTests --filter 'FullyQualifiedName~ImoniAudioPlanTests|FullyQualifiedName~ProfilePersistenceTests' --no-restore --nologo -v minimal`。この対象では新規18件＋既存プロフィール7件の計25件成功、失敗0。

## 目標インフレと自動プレイ

`ObjectiveGenerator` は素材別基準量にラウンド成長・解放済み最高品質・チーム強化を掛け、共有在庫・往復移動・採集時間・品質分布・有限回数の連続ボーナスを含む期待供給量の85%以内へ制限する。数値は `objectiveGeneration` に集約した。基準量の90〜110%を抽選し、成長率1.35、最高品質補正0.35、実効供給補正1を採用。採集量や報酬のルールは変えていない。

[balance.md](validation/m6/balance.md)に、全目標と履歴上の正確な到達時刻を掲載。無強化はR4で失敗。毎ラウンド各項目を最大1段階ずつ強化する試行はR5を115.97秒で達成した（薪1,518,378・魚393,162・里芋803,690）。残り時間はボーナス採集に使う。

予算50%でまとめて強化する試行も別に保存した。前ラウンドの大きなボーナス報酬でR5開始後に強化を大量購入すると12.48秒で必須達成する。目標はラウンド開始時に確定し、途中の購入で動かさないため、このケースは残る。QAの購入制限で隠さず記録し、今回指定外の報酬・強化価格・購入回数制限は変更していない。根拠と残る調整点はQUESTIONSに記載。

Unityでも `ImoniM6ReviewJob` から同じ2種類の強化方針を全5ラウンドずつ実行した。`unity-autoplay.json` の全10ラウンドについて、必須・ボーナスの目標、終了時数量、到達小数秒が純dotnetの `balance.json` と一致した。両方とも通常の `IGameServer.TrySelectResource/Upgrade/Buy/Tick` のみで進行し、資金・目標・品質・結果を直接設定していない。

## Unityでの確認手順

1. Play Modeを終了して `Imoni > Setup Project`。BootのGameビューを390×844で起動する。
2. Home右上の設定を開き、音量を変更してON/OFFを切り替える。Playを止めて再起動し、設定が残ることを確認する。
3. 通常のLoadingからGameへ進む。検証時だけ `Imoni > Development > Short Rounds (GameConfig)` を使ってもよい。通常は5分。
4. タイムアップ → 結果を見る → 芋煮会演出 → 結果、成功なら次ラウンドへ進む。音はドローン、ビート、食材着地、レベルアップ、完成音の順で変化する。
5. 失敗時は背景画像・食材・光が灰色化し、`必須素材「魚」が足りなかった` の形式で不足を表示する。成功5回で通常クリアへ進む。

再現用Editorジョブは `tools/Invoke-M6Review.ps1 -RequestJson '<JSON>'`。最初に `{"command":"open"}` を実行すると、通常のユーザーセーブとは別の `Library/M6-profile.json` を使う。`prepare`（seed 202647）→ `autoplay`（upgrades planned/budget）→ `click` TimeUpResults → `sample` timeMs 9000 → `click` ResultButton → `click` ResultNext を繰り返せる。`preview` は演出比較だけの明示的なfixtureで、Coreの精算結果を変更しない。

## 提出した検証結果

- 全Coreテスト: **368成功、0失敗、0スキップ**。`dotnet-test.txt` / `dotnet-test.trx`。
- 新規目標テスト27ケース: 全素材R1〜R5の増加、3シードで最終生成値の増加、各強化の供給反映、全ラウンド×満在庫/成長中の実行可能な作業計画、連続期待量、巨大整数とConfig検証。表示・音の追加テストも全件実行に含む。
- Unity再コンパイル、Setup、実プレイ全10ラウンド、結果/通常クリア、失敗演出でエラー0・警告0を確認。最終Console結果は `unity-console.json`。
- `compare_43_Result.png` は左がモック、右が実際のR4結果。`compare_Clear.png` はモックがないため左が承認済みM5通常クリア、右がM6実結果。数量はCoreの実値なので比較元の固定値と異なる。
- `compare_44_ImoniFail_mid.png` / `compare_45_ImoniFail_end.png` は演出開始4.0秒/9.0秒、左がモック、右がUnity。撮影解像度は390×844、比較画像は780×844。
- `proof_bonus_multiplier.png` は実結果の「目標の18倍」、`proof_clear_stress.png` はさらに大きな値の通常クリア。小数表示は使わない。
- `proof_settings_off_50` / `proof_settings_restart_off_50` / `proof_settings_on_50` は保存・再起動・ON復帰の証拠。設定50%のMixer実読出しは **−6.0206dB**、全ソースのMaster接続を `proof_mixer_runtime.json` に保存。
- 実時間再生の時刻・音源・音程・DSP呼出は [audio-events.md](validation/m6/audio-events.md) と3つのJSONへ保存。音声波形や圧縮後の音圧の測定ではない。

初期検証時にUnityの動的フォントキャッシュでNativeFormatImporterの一貫性警告が出た。音イベント原本にはその時点のConsole数も保持した。`ImoniFontWarmup` をSetupへ追加し、BlackフォントのHome/設定用既知文字と大数の単位124字を事前生成するよう修正した。増えたAtlasはEditor APIで保存・同期Importし、Dynamicは未知の名前用に維持する。Setup連続2回と、cold PlayからHome設定→全5ラウンド→Home復帰を警告0で確認した。Setupで生成したBlack assetだけを含め、プレイで増えた他のフォントキャッシュ・slnxは含めない。提供Blackフォントにない「秭」は事前生成の単位集合からだけ除外し、既存のUIフォールバックを維持する。
