using Imoni.Core;
using Imoni.Game.UI;
using UnityEngine;
using UnityEngine.Tilemaps;
using UnityEngine.UIElements;

namespace Imoni.Game.World
{
    public sealed class LockedAreaOverlayView : MonoBehaviour
    {
        [SerializeField] private Tilemap tiles;
        private VisualElement label;
        private Label caption;
        private int round;
        internal Vector2 LabelSize => label == null ? Vector2.zero : new Vector2(
            float.IsNaN(label.resolvedStyle.width) ? 0 : label.resolvedStyle.width,
            float.IsNaN(label.resolvedStyle.height) ? 0 : label.resolvedStyle.height);
        internal void Initialize(WorldMap world, WorldTileSet tileSet, int currentRound, VisualElement overlay)
        {
            label = new VisualElement { name = "LockedAreaLabel", pickingMode = PickingMode.Ignore };
            label.AddToClassList("world-locked-label");
            var icon = UiTheme.Icon("icon_lock", "world-locked-label__icon"); label.Add(icon);
            caption = UiTheme.Label(string.Empty, "world-locked-label__text"); label.Add(caption); overlay.Add(label);
            Rebuild(world, tileSet, currentRound);
        }
        internal void Rebuild(WorldMap world, WorldTileSet tileSet, int currentRound)
        {
            if (round == currentRound) return;
            round = currentRound; tiles.ClearAllTiles();
            for (int y = 0; y < world.Height; y++) for (int x = 0; x < world.Width; x++)
                if (!world.IsUnlocked(new GridPosition(x, y), round)) tiles.SetTile(GameWorldView.Cell(x, y), tileSet.locked);
            caption.text = "R" + (round + 1) + "で探索範囲が拡大";
        }
        internal void PositionOverlay(Vector2 point, bool visible)
        { WorldViewStyle.Visible(label, visible); WorldViewStyle.Position(label, point); }
        internal void Release() { label?.RemoveFromHierarchy(); }
#if UNITY_EDITOR
        public void Configure(Tilemap tilemap) { tiles = tilemap; }
#endif
    }
}
