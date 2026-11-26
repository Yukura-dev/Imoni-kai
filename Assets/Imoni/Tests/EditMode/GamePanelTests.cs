using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using NUnit.Framework;

namespace Imoni.Core.Tests
{
    public sealed class GamePanelTests
    {
        private static GameConfig Config(Action<JObject> change = null)
        {
#if UNITY_EDITOR
            string path = Path.Combine(UnityEngine.Application.dataPath, "Imoni", "Config", "GameConfig.json");
#else
            string path = Path.Combine(TestContext.CurrentContext.TestDirectory, "GameConfig.json");
#endif
            JObject data = JObject.Parse(File.ReadAllText(path)); change?.Invoke(data);
            return GameConfig.FromJson(data.ToString());
        }
        private static LocalGameServer Server(GameConfig config, bool bots = false) =>
            new LocalGameServer(config, 202647, objectives: config.DefaultObjectives, enableBots: bots);
        private static InventoryPanelRow Row(GameConfig config, GameSnapshot state, string material = "wood", string quality = "normal") =>
            GamePanelProjection.Inventory(config, state).Single(row => row.MaterialId == material && row.QualityId == quality);
        private static GameSnapshot WithLots(GameConfig config, IEnumerable<MaterialLot> lots, IEnumerable<ProtectionSnapshot> protection = null) =>
            new GameSnapshot(RoundPhase.Collecting, 1, 202647, 0, false, config.StartFunds, 0, config.DefaultObjectives.Required, null,
                new[] { new PlayerSnapshot("player1", 0, config.Upgrades.ToDictionary(upgrade => upgrade.Id, _ => 0)) },
                lots, protection ?? Array.Empty<ProtectionSnapshot>(), Array.Empty<OperationRecord>(), Array.Empty<RoundEvent>(), null, Array.Empty<ResourceSnapshot>());

        [Test]
        public void WarehouseGroupsLotsByMaterialAndQualityWithoutApplyingPointMultipliers()
        {
            GameConfig config = Config();
            var lots = new[] { new MaterialLot(1, "wood", "normal", 10, config.FixedScale * 100),
                new MaterialLot(2, "wood", "normal", 5, config.FixedScale), new MaterialLot(3, "wood", "good", 3, config.FixedScale),
                new MaterialLot(4, "taro", "normal", 2, config.FixedScale), new MaterialLot(5, "negi", "normal", 4, config.FixedScale),
                new MaterialLot(6, "fish", "normal", 1, config.FixedScale) };
            GameSnapshot snapshot = WithLots(config, lots, new[] { new ProtectionSnapshot("wood", "normal", 4) });
            var rows = GamePanelProjection.Inventory(config, snapshot);
            Assert.That(rows.Select(row => row.Key), Is.EqualTo(new[] { "wood|normal", "wood|good", "taro|normal", "negi|normal", "fish|normal" }));
            InventoryPanelRow normal = rows[0];
            Assert.That(normal.Quantity, Is.EqualTo(new BigInteger(15)));
            Assert.That(normal.Protected, Is.EqualTo(new BigInteger(4)));
            Assert.That(normal.Sellable, Is.EqualTo(new BigInteger(11)));
            Assert.That(normal.UnitPrice, Is.EqualTo(config.Material("wood").BasePrice));
            Assert.That(rows[1].UnitPrice, Is.EqualTo(config.Material("wood").BasePrice * config.Quality("good").Multiplier));
            Assert.That(normal.UnitPriceText, Is.EqualTo("2"));
        }

        [TestCase(25, 3)]
        [TestCase(50, 6)]
        [TestCase(75, 9)]
        [TestCase(100, 13)]
        public void SalePercentagesUseOnlyUnprotectedQuantityAndFloor(int percent, int expected)
        {
            GameConfig config = Config(); var server = Server(config);
            server.Buy("player1", "wood", 20); server.SetProtection("player1", "wood", "normal", 7);
            Assert.That(GamePanelProjection.PercentageQuantity(config, Row(config, server.Snapshot()), percent), Is.EqualTo(new BigInteger(expected)));
        }

