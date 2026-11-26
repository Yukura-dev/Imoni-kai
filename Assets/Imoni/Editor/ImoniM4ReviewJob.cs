using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using Imoni.Core;
using Imoni.Game;
using Imoni.Game.UI.Components;
using Imoni.Game.UI.Screens;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace Imoni.Editor
{
    /// <summary>Bounded Editor-only inspection of real panel controls and authoritative state.</summary>
    [InitializeOnLoad]
    public static class ImoniM4ReviewJob
    {
        private const string Request = "Library/M4Review.request";
        private const string Result = "Library/M4Review.result";
        private static string capturePath;
        private static int captureFrame;
        private static bool captureRequested;
        private static JObject captureState;
        private static Bootstrap Boot => UnityEngine.Object.FindFirstObjectByType<Bootstrap>();
        private static GameScreen Game => Boot?.Router?.CurrentView as GameScreen;

        static ImoniM4ReviewJob() { EditorApplication.update += Update; }

        private static void Update()
        {
            if (EditorApplication.isCompiling || EditorApplication.isUpdating) return;
            try
            {
                if (capturePath != null) CompleteCapture();
                if (capturePath != null || !File.Exists(Request)) return;
                JObject command;
                try { command = JObject.Parse(File.ReadAllText(Request)); File.Delete(Request); }
                catch (IOException) { return; }
                Run(command);
            }
            catch (Exception exception)
            {
                capturePath = null;
                File.WriteAllText(Result, new JObject { ["status"] = "failed", ["failure"] = exception.ToString() }.ToString());
                Debug.LogException(exception);
            }
        }

        private static void Run(JObject request)
        {
            string command = request["command"]?.Value<string>();
            if (command == "console") { File.WriteAllText(Result, ConsoleCounts().ToString()); return; }
            if (!EditorApplication.isPlaying || Boot?.Router == null) throw new InvalidOperationException("Start Boot first.");
            if (command == "prepare")
            {
                Boot.Context.PrepareGame(request["seed"]?.Value<int>());
                Boot.Router.Show("Game"); Game.ReviewPaused = true;
                WriteResult("prepared"); return;
            }
            if (command == "inspect") { SaveInspection(); return; }
            if (Game == null) throw new InvalidOperationException("Open Game first.");
            switch (command)
            {
                case "pause": Game.ReviewPaused = request["paused"]?.Value<bool>() ?? true; WriteResult("pause changed"); break;
                case "step":
                    Game.ReviewPaused = true;
                    decimal seconds = request["seconds"].Value<decimal>();
                    Game.Server.Tick(seconds); Game.RefreshSnapshot((float)seconds); WriteResult("stepped"); break;
                case "open": Game.OpenPanel(request["panel"].Value<string>()); WriteResult("opened"); break;
                case "close": Game.ClosePanel(); WriteResult("closed"); break;
                case "select":
                    Game.Panels.SelectInventory(request["material"].Value<string>(), request["quality"].Value<string>());
                    WriteResult("selected"); break;
                case "input":
                    InputEntry input = Game.Q<InputEntry>(request["name"].Value<string>());
                    if (input == null || !input.enabledInHierarchy) throw new InvalidOperationException("Input unavailable.");
                    input.value = request["value"].Value<string>(); WriteResult("input changed"); break;
                case "click":
                    Button button = Game.Q<Button>(request["name"].Value<string>());
                    if (button == null) throw new InvalidOperationException("Button not found.");
                    if (!button.enabledInHierarchy) { WriteResult("disabled button rejected"); break; }
                    MethodInfo invoke = typeof(Clickable).GetMethod("Invoke", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic,
                        null, new[] { typeof(EventBase) }, null);
                    if (invoke == null) throw new InvalidOperationException("Clickable.Invoke unavailable.");
                    using (ClickEvent click = ClickEvent.GetPooled()) { click.target = button; invoke.Invoke(button.clickable, new object[] { click }); }
                    WriteResult("clicked"); break;
                case "disconnect":
                    Game.Server.SetConnected(request["actor"].Value<string>(), request["connected"].Value<bool>());
                    Game.RefreshSnapshot(); WriteResult("connection changed"); break;
                case "capture":
                    Game.ReviewPaused = true; Game.RefreshSnapshot(); BeginCapture(request["name"].Value<string>()); break;
                default: throw new ArgumentException("Unknown M4 review command: " + command);
            }
        }

        private static void BeginCapture(string name)
        {
            if (name != Path.GetFileName(name)) throw new ArgumentException("Use a filename without a directory.");
            capturePath = Path.GetFullPath("Library/M4-" + name + ".png");
            if (File.Exists(capturePath)) File.Delete(capturePath);
            captureFrame = Time.frameCount; captureRequested = false;
        }

        private static void CompleteCapture()
        {
            if (!captureRequested)
            {
                if (Time.frameCount < captureFrame + 2) return;
                captureState = Describe();
                captureState["image"] = Path.GetFileName(capturePath);
                ScreenCapture.CaptureScreenshot(capturePath); captureRequested = true; return;
            }
            if (!File.Exists(capturePath) || new FileInfo(capturePath).Length == 0) return;
            File.WriteAllText(Path.ChangeExtension(capturePath, ".json"), captureState.ToString());
            File.WriteAllText(Result, new JObject { ["status"] = "complete", ["image"] = capturePath,
                ["panel"] = captureState["panel"], ["funds"] = captureState["funds"], ["console"] = captureState["console"] }.ToString());
            capturePath = null;
        }

        private static JObject Describe()
        {
            var result = new JObject { ["utc"] = DateTime.UtcNow.ToString("O"), ["width"] = Screen.width, ["height"] = Screen.height,
                ["screen"] = Boot.Router.CurrentScreen, ["console"] = ConsoleCounts() };
            if (Game == null) return result;
            GameSnapshot state = Game.Server.Snapshot();
            result["panel"] = Game.CurrentPanel; result["elapsed"] = state.ElapsedSeconds;
            result["phase"] = state.Phase.ToString(); result["funds"] = state.Funds.ToString(CultureInfo.InvariantCulture);
            result["worldInputEnabled"] = Game.WorldView?.InputEnabled;
            result["toastVisible"] = Game.Q<Toast>("GameToast").resolvedStyle.display != DisplayStyle.None;
            result["saleQuantity"] = Game.Panels.SaleQuantityText;
            result["selectedInventory"] = Game.Panels.SelectedInventoryKey;
            result["popups"] = new JArray(Game.Query<Label>().ToList().Where(label => label.ClassListContains("world-gather-popup"))
                .Select(label => new JObject { ["text"] = label.text, ["display"] = label.resolvedStyle.display.ToString() }));
            if (Game.WorldView != null)
            {
                result["growthLabels"] = new JArray(Game.WorldView.GrowthLabelRects.Keys);
                result["lastTappedResource"] = Game.WorldView.LastTappedResourceId;
                result["growthLabelDistanceTiles"] = Imoni.Game.UI.UiTheme.Data["ui"]["worldView"]["growthLabelDistanceTiles"].Value<float>();
            }
            result["players"] = new JArray(state.Players.Select(player => new JObject { ["id"] = player.Id, ["connected"] = player.Connected,
                ["action"] = player.Action.ToString(), ["x"] = player.Position.X, ["y"] = player.Position.Y,
                ["upgrades"] = JObject.FromObject(player.Upgrades) }));
            result["inventory"] = new JArray(GamePanelProjection.Inventory(Imoni.Game.UI.UiTheme.Config, state).Select(row => new JObject {
                ["material"] = row.MaterialId, ["quality"] = row.QualityId, ["quantity"] = row.Quantity.ToString(),
                ["protected"] = row.Protected.ToString(), ["sellable"] = row.Sellable.ToString(), ["unitPrice"] = row.UnitPrice.ToString() }));
            result["history"] = new JArray(state.History.Select(record => new JObject { ["sequence"] = record.Sequence,
                ["actor"] = record.ActorId, ["kind"] = record.Kind.ToString(), ["material"] = record.MaterialId,
                ["quality"] = record.QualityId, ["quantity"] = record.Quantity.ToString(), ["time"] = record.ElapsedSeconds }));
            result["buttons"] = new JArray(Game.Query<ImoniButton>().ToList().Where(button => button.worldBound.width > 0).Select(button => new JObject {
                ["name"] = button.name, ["text"] = button.text, ["enabled"] = button.enabledInHierarchy,
                ["x"] = button.worldBound.x, ["y"] = button.worldBound.y, ["width"] = button.worldBound.width, ["height"] = button.worldBound.height }));
            return result;
        }

        private static void SaveInspection()
        {
            Game?.RefreshSnapshot();
            JObject result = Describe();
            result["layout"] = new JArray(Descendants(Boot.Router.CurrentView).Select(element => new JObject { ["type"] = element.GetType().Name,
                ["name"] = element.name, ["text"] = (element as TextElement)?.text, ["classes"] = new JArray(element.GetClasses()),
                ["x"] = element.worldBound.x, ["y"] = element.worldBound.y, ["width"] = element.worldBound.width, ["height"] = element.worldBound.height,
                ["display"] = element.resolvedStyle.display.ToString(), ["enabled"] = element.enabledInHierarchy }));
            File.WriteAllText("Library/M4-inspect.json", result.ToString()); WriteResult("inspected");
        }
        private static JObject ConsoleCounts()
        {
            Type entries = typeof(EditorApplication).Assembly.GetType("UnityEditor.LogEntries"); object[] counts = { 0, 0, 0 };
            entries.GetMethod("GetCountsByType", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic).Invoke(null, counts);
            return new JObject { ["errors"] = (int)counts[0], ["warnings"] = (int)counts[1], ["logs"] = (int)counts[2] };
        }
        private static void WriteResult(string status) => File.WriteAllText(Result, new JObject { ["status"] = status }.ToString());
        private static IEnumerable<VisualElement> Descendants(VisualElement root)
        { yield return root; foreach (VisualElement child in root.hierarchy.Children()) foreach (VisualElement element in Descendants(child)) yield return element; }
    }
}
