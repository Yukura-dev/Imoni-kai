using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Text;
using Imoni.Core;
using Newtonsoft.Json.Linq;

namespace Imoni.BalanceReview
{
    /// <summary>QA policy only. All gameplay changes use the public server commands.</summary>
    internal static class Program
    {
        private static void Main(string[] args)
        {
            string root = args.Length > 0 ? Path.GetFullPath(args[0]) : Directory.GetCurrentDirectory();
            JObject data = JObject.Parse(File.ReadAllText(Path.Combine(root, "Assets/Imoni/Config/GameConfig.json")));
            GameConfig config = GameConfig.FromJson(data.ToString());
            int seed = data["balanceReview"]["seed"].Value<int>();
            var trials = new JArray(Run(config, data, seed, "no-upgrades"), Run(config, data, seed, "planned-upgrades"),
                Run(config, data, seed, "unlimited-budget-upgrades"));
            string output = args.Length > 1 ? Path.GetFullPath(args[1]) : Path.Combine(root, "docs/validation/m7");
            Directory.CreateDirectory(output);
            File.WriteAllText(Path.Combine(output, "balance.json"), new JObject { ["seed"] = seed,
                ["objectiveConfig"] = data["objectiveGeneration"].DeepClone(), ["qaPolicy"] = data["balanceReview"].DeepClone(),
                ["trials"] = trials }.ToString() + "\n", new UTF8Encoding(false));
            File.WriteAllText(Path.Combine(output, "balance.md"), Markdown(data, trials), new UTF8Encoding(false));
            Console.WriteLine("Saved " + Path.Combine(output, "balance.md"));
        }

        private static JObject Run(GameConfig config, JObject data, int seed, string policy)
        {
            IGameServer server = new LocalGameServer(config, seed);
            var rounds = new JArray();
            string actor = server.Snapshot().Players.First(player => !player.IsBot).Id;
            for (int round = 1; round <= config.RoundsToClear; round++)
            {
                GameSnapshot initial = server.Snapshot();
                ObjectiveGenerationPlan generation = new ObjectiveGenerator(config).GeneratePlan(server.World, round,
                    new Rng(unchecked(seed + round)), initial.Players, initial.Funds);
                if (!initial.RequiredObjectives.Select(goal => goal.MaterialId + ":" + goal.Amount).SequenceEqual(
                        generation.Objectives.Required.Select(goal => goal.MaterialId + ":" + goal.Amount)))
                    throw new InvalidOperationException("Balance inspection must reproduce the server's fixed objectives.");
                JObject startingUpgrades = JObject.FromObject(initial.Players.First(player => player.Id == actor).Upgrades);
                JArray purchases = policy == "no-upgrades" ? new JArray() : UpgradePlayer(server, actor, config, data, policy == "planned-upgrades");
                JObject appliedUpgrades = JObject.FromObject(server.Snapshot().Players.First(player => player.Id == actor).Upgrades);
                var firstMet = new Dictionary<string, decimal>(StringComparer.Ordinal);
                GameSnapshot state = server.Snapshot();
                while (state.Phase == RoundPhase.Collecting)
                {
                    decimal remaining = config.DurationSeconds - state.ElapsedSeconds;
                    if (remaining <= config.BotIntervalSeconds) BuyDeficits(server, actor, config, state);
                    else Select(server, actor, config, state);
                    Observe(server.Snapshot(), config, firstMet);
                    server.Tick(Math.Min(config.BotIntervalSeconds, remaining));
                    state = server.Snapshot();
                    Observe(state, config, firstMet);
                }
                RoundResult result = state.LastResult;
                firstMet = ExactAchievementTimes(config, result);
                var row = new JObject
                {
                    ["round"] = round, ["success"] = result.Evaluation.Success, ["phase"] = state.Phase.ToString(),
                    ["fundsAtStart"] = initial.Funds.ToString(), ["fundsAfter"] = result.FundsAfterSettlement.ToString(),
                    ["goalGenerationUpgrades"] = startingUpgrades, ["upgradePurchases"] = purchases,
                    ["appliedPlayerUpgrades"] = appliedUpgrades,
                    ["upgradeForecast"] = new JObject {
                        ["budget"] = generation.UpgradeForecast.Budget.ToString(), ["spent"] = generation.UpgradeForecast.Spent.ToString(),
                        ["players"] = new JArray(generation.UpgradeForecast.Players.Select(player => new JObject {
                            ["id"] = player.Id, ["isBot"] = player.IsBot, ["upgrades"] = JObject.FromObject(player.Upgrades) })),
                        ["purchases"] = new JArray(generation.UpgradeForecast.Purchases.Select(purchase => new JObject {
                            ["actor"] = purchase.ActorId, ["upgrade"] = purchase.UpgradeId, ["level"] = purchase.Level, ["price"] = purchase.Price.ToString() })) },
                    ["expectedSupply"] = new JObject(generation.SimulatedCapacities.Select(item => new JProperty(item.Key, item.Value.ToString()))),
                    ["required"] = new JArray(result.Evaluation.RequiredGoals.Select(goal => Goal(goal, firstMet))),
                    ["bonus"] = Goal(result.Evaluation.BonusGoal, firstMet),
                    ["requiredMetAtSeconds"] = result.Evaluation.RequiredGoals.All(goal => firstMet.ContainsKey(goal.MaterialId))
                        ? new JValue(result.Evaluation.RequiredGoals.Max(goal => firstMet[goal.MaterialId])) : JValue.CreateNull(),
                    ["endOfRoundPlayerUpgrades"] = JObject.FromObject(state.Players.First(player => player.Id == actor).Upgrades),
                    ["score"] = result.Evaluation.Score.ToString(), ["cumulativeScore"] = result.CumulativeScore.ToString()
                };
                rounds.Add(row);
                Console.WriteLine(policy + " R" + round + " " + state.Phase + " met=" + row["requiredMetAtSeconds"]);
                if (state.Phase != RoundPhase.Succeeded) break;
                server.StartNextRound();
            }
            return new JObject { ["policy"] = policy, ["rounds"] = rounds };
        }

