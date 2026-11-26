using System;
using System.IO;
using System.Linq;
using System.Numerics;
using Imoni.Core;
using Imoni.Core.Effects;
using NUnit.Framework;

namespace Imoni.Tests
{
    public sealed class ImoniEffectResultTests
    {
        private static GameConfig Config()
        {
#if UNITY_EDITOR
            string path = Path.Combine(UnityEngine.Application.dataPath, "Imoni/Config/GameConfig.json");
#else
            string path = Path.Combine(TestContext.CurrentContext.TestDirectory, "GameConfig.json");
#endif
            return GameConfig.FromJson(File.ReadAllText(path));
        }

        [TestCase(1, false, "Result")]
        [TestCase(5, false, "Clear")]
        [TestCase(6, true, "Result")]
        [TestCase(12, true, "Result")]
        public void SuccessfulEffectUsesSettledScoreAndRoutesWithoutAnotherSettlement(int round, bool endless, string route)
        {
            var config = Config(); var inventory = new Inventory(config);
            inventory.Add("wood", "normal", 4, config.FixedScale * 2);
            inventory.Add("mushroom", "normal", 3, config.FixedScale);
            var goals = new RoundObjectives(new[] { new Objective("wood", 4) }, new Objective("mushroom", 3));
            var evaluation = Scoring.Evaluate(config, inventory, goals);
            var result = new RoundResult(round, 123, config.DurationSeconds, evaluation, evaluation.Score, Array.Empty<OperationRecord>(), isEndless: endless);
            var view = ImoniEffectResultProjection.Present(config, result);
            Assert.That(view.Score, Is.EqualTo(evaluation.Score));
            Assert.That(view.SuccessReferenceScore, Is.EqualTo(evaluation.Score));
            Assert.That(view.Failed, Is.False); Assert.That(view.MissingMaterialNames, Is.Empty);
            Assert.That(view.ResultRoute, Is.EqualTo(route)); Assert.That(view.Seed, Is.EqualTo(result.Seed));
        }

        [Test]
        public void FailureReferenceAddsOnlyRequiredDeficitsAndKeepsActualScoreAndMissingNames()
        {
            var config = Config(); var inventory = new Inventory(config);
            inventory.Add("wood", "normal", 3, config.FixedScale * 7 / 3);
            inventory.Add("taro", "good", 1, config.FixedScale * 4);
            inventory.Add("mushroom", "normal", 8, config.FixedScale * 3);
            var goals = new RoundObjectives(new[] { new Objective("wood", 10), new Objective("taro", 5), new Objective("negi", 4) }, new Objective("mushroom", 2));
            var evaluation = Scoring.Evaluate(config, inventory, goals);
            var result = new RoundResult(3, 567, config.DurationSeconds, evaluation, 0, Array.Empty<OperationRecord>());
            var view = ImoniEffectResultProjection.Present(config, result);
            foreach (var deficit in evaluation.RequiredGoals.Where(goal => !goal.IsMet))
                inventory.Add(deficit.MaterialId, "normal", deficit.Required - deficit.Current, config.FixedScale);
            var hypotheticalSuccess = Scoring.Evaluate(config, inventory, goals);
            Assert.That(hypotheticalSuccess.Success, Is.True);
            Assert.That(view.SuccessReferenceScore, Is.EqualTo(hypotheticalSuccess.Score));
            Assert.That(view.Score, Is.EqualTo(evaluation.Score));
            Assert.That(view.MissingMaterialNames, Is.EqualTo(evaluation.MissingMaterialIds.Select(id => config.Material(id).Name)));
            Assert.That(view.MissingMaterials, Is.EqualTo("薪・里芋・ねぎ"));
            Assert.That(view.Failed, Is.True); Assert.That(view.ResultRoute, Is.EqualTo("GameOver"));
        }

        [Test]
        public void FailurePresentationSurvivesAutomaticRunResetAndDoesNotMutateServer()
        {
            var config = Config();
            var objectives = new RoundObjectives(new[] { new Objective("wood", 100), new Objective("taro", 20) }, new Objective("mushroom", 2));
            var server = new LocalGameServer(config, 7, objectives: objectives, enableBots: false);
            server.Buy("player1", "wood", 1); server.Tick(config.DurationSeconds);
            var settled = server.Snapshot();
            Assert.That(settled.Lots, Is.Empty);
            var first = ImoniEffectResultProjection.Present(config, settled.LastResult);
            var again = ImoniEffectResultProjection.Present(config, settled.LastResult);
            Assert.That(first.Score, Is.EqualTo(settled.LastResult.Evaluation.Score));
            Assert.That(first.MissingMaterials, Is.EqualTo("薪・里芋"));
            Assert.That(again.SuccessReferenceScore, Is.EqualTo(first.SuccessReferenceScore));
            Assert.That(server.Snapshot().Funds, Is.EqualTo(settled.Funds));
            Assert.That(server.Snapshot().LastResult, Is.SameAs(settled.LastResult));
        }
    }
}
