using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using Imoni.Core;
using Imoni.Game;
using Imoni.Game.Effects;
using Imoni.Game.UI;
using Imoni.Game.UI.Screens;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;
using BigInteger = System.Numerics.BigInteger;

namespace Imoni.Editor
{
    /// <summary>Editor-only capture of the attached UI and the same absolute-time effect used in play.</summary>
    [InitializeOnLoad]
    public static class ImoniM5ReviewJob
    {
        private const string Request = "Library/M5Review.request", Result = "Library/M5Review.result";
        private static string capturePath, recordDirectory;
        private static int captureFrame, recordFrame, recordCount, recordRate;
        private static bool captureRequested, artworkPreview;
        private static JObject captureState;
        private static readonly JArray recordStates = new JArray();
        private static Bootstrap Boot => UnityEngine.Object.FindFirstObjectByType<Bootstrap>();
        private static GameScreen Game => Boot?.Router?.CurrentView as GameScreen;
        private static ImoniEffectView Effect => (Boot?.Router?.CurrentView as ImoniEffectScreen)?.Effect;
        private static JObject Review => (JObject)UiTheme.Data["effectReview"];

        static ImoniM5ReviewJob() { EditorApplication.update += Update; }

        private static void Update()
        {
            if (EditorApplication.isCompiling || EditorApplication.isUpdating) return;
            try
            {
                if (capturePath != null) CompleteCapture();
                if (capturePath != null) return;
                if (recordDirectory != null) { AdvanceRecording(); return; }
                if (!File.Exists(Request)) return;
                JObject request;
                try { request = JObject.Parse(File.ReadAllText(Request)); File.Delete(Request); }
                catch (IOException) { return; }
                Run(request);
            }
            catch (Exception exception)
            {
                capturePath = recordDirectory = null;
                File.WriteAllText(Result, new JObject { ["status"] = "failed", ["failure"] = exception.ToString() }.ToString());
                Debug.LogException(exception);
            }
        }

        private static void Run(JObject request)
        {
            string command = request["command"]?.Value<string>();
            if (command == "console") { File.WriteAllText(Result, ConsoleCounts().ToString()); return; }
            if (command == "open")
            {
                if (EditorApplication.isPlaying) throw new InvalidOperationException("Exit Play Mode first.");
                typeof(ImoniM0ReviewJob).GetMethod("ConfigureGameView", BindingFlags.Static | BindingFlags.NonPublic).Invoke(null, null);
                ImoniSetup.SetupProject();
                SessionState.SetString("Imoni.ReviewSavePath", Path.GetFullPath("Library/M5-profile.json"));
                SessionState.SetString("Imoni.StartScreen", "Loading");
                EditorApplication.isPlaying = true; WriteResult("opening isolated review"); return;
            }
            if (!EditorApplication.isPlaying || Boot?.Router == null) throw new InvalidOperationException("Start Boot first.");
            switch (command)
            {
                case "prepare":
                    DevelopmentRoundClock.Enabled = false;
                    Boot.Context.PrepareGame(request["seed"]?.Value<int>());
                    Boot.Router.Show("Game"); Game.ReviewPaused = true; artworkPreview = false;
                    WriteResult("prepared"); break;
                case "step":
                    if (Game == null) throw new InvalidOperationException("Open Game before advancing the round.");
                    Game.ReviewPaused = true;
                    decimal seconds = request["seconds"].Value<decimal>();
                    Game.Server.Tick(seconds); Game.RefreshSnapshot((float)seconds); WriteResult("stepped"); break;
                case "autoplay": AutoplayRound(); break;
                case "disconnect":
                    Boot.Context.Server.SetConnected(request["actor"].Value<string>(), request["connected"].Value<bool>());
                    Game?.RefreshSnapshot(); WriteResult("connection changed"); break;
                case "click": Click(request["name"].Value<string>()); break;
                case "show":
                    if (!Boot.Router.Show(request["screen"].Value<string>())) throw new ArgumentException("Unknown screen.");
                    if (Game != null) Game.ReviewPaused = true;
                    if (Effect != null) Effect.ManualTime = true;
                    WriteResult("shown"); break;
                case "preview":
                    BeginArtworkPreview(request["failed"]?.Value<bool>() ?? false); WriteResult("artwork preview"); break;
                case "sample":
                    RequireEffect().ManualTime = true;
                    Effect.SampleAt(request["timeMs"].Value<double>()); WriteResult("sampled"); break;
                case "replay": RequireEffect().Replay(); Effect.ManualTime = true; WriteResult("replayed"); break;
                case "clock":
                    DevelopmentRoundClock.Enabled = request["enabled"].Value<bool>();
                    if (Game != null) Game.ReviewPaused = request["paused"]?.Value<bool>() ?? false;
                    WriteResult("clock changed"); break;
                case "capture": Pause(); BeginCapture("Library/M5-" + SafeName(request["name"].Value<string>()) + ".png"); break;
                case "inspect":
                    Game?.RefreshSnapshot(); JObject state = Describe();
                    state["layout"] = new JArray(Descendants(Boot.Router.CurrentView).Select(element => new JObject {
                        ["type"] = element.GetType().Name, ["name"] = element.name, ["text"] = (element as TextElement)?.text,
                        ["classes"] = new JArray(element.GetClasses()), ["x"] = element.worldBound.x, ["y"] = element.worldBound.y,
                        ["width"] = element.worldBound.width, ["height"] = element.worldBound.height,
                        ["display"] = element.resolvedStyle.display.ToString(), ["enabled"] = element.enabledInHierarchy }));
                    File.WriteAllText("Library/M5-inspect.json", state.ToString()); WriteResult("inspected"); break;
                case "record":
                    RequireEffect().ManualTime = true; Effect.SampleAt(0);
                    recordDirectory = Path.GetFullPath("Library/M5-" + SafeName(request["name"].Value<string>()) + "-frames");
                    Directory.CreateDirectory(recordDirectory);
                    recordRate = Review["frameRate"].Value<int>();
                    recordCount = (int)Math.Ceiling(Review["durationMs"].Value<double>() * recordRate / 1000);
                    recordFrame = 0; recordStates.Clear();
                    WriteResult("recording started"); break;
                default: throw new ArgumentException("Unknown M5 command: " + command);
            }
        }

