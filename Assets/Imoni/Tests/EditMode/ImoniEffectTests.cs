using System;
using System.IO;
using System.Linq;
using System.Numerics;
using Imoni.Core.Effects;
using Newtonsoft.Json.Linq;
using NUnit.Framework;

namespace Imoni.Tests
{
    public sealed class ImoniEffectTests
    {
        private static ImoniEffectConfig Config()
        {
#if UNITY_EDITOR
            var source = Path.Combine(UnityEngine.Application.dataPath, "Imoni/Config/GameConfig.json");
#else
            var source = Path.Combine(TestContext.CurrentContext.TestDirectory, "GameConfig.json");
#endif
            var json = JObject.Parse(File.ReadAllText(source));
            // Staging tests merge the fragment. Integrated CoreTests load the complete GameConfig directly.
            var fragment = Path.Combine(TestContext.CurrentContext.TestDirectory, "effect-visual-config.json");
            if (json["imoniEffect"]?["visual"] == null && File.Exists(fragment))
                json["imoniEffect"]["visual"] = JObject.Parse(File.ReadAllText(fragment));
            return ImoniEffectConfig.FromJson(json.ToString());
        }

        [TestCase("1", false)]
        [TestCase("18420", false)]
        [TestCase("1000000000000000", false)]
        [TestCase("1", true)]
        [TestCase("18420", true)]
        [TestCase("7350", true)]
        [TestCase("1000000000000000", true)]
        [TestCase("0", true)]
        public void EveryScoreShowsResultWithinTenSeconds(string value, bool failed)
        {
            var score = BigInteger.Parse(value);
            var timeline = new ImoniEffectTimeline(Config(), score, failed ? BigInteger.Max(18420, score * 5 / 2) : score, failed);
            timeline.AdvanceTo(10000);
            Assert.That(timeline.ResultVisible, Is.True);
            Assert.That(timeline.ResultAtMs, Is.LessThanOrEqualTo(10000));
            Assert.That(timeline.Score, Is.EqualTo(score));
            Assert.That(timeline.DisplayScore, Is.EqualTo(score));
            Assert.That(timeline.Items, Is.Empty);
        }

        [Test]
        public void TimelineAndDisplayAreIdenticalAtDifferentFrameRates()
        {
            var c = Config();
            var fine = new ImoniEffectTimeline(c, BigInteger.Pow(10, 15), BigInteger.Pow(10, 15), false, 123);
            var coarse = new ImoniEffectTimeline(c, BigInteger.Pow(10, 15), BigInteger.Pow(10, 15), false, 123);
            for (var t = 0.0; t < 5800; t += 1000.0 / 120) fine.AdvanceTo(t);
            fine.AdvanceTo(5800); coarse.AdvanceTo(5800);
            Assert.That(fine.Score, Is.EqualTo(coarse.Score));
            Assert.That(fine.DisplayScore, Is.EqualTo(coarse.DisplayScore));
            Assert.That(fine.Level, Is.EqualTo(coarse.Level));
            Assert.That(fine.Items.Select(i => i.SpawnMs), Is.EqualTo(coarse.Items.Select(i => i.SpawnMs)));
            Assert.That(fine.Events.Select(e => (e.Kind, e.AtMs, e.Points)), Is.EqualTo(coarse.Events.Select(e => (e.Kind, e.AtMs, e.Points))));
            fine.AdvanceTo(10000); coarse.AdvanceTo(10000);
            Assert.That(fine.FinaleAtMs, Is.EqualTo(coarse.FinaleAtMs));
        }

        [TestCase(false)]
        [TestCase(true)]
        public void EveryPresentedFrameIsDeterministicWhenFramesAreSkipped(bool failed)
        {
            var c = Config();
            var fine = new ImoniEffectTimeline(c, 18420, failed ? 46050 : 18420, failed, 812);
            var coarse = new ImoniEffectTimeline(c, 18420, failed ? 46050 : 18420, failed, 812);
            for (int target = 100; target <= 10000; target += 100)
            {
                for (int t = target - 99; t <= target; t++) fine.AdvanceTo(t);
                coarse.AdvanceTo(target);
                var left = fine.Evaluate(); var right = coarse.Evaluate();
                Assert.That(fine.Events.Select(e => (e.Kind, e.AtMs, e.Level, e.Points)),
                    Is.EqualTo(coarse.Events.Select(e => (e.Kind, e.AtMs, e.Level, e.Points))));
                Assert.That((left.PillarWidth, left.BurstOpacity, left.PotScale, left.ShakeX, left.ShakeY,
                    left.BannerScale, left.FinaleScale, left.Gray, left.Gauge, fine.DisplayScore),
                    Is.EqualTo((right.PillarWidth, right.BurstOpacity, right.PotScale, right.ShakeX, right.ShakeY,
                    right.BannerScale, right.FinaleScale, right.Gray, right.Gauge, coarse.DisplayScore)));
                Assert.That(fine.Bursts.Select(b => (b.Id, b.AtMs, b.Seed)),
                    Is.EqualTo(coarse.Bursts.Select(b => (b.Id, b.AtMs, b.Seed))));
            }
        }

