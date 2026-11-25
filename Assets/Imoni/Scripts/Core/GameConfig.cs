using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Numerics;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Imoni.Core
{
    /// <summary>Validated, immutable rules loaded from the project's only tuning source.</summary>
    public sealed class GameConfig
    {
        public int TeamSize { get; }
        public decimal DurationSeconds { get; }
        public decimal BonusRevealSeconds { get; }
        public int RoundsToClear { get; }
        public BigInteger StartFunds { get; }
        public decimal BuyPriceMultiplier { get; }
        public string PurchasedQualityId { get; }
        public decimal BonusFundsPerPoint { get; }
        public BigInteger FixedScale { get; }
        public decimal TopBaseChance { get; }
        public decimal LuckPerLevel { get; }
        public int StreakBase { get; }
        public int StreakDivisor { get; }
        public decimal UpgradePriceGrowth { get; }
        public decimal BaseMoveSpeed { get; }
        public decimal BaseCollectSeconds { get; }
        public decimal RequiredWeight { get; }
        public decimal BonusWeight { get; }
        public int FormatUnitBase { get; }
        public int FormatDecimalPlaces { get; }
        public IReadOnlyList<string> FormatUnits { get; }
        public IReadOnlyList<MaterialDefinition> Materials { get; }
        public IReadOnlyList<QualityDefinition> Qualities { get; }
        public IReadOnlyList<UpgradeDefinition> Upgrades { get; }
        public IReadOnlyList<BonusTier> BonusTiers { get; }
        public RoundObjectives DefaultObjectives { get; }

        private GameConfig(JObject data)
        {
            TeamSize = PositiveInt(data, "team.size");
            DurationSeconds = PositiveDecimal(data, "round.durationSec");
            BonusRevealSeconds = NonnegativeDecimal(data, "round.bonusRevealSec");
            Require(BonusRevealSeconds < DurationSeconds, "bonusRevealSec must precede durationSec.");
            RoundsToClear = PositiveInt(data, "round.roundsToClear");
            StartFunds = Integer(data, "economy.startFunds", false);
            BuyPriceMultiplier = PositiveDecimal(data, "economy.buyPriceMultiplier");
            PurchasedQualityId = Text(data, "economy.purchasedQualityId");
            BonusFundsPerPoint = NonnegativeDecimal(data, "economy.bonusFundsPerPoint");
            FixedScale = Integer(data, "numeric.fixedScale", true);
            TopBaseChance = NonnegativeDecimal(data, "qualityRoll.topBaseChance");
            Require(TopBaseChance <= 1, "topBaseChance must be a probability.");
            LuckPerLevel = NonnegativeDecimal(data, "qualityRoll.luckPerLevel");
            StreakBase = PositiveInt(data, "streak.base");
            StreakDivisor = PositiveInt(data, "streak.divisor");
            UpgradePriceGrowth = PositiveDecimal(data, "upgrades.priceGrowth");
            Require(UpgradePriceGrowth >= 1, "Upgrade prices must not shrink with level.");
            BaseMoveSpeed = PositiveDecimal(data, "gathering.baseMoveSpeedPxPerSec");
            BaseCollectSeconds = PositiveDecimal(data, "gathering.baseCollectSec");
            RequiredWeight = NonnegativeDecimal(data, "score.requiredWeight");
            BonusWeight = NonnegativeDecimal(data, "score.bonusWeight");
            Require(RequiredWeight > 0 || BonusWeight > 0, "At least one score weight must be positive.");
            FormatUnitBase = PositiveInt(data, "format.unitBase");
            Require(FormatUnitBase > 1, "format.unitBase must exceed one.");
            FormatDecimalPlaces = NonnegativeInt(data, "format.decimalPlaces");
            FormatUnits = ReadOnly(Array(data, "format.units").Select(token =>
            {
                Require(token.Type == JTokenType.String, "Format units must be strings.");
                return token.Value<string>();
            }));
            Require(FormatUnits.Count > 0 && FormatUnits[0] == string.Empty,
                "The first format unit must be an empty string.");
            Materials = ReadOnly(Array(data, "materials").Select(token => new MaterialDefinition(
                Text(token, "id"), Text(token, "name"), Integer(token, "basePoint", true),
                Integer(token, "basePrice", true), Integer(token, "baseYield", true))));
            Qualities = ReadOnly(Array(data, "qualities").Select(token => new QualityDefinition(
                Text(token, "id"), Text(token, "name"), Integer(token, "multiplier", true),
                PositiveInt(token, "unlockRound"), PositiveDecimal(token, "weight"))));
            Upgrades = ReadOnly(Array(data, "upgrades.items").Select(token => new UpgradeDefinition(
                Text(token, "id"), Text(token, "name"), Integer(token, "basePrice", true),
                NonnegativeDecimal(token, "effectPerLevel"))));
            BonusTiers = ReadOnly(Array(data, "bonusTiers").Select(token => new BonusTier(
                PositiveInt(token, "minPercent"), Integer(token, "multiplier", false)))
                .OrderBy(tier => tier.MinPercent));
            Unique(Materials.Select(item => item.Id), "material");
            Unique(Qualities.Select(item => item.Id), "quality");
            Unique(Upgrades.Select(item => item.Id), "upgrade");
            Require(Materials.Count > 0 && Qualities.Count > 0 && BonusTiers.Count > 0,
                "Materials, qualities and bonus tiers must not be empty.");
            Require(Qualities.Any(item => item.UnlockRound <= 1), "Round one needs an unlocked quality.");
            Require(Qualities.Select(item => item.Multiplier).Distinct().Count() == Qualities.Count,
                "Quality multipliers must be distinct to identify the highest quality.");
            Require(Quality(PurchasedQualityId).UnlockRound <= 1, "Purchased quality must be unlocked initially.");
            foreach (string id in new[] { "move", "speed", "amount", "luck" }) Upgrade(id);
            Require(BonusTiers.Select(item => item.MinPercent).Distinct().Count() == BonusTiers.Count,
                "Duplicate bonus thresholds.");
            for (int index = 1; index < BonusTiers.Count; index++)
                Require(BonusTiers[index].Multiplier >= BonusTiers[index - 1].Multiplier,
                    "Bonus multipliers must not decrease at higher thresholds.");
            foreach (QualityDefinition quality in Qualities)
                Require(GameMath.ToScaled(quality.Weight, FixedScale) > 0, "Quality weight is below configured precision.");
            DefaultObjectives = new RoundObjectives(Array(data, "round1Mock.required")
                .Select(token => new Objective(Text(token, "material"), Integer(token, "amount", true))),
                new Objective(Text(data, "round1Mock.bonus.material"), Integer(data, "round1Mock.bonus.amount", true)));
            DefaultObjectives.Validate(this);
        }

        public static GameConfig FromJson(string json)
        {
            if (json == null) throw new ArgumentNullException(nameof(json));
            using (var reader = new JsonTextReader(new StringReader(json)) { FloatParseHandling = FloatParseHandling.Decimal })
                return new GameConfig(JObject.Load(reader));
        }

        public static GameConfig Load(string path) => FromJson(File.ReadAllText(path));
        public MaterialDefinition Material(string id) => Materials.FirstOrDefault(item => item.Id == id)
            ?? throw new ArgumentException("Unknown material: " + id, nameof(id));
        public QualityDefinition Quality(string id) => Qualities.FirstOrDefault(item => item.Id == id)
            ?? throw new ArgumentException("Unknown quality: " + id, nameof(id));
        public UpgradeDefinition Upgrade(string id) => Upgrades.FirstOrDefault(item => item.Id == id)
            ?? throw new ArgumentException("Unknown upgrade: " + id, nameof(id));

        private static ReadOnlyCollection<T> ReadOnly<T>(IEnumerable<T> items) => new List<T>(items).AsReadOnly();
        private static void Unique(IEnumerable<string> ids, string type)
        {
            string[] values = ids.ToArray();
            Require(values.Distinct(StringComparer.Ordinal).Count() == values.Length, "Duplicate " + type + " id.");
        }
        private static JToken Required(JToken data, string path) => data.SelectToken(path)
            ?? throw new ArgumentException("Missing GameConfig value: " + path);
        private static JArray Array(JToken data, string path) => Required(data, path) as JArray
            ?? throw new ArgumentException("GameConfig value must be an array: " + path);
        private static string Text(JToken data, string path)
        {
            JToken value = Required(data, path);
            Require(value.Type == JTokenType.String && !string.IsNullOrWhiteSpace(value.Value<string>()),
                "GameConfig value must be nonempty text: " + path);
            return value.Value<string>();
        }
        private static BigInteger Integer(JToken data, string path, bool positive)
        {
            JToken token = Required(data, path);
            Require(token.Type == JTokenType.Integer || token.Type == JTokenType.String,
                "GameConfig value must be an integer: " + path);
            Require(BigInteger.TryParse(token.ToString(), NumberStyles.Integer, CultureInfo.InvariantCulture, out BigInteger value),
                "Invalid integer: " + path);
            Require(positive ? value > 0 : value >= 0, "GameConfig integer is out of range: " + path);
            return value;
        }
        private static int PositiveInt(JToken data, string path) => checked((int)Integer(data, path, true));
        private static int NonnegativeInt(JToken data, string path) => checked((int)Integer(data, path, false));
        private static decimal NonnegativeDecimal(JToken data, string path)
        {
            JToken token = Required(data, path);
            Require(token.Type == JTokenType.Integer || token.Type == JTokenType.Float,
                "GameConfig value must be numeric: " + path);
            decimal value = token.Value<decimal>();
            Require(value >= 0, "GameConfig value cannot be negative: " + path);
            return value;
        }
        private static decimal PositiveDecimal(JToken data, string path)
        {
            decimal value = NonnegativeDecimal(data, path);
            Require(value > 0, "GameConfig value must be positive: " + path);
            return value;
        }
        private static void Require(bool condition, string message)
        {
            if (!condition) throw new ArgumentException(message);
        }
    }

    public sealed class MaterialDefinition
    {
        public string Id { get; }
        public string Name { get; }
        public BigInteger BasePoint { get; }
        public BigInteger BasePrice { get; }
        public BigInteger BaseYield { get; }
        internal MaterialDefinition(string id, string name, BigInteger point, BigInteger price, BigInteger yield)
        { Id = id; Name = name; BasePoint = point; BasePrice = price; BaseYield = yield; }
    }

    public sealed class QualityDefinition
    {
        public string Id { get; }
        public string Name { get; }
        public BigInteger Multiplier { get; }
        public int UnlockRound { get; }
        public decimal Weight { get; }
        internal QualityDefinition(string id, string name, BigInteger multiplier, int unlock, decimal weight)
        { Id = id; Name = name; Multiplier = multiplier; UnlockRound = unlock; Weight = weight; }
    }

    public sealed class UpgradeDefinition
    {
        public string Id { get; }
        public string Name { get; }
        public BigInteger BasePrice { get; }
        public decimal EffectPerLevel { get; }
        internal UpgradeDefinition(string id, string name, BigInteger price, decimal effect)
        { Id = id; Name = name; BasePrice = price; EffectPerLevel = effect; }
    }

    public sealed class BonusTier
    {
        public int MinPercent { get; }
        public BigInteger Multiplier { get; }
        internal BonusTier(int minPercent, BigInteger multiplier) { MinPercent = minPercent; Multiplier = multiplier; }
    }
}
