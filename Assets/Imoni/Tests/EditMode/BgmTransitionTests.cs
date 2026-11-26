using System;
using System.Collections.Generic;
using System.Linq;
using Imoni.Core.Audio;
using NUnit.Framework;

namespace Imoni.Core.Tests
{
    public sealed class BgmTransitionTests
    {
        private static double Weight(BgmTransition transition, string key) => transition.Weights.TryGetValue(key, out double value) ? value : 0;
        private static void IsWeight(BgmTransition transition, string key, double expected) => Assert.That(Weight(transition, key), Is.EqualTo(expected).Within(1e-12));

        [Test]
        public void InitialPlaybackFadesFromSilenceToFullWeight()
        {
            var transition = new BgmTransition();
            Assert.That(transition.Weights, Is.Empty); Assert.That(transition.IsTransitioning, Is.False);
            transition.Request("home", 0.5); IsWeight(transition, "home", 0);
            transition.Tick(0.25); IsWeight(transition, "home", 0.5);
            transition.Tick(0.25); IsWeight(transition, "home", 1);
            Assert.That(transition.IsTransitioning, Is.False);
        }

        [Test]
        public void CrossfadeReplacesTheOldTrackAtTheExactEnd()
        {
            var transition = new BgmTransition(); transition.Reset("home"); transition.Request("game", 0.5);
            transition.Tick(0.25); IsWeight(transition, "home", 0.5); IsWeight(transition, "game", 0.5);
            transition.Tick(0.25);
            Assert.That(transition.Weights.Keys, Is.EquivalentTo(new[] { "game" })); IsWeight(transition, "game", 1);
            Assert.That(transition.ElapsedSeconds, Is.EqualTo(transition.DurationSeconds));
        }

        [Test]
        public void DuplicateScreenIntentDoesNotRestartOrExtendFade()
        {
            var transition = new BgmTransition(); transition.Request("home", 0.5); transition.Tick(0.2);
            Assert.That(transition.Request("home", 0.5), Is.False);
            Assert.That(transition.ElapsedSeconds, Is.EqualTo(0.2)); IsWeight(transition, "home", 0.4);
            transition.Tick(0.3); IsWeight(transition, "home", 1); Assert.That(transition.IsTransitioning, Is.False);
        }

        [Test]
        public void RapidThirdTrackKeepsEveryCurrentWeightContinuous()
        {
            var transition = new BgmTransition(); transition.Reset("home"); transition.Request("game", 0.5); transition.Tick(0.2);
            transition.Request("band-game", 0.5);
            IsWeight(transition, "home", 0.6); IsWeight(transition, "game", 0.4); IsWeight(transition, "band-game", 0);
            transition.Tick(0.25);
            IsWeight(transition, "home", 0.3); IsWeight(transition, "game", 0.2); IsWeight(transition, "band-game", 0.5);
            transition.Tick(0.25);
            Assert.That(transition.Weights.Keys, Is.EquivalentTo(new[] { "band-game" }));
        }

        [Test]
        public void ReturningToAFadingTrackKeepsItsCurrentWeight()
        {
            var transition = new BgmTransition(); transition.Reset("home"); transition.Request("game", 0.5); transition.Tick(0.2);
            transition.Request("home", 0.5); IsWeight(transition, "home", 0.6); IsWeight(transition, "game", 0.4);
            transition.Tick(0.25); IsWeight(transition, "home", 0.8); IsWeight(transition, "game", 0.2);
            transition.Tick(0.25); Assert.That(transition.Weights.Keys, Is.EquivalentTo(new[] { "home" }));
        }

        [Test]
        public void EffectFadeOutStopsEveryStillAudibleTrack()
        {
            var transition = new BgmTransition(); transition.Reset("home"); transition.Request("game", 0.5); transition.Tick(0.2);
            transition.Request(null, 0.3); transition.Tick(0.15);
            IsWeight(transition, "home", 0.3); IsWeight(transition, "game", 0.2);
            Assert.That(transition.Request(null, 0.3), Is.False);
            transition.Tick(0.15);
            Assert.That(transition.Weights, Is.Empty); Assert.That(transition.TargetKey, Is.Null); Assert.That(transition.IsTransitioning, Is.False);
        }

