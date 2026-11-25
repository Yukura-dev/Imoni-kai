using System;
using System.Collections.Generic;
using System.Linq;

namespace Imoni.Core
{
    public sealed class PathResult
    {
        public bool Reachable { get; }
        /// <summary>Includes the start and destination; empty when unreachable.</summary>
        public IReadOnlyList<GridPosition> Steps { get; }
        internal PathResult(bool reachable, IEnumerable<GridPosition> steps)
        { Reachable = reachable; Steps = Array.AsReadOnly(steps.ToArray()); }
        internal static PathResult Unreachable() => new PathResult(false, Array.Empty<GridPosition>());
    }

    /// <summary>Four-neighbour A*, stable N/E/S/W ties, Manhattan admissible heuristic.</summary>
    public static class Pathfinding
    {
        public static PathResult FindPath(WorldMap world, GridPosition start, GridPosition target, int round)
        {
            if (world == null) throw new ArgumentNullException(nameof(world));
            if (!world.IsWalkable(target, round)) return PathResult.Unreachable();
            return Search(world, start, round, point => point == target, point => point.ManhattanDistance(target));
        }

        /// <summary>Targets must be unlocked, including fish on water. Stop at a reachable neighbouring bank/land tile.</summary>
        public static PathResult FindPathToResource(WorldMap world, GridPosition start, ResourceNode resource, int round)
        {
            if (world == null) throw new ArgumentNullException(nameof(world));
            if (resource == null) throw new ArgumentNullException(nameof(resource));
            if (!world.IsUnlocked(resource.Position, round)) return PathResult.Unreachable();
            return Search(world, start, round, point => point.ManhattanDistance(resource.Position) == 1,
                point => Math.Max(0, point.ManhattanDistance(resource.Position) - 1));
        }

        private static PathResult Search(WorldMap world, GridPosition start, int round,
            Func<GridPosition, bool> isGoal, Func<GridPosition, int> heuristic)
        {
            if (!world.IsWalkable(start, round)) return PathResult.Unreachable();
            var costs = new Dictionary<GridPosition, int> { [start] = 0 };
            var previous = new Dictionary<GridPosition, GridPosition>();
            var open = new MinHeap();
            long sequence = 0;
            open.Push(new Entry(start, 0, heuristic(start), sequence++));
            while (open.Count > 0)
            {
                Entry current = open.Pop();
                if (costs[current.Position] != current.Cost) continue;
                if (isGoal(current.Position))
                {
                    var path = new List<GridPosition> { current.Position };
                    while (previous.TryGetValue(path[path.Count - 1], out GridPosition parent)) path.Add(parent);
                    path.Reverse();
                    return new PathResult(true, path);
                }
                foreach (GridPosition next in Neighbours(current.Position))
                {
                    if (!world.IsWalkable(next, round)) continue;
                    int cost = current.Cost + 1;
                    if (costs.TryGetValue(next, out int known) && known <= cost) continue;
                    costs[next] = cost; previous[next] = current.Position;
                    open.Push(new Entry(next, cost, heuristic(next), sequence++));
                }
            }
            return PathResult.Unreachable();
        }

        internal static IEnumerable<GridPosition> Neighbours(GridPosition point)
        {
            yield return new GridPosition(point.X, point.Y - 1);
            yield return new GridPosition(point.X + 1, point.Y);
            yield return new GridPosition(point.X, point.Y + 1);
            yield return new GridPosition(point.X - 1, point.Y);
        }

        private readonly struct Entry
        {
            public GridPosition Position { get; }
            public int Cost { get; }
            public int Estimate { get; }
            public long Order { get; }
            public Entry(GridPosition position, int cost, int estimate, long order)
            { Position = position; Cost = cost; Estimate = estimate; Order = order; }
            public bool Before(Entry other) => Cost + Estimate != other.Cost + other.Estimate
                ? Cost + Estimate < other.Cost + other.Estimate
                : Estimate != other.Estimate ? Estimate < other.Estimate : Order < other.Order;
        }

        // Unity's .NET profile does not provide PriorityQueue<T>.
        private sealed class MinHeap
        {
            private readonly List<Entry> entries = new List<Entry>();
            public int Count => entries.Count;
            public void Push(Entry item)
            {
                entries.Add(item);
                int child = entries.Count - 1;
                while (child > 0)
                {
                    int parent = (child - 1) / 2;
                    if (!entries[child].Before(entries[parent])) break;
                    Entry swap = entries[parent]; entries[parent] = entries[child]; entries[child] = swap;
                    child = parent;
                }
            }
            public Entry Pop()
            {
                Entry first = entries[0], last = entries[entries.Count - 1];
                entries.RemoveAt(entries.Count - 1);
                if (entries.Count == 0) return first;
                entries[0] = last;
                int parent = 0;
                while (parent * 2 + 1 < entries.Count)
                {
                    int child = parent * 2 + 1;
                    if (child + 1 < entries.Count && entries[child + 1].Before(entries[child])) child++;
                    if (!entries[child].Before(entries[parent])) break;
                    Entry swap = entries[parent]; entries[parent] = entries[child]; entries[child] = swap;
                    parent = child;
                }
                return first;
            }
        }
    }
}
