using System;
using System.IO;
using System.Linq;
using Imoni.Core;
using Imoni.Game;
using Imoni.Game.UI.Screens;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace Imoni.Editor
{
    /// <summary>Editor-only evidence. Taps use the production picking/conversion path; Tick stays authoritative.</summary>
    [InitializeOnLoad]
    public static class ImoniM8ReviewJob
    {
        private const string Request = "Library/M8Review.request", Result = "Library/M8Review.result";
        private static Bootstrap Boot => UnityEngine.Object.FindFirstObjectByType<Bootstrap>();
        static ImoniM8ReviewJob() { EditorApplication.update += Update; }
        private static void Update()
        {
            if (EditorApplication.isCompiling || EditorApplication.isUpdating || !File.Exists(Request)) return;
            try
            {
                JObject request;
                try { request = JObject.Parse(File.ReadAllText(Request)); File.Delete(Request); }
                catch (IOException) { return; }
                if (!EditorApplication.isPlaying || Boot?.Router == null) throw new InvalidOperationException("Start Boot first.");
                var game = Boot.Router.CurrentView as GameScreen;
                JObject result;
                switch (request.Value<string>("command"))
                {
                    case "tap-ground":
                        if (game == null) throw new InvalidOperationException("Open Game first.");
                        var tile = new GridPosition(request.Value<int>("x"), request.Value<int>("y"));
                        var player = game.Snapshot.Players.First(p => p.Id == game.LocalActorId);
                        var path = Pathfinding.FindPath(game.Server.World, player.Location, tile, game.Snapshot.RoundNumber);
                        Vector2 panel = game.WorldView.TilePanelPoint(tile);
                        bool accepted = game.WorldView.TryTapPanelPoint(panel);
                        game.RefreshSnapshot();
                        result = Audit(); result["tap"] = new JObject { ["x"] = panel.x, ["y"] = panel.y, ["accepted"] = accepted,
                            ["groundTarget"] = game.WorldView.LastTappedGround?.ToString(), ["resourceTarget"] = game.WorldView.LastTappedResourceId,
                            ["distanceTiles"] = path.DistanceTiles, ["rawCells"] = JArray.FromObject(path.Steps), ["waypoints"] = JArray.FromObject(path.Waypoints) };
                        break;
                    case "stable":
                        int padding = Boot.SafeArea.PaddingWriteCount, viewport = game?.WorldView?.ViewportWriteCount ?? 0;
                        int samples = request.Value<int>("samples");
                        for (int i = 0; i < samples; i++) { Boot.SafeArea.Refresh(); game?.RefreshSnapshot(); }
                        result = Audit(); result["stability"] = new JObject { ["samples"] = samples,
                            ["paddingWrites"] = Boot.SafeArea.PaddingWriteCount - padding,
                            ["viewportWrites"] = (game?.WorldView?.ViewportWriteCount ?? 0) - viewport };
                        break;
                    case "audit": result = Audit(); break;
                    default: throw new ArgumentException("Unknown M8 review command.");
                }
                File.WriteAllText(Result, result.ToString());
            }
            catch (Exception e) { File.WriteAllText(Result, new JObject { ["status"] = "failed", ["failure"] = e.ToString() }.ToString()); Debug.LogException(e); }
        }
        private static JObject RectValue(Rect rect) => new JObject { ["x"] = rect.x, ["y"] = rect.y, ["width"] = rect.width, ["height"] = rect.height };
        private static JObject Audit()
        {
            var game = Boot.Router.CurrentView as GameScreen;
            var root = Boot.UiDocument.rootVisualElement;
            var camera = Boot.WorldCamera;
            string[] names = { "ScreenHost", "SafeArea", "ScreenContent", "SafeTopCover", "SafeBottomCover", "SafeLeftCover", "SafeRightCover", "GameHud", "GameActions", "GameMapViewport", "EffectScreen", "Backdrop", "Rainbow", "Flash", "GrayOverlay", "DarkOverlay" };
            var result = new JObject { ["screen"] = Boot.Router.CurrentScreen, ["paddingWrites"] = Boot.SafeArea.PaddingWriteCount,
                ["camera"] = new JObject { ["rect"] = RectValue(camera.rect), ["pixelRect"] = RectValue(camera.pixelRect),
                    ["enabled"] = camera.enabled, ["clearFlags"] = camera.clearFlags.ToString(),
                    ["viewportWrites"] = game?.WorldView?.ViewportWriteCount, ["orthographicSize"] = camera.orthographicSize },
                ["elements"] = new JArray(names.Select(name => root.Q(name)).Where(item => item != null).Select(item => new JObject {
                    ["name"] = item.name, ["bounds"] = RectValue(item.worldBound), ["backgroundAlpha"] = item.resolvedStyle.backgroundColor.a,
                    ["opacity"] = item.resolvedStyle.opacity, ["display"] = item.resolvedStyle.display.ToString() })) };
            if (game != null)
            {
                PlayerSnapshot player = game.Snapshot.Players.First(p => p.Id == game.LocalActorId);
                result["player"] = new JObject { ["x"] = player.Location.X, ["y"] = player.Location.Y,
                    ["action"] = player.Action.ToString(), ["elapsed"] = game.Snapshot.ElapsedSeconds,
                    ["remaining"] = player.ActionRemainingSeconds, ["duration"] = player.ActionDurationSeconds,
                    ["actorPanelX"] = game.WorldView.ActorPanelPoint(player.Id).x, ["actorPanelY"] = game.WorldView.ActorPanelPoint(player.Id).y };
            }
            return result;
        }
    }
}