        [Test]
        public void ResultCanResumeMusicDuringAnUnfinishedEffectFade()
        {
            var transition = new BgmTransition(); transition.Reset("home"); transition.Request(null, 0.3); transition.Tick(0.15);
            transition.Request("home", 0.5); IsWeight(transition, "home", 0.5);
            transition.Tick(0.25); IsWeight(transition, "home", 0.75);
            transition.Tick(0.25); IsWeight(transition, "home", 1);
        }

        [Test]
        public void ZeroDurationAndExplicitTransportResetAreImmediate()
        {
            var transition = new BgmTransition(); transition.Request("home", 0); IsWeight(transition, "home", 1);
            transition.Request("game", 0.5); transition.Tick(0.2); transition.Reset("probe");
            Assert.That(transition.Weights.Keys, Is.EquivalentTo(new[] { "probe" })); IsWeight(transition, "probe", 1);
            Assert.That(transition.IsTransitioning, Is.False);
            transition.Reset(); Assert.That(transition.Weights, Is.Empty); Assert.That(transition.TargetKey, Is.Null);
        }

        [Test]
        public void ExplicitClockHasTheSameOutcomeAcrossFramePartitions()
        {
            var one = new BgmTransition(); var many = new BgmTransition();
            one.Reset("home"); many.Reset("home"); one.Request("game", 0.5); many.Request("game", 0.5);
            one.Tick(0.25); for (int i = 0; i < 5; i++) many.Tick(0.05);
            IsWeight(many, "home", Weight(one, "home")); IsWeight(many, "game", Weight(one, "game"));
            one.Tick(100); many.Tick(100);
            Assert.That(many.Weights, Is.EquivalentTo(one.Weights)); Assert.That(many.ElapsedSeconds, Is.EqualTo(0.5));
        }

        [Test]
        public void RepeatedRetargetingNeverExceedsNormalizedTotalGainOrLeavesFinishedTracks()
        {
            var transition = new BgmTransition();
            string[] targets = { "home", "game", "band-game", null, "band-home", "home", null };
            foreach (string target in targets)
            {
                Dictionary<string, double> before = transition.Weights.ToDictionary(pair => pair.Key, pair => pair.Value);
                transition.Request(target, 0.5);
                foreach (var pair in before) IsWeight(transition, pair.Key, pair.Value);
                transition.Tick(0.1);
                Assert.That(transition.Weights.Values.All(weight => weight >= 0 && weight <= 1), Is.True);
                Assert.That(transition.Weights.Values.Sum(), Is.LessThanOrEqualTo(1 + 1e-12));
            }
            transition.Tick(0.5); Assert.That(transition.Weights, Is.Empty);
        }

        [Test]
        public void StoppingSilenceAndZeroTickDoNotStartAnEnvelope()
        {
            var transition = new BgmTransition();
            Assert.That(transition.Request(null, 0.3), Is.False); Assert.That(transition.IsTransitioning, Is.False);
            transition.Request("home", 0.5); transition.Tick(0);
            IsWeight(transition, "home", 0); Assert.That(transition.ElapsedSeconds, Is.Zero);
        }

        [TestCase(-0.1)] [TestCase(double.NaN)] [TestCase(double.PositiveInfinity)]
        public void InvalidTimeIsRejectedWithoutChangingTheCurrentEnvelope(double value)
        {
            var transition = new BgmTransition(); transition.Reset("home");
            Assert.Throws<ArgumentOutOfRangeException>(() => transition.Request("game", value));
            Assert.Throws<ArgumentOutOfRangeException>(() => transition.Tick(value));
            Assert.That(transition.TargetKey, Is.EqualTo("home")); IsWeight(transition, "home", 1);
        }

        [TestCase("")] [TestCase(" ")]
        public void EmptyKeysAreRejectedWithoutStoppingCurrentTrack(string key)
        {
            var transition = new BgmTransition(); transition.Reset("home");
            Assert.Throws<ArgumentException>(() => transition.Request(key, 0.5));
            Assert.Throws<ArgumentException>(() => transition.Reset(key));
            Assert.That(transition.TargetKey, Is.EqualTo("home"));
        }

        [Test]
        public void WeightViewCannotBeMutatedByTheAudioAdapter()
        {
            var transition = new BgmTransition(); transition.Reset("home");
            Assert.Throws<NotSupportedException>(() => ((IDictionary<string, double>)transition.Weights)["home"] = 0);
            IsWeight(transition, "home", 1);
        }
    }
}
