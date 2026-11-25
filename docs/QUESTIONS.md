# 質問・仮決定ログ

実装中に仕様が曖昧だった点を、ここに追記していく（エージェント・人間どちらも）。
形式: `- [日付] 質問 / 仮に決めた内容 / 該当ファイル`

- [2026-11-25] 成功ランク(S/A/B/C)の基準が仕様にない / ボーナス達成率で S:300% A:150% B:100% C:それ以外 とした / GameConfig.json rankGrades
- [2026-11-25] ショップの購入価格が仕様にない / 売却単価×3、購入品は普通品質とした / GameConfig.json economy.buyPriceMultiplier
- [2026-11-25] 二つ名パーツ・バッジの獲得条件が未定 / M7 で仮の条件を決めてここに書く / —

## 2026-11-25 — 芋煮会演出の修正

- 質問：前回M0を作ったhandoffと、現在表示されているゲームのどちらに反映するか。
  - 判断：見た目が崩れている実装は `D:/UnityProjects/Imoni-kai` にあるため、こちらの演出を修正する。前回の独立handoffフォルダにはゲーム本体を移さない。ユーザーにも両者が別であったことを説明した。
  - 該当：`Assets/Imoni/`、`Assets/Scripts/UI/RoundResultController.cs`。配布仕様の `unity/Assets/` は、この既存Unityプロジェクトでは `Assets/` に対応する。
- 質問：既存ゲームはuGUIだが、新仕様はUI Toolkit専用である。
  - 判断：今回作り直す芋煮会演出はUI Toolkitとする。採取、移動、カメラ、手のモーション、確定済み結果画面は既存処理に接続し、ゲーム全体の置換は行わない。
  - 該当：`Assets/Imoni/UI/ImoniEffect.uxml`、`Assets/Imoni/Scripts/Game/ImoniEffectView.cs`、`RoundResultController.cs`。
- 質問：失敗スナップショットには「成功した場合の最終スコア」が保存されていない。
  - 判断：演出の閾値用の仮基準を、実際の失敗スコア÷0.4（切り上げ、最低1）として渡す。確定スコア・報酬は変更しない。0.4は仕様3章の約40%に従い `effectReview.failureScoreRatio` に置く。録画用の固定入力は成功18,420／失敗7,350、共通基準18,420。実ゲームの不足素材名はスナップショットから取得する。
  - 該当：`RoundResultController.Replay()`、`GameConfig.json`。
- 質問：参照動画のランダム配置・フレームごとの発生時刻を完全再現できるか。
  - 判断：原本の食材生成には非シードMath.randomが使われているため、指定の式・速度・段階を守り、シード固定で再現可能にする。録画は共通seed40410（背景配置seed7）で比較する。原本の冒頭空白は成功0.76秒／失敗0.24秒だけ除去し、演出開始を合わせる。比較動画でそれ以外の時間変更や補正は行わない。
  - 該当：`ImoniEffectTimeline.cs`、`GameConfig.json`、`tools/EffectReview/compare_videos.py`。
- 質問：失敗動画の完全なグレースケールと、仕様6章の半透明の覆いは同じ色になるか。
  - 判断：新しい実装仕様を優先して指定された茶灰色と黒の2枚を使う。動画のCSSフィルターとは残る色に差があることを比較報告に明記する。
  - 該当：`ImoniEffect.uxml`、`ImoniEffect.uss`。