        [Test]
        public void HugeSaleQuantityAndPriceStayExactWithoutFloatConversion()
        {
            GameConfig config = Config(); BigInteger huge = BigInteger.Pow(10, 100) + 9;
            var state = WithLots(config, new[] { new MaterialLot(1, "wood", "normal", huge, config.FixedScale) },
                new[] { new ProtectionSnapshot("wood", "normal", 5) });
            BigInteger selected = GamePanelProjection.PercentageQuantity(config, Row(config, state), 75);
            SaleSelection sale = GamePanelProjection.SelectSale(config, state, "player1", "wood", "normal", selected.ToString());
            Assert.That(sale.CanSell, Is.True);
            Assert.That(sale.Quantity, Is.EqualTo((huge - 5) * 75 / 100));
            Assert.That(sale.Proceeds, Is.EqualTo(selected * config.Material("wood").BasePrice));
        }

        [Test]
        public void ProtectionChangesUseConfiguredStepAndClampToOwnedQuantity()
        {
            GameConfig config = Config(); var server = Server(config); server.Buy("player1", "wood", 8);
            InventoryPanelRow row = Row(config, server.Snapshot());
            Assert.That(GamePanelProjection.ProtectionQuantity(config, row, ProtectionChange.Decrease), Is.EqualTo(BigInteger.Zero));
            server.SetProtection("player1", "wood", "normal", GamePanelProjection.ProtectionQuantity(config, row, ProtectionChange.Increase));
            row = Row(config, server.Snapshot());
            Assert.That(row.Protected, Is.EqualTo(new BigInteger(5)));
            Assert.That(GamePanelProjection.ProtectionQuantity(config, row, ProtectionChange.Increase), Is.EqualTo(new BigInteger(8)));
            server.SetProtection("player1", "wood", "normal", GamePanelProjection.ProtectionQuantity(config, row, ProtectionChange.All));
            row = Row(config, server.Snapshot());
            Assert.That(row.AllProtected, Is.True); Assert.That(row.ToggleProtectionLabel, Is.EqualTo("保護解除"));
            Assert.That(GamePanelProjection.ProtectionQuantity(config, row, ProtectionChange.None), Is.EqualTo(BigInteger.Zero));
            GameConfig changed = Config(data => data["ui"]["panels"]["protectionStep"] = 7);
            Assert.That(GamePanelProjection.ProtectionQuantity(changed, row, ProtectionChange.Decrease), Is.EqualTo(BigInteger.One));
        }

        [TestCase("")]
        [TestCase("-1")]
        [TestCase("+1")]
        [TestCase("1.5")]
        [TestCase("1e3")]
        [TestCase("abc")]
        public void InvalidQuantityTextDisablesSaleWithoutChangingInventory(string input)
        {
            GameConfig config = Config(); var server = Server(config); server.Buy("player1", "wood", 10);
            SaleSelection sale = GamePanelProjection.SelectSale(config, server.Snapshot(), "player1", "wood", "normal", input);
            Assert.That(sale.CanSell, Is.False); Assert.That(sale.Error, Is.Not.Empty);
            Assert.That(Row(config, server.Snapshot()).Quantity, Is.EqualTo(new BigInteger(10)));
        }

        [Test]
        public void SaleAtProtectionBoundarySucceedsAndLargerSaleCannotChangeFundsOrInventory()
        {
            GameConfig config = Config(); var server = Server(config);
            server.Buy("player1", "wood", 20); server.SetProtection("player1", "wood", "normal", 15);
            GameSnapshot before = server.Snapshot();
            Assert.That(GamePanelProjection.SelectSale(config, before, "player1", "wood", "normal", "6").CanSell, Is.False);
            Assert.That(GamePanelProjection.SelectSale(config, before, "player1", "wood", "normal", "5").CanSell, Is.True);
            Assert.Throws<InvalidOperationException>(() => server.Sell("player1", "wood", "normal", 6));
            Assert.That(server.Snapshot().Funds, Is.EqualTo(before.Funds));
            Assert.That(server.Snapshot().History.Count, Is.EqualTo(before.History.Count));
            server.Sell("player1", "wood", "normal", 5);
            InventoryPanelRow after = Row(config, server.Snapshot());
            Assert.That(after.Quantity, Is.EqualTo(new BigInteger(15))); Assert.That(after.Sellable, Is.EqualTo(BigInteger.Zero));
            Assert.That(after.Protected, Is.EqualTo(before.Protection.Single().Quantity));
        }

