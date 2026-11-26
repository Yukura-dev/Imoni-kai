using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Numerics;
using Newtonsoft.Json.Linq;
using NUnit.Framework;

namespace Imoni.Core.Tests
{
    public sealed class ProgressionRankingTests
    {
        private static JObject Data()
        {
#if UNITY_EDITOR
            return JObject.Parse(File.ReadAllText(Path.Combine(UnityEngine.Application.dataPath, "Imoni/Config/GameConfig.json")));
#else
            return JObject.Parse(File.ReadAllText(Path.Combine(TestContext.CurrentContext.TestDirectory, "GameConfig.json")));
#endif
        }
        private static RoundObjectives Goals() => new RoundObjectives(new[] { new Objective("wood", 1) }, new Objective("mushroom", 1));
        private static RoundResult Result(JObject data, int round, bool success = true, bool endless = false, BigInteger? score = null, IEnumerable<OperationRecord> history = null, int? seed = null)
        {
            GameConfig config = GameConfig.FromJson(data.ToString()); var inventory = new Inventory(config);
            if (success) inventory.Add("wood", "normal", 1, config.FixedScale);
            var evaluation = Scoring.Evaluate(config, inventory, Goals());
            return new RoundResult(round, seed ?? data["outGame"]["lobby"]["commonSeed"].Value<int>(), config.DurationSeconds, evaluation,
                score ?? evaluation.Score, history ?? Array.Empty<OperationRecord>(), isEndless: endless);
        }
        private static OperationRecord Gather(int round, string material, int quantity, int streak = 0, string quality = "normal", string actor = "player1", OperationKind kind = OperationKind.Gather) =>
            new OperationRecord(1, actor, kind, material, quality, quantity, 0, round, 10, null, streak);
        private static ProfileData Grant(JObject data, ProfileData profile, RoundResult result, string key) => PlayerProgression.Apply(data, profile, result, "player1", key).Profile;
        private static ProfileData Register(JObject data, ProfileData profile, RoundResult result, string id, RankingSeed seed = RankingSeed.Common, bool finish = false, string team = "鍋底同盟") =>
            LocalRanking.Register(data, profile, result, id, team, data["team"]["size"].Value<int>(), seed, new DateTimeOffset(2026, 11, 26, 12, 0, 0, TimeSpan.Zero), finish);

