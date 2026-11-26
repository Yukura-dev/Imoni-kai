using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace Imoni.Game.World
{
    /// <summary>One map-local border-box model for measuring, placing and reserving world labels.</summary>
    internal static class WorldAnnotationLayout
    {
        internal static Vector2 Measure(Label label)
        {
            Vector2 text = label.MeasureTextSize(label.text, 0, VisualElement.MeasureMode.Undefined,
                0, VisualElement.MeasureMode.Undefined);
            IResolvedStyle style = label.resolvedStyle;
            return new Vector2(Mathf.Ceil(text.x + style.paddingLeft + style.paddingRight + style.borderLeftWidth + style.borderRightWidth),
                Mathf.Ceil(text.y + style.paddingTop + style.paddingBottom + style.borderTopWidth + style.borderBottomWidth));
        }

        internal static bool Valid(Vector2 size) => FinitePositive(size.x) && FinitePositive(size.y);
        private static bool FinitePositive(float value) => value > 0 && !float.IsNaN(value) && !float.IsInfinity(value);

        internal static void Place(VisualElement label, Rect? bounds)
        {
            label.style.visibility = bounds.HasValue ? Visibility.Visible : Visibility.Hidden;
            if (!bounds.HasValue) return;
            // Avoid mixing an estimated top-left box with a later %-translated actual label.
            Rect rectangle = bounds.Value;
            label.style.translate = new Translate(0, 0);
            label.style.left = rectangle.x; label.style.top = rectangle.y;
            label.style.width = rectangle.width; label.style.height = rectangle.height;
        }

        internal static bool Fits(Rect rectangle, Rect map, IReadOnlyList<Rect> occupied, float gap)
        {
            if (!Valid(rectangle.size) || rectangle.xMin < map.xMin || rectangle.yMin < map.yMin
                || rectangle.xMax > map.xMax || rectangle.yMax > map.yMax) return false;
            foreach (Rect blocker in occupied)
            {
                Rect padded = new Rect(blocker.x - gap, blocker.y - gap, blocker.width + gap * 2, blocker.height + gap * 2);
                if (padded.Overlaps(rectangle)) return false;
            }
            return true;
        }

        internal static Rect? Nearest(Rect preferred, Rect map, IReadOnlyList<Rect> occupied, float gap)
        {
            if (!Valid(preferred.size) || preferred.width > map.width || preferred.height > map.height) return null;
            Rect? best = null;
            float bestDistance = float.MaxValue;
            void Consider(float x, float y)
            {
                var candidate = new Rect(Mathf.Clamp(x, map.xMin, map.xMax - preferred.width),
                    Mathf.Clamp(y, map.yMin, map.yMax - preferred.height), preferred.width, preferred.height);
                float distance = (candidate.position - preferred.position).sqrMagnitude;
                if (distance >= bestDistance || !Fits(candidate, map, occupied, gap)) return;
                best = candidate; bestDistance = distance;
            }
            Consider(preferred.x, preferred.y);
            if (bestDistance == 0) return best;
            // Candidate edges come from the live occupied boxes, not a fixed number of pixel lanes.
            foreach (Rect blocker in occupied)
            {
                float left = blocker.xMin - gap - preferred.width, right = blocker.xMax + gap;
                float above = blocker.yMin - gap - preferred.height, below = blocker.yMax + gap;
                Consider(preferred.x, above); Consider(preferred.x, below);
                Consider(left, preferred.y); Consider(right, preferred.y);
                Consider(left, above); Consider(right, above); Consider(left, below); Consider(right, below);
            }
            return best;
        }
    }
}
