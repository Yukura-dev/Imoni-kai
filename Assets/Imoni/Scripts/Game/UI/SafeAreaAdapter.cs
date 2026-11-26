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
        private readonly VisualElement topCover, bottomCover, leftCover, rightCover;
        private readonly IVisualElementScheduledItem poll;
        public int PaddingWriteCount { get; private set; }
        private sealed class DesignPadding { public float Top, Bottom; }
        private readonly ConditionalWeakTable<VisualElement, DesignPadding> designPadding = new ConditionalWeakTable<VisualElement, DesignPadding>();
        public SafeAreaAdapter(VisualElement root, VisualElement covers = null)
        {
            this.root = root ?? throw new ArgumentNullException(nameof(root));
            topCover = covers?.Q("SafeTopCover"); bottomCover = covers?.Q("SafeBottomCover");
            leftCover = covers?.Q("SafeLeftCover"); rightCover = covers?.Q("SafeRightCover");
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
            if (!Finite(left) || !Finite(top) || !Finite(right) || !Finite(bottom)) return;
            left = Mathf.Max(0, left); top = Mathf.Max(0, top); right = Mathf.Max(0, right); bottom = Mathf.Max(0, bottom);
            bool hasInsets = left > 0 || top > 0 || right > 0 || bottom > 0;
            if (root.ClassListContains("has-safe-insets") != hasInsets) root.EnableInClassList("has-safe-insets", hasInsets);
            if (!Same(root.style.paddingLeft, left)) { root.style.paddingLeft = left; PaddingWriteCount++; }
            if (!Same(root.style.paddingTop, top)) { root.style.paddingTop = top; PaddingWriteCount++; }
            if (!Same(root.style.paddingRight, right)) { root.style.paddingRight = right; PaddingWriteCount++; }
            if (!Same(root.style.paddingBottom, bottom)) { root.style.paddingBottom = bottom; PaddingWriteCount++; }
            if (topCover != null && !Same(topCover.style.height, top)) topCover.style.height = top;
            if (bottomCover != null && !Same(bottomCover.style.height, bottom)) bottomCover.style.height = bottom;
            if (leftCover != null && !Same(leftCover.style.width, left)) leftCover.style.width = left;
            if (rightCover != null && !Same(rightCover.style.width, right)) rightCover.style.width = right;
            // These pages already reserve top/bottom space in the 390x844 mock.
            // Consume that decorative space before compressing their content on a notched phone.
            root.Query<VisualElement>(className: "safe-design-padding").ForEach(page =>
            {
                if (top <= 0 && bottom <= 0)
                {
                    if (page.style.paddingTop.keyword != StyleKeyword.Null) { page.style.paddingTop = StyleKeyword.Null; PaddingWriteCount++; }
                    if (page.style.paddingBottom.keyword != StyleKeyword.Null) { page.style.paddingBottom = StyleKeyword.Null; PaddingWriteCount++; }
                    return;
                }
                if (!designPadding.TryGetValue(page, out DesignPadding baseline))
                {
                    if (page.layout.height <= 0 || page.resolvedStyle.paddingTop <= 0) return;
                    baseline = new DesignPadding { Top = page.resolvedStyle.paddingTop, Bottom = page.resolvedStyle.paddingBottom };
                    designPadding.Add(page, baseline);
                }
                float pageTop = Mathf.Max(0, baseline.Top - top), pageBottom = Mathf.Max(0, baseline.Bottom - bottom);
                if (!Same(page.style.paddingTop, pageTop)) { page.style.paddingTop = pageTop; PaddingWriteCount++; }
                if (!Same(page.style.paddingBottom, pageBottom)) { page.style.paddingBottom = pageBottom; PaddingWriteCount++; }
            });
        }
        private static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
        private static bool Same(StyleLength current, float value) => current.keyword == StyleKeyword.Undefined
            && current.value.unit == LengthUnit.Pixel && Mathf.Approximately(current.value.value, value);
        public void Dispose() { poll.Pause(); root.UnregisterCallback<GeometryChangedEvent>(OnGeometry); }
    }
}
