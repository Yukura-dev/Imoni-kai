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
            .OrderBy(node => Pathfinding.FindPathToResource(server.World, Player(server).Location, node, 1).DistanceTiles)
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
            for (int index = 1; index < route.Waypoints.Count; index++)
            {
                WorldPosition step = route.Waypoints[index];
                server.Tick(GatheringTiming.MoveStepSeconds(config, 0) * route.Waypoints[index - 1].DistanceTo(step));
                Assert.That(Player(server).Location, Is.EqualTo(step));
                Assert.That(server.World.IsWalkable(step.Cell, 1), Is.True);
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
            server.Tick(route.DistanceTiles * GatheringTiming.MoveStepSeconds(config, 0));
            Assert.That(Player(server).Action, Is.EqualTo(PlayerAction.Gathering));
            server.Tick(GatheringTiming.CollectSeconds(config, 0, node) / 2);
            Assert.That(server.TryMoveTo("player1", server.World.Start), Is.True);
            Assert.That(Player(server).TargetResourceId, Is.Null);
            server.Tick(route.DistanceTiles * GatheringTiming.MoveStepSeconds(config, 0) + config.BaseCollectSeconds);
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
        public void RetapDuringMovementStartsFromActualLocationWithoutSnappingBack()
        {
            GameConfig config = Config();
            IGameServer server = new LocalGameServer(config, 482, enableBots: false);
            GridPosition start = server.World.Start;
            server.TryMoveTo("player1", new GridPosition(start.X + 2, start.Y));
            server.Tick(GatheringTiming.MoveStepSeconds(config, 0) / 2);
            WorldPosition before = Player(server).Location;
            Assert.That(server.CanMoveTo("player1", start), Is.True);
            Assert.That(server.TryMoveTo("player1", start), Is.True);
            Assert.That(Player(server).Location, Is.EqualTo(before));
            Assert.That(before.X, Is.EqualTo(start.X + 0.5m));
            Assert.That(Player(server).Action, Is.EqualTo(PlayerAction.Moving));
            Assert.That(Player(server).ActionRemainingSeconds, Is.EqualTo(GatheringTiming.MoveStepSeconds(config, 0) / 2));
            server.Tick(GatheringTiming.MoveStepSeconds(config, 0) / 2);
            Assert.That(Player(server).Position, Is.EqualTo(start));
            Assert.That(Player(server).Action, Is.EqualTo(PlayerAction.Idle));
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
            Assert.That(Player(server).ActionDurationSeconds, Is.EqualTo(duration * 2));
            Assert.That(duration, Is.LessThan(GatheringTiming.MoveStepSeconds(config, 0)));
            server.Tick(duration * 2);
            Assert.That(Player(server).Position, Is.EqualTo(target));
            Assert.That(Player(server).Action, Is.EqualTo(PlayerAction.Idle));
        }

        [TestCase("player1")]
        [TestCase("player2")]
        public void MovementUpgradeImmediatelyRetimesOnlyThePurchasingActorsRemainingStraightLeg(string actor)
        {
            GameConfig config = Config();
            IGameServer server = new LocalGameServer(config, 482, enableBots: false);
            var target = new GridPosition(32, 22);
            Assert.That(Pathfinding.FindPath(server.World, server.World.Start, target, 1).Waypoints.Count, Is.EqualTo(2));
            foreach (PlayerSnapshot teammate in server.Snapshot().Players) server.TryMoveTo(teammate.Id, target);
            server.Tick(0.1m);
            PlayerSnapshot[] before = server.Snapshot().Players.ToArray();
            WorldPosition position = Player(server, actor).Location;
            decimal expected = position.DistanceTo(new WorldPosition(target)) * GatheringTiming.MoveStepSeconds(config, 1);
            server.Upgrade(actor, "move");
            Assert.That(Player(server, actor).Location, Is.EqualTo(position));
            Assert.That(Player(server, actor).ActionDurationSeconds, Is.EqualTo(expected));
            Assert.That(Player(server, actor).ActionRemainingSeconds, Is.EqualTo(expected));
            foreach (PlayerSnapshot teammate in before.Where(player => player.Id != actor))
            {
                PlayerSnapshot unchanged = Player(server, teammate.Id);
                Assert.That(unchanged.Location, Is.EqualTo(teammate.Location));
                Assert.That(unchanged.Upgrades["move"], Is.EqualTo(teammate.Upgrades["move"]));
                Assert.That(unchanged.ActionRemainingSeconds, Is.EqualTo(teammate.ActionRemainingSeconds));
            }
            server.Tick(expected);
            Assert.That(Player(server, actor).Location, Is.EqualTo(new WorldPosition(target)));
            Assert.That(Player(server, actor).Action, Is.EqualTo(PlayerAction.Idle));
            Assert.That(server.Snapshot().Players.Where(player => player.Id != actor).All(player => player.Action == PlayerAction.Moving), Is.True);
        }

        [Test]
        public void InFlightMovementUpgradeProducesTheSameArrivalForWholeOrPartitionedTicks()
        {
            GameConfig config = Config();
            IGameServer whole = new LocalGameServer(config, 482, enableBots: false);
            IGameServer partitioned = new LocalGameServer(config, 482, enableBots: false);
            var target = new GridPosition(32, 22);
            whole.TryMoveTo("player1", target); partitioned.TryMoveTo("player1", target);
            whole.Tick(0.1m);
            for (int i = 0; i < 4; i++) partitioned.Tick(0.025m);
            whole.Upgrade("player1", "move"); partitioned.Upgrade("player1", "move");
            Assert.That(Player(partitioned).Location, Is.EqualTo(Player(whole).Location));
            decimal remaining = Player(whole).ActionRemainingSeconds;
            whole.Tick(remaining / 2);
            for (int i = 0; i < 4; i++) partitioned.Tick(remaining / 8);
            Assert.That(Player(partitioned).Location, Is.EqualTo(Player(whole).Location));
            Assert.That(Player(partitioned).ActionRemainingSeconds, Is.EqualTo(Player(whole).ActionRemainingSeconds));
            whole.Tick(remaining / 2);
            for (int i = 0; i < 4; i++) partitioned.Tick(remaining / 8);
            Assert.That(Player(partitioned).Location, Is.EqualTo(new WorldPosition(target)));
            Assert.That(Player(whole).Location, Is.EqualTo(new WorldPosition(target)));
            Assert.That(Player(partitioned).Action, Is.EqualTo(PlayerAction.Idle));
            Assert.That(Player(whole).Action, Is.EqualTo(PlayerAction.Idle));
            Assert.That(partitioned.Snapshot().ElapsedSeconds, Is.EqualTo(whole.Snapshot().ElapsedSeconds));
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
            server.Tick(route.DistanceTiles * GatheringTiming.MoveStepSeconds(config, 0) +
                GatheringTiming.CollectSeconds(config, 0, node));
            Assert.That(server.Snapshot().History.Count(item => item.Kind == OperationKind.Gather), Is.EqualTo(1));
            Assert.That(server.Snapshot().Lots, Is.Not.Empty);
        }

        [Test]
        public void OpenGroundTapMovesContinuouslyOnTheStraightLineAtConstantSpeed()
        {
            GameConfig config = Config();
            IGameServer server = new LocalGameServer(config, 482, enableBots: false);
            GridPosition start = server.World.Start;
            var target = new GridPosition(start.X + 2, start.Y + 1);
            PathResult path = Pathfinding.FindPath(server.World, start, target, 1);
            Assert.That(path.Waypoints.Count, Is.EqualTo(2));
            decimal duration = path.DistanceTiles * GatheringTiming.MoveStepSeconds(config, 0);
            Assert.That(server.TryMoveTo("player1", target), Is.True);
            for (int frame = 1; frame <= 4; frame++)
            {
                server.Tick(duration / 4);
                WorldPosition expected = WorldPosition.Lerp(new WorldPosition(start), new WorldPosition(target), frame / 4m);
                Assert.That(Player(server).Location, Is.EqualTo(expected));
                Assert.That(Player(server).Action, Is.EqualTo(frame == 4 ? PlayerAction.Idle : PlayerAction.Moving));
            }
            TestContext.WriteLine($"M8 movement proof seed=482, start={start}, target={target}, seconds={duration}");
        }

        [Test]
        public void CancelAndDisconnectPreserveTheContinuousPosition()
        {
            GameConfig config = Config();
            IGameServer server = new LocalGameServer(config, 482, enableBots: false);
            GridPosition start = server.World.Start;
            server.TryMoveTo("player1", new GridPosition(start.X + 2, start.Y + 1));
            server.Tick(GatheringTiming.MoveStepSeconds(config, 0) / 3);
            WorldPosition before = Player(server).Location;
            server.CancelAction("player1");
            Assert.That(Player(server).Location, Is.EqualTo(before));
            server.TryMoveTo("player1", start);
            server.Tick(GatheringTiming.MoveStepSeconds(config, 0) / 10);
            before = Player(server).Location;
            server.SetConnected("player1", false);
            Assert.That(Player(server).Location, Is.EqualTo(before));
            Assert.That(Player(server).Action, Is.EqualTo(PlayerAction.Idle));
        }
    }
}
