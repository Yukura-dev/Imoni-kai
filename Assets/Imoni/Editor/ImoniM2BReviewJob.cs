using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Imoni.Game;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace Imoni.Editor
{
    /// <summary>Bounded editor-only capture of the real Boot UI; all state changes use screen input handlers.</summary>
    [InitializeOnLoad]
    public static class ImoniM2BReviewJob
    {
        private const string Request = "Library/M2BReview.request";
        private const string Result = "Library/M2BReview.result";
        private static readonly Queue<JObject> shots = new Queue<JObject>();
        private static readonly JArray completed = new JArray();
        private static JObject current;
        private static double started;
        private static int frame;
        private static bool requested;
        private static string destination;
        static ImoniM2BReviewJob() { EditorApplication.update += Tick; }
        private static Bootstrap Boot => UnityEngine.Object.FindFirstObjectByType<Bootstrap>();
        private static void Tick()
        {
            if (EditorApplication.isCompiling || EditorApplication.isUpdating) return;
            try
            {
                if (current == null && shots.Count == 0 && File.Exists(Request))
                {
                    JObject request;
                    try { request = JObject.Parse(File.ReadAllText(Request)); File.Delete(Request); }
                    catch (IOException) { return; }
                    if (request["command"]?.Value<string>() == "validate")
                    { ImoniM2BValidation.Validate(); File.WriteAllText(Result, "validation complete"); return; }
                    if (!EditorApplication.isPlaying || !Boot || Boot.Router == null) throw new InvalidOperationException("Start Boot with Imoni > Open App first.");
                    if (request["command"]?.Value<string>() == "inspect")
                    { Inspect(); File.WriteAllText(Result, "inspection complete"); return; }
                    if (request["resetSession"]?.Value<bool>() == true)
                    { Boot.Context.JoinedTeam = false; Boot.Context.FreeSeed = false; Boot.Context.MemberCount = 1; }
                    completed.Clear();
                    foreach (JObject shot in request["shots"]) shots.Enqueue(shot);
                }
                if (current == null && shots.Count > 0)
                {
                    current = shots.Dequeue();
                    if (current["screen"] != null) Boot.Router.Show(current["screen"].Value<string>());
                    foreach (JObject action in current["actions"] ?? new JArray()) Act(action);
                    destination = Path.GetFullPath("Library/M2B-" + current["name"].Value<string>() + ".png");
                    if (File.Exists(destination)) File.Delete(destination);
                    started = EditorApplication.timeSinceStartup; frame = Time.frameCount; requested = false;
                }
                if (current == null) return;
                if (!EditorApplication.isPlaying) throw new InvalidOperationException("Play Mode ended during capture.");
                if (EditorApplication.timeSinceStartup - started > 30) throw new TimeoutException("Capture timed out.");
                if (!requested)
                {
                    if (Time.frameCount < frame + 3 || EditorApplication.timeSinceStartup - started < (current["waitSeconds"]?.Value<double>() ?? .1)) return;
                    ScreenCapture.CaptureScreenshot(destination); requested = true; return;
                }
                if (!File.Exists(destination) || new FileInfo(destination).Length == 0) return;
                completed.Add(new JObject { ["name"] = current["name"], ["screen"] = Boot.Router.CurrentScreen, ["image"] = destination, ["width"] = Screen.width, ["height"] = Screen.height });
                File.WriteAllText(Result, new JObject { ["status"] = shots.Count == 0 ? "complete" : "running", ["captures"] = completed }.ToString());
                current = null;
            }
            catch (Exception exception)
            { File.WriteAllText(Result, new JObject { ["status"] = "failed", ["failure"] = exception.ToString() }.ToString()); current = null; shots.Clear(); }
        }
        private static void Act(JObject action)
        {
            VisualElement view = Boot.Router.OverlayView ?? Boot.Router.CurrentView;
            string name = action["name"]?.Value<string>();
            switch (action["type"].Value<string>())
            {
                case "text":
                    TextField field = string.IsNullOrEmpty(name) ? view.Q<TextField>() : view.Q<TextField>(name);
                    if (field == null) throw new InvalidOperationException("Input missing: " + name);
                    field.value = action["value"].Value<string>(); break;
                case "click":
                    Button button = string.IsNullOrEmpty(name)
                        ? view.Query<Button>().ToList().FirstOrDefault(item => item.text == action["text"].Value<string>()) : view.Q<Button>(name);
                    if (button == null || !button.enabledInHierarchy) throw new InvalidOperationException("Enabled button missing: " + name);
                    InvokeClick(button); break;
                case "flip": view.Q<Imoni.Game.UI.Components.MemberCard>().Flip(); break;
                case "segment":
                    var segment = view.Q<Imoni.Game.UI.Components.SegmentedControl>(name);
                    InvokeClick(segment.Query<Button>().ToList()[action["index"].Value<int>()]); break;
                default: throw new InvalidOperationException("Unknown review action: " + action["type"]);
            }
        }
        private static void InvokeClick(Button button)
        {
            typeof(Clickable).GetMethod("Invoke", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
                .Invoke(button.clickable, new object[] { null });
        }
        private static void Inspect()
        {
            var entries = new JArray();
            foreach (VisualElement element in Descendants(Boot.Router.OverlayView ?? Boot.Router.CurrentView))
            {
                var style = element.resolvedStyle;
                entries.Add(new JObject { ["type"] = element.GetType().Name, ["name"] = element.name, ["text"] = (element as TextElement)?.text,
                    ["classes"] = new JArray(element.GetClasses()), ["x"] = element.worldBound.x, ["y"] = element.worldBound.y,
                    ["width"] = element.worldBound.width, ["height"] = element.worldBound.height, ["fontSize"] = style.fontSize,
                    ["font"] = style.unityFontDefinition.fontAsset ? style.unityFontDefinition.fontAsset.name : style.unityFont ? style.unityFont.name : "inherited/default",
                    ["display"] = style.display.ToString(), ["sheetCount"] = element.styleSheets.count });
            }
            File.WriteAllText("Library/M2BLayout.json", entries.ToString());
        }
        private static IEnumerable<VisualElement> Descendants(VisualElement root)
        { yield return root; foreach (var child in root.hierarchy.Children()) foreach (var item in Descendants(child)) yield return item; }
    }
}
