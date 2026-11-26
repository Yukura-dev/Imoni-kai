using System;
using Imoni.Core.Audio;
using Newtonsoft.Json.Linq;
using NUnit.Framework;

namespace Imoni.Core.Tests
{
    public sealed class BgmLoopPlanTests
    {
        private static JObject Config() => JObject.Parse(@"{
            'audio': { 'gameMain': { 'loopStartSample': 557149, 'loopEndSample': 3446763, 'sampleRate': 44100 } }
        }");

        [Test]
        public void SuppliedLoopMetadataPreservesEveryPcmFrameAtTheJoin()
        {
            var plan = new BgmLoopPlan(Config());
            Assert.That(plan.IntroSampleCount, Is.EqualTo(557149));
            Assert.That(plan.LoopSampleCount, Is.EqualTo(2889614));
            Assert.That(plan.IntroSampleCount + plan.LoopSampleCount, Is.EqualTo(plan.LoopEndSample));
            Assert.That(plan.IntroSeconds, Is.EqualTo(557149.0 / 44100).Within(1e-12));
            Assert.That(plan.LoopSeconds, Is.EqualTo(2889614.0 / 44100).Within(1e-12));
        }

        [Test]
        public void ReplacedTrackUsesConfiguredSamplesAndFrequency()
        {
            JObject config = Config();
            config["audio"]["gameMain"]["loopStartSample"] = 48000;
            config["audio"]["gameMain"]["loopEndSample"] = 240000;
            config["audio"]["gameMain"]["sampleRate"] = 48000;
            var plan = new BgmLoopPlan(config);
            Assert.That(plan.IntroSeconds, Is.EqualTo(1));
            Assert.That(plan.LoopSeconds, Is.EqualTo(4));
        }

        [TestCase("loopStartSample")] [TestCase("loopEndSample")] [TestCase("sampleRate")]
        public void MissingNonpositiveAndFractionalSampleSettingsAreRejected(string field)
        {
            JObject config = Config(); ((JObject)config["audio"]["gameMain"]).Remove(field);
            Assert.Throws<ArgumentException>(() => new BgmLoopPlan(config));
            foreach (JToken value in new JToken[] { 0, -1, 1.5, (long)int.MaxValue + 1, "44100" })
            {
                config["audio"]["gameMain"][field] = value;
                Assert.Throws<ArgumentException>(() => new BgmLoopPlan(config));
            }
        }

        [TestCase(557149)] [TestCase(1)]
        public void EmptyAndReversedLoopRegionsAreRejected(int endSample)
        {
            JObject config = Config(); config["audio"]["gameMain"]["loopEndSample"] = endSample;
            Assert.Throws<ArgumentException>(() => new BgmLoopPlan(config));
        }
    }
}
