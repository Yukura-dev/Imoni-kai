using System;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using Newtonsoft.Json.Linq;
using NUnit.Framework;

namespace Imoni.Core.Tests
{
    public sealed class WorldMapExportTests
    {
        [TestCase(482)]
        [TestCase(777)]
        [TestCase(202647)]
        public void ExportThreeValidationSeeds(int seed)
        {
#if UNITY_EDITOR
            string configPath = Path.Combine(UnityEngine.Application.dataPath, "Imoni", "Config", "GameConfig.json");
#else
            string configPath = Path.Combine(TestContext.CurrentContext.TestDirectory, "GameConfig.json");
#endif
            GameConfig config = GameConfig.Load(configPath);
            WorldMap world = WorldGen.Generate(config, seed);
            GridPosition[] positions = Enumerable.Range(0, world.Height).SelectMany(y => Enumerable.Range(0, world.Width)
                .Select(x => new GridPosition(x, y))).ToArray();
            GridPosition[] firstPositions = positions.Where(point => world.IsUnlocked(point, 1)).ToArray();
            ResourceNode[] firstResources = world.ResourcesAvailable(1).ToArray();
            var statistics = new JObject
            {
                ["seed"] = seed, ["width"] = world.Width, ["height"] = world.Height,
                ["totalResourceNodes"] = world.Resources.Count,
                ["firstRoundTiles"] = firstPositions.Length,
                ["firstRoundResourceNodes"] = firstResources.Length,
                ["biomes"] = new JArray(config.World.BiomeTargets.Select(target => new JObject
                {
                    ["id"] = target.Id, ["targetFraction"] = target.Fraction,
                    ["tiles"] = positions.Count(point => world.TileAt(point).BiomeId == target.Id),
                    ["fraction"] = (decimal)positions.Count(point => world.TileAt(point).BiomeId == target.Id) / positions.Length,
                    ["firstRoundTiles"] = firstPositions.Count(point => world.TileAt(point).BiomeId == target.Id),
                    ["firstRoundFraction"] = (decimal)firstPositions.Count(point => world.TileAt(point).BiomeId == target.Id) / firstPositions.Length
                })),
                ["materials"] = new JArray(config.Materials.Select(material => new JObject
                {
                    ["id"] = material.Id, ["spawnWeight"] = material.SpawnWeight,
                    ["nodes"] = world.Resources.Count(node => node.MaterialId == material.Id),
                    ["firstRoundNodes"] = firstResources.Count(node => node.MaterialId == material.Id)
                }))
            };
            var text = new StringBuilder();
            text.AppendLine("Imoni M3a deterministic world / seed " + seed.ToString(CultureInfo.InvariantCulture));
            text.AppendLine($"Size: {world.Width}x{world.Height}; chunk: {world.ChunkSize}x{world.ChunkSize}; base: {world.Start}");
            text.AppendLine("Coordinates: x increases right; y increases down; each grid has exactly 64 columns and 64 rows.");
            text.AppendLine("Terrain: .=plain F=forest ~=river ^=mountain s=swamp ==bridge @=base");
            text.AppendLine("Resources: W=wood N=negi T=taro M=mushroom I=fish S=sansai");
            text.AppendLine("Unlock: first normal round (1..5), .=outside normal range");
            text.AppendLine();
            AppendGrid(text, "TERRAIN", world, position =>
            {
                if (position.Equals(world.Start)) return '@';
                var tile = world.TileAt(position);
                if (tile.IsBridge) return '=';
                switch (tile.BiomeId)
                {
                    case "plain": return '.';
                    case "forest": return 'F';
                    case "river": return '~';
                    case "mountain": return '^';
                    case "swamp": return 's';
                    default: throw new InvalidOperationException("Unknown biome " + tile.BiomeId);
                }
            });
            var nodes = world.Resources.ToDictionary(node => node.Position);
            AppendGrid(text, "RESOURCES", world, position =>
            {
                if (position.Equals(world.Start)) return '@';
                if (!nodes.TryGetValue(position, out var node)) return '.';
                switch (node.MaterialId)
                {
                    case "wood": return 'W';
                    case "negi": return 'N';
                    case "taro": return 'T';
                    case "mushroom": return 'M';
                    case "fish": return 'I';
                    case "sansai": return 'S';
                    default: throw new InvalidOperationException("Unknown material " + node.MaterialId);
                }
            });
            AppendGrid(text, "UNLOCK RINGS", world, position =>
            {
                if (position.Equals(world.Start)) return '@';
                for (int round = 1; round <= config.RoundsToClear; round++)
                    if (world.IsUnlocked(position, round)) return (char)('0' + round);
                return '.';
            });
            text.AppendLine("COUNTS (reachable harvest position, path wholly inside that round's unlock area)");
            var rounds = new JArray();
            for (int round = 1; round <= config.RoundsToClear; round++)
            {
                var available = world.ResourcesAvailable(round).ToArray();
                text.AppendLine("R" + round + ": " + string.Join(", ", config.Materials.Select(material =>
                    material.Id + "=" + available.Count(node => node.MaterialId == material.Id))));
                foreach (var node in available)
                    Assert.That(Pathfinding.FindPathToResource(world, world.Start, node, round).Reachable, Is.True, node.Id);
                ObjectiveGenerationPlan plan = new ObjectiveGenerator(config).GeneratePlan(world, round, new Rng(unchecked(seed + round)));
                text.AppendLine("  Required: " + string.Join(", ", plan.Objectives.Required.Select(goal => goal.MaterialId + "=" + goal.Amount)) +
                    "; Bonus: " + plan.Objectives.Bonus.MaterialId + "=" + plan.Objectives.Bonus.Amount);
                rounds.Add(new JObject
                {
                    ["round"] = round,
                    ["availableResources"] = new JObject(config.Materials.Select(material =>
                        new JProperty(material.Id, available.Count(node => node.MaterialId == material.Id)))),
                    ["required"] = new JArray(plan.Objectives.Required.Select(goal => GoalJson(goal, plan))),
                    ["bonus"] = GoalJson(plan.Objectives.Bonus, plan)
                });
            }
            statistics["rounds"] = rounds;
            statistics["objectiveProjection"] = "Fresh stock, base-level team at the starting base for each round; same objective seed derivation as LocalGameServer. Live later-round quantities also reflect upgrades and current resources.";
            text.AppendLine("Goals are base-level team projections with fresh stock for each round; live later-round upgrades and stock also affect quantities.");
            text.AppendLine();
            text.AppendLine("BIOME AREAS (bridges count as river; final terrain after starting-supply repairs)");
            foreach (JObject biome in (JArray)statistics["biomes"])
                text.AppendLine(string.Format(CultureInfo.InvariantCulture, "{0}: {1} tiles ({2:P2}), target {3:P2}; R1 {4} tiles ({5:P2})",
                    (string)biome["id"], (int)biome["tiles"], (decimal)biome["fraction"], (decimal)biome["targetFraction"],
                    (int)biome["firstRoundTiles"], (decimal)biome["firstRoundFraction"]));
            text.AppendLine("MATERIAL TOTALS (global weight budget; minimum supplies take precedence in sparse maps)");
            foreach (JObject material in (JArray)statistics["materials"])
                text.AppendLine(material["id"] + ": " + material["nodes"] + " nodes; weight " + material["spawnWeight"] + "; R1 " + material["firstRoundNodes"]);
            foreach (var node in world.Resources)
                Assert.That(Pathfinding.FindPathToResource(world, world.Start, node, config.RoundsToClear).Reachable,
                    Is.True, "Unreachable resource in export: " + node.Id);
            string outputDirectory = Environment.GetEnvironmentVariable("IMONI_MAP_EXPORT_DIR");
            if (!string.IsNullOrWhiteSpace(outputDirectory))
            {
                Directory.CreateDirectory(outputDirectory);
                File.WriteAllText(Path.Combine(outputDirectory, "map_seed_" + seed + ".txt"),
                    text.ToString().Replace("\r\n", "\n"), new UTF8Encoding(false));
                File.WriteAllText(Path.Combine(outputDirectory, "map_seed_" + seed + ".json"),
                    statistics.ToString().Replace("\r\n", "\n") + "\n", new UTF8Encoding(false));
            }
        }

        private static JObject GoalJson(Objective goal, ObjectiveGenerationPlan plan) => new JObject
        {
            ["material"] = goal.MaterialId,
            ["quantity"] = goal.Amount.ToString(CultureInfo.InvariantCulture),
            ["simulatedCapacity"] = plan.SimulatedCapacities[goal.MaterialId].ToString(CultureInfo.InvariantCulture)
        };

        private static void AppendGrid(StringBuilder output, string title, WorldMap world,
            Func<GridPosition, char> symbol)
        {
            output.AppendLine(title);
            for (int y = 0; y < world.Height; y++)
            {
                var line = new StringBuilder();
                for (int x = 0; x < world.Width; x++) line.Append(symbol(new GridPosition(x, y)));
                Assert.That(line.Length, Is.EqualTo(world.Width));
                output.AppendLine(line.ToString());
            }
            output.AppendLine();
        }
    }
}
