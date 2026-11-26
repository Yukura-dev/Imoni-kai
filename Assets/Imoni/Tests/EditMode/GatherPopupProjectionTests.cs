using System;
using System.IO;
using System.Numerics;
using Imoni.Core;
using NUnit.Framework;

namespace Imoni.Core.Tests
{
    public sealed class GatherPopupProjectionTests
    {
        private static GameConfig Config()
        {
#if UNITY_EDITOR
            string path = Path.Combine(UnityEngine.Application.dataPath, "Imoni", "Config", "GameConfig.json");
#else
            string path = Path.Combine(TestContext.CurrentContext.TestDirectory, "GameConfig.json");
#endif
            return GameConfig.FromJson(File.ReadAllText(path));
        }

        [TestCase("normal", 1, 0, "+3 薪(普通)")]
        [TestCase("good", 1, 0, "+3 薪(良質)")]
        [TestCase("rare", 1, 1, "+3 薪(希少) 連続1")]
        [TestCase("rare", 1, 4, "+3 薪(希少) 連続4")]
        [TestCase("rare", 1, 0, "+3 薪(希少)")]
        [TestCase("rare", 2, 4, "+3 薪(希少)")]
        [TestCase("legend", 2, 1, "+3 薪(伝説) 連続1")]
        [TestCase("good", 1, 4, "+3 薪(良質)")]
        public void StreakSuffixRequiresPositiveStreakAndHighestQualityForTheRecordedRound(
            string quality, int round, int streak, string expected)
        {
            var record = new OperationRecord(1, "player1", OperationKind.Gather, "wood", quality, 3, 0, round, 1, null, streak);
            Assert.That(GatherPopupProjection.Text(Config(), record), Is.EqualTo(expected));
        }

        [Test]
        public void PopupUsesConfiguredBigIntegerFormattingWithoutTruncation()
        {
            GameConfig config = Config();
            BigInteger quantity = BigInteger.Pow(10, 20);
            var record = new OperationRecord(1, "player1", OperationKind.Gather, "wood", "normal", quantity, 0, 1, 1, null);
            Assert.That(GatherPopupProjection.Text(config, record), Is.EqualTo("+" + Format.Big(quantity, config) + " 薪(普通)"));
        }

        [Test]
        public void PurchaseHistoryCannotBePresentedAsAGather()
        {
            var record = new OperationRecord(1, "player1", OperationKind.Buy, "wood", "normal", 10, -100, 1, 1, null);
            Assert.Throws<ArgumentException>(() => GatherPopupProjection.Text(Config(), record));
        }
    }
}
