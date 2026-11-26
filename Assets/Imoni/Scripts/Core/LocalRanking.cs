using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using Newtonsoft.Json.Linq;

namespace Imoni.Core
{
    public enum RankingMode { Normal, Endless }
    public enum RankingSeed { Common, Free }

    public sealed class LocalRankingRecord
    {
        public string ChallengeId { get; set; }
        public string TeamName { get; set; }
        public int TeamSize { get; set; }
        public RankingMode Mode { get; set; }
        public RankingSeed SeedDivision { get; set; }
        public int Seed { get; set; }
        public int RoundNumber { get; set; }
        public BigInteger Score { get; set; }
        public DateTimeOffset RecordedAtUtc { get; set; }
    }

    /// <summary>Only completed normal clears or explicitly finished successful endless results become records.</summary>
    public static class LocalRanking
    {
        public static ProfileData Register(JObject data, ProfileData profile, RoundResult result, string challengeId,
            string teamName, int teamSize, RankingSeed division, DateTimeOffset recordedAtUtc, bool finishEndless = false)
        {
            ProfileData next = ProfilePersistence.Normalize(data, profile);
            if (result == null || !result.Evaluation.Success) return next;
            int clearRound = data["round"]["roundsToClear"].Value<int>();
            if (result.IsEndless ? !finishEndless || result.RoundNumber <= clearRound : result.RoundNumber != clearRound) return next;
            if (string.IsNullOrWhiteSpace(challengeId) || string.IsNullOrWhiteSpace(teamName) || teamSize != data["team"]["size"].Value<int>())
                throw new ArgumentException("Ranking requires the completed challenge's identity and team.");
            if (!Enum.IsDefined(typeof(RankingSeed), division)) throw new ArgumentException("Invalid seed division.");
            if (division == RankingSeed.Common && result.Seed != data["outGame"]["lobby"]["commonSeed"].Value<int>())
                throw new ArgumentException("A common-seed record must use the configured common seed.");
            RankingMode mode = result.IsEndless ? RankingMode.Endless : RankingMode.Normal;
            if (next.Rankings.Any(item => item.ChallengeId == challengeId && item.Mode == mode)) return next;
            next.Rankings = Normalize(data, next.Rankings.Concat(new[]
            {
                new LocalRankingRecord { ChallengeId = challengeId, TeamName = teamName.Trim(), TeamSize = teamSize,
                    Mode = mode, SeedDivision = division, Seed = result.Seed, RoundNumber = result.RoundNumber,
                    Score = result.CumulativeScore, RecordedAtUtc = recordedAtUtc.ToUniversalTime() }
            }).ToArray());
            return next;
        }

        public static IReadOnlyList<LocalRankingRecord> Board(ProfileData profile, RankingMode mode, RankingSeed division, int teamSize, int? commonSeed = null)
        {
            var rows = (profile.Rankings ?? Array.Empty<LocalRankingRecord>()).Where(row => row != null && row.Mode == mode && row.SeedDivision == division && row.TeamSize == teamSize
                && (division != RankingSeed.Common || !commonSeed.HasValue || row.Seed == commonSeed.Value));
            return Array.AsReadOnly(Order(rows, mode).ToArray());
        }

        private static IOrderedEnumerable<LocalRankingRecord> Order(IEnumerable<LocalRankingRecord> rows, RankingMode mode) =>
            rows.OrderByDescending(row => mode == RankingMode.Endless ? row.RoundNumber : 0).ThenByDescending(row => row.Score)
                .ThenBy(row => row.RecordedAtUtc).ThenBy(row => row.ChallengeId, StringComparer.Ordinal);

        internal static LocalRankingRecord[] Normalize(JObject data, IEnumerable<LocalRankingRecord> records)
        {
            int limit = data["progression"]["rankingLimitPerBoard"].Value<int>();
            if (limit <= 0) throw new ArgumentException("rankingLimitPerBoard must be positive.");
            int clearRound = data["round"]["roundsToClear"].Value<int>();
            return (records ?? Array.Empty<LocalRankingRecord>()).Where(row => row != null && !string.IsNullOrWhiteSpace(row.ChallengeId) && !string.IsNullOrWhiteSpace(row.TeamName)
                && row.TeamSize > 0 && row.Score >= 0 && row.RecordedAtUtc != default
                && Enum.IsDefined(typeof(RankingMode), row.Mode) && Enum.IsDefined(typeof(RankingSeed), row.SeedDivision)
                && (row.Mode == RankingMode.Normal ? row.RoundNumber == clearRound : row.RoundNumber > clearRound))
                .GroupBy(row => row.ChallengeId + "|" + row.Mode, StringComparer.Ordinal).Select(group => group.First())
                .GroupBy(row => new { row.Mode, row.SeedDivision, row.TeamSize, CommonSeed = row.SeedDivision == RankingSeed.Common ? row.Seed : 0 })
                .SelectMany(group => Order(group, group.Key.Mode).Take(limit))
                .Select(row => new LocalRankingRecord { ChallengeId = row.ChallengeId, TeamName = row.TeamName, TeamSize = row.TeamSize, Mode = row.Mode,
                    SeedDivision = row.SeedDivision, Seed = row.Seed, RoundNumber = row.RoundNumber, Score = row.Score, RecordedAtUtc = row.RecordedAtUtc.ToUniversalTime() }).ToArray();
        }
    }
}