        private static void BeginArtworkPreview(bool failed)
        {
            // A separate, explicitly labelled artwork preview never changes a settled server evaluation.
            if (Boot.Context.Server?.Snapshot().LastResult == null)
            {
                Boot.Context.PrepareGame(Review["seed"].Value<int>());
                Boot.Context.Server.Tick(UiTheme.Config.DurationSeconds);
            }
            Boot.Router.Show(Boot.Context.Server.Snapshot().LastResult.Evaluation.Success ? "Imoni" : "ImoniFail");
            ImoniEffectView view = RequireEffect(); view.ManualTime = true;
            view.Begin(BigInteger.Parse(Review[failed ? "failureScore" : "successScore"].Value<string>(), CultureInfo.InvariantCulture),
                BigInteger.Parse(Review["successScore"].Value<string>(), CultureInfo.InvariantCulture), failed,
                Review["failureMissing"].Value<string>(), () => { }, Review["seed"].Value<int>());
            artworkPreview = true;
        }

        private static ImoniEffectView RequireEffect() => Effect != null ? Effect : throw new InvalidOperationException("Open the effect first.");
        private static void Pause() { if (Game != null) Game.ReviewPaused = true; if (Effect != null) Effect.ManualTime = true; }
        private static string SafeName(string name) => !string.IsNullOrWhiteSpace(name) && name == Path.GetFileName(name)
            ? name : throw new ArgumentException("Use a filename without a directory.");

