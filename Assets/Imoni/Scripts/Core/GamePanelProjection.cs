using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Numerics;

namespace Imoni.Core
{
    public enum ProtectionChange { Decrease, Increase, All, None }

    public sealed class InventoryPanelRow
    {
        public string Key { get; internal set; }
        public string MaterialId { get; internal set; }
        public string QualityId { get; internal set; }
        public string Name { get; internal set; }
        public string QualityName { get; internal set; }
        public BigInteger Quantity { get; internal set; }
        public BigInteger Protected { get; internal set; }
        public BigInteger Sellable { get; internal set; }
        public BigInteger UnitPrice { get; internal set; }
        public string QuantityText { get; internal set; }
        public string ProtectedText { get; internal set; }
        public string SellableText { get; internal set; }
        public string UnitPriceText { get; internal set; }
        public bool AllProtected => Quantity > 0 && Protected == Quantity;
        public string ToggleProtectionLabel => AllProtected ? "保護解除" : "全部保護";
    }

    public sealed class SaleSelection
    {
        public BigInteger Quantity { get; internal set; }
        public BigInteger Proceeds { get; internal set; }
        public bool CanSell { get; internal set; }
        public string Error { get; internal set; }
        public string Label { get; internal set; }
    }

    public sealed class ShopOffer
    {
        public BigInteger Quantity { get; internal set; }
        public BigInteger Cost { get; internal set; }
        public bool CanBuy { get; internal set; }
        public string QuantityText { get; internal set; }
        public string CostText { get; internal set; }
        public string Label => QuantityText + "  " + CostText;
    }

    public sealed class ShopPanelRow
    {
        public string MaterialId { get; internal set; }
        public string Name { get; internal set; }
        public BigInteger UnitPrice { get; internal set; }
        public string UnitPriceText { get; internal set; }
        public IReadOnlyList<ShopOffer> Offers { get; internal set; }
    }

    public sealed class UpgradePanelRow
    {
        public string Id { get; internal set; }
        public string Name { get; internal set; }
        public int Level { get; internal set; }
        public string CurrentEffect { get; internal set; }
        public string NextEffect { get; internal set; }
        public string EffectText => CurrentEffect + " → " + NextEffect;
        public BigInteger Price { get; internal set; }
        public string PriceText { get; internal set; }
        public bool CanBuy { get; internal set; }
    }

    public sealed class MemberPanelView
    {
        public string ActorId { get; internal set; }
        public bool Connected { get; internal set; }
        public string StateText { get; internal set; }
        public string DetailText { get; internal set; }
        public string DisplayText { get; internal set; }
    }

    /// <summary>Inventory grouping, eligibility and exact input arithmetic belong to Core, never to UI controls.</summary>
    public static class GamePanelProjection
    {
        public static IReadOnlyList<InventoryPanelRow> Inventory(GameConfig config, GameSnapshot snapshot)
        {
            Require(config, snapshot);
            var rows = new List<InventoryPanelRow>();
            foreach (MaterialDefinition material in config.Panels.ShopMaterials.Concat(config.Materials.Select(item => item.Id))
                .Distinct(StringComparer.Ordinal).Select(config.Material))
                foreach (QualityDefinition quality in config.Qualities)
                {
                    BigInteger quantity = snapshot.Lots.Where(lot => lot.MaterialId == material.Id && lot.QualityId == quality.Id)
                        .Aggregate(BigInteger.Zero, (total, lot) => total + lot.Quantity);
                    if (quantity <= 0) continue;
                    BigInteger protection = snapshot.Protection.FirstOrDefault(item => item.MaterialId == material.Id && item.QualityId == quality.Id)?.Quantity ?? BigInteger.Zero;
                    BigInteger unit = material.BasePrice * quality.Multiplier;
                    rows.Add(new InventoryPanelRow { Key = material.Id + "|" + quality.Id, MaterialId = material.Id, QualityId = quality.Id,
                        Name = material.Name, QualityName = quality.Name, Quantity = quantity, Protected = protection, Sellable = quantity - protection,
                        UnitPrice = unit, QuantityText = Format.Big(quantity, config), ProtectedText = Format.Big(protection, config),
                        SellableText = Format.Big(quantity - protection, config), UnitPriceText = Format.Big(unit, config) });
                }
            return rows.AsReadOnly();
        }

        public static bool CanOperate(GameSnapshot snapshot, string actorId) => snapshot != null && snapshot.Phase == RoundPhase.Collecting
            && snapshot.Players.Any(player => player.Id == actorId && player.Connected);

