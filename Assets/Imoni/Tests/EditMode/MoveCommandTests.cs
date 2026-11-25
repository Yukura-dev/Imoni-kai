using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;

namespace Imoni.Core.Tests
{
    public sealed class MoveCommandTests
    {
        private static GameConfig Config()
        {
#if UNITY_EDITOR
            string path = Path.Combine(UnityEngine.Application.dataPath, "Imoni", "Config", "GameConfig.json");
#else
            string path = Path.Combine(TestContext.CurrentContext.TestDirectory, "GameConfig.json");
#endif
            return GameConfig.Load(path);
        }

        private static PlayerSnapshot Player(IGameServer server, string actor = "player1") =>
            server.Snapshot().Players.Single(player => player.Id == actor);

        private static IEnumerable<GridPosition> Positions(WorldMap world)
        {
            for (int y = 0; y < world.Height; y++)
                for (int x = 0; x < world.Width; x++) yield return new GridPosition(x, y);
        }

        private static ResourceNode Nearest(IGameServer server) => server.World.Resources
            .Where(node => server.CanSelectResource("player1", node.Id))
            .OrderBy(node => Pathfinding.FindPathToResource(server.World, Player(server).Position, node, 1).Steps.Count)
            .ThenBy(node => node.Id, StringComparer.Ordinal).First();

        [Test]
        public void GroundCommandUsesAStarBridgeRouteAndArrivesIdleWithoutGathering()
        {
            GameConfig config = Config();
            IGameServer server = new LocalGameServer(config, 482, enableBots: false);
            PathResult route = Positions(server.World).Where(point => server.World.IsWalkable(point, 1))
                .Select(point => Pathfinding.FindPath(server.World, server.World.Start, point, 1))
                .First(path => path.Reachable && path.Steps.Any(point => server.World.TileAt(point).IsBridge));
            GridPosition target = route.Steps.Last();
            Assert.That(server.CanMoveTo("player1", target), Is.True);
            Assert.That(server.TryMoveTo("player1", target), Is.True);
            Assert.That(Player(server).Action, Is.EqualTo(PlayerAction.Moving));
            Assert.That(Player(server).TargetResourceId, Is.Null);
            foreach (GridPosition step in route.Steps.Skip(1))
            {
                server.Tick(GatheringTiming.MoveStepSeconds(config, 0));
                Assert.That(Player(server).Position, Is.EqualTo(step));
                Assert.That(server.World.IsWalkable(step, 1), Is.True);
            }
            Assert.That(Player(server).Action, Is.EqualTo(PlayerAction.Idle));
            server.Tick(config.BaseCollectSeconds * config.ResourceStock);
            Assert.That(Player(server).Position, Is.EqualTo(target));
            Assert.That(server.Snapshot().Lots, Is.Empty);
            Assert.That(server.Snapshot().History, Is.Empty);
            Assert.That(server.World.Resources.All(node => node.RemainingStock == config.ResourceStock), Is.True);
        }

        [Test]
        public void TeammatesCanMoveOntoTheSameTile()
        {
            GameConfig config = Config();
            IGameServer server = new LocalGameServer(config, 482, enableBots: false);
            var target = new GridPosition(server.World.Start.X + 1, server.World.Start.Y);
            foreach (PlayerSnapshot player in server.Snapshot().Players)
                Assert.That(server.TryMoveTo(player.Id, target), Is.True);
            server.Tick(GatheringTiming.MoveStepSeconds(config, 0));
            Assert.That(server.Snapshot().Players.All(player => player.Position == target && player.Action == PlayerAction.Idle), Is.True);
        }

