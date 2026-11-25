using System;
using System.Collections.Generic;
using System.Linq;

namespace Imoni.Core
{
    public static class WorldGen
    {
        public static WorldMap Generate(GameConfig config, int seed)
        {
            if (config == null) throw new ArgumentNullException(nameof(config));
            WorldConfig settings = config.World;
            var rng = new Rng(seed);
            var start = new GridPosition(settings.Width / 2, settings.Height / 2);
            WorldTile[,] tiles = GenerateTerrain(settings, rng, start);

            var resources = new List<ResourceNode>();
            var occupied = new HashSet<GridPosition>();
            WorldMap terrain = new WorldMap(settings, seed, tiles, resources, start);
            // R1 supplies enough distinct types for the generated required/bonus set. Other types
            // are guaranteed somewhere in the world and become eligible when their route unlocks.
            foreach (string id in settings.StartingMaterials) config.Material(id);
            foreach (MaterialDefinition material in config.Materials)
            {
                int round = settings.StartingMaterials.Contains(material.Id) ? 1 : int.MaxValue;
                int minimum = config.ObjectiveGeneration.FirstRoundRequiredMaterials.Contains(material.Id)
                    ? Math.Max(settings.MinimumStartingRequiredNodes, settings.MinimumNodesPerMaterial)
                    : settings.MinimumNodesPerMaterial;
                int present = resources.Count(node => node.MaterialId == material.Id &&
                    Pathfinding.FindPathToResource(terrain, start, node, round).Reachable);
                if (present >= minimum) continue;
                List<GridPosition> candidates = Positions(settings).Where(point => !occupied.Contains(point) &&
                    !InClearing(point, start, settings.StartClearRadius) && !tiles[point.X, point.Y].IsBridge &&
                    terrain.IsUnlocked(point, round)).ToList();
                Shuffle(candidates, rng);
                foreach (GridPosition point in candidates)
                {
                    string biome = tiles[point.X, point.Y].BiomeId;
                    if (!material.Biomes.Contains(biome)) continue;
                    ResourceNode node = CreateNode(config, material, tiles, point);
                    if (!Pathfinding.FindPathToResource(terrain, start, node, round).Reachable) continue;
                    resources.Add(node); occupied.Add(point);
                    if (++present >= minimum) break;
                }
                if (present >= minimum) continue;

                // Repair a sparse land biome locally without violating its material table.
                string landBiome = material.Biomes.FirstOrDefault(biome => biome != "river");
                if (landBiome != null)
                    foreach (GridPosition point in candidates.OrderBy(point => tiles[point.X, point.Y].BiomeId == "plain"))
                    {
                        if (occupied.Contains(point) || tiles[point.X, point.Y].IsWater ||
                            !Pathfinding.FindPath(terrain, start, point, round).Reachable) continue;
                        tiles[point.X, point.Y] = new WorldTile(landBiome);
                        terrain = new WorldMap(settings, seed, tiles, resources, start);
                        ResourceNode node = CreateNode(config, material, tiles, point);
                        if (!Pathfinding.FindPathToResource(terrain, start, node, round).Reachable) continue;
                        resources.Add(node); occupied.Add(point);
                        if (++present >= minimum) break;
                    }
                if (present < minimum)
                    throw new ArgumentException("World configuration cannot provide reachable " + material.Id + " nodes.");
            }

            // Allocate global material budgets before selecting eligible positions. Sampling a
            // material independently inside each biome would make a swamp-only crop abundant
            // regardless of its weight. Minimum supply guarantees take precedence in sparse maps.
            int total = (int)decimal.Round(settings.Width * settings.Height * settings.ResourceChance);
            Dictionary<string, int> budgets = AllocateCounts(config.Materials.Select(material =>
                (material.Id, material.SpawnWeight)).ToArray(), total);
            foreach (MaterialDefinition material in config.Materials)
            {
                int present = resources.Count(node => node.MaterialId == material.Id);
                var candidates = Positions(settings).Where(point => !occupied.Contains(point) &&
                    !InClearing(point, start, settings.StartClearRadius) && !tiles[point.X, point.Y].IsBridge &&
                    material.Biomes.Contains(tiles[point.X, point.Y].BiomeId)).ToList();
                Shuffle(candidates, rng);
                foreach (GridPosition point in candidates)
                {
                    if (present >= budgets[material.Id]) break;
                    ResourceNode node = CreateNode(config, material, tiles, point);
                    if (!Pathfinding.FindPathToResource(terrain, start, node, int.MaxValue).Reachable) continue;
                    resources.Add(node); occupied.Add(point); present++;
                }
            }
            return new WorldMap(settings, seed, tiles,
                resources.OrderBy(node => node.Position.Y).ThenBy(node => node.Position.X), start);
        }

