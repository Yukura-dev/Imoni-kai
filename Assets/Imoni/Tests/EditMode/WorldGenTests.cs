using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Newtonsoft.Json.Linq;
using NUnit.Framework;

namespace Imoni.Core.Tests
{
    public sealed class WorldGenTests
    {
        private static GameConfig Config(Action<JObject> edit = null)
        {
#if UNITY_EDITOR
            string path = Path.Combine(UnityEngine.Application.dataPath, "Imoni", "Config", "GameConfig.json");
#else
            string path = Path.Combine(TestContext.CurrentContext.TestDirectory, "GameConfig.json");
#endif
            JObject json = JObject.Parse(File.ReadAllText(path));
            edit?.Invoke(json);
            return GameConfig.FromJson(json.ToString());
        }

        [TestCase(202647)]
        [TestCase(482)]
        [TestCase(777)]
        public void SameSeedProducesIdenticalTerrainResourcesAndStart(int seed)
        {
            GameConfig config = Config();
            Assert.That(Signature(WorldGen.Generate(config, seed)), Is.EqualTo(Signature(WorldGen.Generate(config, seed))));
        }

        [Test]
        public void DifferentSeedsChangeWorldLayout()
        {
            GameConfig config = Config();
            Assert.That(Signature(WorldGen.Generate(config, 482)), Is.Not.EqualTo(Signature(WorldGen.Generate(config, 777))));
        }

        [TestCase(202647)]
        [TestCase(482)]
        [TestCase(777)]
        [TestCase(0)]
        [TestCase(-1)]
        public void EveryResourceHasAnAccessibleAdjacentHarvestTileAndCorrectBiome(int seed)
        {
            GameConfig config = Config();
            WorldMap world = WorldGen.Generate(config, seed);
            Assert.That(world.Resources, Is.Not.Empty);
            foreach (ResourceNode resource in world.Resources)
            {
                PathResult path = Pathfinding.FindPathToResource(world, world.Start, resource, config.RoundsToClear);
                Assert.That(path.Reachable, Is.True, resource.Id);
                Assert.That(path.Steps[0], Is.EqualTo(world.Start));
                Assert.That(path.Steps.Last().ManhattanDistance(resource.Position), Is.EqualTo(1));
                Assert.That(path.Steps.All(point => world.IsWalkable(point, config.RoundsToClear)), Is.True);
                Assert.That(config.Material(resource.MaterialId).Biomes, Does.Contain(resource.BiomeId));
                Assert.That(world.TileAt(resource.Position).BiomeId, Is.EqualTo(resource.BiomeId));
            }
            Assert.That(world.Resources.Select(node => node.Position).Distinct().Count(), Is.EqualTo(world.Resources.Count));
        }

        [TestCase(202647)]
        [TestCase(482)]
        [TestCase(777)]
        public void StartingSuppliesAndAllMaterialTypesAreGuaranteed(int seed)
        {
            GameConfig config = Config();
            WorldMap world = WorldGen.Generate(config, seed);
            IReadOnlyList<ResourceNode> firstRound = world.ResourcesAvailable(1);
            foreach (string material in config.World.StartingMaterials)
                Assert.That(firstRound.Count(node => node.MaterialId == material), Is.GreaterThanOrEqualTo(config.World.MinimumNodesPerMaterial));
            foreach (MaterialDefinition material in config.Materials)
                Assert.That(world.Resources.Count(node => node.MaterialId == material.Id), Is.GreaterThanOrEqualTo(config.World.MinimumNodesPerMaterial));
        }

        [Test]
        public void SparseGenerationRepairsStartingSuppliesWithoutBreakingBiomeRules()
        {
            GameConfig config = Config(json => json["world"]["resourceChance"] = 0m);
            WorldMap world = WorldGen.Generate(config, 7);
            foreach (string material in config.World.StartingMaterials)
                Assert.That(world.ResourcesAvailable(1).Count(node => node.MaterialId == material),
                    Is.GreaterThanOrEqualTo(config.World.MinimumNodesPerMaterial));
            Assert.That(world.Resources.All(node => config.Material(node.MaterialId).Biomes.Contains(world.TileAt(node.Position).BiomeId)), Is.True);
        }

