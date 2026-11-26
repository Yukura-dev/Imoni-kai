using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Numerics;
using Newtonsoft.Json.Linq;

namespace Imoni.Core
{
    /// <summary>Panel interaction settings share the same JSON source as the game rules.</summary>
    public sealed class GamePanelConfig
    {
        public BigInteger ProtectionStep { get; }
        public IReadOnlyList<int> SalePercentages { get; }
        public IReadOnlyList<BigInteger> ShopQuantities { get; }
        public IReadOnlyList<string> ShopMaterials { get; }
        public int HistoryLimit { get; }
        public IReadOnlyDictionary<string, int> EffectDecimalPlaces { get; }

        internal GamePanelConfig(JObject data, GameConfig config)
        {
            JToken settings = data.SelectToken("ui.panels") ?? throw new ArgumentException("Missing GameConfig value: ui.panels");
            ProtectionStep = PositiveInteger(settings["protectionStep"], "protectionStep");
            SalePercentages = System.Array.AsReadOnly(RequiredArray(settings, "salePercentages")
                .Select(token => checked((int)PositiveInteger(token, "salePercentages"))).ToArray());
            if (SalePercentages.Count == 0 || SalePercentages.Any(value => value > 100) || SalePercentages.Distinct().Count() != SalePercentages.Count)
                throw new ArgumentException("Panel sale percentages must be distinct and within 1..100.");
            ShopQuantities = System.Array.AsReadOnly(RequiredArray(settings, "shopQuantities")
                .Select(token => PositiveInteger(token, "shopQuantities")).ToArray());
            if (ShopQuantities.Count == 0 || ShopQuantities.Distinct().Count() != ShopQuantities.Count)
                throw new ArgumentException("Panel shop quantities must be nonempty and distinct.");
            ShopMaterials = System.Array.AsReadOnly(RequiredArray(settings, "shopMaterials").Select(token => token.Value<string>()).ToArray());
            if (ShopMaterials.Count == 0 || ShopMaterials.Distinct(StringComparer.Ordinal).Count() != ShopMaterials.Count)
                throw new ArgumentException("Panel shop materials must be nonempty and distinct.");
            foreach (string id in ShopMaterials) config.Material(id);
            HistoryLimit = checked((int)PositiveInteger(settings["historyLimit"], "historyLimit"));
            var precision = new Dictionary<string, int>(StringComparer.Ordinal);
            foreach (UpgradeDefinition upgrade in config.Upgrades)
            {
                JToken token = settings["effectDecimalPlaces"]?[upgrade.Id];
                if (token == null || token.Type != JTokenType.Integer || token.Value<int>() < 0)
                    throw new ArgumentException("Panel effect decimal places must be nonnegative integers.");
                precision.Add(upgrade.Id, token.Value<int>());
            }
            EffectDecimalPlaces = new System.Collections.ObjectModel.ReadOnlyDictionary<string, int>(precision);
        }

        private static JArray RequiredArray(JToken settings, string key) => settings[key] as JArray
            ?? throw new ArgumentException("Missing panel array: " + key);
        private static BigInteger PositiveInteger(JToken token, string key)
        {
            if (token == null || (token.Type != JTokenType.Integer && token.Type != JTokenType.String)
                || !BigInteger.TryParse(token.ToString(), NumberStyles.Integer, CultureInfo.InvariantCulture, out BigInteger value) || value <= 0)
                throw new ArgumentException("Panel value must be a positive integer: " + key);
            return value;
        }
    }
}
