using System;
using System.Collections.Generic;
using System.Linq;
using Imoni.Core;
using Imoni.Game.UI;
using Newtonsoft.Json.Linq;
using UnityEngine;
using UnityEngine.Tilemaps;
using UnityEngine.UIElements;
using Object = UnityEngine.Object;

namespace Imoni.Game.World
{
    /// <summary>
    /// Tilemap/Sprite presentation and input adapter. GameScreen is the sole caller of server.Tick.
    /// A map cell is one Unity unit; its on-screen size is read from GameConfig.world.tileSizePx.
    /// </summary>
    public sealed class GameWorldView : IDisposable
    {
        private readonly IGameServer server;
        private readonly Bootstrap boot;
        private readonly Camera camera;
        private readonly VisualElement viewport, overlay, inputRoot;
        private readonly WorldRuntimeAssets assets;
        private readonly WorldViewStyle style;
        private readonly GameObject worldRoot;
        private readonly Dictionary<string, ResourceNodeView> resources = new Dictionary<string, ResourceNodeView>();
        private readonly Dictionary<string, CharacterView> characters = new Dictionary<string, CharacterView>();
        private readonly List<GatherPopupView> popups = new List<GatherPopupView>();
        private readonly Dictionary<string, Rect> growthLabelRects = new Dictionary<string, Rect>();
        private readonly List<Rect> characterAvoidanceRects = new List<Rect>();
        private readonly LockedAreaOverlayView locked;
        private readonly Label campLabel;
        private readonly Vector3 campPosition;
        private readonly Rect originalCameraRect;
        private readonly Vector3 originalCameraPosition;
        private readonly float originalCameraSize;
        private readonly Color originalCameraBackground;
        private readonly CameraClearFlags originalClearFlags;
        private readonly string selfId;
        private long lastOperation;
        private bool disposed;
        private GameSnapshot snapshot;
        public string LastTappedResourceId { get; private set; }
        public GridPosition? LastTappedGround { get; private set; }
        public string SelfId => selfId;
        public IGameServer Server => server;
        public Camera Camera => camera;
        public VisualElement MapViewport => viewport;
        public GameSnapshot Snapshot => snapshot;
        /// <summary>Measured map-local rectangles exposed for visual validation.</summary>
        public IReadOnlyDictionary<string, Rect> GrowthLabelRects => growthLabelRects;
        public IReadOnlyList<Rect> CharacterAvoidanceRects => characterAvoidanceRects;

