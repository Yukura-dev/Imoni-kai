using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Imoni.Core;
using Imoni.Game;
using Imoni.Game.UI.Screens;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace Imoni.Editor
{
    /// <summary>Editor-only, bounded inspection and Game-view captures of the real Core session.</summary>
    [InitializeOnLoad]
    public static class ImoniM3BReviewJob
    {
        private const string Request = "Library/M3BReview.request";
        private const string Result = "Library/M3BReview.result";
        private static string capturePath;
        private static int captureFrame;
        private static bool captureRequested;
        private static JObject captureState;
        private static bool armed;
        private static bool sequence;
        private static int sequenceIndex;
        private static int sequenceCount;
        private static double nextShot;
        private static string previousTap;
        private static readonly JArray frames = new JArray();
        private static Bootstrap Boot => UnityEngine.Object.FindFirstObjectByType<Bootstrap>();
        private static GameScreen Game => Boot?.Router?.CurrentView as GameScreen;

        static ImoniM3BReviewJob() { EditorApplication.update += Update; }

        private static void Update()
        {
            if (EditorApplication.isCompiling || EditorApplication.isUpdating) return;
            try
            {
                if (File.Exists(Request) && capturePath == null)
                {
                    JObject request;
                    try { request = JObject.Parse(File.ReadAllText(Request)); File.Delete(Request); }
                    catch (IOException) { return; }
                    Run(request);
                }
                if (capturePath != null) CompleteCapture();
                if (!armed && !sequence) return;
                if (Game == null || !EditorApplication.isPlaying) throw new InvalidOperationException("Game left during sequence capture.");
                if (armed)
                {
                    string tapped = Game.WorldView?.LastTappedResourceId;
                    if (string.IsNullOrEmpty(tapped) || tapped == previousTap) return;
                    armed = false; sequence = true; sequenceIndex = 0;
                    Game.RefreshSnapshot();
                    nextShot = EditorApplication.timeSinceStartup;
                }
                if (capturePath == null && sequence && EditorApplication.timeSinceStartup >= nextShot)
                {
                    if (sequenceIndex > 0)
                    {
                        Game.Server.Tick(1m);
                        Game.RefreshSnapshot(1f);
                    }
                    BeginCapture("sequence_" + sequenceIndex.ToString("00"));
                    nextShot = EditorApplication.timeSinceStartup + 1;
                }
            }
            catch (Exception exception)
            {
                File.WriteAllText(Result, new JObject { ["status"] = "failed", ["failure"] = exception.ToString() }.ToString());
                capturePath = null; armed = false; sequence = false;
                Debug.LogException(exception);
            }
        }

        private static void Run(JObject request)
        {
            string command = request["command"]?.Value<string>();
            if (command == "open")
            {
                if (EditorApplication.isPlaying) throw new InvalidOperationException("Exit Play Mode before opening review.");
                typeof(ImoniM0ReviewJob).GetMethod("ConfigureGameView", BindingFlags.Static | BindingFlags.NonPublic).Invoke(null, null);
                ImoniSetup.SetupProject();
                SessionState.SetString("Imoni.StartScreen", "Loading");
                EditorApplication.isPlaying = true;
                WriteResult("opening Loading"); return;
            }
            if (command == "console") { File.WriteAllText(Result, ConsoleCounts().ToString()); return; }
            if (!EditorApplication.isPlaying || Boot?.Router == null) throw new InvalidOperationException("Start Boot first.");
            if (command == "inspect") { Inspect(); return; }
            if (command == "prepare")
            {
                Boot.Context.PrepareGame(request["seed"]?.Value<int>());
                Boot.Router.Show("Game");
                Game.ReviewPaused = true;
                WriteResult("prepared"); return;
            }
            if (Game == null) throw new InvalidOperationException("Open Game first.");
            switch (command)
            {
                case "pause": Game.ReviewPaused = request["paused"]?.Value<bool>() ?? true; WriteResult("paused changed"); break;
                case "step":
                    Game.ReviewPaused = true;
                    decimal seconds = request["seconds"].Value<decimal>();
                    Game.Server.Tick(seconds); Game.RefreshSnapshot((float)seconds);
                    if (request["name"] != null) BeginCapture(request["name"].Value<string>()); else WriteResult("stepped");
                    break;
                case "disconnect":
                    Game.Server.SetConnected(request["actor"].Value<string>(), request["connected"].Value<bool>());
                    Game.RefreshSnapshot(); WriteResult("connection changed"); break;
                case "capture": Game.ReviewPaused = true; Game.RefreshSnapshot(); BeginCapture(request["name"].Value<string>()); break;
                case "arm":
                    Game.ReviewPaused = true;
                    Game.RefreshSnapshot();
                    frames.Clear(); armed = true; sequence = false;
                    previousTap = Game.WorldView?.LastTappedResourceId;
                    sequenceCount = request["count"]?.Value<int>() ?? 8;
                    if (sequenceCount < 5 || sequenceCount > 8) throw new ArgumentException("Capture between five and eight frames.");
                    WriteResult("armed: tap a resource in the Game view"); break;
                default: throw new ArgumentException("Unknown M3b review command: " + command);
            }
        }

        private static void BeginCapture(string name)
        {
            if (name != Path.GetFileName(name)) throw new ArgumentException("Use a filename without a directory.");
            capturePath = Path.GetFullPath("Library/M3B-" + name + ".png");
            if (File.Exists(capturePath)) File.Delete(capturePath);
            captureFrame = Time.frameCount;
            captureRequested = false;
            captureState = Describe();
            captureState["name"] = name;
            captureState["image"] = capturePath;
            captureState["editorTime"] = EditorApplication.timeSinceStartup;
        }

        private static void CompleteCapture()
        {
            if (!captureRequested)
            {
                if (Time.frameCount < captureFrame + 2) return;
                ScreenCapture.CaptureScreenshot(capturePath);
                captureRequested = true; return;
            }
            if (!File.Exists(capturePath) || new FileInfo(capturePath).Length == 0) return;
            File.WriteAllText(Path.ChangeExtension(capturePath, ".json"), captureState.ToString());
            if (sequence)
            {
                frames.Add(captureState);
                sequenceIndex++;
                if (sequenceIndex >= sequenceCount)
                {
                    sequence = false;
                    bool moved = frames.Any(item => item["players"][0]["action"].Value<string>() == "Moving");
                    bool gathered = frames.Any(item => item["players"][0]["action"].Value<string>() == "Gathering");
                    bool gained = frames.Last["humanGatherCount"].Value<int>() > frames.First["humanGatherCount"].Value<int>();
                    var report = new JObject { ["status"] = "complete", ["intervalSimulationSeconds"] = 1,
                        ["input"] = "Game-view pointer tap; normal IGameServer command; paused between fixed one-second ticks",
                        ["movingObserved"] = moved, ["gatheringObserved"] = gathered, ["humanGatherConfirmed"] = gained,
                        ["frames"] = frames };
                    File.WriteAllText("Library/M3B-sequence.json", report.ToString());
                    File.WriteAllText(Result, report.ToString());
                }
            }
            else File.WriteAllText(Result, new JObject { ["status"] = "complete", ["capture"] = captureState }.ToString());
            capturePath = null;
        }

        private static JObject Describe()
        {
            GameSnapshot state = Game.Server.Snapshot();
            string human = state.Players[0].Id;
            var history = new JArray(state.History.Select(item => new JObject
            {
                ["sequence"] = item.Sequence, ["actor"] = item.ActorId, ["kind"] = item.Kind.ToString(),
                ["material"] = item.MaterialId, ["quality"] = item.QualityId,
                ["quantity"] = item.Quantity.ToString(), ["time"] = item.ElapsedSeconds, ["gatherStreak"] = item.GatherStreak
            }));
            var actors = new JArray(state.Players.Select(item => new JObject
            {
                ["id"] = item.Id, ["x"] = item.Position.X, ["y"] = item.Position.Y,
                ["nextX"] = item.NextPosition.X, ["nextY"] = item.NextPosition.Y,
                ["action"] = item.Action.ToString(), ["connected"] = item.Connected,
                ["target"] = item.TargetResourceId, ["remaining"] = item.ActionRemainingSeconds,
                ["duration"] = item.ActionDurationSeconds, ["streak"] = item.Streak
            }));
            var result = new JObject { ["width"] = Screen.width, ["height"] = Screen.height,
                ["screen"] = Boot.Router.CurrentScreen, ["seed"] = state.Seed,
                ["elapsed"] = state.ElapsedSeconds, ["phase"] = state.Phase.ToString(),
                ["funds"] = state.Funds.ToString(), ["bonusRevealed"] = state.BonusRevealed,
                ["lastTappedResource"] = Game.WorldView?.LastTappedResourceId,
                ["lastTappedGround"] = Game.WorldView?.LastTappedGround?.ToString(),
                ["players"] = actors, ["history"] = history,
                ["humanGatherCount"] = state.History.Count(item => item.ActorId == human && item.Kind == OperationKind.Gather),
                ["lots"] = new JArray(state.Lots.Select(item => new JObject { ["material"] = item.MaterialId,
                    ["quality"] = item.QualityId, ["quantity"] = item.Quantity.ToString() })),
                ["console"] = ConsoleCounts() };
            Camera camera = Boot.WorldCamera;
            result["camera"] = new JObject { ["x"] = camera.transform.position.x, ["y"] = camera.transform.position.y,
                ["orthographicSize"] = camera.orthographicSize, ["viewport"] = JArray.FromObject(new[] { camera.rect.x, camera.rect.y, camera.rect.width, camera.rect.height }) };
            if (Game.WorldView != null)
            {
                Rect[] labels = Game.WorldView.GrowthLabelRects.Values.ToArray();
                result["growthLabels"] = new JObject { ["visible"] = labels.Length,
                    ["characterOverlaps"] = labels.Sum(label => Game.WorldView.CharacterAvoidanceRects.Count(label.Overlaps)),
                    ["labelOverlaps"] = labels.Select((label, index) => labels.Skip(index + 1).Count(label.Overlaps)).Sum() };
            }
            string target = state.Players[0].TargetResourceId ?? Game.WorldView?.LastTappedResourceId;
            var resource = state.Resources.FirstOrDefault(item => item.Id == target);
            if (resource != null) result["targetResource"] = new JObject { ["id"] = resource.Id, ["stock"] = resource.RemainingStock,
                ["stage"] = resource.GrowthStage.ToString(), ["untilReady"] = resource.SecondsUntilReady };
            return result;
        }

        private static void Inspect()
        {
            Game?.RefreshSnapshot();
            var entries = new JArray();
            foreach (var element in Descendants(Boot.Router.CurrentView))
                entries.Add(new JObject { ["type"] = element.GetType().Name, ["name"] = element.name,
                    ["text"] = (element as TextElement)?.text, ["classes"] = new JArray(element.GetClasses()),
                    ["x"] = element.worldBound.x, ["y"] = element.worldBound.y,
                    ["width"] = element.worldBound.width, ["height"] = element.worldBound.height,
                    ["display"] = element.resolvedStyle.display.ToString() });
            JObject result = Game == null ? new JObject { ["screen"] = Boot.Router.CurrentScreen } : Describe();
            result["layout"] = entries;
            if (Game?.WorldView != null)
            {
                var resources = new JArray();
                foreach (ResourceNode node in Game.Server.World.ResourcesAvailable(Game.Snapshot.RoundNumber))
                {
                    Vector2 point = Game.WorldView.ResourcePanelPoint(node.Id);
                    if (point.x < 0 || point.x > Screen.width || point.y < 0 || point.y > Screen.height) continue;
                    resources.Add(new JObject { ["id"] = node.Id, ["material"] = node.MaterialId,
                        ["panelX"] = point.x, ["panelY"] = point.y, ["stock"] = node.RemainingStock,
                        ["pathLength"] = Pathfinding.FindPathToResource(Game.Server.World, Game.Snapshot.Players[0].Position, node, Game.Snapshot.RoundNumber).Steps.Count });
                }
                result["visibleResources"] = resources;
            }
            File.WriteAllText("Library/M3B-inspect.json", result.ToString());
            WriteResult("inspection complete");
        }

        private static JObject ConsoleCounts()
        {
            Type entries = typeof(EditorApplication).Assembly.GetType("UnityEditor.LogEntries");
            object[] counts = { 0, 0, 0 };
            entries.GetMethod("GetCountsByType", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic).Invoke(null, counts);
            return new JObject { ["errors"] = (int)counts[0], ["warnings"] = (int)counts[1], ["logs"] = (int)counts[2] };
        }
        private static void WriteResult(string status) => File.WriteAllText(Result, new JObject { ["status"] = status }.ToString());
        private static IEnumerable<VisualElement> Descendants(VisualElement root)
        { yield return root; foreach (var child in root.hierarchy.Children()) foreach (var entry in Descendants(child)) yield return entry; }
    }
}