        [Test]
        public void MapContainsAllBiomesBridgesAndSixteenSquareChunks()
        {
            GameConfig config = Config();
            WorldMap world = WorldGen.Generate(config, 202647);
            Assert.That(world.Width, Is.EqualTo(64));
            Assert.That(world.Height, Is.EqualTo(64));
            Assert.That(world.ChunkSize, Is.EqualTo(16));
            Assert.That(world.Chunks.Count, Is.EqualTo(16));
            var tiles = Enumerable.Range(0, world.Height).SelectMany(y => Enumerable.Range(0, world.Width)
                .Select(x => world.TileAt(new GridPosition(x, y)))).ToArray();
            Assert.That(tiles.Select(tile => tile.BiomeId).Distinct(), Is.EquivalentTo(new[] { "plain", "forest", "river", "mountain", "swamp" }));
            Assert.That(tiles.Count(tile => tile.IsBridge), Is.GreaterThanOrEqualTo(config.World.RiverWidth));
            Assert.That(tiles.Where(tile => tile.IsBridge).All(tile => tile.IsWater && tile.IsWalkable), Is.True);
            Assert.That(world.Chunks.SelectMany(chunk => chunk.Resources).Select(node => node.Id),
                Is.EquivalentTo(world.Resources.Select(node => node.Id)));
            foreach (WorldChunk chunk in world.Chunks)
                Assert.That(chunk.Resources.All(node => node.Position.X / world.ChunkSize == chunk.X &&
                    node.Position.Y / world.ChunkSize == chunk.Y), Is.True);
        }

        [Test]
        public void RoundUnlocksAreConcentricAndNeverShrink()
        {
            GameConfig config = Config();
            WorldMap world = WorldGen.Generate(config, 482);
            for (int round = 1; round <= config.RoundsToClear; round++)
                for (int y = 0; y < world.Height; y++)
                    for (int x = 0; x < world.Width; x++)
                    {
                        var point = new GridPosition(x, y);
                        long radius = config.World.UnlockRadius(round);
                        long dx = x - world.Start.X, dy = y - world.Start.Y;
                        Assert.That(world.IsUnlocked(point, round), Is.EqualTo(dx * dx + dy * dy <= radius * radius));
                        if (world.IsUnlocked(point, round)) Assert.That(world.IsUnlocked(point, round + 1), Is.True);
                    }
            Assert.That(world.IsUnlocked(new GridPosition(0, 0), int.MaxValue), Is.True);
        }

        [TestCase(1)]
        [TestCase(2)]
        public void MeanderingRiverStaysConnectedAndHasCrossings(int width)
        {
            GameConfig config = Config(json =>
            {
                var targets = (JArray)json["world"]["biomeTargets"];
                JToken river = targets.Single(item => (string)item["id"] == "river");
                JToken plain = targets.Single(item => (string)item["id"] == "plain");
                decimal fraction = (decimal)width / (int)json["world"]["width"];
                plain["fraction"] = (decimal)plain["fraction"] + (decimal)river["fraction"] - fraction;
                river["fraction"] = fraction;
            });
            WorldMap world = WorldGen.Generate(config, 777);
            var water = new HashSet<GridPosition>();
            for (int y = 0; y < world.Height; y++)
                for (int x = 0; x < world.Width; x++)
                {
                    var point = new GridPosition(x, y);
                    if (world.TileAt(point).IsWater) water.Add(point);
                }
            var visited = new HashSet<GridPosition>();
            var queue = new Queue<GridPosition>();
            queue.Enqueue(water.OrderBy(point => point.Y).ThenBy(point => point.X).First());
            while (queue.Count > 0)
            {
                GridPosition point = queue.Dequeue();
                if (!visited.Add(point)) continue;
                foreach (GridPosition neighbour in Pathfinding.Neighbours(point))
                    if (water.Contains(neighbour) && !visited.Contains(neighbour)) queue.Enqueue(neighbour);
            }
            Assert.That(visited, Is.EquivalentTo(water));
            Assert.That(water.Any(point => world.TileAt(point).IsBridge), Is.True);
            Assert.That(config.World.RiverWidth, Is.EqualTo(width));
        }