        public GameWorldView(IGameServer server, Bootstrap boot, VisualElement mapViewport, ProfileData profile)
        {
            this.server = server ?? throw new ArgumentNullException(nameof(server));
            this.boot = boot ?? throw new ArgumentNullException(nameof(boot));
            viewport = mapViewport ?? throw new ArgumentNullException(nameof(mapViewport));
            camera = boot.WorldCamera;
            if (!camera || !boot.WorldGrid || !boot.WorldTilemap) throw new InvalidOperationException("Run Imoni > Setup Project before opening Game.");
            assets = Resources.Load<WorldRuntimeAssets>("World/WorldRuntimeAssets");
            if (!assets || !assets.tileSet || !assets.resourceNode || !assets.character || !assets.gatherPopup || !assets.lockedAreaOverlay)
                throw new InvalidOperationException("Generated world assets are missing. Run Imoni > Setup Project.");
            style = new WorldViewStyle();
            originalCameraRect = camera.rect; originalCameraPosition = camera.transform.position;
            originalCameraSize = camera.orthographicSize; originalCameraBackground = camera.backgroundColor; originalClearFlags = camera.clearFlags;
            camera.orthographic = true; camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = UiTheme.Color("#DCE8C4");
            viewport.pickingMode = PickingMode.Ignore;
            overlay = new VisualElement { name = "WorldAnnotations", pickingMode = PickingMode.Ignore };
            overlay.AddToClassList("world-overlay"); UiTheme.Apply(overlay, "UI/WorldOverlay");
            viewport.Insert(0, overlay);
            worldRoot = new GameObject("Imoni World Runtime"); worldRoot.transform.SetParent(boot.WorldGrid.transform, false);
            snapshot = server.Snapshot(); selfId = snapshot.Players.First(player => !player.IsBot).Id;
            lastOperation = snapshot.History.Count == 0 ? 0 : snapshot.History.Max(record => record.Sequence);
            PaintTerrain();
            foreach (ResourceSnapshot state in snapshot.Resources)
            {
                ResourceNodeView view = Object.Instantiate(assets.resourceNode, worldRoot.transform);
                view.name = "Resource " + state.Id; view.transform.position = Position(state.Position);
                view.Initialize(state, assets, style, overlay); resources.Add(state.Id, view);
            }
            for (int index = 0; index < snapshot.Players.Count; index++)
            {
                PlayerSnapshot player = snapshot.Players[index]; bool self = player.Id == selfId;
                CharacterView view = Object.Instantiate(assets.character, worldRoot.transform);
                view.name = "Character " + player.Id;
                view.Initialize(player.Id, self ? profile.Name : index == 1 ? "たろう" : "はなこ", self,
                    self ? profile.OutfitColor : index == 1 ? "#C9A27A" : "#6B9B3A", self ? profile.BandColor : "#8B2310", assets, style, overlay);
                characters.Add(player.Id, view);
            }
            locked = Object.Instantiate(assets.lockedAreaOverlay, worldRoot.transform);
            locked.Initialize(server.World, assets.tileSet, snapshot.RoundNumber, overlay);
            campPosition = Position(server.World.Start) + Vector3.up * style.Units("campHeightPx") / 2;
            var campObject = new GameObject("Starting Camp"); campObject.transform.SetParent(worldRoot.transform, false); campObject.transform.position = campPosition;
            int campOrder = SortOrder(server.World.Start.Y) - 3;
            AddCampLayer(campObject.transform, "Outline", assets.circle, "#C9A27A", style.Units("campWidthPx"), style.Units("campHeightPx"), campOrder);
            AddCampLayer(campObject.transform, "Clearing", assets.circle, "#E8F0D3",
                style.Units("campWidthPx") - style.Units("campBorderPx") * 2, style.Units("campHeightPx") - style.Units("campBorderPx") * 2, campOrder + 1);
            var campArtwork = new GameObject("Artwork"); campArtwork.transform.SetParent(campObject.transform, false);
            var campRenderer = campArtwork.AddComponent<SpriteRenderer>(); campRenderer.sprite = assets.camp;
            campRenderer.sortingOrder = campOrder + 2;
            WorldViewStyle.Size(campRenderer, style.Units("campPotWidthPx"), style.Units("campPotHeightPx"));
            campLabel = UiTheme.Label("拠点", "world-camp-label"); overlay.Add(campLabel);
            inputRoot = boot.UiDocument.rootVisualElement;
            inputRoot.RegisterCallback<PointerDownEvent>(OnPointerDown);
            Present(snapshot, 0);
        }

