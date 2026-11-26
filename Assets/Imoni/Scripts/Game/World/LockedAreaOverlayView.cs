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
        private WorldBoundarySurface boundary;
        internal Vector2 LabelSize => label == null ? Vector2.zero : new Vector2(
            float.IsNaN(label.resolvedStyle.width) ? 0 : label.resolvedStyle.width,
            float.IsNaN(label.resolvedStyle.height) ? 0 : label.resolvedStyle.height);
        internal void Initialize(WorldMap world, WorldRuntimeAssets assets, WorldViewStyle style, int currentRound, VisualElement overlay)
        {
            label = new VisualElement { name = "LockedAreaLabel", pickingMode = PickingMode.Ignore };
            label.AddToClassList("world-locked-label");
            var icon = UiTheme.Icon("icon_lock", "world-locked-label__icon"); label.Add(icon);
            caption = UiTheme.Label(string.Empty, "world-locked-label__text"); label.Add(caption); overlay.Add(label);
            tiles.ClearAllTiles();
            boundary = new WorldBoundarySurface(transform, "Locked Boundary", world, assets.worldBoundaryShader,
                assets.tileSet.locked.sprite.texture, assets.tileSet.locked.color, style.Units("boundaryFeatherPx"), short.MaxValue);
            Rebuild(world, currentRound);
        }
        internal void Rebuild(WorldMap world, int currentRound)
        {
            if (round == currentRound) return;
            round = currentRound;
            boundary.Rebuild(position => !world.IsUnlocked(position, round));
            caption.text = "R" + (round + 1) + "で探索範囲が拡大";
        }
        internal void PositionOverlay(Vector2 point, bool visible)
        { WorldViewStyle.Visible(label, visible); WorldViewStyle.Position(label, point); }
        internal void Release() { label?.RemoveFromHierarchy(); boundary?.Dispose(); boundary = null; }
#if UNITY_EDITOR
        public void Configure(Tilemap tilemap) { tiles = tilemap; }
#endif
    }
}