        [TestCase(false)]
        [TestCase(true)]
        public void ConfigDerivedPoolsCoverAllLiveItemsBurstsAndRings(bool failed)
        {
            var c = Config(); var capacity = ImoniEffectPoolCapacity.From(c);
            foreach (int seed in new[] { 1, 7, 123, int.MinValue, int.MaxValue })
            {
                var timeline = new ImoniEffectTimeline(c, BigInteger.Pow(10, 15), BigInteger.Pow(10, 15), failed, seed);
                for (int t = 0; t <= 10000; t += 5)
                {
                    timeline.AdvanceTo(t);
                    Assert.That(timeline.Items.Count, Is.LessThanOrEqualTo(capacity.Ingredients));
                    Assert.That(timeline.Bursts.Count, Is.LessThanOrEqualTo(capacity.Bursts));
                    Assert.That(timeline.Bursts.Count(b => b.Big), Is.LessThanOrEqualTo(capacity.Rings));
                    Assert.That(ImoniEffectMath.SmallStarCount(timeline.Level, c), Is.LessThanOrEqualTo(capacity.StarsPerBurst));
                }
            }
        }

        [Test]
        public void FailureUsesTheSuccessThresholdsAndStopsAtLevelThree()
        {
            var c = Config();
            var success = new ImoniEffectTimeline(c, 18420, 18420, false);
            var failed = new ImoniEffectTimeline(c, 7350, 18420, true);
            success.AdvanceTo(10000); failed.AdvanceTo(10000);
            TestContext.WriteLine("Success levels(ms): " + string.Join(", ", success.LevelEvents.Select(e => e.Level + "=" + e.AtMs.ToString("F1"))) + "; finale=" + success.FinaleAtMs + "; result=" + success.ResultAtMs);
            TestContext.WriteLine("Failure levels(ms): " + string.Join(", ", failed.LevelEvents.Select(e => e.Level + "=" + e.AtMs.ToString("F1"))) + "; finale=" + failed.FinaleAtMs + "; result=" + failed.ResultAtMs);
            Assert.That(failed.Thresholds, Is.EqualTo(success.Thresholds));
            Assert.That(failed.Level, Is.EqualTo(3));
            Assert.That(success.Level, Is.EqualTo(5));
            Assert.That(failed.Evaluate().Gray, Is.EqualTo(1));
            Assert.That(failed.Evaluate().BurstOpacity, Is.Zero);
            Assert.That(failed.Evaluate().Flash, Is.Zero);
            Assert.That(failed.ResultAtMs - failed.FinaleAtMs, Is.EqualTo(1300).Within(0.000001));
        }

        [Test]
        public void IngredientCoordinatesUseScreenFormulaWithoutTwentyPixelOffset()
        {
            var c = Config();
            var timeline = new ImoniEffectTimeline(c, 18420, 18420, false);
            timeline.AdvanceTo(500);
            var item = timeline.Items.Single();
            var start = ImoniEffectMath.Ingredient(item, item.SpawnMs, c);
            var end = ImoniEffectMath.Ingredient(item, item.LandingMs, c);
            Assert.That(start.Y, Is.EqualTo(-90));
            Assert.That(start.X, Is.EqualTo(171 + item.JitterX));
            Assert.That(end.X, Is.EqualTo(171));
            Assert.That(end.Y, Is.EqualTo(516));
            Assert.That(end.X + 24, Is.EqualTo(195));
            Assert.That(end.Y + 24, Is.EqualTo(540));
            Assert.That(end.Scale, Is.EqualTo(0.65).Within(0.000001));
        }

        [Test]
        public void StalledFramesCannotDelayHardCapOrResultButton()
        {
            var c = Config();
            c.Data["fallMs"] = new JArray(9000, 9000, 9000, 9000, 9000, 9000);
            c = ImoniEffectConfig.FromJson(c.Data.ToString());
            var timeline = new ImoniEffectTimeline(c, 18420, 18420, false);
            timeline.AdvanceTo(12000);
            Assert.That(timeline.FinaleAtMs, Is.EqualTo(8200));
            Assert.That(timeline.ResultAtMs, Is.EqualTo(9200));
            Assert.That(timeline.ResultVisible, Is.True);
            Assert.That(timeline.Score, Is.EqualTo(new BigInteger(18420)));
        }

        [Test]
        public void SuccessFinaleUsesCorrectFlashAndOvershoot()
        {
            var complete = new ImoniEffectTimeline(Config(), 1, 1, false);
            complete.AdvanceTo(10000);
            var at = complete.FinaleAtMs.Value;
            var timeline = new ImoniEffectTimeline(Config(), 1, 1, false);
            timeline.AdvanceTo(at);
            Assert.That(timeline.Evaluate().FinaleScale, Is.Zero);
            Assert.That(timeline.Evaluate().Flash, Is.EqualTo(1));
            Assert.That(timeline.Evaluate().Gauge, Is.EqualTo(100));
            timeline.AdvanceTo(at + 300);
            Assert.That(timeline.Evaluate().FinaleScale, Is.EqualTo(1.15));
            timeline.AdvanceTo(at + 450);
            Assert.That(timeline.Evaluate().FinaleScale, Is.EqualTo(1));
            timeline.AdvanceTo(at + 1000);
            Assert.That(timeline.ResultVisible, Is.True);
        }

        [Test]
        public void ReferenceRandomSeedMatchesMockParkMillerSequence()
        {
            var rng = new ImoniEffectRandom(7);
            Assert.That(rng.Next(), Is.EqualTo((117649 - 1.0) / 2147483646).Within(1e-15));
        }

        [Test]
        public void RejectsInvalidTimeAndImpossibleDeadline()
        {
            var c = Config();
            var timeline = new ImoniEffectTimeline(c, 18420, 18420, false);
            timeline.AdvanceTo(1000);
            Assert.Throws<ArgumentOutOfRangeException>(() => timeline.AdvanceTo(0));
            Assert.Throws<ArgumentOutOfRangeException>(() => timeline.AdvanceTo(double.NaN));
            c.Data["hardCapMs"] = 9999;
            Assert.Throws<ArgumentException>(() => ImoniEffectConfig.FromJson(c.Data.ToString()));
        }
    }
}
