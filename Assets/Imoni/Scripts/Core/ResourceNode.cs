using System;

namespace Imoni.Core
{
    public enum ResourceGrowthStage { Stump, Sapling, Young, Mature, Ready }

    /// <summary>A persistent world node. Only the authoritative Core simulation may consume or age it.</summary>
    public sealed class ResourceNode
    {
        private readonly GameConfig config;
        public string Id { get; }
        public string MaterialId { get; }
        public string BiomeId { get; }
        public GridPosition Position { get; }
        public decimal QualityModifier { get; }
        public decimal EfficiencyModifier { get; }
        public int RemainingStock { get; private set; }
        public decimal RegrowthSeconds { get; private set; }
        public decimal SecondsUntilReady => IsReady ? 0 : config.ResourceRegrowSeconds - RegrowthSeconds;
        public bool IsReady => RemainingStock > 0;
        public ResourceGrowthStage GrowthStage
        {
            get
            {
                if (IsReady) return ResourceGrowthStage.Ready;
                decimal progress = RegrowthSeconds / config.ResourceRegrowSeconds;
                int stage = 0;
                while (stage < config.ResourceGrowthStageFractions.Count &&
                    progress >= config.ResourceGrowthStageFractions[stage]) stage++;
                return (ResourceGrowthStage)stage;
            }
        }

        public ResourceNode(GameConfig config, string id, string materialId, string biomeId,
            GridPosition position, decimal qualityModifier = 1m, decimal efficiencyModifier = 1m)
        {
            this.config = config ?? throw new ArgumentNullException(nameof(config));
            if (string.IsNullOrWhiteSpace(id)) throw new ArgumentException("A resource ID is required.", nameof(id));
            MaterialDefinition material = config.Material(materialId);
            if (!System.Linq.Enumerable.Contains(material.Biomes, biomeId))
                throw new ArgumentException("The resource does not belong to this biome.", nameof(biomeId));
            if (qualityModifier <= 0 || efficiencyModifier <= 0)
                throw new ArgumentOutOfRangeException(nameof(qualityModifier));
            Id = id; MaterialId = materialId; BiomeId = biomeId; Position = position;
            QualityModifier = qualityModifier; EfficiencyModifier = efficiencyModifier;
            RemainingStock = config.ResourceStock;
        }

        internal bool TryGather()
        {
            if (!IsReady) return false;
            RemainingStock--;
            if (RemainingStock == 0) RegrowthSeconds = 0;
            return true;
        }

        internal void Tick(decimal seconds)
        {
            if (seconds < 0) throw new ArgumentOutOfRangeException(nameof(seconds));
            if (IsReady) return;
            RegrowthSeconds = seconds >= SecondsUntilReady ? config.ResourceRegrowSeconds : RegrowthSeconds + seconds;
            if (RegrowthSeconds >= config.ResourceRegrowSeconds)
            {
                RemainingStock = config.ResourceStock;
                RegrowthSeconds = 0;
            }
        }

        public ResourceSnapshot Snapshot() => new ResourceSnapshot(this);
    }

    public sealed class ResourceSnapshot
    {
        public string Id { get; }
        public string MaterialId { get; }
        public string BiomeId { get; }
        public GridPosition Position { get; }
        public int RemainingStock { get; }
        public ResourceGrowthStage GrowthStage { get; }
        public decimal RegrowthSeconds { get; }
        public decimal SecondsUntilReady { get; }
        public bool IsReady => RemainingStock > 0;
        internal ResourceSnapshot(ResourceNode node)
        {
            Id = node.Id; MaterialId = node.MaterialId; BiomeId = node.BiomeId; Position = node.Position;
            RemainingStock = node.RemainingStock; GrowthStage = node.GrowthStage;
            RegrowthSeconds = node.RegrowthSeconds; SecondsUntilReady = node.SecondsUntilReady;
        }
    }
}
