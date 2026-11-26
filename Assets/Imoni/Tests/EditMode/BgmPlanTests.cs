using System;
using System.IO;
using System.Linq;
using Imoni.Core.Audio;
using Newtonsoft.Json.Linq;
using NUnit.Framework;

namespace Imoni.Core.Tests
{
    public sealed class BgmPlanTests
    {
        private static JObject RepositoryConfig()
        {
#if UNITY_EDITOR
            return JObject.Parse(File.ReadAllText(Path.Combine(UnityEngine.Application.dataPath, "Imoni/Config/GameConfig.json")));
#else
            return JObject.Parse(File.ReadAllText(Path.Combine(TestContext.CurrentContext.TestDirectory, "GameConfig.json")));
#endif
        }
        private static JObject Config()
        {
            JObject root = RepositoryConfig();
            // Distinct fixture keys test selection independently of the final WAV naming convention.
            root["audio"]["bgmStyle"] = "wa";
            root["audio"]["bgmCrossfadeSeconds"] = 0.5;
            root["audio"]["bgmEffectFadeOutSeconds"] = 0.3;
            root["audio"]["bgmTracks"] = new JObject {
                ["wa"] = new JObject { ["home"] = "wa-home" },
                ["band"] = new JObject { ["home"] = "band-home" },
                ["game"] = "game-main"
            };
            return root;
        }

        [TestCase("wa", "Home", "wa-home")]
        [TestCase("band", "Home", "band-home")]
        [TestCase("wa", "Game", "game-main")]
        [TestCase("band", "Game", "game-main")]
        public void StyleSelectsHomeTrackAndBothStylesShareTheOfficialGameTrack(string style, string screen, string expected)
        {
            JObject config = Config(); config["audio"]["bgmStyle"] = style;
            BgmSelection selection = new BgmPlan(config).Select(screen);
            Assert.That(selection.TrackKey, Is.EqualTo(expected));
            Assert.That(selection.FadeSeconds, Is.EqualTo(0.5));
        }

        [TestCase("Main")] [TestCase("Tutorial1")] [TestCase("Tutorial2")] [TestCase("Tutorial3")]
        [TestCase("Home")] [TestCase("Lobby")] [TestCase("Waiting")] [TestCase("Loading")]
        [TestCase("Member")] [TestCase("Ranking")] [TestCase("Result")] [TestCase("GameOver")]
        [TestCase("Clear")] [TestCase("Endless")]
        public void OutOfGameScreensUseTheSameHomeTrack(string screen)
        { Assert.That(new BgmPlan(Config()).Select(screen).TrackKey, Is.EqualTo("wa-home")); }

        [TestCase("Imoni")] [TestCase("ImoniFail")]
        public void ImoniEffectStopsMusicWithConfiguredFade(string screen)
        {
            JObject config = Config(); config["audio"]["bgmEffectFadeOutSeconds"] = 0.17;
            BgmSelection selection = new BgmPlan(config).Select(screen);
            Assert.That(selection.TrackKey, Is.Null); Assert.That(selection.FadeSeconds, Is.EqualTo(0.17));
        }

        [TestCase("Home", "wa-home")] [TestCase("Game", "game-main")]
        [TestCase("Imoni", null)] [TestCase(null, "wa-home")]
        public void HelpKeepsItsOriginMusicContext(string origin, string expected)
        { Assert.That(new BgmPlan(Config()).Select("Help", origin).TrackKey, Is.EqualTo(expected)); }

        [Test]
        public void RuntimeStyleOverrideDoesNotMutateDefaultOrRestartTheGameTrack()
        {
            JObject config = Config();
            var plan = new BgmPlan(config);
            Assert.That(plan.Select("Home", styleOverride: "band").TrackKey, Is.EqualTo("band-home"));
            Assert.That(plan.Select("Home").TrackKey, Is.EqualTo("wa-home"));
            Assert.That(config["audio"]["bgmStyle"].Value<string>(), Is.EqualTo("wa"));
            var transition = new BgmTransition(); transition.Reset(plan.Select("Game").TrackKey);
            BgmSelection selection = plan.Select("Game", styleOverride: "band");
            Assert.That(transition.Request(selection.TrackKey, selection.FadeSeconds), Is.False);
            Assert.That(transition.IsTransitioning, Is.False);
        }