        private static void AutoplayRound()
        {
            if (Game == null) throw new InvalidOperationException("Open Game before autoplaying one round.");
            Game.ReviewPaused = true;
            IGameServer server = Game.Server;
            GameConfig config = UiTheme.Config;
            GameSnapshot state = server.Snapshot();
            if (state.Phase != RoundPhase.Collecting) throw new InvalidOperationException("Autoplay requires a collecting round.");
            string actorId = Game.LocalActorId;
            if (!state.Players.First(player => player.Id == actorId).Connected)
                throw new InvalidOperationException("Autoplay does not reconnect disconnected players.");
            int initialRound = state.RoundNumber, ticks = 0, selections = 0;
            decimal initialTime = state.ElapsedSeconds;
            // Use the existing decision cadence. Every movement tile, gather duration and bot event
            // is still processed by authoritative Tick; no inventory/result/world value is assigned.
            while (state.Phase == RoundPhase.Collecting)
            {
                decimal remaining = config.DurationSeconds - state.ElapsedSeconds;
                if (remaining <= config.BotIntervalSeconds)
                    AutoplayBuyDeficits(server, actorId, config, state);
                else
                {
                    PlayerSnapshot actor = state.Players.First(player => player.Id == actorId);
                    var missing = state.RequiredObjectives.Select(goal => new
                    {
                        Goal = goal,
                        Deficit = BigInteger.Max(0, goal.Amount - GameHudProjection.Equivalent(config, state.Lots, goal.MaterialId))
                    }).Where(item => item.Deficit > 0).ToArray();
                    ResourceNode activeTarget = server.World.Resources.FirstOrDefault(node => node.Id == actor.TargetResourceId);
                    bool alreadyWorking = actor.Action != PlayerAction.Idle && activeTarget != null
                        && missing.Any(item => item.Goal.MaterialId == activeTarget.MaterialId);
                    if (!alreadyWorking)
                    {
                        // Shop cannot rescue fish/sansai deficits; collect those first. Within each
                        // group, prioritize the largest cost of replacing the missing equivalents.
                        foreach (var item in missing.OrderBy(item => config.Panels.ShopMaterials.Contains(item.Goal.MaterialId))
                            .ThenByDescending(item => item.Deficit * config.Material(item.Goal.MaterialId).BasePrice))
                            if (AutoplaySelectResource(server, actor, state.RoundNumber, item.Goal.MaterialId))
                            { selections++; alreadyWorking = true; break; }
                        if (!alreadyWorking && missing.Length == 0 && state.BonusObjective != null
                            && (activeTarget == null || activeTarget.MaterialId != state.BonusObjective.MaterialId || actor.Action == PlayerAction.Idle)
                            && AutoplaySelectResource(server, actor, state.RoundNumber, state.BonusObjective.MaterialId)) selections++;
                    }
                }
                server.Tick(Math.Min(config.BotIntervalSeconds, remaining));
                ticks++;
                state = server.Snapshot();
            }
            artworkPreview = false;
            Game.RefreshSnapshot();
            JObject report = Describe();
            report["status"] = "autoplay complete";
            report["autoplay"] = new JObject { ["round"] = initialRound, ["actor"] = actorId,
                ["secondsAdvanced"] = config.DurationSeconds - initialTime, ["tickCalls"] = ticks,
                ["resourceSelections"] = selections, ["commands"] = "IGameServer.TrySelectResource/Buy/Tick" };
            File.WriteAllText(Result, report.ToString());
        }

        private static bool AutoplaySelectResource(IGameServer server, PlayerSnapshot actor, int round, string materialId)
        {
            ResourceNode best = null;
            int bestSteps = int.MaxValue;
            foreach (ResourceNode node in server.World.Resources.Where(node => node.MaterialId == materialId && node.IsReady)
                .OrderBy(node => actor.Position.ManhattanDistance(node.Position)).ThenBy(node => node.Id, StringComparer.Ordinal))
            {
                if (actor.Position.ManhattanDistance(node.Position) > bestSteps) break;
                PathResult path = Pathfinding.FindPathToResource(server.World, actor.Position, node, round);
                if (!path.Reachable || path.Steps.Count >= bestSteps) continue;
                best = node; bestSteps = path.Steps.Count;
            }
            return best != null && server.TrySelectResource(actor.Id, best.Id);
        }

        private static void AutoplayBuyDeficits(IGameServer server, string actorId, GameConfig config, GameSnapshot state)
        {
            BigInteger equivalent = config.Quality(config.PurchasedQualityId).Multiplier;
            foreach (Objective goal in state.RequiredObjectives.Where(goal => config.Panels.ShopMaterials.Contains(goal.MaterialId)))
            {
                state = server.Snapshot();
                BigInteger deficit = goal.Amount - GameHudProjection.Equivalent(config, state.Lots, goal.MaterialId);
                if (deficit <= 0) continue;
                BigInteger desired = (deficit + equivalent - 1) / equivalent;
                // PurchaseCost can round decimal prices, so find an affordable quantity using the
                // same exact Core quotation rather than dividing by an approximate unit price.
                BigInteger affordable = 0, upper = desired + 1;
                while (upper - affordable > 1)
                {
                    BigInteger quantity = (affordable + upper) / 2;
                    if (GamePanelProjection.PurchaseCost(config, goal.MaterialId, quantity) <= state.Funds) affordable = quantity;
                    else upper = quantity;
                }
                if (affordable > 0) server.Buy(actorId, goal.MaterialId, affordable);
            }
        }