        public void Present(GameSnapshot state, float deltaSeconds)
        {
            if (disposed) return;
            snapshot = state ?? throw new ArgumentNullException(nameof(state));
            // M5 owns the time-up/result sequence. Retain the final visible world until that sequence exists.
            if (snapshot.Phase != RoundPhase.Collecting) return;
            // AttachToPanel precedes the first layout. Defer world projection until real bounds exist.
            if (!ResizeCamera()) return;
            foreach (PlayerSnapshot player in snapshot.Players)
            {
                Vector3 position = Interpolated(player);
                Vector3? target = player.TargetResourceId != null && resources.TryGetValue(player.TargetResourceId, out ResourceNodeView resource)
                    ? resource.transform.position : (Vector3?)null;
                // Core stops on an adjacent tile. This small visual offset preserves the close, non-overlapping approach.
                if (player.Action == PlayerAction.Gathering && target.HasValue)
                    position += (target.Value - position).normalized * style.Units("characterStopOffsetPx");
                characters[player.Id].Present(player, position, target, deltaSeconds);
            }
            Vector3 focus = characters[selfId].DisplayPosition;
            camera.transform.position = new Vector3(focus.x, focus.y, originalCameraPosition.z);
            foreach (ResourceSnapshot resource in snapshot.Resources)
            {
                ResourceNodeView view = resources[resource.Id];
                PlayerSnapshot gatherer = snapshot.Players.FirstOrDefault(player => player.TargetResourceId == resource.Id && player.Action == PlayerAction.Gathering);
                bool selected = snapshot.Players.Any(player => player.Id == selfId && player.TargetResourceId == resource.Id);
                float progress = gatherer == null || gatherer.ActionDurationSeconds <= 0 ? 0 : 1 - (float)(gatherer.ActionRemainingSeconds / gatherer.ActionDurationSeconds);
                view.Present(resource, selected, gatherer != null, progress);
                bool unlocked = server.World.IsUnlocked(resource.Position, snapshot.RoundNumber);
                view.gameObject.SetActive(unlocked);
                Vector2 point = LocalPoint(view.transform.position);
                view.PositionOverlay(point, unlocked && OnMap(point, style.Number("resourceTapSizePx")));
            }
            foreach (CharacterView character in characters.Values)
            {
                Vector2 point = LocalPoint(character.DisplayPosition);
                character.PositionOverlay(point, OnMap(point, style.Number("playerHeightPx")));
            }
            ArrangeGrowthLabels();
            locked.Rebuild(server.World, assets.tileSet, snapshot.RoundNumber);
            PositionLockLabel();
            Vector2 campPoint = LocalPoint(campPosition);
            WorldViewStyle.Position(campLabel, campPoint + new Vector2(style.Number("campLabelOffsetXPx"), style.Number("campHeightPx") / 2 + style.Number("labelOffsetPx")));
            WorldViewStyle.Visible(campLabel, OnMap(campPoint, style.Number("campHeightPx")));
            ShowGatherResults();
            foreach (GatherPopupView popup in popups) if (popup.Active) popup.Present(deltaSeconds, LocalPoint(popup.transform.position));
        }

        public bool TryTapResource(string resourceId)
        {
            if (disposed || !server.TrySelectResource(selfId, resourceId)) return false;
            LastTappedResourceId = resourceId; LastTappedGround = null; return true;
        }

        /// <summary>Uses the same coordinate conversion and target picking as actual PointerDown events.</summary>
        public bool TryTapPanelPoint(Vector2 panelPoint)
        {
            if (disposed || !viewport.worldBound.Contains(panelPoint)) return false;
            Vector3 worldPoint = PanelToWorld(panelPoint);
            ResourceNodeView selected = resources.Values.Where(view => view.gameObject.activeSelf && view.State.IsReady && view.Contains(worldPoint))
                .OrderBy(view => (view.transform.position - worldPoint).sqrMagnitude).FirstOrDefault();
            if (selected != null) return TryTapResource(selected.ResourceId);
            GridPosition target = new GridPosition(Mathf.FloorToInt(worldPoint.x), Mathf.FloorToInt(-worldPoint.y));
            if (!server.TryMoveTo(selfId, target)) return false;
            LastTappedResourceId = null; LastTappedGround = target; return true;
        }

        public Vector2 ResourcePanelPoint(string resourceId) => WorldToPanel(resources[resourceId].transform.position);
        public Vector2 ActorPanelPoint(string actorId) => WorldToPanel(characters[actorId].DisplayPosition);
        public Vector2 TilePanelPoint(GridPosition tile) => WorldToPanel(Position(tile));
        public bool IsResourceVisible(string resourceId)
        { ResourceNodeView view = resources[resourceId]; return view.gameObject.activeSelf && viewport.worldBound.Contains(ResourcePanelPoint(resourceId)); }

