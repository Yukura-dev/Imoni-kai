using System;
using System.Collections.Generic;
using System.Linq;

namespace Imoni.Core
{
    public sealed class PathResult
    {
        public bool Reachable { get; }
        /// <summary>Raw eight-neighbour A* cells, including start and destination; empty when unreachable.</summary>
        public IReadOnlyList<GridPosition> Steps { get; }
        /// <summary>Collision-checked straight segments, including the actual continuous start position.</summary>
        public IReadOnlyList<WorldPosition> Waypoints { get; }
        public decimal DistanceTiles { get; }
        internal PathResult(bool reachable, IEnumerable<GridPosition> steps, IEnumerable<WorldPosition> waypoints)
        {
            Reachable = reachable; Steps = Array.AsReadOnly(steps.ToArray());
            Waypoints = Array.AsReadOnly(waypoints.ToArray());
            for (int i = 1; i < Waypoints.Count; i++) DistanceTiles += Waypoints[i - 1].DistanceTo(Waypoints[i]);
        }
        internal static PathResult Unreachable() => new PathResult(false, Array.Empty<GridPosition>(), Array.Empty<WorldPosition>());
    }

    /// <summary>Eight-neighbour A*, octile heuristic and stable ties, followed by supercover line-of-sight smoothing.</summary>
    public static class Pathfinding
    {
        private static readonly decimal DiagonalCost = (decimal)Math.Sqrt(2);
        public static PathResult FindPath(WorldMap world, GridPosition start, GridPosition target, int round)
            => FindPath(world, new WorldPosition(start), target, round);

        public static PathResult FindPath(WorldMap world, WorldPosition start, GridPosition target, int round)
        {
            if (world == null) throw new ArgumentNullException(nameof(world));
            if (!world.IsWalkable(target, round)) return PathResult.Unreachable();
            return Search(world, start, round, point => point == target, point => OctileDistance(point, target));
        }

        /// <summary>Targets must be unlocked, including fish on water. Stop at a reachable neighbouring bank/land tile.</summary>
        public static PathResult FindPathToResource(WorldMap world, GridPosition start, ResourceNode resource, int round)
            => FindPathToResource(world, new WorldPosition(start), resource, round);

        public static PathResult FindPathToResource(WorldMap world, WorldPosition start, ResourceNode resource, int round)
        {
            if (world == null) throw new ArgumentNullException(nameof(world));
            if (resource == null) throw new ArgumentNullException(nameof(resource));
            if (!world.IsUnlocked(resource.Position, round)) return PathResult.Unreachable();
            GridPosition[] goals = Neighbours(resource.Position).Where(point => world.IsWalkable(point, round)).ToArray();
            if (goals.Length == 0) return PathResult.Unreachable();
            return Search(world, start, round, point => point.ManhattanDistance(resource.Position) == 1,
                point => goals.Min(goal => OctileDistance(point, goal)));
        }