        private static void Click(string name)
        {
            Button button = Boot.Router.CurrentView.Q<Button>(name);
            if (button == null) throw new InvalidOperationException("Button not found: " + name);
            if (!button.enabledInHierarchy || button.resolvedStyle.display == DisplayStyle.None) { WriteResult("disabled or hidden button rejected"); return; }
            MethodInfo invoke = typeof(Clickable).GetMethod("Invoke", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic,
                null, new[] { typeof(EventBase) }, null);
            using (ClickEvent click = ClickEvent.GetPooled()) { click.target = button; invoke.Invoke(button.clickable, new object[] { click }); }
            if (Game != null) Game.ReviewPaused = true;
            if (Effect != null) Effect.ManualTime = true;
            WriteResult("clicked");
        }

        private static void BeginCapture(string path)
        {
            capturePath = Path.GetFullPath(path);
            if (File.Exists(capturePath)) File.Delete(capturePath);
            captureFrame = Time.frameCount; captureRequested = false;
        }
        private static void CompleteCapture()
        {
            if (!captureRequested)
            {
                if (Time.frameCount < captureFrame + 2) return;
                if (Screen.width != Review["width"].Value<int>() || Screen.height != Review["height"].Value<int>())
                    throw new InvalidOperationException("Set Game view to configured 390x844 before capturing.");
                captureState = Describe(); captureState["image"] = Path.GetFileName(capturePath);
                ScreenCapture.CaptureScreenshot(capturePath); captureRequested = true; return;
            }
            if (!File.Exists(capturePath) || new FileInfo(capturePath).Length == 0) return;
            if (recordDirectory == null)
            {
                File.WriteAllText(Path.ChangeExtension(capturePath, ".json"), captureState.ToString());
                File.WriteAllText(Result, new JObject { ["status"] = "complete", ["image"] = capturePath, ["screen"] = Boot.Router.CurrentScreen,
                    ["console"] = captureState["console"] }.ToString());
            }
            else
            {
                recordStates.Add(captureState); recordFrame++;
                File.WriteAllText("Library/M5-record-progress.json", new JObject { ["frame"] = recordFrame, ["count"] = recordCount, ["directory"] = recordDirectory }.ToString());
            }
            capturePath = null;
        }
        private static void AdvanceRecording()
        {
            if (recordFrame >= recordCount)
            {
                string path = recordDirectory;
                File.WriteAllText(path + ".json", new JObject { ["frameRate"] = recordRate, ["frames"] = recordStates }.ToString());
                recordDirectory = null;
                File.WriteAllText(Result, new JObject { ["status"] = "recording complete", ["frames"] = recordCount,
                    ["directory"] = path, ["console"] = ConsoleCounts() }.ToString()); return;
            }
            RequireEffect().SampleAt(recordFrame * 1000.0 / recordRate);
            BeginCapture(Path.Combine(recordDirectory, recordFrame.ToString("D5") + ".png"));
        }