        private void OnPointerDown(PointerDownEvent evt)
        { if (evt.button == 0 && TryTapPanelPoint(evt.position)) evt.StopPropagation(); }

        private void PaintTerrain()
        {
            Tilemap tilemap = boot.WorldTilemap; tilemap.ClearAllTiles();
            tilemap.GetComponent<TilemapRenderer>().sortingOrder = 0;
            for (int y = 0; y < server.World.Height; y++) for (int x = 0; x < server.World.Width; x++)
                tilemap.SetTile(Cell(x, y), assets.tileSet.For(server.World.TileAt(new GridPosition(x, y))));
        }

        private void ArrangeGrowthLabels()
        {
            growthLabelRects.Clear(); characterAvoidanceRects.Clear();
            float gap = style.Number("labelOffsetPx");
            foreach (CharacterView character in characters.Values)
            {
                SpriteRenderer[] renderers = character.GetComponentsInChildren<SpriteRenderer>();
                if (renderers.Length > 0)
                {
                    Bounds body = renderers[0].bounds;
                    foreach (SpriteRenderer renderer in renderers) body.Encapsulate(renderer.bounds);
                    Vector2 first = LocalPoint(body.min), second = LocalPoint(body.max);
                    characterAvoidanceRects.Add(Rect.MinMaxRect(Mathf.Min(first.x, second.x), Mathf.Min(first.y, second.y), Mathf.Max(first.x, second.x), Mathf.Max(first.y, second.y)));
                }
                Label nameTag = overlay.Q<Label>("Actor_" + character.ActorId);
                if (nameTag == null || !FinitePositive(nameTag.resolvedStyle.width) || !FinitePositive(nameTag.resolvedStyle.height)) continue;
                Vector2 anchor = LocalPoint(character.DisplayPosition);
                float height = style.Number(character.ActorId == selfId ? "playerHeightPx" : "teammateHeightPx");
                characterAvoidanceRects.Add(new Rect(anchor.x - nameTag.resolvedStyle.width / 2, anchor.y + height / 2 + gap,
                    nameTag.resolvedStyle.width, nameTag.resolvedStyle.height));
            }
            var occupied = characterAvoidanceRects.Select(rect => Expanded(rect, gap)).ToList();
            // Preserve the active interaction target and its progress bar as well as the characters.
            foreach (string id in snapshot.Players.Select(player => player.TargetResourceId).Where(id => id != null).Distinct())
            {
                Vector2 center = LocalPoint(resources[id].transform.position);
                float size = style.Number("resourceTapSizePx");
                float top = resources[id].GatherBarTopOffset;
                occupied.Add(new Rect(center.x - size / 2 - gap, center.y - size / 2 + top - gap, size + gap * 2, size - top + gap * 2));
            }
            Vector2 self = LocalPoint(characters[selfId].DisplayPosition);
            foreach (ResourceNodeView view in resources.Values.Where(view => view.NeedsGrowthLabel)
                .OrderByDescending(view => view.ResourceId == LastTappedResourceId)
                .ThenBy(view => (LocalPoint(view.transform.position) - self).sqrMagnitude).ThenBy(view => view.ResourceId, StringComparer.Ordinal))
            {
                Vector2 size = view.GrowthLabelSize;
                if (!FinitePositive(size.x) || !FinitePositive(size.y)) { view.PlaceGrowthLabel(null); continue; }
                Vector2 center = LocalPoint(view.transform.position);
                float halfWidth = style.Number("resourceVisualWidthPx") / 2 + gap;
                float halfHeight = style.Number("resourceVisualHeightPx") / 2 + gap;
                Vector2 below = new Vector2(center.x - size.x / 2, center.y + halfHeight);
                Vector2 above = new Vector2(center.x - size.x / 2, center.y - halfHeight - size.y);
                Vector2 right = new Vector2(center.x + halfWidth, center.y - size.y / 2);
                Vector2 left = new Vector2(center.x - halfWidth - size.x, center.y - size.y / 2);
                Vector2[] candidates = { below, above, right, left, new Vector2(right.x, below.y), new Vector2(left.x, below.y), new Vector2(right.x, above.y), new Vector2(left.x, above.y) };
                Rect? chosen = null;
                foreach (Vector2 point in candidates)
                {
                    var rectangle = new Rect(point, size);
                    if (rectangle.xMin < 0 || rectangle.yMin < 0 || rectangle.xMax > viewport.contentRect.width || rectangle.yMax > viewport.contentRect.height
                        || occupied.Any(blocked => blocked.Overlaps(rectangle))) continue;
                    chosen = rectangle; break;
                }
                view.PlaceGrowthLabel(chosen);
                if (chosen.HasValue) { growthLabelRects.Add(view.ResourceId, chosen.Value); occupied.Add(Expanded(chosen.Value, gap)); }
            }
        }
        private static Rect Expanded(Rect rectangle, float padding) => new Rect(rectangle.x - padding, rectangle.y - padding, rectangle.width + padding * 2, rectangle.height + padding * 2);

