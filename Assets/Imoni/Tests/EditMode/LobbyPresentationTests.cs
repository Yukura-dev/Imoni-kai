using System;
using System.IO;
using System.Linq;
using Imoni.Core;
using Newtonsoft.Json.Linq;
using NUnit.Framework;

namespace Imoni.Core.Tests
{
    public sealed class LobbyPresentationTests
    {
        private static LobbyFlowConfig Config(Action<JObject> edit = null)
        {
#if UNITY_EDITOR
            string path = Path.Combine(UnityEngine.Application.dataPath, "Imoni", "Config", "GameConfig.json");
#else
            string path = Path.Combine(TestContext.CurrentContext.TestDirectory, "GameConfig.json");
#endif
            var data = JObject.Parse(File.ReadAllText(path)); edit?.Invoke(data);
            return LobbyFlowConfig.FromJson(data.ToString());
        }
        [Test]
        public void ModeAndBonusDescriptionsFollowChangedGameRules()
        {
            GameConfig rules = null;
            Config(data =>
            {
                data["round"]["durationSec"] = 600; data["round"]["roundsToClear"] = 3;
                data["round"]["bonusRevealSec"] = 60; data["bonusTiers"][1]["minPercent"] = 175;
                rules = GameConfig.FromJson(data.ToString());
            });
            Assert.That(LobbyPresentation.ModeDescription(rules), Does.Contain("通常モード10分×3ラウンド"));
            Assert.That(LobbyPresentation.BonusHint(rules), Is.EqualTo("ボーナス素材は開始1分後に発表。175%以上でさらに報酬アップ"));
        }
        [TestCase(null, InviteJoinResult.Empty)]
        [TestCase("  ", InviteJoinResult.Empty)]
        [TestCase(" imo-482 ", InviteJoinResult.Success)]
        [TestCase("imo-777", InviteJoinResult.InProgress)]
        [TestCase("IMO-999", InviteJoinResult.NotFound)]
        public void InviteCodesNormalizeAndReturnDistinctOutcomes(string code, InviteJoinResult expected)
        { Assert.That(LobbyPresentation.Join(Config(), code), Is.EqualTo(expected)); }

        [Test]
        public void InviteCodesUseConfigurationRatherThanFixtureConstants()
        {
            LobbyFlowConfig config = Config(data => { data["outGame"]["lobby"]["successCode"] = "YES-001"; data["outGame"]["lobby"]["busyCode"] = "BUS-001"; });
            Assert.That(LobbyPresentation.Join(config, "IMO-482"), Is.EqualTo(InviteJoinResult.NotFound));
            Assert.That(LobbyPresentation.Join(config, " yes-001 "), Is.EqualTo(InviteJoinResult.Success));
            Assert.That(LobbyPresentation.Join(config, "BUS-001"), Is.EqualTo(InviteJoinResult.InProgress));
        }
        [Test]
        public void BotsJoinExactlyAtConfiguredTimesAndWaitingDotsCycle()
        {
            LobbyFlowConfig config = Config();
            Assert.That(LobbyPresentation.Waiting(config, 0).MemberCount, Is.EqualTo(1));
            for (int index = 0; index < config.BotJoinSeconds.Count; index++)
            {
                decimal time = config.BotJoinSeconds[index];
                Assert.That(LobbyPresentation.Waiting(config, time - .001m).MemberCount, Is.EqualTo(index + 1));
                Assert.That(LobbyPresentation.Waiting(config, time).MemberCount, Is.EqualTo(index + 2));
            }
            Assert.That(LobbyPresentation.Waiting(config, config.BotJoinSeconds.Last()).Full, Is.True);
            Assert.That(LobbyPresentation.Waiting(config, config.WaitingDotSeconds).DotCount, Is.EqualTo(2));
            Assert.That(LobbyPresentation.Waiting(config, config.WaitingDotSeconds * config.WaitingDotCount).DotCount, Is.EqualTo(1));
        }
        [Test]
        public void LoadingTracksEachExactThresholdAndStopsAtOneHundredPercent()
        {
            LobbyFlowConfig config = Config();
            Assert.That(LobbyPresentation.Loading(config, 0, 4).Steps[0], Is.EqualTo(LoadingStepState.Active));
            for (int index = 0; index < config.StepEnds.Count; index++)
            {
                decimal time = config.LoadingSeconds * config.StepEnds[index] / 100;
                Assert.That(LobbyPresentation.Loading(config, time - .0001m, 4).Steps[index], Is.Not.EqualTo(LoadingStepState.Done));
                Assert.That(LobbyPresentation.Loading(config, time, 4).Steps[index], Is.EqualTo(LoadingStepState.Done));
            }
            LoadingPresentation finished = LobbyPresentation.Loading(config, config.LoadingSeconds * 2, 4);
            Assert.That(finished.Percent, Is.EqualTo(100)); Assert.That(finished.Ready, Is.True);
            Assert.That(finished.SyncedMembers, Is.EqualTo(config.TeamSize));
            Assert.That(finished.Steps.All(state => state == LoadingStepState.Done), Is.True);
        }
        [Test]
        public void LoadingMemberSyncAndHintsUseConfiguredThresholdsAndIntervals()
        {
            LobbyFlowConfig config = Config(data => { data["outGame"]["loading"]["durationSeconds"] = 10; data["outGame"]["loading"]["tipSeconds"] = 1; });
            for (int index = 0; index < config.SyncThresholds.Count; index++)
            {
                decimal time = config.LoadingSeconds * config.SyncThresholds[index] / 100;
                Assert.That(LobbyPresentation.Loading(config, time - .001m, 4).SyncedMembers, Is.EqualTo(index));
                Assert.That(LobbyPresentation.Loading(config, time, 4).SyncedMembers, Is.EqualTo(index + 1));
            }
            Assert.That(LobbyPresentation.Loading(config, .99m, 4).TipIndex, Is.EqualTo(0));
            Assert.That(LobbyPresentation.Loading(config, 1, 4).TipIndex, Is.EqualTo(1));
            Assert.That(LobbyPresentation.Loading(config, 4, 4).TipIndex, Is.EqualTo(0));
        }
        [Test]
        public void InvalidTimingAndInputAreRejected()
        {
            Assert.Throws<ArgumentException>(() => Config(data => data["outGame"]["loading"]["durationSeconds"] = 0));
            Assert.Throws<ArgumentException>(() => Config(data => data["outGame"]["lobby"]["botJoinSeconds"] = new JArray(5, 2)));
            Assert.Throws<ArgumentOutOfRangeException>(() => LobbyPresentation.Waiting(Config(), -1));
            Assert.Throws<ArgumentOutOfRangeException>(() => LobbyPresentation.Loading(Config(), 0, 0));
        }
    }
}