        [TestCase(482)]
        [TestCase(777)]
        [TestCase(202647)]
        public void BiomeAreasFollowTargetsAndStartingAreaIsMostlyPlain(int seed)
        {
            GameConfig config = Config();
            WorldMap world = WorldGen.Generate(config, seed);
            GridPosition[] positions = Enumerable.Range(0, world.Height).SelectMany(y =>
                Enumerable.Range(0, world.Width).Select(x => new GridPosition(x, y))).ToArray();
            foreach (BiomeTarget target in config.World.BiomeTargets)
            {
                decimal fraction = (decimal)positions.Count(point => world.TileAt(point).BiomeId == target.Id) / positions.Length;
                Assert.That(Math.Abs(fraction - target.Fraction), Is.LessThanOrEqualTo(0.015m), target.Id);
            }
            GridPosition[] first = positions.Where(point => world.IsUnlocked(point, 1)).ToArray();
            decimal startingPlain = (decimal)first.Count(point => world.TileAt(point).BiomeId == "plain") / first.Length;
            Assert.That(startingPlain, Is.GreaterThanOrEqualTo(config.World.StartingPlainFraction));
        }

        [TestCase(482)]
        [TestCase(777)]
        [TestCase(202647)]
        public void MaterialAbundanceFollowsGlobalWeightsAndFirstRoundHasSixOfEveryRequiredSupply(int seed)
        {
            GameConfig config = Config();
            WorldMap world = WorldGen.Generate(config, seed);
            decimal totalWeight = config.Materials.Sum(material => material.SpawnWeight);
            foreach (MaterialDefinition material in config.Materials)
            {
                decimal expected = world.Resources.Count * material.SpawnWeight / totalWeight;
                int actual = world.Resources.Count(node => node.MaterialId == material.Id);
                Assert.That(Math.Abs(actual - expected), Is.LessThanOrEqualTo(1m), material.Id);
            }
            Assert.That(world.Resources.Count(node => node.MaterialId == "wood"),
                Is.GreaterThan(world.Resources.Count(node => node.MaterialId == "mushroom")));
            foreach (string material in config.ObjectiveGeneration.FirstRoundRequiredMaterials)
                Assert.That(world.ResourcesAvailable(1).Count(node => node.MaterialId == material),
                    Is.GreaterThanOrEqualTo(config.World.MinimumStartingRequiredNodes), material);
        }

        [Test]
        public void ChangingWeightsAndStartingGuaranteeChangesGeneration()
        {
            GameConfig config = Config(json =>
            {
                ((JArray)json["materials"]).Single(item => (string)item["id"] == "wood")["spawnWeight"] = 10;
                ((JArray)json["materials"]).Single(item => (string)item["id"] == "mushroom")["spawnWeight"] = 40;
                json["world"]["minimumStartingRequiredNodes"] = 9;
            });
            WorldMap world = WorldGen.Generate(config, 482);
            Assert.That(world.Resources.Count(node => node.MaterialId == "mushroom"),
                Is.GreaterThan(world.Resources.Count(node => node.MaterialId == "wood")));
            foreach (string material in config.ObjectiveGeneration.FirstRoundRequiredMaterials)
                Assert.That(world.ResourcesAvailable(1).Count(node => node.MaterialId == material), Is.GreaterThanOrEqualTo(9));
        }

        [Test]
        public void InvalidBiomeFractionsWeightsAndStartingGuaranteeFailAtConfigLoad()
        {
            Assert.Throws<ArgumentException>(() => Config(json => json["world"]["biomeTargets"][0]["fraction"] = 0.9m));
            Assert.Throws<ArgumentException>(() => Config(json => json["materials"][0]["spawnWeight"] = 0));
            Assert.Throws<ArgumentException>(() => Config(json => json["world"]["minimumStartingRequiredNodes"] = 0));
            Assert.Throws<ArgumentException>(() => Config(json => json["world"]["startingPlainFraction"] = 1));
        }

        [Test]
        public void InvalidWorldDimensionsAndClearingFailAtConfigLoad()
        {
            Assert.Throws<ArgumentException>(() => Config(json => json["world"]["width"] = 63));
            Assert.Throws<ArgumentException>(() => Config(json => json["world"]["startClearRadius"] = 40));
            Assert.Throws<ArgumentException>(() => Config(json => json["world"]["resourceChance"] = -0.1m));
        }

