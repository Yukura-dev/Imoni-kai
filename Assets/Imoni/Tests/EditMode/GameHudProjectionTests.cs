using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Numerics;
using Imoni.Core;
using Newtonsoft.Json.Linq;
using NUnit.Framework;

namespace Imoni.Core.Tests
{
    public sealed class GameHudProjectionTests
    {
        private static GameConfig Config(Action<JObject> change = null)
        {
#if UNITY_EDITOR
            string path = Path.Combine(UnityEngine.Application.dataPath, "Imoni", "Config", "GameConfig.json");
#else
            string path = Path.Combine(TestContext.CurrentContext.TestDirectory, "GameConfig.json");
#endif
            JObject json = JObject.Parse(File.ReadAllText(path)); change?.Invoke(json);
            return GameConfig.FromJson(json.ToString());
        }
        private static GameSnapshot Snapshot(GameConfig config, decimal elapsed = 0, bool bonus = false,
            IEnumerable<MaterialLot> lots = null, int streak = 0) =>
            new GameSnapshot(RoundPhase.Collecting, 1, 1, elapsed, bonus, config.StartFunds, 0,
                config.DefaultObjectives.Required, bonus ? config.DefaultObjectives.Bonus : null,
                new[] { new PlayerSnapshot("player1", streak, new Dictionary<string, int>()), new PlayerSnapshot("player2", 0, new Dictionary<string, int>()) },
                lots ?? Array.Empty<MaterialLot>(), Array.Empty<ProtectionSnapshot>(), Array.Empty<OperationRecord>(),
                Array.Empty<RoundEvent>(), null, Array.Empty<ResourceSnapshot>());

        [Test]
        public void EndlessHudDoesNotDisplayTheNormalFiveRoundDenominator()
        {
            GameConfig config = Config();
            var server = new LocalGameServer(config, 202647, enableBots: false, endless: true);
            GameSnapshot snapshot = server.Snapshot();
            Assert.That(GameHudProjection.Present(config, snapshot, "player1", 30).RoundText,
                Is.EqualTo("エンドレス R" + snapshot.RoundNumber));
        }

        [Test]
        public void QualityEquivalentCountsAllLotsButNotPointMultipliersOrOtherMaterials()
        {
            GameConfig config = Config();
            var lots = new[] { new MaterialLot(1, "wood", "normal", 3, config.FixedScale * 100),
                new MaterialLot(2, "wood", "good", 4, config.FixedScale), new MaterialLot(3, "taro", "rare", 10, config.FixedScale) };
            GameHudView view = GameHudProjection.Present(config, Snapshot(config, lots: lots), "player1", 30);
            Assert.That(view.Required.Single(goal => goal.MaterialId == "wood").CurrentText,
                Is.EqualTo(Format.Big(3 + 4 * config.Quality("good").Multiplier, config)));
            Assert.That(view.InventoryQuantity, Is.EqualTo(new BigInteger(17)));
        }

        [Test]
        public void BonusObjectiveRemainsHiddenUntilServerRevealsIt()
        {
            GameConfig config = Config();
            var server = new LocalGameServer(config, 202647, objectives: config.DefaultObjectives, enableBots: false);
            server.Tick(config.BonusRevealSeconds - .01m);
            GameHudView before = GameHudProjection.Present(config, server.Snapshot(), "player1", 30);
            Assert.That(before.BonusRevealed, Is.False);
            Assert.That(before.BonusText, Is.EqualTo("あと 0:01 で発表!"));
            Assert.That(before.BonusStatusText, Is.Empty);
            server.Tick(.01m);
            GameHudView after = GameHudProjection.Present(config, server.Snapshot(), "player1", 30);
            Assert.That(after.BonusRevealed, Is.True);
            Assert.That(after.BonusText, Does.StartWith(config.Material(config.DefaultObjectives.Bonus.MaterialId).Name));
        }

        [TestCase(44, "146", "1")]
        [TestCase(45, "150", "2")]
        [TestCase(60, "200", "4")]
        public void BonusRewardUsesConfiguredExactThreshold(int quantity, string percent, string multiplier)
        {
            GameConfig config = Config();
            var lot = new MaterialLot(1, "mushroom", "normal", quantity, config.FixedScale);
            GameHudView view = GameHudProjection.Present(config, Snapshot(config, 120, true, new[] { lot }), "player1", 30);
            Assert.That(view.BonusStatusText, Is.EqualTo("達成率 " + percent + "% 報酬×" + multiplier));
        }