        [Test]
        public void StaleEnabledSaleIsRejectedIfAnotherMemberProtectsStock()
        {
            GameConfig config = Config(); var server = Server(config); server.Buy("player1", "wood", 10);
            SaleSelection stale = GamePanelProjection.SelectSale(config, server.Snapshot(), "player1", "wood", "normal", "10");
            Assert.That(stale.CanSell, Is.True);
            server.SetProtection("player2", "wood", "normal", 5);
            Assert.Throws<InvalidOperationException>(() => server.Sell("player1", "wood", "normal", stale.Quantity));
            Assert.That(Row(config, server.Snapshot()).Quantity, Is.EqualTo(new BigInteger(10)));
        }

        [Test]
        public void SellAllIsOneHistoryOperationAndLeavesEveryProtectedQuantity()
        {
            GameConfig config = Config(); var server = Server(config);
            server.Buy("player1", "wood", 20); server.Buy("player1", "taro", 10);
            server.SetProtection("player1", "wood", "normal", 15); server.SetProtection("player2", "taro", "normal", 4);
            GameSnapshot before = server.Snapshot();
            BigInteger expected = 5 * config.Material("wood").BasePrice + 6 * config.Material("taro").BasePrice;
            Assert.That(server.SellAllUnprotected("player1"), Is.EqualTo(expected));
            GameSnapshot after = server.Snapshot();
            Assert.That(after.Funds, Is.EqualTo(before.Funds + expected));
            Assert.That(after.History.Count, Is.EqualTo(before.History.Count + 1));
            Assert.That(after.History.Last().Kind, Is.EqualTo(OperationKind.SellAll));
            Assert.That(after.History.Last().Quantity, Is.EqualTo(new BigInteger(11)));
            Assert.That(GamePanelProjection.Inventory(config, after).All(row => row.Quantity == row.Protected), Is.True);
            Assert.That(server.SellAllUnprotected("player2"), Is.EqualTo(BigInteger.Zero));
            Assert.That(server.Snapshot().History.Count, Is.EqualTo(after.History.Count));
        }

        [Test]
        public void ConcurrentSellAllCommandsCannotDuplicateRevenueOrConsumeProtection()
        {
            GameConfig config = Config(); var server = Server(config); server.Buy("player1", "wood", 50);
            server.SetProtection("player1", "wood", "normal", 20);
            BigInteger before = server.Snapshot().Funds;
            var results = new BigInteger[2];
            Parallel.Invoke(() => results[0] = server.SellAllUnprotected("player1"), () => results[1] = server.SellAllUnprotected("player2"));
            Assert.That(results[0] + results[1], Is.EqualTo(30 * config.Material("wood").BasePrice));
            Assert.That(server.Snapshot().Funds - before, Is.EqualTo(results[0] + results[1]));
            Assert.That(Row(config, server.Snapshot()).Quantity, Is.EqualTo(new BigInteger(20)));
        }

        [Test]
        public void ShopOffersUseConfiguredMaterialsQuantitiesAndExactFundsBoundary()
        {
            GameConfig config = Config(data => data["economy"]["startFunds"] = 60); var server = Server(config);
            var rows = GamePanelProjection.Shop(config, server.Snapshot(), "player1");
            Assert.That(rows.Select(row => row.MaterialId), Is.EqualTo(config.Panels.ShopMaterials));
            Assert.That(rows[0].Offers.Select(offer => offer.Quantity), Is.EqualTo(new BigInteger[] { 10, 50 }));
            ShopOffer affordable = rows[0].Offers[0];
            Assert.That(affordable.Cost, Is.EqualTo(new BigInteger(60))); Assert.That(affordable.CanBuy, Is.True);
            Assert.That(rows[0].Offers[1].CanBuy, Is.False);
            Assert.That(server.Buy("player1", "wood", affordable.Quantity), Is.EqualTo(affordable.Cost));
            Assert.That(server.Snapshot().Funds, Is.EqualTo(BigInteger.Zero));
            Assert.That(server.Snapshot().Lots.All(lot => lot.QualityId == config.PurchasedQualityId && lot.PointMultiplierRaw == config.FixedScale), Is.True);
            Assert.That(GamePanelProjection.Shop(config, server.Snapshot(), "player1").SelectMany(row => row.Offers).All(offer => !offer.CanBuy), Is.True);
            GameSnapshot before = server.Snapshot();
            Assert.Throws<InvalidOperationException>(() => server.Buy("player1", "wood", 10));
            Assert.That(server.Snapshot().Lots.Sum(lot => (int)lot.Quantity), Is.EqualTo(10));
            Assert.That(server.Snapshot().History.Count, Is.EqualTo(before.History.Count));
        }