        [Test]
        public void AStarAvoidsWaterAndUsesBridgeWithShortestPath()
        {
            GameConfig config = Config();
            WorldTile[,] tiles = PlainTiles(config);
            for (int y = 0; y < config.World.Height; y++) tiles[33, y] = new WorldTile("river", y == 34);
            var start = new GridPosition(32, 32);
            var world = new WorldMap(config.World, 0, tiles, Array.Empty<ResourceNode>(), start);
            PathResult path = Pathfinding.FindPath(world, start, new GridPosition(34, 32), 1);
            Assert.That(path.Reachable, Is.True);
            Assert.That(path.Steps.Count - 1, Is.EqualTo(6));
            Assert.That(path.Steps, Does.Contain(new GridPosition(33, 34)));
            Assert.That(path.Steps.All(point => !world.TileAt(point).IsWater || world.TileAt(point).IsBridge), Is.True);
        }

        [Test]
        public void AStarReportsWaterLockedAndDisconnectedTargetsAsUnreachable()
        {
            GameConfig config = Config();
            WorldTile[,] tiles = PlainTiles(config);
            for (int y = 0; y < config.World.Height; y++) tiles[33, y] = new WorldTile("river");
            var start = new GridPosition(32, 32);
            var world = new WorldMap(config.World, 0, tiles, Array.Empty<ResourceNode>(), start);
            Assert.That(Pathfinding.FindPath(world, start, new GridPosition(33, 32), 1).Reachable, Is.False);
            Assert.That(Pathfinding.FindPath(world, start, new GridPosition(34, 32), 1).Steps, Is.Empty);
            Assert.That(Pathfinding.FindPath(world, start, new GridPosition(0, 0), 1).Reachable, Is.False);
            Assert.That(Pathfinding.FindPath(world, start, new GridPosition(-1, 0), 1).Reachable, Is.False);
        }

        [Test]
        public void FishIsReachedFromBankWithoutWalkingIntoWater()
        {
            GameConfig config = Config();
            WorldTile[,] tiles = PlainTiles(config);
            var start = new GridPosition(32, 32);
            var fish = new ResourceNode(config, "fish-node", "fish", "river", new GridPosition(34, 32));
            tiles[34, 32] = new WorldTile("river");
            var world = new WorldMap(config.World, 0, tiles, new[] { fish }, start);
            PathResult path = Pathfinding.FindPathToResource(world, start, fish, 1);
            Assert.That(path.Reachable, Is.True);
            Assert.That(path.Steps.Last(), Is.EqualTo(new GridPosition(33, 32)));
            Assert.That(path.Steps, Has.No.Member(fish.Position));
        }

        [Test]
        public void ResourceBehindLockedRouteCannotBeSelectedEvenWhenTargetIsUnlocked()
        {
            GameConfig config = Config();
            WorldTile[,] tiles = PlainTiles(config);
            for (int y = 0; y < config.World.Height; y++) tiles[33, y] = new WorldTile("river", y == 48);
            var start = new GridPosition(32, 32);
            var node = new ResourceNode(config, "taro-node", "taro", "plain", new GridPosition(35, 32));
            var world = new WorldMap(config.World, 0, tiles, new[] { node }, start);
            Assert.That(world.IsUnlocked(node.Position, 1), Is.True);
            Assert.That(Pathfinding.FindPathToResource(world, start, node, 1).Reachable, Is.False);
            Assert.That(world.ResourcesAvailable(1), Is.Empty);
            Assert.That(Pathfinding.FindPathToResource(world, start, node, 2).Reachable, Is.True);
        }

        [Test]
        public void PathTieBreakingAndStartAtDestinationAreDeterministic()
        {
            GameConfig config = Config();
            var start = new GridPosition(32, 32);
            var world = new WorldMap(config.World, 0, PlainTiles(config), Array.Empty<ResourceNode>(), start);
            GridPosition target = new GridPosition(34, 30);
            Assert.That(Pathfinding.FindPath(world, start, target, 1).Steps,
                Is.EqualTo(Pathfinding.FindPath(world, start, target, 1).Steps));
            Assert.That(Pathfinding.FindPath(world, start, start, 1).Steps, Is.EqualTo(new[] { start }));
        }

