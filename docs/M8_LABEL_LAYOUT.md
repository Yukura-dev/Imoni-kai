# M8 ワールドの名札・獲得表示・成長札

## 修正内容

`Character`、`GatherPopup`、`ResourceNode` の既存プレハブを維持し、3種類の札に共通の `WorldAnnotationLayout` を使用する。札の文字列をそのフレームで測り、padding と border を加えた矩形を実描画と重なり判定の両方に使う。前フレームの `resolvedStyle.width` と、中央揃えの `% translate` から別々に矩形を推測しない。

優先順位は「名札（自分を先頭）→獲得ポップアップ（新しいものを先頭）→成長札」。各札は先に置いた札から `ui.worldView.labelOffsetPx` の間隔を取る。名札とポップアップは元の位置に最も近い空き場所へ移す。成長札は資源の上下左右・斜めの候補から空き場所を選ぶ。画面内に収まる空き場所がない札は、そのフレームだけ非表示にする。採集履歴・在庫・ポップアップの寿命は変えない。画面外にいる仲間のポップアップを画面端へ寄せて表示することもしない。

キャラクターの体は重なってよい。体の表示範囲は札の配置を避ける領域として使い、キャラクターの移動や当たり判定には使わない。

## 成長札の距離

`GameConfig.json` の `ui.worldView.growthLabelSelfExclusionTiles` は **1.5 タイル**。自分からこの距離以内の資源は、選択中でも成長札を表示せず、既存の芽アイコン表示にする。それより遠い資源には従来の `growthLabelDistanceTiles`（2 タイル以内）または選択中という表示条件を適用する。距離は資源と自分の表示位置の中心同士で測る。

これにより、採り切った直後に自分の足元付近へ「成長中」が出て「あなた」と競合することを防ぐ。離れてから表示される成長札も、共通の占有矩形を避ける。

## 検証 API

`GameWorldView` が返す次の値は、すべてマップ要素を原点とする UI Toolkit 座標（左上原点）で、実際に配置した border-box。

| API | 内容 |
|---|---|
| `NameLabelRects` | actor ID ごとの表示中の名札 |
| `GatherPopupRects` | 表示中の獲得ポップアップ |
| `GrowthLabelRects` | resource ID ごとの表示中の成長札 |
| `CharacterAvoidanceRects` | キャラクターの SpriteRenderer 範囲と配置済み名札。体同士の重なりは許可 |

前3種類の矩形を1つの列へまとめ、全ペアの `Rect.Overlaps` が false であることを調べる。非表示の札は列に含まれない。`ActivePopupTexts` は寿命が残っている非表示のポップアップも含むため、可視数の判定には `GatherPopupRects` を使用する。描画後の要素の `worldBound` をマップの `WorldToLocal` で変換すると、画像上の矩形との照合もできる。

Editor 専用の `ImoniM8LabelValidation.Inspect(world)` は、上記の全ペア交差を `intersectionCount` / `intersections`、描画後の要素の交差を `renderedIntersectionCount` / `renderedIntersections` として JSON に返す。`growthStates` には成長中の各資源の距離・在庫・除外対象か・成長札の表示状態を保存し、近接対象なのに表示している件数を `exclusionViolationCount` に出す。検証呼び出しは表示の更新を行わない。実描画との照合時は、`RefreshSnapshot` の後にレイアウトが反映されたフレームで呼び出す。

## 再現・撮影手順

1. 修正前画像と同じ **seed 482、経過 25 秒** を開き、名札と複数ポップアップが同時に出る場面を比較する。修正前画像は `Library/M7-extra-before-game-labels.png`。
2. 採り切り直後の確認には **seed 202647、`resource-33-27`** を使用する。自分で3回採集し、Core の資源スナップショットが `RemainingStock == 0` になった直後を撮影する。自分が資源に近い間は成長札がなく、芽だけであり、「あなた」と獲得表示が重ならないことを確認する。
3. 地面の **(31, 27)** へ移動する。資源 (33, 27) との距離が2タイルになり、除外距離1.5より外・表示距離2以内になる。成長札が再び現れ、名札・残っているポップアップと交差しないことを確認する。地面移動で選択状態が解除されても、距離条件で成長札は表示される。
4. 画面の端や3人が近い状態でも前節の矩形検証を行う。成長札の置き場がない場合は非表示が正しい。実機の文字列サイズ差にも同じ測定処理を使う。

Unity の撮影画像と実行結果は `docs/validation/m8/` に記録する。この文書の再現手順は、撮影済み・実機で確認済みという意味ではない。
