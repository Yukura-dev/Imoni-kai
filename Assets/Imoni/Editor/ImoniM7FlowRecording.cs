using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using Imoni.Core;
using Imoni.Game;
using Imoni.Game.UI;
using Imoni.Game.UI.Screens;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;
using BigInteger = System.Numerics.BigInteger;

namespace Imoni.Editor
{
    /// <summary>Continuous real Game-view recording. Never advances or replaces Core state.</summary>
    [InitializeOnLoad]
    public static class ImoniM7FlowRecording
    {
        private const string PendingKey = "Imoni.M7FlowRecording.Pending";
        private const string PreviousClockKey = "Imoni.M7FlowRecording.PreviousShortClock";
        public const string FramesDirectory = "Library/M7-flow-frames";
        public const string StatusPath = "Library/M7-flow-status.json";
        private static readonly BindingFlags StaticPrivate = BindingFlags.Static | BindingFlags.NonPublic;
        private static readonly List<JObject> frames = new List<JObject>();
        private static readonly JArray transitions = new JArray(), decisions = new JArray(), clicks = new JArray();
        private static bool running, draining, upgraded, rescued;
        private static int frameRate, lastUnityFrame = -1;
        private static double began, nextFrameAt, enteredAt, readyAt = -1, endedAt;
        private static decimal nextDecision;
        private static string currentKey, failure, state = "idle";
        private static JArray purchases;
        private static StreamWriter frameLog;
        private static Bootstrap Boot => UnityEngine.Object.FindFirstObjectByType<Bootstrap>();
        public static bool IsRunning => running || draining || SessionState.GetBool(PendingKey, false);

        static ImoniM7FlowRecording()
        {
            EditorApplication.update += Update;
            AssemblyReloadEvents.beforeAssemblyReload += () => { if (running || draining) Fail("Compilation interrupted the continuous recording."); };
        }