        [Test]
        public void CustomTrackKeysAndFadeDurationsComeFromConfig()
        {
            JObject config = Config(); config["audio"]["bgmTracks"]["wa"]["home"] = "replacement";
            config["audio"]["bgmCrossfadeSeconds"] = 1.25;
            BgmSelection selection = new BgmPlan(config).Select("Result");
            Assert.That(selection.TrackKey, Is.EqualTo("replacement")); Assert.That(selection.FadeSeconds, Is.EqualTo(1.25));
        }

        [TestCase("unknown")] [TestCase("")]
        public void UnknownStyleIsRejected(string value)
        {
            JObject config = Config(); config["audio"]["bgmStyle"] = value;
            Assert.Throws<ArgumentException>(() => new BgmPlan(config));
        }

        [TestCase("bgmCrossfadeSeconds")] [TestCase("bgmEffectFadeOutSeconds")]
        public void MissingAndInvalidDurationsAreRejected(string setting)
        {
            JObject config = Config(); ((JObject)config["audio"]).Remove(setting);
            Assert.Throws<ArgumentException>(() => new BgmPlan(config));
            foreach (double value in new[] { -0.1, double.NaN, double.PositiveInfinity })
            {
                config["audio"][setting] = value;
                Assert.Throws<ArgumentException>(() => new BgmPlan(config));
            }
        }

        [Test]
        public void MissingTrackIsRejectedBeforePlayback()
        {
            JObject config = Config(); ((JObject)config["audio"]["bgmTracks"]["band"]).Remove("home");
            Assert.Throws<ArgumentException>(() => new BgmPlan(config));
        }

        [Test]
        public void RepositoryConfigSelectsOnlySuppliedTracksAndMatchesLoopMetadata()
        {
            JObject config = RepositoryConfig();
            var plan = new BgmPlan(config); var loop = new BgmLoopPlan(config);
#if UNITY_EDITOR
            JObject metadata = JObject.Parse(File.ReadAllText(Path.Combine(UnityEngine.Application.dataPath, "Imoni/Audio/BGM/bgm_game_main_loop.json")));
#else
            JObject metadata = JObject.Parse(File.ReadAllText(Path.Combine(TestContext.CurrentContext.TestDirectory, "BgmLoopMetadata.json")));
#endif
            Assert.That(plan.Style, Is.EqualTo("wa"));
            Assert.That(plan.CrossfadeSeconds, Is.EqualTo(0.5));
            Assert.That(plan.EffectFadeOutSeconds, Is.EqualTo(0.3));
            string[] selected = { plan.Select("Home").TrackKey, plan.Select("Home", styleOverride: "band").TrackKey, plan.Select("Game").TrackKey };
            Assert.That(selected, Is.EquivalentTo(new[] { "bgm_home_loop", "bgm_home_loop_band", "bgm_game_main" }));
            JArray catalog = (JArray)config["audio"]["bgmCatalog"];
            Assert.That(catalog.Select(track => track["key"].Value<string>()), Is.EquivalentTo(selected));
            Assert.That(catalog.Values<JObject>().All(track => !track["loop"].Value<string>().Contains("bgm_game_loop")), Is.True);
            JObject game = catalog.Values<JObject>().Single(track => track["key"].Value<string>() == plan.GameTrackKey);
            Assert.That(Path.GetFileName(game["intro"].Value<string>()), Is.EqualTo("bgm_game_main_intro.wav"));
            Assert.That(Path.GetFileName(game["loop"].Value<string>()), Is.EqualTo("bgm_game_main_loop.wav"));
            Assert.That(loop.IntroSampleCount, Is.EqualTo(metadata["loopStartSample"].Value<int>()));
            Assert.That(loop.LoopEndSample, Is.EqualTo(metadata["loopEndSample"].Value<int>()));
            Assert.That(loop.SampleRate, Is.EqualTo(metadata["sampleRate"].Value<int>()));
        }
    }
}