        private static JObject Describe()
        {
            var state = new JObject { ["utc"] = DateTime.UtcNow.ToString("O"), ["width"] = Screen.width, ["height"] = Screen.height,
                ["screen"] = Boot.Router.CurrentScreen, ["source"] = artworkPreview ? "artwork-preview" : "IGameServer",
                ["console"] = ConsoleCounts(), ["shortRounds"] = DevelopmentRoundClock.Enabled, ["clockScale"] = DevelopmentRoundClock.Scale };
            GameSnapshot snapshot = Boot.Context.Server?.Snapshot();
            if (snapshot != null && recordDirectory == null)
            {
                state["round"] = snapshot.RoundNumber; state["seed"] = snapshot.Seed; state["phase"] = snapshot.Phase.ToString();
                state["elapsed"] = snapshot.ElapsedSeconds; state["funds"] = snapshot.Funds.ToString(); state["cumulative"] = snapshot.CumulativeScore.ToString();
                state["inventoryCount"] = snapshot.Lots.Count; state["protectionCount"] = snapshot.Protection.Count;
                RoundResult settled = snapshot.Phase == RoundPhase.Collecting ? null : snapshot.LastResult;
                state["requiredObjectives"] = settled == null
                    ? new JArray(snapshot.RequiredObjectives.Select(goal => RequiredProgress(goal.MaterialId, goal.Amount,
                        GameHudProjection.Equivalent(UiTheme.Config, snapshot.Lots, goal.MaterialId))))
                    : new JArray(settled.Evaluation.RequiredGoals.Select(goal => RequiredProgress(goal.MaterialId, goal.Required, goal.Current)));
                state["players"] = new JArray(snapshot.Players.Select(player => new JObject { ["id"] = player.Id, ["streak"] = player.Streak,
                    ["action"] = player.Action.ToString(), ["upgrades"] = JObject.FromObject(player.Upgrades) }));
                state["resourceCount"] = snapshot.Resources.Count;
                state["resourceStateHash"] = Hash(string.Join(";", snapshot.Resources.Select(node => node.Id + ":" + node.RemainingStock + ":" + node.RegrowthSeconds.ToString(CultureInfo.InvariantCulture))));
                state["worldIdentityHash"] = Hash(string.Join(";", snapshot.Resources.Select(node => node.Id + ":" + node.MaterialId + ":" + node.Position)));
                state["unlockedRadius"] = Boot.Context.Server.World.Settings.UnlockRadius(snapshot.RoundNumber);
                RoundResult result = snapshot.LastResult;
                if (result != null) state["result"] = new JObject { ["round"] = result.RoundNumber, ["success"] = result.Evaluation.Success,
                    ["score"] = result.Evaluation.Score.ToString(), ["requiredPoints"] = result.Evaluation.RequiredPoints.ToString(),
                    ["bonusPoints"] = result.Evaluation.BonusPoints.ToString(), ["bonusMultiplier"] = result.Evaluation.BonusMultiplier.ToString(),
                    ["fundsReward"] = result.Evaluation.FundsReward.ToString(), ["fundsAfter"] = result.FundsAfterSettlement.ToString(),
                    ["cumulative"] = result.CumulativeScore.ToString(), ["missing"] = new JArray(result.Evaluation.MissingMaterialIds) };
            }
            if (Effect?.Timeline != null)
            {
                var timeline = Effect.Timeline;
                state["effect"] = new JObject { ["timeMs"] = timeline.NowMs, ["failed"] = timeline.Failed, ["level"] = timeline.Level,
                    ["score"] = timeline.Score.ToString(), ["displayScore"] = timeline.DisplayScore.ToString(), ["finalScore"] = timeline.FinalScore.ToString(),
                    ["referenceScore"] = timeline.SuccessReferenceScore.ToString(), ["finaleAtMs"] = timeline.FinaleAtMs,
                    ["resultAtMs"] = timeline.ResultAtMs, ["resultVisible"] = timeline.ResultVisible,
                    ["pose"] = JObject.FromObject(timeline.Evaluate()),
                    ["levelEvents"] = JArray.FromObject(timeline.LevelEvents), ["activeItems"] = timeline.Items.Count };
            }
            if (recordDirectory == null) state["buttons"] = new JArray(Boot.Router.CurrentView.Query<Button>().ToList().Select(button => new JObject {
                ["name"] = button.name, ["text"] = button.text, ["enabled"] = button.enabledInHierarchy,
                ["display"] = button.resolvedStyle.display.ToString(), ["x"] = button.worldBound.x, ["y"] = button.worldBound.y,
                ["width"] = button.worldBound.width, ["height"] = button.worldBound.height }));
            return state;
        }
        private static string Hash(string value)
        { using (SHA256 hash = SHA256.Create()) return BitConverter.ToString(hash.ComputeHash(Encoding.UTF8.GetBytes(value))).Replace("-", string.Empty); }
        private static JObject ConsoleCounts()
        {
            Type entries = typeof(EditorApplication).Assembly.GetType("UnityEditor.LogEntries"); object[] counts = { 0, 0, 0 };
            entries.GetMethod("GetCountsByType", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic).Invoke(null, counts);
            return new JObject { ["errors"] = (int)counts[0], ["warnings"] = (int)counts[1], ["logs"] = (int)counts[2] };
        }
        private static void WriteResult(string status) => File.WriteAllText(Result, new JObject { ["status"] = status }.ToString());
        private static JObject RequiredProgress(string material, BigInteger required, BigInteger current) => new JObject {
            ["material"] = material, ["name"] = UiTheme.Config.Material(material).Name,
            ["required"] = required.ToString(), ["current"] = current.ToString(), ["met"] = current >= required };
        private static IEnumerable<VisualElement> Descendants(VisualElement root)
        { yield return root; foreach (VisualElement child in root.hierarchy.Children()) foreach (VisualElement element in Descendants(child)) yield return element; }
    }
}
