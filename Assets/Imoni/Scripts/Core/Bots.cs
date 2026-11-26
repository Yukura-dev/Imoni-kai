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
            decimal bestDistance = decimal.MaxValue;
            foreach (ResourceNode node in server.World.Resources
                .OrderBy(item => Distance(player.Location, item.Position)).ThenBy(item => item.Id, StringComparer.Ordinal))
            {
                // Euclidean distance minus the one-tile gathering reach is a lower bound for any route.
                if (Distance(player.Location, node.Position) - 1 > bestDistance) break;
                if (!node.IsReady) continue;
                PathResult path = Pathfinding.FindPathToResource(server.World, player.Location, node, state.RoundNumber);
                if (!path.Reachable || path.DistanceTiles > bestDistance ||
                    (path.DistanceTiles == bestDistance && best != null &&
                        StringComparer.Ordinal.Compare(node.Id, best.Id) >= 0)) continue;
                best = node;
                bestDistance = path.DistanceTiles;
            }
            return best != null && server.TrySelectResource(actorId, best.Id);
        }

        private static decimal Distance(WorldPosition a, GridPosition b) => a.DistanceTo(new WorldPosition(b));
    }
}