        [Test]
        public void GroundCommandCancelsIncompleteGatherWithoutCommittingInventory()
        {
            GameConfig config = Config();
            IGameServer server = new LocalGameServer(config, 482, enableBots: false);
            ResourceNode node = Nearest(server);
            PathResult route = Pathfinding.FindPathToResource(server.World, Player(server).Position, node, 1);
            Assert.That(server.TrySelectResource("player1", node.Id), Is.True);
            server.Tick((route.Steps.Count - 1) * GatheringTiming.MoveStepSeconds(config, 0));
            Assert.That(Player(server).Action, Is.EqualTo(PlayerAction.Gathering));
            server.Tick(GatheringTiming.CollectSeconds(config, 0, node) / 2);
            Assert.That(server.TryMoveTo("player1", server.World.Start), Is.True);
            Assert.That(Player(server).TargetResourceId, Is.Null);
            server.Tick((route.Steps.Count - 1) * GatheringTiming.MoveStepSeconds(config, 0) + config.BaseCollectSeconds);
            Assert.That(Player(server).Action, Is.EqualTo(PlayerAction.Idle));
            Assert.That(Player(server).Position, Is.EqualTo(server.World.Start));
            Assert.That(server.Snapshot().Lots, Is.Empty);
            Assert.That(server.Snapshot().History, Is.Empty);
            Assert.That(node.RemainingStock, Is.EqualTo(config.ResourceStock));
        }

        [Test]
        public void InvalidGroundCommandDoesNotCancelTheCurrentResourceSelection()
        {
            GameConfig config = Config();
            IGameServer server = new LocalGameServer(config, 482, enableBots: false);
            ResourceNode node = Nearest(server);
            server.TrySelectResource("player1", node.Id);
            GridPosition water = Positions(server.World).First(point => server.World.TileAt(point).IsWater &&
                !server.World.TileAt(point).IsBridge && server.World.IsUnlocked(point, 1));
            foreach (GridPosition target in new[] { water, new GridPosition(0, 0), new GridPosition(-1, 0) })
            {
                Assert.That(server.CanMoveTo("player1", target), Is.False);
                Assert.That(server.TryMoveTo("player1", target), Is.False);
                Assert.That(Player(server).TargetResourceId, Is.EqualTo(node.Id));
            }
            Assert.That(server.TryMoveTo("unknown", server.World.Start), Is.False);
            Assert.That(server.TryMoveTo(null, server.World.Start), Is.False);
            Assert.That(Player(server).TargetResourceId, Is.EqualTo(node.Id));
        }

        [Test]
        public void DisconnectedAndFinishedRoundRejectGroundCommands()
        {
            GameConfig config = Config();
            IGameServer server = new LocalGameServer(config, 482, enableBots: false);
            var target = new GridPosition(server.World.Start.X + 1, server.World.Start.Y);
            server.TryMoveTo("player1", target);
            server.SetConnected("player1", false);
            Assert.That(server.CanMoveTo("player1", target), Is.False);
            Assert.That(server.TryMoveTo("player1", target), Is.False);
            Assert.That(Player(server).Action, Is.EqualTo(PlayerAction.Idle));
            server.Tick(config.DurationSeconds);
            Assert.That(server.Snapshot().Phase, Is.Not.EqualTo(RoundPhase.Collecting));
            Assert.That(server.CanMoveTo("player1", target), Is.False);
            Assert.That(server.TryMoveTo("player1", target), Is.False);
        }

        [Test]
        public void CurrentPositionCommandSucceedsAndCancelsThePreviousActionImmediately()
        {
            GameConfig config = Config();
            IGameServer server = new LocalGameServer(config, 482, enableBots: false);
            GridPosition start = server.World.Start;
            server.TryMoveTo("player1", new GridPosition(start.X + 2, start.Y));
            server.Tick(GatheringTiming.MoveStepSeconds(config, 0) / 2);
            Assert.That(server.CanMoveTo("player1", start), Is.True);
            Assert.That(server.TryMoveTo("player1", start), Is.True);
            Assert.That(Player(server).Position, Is.EqualTo(start));
            Assert.That(Player(server).Action, Is.EqualTo(PlayerAction.Idle));
            Assert.That(Player(server).ActionRemainingSeconds, Is.Zero);
            server.Tick(config.BaseCollectSeconds);
            Assert.That(Player(server).Position, Is.EqualTo(start));
            Assert.That(server.Snapshot().Lots, Is.Empty);
        }