        private static WorldTile[,] PlainTiles(GameConfig config)
        {
            var tiles = new WorldTile[config.World.Width, config.World.Height];
            for (int y = 0; y < config.World.Height; y++)
                for (int x = 0; x < config.World.Width; x++) tiles[x, y] = new WorldTile("plain");
            return tiles;
        }

        [Test]
        public void EightDirectionSearchUsesDiagonalCostAndSmoothsOpenGroundToOneStraightSegment()
        {
            GameConfig config = Config();
            var start = new GridPosition(32, 32);
            var target = new GridPosition(37, 35);
            var world = new WorldMap(config.World, 0, PlainTiles(config), Array.Empty<ResourceNode>(), start);
            PathResult path = Pathfinding.FindPath(world, start, target, 1);
            Assert.That(path.Reachable, Is.True);
            Assert.That(path.Steps.Count, Is.EqualTo(6));
            decimal searchDistance = path.Steps.Skip(1).Select((step, index) =>
                new WorldPosition(path.Steps[index]).DistanceTo(new WorldPosition(step))).Sum();
            Assert.That(searchDistance, Is.EqualTo(2 + 3 * (decimal)Math.Sqrt(2)));
            Assert.That(path.Waypoints, Is.EqualTo(new[] { new WorldPosition(start), new WorldPosition(target) }));
            Assert.That(path.DistanceTiles, Is.EqualTo((decimal)Math.Sqrt(34)));
        }

        [TestCase(true, false)]
        [TestCase(false, true)]
        [TestCase(true, true)]
        public void DiagonalSearchAndSmoothingCannotCutEitherWaterCorner(bool blockEast, bool blockSouth)
        {
            GameConfig config = Config();
            WorldTile[,] tiles = PlainTiles(config);
            var start = new GridPosition(32, 32);
            var target = new GridPosition(33, 33);
            if (blockEast) tiles[33, 32] = new WorldTile("river");
            if (blockSouth) tiles[32, 33] = new WorldTile("river");
            var world = new WorldMap(config.World, 0, tiles, Array.Empty<ResourceNode>(), start);
            Assert.That(Pathfinding.HasLineOfSight(world, new WorldPosition(start), new WorldPosition(target), 1), Is.False);
            PathResult path = Pathfinding.FindPath(world, start, target, 1);
            Assert.That(path.Reachable, Is.True);
            Assert.That(path.Steps[1], Is.Not.EqualTo(target));
            Assert.That(path.Waypoints.Count, Is.GreaterThan(2));
            for (int i = 1; i < path.Waypoints.Count; i++)
                Assert.That(Pathfinding.HasLineOfSight(world, path.Waypoints[i - 1], path.Waypoints[i], 1), Is.True);
        }

        [Test]
        public void DiagonalDoesNotEscapeATileSurroundedByWater()
        {
            GameConfig config = Config();
            WorldTile[,] tiles = PlainTiles(config);
            var start = new GridPosition(32, 32);
            foreach (GridPosition neighbour in Pathfinding.Neighbours(start)) tiles[neighbour.X, neighbour.Y] = new WorldTile("river");
            var world = new WorldMap(config.World, 0, tiles, Array.Empty<ResourceNode>(), start);
            Assert.That(Pathfinding.FindPath(world, start, new GridPosition(33, 33), 1).Reachable, Is.False);
        }

        [TestCase(32, 33)]
        [TestCase(33, 32)]
        [TestCase(32, 32)]
        public void LineOfSightChecksEveryTileTouchedByACornerEndpointInBothDirections(int waterX, int waterY)
        {
            GameConfig config = Config();
            WorldTile[,] tiles = PlainTiles(config);
            tiles[waterX, waterY] = new WorldTile("river");
            var world = new WorldMap(config.World, 0, tiles, Array.Empty<ResourceNode>(), new GridPosition(33, 33));
            var centre = new WorldPosition(33, 33);
            var corner = new WorldPosition(32.5m, 32.5m);
            Assert.That(Pathfinding.HasLineOfSight(world, centre, corner, 1), Is.False);
            Assert.That(Pathfinding.HasLineOfSight(world, corner, centre, 1), Is.False);
        }

