using Imoni.Core;
using Imoni.Game.UI;
using UnityEngine;
using UnityEngine.UIElements;
using ProgressBar = Imoni.Game.UI.Components.ProgressBar;
using ProgressVariant = Imoni.Game.UI.Components.ProgressVariant;

namespace Imoni.Game.World
{
    /// <summary>The one resource prefab presents all material identities and server-owned growth states.</summary>
    public sealed class ResourceNodeView : MonoBehaviour
    {
        [SerializeField] private SpriteRenderer artwork;
        [SerializeField] private BoxCollider2D tapArea;
        private WorldRuntimeAssets assets;
        private WorldViewStyle style;
        private ResourceArt art;
        private VisualElement anchor;
        private ProgressBar gatherBar;
        private Label growth;
        private Label materialLabel;
        private bool overlayVisible;
        public ResourceSnapshot State { get; private set; }
        public string ResourceId => State.Id;
        internal bool NeedsGrowthLabel => overlayVisible && !State.IsReady;
        internal float GatherBarTopOffset => float.IsNaN(gatherBar.resolvedStyle.top) ? 0 : Mathf.Min(0, gatherBar.resolvedStyle.top);
        internal Vector2 GrowthLabelSize
        {
            get
            {
                Vector2 text = growth.MeasureTextSize(growth.text, 0, VisualElement.MeasureMode.Undefined, 0, VisualElement.MeasureMode.Undefined);
                return new Vector2(text.x + growth.resolvedStyle.paddingLeft + growth.resolvedStyle.paddingRight
                    + growth.resolvedStyle.borderLeftWidth + growth.resolvedStyle.borderRightWidth,
                    text.y + growth.resolvedStyle.paddingTop + growth.resolvedStyle.paddingBottom
                    + growth.resolvedStyle.borderTopWidth + growth.resolvedStyle.borderBottomWidth);
            }
        }

        internal void Initialize(ResourceSnapshot state, WorldRuntimeAssets references, WorldViewStyle settings, VisualElement overlay)
        {
            assets = references; style = settings; art = assets.Resource(state.MaterialId);
            anchor = new VisualElement { name = "Resource_" + state.Id, pickingMode = PickingMode.Ignore };
            anchor.AddToClassList("world-resource");
            anchor.style.width = anchor.style.height = style.Number("resourceTapSizePx");
            gatherBar = new ProgressBar { Variant = ProgressVariant.Thin, FillColor = "#8B2310", pickingMode = PickingMode.Ignore };
            gatherBar.AddToClassList("world-resource__gather"); anchor.Add(gatherBar);
            growth = UiTheme.Label(string.Empty, "world-resource__growth"); growth.name = "Growth_" + state.Id;
            // A separate annotation can avoid character bodies/name tags without moving the resource sprite.
            growth.style.bottom = StyleKeyword.Auto; growth.style.translate = new Translate(0, 0);
            growth.style.left = 0; growth.style.top = 0;
            overlay.Add(growth);
            materialLabel = UiTheme.Label(art.label, "world-resource__material"); anchor.Add(materialLabel);
            overlay.Add(anchor);
            tapArea.size = Vector2.one * style.Units("resourceTapSizePx");
            Present(state, false, false, 0);
        }

        internal void Present(ResourceSnapshot state, bool selected, bool gathering, float progress)
        {
            State = state;
            artwork.sprite = state.GrowthStage == ResourceGrowthStage.Stump ? assets.stump : art.sprite;
            artwork.color = state.GrowthStage == ResourceGrowthStage.Stump ? Color.white : art.tint;
            float scale = state.GrowthStage == ResourceGrowthStage.Stump ? 1 : style.Growth((int)state.GrowthStage);
            WorldViewStyle.Size(artwork, style.Units("resourceVisualWidthPx") * scale, style.Units("resourceVisualHeightPx") * scale);
            artwork.sortingOrder = GameWorldView.SortOrder(state.Position.Y);
            anchor.EnableInClassList("world-resource--selected", selected);
            WorldViewStyle.Visible(gatherBar, gathering && state.IsReady);
            gatherBar.Value = progress;
            growth.text = "成長中 " + Mathf.CeilToInt((float)state.SecondsUntilReady) + "秒";
            WorldViewStyle.Visible(growth, !state.IsReady);
            WorldViewStyle.Visible(materialLabel, state.IsReady && !string.IsNullOrEmpty(art.label));
            tapArea.enabled = state.IsReady;
        }

        internal bool Contains(Vector2 worldPoint)
        {
            Vector2 local = transform.InverseTransformPoint(worldPoint);
            return Mathf.Abs(local.x - tapArea.offset.x) <= tapArea.size.x / 2 && Mathf.Abs(local.y - tapArea.offset.y) <= tapArea.size.y / 2;
        }
        internal void PositionOverlay(Vector2 point, bool visible)
        {
            overlayVisible = visible;
            WorldViewStyle.Visible(anchor, visible);
            WorldViewStyle.Position(anchor, point - Vector2.one * style.Number("resourceTapSizePx") / 2);
            WorldViewStyle.Visible(growth, NeedsGrowthLabel);
        }
        internal void PlaceGrowthLabel(Rect? rectangle)
        {
            growth.style.visibility = rectangle.HasValue ? Visibility.Visible : Visibility.Hidden;
            if (!rectangle.HasValue) return;
            Rect value = rectangle.Value;
            WorldViewStyle.Position(growth, value.position);
            growth.style.width = value.width; growth.style.height = value.height;
        }
        internal void Release() { anchor?.RemoveFromHierarchy(); growth?.RemoveFromHierarchy(); }
#if UNITY_EDITOR
        public void Configure(SpriteRenderer renderer, BoxCollider2D collider) { artwork = renderer; tapArea = collider; }
#endif
    }
}
