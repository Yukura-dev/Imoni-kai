using System;
using System.Collections.Generic;
using System.Linq;
using Imoni.Core;
using Imoni.Game.UI;
using Imoni.Game.World;
using Newtonsoft.Json.Linq;
using UnityEngine;
using UnityEngine.UIElements;

namespace Imoni.Editor
{
    /// <summary>Read-only evidence for world annotation placement, including the rendered UI bounds.</summary>
    public static class ImoniM8LabelValidation
    {
        public static JObject Inspect(GameWorldView world)
        {
            if (world == null) throw new ArgumentNullException(nameof(world));
            var placed = new List<(string Kind, string Id, Rect Bounds)>();
            placed.AddRange(world.NameLabelRects.Select(pair => ("name", pair.Key, pair.Value)));
            placed.AddRange(world.GatherPopupRects.Select((rect, index) => ("popup", index.ToString(), rect)));
            placed.AddRange(world.GrowthLabelRects.Select(pair => ("growth", pair.Key, pair.Value)));
            JArray intersections = Intersections(placed);

            // Inspect after a layout/render frame to compare these actual boxes with the reservations.
            var rendered = new List<(string Kind, string Id, Rect Bounds)>();
            var renderedLabels = new JArray();
            foreach (Label label in world.MapViewport.Query<Label>().ToList())
            {
                string kind = label.ClassListContains("world-character__name") ? "name"
                    : label.ClassListContains("world-gather-popup") ? "popup"
                    : label.ClassListContains("world-resource__growth") ? "growth" : null;
                if (kind == null || label.resolvedStyle.display == DisplayStyle.None
                    || label.resolvedStyle.visibility != Visibility.Visible || label.resolvedStyle.opacity <= 0
                    || label.worldBound.width <= 0 || label.worldBound.height <= 0) continue;
                Vector2 first = world.MapViewport.WorldToLocal(label.worldBound.min);
                Vector2 second = world.MapViewport.WorldToLocal(label.worldBound.max);
                Rect rectangle = Rect.MinMaxRect(first.x, first.y, second.x, second.y);
                string id = string.IsNullOrEmpty(label.name) ? "popup-" + rendered.Count : label.name;
                rendered.Add((kind, id, rectangle));
                renderedLabels.Add(new JObject { ["kind"] = kind, ["id"] = id, ["text"] = label.text,
                    ["rect"] = RectValue(rectangle) });
            }

            GameSnapshot state = world.Snapshot;
            float exclusion = UiTheme.Data["ui"]["worldView"]["growthLabelSelfExclusionTiles"].Value<float>();
            float displayDistance = UiTheme.Data["ui"]["worldView"]["growthLabelDistanceTiles"].Value<float>();
            Vector2 selfPanel = world.ActorPanelPoint(world.SelfId);
            var growthStates = new JArray();
            int exclusionViolations = 0;
            foreach (ResourceSnapshot resource in state.Resources.Where(item => !item.IsReady))
            {
                Vector2 point = world.ResourcePanelPoint(resource.Id);
                // Derive the scale from the same camera projection, avoiding assumptions about panel scale or Safe Area.
                Vector2 next = world.TilePanelPoint(new GridPosition(resource.Position.X, resource.Position.Y + 1));
                float tilePixels = Vector2.Distance(point, next);
                float distance = tilePixels > 0 ? Vector2.Distance(selfPanel, point) / tilePixels : float.NaN;
                bool nearSelf = distance <= exclusion;
                bool visible = world.GrowthLabelRects.ContainsKey(resource.Id);
                bool unlocked = world.Server.World.IsUnlocked(resource.Position, state.RoundNumber);
                bool onMap = world.MapViewport.worldBound.Contains(point);
                bool selected = world.LastTappedResourceId == resource.Id
                    || state.Players.Any(player => player.Id == world.SelfId && player.TargetResourceId == resource.Id);
                if (nearSelf && visible) exclusionViolations++;
                growthStates.Add(new JObject { ["id"] = resource.Id, ["material"] = resource.MaterialId,
                    ["x"] = resource.Position.X, ["y"] = resource.Position.Y,
                    ["remainingStock"] = resource.RemainingStock, ["stage"] = resource.GrowthStage.ToString(),
                    ["secondsUntilReady"] = resource.SecondsUntilReady, ["distanceTiles"] = distance,
                    ["unlocked"] = unlocked, ["onMap"] = onMap, ["selected"] = selected,
                    ["nearSelfExcluded"] = nearSelf, ["distanceOrSelectionEligible"] = selected || distance <= displayDistance,
                    ["growthLabelVisible"] = visible, ["selfExclusionRequiresSproutOnly"] = nearSelf });
            }

            JArray renderedIntersections = Intersections(rendered);
            return new JObject {
                ["coordinateSpace"] = "map-local border-box, top-left origin",
                ["elapsed"] = state.ElapsedSeconds, ["selfId"] = world.SelfId,
                ["map"] = RectValue(new Rect(Vector2.zero, world.MapViewport.contentRect.size)),
                ["growthLabelSelfExclusionTiles"] = exclusion, ["growthLabelDistanceTiles"] = displayDistance,
                ["rects"] = new JArray(placed.Select(item => new JObject { ["kind"] = item.Kind,
                    ["id"] = item.Id, ["rect"] = RectValue(item.Bounds) })),
                ["intersectionCount"] = intersections.Count, ["intersections"] = intersections,
                ["renderedRects"] = renderedLabels,
                ["renderedIntersectionCount"] = renderedIntersections.Count, ["renderedIntersections"] = renderedIntersections,
                ["growthStates"] = growthStates, ["exclusionViolationCount"] = exclusionViolations,
                ["activePopupTexts"] = new JArray(world.ActivePopupTexts)
            };
        }

        private static JArray Intersections(IReadOnlyList<(string Kind, string Id, Rect Bounds)> rectangles)
        {
            var result = new JArray();
            for (int first = 0; first < rectangles.Count; first++)
                for (int second = first + 1; second < rectangles.Count; second++)
                    if (rectangles[first].Bounds.Overlaps(rectangles[second].Bounds))
                        result.Add(new JObject {
                            ["first"] = rectangles[first].Kind + ":" + rectangles[first].Id,
                            ["second"] = rectangles[second].Kind + ":" + rectangles[second].Id
                        });
            return result;
        }

        private static JObject RectValue(Rect rectangle) => new JObject {
            ["x"] = rectangle.x, ["y"] = rectangle.y, ["width"] = rectangle.width, ["height"] = rectangle.height
        };
    }
}