        [Test]
        public void GroundMovementIsIndependentOfTickPartitioning()
        {
            GameConfig config = Config();
            IGameServer whole = new LocalGameServer(config, 482, enableBots: false);
            IGameServer partitioned = new LocalGameServer(config, 482, enableBots: false);
            var target = new GridPosition(whole.World.Start.X + 2, whole.World.Start.Y);
            whole.TryMoveTo("player1", target);
            partitioned.TryMoveTo("player1", target);
            decimal duration = GatheringTiming.MoveStepSeconds(config, 0) * 2 + config.BaseCollectSeconds;
            whole.Tick(duration);
            for (int index = 0; index < 10; index++) partitioned.Tick(duration / 10);
            Assert.That(Player(partitioned).Position, Is.EqualTo(Player(whole).Position));
            Assert.That(Player(partitioned).Action, Is.EqualTo(Player(whole).Action));
            Assert.That(Player(partitioned).ActionRemainingSeconds, Is.EqualTo(Player(whole).ActionRemainingSeconds));
            Assert.That(partitioned.Snapshot().ElapsedSeconds, Is.EqualTo(whole.Snapshot().ElapsedSeconds));
            Assert.That(partitioned.Snapshot().Lots, Is.Empty);
            Assert.That(partitioned.Snapshot().History, Is.Empty);
        }

        [Test]
        public void RepeatedGroundTapsPreserveProgressAndCannotStallMovement()
        {
            GameConfig config = Config();
            IGameServer server = new LocalGameServer(config, 482, enableBots: false);
            var target = new GridPosition(server.World.Start.X + 2, server.World.Start.Y);
            decimal halfStep = GatheringTiming.MoveStepSeconds(config, 0) / 2;
            for (int index = 0; index < 4; index++)
            {
                Assert.That(server.TryMoveTo("player1", target), Is.True);
                server.Tick(halfStep);
            }
            Assert.That(Player(server).Position, Is.EqualTo(target));
            Assert.That(Player(server).Action, Is.EqualTo(PlayerAction.Idle));
            Assert.That(server.Snapshot().Lots, Is.Empty);
        }

        [Test]
        public void GroundMovementUsesTheMovementUpgrade()
        {
            GameConfig config = Config();
            IGameServer server = new LocalGameServer(config, 482, enableBots: false);
            server.Upgrade("player1", "move");
            var target = new GridPosition(server.World.Start.X + 2, server.World.Start.Y);
            server.TryMoveTo("player1", target);
            decimal duration = GatheringTiming.MoveStepSeconds(config, 1);
            Assert.That(Player(server).ActionDurationSeconds, Is.EqualTo(duration));
            Assert.That(duration, Is.LessThan(GatheringTiming.MoveStepSeconds(config, 0)));
            server.Tick(duration * 2);
            Assert.That(Player(server).Position, Is.EqualTo(target));
            Assert.That(Player(server).Action, Is.EqualTo(PlayerAction.Idle));
        }

        [Test]
        public void ResourceSelectionCanReplaceGroundMovementAndResumeTheGatherPipeline()
        {
            GameConfig config = Config();
            IGameServer server = new LocalGameServer(config, 482, enableBots: false);
            ResourceNode node = Nearest(server);
            server.TryMoveTo("player1", new GridPosition(server.World.Start.X + 2, server.World.Start.Y));
            Assert.That(server.TrySelectResource("player1", node.Id), Is.True);
            PathResult route = Pathfinding.FindPathToResource(server.World, Player(server).Position, node, 1);
            server.Tick((route.Steps.Count - 1) * GatheringTiming.MoveStepSeconds(config, 0) +
                GatheringTiming.CollectSeconds(config, 0, node));
            Assert.That(server.Snapshot().History.Count(item => item.Kind == OperationKind.Gather), Is.EqualTo(1));
            Assert.That(server.Snapshot().Lots, Is.Not.Empty);
        }
    }
}
