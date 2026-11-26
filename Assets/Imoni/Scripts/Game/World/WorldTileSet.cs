using Imoni.Core;
using UnityEngine;
using UnityEngine.Tilemaps;

namespace Imoni.Game.World
{
    public sealed class WorldTileSet : ScriptableObject
    {
        public Tile plain, forest, water, bridge, field, mountain, swamp, locked;
        public Tile For(WorldTile tile) => tile.IsBridge ? bridge : tile.BiomeId switch
        {
            "forest" => forest, "river" => water, "mountain" => mountain, "swamp" => swamp, _ => plain
        };
    }
}