        public static BigInteger PercentageQuantity(GameConfig config, InventoryPanelRow row, int percent)
        {
            if (row == null) return BigInteger.Zero;
            if (!config.Panels.SalePercentages.Contains(percent)) throw new ArgumentOutOfRangeException(nameof(percent));
            return row.Sellable * percent / 100;
        }

        public static BigInteger ProtectionQuantity(GameConfig config, InventoryPanelRow row, ProtectionChange change)
        {
            if (row == null) return BigInteger.Zero;
            switch (change)
            {
                case ProtectionChange.Decrease: return BigInteger.Max(BigInteger.Zero, row.Protected - config.Panels.ProtectionStep);
                case ProtectionChange.Increase: return BigInteger.Min(row.Quantity, row.Protected + config.Panels.ProtectionStep);
                case ProtectionChange.All: return row.Quantity;
                case ProtectionChange.None: return BigInteger.Zero;
                default: throw new ArgumentOutOfRangeException(nameof(change));
            }
        }

        public static SaleSelection SelectSale(GameConfig config, GameSnapshot snapshot, string actorId, string materialId, string qualityId, string input)
        {
            InventoryPanelRow row = Inventory(config, snapshot).FirstOrDefault(item => item.MaterialId == materialId && item.QualityId == qualityId);
            bool validInput = BigInteger.TryParse(input?.Trim(), NumberStyles.None, CultureInfo.InvariantCulture, out BigInteger quantity);
            if (!validInput) quantity = BigInteger.Zero;
            BigInteger proceeds = quantity * (row?.UnitPrice ?? BigInteger.Zero);
            string error = row == null ? "素材を選択してください" : !validInput ? "0以上の整数を入力してください"
                : quantity > row.Sellable ? "売却可能数を超えています" : string.Empty;
            return new SaleSelection { Quantity = quantity, Proceeds = proceeds, CanSell = error.Length == 0 && quantity > 0 && CanOperate(snapshot, actorId),
                Error = error, Label = Format.Big(quantity, config) + "個 売却 +" + Format.Big(proceeds, config) + config.CurrencyUnit };
        }

        public static BigInteger PurchaseCost(GameConfig config, string materialId, BigInteger quantity)
        {
            if (quantity < 0) throw new ArgumentOutOfRangeException(nameof(quantity));
            return GameMath.MultiplyFloor(quantity * config.Material(materialId).BasePrice * config.Quality(config.PurchasedQualityId).Multiplier,
                config.BuyPriceMultiplier);
        }

        public static IReadOnlyList<ShopPanelRow> Shop(GameConfig config, GameSnapshot snapshot, string actorId)
        {
            Require(config, snapshot);
            return Array.AsReadOnly(config.Panels.ShopMaterials.Select(id =>
            {
                BigInteger unit = PurchaseCost(config, id, BigInteger.One);
                return new ShopPanelRow { MaterialId = id, Name = config.Material(id).Name, UnitPrice = unit,
                    UnitPriceText = Format.Big(unit, config) + config.CurrencyUnit + " / 個",
                    Offers = Array.AsReadOnly(config.Panels.ShopQuantities.Select(quantity =>
                    {
                        BigInteger cost = PurchaseCost(config, id, quantity);
                        return new ShopOffer { Quantity = quantity, Cost = cost, CanBuy = CanOperate(snapshot, actorId) && snapshot.Funds >= cost,
                            QuantityText = "×" + Format.Big(quantity, config), CostText = Format.Big(cost, config) + config.CurrencyUnit };
                    }).ToArray()) };
            }).ToArray());
        }

        public static IReadOnlyList<UpgradePanelRow> Upgrades(GameConfig config, GameSnapshot snapshot, string actorId)
        {
            Require(config, snapshot);
            PlayerSnapshot actor = snapshot.Players.FirstOrDefault(player => player.Id == actorId) ?? throw new ArgumentException("Unknown actor.", nameof(actorId));
            var rules = new UpgradeRules(config);
            return Array.AsReadOnly(config.Upgrades.Select(upgrade =>
            {
                int level = actor.Upgrades[upgrade.Id];
                BigInteger price = rules.Price(upgrade.Id, level);
                return new UpgradePanelRow { Id = upgrade.Id, Name = upgrade.Name, Level = level,
                    CurrentEffect = UpgradeEffect(config, upgrade.Id, level), NextEffect = UpgradeEffect(config, upgrade.Id, checked(level + 1)),
                    Price = price, PriceText = Format.Big(price, config) + config.CurrencyUnit,
                    CanBuy = CanOperate(snapshot, actorId) && snapshot.Funds >= price };
            }).ToArray());
        }

