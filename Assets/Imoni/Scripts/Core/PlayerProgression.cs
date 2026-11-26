using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Numerics;
using Newtonsoft.Json.Linq;

namespace Imoni.Core
{
    public sealed class ProgressionStats
    {
        public int BestRound { get; set; }
        public int SuccessfulRound { get; set; }
        public int BestStreak { get; set; }
        public BigInteger NormalClears { get; set; }
        public BigInteger BestScore { get; set; }
        public Dictionary<string, BigInteger> GatheredByMaterial { get; set; } = new Dictionary<string, BigInteger>();
        public string[] GatheredQualities { get; set; } = Array.Empty<string>();
    }

    public sealed class ProgressionOutcome
    {
        public ProfileData Profile { get; internal set; }
        public IReadOnlyList<EarnedTitlePart> Titles { get; internal set; }
        public IReadOnlyList<string> BadgeIds { get; internal set; }
    }

    /// <summary>Permanent achievements from authoritative, detached results. No display fixture grants ownership.</summary>
    public static class PlayerProgression
    {
        public static ProfileData ReachRound(JObject data, ProfileData profile, int round)
        {
            if (round <= 0) throw new ArgumentOutOfRangeException(nameof(round));
            ProfileData next = ProfilePersistence.Normalize(data, profile);
            next.Progression.BestRound = Math.Max(next.Progression.BestRound, round);
            foreach (JToken rule in data["progression"]["badgeRules"])
                if (rule["metric"].Value<string>() == "bestRound" && Meets(next.Progression, rule) && !next.OwnedBadges.Contains(rule["id"].Value<string>(), StringComparer.Ordinal))
                    next.OwnedBadges = next.OwnedBadges.Concat(new[] { rule["id"].Value<string>() }).ToArray();
            return next;
        }

        public static ProgressionOutcome Apply(JObject data, ProfileData profile, RoundResult result, string actorId, string roundKey)
        {
            if (result == null) throw new ArgumentNullException(nameof(result));
            if (string.IsNullOrWhiteSpace(roundKey)) throw new ArgumentException("A unique challenge/round key is required.", nameof(roundKey));
            var next = ProfilePersistence.Normalize(data, profile);
            var titles = new List<EarnedTitlePart>();
            var badges = new List<string>();
            var outcome = new ProgressionOutcome { Profile = next, Titles = titles.AsReadOnly(), BadgeIds = badges.AsReadOnly() };
            if (next.AppliedRoundKeys.Contains(roundKey, StringComparer.Ordinal)) return outcome;

            ProgressionStats stats = next.Progression;
            stats.BestRound = Math.Max(stats.BestRound, result.RoundNumber);
            if (result.Evaluation.Success)
            {
                stats.SuccessfulRound = Math.Max(stats.SuccessfulRound, result.RoundNumber);
                stats.BestScore = BigInteger.Max(stats.BestScore, result.CumulativeScore);
                if (!result.IsEndless && result.RoundNumber == data["round"]["roundsToClear"].Value<int>()) stats.NormalClears++;
            }
            var qualities = new HashSet<string>(stats.GatheredQualities, StringComparer.Ordinal);
            foreach (OperationRecord operation in result.History.Where(item => item.RoundNumber == result.RoundNumber && item.ActorId == actorId && item.Kind == OperationKind.Gather))
            {
                stats.GatheredByMaterial.TryGetValue(operation.MaterialId, out BigInteger count);
                stats.GatheredByMaterial[operation.MaterialId] = count + operation.Quantity;
                stats.BestStreak = Math.Max(stats.BestStreak, operation.GatherStreak);
                qualities.Add(operation.QualityId);
            }
            stats.GatheredQualities = qualities.OrderBy(value => value, StringComparer.Ordinal).ToArray();
            // Titles celebrate successful feasts. Personal gathering/reach badges can survive a failed feast.
            if (result.Evaluation.Success)
                foreach (JToken rule in data["progression"]["titleRules"])
                {
                    string part = rule["part"].Value<string>(), value = rule["value"].Value<string>();
                    string[] owned = part == "first" ? next.OwnedFirstTitles : next.OwnedSecondTitles;
                    if (owned.Contains(value, StringComparer.Ordinal) || !Meets(stats, rule)) continue;
                    titles.Add(new EarnedTitlePart(part, value));
                    if (part == "first") next.OwnedFirstTitles = owned.Concat(new[] { value }).ToArray();
                    else next.OwnedSecondTitles = owned.Concat(new[] { value }).ToArray();
                }
            foreach (JToken rule in data["progression"]["badgeRules"])
            {
                string id = rule["id"].Value<string>();
                if (next.OwnedBadges.Contains(id, StringComparer.Ordinal) || !Meets(stats, rule)) continue;
                badges.Add(id); next.OwnedBadges = next.OwnedBadges.Concat(new[] { id }).ToArray();
            }
            next.AppliedRoundKeys = next.AppliedRoundKeys.Concat(new[] { roundKey }).ToArray();
            outcome.Profile = ProfilePersistence.Normalize(data, next);
            return outcome;
        }

        private static bool Meets(ProgressionStats stats, JToken rule)
        {
            if (!BigInteger.TryParse(rule["minimum"]?.ToString(), NumberStyles.Integer, CultureInfo.InvariantCulture, out BigInteger minimum) || minimum < 0)
                throw new ArgumentException("Invalid progression minimum.");
            BigInteger value;
            switch (rule["metric"]?.Value<string>())
            {
                case "successfulRound": value = stats.SuccessfulRound; break;
                case "bestRound": value = stats.BestRound; break;
                case "bestStreak": value = stats.BestStreak; break;
                case "normalClears": value = stats.NormalClears; break;
                case "bestScore": value = stats.BestScore; break;
                case "gatheredTotal": value = stats.GatheredByMaterial.Values.Aggregate(BigInteger.Zero, (sum, quantity) => sum + quantity); break;
                case "materialGathered": stats.GatheredByMaterial.TryGetValue(rule["materialId"].Value<string>(), out value); break;
                case "qualityGathered": value = stats.GatheredQualities.Contains(rule["qualityId"].Value<string>(), StringComparer.Ordinal) ? BigInteger.One : BigInteger.Zero; break;
                default: throw new ArgumentException("Unknown progression metric.");
            }
            return value >= minimum;
        }

        internal static ProgressionStats Normalize(JObject data, ProgressionStats source)
        {
            source = source ?? new ProgressionStats();
            var materials = new HashSet<string>(data["materials"].Select(item => item["id"].Value<string>()), StringComparer.Ordinal);
            var qualities = new HashSet<string>(data["qualities"].Select(item => item["id"].Value<string>()), StringComparer.Ordinal);
            return new ProgressionStats
            {
                BestRound = Math.Max(0, source.BestRound), SuccessfulRound = Math.Max(0, source.SuccessfulRound), BestStreak = Math.Max(0, source.BestStreak),
                NormalClears = BigInteger.Max(0, source.NormalClears), BestScore = BigInteger.Max(0, source.BestScore),
                GatheredByMaterial = (source.GatheredByMaterial ?? new Dictionary<string, BigInteger>()).Where(pair => materials.Contains(pair.Key) && pair.Value >= 0)
                    .ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.Ordinal),
                GatheredQualities = (source.GatheredQualities ?? Array.Empty<string>()).Where(qualities.Contains).Distinct(StringComparer.Ordinal).ToArray()
            };
        }
    }
}