        private static JArray UpgradePlayer(IGameServer server, string actor, GameConfig config, JObject data, bool planned)
        {
            var purchases = new JArray();
            decimal fraction = data["balanceReview"]["upgradeBudgetFraction"].Value<decimal>();
            BigInteger budget = GameMath.MultiplyFloor(server.Snapshot().Funds, fraction);
            string[] priority = data["balanceReview"]["upgradePriority"].Values<string>().ToArray();
            var initial = server.Snapshot().Players.First(item => item.Id == actor).Upgrades;
            int limit = data["balanceReview"]["plannedLevelsPerRound"].Value<int>();
            var rules = new UpgradeRules(config);
            while (true)
            {
                PlayerSnapshot player = server.Snapshot().Players.First(item => item.Id == actor);
                string next = priority.OrderBy(id => rules.Price(id, player.Upgrades[id])).ThenBy(id => Array.IndexOf(priority, id))
                    .FirstOrDefault(id => (!planned || player.Upgrades[id] - initial[id] < limit) && rules.Price(id, player.Upgrades[id]) <= budget);
                if (next == null) break;
                BigInteger cost = server.Upgrade(actor, next); budget -= cost;
                purchases.Add(new JObject { ["upgrade"] = next, ["level"] = player.Upgrades[next] + 1, ["cost"] = cost.ToString() });
            }
            return purchases;
        }

        private static void Observe(GameSnapshot state, GameConfig config, IDictionary<string, decimal> firstMet)
        {
            if (state.Phase != RoundPhase.Collecting)
            {
                foreach (GoalProgress goal in state.LastResult.Evaluation.RequiredGoals.Concat(new[] { state.LastResult.Evaluation.BonusGoal }))
                    if (goal.IsMet && !firstMet.ContainsKey(goal.MaterialId)) firstMet[goal.MaterialId] = state.LastResult.ElapsedSeconds;
                return;
            }
            IEnumerable<Objective> goals = state.RequiredObjectives;
            if (state.BonusObjective != null) goals = goals.Concat(new[] { state.BonusObjective });
            foreach (Objective goal in goals)
                if (!firstMet.ContainsKey(goal.MaterialId) && GameHudProjection.Equivalent(config, state.Lots, goal.MaterialId) >= goal.Amount)
                    firstMet[goal.MaterialId] = state.ElapsedSeconds;
        }