        private bool ResizeCamera()
        {
            Rect area = viewport.worldBound, full = boot.UiDocument.rootVisualElement.worldBound;
            if (!FinitePositive(area.width) || !FinitePositive(area.height) || !FinitePositive(full.width) || !FinitePositive(full.height)
                || float.IsNaN(area.x) || float.IsNaN(area.y)) return false;
            camera.rect = new Rect((area.x - full.x) / full.width, 1 - (area.yMax - full.y) / full.height, area.width / full.width, area.height / full.height);
            camera.orthographicSize = area.height / (2 * style.TileSize);
            return true;
        }
        private static bool FinitePositive(float value) => value > 0 && !float.IsInfinity(value) && !float.IsNaN(value);

        private void PositionLockLabel()
        {
            Vector3 bottomLeft = PanelToWorld(viewport.worldBound.min + new Vector2(0, viewport.worldBound.height));
            Vector3 topRight = PanelToWorld(viewport.worldBound.min + new Vector2(viewport.worldBound.width, 0));
            Vector2 chosen = default; float best = float.MaxValue; bool found = false;
            for (int y = Mathf.Max(0, Mathf.FloorToInt(-topRight.y)); y <= Mathf.Min(server.World.Height - 1, Mathf.FloorToInt(-bottomLeft.y)); y++)
                for (int x = Mathf.Max(0, Mathf.FloorToInt(bottomLeft.x)); x <= Mathf.Min(server.World.Width - 1, Mathf.FloorToInt(topRight.x)); x++)
                {
                    if (server.World.IsUnlocked(new GridPosition(x, y), snapshot.RoundNumber)) continue;
                    Vector2 point = LocalPoint(Position(new GridPosition(x, y)));
                    if (!OnMap(point, 0)) continue;
                    float distance = (point - viewport.contentRect.max).sqrMagnitude;
                    if (distance < best) { best = distance; chosen = point; found = true; }
                }
            // Keep the boundary explanation readable when only a corner of the circular locked region is visible.
            float inset = locked.LabelSize.x / 2 + style.Number("labelOffsetPx");
            chosen.x = Mathf.Clamp(chosen.x, inset, Mathf.Max(inset, viewport.contentRect.width - inset));
            chosen.y = Mathf.Clamp(chosen.y, locked.LabelSize.y, viewport.contentRect.height - style.Number("labelOffsetPx"));
            locked.PositionOverlay(chosen, found);
        }

        private static void AddCampLayer(Transform parent, string name, Sprite sprite, string tint, float width, float height, int order)
        {
            var child = new GameObject(name); child.transform.SetParent(parent, false);
            var renderer = child.AddComponent<SpriteRenderer>(); renderer.sprite = sprite; renderer.color = UiTheme.Color(tint); renderer.sortingOrder = order;
            WorldViewStyle.Size(renderer, width, height);
        }

