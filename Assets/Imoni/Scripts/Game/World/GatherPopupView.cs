using Imoni.Game.UI;
using UnityEngine;
using UnityEngine.UIElements;

namespace Imoni.Game.World
{
    /// <summary>A pooled prefab owns one UI Toolkit label anchored in world space, without a Canvas.</summary>
    public sealed class GatherPopupView : MonoBehaviour
    {
        private Label label;
        private WorldViewStyle style;
        private float age;
        public bool Active { get; private set; }
        internal void Initialize(VisualElement overlay, WorldViewStyle settings)
        { style = settings; label = UiTheme.Label(string.Empty, "world-gather-popup"); overlay.Add(label); Hide(); }
        internal void Show(string text, string background, string foreground, Vector3 position)
        {
            transform.position = position; age = 0; Active = true;
            label.text = text; label.style.backgroundColor = UiTheme.Color(background); label.style.color = UiTheme.Color(foreground);
            WorldViewStyle.Visible(label, true);
        }
        internal void Present(float deltaSeconds, Vector2 point)
        {
            if (!Active) return;
            age += deltaSeconds;
            float duration = style.Number("popupSeconds");
            if (age >= duration) { Hide(); return; }
            WorldViewStyle.Position(label, point + Vector2.down * (style.Number("popupOffsetYPx") + style.Number("popupRisePx") * age / duration));
        }
        private void Hide() { Active = false; if (label != null) WorldViewStyle.Visible(label, false); }
        internal void Release() { label?.RemoveFromHierarchy(); }
    }
}
