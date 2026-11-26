using System;
using System.Collections.Generic;
using System.Linq;

namespace Imoni.Core
{
    /// <summary>Continuous tile-centre coordinates, independent of any renderer or frame rate.</summary>
    public readonly struct WorldPosition : IEquatable<WorldPosition>
    {
        public decimal X { get; }
        public decimal Y { get; }
        public GridPosition Cell => new GridPosition((int)Math.Floor(X + 0.5m), (int)Math.Floor(Y + 0.5m));
        public WorldPosition(decimal x, decimal y) { X = x; Y = y; }
        public WorldPosition(GridPosition cell) : this(cell.X, cell.Y) { }
        public decimal DistanceTo(WorldPosition other)
        {
            decimal x = X - other.X, y = Y - other.Y;
            return (decimal)Math.Sqrt((double)(x * x + y * y));
        }
        public static WorldPosition Lerp(WorldPosition from, WorldPosition to, decimal progress) =>
            new WorldPosition(from.X + (to.X - from.X) * progress, from.Y + (to.Y - from.Y) * progress);
        public bool Equals(WorldPosition other) => X == other.X && Y == other.Y;
        public override bool Equals(object obj) => obj is WorldPosition other && Equals(other);
        public override int GetHashCode() { unchecked { return (X.GetHashCode() * 397) ^ Y.GetHashCode(); } }
        public static bool operator ==(WorldPosition left, WorldPosition right) => left.Equals(right);
        public static bool operator !=(WorldPosition left, WorldPosition right) => !left.Equals(right);
    }

    public readonly struct GridPosition : IEquatable<GridPosition>
    {
        public int X { get; }
        public int Y { get; }
        public GridPosition(int x, int y) { X = x; Y = y; }
        public bool Equals(GridPosition other) => X == other.X && Y == other.Y;
        public override bool Equals(object obj) => obj is GridPosition other && Equals(other);
        public override int GetHashCode() { unchecked { return (X * 397) ^ Y; } }
        public static bool operator ==(GridPosition left, GridPosition right) => left.Equals(right);
        public static bool operator !=(GridPosition left, GridPosition right) => !left.Equals(right);
        public int ManhattanDistance(GridPosition other) => Math.Abs(X - other.X) + Math.Abs(Y - other.Y);
        public override string ToString() => X + "," + Y;
    }

    public sealed class WorldTile
    {
        public string BiomeId { get; }
        public bool IsWater => BiomeId == "river";
        public bool IsBridge { get; }
        public bool IsWalkable => !IsWater || IsBridge;
        public WorldTile(string biomeId, bool isBridge = false)
        {
            if (!new[] { "plain", "forest", "river", "mountain", "swamp" }.Contains(biomeId))
                throw new ArgumentException("Unknown tile biome.", nameof(biomeId));
            if (isBridge && biomeId != "river") throw new ArgumentException("Bridges belong to river tiles.");
            BiomeId = biomeId; IsBridge = isBridge;
        }
    }

    public sealed class WorldChunk
    {
        public int X { get; }
        public int Y { get; }
        public IReadOnlyList<ResourceNode> Resources { get; }
        internal WorldChunk(int x, int y, IEnumerable<ResourceNode> resources)
        { X = x; Y = y; Resources = Array.AsReadOnly(resources.ToArray()); }
    }

    /// <summary>Immutable terrain and resource identities. Resource lifecycle mutations are server-internal.</summary>
    public sealed class WorldMap
    {
        private readonly WorldTile[,] tiles;
        private readonly Dictionary<string, ResourceNode> resourcesById;
        public WorldConfig Settings { get; }
        public int Width => tiles.GetLength(0);
        public int Height => tiles.GetLength(1);
        public int ChunkSize => Settings.ChunkSize;
        public int Seed { get; }
        public GridPosition Start { get; }
        public IReadOnlyList<ResourceNode> Resources { get; }
        public IReadOnlyList<WorldChunk> Chunks { get; }

        public WorldMap(WorldConfig settings, int seed, WorldTile[,] tiles,
            IEnumerable<ResourceNode> resources, GridPosition start)
        {
            Settings = settings ?? throw new ArgumentNullException(nameof(settings));
            this.tiles = (WorldTile[,])(tiles ?? throw new ArgumentNullException(nameof(tiles))).Clone();
            if (Width != settings.Width || Height != settings.Height || this.tiles.Cast<WorldTile>().Any(tile => tile == null))
                throw new ArgumentException("Tiles must cover the configured world dimensions.", nameof(tiles));
            Seed = seed; Start = start;
            if (!Contains(start) || !TileAt(start).IsWalkable) throw new ArgumentException("Start must be on land or a bridge.");
            Resources = Array.AsReadOnly((resources ?? throw new ArgumentNullException(nameof(resources))).ToArray());
            if (Resources.Any(node => node == null || !Contains(node.Position)))
                throw new ArgumentException("Resources must be inside the world.", nameof(resources));
            resourcesById = Resources.ToDictionary(node => node.Id, StringComparer.Ordinal);
            var chunks = new List<WorldChunk>();
            for (int y = 0; y < Height / ChunkSize; y++)
                for (int x = 0; x < Width / ChunkSize; x++)
                    chunks.Add(new WorldChunk(x, y, Resources.Where(node => node.Position.X / ChunkSize == x && node.Position.Y / ChunkSize == y)));
            Chunks = chunks.AsReadOnly();
        }

        public bool Contains(GridPosition position) => position.X >= 0 && position.Y >= 0 && position.X < Width && position.Y < Height;
        public WorldTile TileAt(GridPosition position)
        {
            if (!Contains(position)) throw new ArgumentOutOfRangeException(nameof(position));
            return tiles[position.X, position.Y];
        }
        public bool IsUnlocked(GridPosition position, int round)
        {
            long radius = Settings.UnlockRadius(round);
            if (!Contains(position)) return false;
            long dx = position.X - Start.X, dy = position.Y - Start.Y;
            // Avoid squaring an endless-mode radius that exceeds the map diagonal.
            if (radius >= (long)Width + Height) return true;
            return dx * dx + dy * dy <= radius * radius;
        }
        public bool IsWalkable(GridPosition position, int round) => IsUnlocked(position, round) && TileAt(position).IsWalkable;
        public ResourceNode GetResource(string id) => id != null && resourcesById.TryGetValue(id, out ResourceNode node) ? node : null;
        public IReadOnlyList<ResourceNode> ResourcesAvailable(int round) => Array.AsReadOnly(Resources
            .Where(node => Pathfinding.FindPathToResource(this, Start, node, round).Reachable).ToArray());
    }
}
