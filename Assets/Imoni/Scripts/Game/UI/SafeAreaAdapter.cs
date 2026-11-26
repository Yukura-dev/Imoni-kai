using System;
using System.Runtime.CompilerServices;
using UnityEngine;
using UnityEngine.UIElements;
using Screen = UnityEngine.Device.Screen;

namespace Imoni.Game.UI
{
    /// <summary>Converts bottom-left physical screen insets into top-left UI Toolkit panel units.</summary>
    public sealed class SafeAreaAdapter : IDisposable
    {
        private readonly VisualElement root;
        private readonly IVisualElementScheduledItem poll;
        private sealed class DesignPadding { public float Top, Bottom; }
        private readonly ConditionalWeakTable<VisualElement, DesignPadding> designPadding = new ConditionalWeakTable<VisualElement, DesignPadding>();
        public SafeAreaAdapter(VisualElement root)
        {
            this.root = root ?? throw new ArgumentNullException(nameof(root));
            root.RegisterCallback<GeometryChangedEvent>(OnGeometry);
            poll = root.schedule.Execute(Refresh).Every(Math.Max(1, (long)UiTheme.Number("safeAreaPollMs")));
            Refresh();
        }
        private void OnGeometry(GeometryChangedEvent _) => Refresh();
        public void Refresh()
        {
            if (root.panel == null || Screen.width <= 0 || Screen.height <= 0) return;
            Rect safe = Screen.safeArea;
            Vector2 origin = RuntimePanelUtils.ScreenToPanel(root.panel, Vector2.zero);
            Vector2 leftTop = RuntimePanelUtils.ScreenToPanel(root.panel, new Vector2(safe.xMin, Screen.height - safe.yMax));
            Vector2 rightBottom = RuntimePanelUtils.ScreenToPanel(root.panel, new Vector2(Screen.width - safe.xMax, safe.yMin));
            ApplyInsets(leftTop.x - origin.x, leftTop.y - origin.y, rightBottom.x - origin.x, rightBottom.y - origin.y);
        }
        public void ApplyInsets(float left, float top, float right, float bottom)
        {
            root.EnableInClassList("has-safe-insets", left > 0 || top > 0 || right > 0 || bottom > 0);
            root.style.paddingLeft = Mathf.Max(0, left);
            root.style.paddingTop = Mathf.Max(0, top);
            root.style.paddingRight = Mathf.Max(0, right);
            root.style.paddingBottom = Mathf.Max(0, bottom);
            // These pages already reserve top/bottom space in the 390x844 mock.
            // Consume that decorative space before compressing their content on a notched phone.
            root.Query<VisualElement>(className: "safe-design-padding").ForEach(page =>
            {
                if (top <= 0 && bottom <= 0)
                {
                    page.style.paddingTop = StyleKeyword.Null;
                    page.style.paddingBottom = StyleKeyword.Null;
                    return;
                }
                if (!designPadding.TryGetValue(page, out DesignPadding baseline))
                {
                    if (page.layout.height <= 0 || page.resolvedStyle.paddingTop <= 0) return;
                    baseline = new DesignPadding { Top = page.resolvedStyle.paddingTop, Bottom = page.resolvedStyle.paddingBottom };
                    designPadding.Add(page, baseline);
                }
                page.style.paddingTop = Mathf.Max(0, baseline.Top - Mathf.Max(0, top));
                page.style.paddingBottom = Mathf.Max(0, baseline.Bottom - Mathf.Max(0, bottom));
            });
        }
        public void Dispose() { poll.Pause(); root.UnregisterCallback<GeometryChangedEvent>(OnGeometry); }
    }
}
