using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json.Linq;

namespace Imoni.Core
{
    /// <summary>World tuning; grid arithmetic constants are not balance values.</summary>
    public sealed class WorldConfig
    {
        public int Width { get; }
        public int Height { get; }
        public int ChunkSize { get; }
        public decimal TileSizePx { get; }
        public int StartClearRadius { get; }
        public IReadOnlyList<int> UnlockRadii { get; }
        public int EndlessRadiusStep { get; }
        public int NoiseCellSize { get; }
        public int NoiseResolution { get; }
        public IReadOnlyList<BiomeThreshold> BiomeThresholds { get; }
        public int RiverWidth { get; }
        public int RiverOffsetTiles { get; }
        public int RiverMeanderTiles { get; }
        public int RiverBendStep { get; }
        public int BridgeSpacing { get; }
        public decimal ResourceChance { get; }
        public int MinimumNodesPerMaterial { get; }
        public IReadOnlyList<string> StartingMaterials { get; }

        public WorldConfig(JObject data)
        {
            JToken world = data?["world"] ?? throw new ArgumentException("Missing world configuration.");
            Width = PositiveInt(world, "width");
            Height = PositiveInt(world, "height");
            ChunkSize = PositiveInt(world, "chunkSize");
            Require(Width % ChunkSize == 0 && Height % ChunkSize == 0, "World dimensions must contain whole chunks.");
            TileSizePx = PositiveDecimal(world, "tileSizePx");
            StartClearRadius = PositiveInt(world, "startClearRadius");
            Require(StartClearRadius < Math.Min(Width, Height) / 2, "Starting clearing must fit inside the map.");
            UnlockRadii = Array.AsReadOnly(RequiredArray(world, "unlockRadii").Select(token =>
            {
                int value = token.Value<int>();
                Require(value > 0 && token.Type == JTokenType.Integer, "Unlock radii must be positive integers.");
                return value;
            }).ToArray());
            Require(UnlockRadii.Count > 0, "At least one unlock radius is required.");
            Require(UnlockRadii.SequenceEqual(UnlockRadii.OrderBy(radius => radius).Distinct()), "Unlock radii must increase.");
            EndlessRadiusStep = PositiveInt(world, "endlessRadiusStep");
            NoiseCellSize = PositiveInt(world, "noiseCellSize");
            NoiseResolution = PositiveInt(world, "noiseResolution");
            BiomeThresholds = Array.AsReadOnly(RequiredArray(world, "biomeThresholds").Select(token =>
                new BiomeThreshold(RequiredText(token, "id"), PositiveDecimal(token, "max"))).ToArray());
            Require(BiomeThresholds.Count > 0 && BiomeThresholds.Last().Maximum == 1,
                "Biome thresholds must end at one.");
            decimal previous = 0;
            var biomes = new HashSet<string>(StringComparer.Ordinal);
            foreach (BiomeThreshold threshold in BiomeThresholds)
            {
                Require(threshold.Maximum > previous && threshold.Maximum <= 1 && biomes.Add(threshold.Id),
                    "Biome thresholds must be unique and increasing.");
                previous = threshold.Maximum;
            }
            Require(biomes.SetEquals(new[] { "plain", "forest", "swamp", "mountain" }), "Configure all four land biomes.");
            RiverWidth = PositiveInt(world, "riverWidth");
            RiverOffsetTiles = PositiveInt(world, "riverOffsetTiles");
            RiverMeanderTiles = PositiveInt(world, "riverMeanderTiles");
            RiverBendStep = PositiveInt(world, "riverBendStep");
            BridgeSpacing = PositiveInt(world, "bridgeSpacing");
            Require(RiverOffsetTiles - RiverMeanderTiles > StartClearRadius,
                "River must leave the starting clearing dry.");
            Require(RiverOffsetTiles + RiverWidth < UnlockRadii[0], "The first area needs an accessible river bank.");
            Require(RiverOffsetTiles + RiverMeanderTiles + RiverWidth < Width / 2, "River must fit inside the map.");
            JToken chance = world["resourceChance"];
            Require(chance != null && (chance.Type == JTokenType.Integer || chance.Type == JTokenType.Float),
                "Missing resource probability.");
            ResourceChance = chance.Value<decimal>();
            Require(ResourceChance >= 0 && ResourceChance <= 1, "Resource chance must be a probability.");
            MinimumNodesPerMaterial = PositiveInt(world, "minimumNodesPerMaterial");
            StartingMaterials = Array.AsReadOnly(RequiredArray(world, "startingMaterials")
                .Select(token => token.Value<string>()).ToArray());
            Require(StartingMaterials.Count > 0 && StartingMaterials.All(id => !string.IsNullOrWhiteSpace(id)) &&
                StartingMaterials.Distinct(StringComparer.Ordinal).Count() == StartingMaterials.Count,
                "Starting materials must be distinct IDs.");
        }

        public long UnlockRadius(int round)
        {
            if (round <= 0) throw new ArgumentOutOfRangeException(nameof(round));
            if (round <= UnlockRadii.Count) return UnlockRadii[round - 1];
            return UnlockRadii[UnlockRadii.Count - 1] + (long)(round - UnlockRadii.Count) * EndlessRadiusStep;
        }

        private static int PositiveInt(JToken data, string key)
        {
            JToken value = data[key];
            Require(value != null && value.Type == JTokenType.Integer && value.Value<long>() > 0 && value.Value<long>() <= int.MaxValue,
                "Missing or invalid world integer: " + key);
            return value.Value<int>();
        }
        private static decimal PositiveDecimal(JToken data, string key)
        {
            JToken value = data[key];
            Require(value != null && (value.Type == JTokenType.Integer || value.Type == JTokenType.Float) && value.Value<decimal>() > 0,
                "Missing or invalid world number: " + key);
            return value.Value<decimal>();
        }
        private static JArray RequiredArray(JToken data, string key) => data[key] as JArray
            ?? throw new ArgumentException("Missing world array: " + key);
        private static string RequiredText(JToken data, string key)
        {
            string text = data[key]?.Value<string>();
            Require(!string.IsNullOrWhiteSpace(text), "Missing world text: " + key);
            return text;
        }
        private static void Require(bool condition, string message)
        {
            if (!condition) throw new ArgumentException(message);
        }
    }

    public sealed class BiomeThreshold
    {
        public string Id { get; }
        public decimal Maximum { get; }
        internal BiomeThreshold(string id, decimal maximum) { Id = id; Maximum = maximum; }
    }
}