        [TestCase(299, "達成率 996% 報酬×8")]
        [TestCase(300, "目標の10倍 報酬×8")]
        [TestCase(330, "目標の11倍 報酬×8")]
        public void HudSwitchesLargeAchievementToNaturalIntegerMultiples(int quantity, string expected)
        {
            GameConfig config = Config();
            var lot = new MaterialLot(1, "mushroom", "normal", quantity, config.FixedScale);
            var view = GameHudProjection.Present(config, Snapshot(config, 120, true, new[] { lot }), "player1", 30);
            Assert.That(view.BonusStatusText, Is.EqualTo(expected));
        }

        [Test]
        public void HugeQuantitiesDoNotPassThroughFloatingPoint()
        {
            GameConfig config = Config(); BigInteger huge = BigInteger.Pow(10, 1000);
            var lots = new[] { new MaterialLot(1, "wood", "rare", huge, config.FixedScale),
                new MaterialLot(2, "mushroom", "normal", huge, config.FixedScale) };
            GameHudView view = GameHudProjection.Present(config, Snapshot(config, 120, true, lots), "player1", 30);
            Assert.That(view.Required.Single(goal => goal.MaterialId == "wood").Achieved, Is.True);
            Assert.That(view.Required.Single(goal => goal.MaterialId == "wood").Progress, Is.EqualTo(1));
            Assert.That(view.InventoryQuantity, Is.EqualTo(huge * 2));
            Assert.That(view.BonusStatusText, Does.EndWith("報酬×8"));
        }

        [TestCase(0, "5:00", false)]
        [TestCase(269.9, "0:31", false)]
        [TestCase(270, "0:30", true)]
        [TestCase(400, "0:00", true)]
        public void TimerRoundsRemainingUpAndClampsAtZero(double elapsed, string text, bool urgent)
        {
            GameConfig config = Config();
            GameHudView view = GameHudProjection.Present(config, Snapshot(config, (decimal)elapsed), "player1", 30);
            Assert.That(view.TimerText, Is.EqualTo(text)); Assert.That(view.TimerUrgent, Is.EqualTo(urgent));
        }

        [Test]
        public void StreakUsesSelectedActorAndConfiguredPrecision()
        {
            GameConfig config = Config(); GameSnapshot state = Snapshot(config, streak: 4);
            Assert.That(GameHudProjection.Present(config, state, "player1", 30).StreakMultiplierText, Is.EqualTo("×1.32"));
            Assert.That(GameHudProjection.Present(config, state, "player2", 30).StreakText, Is.EqualTo("0"));
            Assert.Throws<ArgumentException>(() => GameHudProjection.Present(config, state, "missing", 30));
            config = Config(json => json["format"]["decimalPlaces"] = 3);
            Assert.That(GameHudProjection.Present(config, Snapshot(config, streak: 4), "player1", 30).StreakMultiplierText, Is.EqualTo("×1.320"));
        }

        [Test]
        public void FailedRoundKeepsZeroTimerAndFinalGoalsAfterServerResetsChallenge()
        {
            GameConfig config = Config();
            var server = new LocalGameServer(config, 202647, objectives: config.DefaultObjectives, enableBots: false);
            server.Buy("player1", "wood", 5);
            server.Tick(config.DurationSeconds);
            GameSnapshot state = server.Snapshot();
            Assert.That(state.Phase, Is.EqualTo(RoundPhase.Failed));
            Assert.That(state.ElapsedSeconds, Is.Zero);
            GameHudView view = GameHudProjection.Present(config, state, "player1", 30);
            Assert.That(view.TimerText, Is.EqualTo("0:00"));
            Assert.That(view.Required.Single(goal => goal.MaterialId == "wood").CurrentText, Is.EqualTo("5"));
            Assert.That(view.BonusRevealed, Is.True);
        }

        [Test]
        public void HistoryUsesAuthoritativeTimestampMaterialQualityAndQuantity()
        {
            GameConfig config = Config();
            var record = new OperationRecord(1, "player2", OperationKind.Gather, "wood", "good", 3, 0, 1, 64.9m, null);
            Assert.That(GameHudProjection.GatherHistory(config, record, "たろう"), Is.EqualTo("[1:04] たろうが薪×3(良質)を採集"));
        }
    }
}