        private static JObject Goal(GoalProgress goal, IDictionary<string, decimal> firstMet) => new JObject {
            ["material"] = goal.MaterialId, ["target"] = goal.Required.ToString(), ["final"] = goal.Current.ToString(),
            ["metAtSeconds"] = firstMet.TryGetValue(goal.MaterialId, out decimal at) ? new JValue(at) : JValue.CreateNull() };

        private static Dictionary<string, decimal> ExactAchievementTimes(GameConfig config, RoundResult result)
        {
            var times = new Dictionary<string, decimal>(StringComparer.Ordinal);
            var counts = new Dictionary<string, BigInteger>(StringComparer.Ordinal);
            var goals = result.Evaluation.RequiredGoals.Concat(new[] { result.Evaluation.BonusGoal }).ToDictionary(goal => goal.MaterialId);
            foreach (OperationRecord record in result.History.Where(record => record.RoundNumber == result.RoundNumber &&
                (record.Kind == OperationKind.Gather || record.Kind == OperationKind.Buy)).OrderBy(record => record.Sequence))
            {
                if (!goals.TryGetValue(record.MaterialId, out GoalProgress goal)) continue;
                counts.TryGetValue(record.MaterialId, out BigInteger previous);
                counts[record.MaterialId] = previous + record.Quantity * config.Quality(record.QualityId).Multiplier;
                if (!times.ContainsKey(record.MaterialId) && counts[record.MaterialId] >= goal.Required) times[record.MaterialId] = record.ElapsedSeconds;
            }
            return times;
        }

        private static void Select(IGameServer server, string actorId, GameConfig config, GameSnapshot state)
        {
            PlayerSnapshot actor = state.Players.First(player => player.Id == actorId);
            var missing = state.RequiredObjectives.Select(goal => new { Goal = goal,
                Deficit = BigInteger.Max(0, goal.Amount - GameHudProjection.Equivalent(config, state.Lots, goal.MaterialId)) })
                .Where(item => item.Deficit > 0).ToArray();
            ResourceNode active = server.World.Resources.FirstOrDefault(node => node.Id == actor.TargetResourceId);
            if (actor.Action != PlayerAction.Idle && active != null && missing.Any(item => item.Goal.MaterialId == active.MaterialId)) return;
            foreach (var item in missing.OrderBy(item => config.Panels.ShopMaterials.Contains(item.Goal.MaterialId))
                .ThenByDescending(item => item.Deficit * config.Material(item.Goal.MaterialId).BasePrice))
                if (SelectResource(server, actor, state.RoundNumber, item.Goal.MaterialId)) return;
            if (missing.Length == 0 && state.BonusObjective != null &&
                (active == null || active.MaterialId != state.BonusObjective.MaterialId || actor.Action == PlayerAction.Idle))
                SelectResource(server, actor, state.RoundNumber, state.BonusObjective.MaterialId);
        }

        private static bool SelectResource(IGameServer server, PlayerSnapshot actor, int round, string material)
        {
            ResourceNode best = null; int bestSteps = int.MaxValue;
            foreach (ResourceNode node in server.World.Resources.Where(node => node.MaterialId == material && node.IsReady)
                .OrderBy(node => actor.Position.ManhattanDistance(node.Position)).ThenBy(node => node.Id, StringComparer.Ordinal))
            {
                if (actor.Position.ManhattanDistance(node.Position) > bestSteps) break;
                PathResult path = Pathfinding.FindPathToResource(server.World, actor.Position, node, round);
                if (!path.Reachable || path.Steps.Count >= bestSteps) continue;
                best = node; bestSteps = path.Steps.Count;
            }
            return best != null && server.TrySelectResource(actor.Id, best.Id);
        }

