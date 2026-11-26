using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Imoni.Game;
using Imoni.Game.UI;
using Imoni.Game.UI.Screens;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;
using DeviceScreen = UnityEngine.Device.Screen;

namespace Imoni.Editor
{
    /// <summary>Editor-only review bridge. Real Simulator devices, real screen routes, isolated profile.</summary>
    [InitializeOnLoad]
    public static class ImoniM7ReviewJob
    {
        private const string Request = "Library/M7Review.request", Result = "Library/M7Review.result";
        private const BindingFlags Flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance;
        private static string capture;
        private static int captureFrame;
        private static bool requested;
        private static JObject audit;
        private static Bootstrap Boot => UnityEngine.Object.FindFirstObjectByType<Bootstrap>();
        static ImoniM7ReviewJob() { EditorApplication.update += Update; }
        private static void Update()
        {
            if (EditorApplication.isCompiling || EditorApplication.isUpdating) return;
            try
            {
                if (capture != null)
                {
                    if (!requested && Time.frameCount >= captureFrame + 2)
                    { audit = Audit(); ScreenCapture.CaptureScreenshot(capture); requested = true; }
                    else if (requested && File.Exists(capture) && new FileInfo(capture).Length > 0)
                    { File.WriteAllText(Path.ChangeExtension(capture, ".json"), audit.ToString()); Write(new JObject { ["status"] = "captured", ["path"] = capture }); capture = null; }
                    return;
                }
                if (!File.Exists(Request)) return;
                JObject command;
                try { command = JObject.Parse(File.ReadAllText(Request)); File.Delete(Request); }
                catch (IOException) { return; }
                Run(command);
            }
            catch (Exception e) { capture = null; Write(new JObject { ["status"] = "failed", ["failure"] = e.ToString() }); Debug.LogException(e); }
        }
        private static void Run(JObject request)
        {
            switch (request["command"].Value<string>())
            {
                case "open":
                    if (EditorApplication.isPlaying) throw new InvalidOperationException("Exit Play Mode first.");
                    ConfigureGame(); ImoniSetup.SetupProject();
                    SessionState.SetString("Imoni.ReviewSavePath", Path.GetFullPath("Library/" + (request["profile"]?.Value<string>() ?? "M7-profile.json")));
                    SessionState.SetString("Imoni.StartScreen", request["screen"]?.Value<string>() ?? "Main");
                    EditorApplication.isPlaying = true; WriteStatus("opening"); break;
                case "stop": EditorApplication.isPlaying = false; WriteStatus("stopping"); break;
                case "record-flow": ImoniM7FlowRecording.Start(); WriteStatus("recording opening"); break;
                case "flow-status": Write(ImoniM7FlowRecording.Status()); break;
                case "setup": ImoniSetup.SetupProject(); WriteStatus("setup complete"); break;
                case "build": ImoniAndroidSetup.BuildAndroidVariant(request["variant"]?.Value<string>()); Write(JObject.Parse(File.ReadAllText("Library/M7-android-build.json"))); break;
                case "build-support": Write(new JObject { ["androidSupported"] = BuildPipeline.IsBuildTargetSupported(BuildTargetGroup.Android, BuildTarget.Android), ["activeTarget"] = EditorUserBuildSettings.activeBuildTarget.ToString() }); break;
                case "devices": Write(Devices()); break;
                case "simulator":
                    SelectDevice(request["device"].Value<string>()); WriteStatus("device selected"); break;
                case "game-view":
                    foreach (EditorWindow window in Resources.FindObjectsOfTypeAll<EditorWindow>().Where(w => w.GetType().FullName == "UnityEditor.DeviceSimulation.SimulatorWindow")) window.Close();
                    ConfigureGame(); WriteStatus("Game view selected"); break;
                case "reconnect":
                    (Boot.Router.CurrentView as GameScreen)?.BeginReconnectDemo(request["actor"]?.Value<string>());
                    WriteStatus("reconnecting"); break;
                case "capture":
                    string name = request["name"].Value<string>();
                    if (name != Path.GetFileName(name)) throw new ArgumentException("Use a simple capture name.");
                    capture = Path.GetFullPath("Library/M7-" + name + ".png");
                    if (File.Exists(capture)) File.Delete(capture);
                    captureFrame = Time.frameCount; requested = false; break;
                case "audit": Write(Audit()); break;
                case "audit-ui": Write(new JObject { ["screen"] = Boot.Router.CurrentScreen, ["elements"] = new JArray(InspectUi(Boot.Router.CurrentView)) }); break;
                default: throw new ArgumentException("Unknown M7 review command.");
            }
        }
        private static void ConfigureGame() => typeof(ImoniM0ReviewJob).GetMethod("ConfigureGameView", BindingFlags.Static | BindingFlags.NonPublic).Invoke(null, null);
        private static EditorWindow Simulator()
        {
            Type type = AppDomain.CurrentDomain.GetAssemblies().Select(a => a.GetType("UnityEditor.DeviceSimulation.SimulatorWindow")).FirstOrDefault(t => t != null);
            if (type == null) type = Assembly.Load("UnityEditor.DeviceSimulatorModule").GetType("UnityEditor.DeviceSimulation.SimulatorWindow");
            EditorWindow window = EditorWindow.GetWindow(type); window.Show(); window.Focus(); return window;
        }
        private static object Main(EditorWindow window) => window.GetType().GetProperty("main", Flags).GetValue(window);
        private static string DeviceName(object asset)
        {
            object info = asset.GetType().GetField("deviceInfo", Flags).GetValue(asset);
            return (string)info.GetType().GetField("friendlyName", Flags).GetValue(info);
        }
        private static JObject Devices()
        {
            object main = Main(Simulator());
            return new JObject { ["devices"] = new JArray(((IEnumerable)main.GetType().GetProperty("devices", Flags).GetValue(main)).Cast<object>().Select(DeviceName)) };
        }
        private static void SelectDevice(string name)
        {
            EditorWindow window = Simulator(); object main = Main(window);
            object[] devices = ((IEnumerable)main.GetType().GetProperty("devices", Flags).GetValue(main)).Cast<object>().ToArray();
            int index = Array.FindIndex(devices, d => DeviceName(d) == name);
            if (index < 0) throw new ArgumentException("Unknown Simulator device: " + name);
            main.GetType().GetProperty("deviceIndex", Flags).SetValue(main, index);
            window.Repaint();
        }
        private static JObject Bounds(Rect rect) => new JObject { ["x"] = rect.x, ["y"] = rect.y, ["width"] = rect.width, ["height"] = rect.height };
        private static JObject Audit()
        {
            VisualElement root = Boot.UiDocument.rootVisualElement, safe = root.Q("ScreenContent"), view = Boot.Router.CurrentView;
            Rect bounds = safe.worldBound;
            var buttons = view.Query<Button>().ToList().Where(Visible).Select(button => new JObject {
                ["name"] = button.name, ["text"] = button.text, ["bounds"] = Bounds(button.worldBound),
                ["scrollContent"] = HasScrollParent(button),
                ["insideSafeArea"] = Contains(bounds, button.worldBound)
            }).ToArray();
            var game = view as GameScreen;
            return new JObject { ["screen"] = Boot.Router.CurrentScreen,
                ["connectionState"] = game == null ? null : new JObject {
                    ["reconnecting"] = game.IsReconnecting, ["pending"] = game.ReconnectDemoPending,
                    ["elapsed"] = game.Snapshot.ElapsedSeconds,
                    ["players"] = new JArray(game.Snapshot.Players.Select(player => new JObject {
                        ["id"] = player.Id, ["connected"] = player.Connected, ["action"] = player.Action.ToString() })) },
                ["resolution"] = new JObject { ["width"] = DeviceScreen.width, ["height"] = DeviceScreen.height },
                ["nativeResolution"] = new JObject { ["width"] = Screen.width, ["height"] = Screen.height },
                ["safeAreaPixels"] = Bounds(DeviceScreen.safeArea), ["nativeSafeArea"] = Bounds(Screen.safeArea),
                ["contentBounds"] = Bounds(bounds), ["viewBounds"] = Bounds(view.worldBound),
                ["viewInsideSafeArea"] = Contains(bounds, view.worldBound), ["buttons"] = new JArray(buttons),
                ["outsideButtons"] = new JArray(buttons.Where(b => !b["insideSafeArea"].Value<bool>() && !b["scrollContent"].Value<bool>()).Select(b => b["name"])),
                ["console"] = (JObject)typeof(ImoniM6ReviewJob).GetMethod("ConsoleCounts", BindingFlags.Static | BindingFlags.NonPublic).Invoke(null, null) };
        }
        private static bool Contains(Rect outer, Rect inner) => inner.xMin >= outer.xMin - 0.5f && inner.yMin >= outer.yMin - 0.5f && inner.xMax <= outer.xMax + 0.5f && inner.yMax <= outer.yMax + 0.5f;
        private static IEnumerable<JObject> InspectUi(VisualElement element)
        {
            yield return new JObject { ["type"] = element.GetType().Name, ["name"] = element.name,
                ["classes"] = string.Join(" ", element.GetClasses()), ["text"] = (element as TextElement)?.text,
                ["bounds"] = Bounds(element.worldBound), ["layout"] = Bounds(element.layout),
                ["display"] = element.resolvedStyle.display.ToString(), ["visibility"] = element.resolvedStyle.visibility.ToString(),
                ["height"] = element.resolvedStyle.height, ["flexShrink"] = element.resolvedStyle.flexShrink,
                ["overflowInline"] = element.style.overflow.ToString(), ["children"] = element.hierarchy.childCount,
                ["scrollY"] = (element as ScrollView)?.scrollOffset.y };
            foreach (VisualElement child in element.hierarchy.Children())
                foreach (JObject item in InspectUi(child)) yield return item;
        }
        private static bool Visible(VisualElement item)
        { for (VisualElement p = item; p != null; p = p.parent) if (p.resolvedStyle.display == DisplayStyle.None || p.resolvedStyle.visibility == Visibility.Hidden || p.resolvedStyle.opacity <= 0) return false; return item.worldBound.width > 0 && item.worldBound.height > 0; }
        private static bool HasScrollParent(VisualElement item)
        { for (VisualElement p = item.parent; p != null; p = p.parent) if (p is ScrollView) return true; return false; }
        private static void WriteStatus(string status) => Write(new JObject { ["status"] = status });
        private static void Write(JObject value) => File.WriteAllText(Result, value.ToString());
    }
}
