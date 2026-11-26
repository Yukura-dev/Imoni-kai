using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json.Linq;

namespace Imoni.Core
{
    public sealed class RankGradeDefinition
    {
        public string Grade { get; }
        public int MinimumBonusPercent { get; }
        internal RankGradeDefinition(string grade, int minimum) { Grade = grade; MinimumBonusPercent = minimum; }
    }

    public sealed class TitleUnlockRule
    {
        public string Part { get; }
        public string Value { get; }
        public int MinimumRound { get; }
        public int MinimumStreak { get; }
        public bool RequiresClear { get; }
        internal TitleUnlockRule(string part, string value, int round, int streak, bool clear)
        { Part = part; Value = value; MinimumRound = round; MinimumStreak = streak; RequiresClear = clear; }
    }

    public sealed class RoundResultRules
    {
        public IReadOnlyList<RankGradeDefinition> Grades { get; }
        public IReadOnlyList<TitleUnlockRule> TitleUnlocks { get; }

        internal RoundResultRules(JObject data)
        {
            JArray grades = data["rankGrades"] as JArray ?? throw new ArgumentException("Missing rankGrades.");
            Grades = Array.AsReadOnly(grades.Select(token => new RankGradeDefinition(Text(token, "grade"), Integer(token, "minBonusPercent", false)))
                .OrderByDescending(grade => grade.MinimumBonusPercent).ToArray());
            if (Grades.Count == 0 || Grades.Last().MinimumBonusPercent != 0 || Grades.Select(grade => grade.MinimumBonusPercent).Distinct().Count() != Grades.Count)
                throw new ArgumentException("Rank grades must have distinct thresholds including zero.");
            JArray unlocks = data.SelectToken("titles.unlockRules") as JArray ?? throw new ArgumentException("Missing titles.unlockRules.");
            TitleUnlocks = Array.AsReadOnly(unlocks.Select(token =>
            {
                string part = Text(token, "part"), value = Text(token, "value");
                if ((part != "first" && part != "second") || !(data["titles"][part + "Parts"] as JArray).Values<string>().Contains(value))
                    throw new ArgumentException("Title unlock must reference a catalog part.");
                if (token["requiresClear"]?.Type != JTokenType.Boolean) throw new ArgumentException("requiresClear must be boolean.");
                return new TitleUnlockRule(part, value, Integer(token, "minimumRound", true), Integer(token, "minimumStreak", false), token["requiresClear"].Value<bool>());
            }).ToArray());
            if (TitleUnlocks.Select(rule => rule.Part + "|" + rule.Value).Distinct(StringComparer.Ordinal).Count() != TitleUnlocks.Count)
                throw new ArgumentException("Title unlock rules must be unique by part.");
        }
        private static string Text(JToken data, string key) => data[key]?.Type == JTokenType.String && !string.IsNullOrWhiteSpace(data[key].Value<string>())
            ? data[key].Value<string>() : throw new ArgumentException("Missing result text: " + key);
        private static int Integer(JToken data, string key, bool positive)
        {
            if (data[key]?.Type != JTokenType.Integer) throw new ArgumentException("Missing result integer: " + key);
            int value = data[key].Value<int>();
            if (positive ? value <= 0 : value < 0) throw new ArgumentException("Result integer is out of range: " + key);
            return value;
        }
    }
}
