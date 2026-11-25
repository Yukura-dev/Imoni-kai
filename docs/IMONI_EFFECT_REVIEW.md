# 芋煮会演出の確認記録

対象は現在のUnityプロジェクト `D:/UnityProjects/Imoni-kai`。M0の土台はこのプロジェクトに統合済み。旧handoffは参照元のみ。

## 実装

- `Assets/Imoni/UI/ImoniEffect.uxml` / `.uss` と `Scripts/Game/ImoniEffectView.cs` が演出を担当する。
- 座標・光・揺れ・食材の式は `IMONI_EFFECT_SPEC.md` の画面座標に従う。鍋の要素は `(55,500,280,170)`、拡大中心 `(50%,80%)`、柱と星・輪の中心は `(195,536)`。
- `Art/Sprites/Effect` の提供PNG、`Game/pot_effect.png`、`Game/fx_sparkle.png` を使用する。
- `Scripts/Core/ImoniEffectTimeline.cs` は絶対時刻で生成・着地・レベルアップを進め、BigIntegerの確定スコアを保つ。
- `RoundResultController` は確定済みスナップショットを演出へ渡し、演出終了操作から既存の成功／失敗結果へ戻す。採取の実装・報酬計算は変更しない。

## 再生成・録画

1. Unity 6.3 LTSでこのプロジェクトを開き、Playを停止する。
2. `Imoni > Legacy > Setup Effect`。フォント、PanelSettings、演出のPrefab接続、独立した確認用シーンをEditor APIで生成する。もう一度実行しても同名要素を再利用する。
3. `Imoni > Round Flow > Validate Saved Flow`。
4. `Imoni > Effect > Record Success and Failure`。Gameビューを固定390×844にし、同じ描画処理から25fpsのPNG連番を取得する。録画中はGameタブを表示したままにする。
5. `python tools/EffectReview/compare_videos.py --ffmpeg <ffmpeg.exeのパス>`。`design/videos/unity/` に録画MP4、左右比較MP4、比較PNG、計測JSONを保存する。
6. `Assets/Scenes/Game.unity` に戻り、通常の採取終了から演出・結果へ進めることも確認する。

比較動画の左は配布モック、右はUnity。基準動画の冒頭白フレームのみ、成功0.76秒／失敗0.24秒を除く。以降の速度変更はしない。

## 現在の検証状態

- `dotnet test tools/CoreTests --nologo`：15件合格、失敗0（M0 Smoke 1件＋既存演出14件）。
- Unity 6000.3.21f1 DLLを用いたC#9静的コンパイル：Core/Game/新Editorは警告0・エラー0。全既存コードを含めた検証はエラー0（既存SerializeField警告37件）。
- **未完了**：UnityでSetup実行、390×844録画、仕様8章の目視照合、実際の結果ボタン・再生し直し・採取終了フローの操作確認。2026-11-25に作業方針を全体の再構築へ変更し、M0で区切ることが承認された。これらはM5で再開する。Unity画面の取得は復旧済み。
- 従って、見た目が一致した／仕様8章を満たしたとはまだ判定していない。

## 比較で区別する差

- 原本の食材生成は非シード乱数。実装はシード固定で再現可能にしている。共通seed40410のCore上の完成／ボタン予定は、成功7.582／8.582秒、失敗7.651／8.951秒。これは録画による実測値ではない。
- 原本のCSSグレースケールに対し、実装は新仕様6章で指定された2枚の覆いを使うため、残る色が異なる。
- 参照動画はブラウザーの自動再生制限で「タップで音ON」。Unityでは音が有効なら「音あり」。
- 失敗動画の不足素材は「魚」。実ゲームでは実際に不足した素材名を表示する。