        private static WorldTile[,] GenerateTerrain(WorldConfig settings, Rng rng, GridPosition start)
        {
            int columns = (settings.Width + settings.NoiseCellSize - 1) / settings.NoiseCellSize + 1;
            int rows = (settings.Height + settings.NoiseCellSize - 1) / settings.NoiseCellSize + 1;
            var noise = new decimal[columns, rows];
            for (int y = 0; y < rows; y++)
                for (int x = 0; x < columns; x++)
                    noise[x, y] = (decimal)rng.NextBelow(settings.NoiseResolution) / settings.NoiseResolution;
            var tiles = new WorldTile[settings.Width, settings.Height];
            var values = new Dictionary<GridPosition, decimal>();
            foreach (GridPosition point in Positions(settings))
            {
                int cellX = point.X / settings.NoiseCellSize, cellY = point.Y / settings.NoiseCellSize;
                decimal fx = (decimal)(point.X % settings.NoiseCellSize) / settings.NoiseCellSize;
                decimal fy = (decimal)(point.Y % settings.NoiseCellSize) / settings.NoiseCellSize;
                decimal top = noise[cellX, cellY] * (1 - fx) + noise[cellX + 1, cellY] * fx;
                decimal bottom = noise[cellX, cellY + 1] * (1 - fx) + noise[cellX + 1, cellY + 1] * fx;
                decimal value = top * (1 - fy) + bottom * fy;
                values.Add(point, value);
                tiles[point.X, point.Y] = new WorldTile("plain");
            }
            AddRiver(settings, rng, tiles, start);
            GridPosition[] dry = Positions(settings).Where(point => !tiles[point.X, point.Y].IsWater).ToArray();
            BiomeTarget[] landTargets = settings.BiomeTargets.Where(target => target.Id != "river").ToArray();
            Dictionary<string, int> budgets = AllocateCounts(landTargets.Select(target => (target.Id, target.Fraction)).ToArray(), dry.Length);
            var plain = new HashSet<GridPosition>(dry.Where(point => InClearing(point, start, settings.StartClearRadius)));
            long firstRadius = settings.UnlockRadius(1);
            GridPosition[] firstArea = Positions(settings).Where(point => InClearing(point, start, firstRadius)).ToArray();
            int startingPlain = (int)decimal.Ceiling(firstArea.Length * settings.StartingPlainFraction);
            foreach (GridPosition point in dry.Where(point => InClearing(point, start, firstRadius))
                .OrderBy(point => values[point]).ThenBy(point => point.Y).ThenBy(point => point.X))
            {
                if (plain.Count >= startingPlain) break;
                plain.Add(point);
            }
            if (plain.Count < startingPlain || plain.Count > budgets["plain"])
                throw new ArgumentException("Biome fractions cannot provide the configured starting plains.");
            budgets["plain"] -= plain.Count;
            // Quantiles preserve coherent noise contours, unlike independent tile draws, while
            // correcting the central bias of interpolated value-noise thresholds.
            GridPosition[] ordered = dry.Where(point => !plain.Contains(point)).OrderBy(point => values[point])
                .ThenBy(point => point.Y).ThenBy(point => point.X).ToArray();
            int next = 0;
            foreach (BiomeTarget target in landTargets)
                for (int index = 0; index < budgets[target.Id]; index++)
                {
                    GridPosition point = ordered[next++];
                    tiles[point.X, point.Y] = new WorldTile(target.Id);
                }
            return tiles;
        }

        private static Dictionary<string, int> AllocateCounts(IReadOnlyList<(string Id, decimal Weight)> weights, int total)
        {
            decimal sum = weights.Sum(item => item.Weight);
            var exact = weights.ToDictionary(item => item.Id, item => total * item.Weight / sum, StringComparer.Ordinal);
            var counts = exact.ToDictionary(item => item.Key, item => (int)decimal.Floor(item.Value), StringComparer.Ordinal);
            int remaining = total - counts.Values.Sum();
            foreach (string id in exact.OrderByDescending(item => item.Value - counts[item.Key])
                .ThenBy(item => item.Key, StringComparer.Ordinal).Take(remaining).Select(item => item.Key)) counts[id]++;
            return counts;
        }

        private static void AddRiver(WorldConfig settings, Rng rng, WorldTile[,] tiles, GridPosition start)
        {
            int side = rng.NextBelow(2) == 0 ? -1 : 1;
            int axis = start.X + side * settings.RiverOffsetTiles - (side < 0 ? settings.RiverWidth - 1 : 0);
            int offset = 0;
            for (int y = 0; y < settings.Height; y++)
            {
                int previousOffset = offset;
                if (y % settings.RiverBendStep == 0)
                    offset = Math.Max(-settings.RiverMeanderTiles,
                        Math.Min(settings.RiverMeanderTiles, offset + (int)rng.NextBelow(3) - 1));
                // Every crossing spans the entire river, including the crossing nearest the base.
                bool bridge = (y - start.Y) % settings.BridgeSpacing == 0;
                // Include the connecting corner on a bend; even a one-tile river stays
                // orthogonally connected, and a bridge spans the whole widened row.
                for (int x = axis + Math.Min(previousOffset, offset);
                    x < axis + Math.Max(previousOffset, offset) + settings.RiverWidth; x++)
                    tiles[x, y] = new WorldTile("river", bridge);
            }
        }

        private static ResourceNode CreateNode(GameConfig config, MaterialDefinition material, WorldTile[,] tiles, GridPosition point) =>
            new ResourceNode(config, "resource-" + point.X + "-" + point.Y, material.Id, tiles[point.X, point.Y].BiomeId, point);

        private static bool InClearing(GridPosition point, GridPosition start, long radius)
        {
            long dx = point.X - start.X, dy = point.Y - start.Y;
            return dx * dx + dy * dy <= (long)radius * radius;
        }
        private static IEnumerable<GridPosition> Positions(WorldConfig settings)
        {
            for (int y = 0; y < settings.Height; y++)
                for (int x = 0; x < settings.Width; x++) yield return new GridPosition(x, y);
        }
        private static void Shuffle<T>(IList<T> values, Rng rng)
        {
            for (int last = values.Count - 1; last > 0; last--)
            {
                int chosen = (int)rng.NextBelow(last + 1);
                T swap = values[last]; values[last] = values[chosen]; values[chosen] = swap;
            }
        }
    }
}