        private static void BuyDeficits(IGameServer server, string actor, GameConfig config, GameSnapshot state)
        {
            BigInteger equivalent = config.Quality(config.PurchasedQualityId).Multiplier;
            foreach (Objective goal in state.RequiredObjectives.Where(goal => config.Panels.ShopMaterials.Contains(goal.MaterialId)))
            {
                state = server.Snapshot();
                BigInteger deficit = goal.Amount - GameHudProjection.Equivalent(config, state.Lots, goal.MaterialId);
                if (deficit <= 0) continue;
                BigInteger affordable = 0, upper = (deficit + equivalent - 1) / equivalent + 1;
                while (upper - affordable > 1)
                {
                    BigInteger quantity = (affordable + upper) / 2;
                    if (GamePanelProjection.PurchaseCost(config, goal.MaterialId, quantity) <= state.Funds) affordable = quantity;
                    else upper = quantity;
                }
                if (affordable > 0) server.Buy(actor, goal.MaterialId, affordable);
            }
        }

        private static string Markdown(JObject data, JArray trials)
        {
            var text = new StringBuilder("# M7 見込み強化を含む目標生成の自動操作記録\n\n");
            text.AppendLine("再実行: `dotnet run --project tools/BalanceReview -- . docs/validation/m7`。シードはGameConfig.balanceReview.seed（" + data["balanceReview"]["seed"] + "）。");
            text.AppendLine("\nM5の自動操作と同じ必須優先・最寄り到達可能資源選択を使用。移動・採集・成長は通常のIGameServer.Tick、末尾の不足購入も所持金内でShop対象素材だけをBuyする。ルール・数量・資金・品質抽選・結果を書き換えない。操作判断はbotIntervalSecごと、到達時刻は終了時のOperationRecordを再生し、品質換算量が初めて目標を超えた正確なelapsedを使う（表は秒以下切捨て、JSONに小数秒を保存）。ボーナスは公開前には選ばない。\n");
            text.AppendLine("強化あり試行だけ、各ラウンドの目標生成後にplayer1が現在資金の" + (data["balanceReview"]["upgradeBudgetFraction"].Value<decimal>() * 100).ToString(CultureInfo.InvariantCulture) + "%以内でUpgradeを購入する。実際の価格が安い順、同額はamount→luck→speed→move。planned-upgradesは各項目" + data["balanceReview"]["plannedLevelsPerRound"] + "Lv/Rまで、unlimited-budget-upgradesは予算内で回数制限なし。ボットの強化は代行しない。これはbalanceReviewのQA操作方針で、ゲーム側の強化制限ではない。\n");
            text.AppendLine("M7では目標生成前に、現在の共有資金の" + (data["objectiveGeneration"]["anticipatedUpgradeBudgetFraction"].Value<decimal>() * 100).ToString(CultureInfo.InvariantCulture) + "%から購入できる見込みLvをCoreで計算する。IsBot=falseの人間だけに、同じUpgradeRulesの価格で安い順に仮配分する。予算は全人間で共有し、同額の優先順はobjectiveGeneration.anticipatedUpgradePriority、その次にactor順。実資金・Lv・履歴・乱数は変えない。生成時の見込みLvを目標補正と供給予測の両方に使い、生成済み目標は実際の購入後も固定する。QAの購入方針を参照して目標を変える処理はない。\n");
            foreach (JObject trial in trials)
            {
                text.AppendLine("## " + trial["policy"] + "\n");
                text.AppendLine("| ラウンド | 必須素材:目標 → 終了時品質換算量（到達時刻） | 必須すべて到達 | 結果 |\n|---|---|---|---|");
                foreach (JObject row in (JArray)trial["rounds"])
                {
                    string goals = string.Join(" / ", ((JArray)row["required"]).Cast<JObject>().Select(goal =>
                        goal["material"] + ":" + goal["target"] + " → " + goal["final"] + "（" + Clock(goal["metAtSeconds"]) + "）"));
                    text.AppendLine("| R" + row["round"] + " | " + goals + " | " + Clock(row["requiredMetAtSeconds"]) + " | " + row["phase"] + " |");
                }
                text.AppendLine("\n失敗した試行はそこで終了する。後続ラウンドを成功扱いで作らない。各Rの目標生成前Lv、見込み購入とLv、期待供給、実購入、実購入直後Lv、資金・ボーナス・スコアは `balance.json` に保存。endOfRoundPlayerUpgradesは精算後状態なので、失敗時は通常ルールで0へ戻る。\n");
            }
            text.AppendLine("## 目標生成と達成可能判定\n");
            text.AppendLine("素材別基準量の設定範囲を抽選し、roundGrowth^(R−1)、[1 + highestQualityWeight × (当該R最高品質 / R1最高品質 − 1)]、[1 + Σ(見込み強化ごとのチーム平均Lv × upgradeWeights)]と[1 + upgradeSupplyWeight × max(0,見込み強化期待供給 / 同条件の無強化期待供給 − 1)]を掛ける。後ろの2項がチーム強化補正。実際の目標は、その結果と見込み期待供給×maximumCapacityFractionの小さい方。M6の基準量・成長率1.35・品質補正0.35・容量率0.85・実効供給重み1は変更せず、見込み予算率0.5だけを追加した。全係数は `balance.json` に同梱する。\n");
            text.AppendLine("供給シミュレーションは全員の5分を必須3種＋ボーナスに分け、実際のA*移動時間、往復、1訪問の採集回数、共有在庫と40秒再生、見込み採集速度／量／運、バイオーム・資源の品質分布、有限回数の最高品質連続期待量を含む。見込み購入は開始資金で実行可能であり、購入そのものの採集時間コストは通常ルールと同じ0秒。ボットは現在Lvのまま。候補不足や供給ゼロは再抽選／拒否する。品質の上振れ・下振れがあるので、『購入可能な想定能力を使い、全員が適切に採る場合の期待供給に収まる』保証であり、全乱数列・放置・投資しない方針・現在の単純ボット方針での必勝保証ではない。\n");
            text.AppendLine("単調性は素材抽選の違う総量を足して比較せず、同素材・同シード・同じ位置と資源状態で比較する。R1→R5は式の値に加え、上限適用後のGeneratePlanの返却目標も3シードで厳密増加を確認する。各強化は目標を下げず、4項目を増やした場合は全目標が厳密増加する。移動速度や採集速度だけを増やしても資源在庫が上限になる素材では、達成可能性を守るため同値を認める。\n");
            text.AppendLine("## M6との比較と限界\n");
            JObject budgetTrial = trials.Cast<JObject>().Single(trial => trial["policy"].Value<string>() == "unlimited-budget-upgrades");
            JObject fifth = ((JArray)budgetTrial["rounds"]).Cast<JObject>().FirstOrDefault(row => row["round"].Value<int>() == 5);
            text.AppendLine("M6の同シード・予算内無制限試行はR5を12.478390秒で達成した。今回のR5は" + (fifth == null ? "未到達" : fifth["requiredMetAtSeconds"].ToString()) + "秒（" + Clock(fifth?["requiredMetAtSeconds"]) + "）。大量投資後の採集力を目標が予見できず数秒で終わる問題を改善した。比較元は `../m6/balance.json`。\n");
            text.AppendLine("無強化と各項目+1Lv/Rの計画的試行も同条件で残す。後半に大きい共有資金があるのに投資しない場合は見込み能力へ届かず、失敗しうる。開始後に全資金を使う、安い順以外に集中投資する、品質連続が大きく上振れるケースの短縮はあり得る。見込み予算率はテンポ調整値であり、全プレイ方針のクリア時刻を保証しない。採集式、連続倍率、強化の上限、実購入価格、資金報酬は変更していない。\n");
            text.AppendLine("## 追加Coreテスト\n");
            text.AppendLine("`ObjectiveUpgradeForecastTests` 15ケース: 実価格の安い順／単一予算／ボット除外／入力不変、複数人間の配分・決定性・10^50資金、設定と不正値、5Rの見込みLvと明示購入相当Lvによる目標・供給一致と作業予定の再生、3シードの最終目標のR成長と資金増による非減少、サーバー初期目標と実購入後固定、次Rの精算済資金・Lvの参照、Bot有無それぞれのRetry初期化。既存ObjectiveInflationTests 27件とWorldObjectiveTests 14件も継続検証する。\n");
            return text.ToString().TrimEnd() + "\n";
        }
        private static string Clock(JToken seconds) => seconds == null || seconds.Type == JTokenType.Null ? "未達成" : GameHudProjection.Clock(seconds.Value<decimal>());
    }
}