        [Test]
        public void UpgradeEffectsAndPricesUseConfigAndOnlyUpgradeTheActingPlayer()
        {
            GameConfig config = Config(); var server = Server(config);
            var before = GamePanelProjection.Upgrades(config, server.Snapshot(), "player1");
            Assert.That(before.Select(row => row.CurrentEffect), Is.EqualTo(new[] { "移動 ×1.00", "採集 1.60秒", "獲得量 ×1.0", "最高品質 8.0%" }));
            Assert.That(before.Select(row => row.NextEffect), Is.EqualTo(new[] { "移動 ×1.25", "採集 1.28秒", "獲得量 ×1.5", "最高品質 12.8%" }));
            Assert.That(server.Upgrade("player1", "move"), Is.EqualTo(before[0].Price));
            Assert.That(GamePanelProjection.Upgrades(config, server.Snapshot(), "player1")[0].CurrentEffect, Is.EqualTo(before[0].NextEffect));
            Assert.That(server.Snapshot().Players.Single(player => player.Id == "player2").Upgrades["move"], Is.Zero);
            GameConfig changed = Config(data => { data["upgrades"]["items"][0]["effectPerLevel"] = .5m; data["upgrades"]["items"][0]["basePrice"] = 321; });
            var changedRow = GamePanelProjection.Upgrades(changed, Server(changed).Snapshot(), "player1")[0];
            Assert.That(changedRow.NextEffect, Is.EqualTo("移動 ×1.50")); Assert.That(changedRow.Price, Is.EqualTo(new BigInteger(321)));
        }

        [Test]
        public void InsufficientUpgradeIsDisabledAndServerRejectsWithoutMutation()
        {
            GameConfig config = Config(data => data["economy"]["startFunds"] = 149); var server = Server(config);
            Assert.That(GamePanelProjection.Upgrades(config, server.Snapshot(), "player1").All(row => !row.CanBuy), Is.True);
            Assert.Throws<InvalidOperationException>(() => server.Upgrade("player1", "luck"));
            Assert.That(server.Snapshot().Funds, Is.EqualTo(new BigInteger(149)));
            Assert.That(server.Snapshot().Players[0].Upgrades["luck"], Is.Zero); Assert.That(server.Snapshot().History, Is.Empty);
        }

        [Test]
        public void HistoryContainsLatestThreeOperationsWithAuthoritativeTimeAndFrozenUpgradeLevel()
        {
            GameConfig config = Config(); var server = Server(config);
            server.Tick(62.8m); server.Buy("player1", "wood", 10); server.SetProtection("player2", "wood", "normal", 5);
            server.Upgrade("player1", "move"); var firstUpgrade = server.Snapshot().History.Last(); server.Tick(1m);
            server.Upgrade("player1", "move"); server.Sell("player1", "wood", "normal", 5);
            var text = GamePanelProjection.History(config, server.Snapshot(), id => id == "player1" ? "あなた" : "たろう");
            Assert.That(text.Count, Is.EqualTo(3));
            Assert.That(text[0], Is.EqualTo("[1:03] あなたが薪×5(普通)を売却 +10G"));
            Assert.That(text[1], Does.Contain("Lv.2")); Assert.That(text[2], Does.Contain("Lv.1"));
            Assert.That(firstUpgrade.UpgradeLevel, Is.EqualTo(1));
            GameConfig shortHistory = Config(data => data["ui"]["panels"]["historyLimit"] = 1);
            Assert.That(GamePanelProjection.History(shortHistory, server.Snapshot(), id => id).Count, Is.EqualTo(1));
        }

