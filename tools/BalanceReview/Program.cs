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
            string output = args.Length > 1 ? Path.GetFullPath(args[1]) : Path.Combine(root, "docs/validation/m6");
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
                JObject startingUpgrades = JObject.FromObject(initial.Players.First(player => player.Id == actor).Upgrades);
                JArray purchases = policy == "no-upgrades" ? new JArray() : UpgradePlayer(server, actor, config, data, policy == "planned-upgrades");
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
                string next = priority.OrderBy(id => player.Upgrades[id]).ThenBy(id => Array.IndexOf(priority, id))
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
            var text = new StringBuilder("# M6 目標インフレの自動操作記録\n\n");
            text.AppendLine("再実行: `dotnet run --project tools/BalanceReview -- . docs/validation/m6`。シードはGameConfig.balanceReview.seed（" + data["balanceReview"]["seed"] + "）。");
            text.AppendLine("\nM5の自動操作と同じ必須優先・最寄り到達可能資源選択を使用。移動・採集・成長は通常のIGameServer.Tick、末尾の不足購入も所持金内でShop対象素材だけをBuyする。ルール・数量・資金・品質抽選・結果を書き換えない。操作判断はbotIntervalSecごと、到達時刻は終了時のOperationRecordを再生し、品質換算量が初めて目標を超えた正確なelapsedを使う（表は秒以下切捨て、JSONに小数秒を保存）。ボーナスは公開前には選ばない。\n");
            text.AppendLine("強化あり試行だけ、各ラウンドの目標生成後にplayer1が現在資金の" + (data["balanceReview"]["upgradeBudgetFraction"].Value<decimal>() * 100).ToString(CultureInfo.InvariantCulture) + "%以内でUpgradeを購入する。低Lv優先、同Lvはamount→luck→speed→move。planned-upgradesは各項目" + data["balanceReview"]["plannedLevelsPerRound"] + "Lv/Rまで、unlimited-budget-upgradesは予算内で回数制限なし。両方の結果を残す。ボットの強化は代行しない。目標はそのラウンド開始時の強化値で既に確定しており、購入で下げたり再抽選したりしない。これはbalanceReviewのQA操作方針で、通常の目標生成係数とは別。\n");
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
                text.AppendLine("\n失敗した試行はそこで終了する。後続ラウンドを成功扱いで作らない。各Rの強化購入・目標生成時Lv・資金・ボーナス・スコアは `balance.json` に保存。\n");
            }
            text.AppendLine("## 目標生成と達成可能判定\n");
            text.AppendLine("素材別基準量の設定範囲を抽選し、roundGrowth^(R−1)、[1 + highestQualityWeight × (当該R最高品質 / R1最高品質 − 1)]、[1 + Σ(強化ごとのチーム平均Lv × upgradeWeights)]と[1 + upgradeSupplyWeight × max(0,実強化期待供給 / 同条件の無強化期待供給 − 1)]を掛ける。後ろの2項がチーム強化補正。実際の目標は、その結果と期待供給×maximumCapacityFractionの小さい方。係数の確定値は `balance.json` に同梱する。\n");
            text.AppendLine("供給シミュレーションは全員の5分を必須3種＋ボーナスに分け、実際のA*移動時間、往復、1訪問の採集回数、共有在庫と40秒再生、採集速度／量／運、バイオーム・資源の品質分布、有限回数の最高品質連続期待量を含む。候補不足や供給ゼロは再抽選／拒否する。品質の確率的な上振れ・下振れがあるので、『想定した能力と適切な操作の下での期待供給に収まる』保証であり、全乱数列・放置・無強化の勝利保証ではない。\n");
            text.AppendLine("単調性は素材抽選の違う総量を足して比較せず、同素材・同シード・同じ位置と資源状態で比較する。R1→R5は式の値に加え、上限適用後のGeneratePlanの返却目標も3シードで厳密増加を確認する。各強化は目標を下げず、4項目を増やした場合は全目標が厳密増加する。移動速度や採集速度だけを増やしても資源在庫が上限になる素材では、達成可能性を守るため同値を認める。\n");
            text.AppendLine("## 残るテンポ上の限界\n");
            text.AppendLine("計画的強化では後半も約2分の必須集めが必要になった。一方、予算だけを制限した投資では前Rのボーナス報酬を使って目標生成後に多数の強化を購入でき、R5は約12秒で必須を達成する。開始後の強化に合わせて確定目標を変更する仕様は追加しないため、このケースは残る。成功ケースだけを抽出せず、無強化R4失敗と投資による高速クリアを両方記録した。採集式、連続倍率、強化の上限、資金報酬は変更していない。\n");
            text.AppendLine("## 追加Coreテスト\n");
            text.AppendLine("`ObjectiveInflationTests` 27ケース: 素材6種の式のR1→R5成長、3シードの最終生成目標の成長、4個別強化＋全強化の非減少／増加、5R×満在庫／再生中の10個の作業スケジュール再生、最高品質確定時の有限連続量、2回採集の全品質列列挙との期待値照合、係数設定と10^100の基準量。スケジュールはResourceNodeを別途生成し、時刻順にTick/TryGatherを再生して共有在庫・再生・移動・採集時間を検証する。既存WorldObjectiveTestsの14ケースも、品質換算容量と新しい基準量抽選の契約に合わせて継続検証する。\n");
            return text.ToString().TrimEnd() + "\n";
        }
        private static string Clock(JToken seconds) => seconds == null || seconds.Type == JTokenType.Null ? "未達成" : GameHudProjection.Clock(seconds.Value<decimal>());
    }
}
