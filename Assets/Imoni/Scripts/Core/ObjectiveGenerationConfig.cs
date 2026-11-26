using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Numerics;
using Newtonsoft.Json.Linq;

namespace Imoni.Core
{
    public sealed class ObjectiveGenerationConfig
    {
        public int RequiredCount { get; }
        public decimal MinimumFraction { get; }
        public decimal MaximumFraction { get; }
        public decimal RoundGrowth { get; }
        public decimal HighestQualityWeight { get; }
        public decimal MaximumCapacityFraction { get; }
        public decimal UpgradeSupplyWeight { get; }
        public decimal AnticipatedUpgradeBudgetFraction { get; }
        public IReadOnlyList<string> AnticipatedUpgradePriority { get; }
        public int SimulationGathersPerVisit { get; }
        public IReadOnlyDictionary<string, BigInteger> BaseAmounts { get; }
        public IReadOnlyDictionary<string, decimal> UpgradeWeights { get; }
        public IReadOnlyList<string> FirstRoundRequiredMaterials { get; }

        internal ObjectiveGenerationConfig(JObject data)
        {
            JToken section = data["objectiveGeneration"] ?? throw new ArgumentException("Missing objectiveGeneration config.");
            if (section["requiredCount"]?.Type != JTokenType.Integer ||
                !IsNumber(section["minimumFraction"]) || !IsNumber(section["maximumFraction"]))
                throw new ArgumentException("Objective count must be an integer and fractions must be numeric.");
            RequiredCount = (int?)section["requiredCount"] ?? throw new ArgumentException("Missing requiredCount.");
            MinimumFraction = (decimal?)section["minimumFraction"] ?? throw new ArgumentException("Missing minimumFraction.");
            MaximumFraction = (decimal?)section["maximumFraction"] ?? throw new ArgumentException("Missing maximumFraction.");
            if (RequiredCount < 1 || MinimumFraction <= 0 || MinimumFraction > MaximumFraction)
                throw new ArgumentException("Invalid objective generation count/fractions.");
            RoundGrowth = Number(section, "roundGrowth");
            HighestQualityWeight = Number(section, "highestQualityWeight");
            MaximumCapacityFraction = Number(section, "maximumCapacityFraction");
            UpgradeSupplyWeight = Number(section, "upgradeSupplyWeight");
            AnticipatedUpgradeBudgetFraction = Number(section, "anticipatedUpgradeBudgetFraction");
            if (AnticipatedUpgradeBudgetFraction < 0 || AnticipatedUpgradeBudgetFraction > 1)
                throw new ArgumentException("Anticipated upgrade budget fraction must be between zero and one.");
            var priority = section["anticipatedUpgradePriority"] as JArray
                ?? throw new ArgumentException("Missing anticipatedUpgradePriority.");
            AnticipatedUpgradePriority = Array.AsReadOnly(priority.Values<string>().ToArray());
            if (!AnticipatedUpgradePriority.OrderBy(id => id, StringComparer.Ordinal).SequenceEqual(
                    new[] { "amount", "luck", "move", "speed" }))
                throw new ArgumentException("Anticipated upgrade priority must include each upgrade exactly once.");
            if (RoundGrowth <= 1 || HighestQualityWeight < 0 || HighestQualityWeight > 1 ||
                MaximumCapacityFraction <= 0 || MaximumCapacityFraction >= 1 || UpgradeSupplyWeight < 0 ||
                RoundGrowth * MinimumFraction <= MaximumFraction)
                throw new ArgumentException("Objective inflation must grow beyond its sampling range and remain within expected supply.");
            if (section["simulationGathersPerVisit"]?.Type != JTokenType.Integer || section["simulationGathersPerVisit"].Value<int>() < 1)
                throw new ArgumentException("simulationGathersPerVisit must be a positive integer.");
            SimulationGathersPerVisit = section["simulationGathersPerVisit"].Value<int>();
            JObject amounts = section["baseAmounts"] as JObject ?? throw new ArgumentException("Missing baseAmounts.");
            BaseAmounts = new ReadOnlyDictionary<string, BigInteger>(amounts.Properties().ToDictionary(item => item.Name, item =>
            {
                if (item.Value.Type != JTokenType.Integer && item.Value.Type != JTokenType.String)
                    throw new ArgumentException("Base amounts must be positive integers.");
                if (!BigInteger.TryParse(item.Value.ToString(), out BigInteger amount) || amount <= 0)
                    throw new ArgumentException("Base amounts must be positive integers.");
                return amount;
            }, StringComparer.Ordinal));
            JObject weights = section["upgradeWeights"] as JObject ?? throw new ArgumentException("Missing upgradeWeights.");
            UpgradeWeights = new ReadOnlyDictionary<string, decimal>(new[] { "move", "speed", "amount", "luck" }.ToDictionary(id => id, id =>
            {
                decimal weight = Number(weights, id);
                if (weight < 0) throw new ArgumentException("Upgrade goal weights cannot be negative.");
                return weight;
            }, StringComparer.Ordinal));
            var firstRound = section["firstRoundRequiredMaterials"] as JArray
                ?? throw new ArgumentException("Missing firstRoundRequiredMaterials.");
            FirstRoundRequiredMaterials = Array.AsReadOnly(firstRound.Select(token => token.Value<string>()).ToArray());
            if (FirstRoundRequiredMaterials.Count < RequiredCount || FirstRoundRequiredMaterials.Any(string.IsNullOrWhiteSpace) ||
                FirstRoundRequiredMaterials.Distinct(StringComparer.Ordinal).Count() != FirstRoundRequiredMaterials.Count)
                throw new ArgumentException("First-round required material pool must contain enough distinct material IDs.");
        }

        private static bool IsNumber(JToken token) => token?.Type == JTokenType.Integer || token?.Type == JTokenType.Float;
        private static decimal Number(JToken data, string key) => IsNumber(data[key]) ? data[key].Value<decimal>()
            : throw new ArgumentException("Missing objective number: " + key);
    }
}
