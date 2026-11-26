using System.IO;
using System.Linq;
using Imoni.Core;
using NUnit.Framework;

namespace Imoni.Tests
{
    public sealed class ReconnectionTests
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

        [Test]
        public void LocalReconnectKeepsTheLiveWorldAndTeamStateRatherThanRestartingTheChallenge()
        {
            GameConfig config = Config(); IGameServer server = new LocalGameServer(config, 202647);
            server.Buy("player1", "wood", 10); server.SetProtection("player1", "wood", "normal", 5); server.Upgrade("player1", "move");
            ResourceNode target = server.World.Resources.First(node => server.CanSelectResource("player1", node.Id));
            Assert.That(server.TrySelectResource("player1", target.Id), Is.True);
            WorldMap world = server.World; GameSnapshot before = server.Snapshot();
            server.SetConnected("player1", false);
            server.Tick(config.DurationSeconds / 10);
            GameSnapshot disconnected = server.Snapshot();
            Assert.That(disconnected.Players.First(player => player.Id == "player1").Action, Is.EqualTo(PlayerAction.Idle));
            Assert.That(disconnected.History.Any(record => record.ActorId == "player1" && record.Kind == OperationKind.Gather), Is.False);
            Assert.That(disconnected.History.Any(record => record.ActorId != "player1" && record.Kind == OperationKind.Gather), Is.True);
            Assert.That(server.TrySelectResource("player1", target.Id), Is.False);
            server.SetConnected("player1", true);
            GameSnapshot reconnected = server.Snapshot();
            Assert.That(server.World, Is.SameAs(world));
            Assert.That(reconnected.RoundNumber, Is.EqualTo(before.RoundNumber));
            Assert.That(reconnected.ElapsedSeconds, Is.EqualTo(disconnected.ElapsedSeconds));
            Assert.That(reconnected.Funds, Is.EqualTo(disconnected.Funds));
            Assert.That(reconnected.Players.First(player => player.Id == "player1").Upgrades["move"], Is.EqualTo(1));
            Assert.That(reconnected.Protection.Select(item => item.Quantity), Is.EqualTo(disconnected.Protection.Select(item => item.Quantity)));
            Assert.That(reconnected.Lots.Select(item => item.Quantity), Is.EqualTo(disconnected.Lots.Select(item => item.Quantity)));
            ResourceNode available = server.World.Resources.First(node => server.CanSelectResource("player1", node.Id));
            Assert.That(server.TrySelectResource("player1", available.Id), Is.True);
            Assert.That(reconnected.Phase, Is.EqualTo(RoundPhase.Collecting));
        }
    }
}