        [Test]
        public void MemberProjectionFollowsAuthoritativeGatherAndDisconnectState()
        {
            GameConfig config = Config(); var server = Server(config);
            Assert.That(GamePanelProjection.Members(config, server.Snapshot())[0].DisplayText, Is.EqualTo("待機中"));
            ResourceNode node = server.World.Resources.Where(resource => server.CanSelectResource("player2", resource.Id))
                .OrderBy(resource => Pathfinding.FindPathToResource(server.World, server.World.Start, resource, 1).Steps.Count).First();
            server.TrySelectResource("player2", node.Id);
            while (server.Snapshot().Players[1].Action == PlayerAction.Moving) server.Tick(.01m);
            MemberPanelView gathering = GamePanelProjection.Members(config, server.Snapshot())[1];
            Assert.That(gathering.StateText, Is.EqualTo("採集中")); Assert.That(gathering.DisplayText, Does.EndWith(config.Material(node.MaterialId).Name));
            server.SetConnected("player2", false);
            MemberPanelView disconnected = GamePanelProjection.Members(config, server.Snapshot())[1];
            Assert.That(disconnected.Connected, Is.False); Assert.That(disconnected.DisplayText, Is.EqualTo("切断中・再接続待ち"));
        }

        [TestCase(OperationKind.Buy)]
        [TestCase(OperationKind.Sell)]
        [TestCase(OperationKind.SetProtection)]
        [TestCase(OperationKind.Upgrade)]
        [TestCase(OperationKind.SellAll)]
        public void DisconnectedPlayerCannotMutateSharedFundsOrStock(OperationKind kind)
        {
            GameConfig config = Config(); var server = Server(config); server.Buy("player1", "wood", 10);
            server.SetConnected("player1", false); GameSnapshot before = server.Snapshot();
            Assert.That(GamePanelProjection.CanOperate(before, "player1"), Is.False);
            Assert.Throws<InvalidOperationException>(() =>
            {
                switch (kind)
                {
                    case OperationKind.Buy: server.Buy("player1", "wood", 1); break;
                    case OperationKind.Sell: server.Sell("player1", "wood", "normal", 1); break;
                    case OperationKind.SetProtection: server.SetProtection("player1", "wood", "normal", 1); break;
                    case OperationKind.Upgrade: server.Upgrade("player1", "move"); break;
                    case OperationKind.SellAll: server.SellAllUnprotected("player1"); break;
                }
            });
            Assert.That(server.Snapshot().Funds, Is.EqualTo(before.Funds)); Assert.That(server.Snapshot().History.Count, Is.EqualTo(before.History.Count));
        }

        [Test]
        public void AbortingChallengeStopsBotsTimeAndEveryPanelCommand()
        {
            GameConfig config = Config(); var server = Server(config, bots: true); server.Tick(3);
            server.AbortChallenge("player1"); GameSnapshot before = server.Snapshot();
            Assert.That(before.Phase, Is.EqualTo(RoundPhase.Aborted)); Assert.That(before.Players.All(player => player.Action == PlayerAction.Idle), Is.True);
            server.Tick(100); GameSnapshot after = server.Snapshot();
            Assert.That(after.ElapsedSeconds, Is.EqualTo(before.ElapsedSeconds)); Assert.That(after.History.Count, Is.EqualTo(before.History.Count));
            Assert.That(GamePanelProjection.Shop(config, after, "player1").SelectMany(row => row.Offers).Any(offer => offer.CanBuy), Is.False);
            Assert.Throws<InvalidOperationException>(() => server.SellAllUnprotected("player1"));
            Assert.Throws<InvalidOperationException>(() => server.Buy("player1", "wood", 1));
            Assert.Throws<InvalidOperationException>(() => server.Upgrade("player1", "move"));
            Assert.That(server.TryMoveTo("player1", server.World.Start), Is.False);
        }
    }
}
