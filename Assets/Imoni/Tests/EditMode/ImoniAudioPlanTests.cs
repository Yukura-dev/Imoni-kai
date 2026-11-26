using System;
using System.IO;
using System.Linq;
using System.Numerics;
using Imoni.Core.Effects;
using Newtonsoft.Json.Linq;
using NUnit.Framework;

namespace Imoni.Core.Tests
{
    public sealed class ImoniAudioPlanTests
    {
        private static JObject Config()
        {
#if UNITY_EDITOR
            return JObject.Parse(File.ReadAllText(Path.Combine(UnityEngine.Application.dataPath, "Imoni/Config/GameConfig.json")));
#else
            return JObject.Parse(File.ReadAllText(Path.Combine(TestContext.CurrentContext.TestDirectory, "GameConfig.json")));
#endif
        }
        private static ImoniEffectTimeline Timeline(bool failed = false)
        {
            var timeline = new ImoniEffectTimeline(ImoniEffectConfig.FromJson(Config().ToString()), failed ? new BigInteger(7350) : new BigInteger(18420), new BigInteger(18420), failed, 7);
            timeline.AdvanceTo(10000); return timeline;
        }
        [Test]
        public void DroneStartsAtConfiguredTimeAndBeatUsesLevelInterval()
        {
            var timeline = Timeline(); var config = timeline.Config;
            Assert.That(timeline.Events.Single(item => item.Kind == "drone").AtMs, Is.EqualTo(config.V("droneStartMs")));
            var beats = timeline.Events.Where(item => item.Kind == "beat").ToArray();
            for (int index = 1; index < beats.Length; index++)
                Assert.That(beats[index].AtMs - beats[index - 1].AtMs, Is.EqualTo(config.BeatIntervals[beats[index - 1].Level]));
        }
        [Test]
        public void BeatClipChangesAtConfiguredHihatLevel()
        {
            var timeline = Timeline(); var plan = new ImoniAudioPlan(timeline.Config);
            foreach (var item in timeline.Events.Where(item => item.Kind == "beat"))
                Assert.That(plan.Cue(item, false).ClipKey, Is.EqualTo(item.Level >= plan.Number("hihatMinLevel") ? "sfx_beat_hihat" : "sfx_beat"));
        }
        [Test]
        public void EveryHitUsesLevelPentatonicAndOctaveFormulaAndResetsOnLevelUp()
        {
            var timeline = Timeline(); var plan = new ImoniAudioPlan(timeline.Config); double[] penta = { 0, 2, 4, 7, 9 };
            foreach (var item in timeline.HitEvents)
            {
                int step = Math.Min(item.Hits, 9);
                Assert.That(plan.Cue(item, false).Pitch, Is.EqualTo(Math.Pow(2, (2 * item.Level + penta[step % 5] + 12 * (step / 5)) / 12.0)).Within(0.00000001));
            }
            foreach (var group in timeline.HitEvents.GroupBy(item => item.Level)) Assert.That(group.First().Hits, Is.EqualTo(1));
        }
        [TestCase(0)] [TestCase(1)] [TestCase(2)] [TestCase(3)] [TestCase(4)] [TestCase(5)]
        public void DronePitchAndLowPassFollowEachLevel(int level)
        {
            var plan = new ImoniAudioPlan(ImoniEffectConfig.FromJson(Config().ToString()));
            var state = plan.Drone(2000, level, 300, null, false);
            Assert.That(state.Pitch, Is.EqualTo(Math.Pow(2, 2 * level / 12.0)).Within(0.00000001));
            Assert.That(state.CutoffHz, Is.EqualTo(260 + 260 * level));
            Assert.That(state.Gain, Is.EqualTo(0.4 + 0.08 * level).Within(0.00000001));
        }
        [TestCase(false)] [TestCase(true)]
        public void FinaleClipAndDroneFadeMatchOutcome(bool failed)
        {
            var timeline = Timeline(failed); var plan = new ImoniAudioPlan(timeline.Config);
            var finale = timeline.Events.Single(item => item.Kind == "finale");
            Assert.That(plan.Cue(finale, failed).ClipKey, Is.EqualTo(failed ? "sfx_finale_fail" : "sfx_finale_success"));
            double fade = plan.Number(failed ? "failureFadeMs" : "successFadeMs");
            Assert.That(fade, Is.InRange(600, 1200));
            var middle = plan.Drone(finale.AtMs + fade / 2, finale.Level, 300, finale.AtMs, failed);
            var complete = plan.Drone(finale.AtMs + fade, finale.Level, 300, finale.AtMs, failed);
            Assert.That(middle.Gain, Is.GreaterThan(0)); Assert.That(complete.Gain, Is.Zero); Assert.That(complete.Finished, Is.True);
        }
        [Test]
        public void EveryLevelUsesItsSuppliedLevelClip()
        {
            var timeline = Timeline(); var plan = new ImoniAudioPlan(timeline.Config);
            foreach (var item in timeline.LevelEvents) Assert.That(plan.Cue(item, false).ClipKey, Is.EqualTo("sfx_levelup_L" + item.Level));
        }
        [Test]
        public void ChangedAudioConfigChangesPitchGainAndFadeWithoutCodeChanges()
        {
            JObject config = Config(); config["imoniEffect"]["audio"]["levelSemitones"] = 4; config["imoniEffect"]["audio"]["droneBaseVolume"] = 0.2;
            config["imoniEffect"]["audio"]["droneVolumePerLevel"] = 0; config["imoniEffect"]["audio"]["successFadeMs"] = 600;
            var plan = new ImoniAudioPlan(ImoniEffectConfig.FromJson(config.ToString())); var state = plan.Drone(2000, 3, 300, null, false);
            Assert.That(state.Pitch, Is.EqualTo(2)); Assert.That(state.Gain, Is.EqualTo(0.2));
            Assert.That(plan.Drone(2600, 3, 300, 2000, false).Finished, Is.True);
        }
        [Test]
        public void MasterMuteAndVolumeMapToDecibels()
        {
            JObject config = Config();
            Assert.That(ImoniAudioPlan.MasterDecibels(config, true, 1), Is.Zero);
            Assert.That(ImoniAudioPlan.MasterDecibels(config, true, 0.5), Is.EqualTo(20 * Math.Log10(0.5)).Within(0.00000001));
            Assert.That(ImoniAudioPlan.MasterDecibels(config, false, 1), Is.EqualTo(config["audio"]["minDecibels"].Value<double>()));
            Assert.That(ImoniAudioPlan.MasterDecibels(config, true, 0), Is.EqualTo(config["audio"]["minDecibels"].Value<double>()));
        }
        [Test]
        public void SoundVolumePersistsAndOldSaveUsesConfiguredDefault()
        {
            JObject config = Config(); var profile = ProfilePersistence.Load(config, "{\"SoundEnabled\":false}");
            Assert.That(profile.SoundVolume, Is.EqualTo(config["audio"]["defaultVolume"].Value<double>()));
            profile.SoundVolume = 0.3;
            var restored = ProfilePersistence.Load(config, ProfilePersistence.Save(config, profile));
            Assert.That(restored.SoundEnabled, Is.False); Assert.That(restored.SoundVolume, Is.EqualTo(0.3));
        }
        [TestCase(-1, 0)] [TestCase(2, 1)]
        public void SoundVolumeClampsToConfiguredLimits(double value, double expected)
        { Assert.That(ProfilePersistence.Normalize(Config(), new ProfileData { SoundVolume = value }).SoundVolume, Is.EqualTo(expected)); }
        [Test]
        public void InvalidSoundVolumeFallsBackAndRepeatedStepsReachExactEndpoints()
        {
            JObject config = Config(); double volume = double.NaN;
            Assert.That(ImoniAudioPlan.NormalizeVolume(config, volume), Is.EqualTo(config["audio"]["defaultVolume"].Value<double>()));
            volume = 1; for (int i = 0; i < 10; i++) volume = ImoniAudioPlan.AdjustVolume(config, volume, -1);
            Assert.That(volume, Is.Zero);
            for (int i = 0; i < 10; i++) volume = ImoniAudioPlan.AdjustVolume(config, volume, 1);
            Assert.That(volume, Is.EqualTo(1));
        }
    }
}