        [MenuItem("Imoni/Development/Record Main to Round 2")]
        public static void Start()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode || IsRunning)
                throw new InvalidOperationException("Stop Play Mode before starting the isolated flow recording.");
            if (!Boot) throw new InvalidOperationException("Open the generated Boot scene first.");
            string directory = Path.GetFullPath(FramesDirectory); Directory.CreateDirectory(directory);
            // Only remove this driver's reserved frame names; never touch arbitrary files or folders.
            foreach (string path in Directory.EnumerateFiles(directory, "frame_*.png"))
                if (int.TryParse(Path.GetFileNameWithoutExtension(path).Substring("frame_".Length), out _)) File.Delete(path);
            foreach (EditorWindow window in Resources.FindObjectsOfTypeAll<EditorWindow>()
                .Where(window => window.GetType().FullName == "UnityEditor.DeviceSimulation.SimulatorWindow")) window.Close();
            typeof(ImoniM0ReviewJob).GetMethod("ConfigureGameView", StaticPrivate).Invoke(null, null);
            SessionState.SetBool(PreviousClockKey, DevelopmentRoundClock.Enabled);
            SessionState.SetBool(PendingKey, true); DevelopmentRoundClock.Enabled = true;
            string profile = Path.GetFullPath("Library/M7-flow-profile-" + DateTime.UtcNow.ToString("yyyyMMdd-HHmmssfff", CultureInfo.InvariantCulture) + ".json");
            SessionState.SetString("Imoni.ReviewSavePath", profile);
            SessionState.SetString("Imoni.StartScreen", "Main");
            state = "opening"; WriteStatus(); EditorApplication.isPlaying = true;
        }

        [MenuItem("Imoni/Development/Stop Flow Recording")]
        public static void Stop()
        {
            if (running || draining) Fail("Recording stopped before reaching Round 2.");
            else { SessionState.SetBool(PendingKey, false); RestoreClock(); state = "stopped"; WriteStatus(); }
        }

        public static JObject Status() => new JObject
        {
            ["status"] = state, ["running"] = IsRunning, ["frames"] = frames.Count,
            ["screen"] = Boot?.Router?.CurrentScreen, ["stage"] = currentKey,
            ["elapsedRealSeconds"] = running || draining ? Time.realtimeSinceStartupAsDouble - began : endedAt,
            ["frameRateRequested"] = frameRate, ["directory"] = Path.GetFullPath(FramesDirectory),
            ["failure"] = failure
        };

        private static void Update()
        {
            if (EditorApplication.isCompiling || EditorApplication.isUpdating) return;
            try
            {
                if (!running && !draining && SessionState.GetBool(PendingKey, false))
                {
                    if (!EditorApplication.isPlaying || Boot?.Router?.CurrentScreen != "Main") return;
                    if (Boot.Router.CurrentView.worldBound.width <= 0) return;
                    BeginCapture();
                }
                if (!running && !draining) return;
                if (!EditorApplication.isPlaying) { Fail("Play Mode stopped during recording."); return; }
                double now = Time.realtimeSinceStartupAsDouble;
                if (now - began > UiTheme.Data["flowReview"]["maxSeconds"].Value<double>())
                    throw new TimeoutException("The flow did not reach Round 2 within flowReview.maxSeconds.");
                if (draining) { Drain(); return; }
                Drive(now);
                if (!running) return;
                if (now >= nextFrameAt && Time.frameCount != lastUnityFrame)
                {
                    Capture(now); lastUnityFrame = Time.frameCount;
                    do { nextFrameAt += 1d / frameRate; } while (nextFrameAt <= now);
                }
                EditorApplication.QueuePlayerLoopUpdate();
            }
            catch (Exception exception) { Fail(exception.ToString()); }
        }

        private static void BeginCapture()
        {
            if (Boot.Context.Profile.TutorialCompleted) throw new InvalidOperationException("The isolated profile must begin before tutorial completion.");
            if (UiTheme.Data["outGame"]["lobby"]["commonSeed"].Value<int>() != UiTheme.Data["balanceReview"]["seed"].Value<int>())
                throw new InvalidOperationException("The normal common-seed flow must use balanceReview.seed.");
            SessionState.SetBool(PendingKey, false);
            frames.Clear(); transitions.Clear(); decisions.Clear(); clicks.Clear();
            failure = currentKey = null; upgraded = rescued = false; nextDecision = 0;
            purchases = new JArray(); frameRate = UiTheme.Data["effectReview"]["frameRate"].Value<int>();
            began = nextFrameAt = Time.realtimeSinceStartupAsDouble; endedAt = 0; readyAt = -1; lastUnityFrame = -1;
            frameLog = new StreamWriter(Path.Combine(FramesDirectory, "frames.jsonl"), false, new UTF8Encoding(false));
            running = true; draining = false; state = "recording"; WriteStatus();
        }

        private static void Drive(double now)
        {
            string screen = Boot.Router.CurrentScreen;
            GameScreen game = Boot.Router.CurrentView as GameScreen;
            GameSnapshot snapshot = game?.Server.Snapshot();
            string key = screen + (snapshot == null ? string.Empty : ":R" + snapshot.RoundNumber + ":" + snapshot.Phase);
            if (key != currentKey)
            {
                currentKey = key; enteredAt = now; readyAt = -1;
                transitions.Add(new JObject { ["realSeconds"] = now - began, ["stage"] = key }); WriteStatus();
            }
            double dwell = UiTheme.Number("toastSeconds");
            switch (screen)
            {
                case "Main": ClickWhenReady("start", now, dwell); break;
                case "Tutorial1": case "Tutorial2": case "Tutorial3": ClickWhenReady("next", now, dwell); break;
                case "Home": ClickWhenReady("play", now, dwell); break;
                case "Lobby": ClickWhenReady("CreateTeam", now, dwell); break;
                case "Waiting": ClickWhenReady("WaitingStart", now, dwell); break;
                case "Loading": ClickWhenReady("LoadingStart", now, dwell); break;
                case "Game":
                    if (snapshot.RoundNumber == 2)
                    {
                        if (snapshot.Phase != RoundPhase.Collecting) throw new InvalidOperationException("Round 2 is not collecting.");
                        if (now - enteredAt >= dwell) Complete(now);
                    }
                    else if (snapshot.RoundNumber != 1) throw new InvalidOperationException("Unexpected round in the R1 flow.");
                    else if (snapshot.Phase == RoundPhase.Collecting) Autoplay(game, snapshot, now);
                    else if (snapshot.LastResult?.Evaluation.Success == true) ClickWhenReady("TimeUpResults", now, dwell);
                    else throw new InvalidOperationException("R1 failed during real-time autoplay; the recording preserves that result.");
                    break;
                case "Imoni": ClickWhenReady("ResultButton", now, dwell); break;
                case "Result": ClickWhenReady("ResultNext", now, dwell); break;
                default: throw new InvalidOperationException("Unexpected screen in the normal flow: " + screen);
            }
        }

        private static void ClickWhenReady(string name, double now, double dwell)
        {
            Button button = Boot.Router.CurrentView.Q<Button>(name);
            if (button == null || !button.enabledInHierarchy || !Visible(button)) { readyAt = -1; return; }
            if (readyAt < 0) readyAt = now;
            if (now - readyAt < dwell) return;
            MethodInfo invoke = typeof(Clickable).GetMethod("Invoke", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic,
                null, new[] { typeof(EventBase) }, null);
            clicks.Add(new JObject { ["realSeconds"] = now - began, ["screen"] = Boot.Router.CurrentScreen, ["button"] = name });
            using (ClickEvent click = ClickEvent.GetPooled()) { click.target = button; invoke.Invoke(button.clickable, new object[] { click }); }
            readyAt = -1;
        }

        private static bool Visible(VisualElement element)
        {
            for (VisualElement parent = element; parent != null; parent = parent.parent)
                if (parent.resolvedStyle.display == DisplayStyle.None || parent.resolvedStyle.visibility == Visibility.Hidden || parent.resolvedStyle.opacity <= 0) return false;
            return element.worldBound.width > 0 && element.worldBound.height > 0;
        }

        private static void Autoplay(GameScreen game, GameSnapshot snapshot, double now)
        {
            IGameServer server = game.Server; GameConfig config = UiTheme.Config;
            if (game.ReviewPaused) throw new InvalidOperationException("Continuous recording requires real Game.Tick, not ReviewPaused.");
            if (!upgraded)
            {
                if (snapshot.Seed != UiTheme.Data["balanceReview"]["seed"].Value<int>()) throw new InvalidOperationException("Unexpected autoplay seed.");
                purchases = (JArray)InvokeM6("AutoplayUpgrades", server, game.LocalActorId, config, "budget");
                upgraded = true; snapshot = server.Snapshot();
            }
            decimal remaining = config.DurationSeconds - snapshot.ElapsedSeconds;
            if (remaining <= config.BotIntervalSeconds && !rescued)
            {
                InvokeM6("AutoplayBuyDeficits", server, game.LocalActorId, config, snapshot); rescued = true;
                decisions.Add(new JObject { ["realSeconds"] = now - began, ["gameSeconds"] = snapshot.ElapsedSeconds, ["command"] = "Buy affordable required deficits" });
                return;
            }
            if (snapshot.ElapsedSeconds < nextDecision) return;
            nextDecision = (decimal.Floor(snapshot.ElapsedSeconds / config.BotIntervalSeconds) + 1) * config.BotIntervalSeconds;
            PlayerSnapshot actor = snapshot.Players.First(player => player.Id == game.LocalActorId);
            var missing = snapshot.RequiredObjectives.Select(goal => new
            {
                Goal = goal, Deficit = BigInteger.Max(0, goal.Amount - GameHudProjection.Equivalent(config, snapshot.Lots, goal.MaterialId))
            }).Where(item => item.Deficit > 0).ToArray();
            ResourceNode active = server.World.Resources.FirstOrDefault(node => node.Id == actor.TargetResourceId);
            bool working = actor.Action != PlayerAction.Idle && active != null && missing.Any(item => item.Goal.MaterialId == active.MaterialId);
            string selected = null;
            if (!working)
            {
                foreach (var item in missing.OrderBy(item => config.Panels.ShopMaterials.Contains(item.Goal.MaterialId))
                    .ThenByDescending(item => item.Deficit * config.Material(item.Goal.MaterialId).BasePrice))
                    if ((bool)InvokeM6("AutoplaySelectResource", server, actor, snapshot.RoundNumber, item.Goal.MaterialId))
                    { selected = item.Goal.MaterialId; working = true; break; }
                if (!working && missing.Length == 0 && snapshot.BonusObjective != null
                    && (active == null || active.MaterialId != snapshot.BonusObjective.MaterialId || actor.Action == PlayerAction.Idle)
                    && (bool)InvokeM6("AutoplaySelectResource", server, actor, snapshot.RoundNumber, snapshot.BonusObjective.MaterialId))
                    selected = snapshot.BonusObjective.MaterialId;
            }
            decisions.Add(new JObject { ["realSeconds"] = now - began, ["gameSeconds"] = snapshot.ElapsedSeconds,
                ["command"] = selected == null ? "Continue current action" : "TrySelectResource", ["material"] = selected });
        }

        private static object InvokeM6(string name, params object[] arguments) => typeof(ImoniM6ReviewJob).GetMethod(name, StaticPrivate).Invoke(null, arguments);

        private static void Capture(double now)
        {
            JObject review = (JObject)UiTheme.Data["effectReview"];
            if (Screen.width != review["width"].Value<int>() || Screen.height != review["height"].Value<int>())
                throw new InvalidOperationException("The flow must be captured at the configured 390x844 Game-view resolution.");
            string file = "frame_" + frames.Count.ToString("D5", CultureInfo.InvariantCulture) + ".png";
            ScreenCapture.CaptureScreenshot(Path.GetFullPath(Path.Combine(FramesDirectory, file)));
            GameSnapshot snapshot = Boot.Context.Server?.Snapshot();
            var frame = new JObject { ["file"] = file, ["realSeconds"] = now - began, ["unityFrame"] = Time.frameCount,
                ["screen"] = Boot.Router.CurrentScreen, ["round"] = snapshot?.RoundNumber, ["gameSeconds"] = snapshot?.ElapsedSeconds,
                ["phase"] = snapshot?.Phase.ToString() };
            frames.Add(frame); frameLog.WriteLine(frame.ToString(Newtonsoft.Json.Formatting.None));
            if (frames.Count % frameRate == 0) { frameLog.Flush(); WriteStatus(); }
        }

        private static void Complete(double now)
        {
            Capture(now); endedAt = now - began; running = false; draining = true; state = "saving";
            (Boot.Router.CurrentView as GameScreen).ReviewPaused = true;
            frameLog.Flush(); RestoreClock(); WriteStatus();
        }

        private static void Drain()
        {
            if (frames.Any(frame => !File.Exists(Path.Combine(FramesDirectory, frame["file"].Value<string>()))
                || new FileInfo(Path.Combine(FramesDirectory, frame["file"].Value<string>())).Length == 0)) return;
            frameLog?.Dispose(); frameLog = null;
            var concat = new StringBuilder("ffconcat version 1.0\n");
            for (int index = 0; index < frames.Count; index++)
            {
                double next = index + 1 < frames.Count ? frames[index + 1]["realSeconds"].Value<double>() : endedAt + 1d / frameRate;
                double duration = next - frames[index]["realSeconds"].Value<double>();
                concat.Append("file '").Append(frames[index]["file"].Value<string>()).Append("'\nduration ")
                    .Append(duration.ToString("0.000000", CultureInfo.InvariantCulture)).Append('\n');
            }
            concat.Append("file '").Append(frames[frames.Count - 1]["file"].Value<string>()).Append("'\n");
            File.WriteAllText(Path.Combine(FramesDirectory, "frames.ffconcat"), concat.ToString(), new UTF8Encoding(false));
            var report = new JObject { ["source"] = "Continuous Game view, normal UI button sequence and real Bootstrap.Tick",
                ["captureTimestamp"] = "Time.realtimeSinceStartupAsDouble at screenshot request", ["frameRateRequested"] = frameRate,
                ["durationSeconds"] = endedAt, ["frameCount"] = frames.Count,
                ["gameClockScale"] = UiTheme.Data["round"]["mockTimeScale"], ["effectClockScale"] = 1,
                ["profile"] = Boot.Context.SavePath, ["seed"] = Boot.Context.Server.Snapshot().Seed,
                ["transitions"] = transitions, ["clicks"] = clicks, ["upgradePurchases"] = purchases,
                ["autoplayDecisions"] = decisions, ["frames"] = new JArray(frames), ["finalRound"] = Boot.Context.Server.Snapshot().RoundNumber,
                ["console"] = (JObject)InvokeM6("ConsoleCounts") };
            File.WriteAllText(Path.Combine(FramesDirectory, "recording.json"), report.ToString());
            draining = false; state = "complete"; WriteStatus();
        }

        private static void Fail(string message)
        {
            failure = message; endedAt = began > 0 ? Time.realtimeSinceStartupAsDouble - began : 0;
            running = draining = false; state = "failed"; SessionState.SetBool(PendingKey, false);
            frameLog?.Dispose(); frameLog = null; RestoreClock(); WriteStatus();
        }
        private static void RestoreClock() => DevelopmentRoundClock.Enabled = SessionState.GetBool(PreviousClockKey, false);
        private static void WriteStatus() => File.WriteAllText(StatusPath, Status().ToString());
    }
}