        public static string UpgradeEffect(GameConfig config, string id, int level)
        {
            var rules = new UpgradeRules(config);
            int places = config.Panels.EffectDecimalPlaces[id];
            switch (id)
            {
                case "move": return "移動 ×" + Fixed(rules.MultiplierRaw(id, level), config.FixedScale, places);
                case "speed": return "採集 " + Fixed(rules.CollectDurationRaw(level), config.FixedScale, places) + "秒";
                case "amount": return "獲得量 ×" + Fixed(rules.MultiplierRaw(id, level), config.FixedScale, places);
                case "luck": return "最高品質 " + Fixed(new QualityRoller(config).TopChanceRaw(level) * 100, config.FixedScale, places) + "%";
                default: throw new ArgumentException("Unknown upgrade.", nameof(id));
            }
        }

        public static IReadOnlyList<MemberPanelView> Members(GameConfig config, GameSnapshot snapshot)
        {
            Require(config, snapshot);
            return Array.AsReadOnly(snapshot.Players.Select(player =>
            {
                ResourceSnapshot target = snapshot.Resources.FirstOrDefault(resource => resource.Id == player.TargetResourceId);
                string state = !player.Connected ? "切断中" : player.Action == PlayerAction.Gathering ? "採集中" : player.Action == PlayerAction.Moving ? "移動中" : "待機中";
                return new MemberPanelView { ActorId = player.Id, Connected = player.Connected, StateText = state,
                    DetailText = !player.Connected ? "再接続待ち" : target == null ? state : config.Material(target.MaterialId).Name + " " + state,
                    DisplayText = !player.Connected ? "切断中・再接続待ち" : target == null ? state : state + "・" + config.Material(target.MaterialId).Name };
            }).ToArray());
        }

        public static IReadOnlyList<string> History(GameConfig config, GameSnapshot snapshot, Func<string, string> actorName)
        {
            Require(config, snapshot);
            if (actorName == null) throw new ArgumentNullException(nameof(actorName));
            return Array.AsReadOnly(snapshot.History.OrderByDescending(record => record.Sequence).Take(config.Panels.HistoryLimit)
                .Select(record => HistoryText(config, record, actorName(record.ActorId))).ToArray());
        }

        public static string HistoryText(GameConfig config, OperationRecord record, string actorName)
        {
            string prefix = "[" + GameHudProjection.Clock(record.ElapsedSeconds) + "] " + actorName + "が";
            string amount = Format.Big(record.Quantity, config);
            string money = Format.Big(BigInteger.Abs(record.FundsDelta), config) + config.CurrencyUnit;
            if (record.Kind == OperationKind.Gather) return GameHudProjection.GatherHistory(config, record, actorName);
            if (record.Kind == OperationKind.SellAll) return prefix + "素材" + amount + "個をまとめて売却 +" + money;
            if (record.Kind == OperationKind.Upgrade) return prefix + config.Upgrade(record.UpgradeId).Name + "を強化(Lv." + record.UpgradeLevel + ") -" + money;
            string material = config.Material(record.MaterialId).Name;
            string quality = "(" + config.Quality(record.QualityId).Name + ")";
            if (record.Kind == OperationKind.SetProtection) return prefix + material + quality + "の保護を" + amount + "個に設定";
            return prefix + material + "×" + amount + quality + (record.Kind == OperationKind.Buy ? "を購入 -" : "を売却 +") + money;
        }

        private static string Fixed(BigInteger numerator, BigInteger denominator, int places)
        {
            BigInteger scale = BigInteger.Pow(10, places);
            BigInteger value = GameMath.DivideRoundHalfUp(numerator * scale, denominator);
            return (value / scale).ToString(CultureInfo.InvariantCulture) + (places == 0 ? string.Empty
                : "." + (value % scale).ToString(CultureInfo.InvariantCulture).PadLeft(places, '0'));
        }
        private static void Require(GameConfig config, GameSnapshot snapshot)
        {
            if (config == null) throw new ArgumentNullException(nameof(config));
            if (snapshot == null) throw new ArgumentNullException(nameof(snapshot));
        }
    }
}
