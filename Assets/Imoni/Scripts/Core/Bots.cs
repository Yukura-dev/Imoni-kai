using System;
using System.Linq;

namespace Imoni.Core
{
    /// <summary>Deterministic teammate decisions. All actions use the same server command as a human.</summary>
    public sealed class Bots
    {
        public bool Think(IGameServer server, string actorId)
        {
            if (server == null) throw new ArgumentNullException(nameof(server));
            GameSnapshot state = server.Snapshot();
            PlayerSnapshot player = state.Players.FirstOrDefault(item => item.Id == actorId);
            if (state.Phase != RoundPhase.Collecting || player == null || !player.Connected ||
                player.Action != PlayerAction.Idle) return false;
            ResourceNode best = null;
            int bestDistance = int.MaxValue;
            foreach (ResourceNode node in server.World.Resources
                .OrderBy(item => Distance(player.Position, item.Position)).ThenBy(item => item.Id, StringComparer.Ordinal))
            {
                // Manhattan distance is a lower bound on the path ending one tile before the node.
                if (Distance(player.Position, node.Position) > bestDistance) break;
                if (!node.IsReady) continue;
                PathResult path = Pathfinding.FindPathToResource(server.World, player.Position, node, state.RoundNumber);
                if (!path.Reachable || path.Steps.Count > bestDistance ||
                    (path.Steps.Count == bestDistance && best != null &&
                        StringComparer.Ordinal.Compare(node.Id, best.Id) >= 0)) continue;
                best = node;
                bestDistance = path.Steps.Count;
            }
            return best != null && server.TrySelectResource(actorId, best.Id);
        }

        private static int Distance(GridPosition a, GridPosition b) => Math.Abs(a.X - b.X) + Math.Abs(a.Y - b.Y);
    }
}
