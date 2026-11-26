using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;

namespace Imoni.Core
{
    /// <summary>A frozen acquisition lot. Its point multiplier never follows later player upgrades.</summary>
    public sealed class MaterialLot
    {
        public long Sequence { get; }
        public string MaterialId { get; }
        public string QualityId { get; }
        public BigInteger Quantity { get; }
        public BigInteger PointMultiplierRaw { get; }

        internal MaterialLot(long sequence, string materialId, string qualityId,
            BigInteger quantity, BigInteger pointMultiplierRaw)
        {
            Sequence = sequence;
            MaterialId = materialId;
            QualityId = qualityId;
            Quantity = quantity;
            PointMultiplierRaw = pointMultiplierRaw;
        }

        internal MaterialLot WithQuantity(BigInteger quantity) =>
            new MaterialLot(Sequence, MaterialId, QualityId, quantity, PointMultiplierRaw);
    }

    /// <summary>Lot accounting. The authoritative server serializes mutations to this domain object.</summary>
    public sealed class Inventory
    {
        private readonly GameConfig config;
        private readonly List<MaterialLot> lots = new List<MaterialLot>();
        private readonly Dictionary<(string Material, string Quality), BigInteger> protection =
            new Dictionary<(string, string), BigInteger>();
        private long nextSequence;

        public Inventory(GameConfig config)
        {
            this.config = config ?? throw new ArgumentNullException(nameof(config));
        }

        // Copy the collection as well as keeping each lot immutable: callers can retain old snapshots.
        public IReadOnlyList<MaterialLot> Lots => Array.AsReadOnly(lots.ToArray());

        public MaterialLot Add(string materialId, string qualityId, BigInteger quantity,
            BigInteger pointMultiplierRaw)
        {
            ValidatePair(materialId, qualityId);
            RequirePositive(quantity, nameof(quantity));
            RequirePositive(pointMultiplierRaw, nameof(pointMultiplierRaw));
            long sequence = checked(nextSequence + 1);
            var lot = new MaterialLot(sequence, materialId, qualityId, quantity, pointMultiplierRaw);
            lots.Add(lot);
            nextSequence = sequence;
            return lot;
        }

        public BigInteger Quantity(string materialId, string qualityId)
        {
            ValidatePair(materialId, qualityId);
            return lots.Where(lot => lot.MaterialId == materialId && lot.QualityId == qualityId)
                .Aggregate(BigInteger.Zero, (sum, lot) => sum + lot.Quantity);
        }

        public BigInteger ProtectedQuantity(string materialId, string qualityId)
        {
            ValidatePair(materialId, qualityId);
            return protection.TryGetValue((materialId, qualityId), out BigInteger amount)
                ? amount : BigInteger.Zero;
        }

        public BigInteger SellableQuantity(string materialId, string qualityId) =>
            Quantity(materialId, qualityId) - ProtectedQuantity(materialId, qualityId);

        public void SetProtectedQuantity(string materialId, string qualityId, BigInteger quantity)
        {
            BigInteger total = Quantity(materialId, qualityId);
            if (quantity < 0 || quantity > total)
                throw new ArgumentOutOfRangeException(nameof(quantity), "Protection must be within the owned quantity.");
            if (quantity.IsZero) protection.Remove((materialId, qualityId));
            else protection[(materialId, qualityId)] = quantity;
        }

        /// <summary>Rejects an invalid sale before changing any lot; consumes matching lots oldest first.</summary>
        public BigInteger Sell(string materialId, string qualityId, BigInteger quantity)
        {
            RequirePositive(quantity, nameof(quantity));
            BigInteger available = SellableQuantity(materialId, qualityId);
            if (quantity > available)
                throw new InvalidOperationException("Sale exceeds the unprotected quantity.");

            BigInteger proceeds = quantity * config.Material(materialId).BasePrice * config.Quality(qualityId).Multiplier;
            BigInteger remaining = quantity;
            for (int i = 0; i < lots.Count && remaining > 0;)
            {
                MaterialLot lot = lots[i];
                if (lot.MaterialId != materialId || lot.QualityId != qualityId) { i++; continue; }
                BigInteger taken = BigInteger.Min(remaining, lot.Quantity);
                remaining -= taken;
                if (taken == lot.Quantity) lots.RemoveAt(i);
                else { lots[i] = lot.WithQuantity(lot.Quantity - taken); i++; }
            }
            return proceeds;
        }

        public BigInteger QualityEquivalent(string materialId)
        {
            config.Material(materialId);
            return lots.Where(lot => lot.MaterialId == materialId)
                .Aggregate(BigInteger.Zero, (sum, lot) => sum + lot.Quantity * config.Quality(lot.QualityId).Multiplier);
        }

        /// <summary>Points in config.FixedScale units. Keep this precision until the final score division.</summary>
        public BigInteger PointsRaw(string materialId)
        {
            BigInteger basePoint = config.Material(materialId).BasePoint;
            return lots.Where(lot => lot.MaterialId == materialId).Aggregate(BigInteger.Zero,
                (sum, lot) => sum + lot.Quantity * basePoint * config.Quality(lot.QualityId).Multiplier * lot.PointMultiplierRaw);
        }

        /// <summary>The pot consumes all lots, including protected quantities.</summary>
        public void Clear()
        {
            lots.Clear();
            protection.Clear();
            nextSequence = 0;
        }

        private void ValidatePair(string materialId, string qualityId)
        {
            config.Material(materialId);
            config.Quality(qualityId);
        }

        private static void RequirePositive(BigInteger value, string parameter)
        {
            if (value <= 0) throw new ArgumentOutOfRangeException(parameter, "Must be positive.");
        }
    }
}