        private static PathResult Search(WorldMap world, WorldPosition origin, int round,
            Func<GridPosition, bool> isGoal, Func<GridPosition, decimal> heuristic)
        {
            GridPosition start = origin.Cell;
            if (!world.IsWalkable(start, round)) return PathResult.Unreachable();
            var costs = new Dictionary<GridPosition, decimal> { [start] = 0 };
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
                    return new PathResult(true, path, Smooth(world, origin, path, round));
                }
                foreach (GridPosition next in MovementNeighbours(current.Position))
                {
                    if (!world.IsWalkable(next, round)) continue;
                    bool diagonal = next.X != current.Position.X && next.Y != current.Position.Y;
                    if (diagonal && (!world.IsWalkable(new GridPosition(next.X, current.Position.Y), round) ||
                        !world.IsWalkable(new GridPosition(current.Position.X, next.Y), round))) continue;
                    decimal cost = current.Cost + (diagonal ? DiagonalCost : 1);
                    if (costs.TryGetValue(next, out decimal known) && known <= cost) continue;
                    costs[next] = cost; previous[next] = current.Position;
                    open.Push(new Entry(next, cost, heuristic(next), sequence++));
                }
            }
            return PathResult.Unreachable();
        }

        private static decimal OctileDistance(GridPosition a, GridPosition b)
        {
            int x = Math.Abs(a.X - b.X), y = Math.Abs(a.Y - b.Y);
            return Math.Max(x, y) + (DiagonalCost - 1) * Math.Min(x, y);
        }

        private static IEnumerable<WorldPosition> Smooth(WorldMap world, WorldPosition origin,
            IReadOnlyList<GridPosition> steps, int round)
        {
            var candidates = new List<WorldPosition> { origin };
            foreach (GridPosition step in steps)
            {
                var point = new WorldPosition(step);
                if (point != candidates[candidates.Count - 1]) candidates.Add(point);
            }
            yield return origin;
            for (int current = 0; current + 1 < candidates.Count;)
            {
                int next = candidates.Count - 1;
                while (next > current + 1 && !HasLineOfSight(world, candidates[current], candidates[next], round)) next--;
                yield return candidates[next];
                current = next;
            }
        }

        /// <summary>Checks every touched cell, including both sides of a corner; locked cells also block sight.</summary>
        public static bool HasLineOfSight(WorldMap world, WorldPosition from, WorldPosition to, int round)
        {
            if (world == null) throw new ArgumentNullException(nameof(world));
            GridPosition cell = from.Cell, target = to.Cell;
            if (!TouchesOnlyWalkableCells(world, from, round) || !TouchesOnlyWalkableCells(world, to, round)) return false;
            decimal dx = to.X - from.X, dy = to.Y - from.Y;
            int sx = Math.Sign(dx), sy = Math.Sign(dy);
            decimal ax = Math.Abs(dx), ay = Math.Abs(dy);
            decimal nextX = sx > 0 ? cell.X + 0.5m - from.X : from.X - (cell.X - 0.5m);
            decimal nextY = sy > 0 ? cell.Y + 0.5m - from.Y : from.Y - (cell.Y - 0.5m);
            bool verticalEdge = sx == 0 && from.X == cell.X - 0.5m;
            bool horizontalEdge = sy == 0 && from.Y == cell.Y - 0.5m;
            while (true)
            {
                if (!world.IsWalkable(cell, round) ||
                    (verticalEdge && !world.IsWalkable(new GridPosition(cell.X - 1, cell.Y), round)) ||
                    (horizontalEdge && !world.IsWalkable(new GridPosition(cell.X, cell.Y - 1), round))) return false;
                if (cell == target) return true;
                // Cross products avoid division/epsilon ambiguity when the segment touches a tile corner.
                int crossing = sx == 0 ? 1 : sy == 0 ? -1 : (nextX * ay).CompareTo(nextY * ax);
                if (crossing == 0)
                {
                    if (!world.IsWalkable(new GridPosition(cell.X + sx, cell.Y), round) ||
                        !world.IsWalkable(new GridPosition(cell.X, cell.Y + sy), round)) return false;
                    cell = new GridPosition(cell.X + sx, cell.Y + sy); nextX++; nextY++;
                }
                else if (crossing < 0) { cell = new GridPosition(cell.X + sx, cell.Y); nextX++; }
                else { cell = new GridPosition(cell.X, cell.Y + sy); nextY++; }
            }
        }

        private static bool TouchesOnlyWalkableCells(WorldMap world, WorldPosition point, int round)
        {
            GridPosition cell = point.Cell;
            bool onVerticalEdge = point.X == cell.X - 0.5m;
            bool onHorizontalEdge = point.Y == cell.Y - 0.5m;
            return world.IsWalkable(cell, round) &&
                (!onVerticalEdge || world.IsWalkable(new GridPosition(cell.X - 1, cell.Y), round)) &&
                (!onHorizontalEdge || world.IsWalkable(new GridPosition(cell.X, cell.Y - 1), round)) &&
                (!(onVerticalEdge && onHorizontalEdge) || world.IsWalkable(new GridPosition(cell.X - 1, cell.Y - 1), round));
        }

        private static IEnumerable<GridPosition> MovementNeighbours(GridPosition point)
        {
            foreach (GridPosition cardinal in Neighbours(point)) yield return cardinal;
            yield return new GridPosition(point.X + 1, point.Y - 1);
            yield return new GridPosition(point.X + 1, point.Y + 1);
            yield return new GridPosition(point.X - 1, point.Y + 1);
            yield return new GridPosition(point.X - 1, point.Y - 1);
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
            public decimal Cost { get; }
            public decimal Estimate { get; }
            public long Order { get; }
            public Entry(GridPosition position, decimal cost, decimal estimate, long order)
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