        private void ShowGatherResults()
        {
            foreach (OperationRecord record in snapshot.History.Where(record => record.Sequence > lastOperation).OrderBy(record => record.Sequence))
            {
                if (record.Kind != OperationKind.Gather) continue;
                PlayerSnapshot player = snapshot.Players.First(item => item.Id == record.ActorId);
                JToken quality = UiTheme.Data["qualities"].First(item => item["id"].Value<string>() == record.QualityId);
                string text = "+" + Format.Big(record.Quantity, UiTheme.Config) + " " + UiTheme.Config.Material(record.MaterialId).Name
                    + "(" + quality["name"].Value<string>() + ") 連続" + record.GatherStreak;
                GatherPopupView popup = popups.FirstOrDefault(item => !item.Active);
                if (!popup)
                {
                    popup = Object.Instantiate(assets.gatherPopup, worldRoot.transform);
                    popup.Initialize(overlay, style); popups.Add(popup);
                }
                popup.Show(text, quality["bg"].Value<string>(), quality["fg"].Value<string>(), characters[player.Id].DisplayPosition);
            }
            if (snapshot.History.Count > 0) lastOperation = snapshot.History.Max(record => record.Sequence);
        }

        private Vector3 Interpolated(PlayerSnapshot player)
        {
            Vector3 current = Position(player.Position);
            return player.Action != PlayerAction.Moving || player.ActionDurationSeconds <= 0 ? current
                : Vector3.Lerp(current, Position(player.NextPosition), 1 - (float)(player.ActionRemainingSeconds / player.ActionDurationSeconds));
        }
        private Vector2 WorldToPanel(Vector3 world)
        {
            Vector3 point = camera.WorldToViewportPoint(world); Rect area = viewport.worldBound;
            return new Vector2(area.x + point.x * area.width, area.y + (1 - point.y) * area.height);
        }
        private Vector2 LocalPoint(Vector3 world) => viewport.WorldToLocal(WorldToPanel(world));
        private Vector3 PanelToWorld(Vector2 point)
        {
            Rect area = viewport.worldBound;
            if (area.width <= 0 || area.height <= 0) return Position(server.World.Start);
            return camera.ViewportToWorldPoint(new Vector3((point.x - area.x) / area.width, 1 - (point.y - area.y) / area.height, -camera.transform.position.z));
        }
        private bool OnMap(Vector2 point, float margin) => point.x >= -margin && point.y >= -margin && point.x <= viewport.contentRect.width + margin && point.y <= viewport.contentRect.height + margin;
        internal static Vector3Int Cell(int x, int y) => new Vector3Int(x, -y - 1, 0);
        internal static Vector3 Position(GridPosition cell) => new Vector3(cell.X + 0.5f, -cell.Y - 0.5f, 0);
        internal static int SortOrder(float row) => 100 + Mathf.RoundToInt(row * 10);

        public void Dispose()
        {
            if (disposed) return; disposed = true;
            inputRoot.UnregisterCallback<PointerDownEvent>(OnPointerDown);
            foreach (ResourceNodeView view in resources.Values) view.Release();
            foreach (CharacterView view in characters.Values) view.Release();
            foreach (GatherPopupView popup in popups) popup.Release();
            locked.Release(); overlay.RemoveFromHierarchy();
            if (boot.WorldTilemap) boot.WorldTilemap.ClearAllTiles();
            if (worldRoot) Object.Destroy(worldRoot);
            if (camera)
            {
                camera.rect = originalCameraRect; camera.transform.position = originalCameraPosition;
                camera.orthographicSize = originalCameraSize; camera.backgroundColor = originalCameraBackground; camera.clearFlags = originalClearFlags;
            }
        }
    }
}