        [Test]
        public void FreshAndLegacySavesHaveNoMockAchievementsWhileOldOwnedPartsSurvive()
        {
            JObject data = Data(); ProfileData fresh = ProfilePersistence.Load(data, null);
            Assert.That(fresh.OwnedFirstTitles, Is.EqualTo(new[] { "芋煮会の" })); Assert.That(fresh.OwnedSecondTitles, Is.EqualTo(new[] { "初心者" }));
            Assert.That(fresh.OwnedBadges, Is.Empty); Assert.That(fresh.Progression.BestRound, Is.Zero); Assert.That(fresh.Rankings, Is.Empty);
            ProfileData old = ProfilePersistence.Load(data, "{\"Name\":\"旧会員\",\"OwnedFirstTitles\":[\"山の\",\"伝説の\"],\"FirstTitle\":\"山の\"}");
            Assert.That(old.FirstTitle, Is.EqualTo("山の")); Assert.That(old.OwnedFirstTitles, Does.Contain("伝説の")); Assert.That(old.Progression.NormalClears, Is.EqualTo(BigInteger.Zero));
        }
        [Test]
        public void FirstSuccessPreviewAndPersistenceGrantOnceWithoutChangingChosenTitle()
        {
            JObject data = Data(); ProfileData original = ProfilePersistence.Load(data, null); RoundResult result = Result(data, 1);
            ProgressionOutcome preview = PlayerProgression.Apply(data, original, result, "player1", "run1:1");
            Assert.That(preview.Titles.Select(title => title.Value), Does.Contain("鍋奉行")); Assert.That(preview.BadgeIds, Does.Contain("first_step"));
            Assert.That(original.OwnedSecondTitles, Does.Not.Contain("鍋奉行")); Assert.That(preview.Profile.SecondTitle, Is.EqualTo(original.SecondTitle));
            ProfileData saved = ProfilePersistence.Load(data, ProfilePersistence.Save(data, preview.Profile));
            ProgressionOutcome again = PlayerProgression.Apply(data, saved, result, "player1", "run1:1");
            Assert.That(again.Titles, Is.Empty); Assert.That(again.BadgeIds, Is.Empty); Assert.That(again.Profile.AppliedRoundKeys.Length, Is.EqualTo(1));
        }
        [Test]
        public void MultipleTitleRewardsUseTwoSummaryLinesAndPreserveAllNamesForDetails()
        {
            JObject data = Data(); var result = Result(data, 1, history: new[] { Gather(1, "wood", 300), Gather(1, "taro", 30) });
            var outcome = PlayerProgression.Apply(data, ProfilePersistence.Load(data, null), result, "player1", "many:1");
            var view = RoundResultProjection.Present(GameConfig.FromJson(data.ToString()), result, progressionTitles: outcome.Titles);
            Assert.That(outcome.Titles.Count, Is.EqualTo(4)); Assert.That(view.TitleRewardText, Is.EqualTo("「薪割り職人」GET!\nほか3件"));
            Assert.That(view.NewTitleText, Does.Contain("薪割り職人").And.Contain("里芋ハンター").And.Contain("鍋奉行").And.Contain("採集王"));
        }
        [Test]
        public void PreOwnershipSavePreservesSelectedCatalogPartsWithoutGrantingOtherMockParts()
        {
            JObject data = Data();
            ProfileData profile = ProfilePersistence.Load(data, "{\"FirstTitle\":\"山の\",\"SecondTitle\":\"薪割り職人\"}");
            Assert.That(profile.FirstTitle, Is.EqualTo("山の")); Assert.That(profile.SecondTitle, Is.EqualTo("薪割り職人"));
            Assert.That(profile.OwnedFirstTitles, Is.EquivalentTo(new[] { "芋煮会の", "山の" })); Assert.That(profile.OwnedSecondTitles, Does.Not.Contain("里芋ハンター"));
        }
        [Test]
        public void ReachingRoundGrantsReachBadgeEvenIfPlayerThenAbortsButNeverClearOrRanking()
        {
            JObject data = Data(); ProfileData profile = PlayerProgression.ReachRound(data, ProfilePersistence.Load(data, null), 3);
            Assert.That(profile.Progression.BestRound, Is.EqualTo(3)); Assert.That(profile.OwnedBadges, Does.Contain("round3"));
            Assert.That(profile.Progression.NormalClears, Is.EqualTo(BigInteger.Zero)); Assert.That(profile.Rankings, Is.Empty);
            profile = PlayerProgression.ReachRound(data, profile, 1); Assert.That(profile.Progression.BestRound, Is.EqualTo(3));
        }
        [Test]
        public void OnlyThisActorsCurrentRoundGathersCountNotPurchasesOrBotsOrPriorHistory()
        {
            JObject data = Data(); var operations = new[] { Gather(1, "wood", 1000), Gather(2, "wood", 1000, actor: "player2"),
                Gather(2, "wood", 1000, kind: OperationKind.Buy), Gather(2, "wood", 29), Gather(2, "taro", 30, 10, "ultimate") };
            ProfileData profile = Grant(data, ProfilePersistence.Load(data, null), Result(data, 2, history: operations), "gather:2");
            Assert.That(profile.Progression.GatheredByMaterial["wood"], Is.EqualTo(new BigInteger(29)));
            Assert.That(profile.OwnedSecondTitles, Does.Not.Contain("薪割り職人")); Assert.That(profile.OwnedSecondTitles, Does.Contain("里芋ハンター"));
            Assert.That(profile.OwnedFirstTitles, Does.Contain("山の")); Assert.That(profile.OwnedFirstTitles, Does.Contain("連続の"));
            Assert.That(profile.OwnedBadges, Does.Contain("streak10")); Assert.That(profile.OwnedBadges, Does.Contain("ultimate"));
            profile = Grant(data, profile, Result(data, 3, history: new[] { Gather(3, "wood", 1) }), "gather:3");
            Assert.That(profile.OwnedSecondTitles, Does.Contain("薪割り職人")); Assert.That(profile.OwnedBadges, Does.Contain("round3"));
        }
        [Test]
        public void GatheringTotalAndThresholdsComeFromConfig()
        {
            JObject data = Data(); JToken rule = data["progression"]["titleRules"].First(item => item["value"].Value<string>() == "採集王"); rule["minimum"] = 11;
            ProfileData profile = Grant(data, ProfilePersistence.Load(data, null), Result(data, 1, history: new[] { Gather(1, "wood", 10) }), "a:1");
            Assert.That(profile.OwnedSecondTitles, Does.Not.Contain("採集王"));
            profile = Grant(data, profile, Result(data, 2, history: new[] { Gather(2, "taro", 1) }), "a:2");
            Assert.That(profile.OwnedSecondTitles, Does.Contain("採集王"));
        }
        [Test]
        public void NormalClearCountIsPerChallengeAndEndlessDoesNotCountAsNormalClear()
        {
            JObject data = Data(); ProfileData profile = ProfilePersistence.Load(data, null);
            profile = Grant(data, profile, Result(data, 5), "a:5"); profile = Grant(data, profile, Result(data, 5), "a:5");
            Assert.That(profile.Progression.NormalClears, Is.EqualTo(BigInteger.One)); Assert.That(profile.OwnedFirstTitles, Does.Contain("伝説の"));
            profile = Grant(data, profile, Result(data, 5, endless: true), "endless:5"); Assert.That(profile.Progression.NormalClears, Is.EqualTo(BigInteger.One));
            profile = Grant(data, profile, Result(data, 5), "b:5"); profile = Grant(data, profile, Result(data, 5), "c:5");
            Assert.That(profile.OwnedSecondTitles, Does.Contain("芋煮マスター")); Assert.That(profile.Progression.NormalClears, Is.EqualTo(new BigInteger(3)));
        }
        [Test]
        public void EndlessRoundTitlesAndHugeScoreBadgePreserveExactCountsAfterReload()
        {
            JObject data = Data(); BigInteger huge = BigInteger.Pow(10, 100) + 123;
            ProfileData profile = Grant(data, ProfilePersistence.Load(data, null), Result(data, 7, endless: true, score: huge), "a:7");
            Assert.That(profile.OwnedFirstTitles, Does.Contain("神話の")); Assert.That(profile.OwnedFirstTitles, Does.Not.Contain("究極の"));
            profile = Grant(data, profile, Result(data, 10, endless: true, score: huge), "a:10");
            profile = ProfilePersistence.Load(data, ProfilePersistence.Save(data, profile));
            Assert.That(profile.OwnedFirstTitles, Does.Contain("究極の")); Assert.That(profile.OwnedBadges, Does.Contain("score_100m"));
            Assert.That(profile.Progression.BestScore, Is.EqualTo(huge));
        }
        [Test]
        public void FailureKeepsPersonalReachAndGatherBadgesButGivesNoTitlesOrRanking()
        {
            JObject data = Data(); RoundResult failed = Result(data, 3, false, score: BigInteger.Pow(10, 20), history: new[] { Gather(3, "wood", 300, 10) });
            var outcome = PlayerProgression.Apply(data, ProfilePersistence.Load(data, null), failed, "player1", "failed:3");
            Assert.That(outcome.Titles, Is.Empty); Assert.That(outcome.Profile.OwnedBadges, Does.Contain("round3"));
            Assert.That(outcome.Profile.OwnedBadges, Does.Contain("streak10")); Assert.That(outcome.Profile.OwnedBadges, Does.Not.Contain("score_100m"));
            Assert.That(Register(data, outcome.Profile, failed, "failed").Rankings, Is.Empty);
        }
        [TestCase(false, 1, false, false)]
        [TestCase(false, 4, false, false)]
        [TestCase(false, 5, false, true)]
        [TestCase(true, 5, true, false)]
        [TestCase(true, 6, false, false)]
        [TestCase(true, 6, true, true)]
        public void RankingAcceptsOnlyCompletedNormalOrExplicitSuccessfulEndlessEnd(bool endless, int round, bool finish, bool expected)
        {
            JObject data = Data(); var profile = Register(data, ProfilePersistence.Load(data, null), Result(data, round, endless: endless), "run", finish: finish);
            Assert.That(profile.Rankings.Length, Is.EqualTo(expected ? 1 : 0));
        }
        [TestCase(false)]
        [TestCase(true)]
        public void FailedResultsNeverEnterEitherBoard(bool endless)
        { JObject data = Data(); Assert.That(Register(data, ProfilePersistence.Load(data, null), Result(data, 7, false, endless), "failed", finish: true).Rankings, Is.Empty); }
        [Test]
        public void FourBoardsRemainSeparateAndEndlessOrdersByRoundBeforeScore()
        {
            JObject data = Data(); ProfileData profile = ProfilePersistence.Load(data, null);
            profile = Register(data, profile, Result(data, 5, score: 100), "normal-common");
            profile = Register(data, profile, Result(data, 5, score: 200, seed: 999), "normal-free", RankingSeed.Free);
            profile = Register(data, profile, Result(data, 7, endless: true, score: 1), "endless-common", finish: true);
            profile = Register(data, profile, Result(data, 6, endless: true, score: 999), "endless-common-low", finish: true);
            profile = Register(data, profile, Result(data, 8, endless: true, score: 400, seed: 999), "endless-free", RankingSeed.Free, true);
            int size = data["team"]["size"].Value<int>();
            Assert.That(LocalRanking.Board(profile, RankingMode.Normal, RankingSeed.Common, size).Single().Score, Is.EqualTo(new BigInteger(100)));
            Assert.That(LocalRanking.Board(profile, RankingMode.Normal, RankingSeed.Free, size).Single().Score, Is.EqualTo(new BigInteger(200)));
            Assert.That(LocalRanking.Board(profile, RankingMode.Endless, RankingSeed.Common, size).Select(row => row.RoundNumber), Is.EqualTo(new[] { 7, 6 }));
            Assert.That(LocalRanking.Board(profile, RankingMode.Endless, RankingSeed.Free, size).Single().RoundNumber, Is.EqualTo(8));
        }
        [Test]
        public void RankingReloadIsExactIdempotentAndRespectsConfiguredLimit()
        {
            JObject data = Data(); data["progression"]["rankingLimitPerBoard"] = 2; BigInteger huge = BigInteger.Pow(10, 100) + 7;
            ProfileData profile = ProfilePersistence.Load(data, null);
            profile = Register(data, profile, Result(data, 5, score: huge), "a"); profile = Register(data, profile, Result(data, 5, score: huge), "a");
            profile = Register(data, profile, Result(data, 5, score: 10), "b"); profile = Register(data, profile, Result(data, 5, score: 20), "c");
            ProfileData loaded = ProfilePersistence.Load(data, ProfilePersistence.Save(data, profile));
            Assert.That(loaded.Rankings.Length, Is.EqualTo(2)); Assert.That(loaded.Rankings[0].Score, Is.EqualTo(huge));
            Assert.That(loaded.Rankings[1].ChallengeId, Is.EqualTo("c")); Assert.That(loaded.Rankings[0].RecordedAtUtc.Offset, Is.EqualTo(TimeSpan.Zero));
        }
        [Test]
        public void CommonDivisionRejectsFreeSeedAndFailedExtensionKeepsEarlierNormalRecord()
        {
            JObject data = Data(); ProfileData profile = ProfilePersistence.Load(data, null);
            Assert.Throws<ArgumentException>(() => Register(data, profile, Result(data, 5, seed: 999), "free-as-common"));
            profile = Register(data, profile, Result(data, 5), "run"); profile = Register(data, profile, Result(data, 7, false, true), "run", finish: true);
            Assert.That(profile.Rankings.Single().Mode, Is.EqualTo(RankingMode.Normal));
        }
        [Test]
        public void ClearCanContinueSameWorldAndEndlessCanFinishOnlyAtSuccessfulBoundary()
        {
            JObject data = Data(); GameConfig config = GameConfig.FromJson(data.ToString());
            var server = new LocalGameServer(config, 202647, objectives: Goals(), enableBots: false);
            for (int round = 1; round <= config.RoundsToClear; round++)
            { server.Buy("player1", "wood", 1); server.Tick(config.DurationSeconds); if (round < config.RoundsToClear) server.StartNextRound(Goals()); }
            WorldMap world = server.World; GameSnapshot before = server.Snapshot(); server.StartEndless(Goals());
            Assert.That(server.World, Is.SameAs(world)); Assert.That(server.Snapshot().RoundNumber, Is.EqualTo(config.RoundsToClear + 1));
            Assert.That(server.Snapshot().Funds, Is.EqualTo(before.Funds)); Assert.That(server.Snapshot().CumulativeScore, Is.EqualTo(before.CumulativeScore));
            Assert.Throws<InvalidOperationException>(() => server.StartEndless()); Assert.Throws<InvalidOperationException>(() => server.FinishEndless());
            server.Buy("player1", "wood", 1); server.Tick(config.DurationSeconds);
            Assert.That(server.Snapshot().Phase, Is.EqualTo(RoundPhase.Succeeded)); Assert.That(server.Snapshot().LastResult.IsEndless, Is.True);
            Assert.That(RoundResultProjection.Present(config, server.Snapshot().LastResult).IsClear, Is.False);
            server.FinishEndless(); Assert.That(server.Snapshot().Phase, Is.EqualTo(RoundPhase.EndlessFinished));
            Assert.Throws<InvalidOperationException>(() => server.StartNextRound()); Assert.Throws<InvalidOperationException>(() => server.FinishEndless());
        }
        [Test]
        public void EndlessFreshRunStartsAtR1AndAbortOrFailureCannotFinish()
        {
            JObject data = Data(); GameConfig config = GameConfig.FromJson(data.ToString());
            var server = new LocalGameServer(config, 202647, objectives: Goals(), enableBots: false, endless: true);
            Assert.That(server.Snapshot().RoundNumber, Is.EqualTo(1)); Assert.That(server.Snapshot().IsEndless, Is.True);
            server.Tick(config.DurationSeconds); Assert.That(server.Snapshot().LastResult.IsEndless, Is.True);
            Assert.Throws<InvalidOperationException>(() => server.FinishEndless()); server.Retry(999, Goals());
            Assert.That(server.Snapshot().IsEndless, Is.True); server.AbortChallenge("player1");
            Assert.Throws<InvalidOperationException>(() => server.FinishEndless());
        }
    }
}
