using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Numerics;

namespace Imoni.Core
{
    /// <summary>A detached, affordable purchase plan used only when a round's objectives are generated.</summary>
    public sealed class ObjectiveUpgradeForecast
    {
        public BigInteger Budget { get; }
        public BigInteger Spent { get; }
        public IReadOnlyList<PlayerSnapshot> Players { get; }
        public IReadOnlyList<AnticipatedUpgradePurchase> Purchases { get; }

        private ObjectiveUpgradeForecast(BigInteger budget, BigInteger spent, PlayerSnapshot[] players,
            IList<AnticipatedUpgradePurchase> purchases)
        {
            Budget = budget; Spent = spent;
            Players = Array.AsReadOnly(players);
            Purchases = new ReadOnlyCollection<AnticipatedUpgradePurchase>(purchases.ToArray());
        }

        public static ObjectiveUpgradeForecast Create(GameConfig config, IEnumerable<PlayerSnapshot> players, BigInteger sharedFunds)
        {
            if (config == null) throw new ArgumentNullException(nameof(config));
            if (players == null) throw new ArgumentNullException(nameof(players));
            if (sharedFunds < 0) throw new ArgumentOutOfRangeException(nameof(sharedFunds));
            PlayerSnapshot[] team = players.ToArray();
            if (team.Length != config.TeamSize) throw new ArgumentException("Supply every team member.", nameof(players));
            var levels = team.Select(player => player.Upgrades.ToDictionary(item => item.Key, item => item.Value)).ToArray();
            BigInteger budget = GameMath.MultiplyFloor(sharedFunds, config.ObjectiveGeneration.AnticipatedUpgradeBudgetFraction);
            BigInteger remaining = budget;
            var purchases = new List<AnticipatedUpgradePurchase>();
            var rules = new UpgradeRules(config);
            while (true)
            {
                int chosenActor = -1;
                string chosenUpgrade = null;
                BigInteger chosenPrice = remaining + 1;
                // Bots collect autonomously but never buy upgrades. Stable priority and actor order
                // break equal-price ties without consuming the gameplay RNG.
                foreach (string upgrade in config.ObjectiveGeneration.AnticipatedUpgradePriority)
                    for (int actor = 0; actor < team.Length; actor++)
                    {
                        if (team[actor].IsBot) continue;
                        BigInteger price = rules.Price(upgrade, levels[actor][upgrade]);
                        if (price > remaining || price >= chosenPrice) continue;
                        chosenActor = actor; chosenUpgrade = upgrade; chosenPrice = price;
                    }
                if (chosenActor < 0) break;
                int level = checked(levels[chosenActor][chosenUpgrade] + 1);
                levels[chosenActor][chosenUpgrade] = level;
                remaining -= chosenPrice;
                purchases.Add(new AnticipatedUpgradePurchase(team[chosenActor].Id, chosenUpgrade, level, chosenPrice));
            }
            PlayerSnapshot[] forecast = team.Select((player, actor) => new PlayerSnapshot(player.Id, player.Streak,
                levels[actor], player.Position, player.NextPosition, player.Action, player.TargetResourceId,
                player.ActionRemainingSeconds, player.ActionDurationSeconds, player.Connected, player.IsBot, player.Location)).ToArray();
            return new ObjectiveUpgradeForecast(budget, budget - remaining, forecast, purchases);
        }
    }

    public sealed class AnticipatedUpgradePurchase
    {
        public string ActorId { get; }
        public string UpgradeId { get; }
        public int Level { get; }
        public BigInteger Price { get; }
        internal AnticipatedUpgradePurchase(string actor, string upgrade, int level, BigInteger price)
        { ActorId = actor; UpgradeId = upgrade; Level = level; Price = price; }
    }
}