        [TestCase(32.5, 33, 32, 33)]
        [TestCase(33, 32.5, 33, 32)]
        public void LineOfSightChecksBothSidesOfAnEdgeEndpointInBothDirections(double x, double y, int waterX, int waterY)
        {
            GameConfig config = Config();
            WorldTile[,] tiles = PlainTiles(config);
            tiles[waterX, waterY] = new WorldTile("river");
            var world = new WorldMap(config.World, 0, tiles, Array.Empty<ResourceNode>(), new GridPosition(33, 33));
            var centre = new WorldPosition(33, 33);
            var edge = new WorldPosition((decimal)x, (decimal)y);
            Assert.That(Pathfinding.HasLineOfSight(world, centre, edge, 1), Is.False);
            Assert.That(Pathfinding.HasLineOfSight(world, edge, centre, 1), Is.False);
        }

        [Test]
        public void LineOfSightAllowsWalkableCornerAndEdgeEndpointsInBothDirections()
        {
            GameConfig config = Config();
            var world = new WorldMap(config.World, 0, PlainTiles(config), Array.Empty<ResourceNode>(), new GridPosition(33, 33));
            var centre = new WorldPosition(33, 33);
            foreach (WorldPosition endpoint in new[] { new WorldPosition(32.5m, 32.5m), new WorldPosition(32.5m, 33), new WorldPosition(33, 32.5m) })
            {
                Assert.That(Pathfinding.HasLineOfSight(world, centre, endpoint, 1), Is.True);
                Assert.That(Pathfinding.HasLineOfSight(world, endpoint, centre, 1), Is.True);
            }
        }

        [Test]
        public void DiagonalDoesNotCutAnUnopenedCorner()
        {
            GameConfig config = Config();
            var start = new GridPosition(32, 32);
            var world = new WorldMap(config.World, 0, PlainTiles(config), Array.Empty<ResourceNode>(), start);
            var edge = (from x in Enumerable.Range(1, world.Width - 2)
                        from y in Enumerable.Range(1, world.Height - 2)
                        let a = new GridPosition(x, y)
                        let b = new GridPosition(x + 1, y + 1)
                        where world.IsWalkable(a, 1) && world.IsWalkable(b, 1) &&
                            (!world.IsUnlocked(new GridPosition(x + 1, y), 1) || !world.IsUnlocked(new GridPosition(x, y + 1), 1))
                        select (a, b)).First();
            Assert.That(Pathfinding.HasLineOfSight(world, new WorldPosition(edge.a), new WorldPosition(edge.b), 1), Is.False);
            PathResult path = Pathfinding.FindPath(world, edge.a, edge.b, 1);
            Assert.That(path.Reachable, Is.True);
            Assert.That(path.Steps.Count, Is.GreaterThan(2));
        }

        [TestCase(1)]
        [TestCase(3)]
        [TestCase(5)]
        public void EightDirectionReachabilityMatchesOriginalCardinalConnectedComponent(int round)
        {
            WorldMap world = WorldGen.Generate(Config(), 482);
            var reached = new HashSet<GridPosition> { world.Start };
            var queue = new Queue<GridPosition>(); queue.Enqueue(world.Start);
            while (queue.Count > 0)
                foreach (GridPosition next in Pathfinding.Neighbours(queue.Dequeue()))
                    if (world.IsWalkable(next, round) && reached.Add(next)) queue.Enqueue(next);
            for (int y = 0; y < world.Height; y += 4)
                for (int x = 0; x < world.Width; x += 4)
                {
                    var target = new GridPosition(x, y);
                    Assert.That(Pathfinding.FindPath(world, world.Start, target, round).Reachable,
                        Is.EqualTo(reached.Contains(target)), target.ToString());
                }
        }

        private static string Signature(WorldMap world)
        {
            var text = new StringBuilder(world.Start.ToString());
            for (int y = 0; y < world.Height; y++)
                for (int x = 0; x < world.Width; x++)
                {
                    WorldTile tile = world.TileAt(new GridPosition(x, y));
                    text.Append('|').Append(tile.BiomeId).Append(tile.IsBridge);
                }
            foreach (ResourceNode node in world.Resources)
                text.Append('|').Append(node.Id).Append(':').Append(node.MaterialId).Append(':').Append(node.BiomeId).Append(':').Append(node.Position);
            return text.ToString();
        }
    }
}
