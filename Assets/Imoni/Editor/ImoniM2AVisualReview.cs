using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Imoni.Game;
using Imoni.Game.UI;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace Imoni.Editor
{
    /// <summary>Local editor diagnostics for the rendered development gallery. No gameplay commands.</summary>
    [InitializeOnLoad]
    public static class ImoniM2AVisualReview
    {
        private const string RequestPath = "Library/M2AVisualReview.request";
        private const string ResultPath = "Library/M2AVisualReview.result";
        private const string RenderedPath = "Library/M2ARendered.json";
        private sealed class Page { public string Section; public int Number; public float Offset; }
        private static readonly Queue<Page> pending = new Queue<Page>();
        private static readonly List<string> captured = new List<string>();
        private static string command, currentSection, screenshotPath;
        private static int captureAfterFrame;
        private static bool screenshotRequested;
        private static double nextPoll, jobStarted;

        static ImoniM2AVisualReview() { EditorApplication.update += Tick; }

        private static void Tick()
        {
            if (EditorApplication.isCompiling || EditorApplication.isUpdating) return;
            try
            {
                if (command != null) { CaptureTick(); return; }
                if (EditorApplication.timeSinceStartup < nextPoll) return;
                nextPoll = EditorApplication.timeSinceStartup + .2;
                if (!File.Exists(RequestPath)) return;
                string request;
                try
                {
                    request = File.ReadAllText(RequestPath).Trim();
                    if (request.Length == 0) return;
                    File.Delete(RequestPath);
                }
                catch (IOException) { return; }
                if (request == "console")
                {
                    WriteResult(request, "complete", new JObject { ["console"] = ConsoleCounts() });
                    return;
                }
                ComponentGallery gallery = GetGallery();
                if (request == "inspect")
                {
                    File.WriteAllText(RenderedPath, Inspect(gallery).ToString(Formatting.Indented));
                    WriteResult(request, "complete", new JObject { ["report"] = Path.GetFullPath(RenderedPath) });
                    return;
                }
                captured.Clear(); pending.Clear();
                if (request == "all")
                {
                    float height = gallery.Q<ScrollView>("gallery-scroll").contentViewport.layout.height;
                    foreach (var section in gallery.Sections)
                        for (int page = 0; page < Math.Max(1, Mathf.CeilToInt(section.Value.layout.height / height)); page++)
                            pending.Enqueue(new Page { Section = section.Key, Number = page + 1, Offset = page * height });
                }
                else if (request.StartsWith("capture:", StringComparison.Ordinal))
                {
                    string section = request.Substring("capture:".Length);
                    if (!gallery.Sections.ContainsKey(section)) throw new ArgumentException("Unknown gallery section: " + section);
                    pending.Enqueue(new Page { Section = section, Number = 1 });
                }
                else throw new ArgumentException("Unknown M2a visual review request: " + request);
                command = request; currentSection = null;
                WriteResult(command, "running", new JObject { ["sections"] = pending.Count });
            }
            catch (Exception exception)
            {
                // Preserve the Console for zero-warning/error verification; diagnostics go only to their report.
                WriteResult(command ?? "request", "failed", new JObject { ["failure"] = exception.ToString() });
                command = null; currentSection = null; pending.Clear();
            }
        }

        private static ComponentGallery GetGallery()
        {
            if (!EditorApplication.isPlaying) throw new InvalidOperationException("The ComponentGallery must be running in Play Mode.");
            Bootstrap boot = UnityEngine.Object.FindFirstObjectByType<Bootstrap>();
            VisualElement view = boot ? boot.Router?.CurrentView : null;
            ComponentGallery gallery = view as ComponentGallery ?? view?.Q<ComponentGallery>();
            if (gallery?.panel == null) throw new InvalidOperationException("No attached ComponentGallery exists below Bootstrap.Router.CurrentView.");
            return gallery;
        }

        private static void CaptureTick()
        {
            ComponentGallery gallery = GetGallery();
            if (currentSection == null)
            {
                if (pending.Count == 0)
                {
                    WriteResult(command, "complete", new JObject { ["captures"] = new JArray(captured), ["console"] = ConsoleCounts() });
                    command = null; return;
                }
                Page page = pending.Dequeue(); currentSection = page.Section;
                if (!gallery.ScrollToSection(currentSection, page.Offset)) throw new InvalidOperationException("The gallery section disappeared: " + currentSection);
                string safeName = string.Concat(currentSection.Select(character => Path.GetInvalidFileNameChars().Contains(character) ? '_' : character));
                screenshotPath = Path.GetFullPath("Library/M2A-" + safeName + (page.Number > 1 ? "_" + page.Number.ToString("00") : "") + ".png");
                if (File.Exists(screenshotPath)) File.Delete(screenshotPath);
                screenshotRequested = false;
                captureAfterFrame = Time.frameCount + 3;
                jobStarted = EditorApplication.timeSinceStartup;
                EditorApplication.QueuePlayerLoopUpdate();
                return;
            }
            if (EditorApplication.timeSinceStartup - jobStarted > 30)
                throw new TimeoutException("Screenshot did not complete (check that Play Mode is not paused): " + currentSection);
            if (!screenshotRequested)
            {
                if (Time.frameCount < captureAfterFrame) return;
                ScreenCapture.CaptureScreenshot(screenshotPath);
                screenshotRequested = true;
                return;
            }
            if (!File.Exists(screenshotPath) || new FileInfo(screenshotPath).Length == 0) return;
            captured.Add(screenshotPath);
            WriteResult(command, "running", new JObject { ["lastSection"] = currentSection, ["captures"] = new JArray(captured), ["remaining"] = pending.Count });
            currentSection = null;
        }

        private static JObject Inspect(ComponentGallery gallery)
        {
            var resources = new JArray();
            foreach (string path in new[] { "UI/ComponentGallery", "UI/GalleryLayout", "UI/Components/Data", "UI/Components/Navigation", "UI/Components/Profile", "UI/Components/Effects" })
            {
                UnityEngine.Object[] all = Resources.LoadAll(path);
                resources.Add(new JObject
                {
                    ["requestedPath"] = path,
                    ["loadedStyleSheet"] = Asset(Resources.Load<StyleSheet>(path)),
                    ["loadedVisualTree"] = Asset(Resources.Load<VisualTreeAsset>(path)),
                    ["allMatchingAssets"] = new JArray(all.Select(Asset))
                });
            }
            var ancestors = new JArray();
            for (VisualElement element = gallery; element != null; element = element.hierarchy.parent) ancestors.Add(Element(element));
            var images = new JArray();
            foreach (VisualElement element in Descendants(gallery))
            {
                Background background = element.resolvedStyle.backgroundImage;
                UnityEngine.Object image = element is Image imageElement ? imageElement.image : null;
                if (!image) image = background.texture ? (UnityEngine.Object)background.texture : background.sprite ? background.sprite : background.vectorImage;
                if (!image) continue;
                images.Add(new JObject { ["element"] = Element(element), ["image"] = Asset(image) });
            }
            return new JObject
            {
                ["atUtc"] = DateTime.UtcNow.ToString("O"),
                ["screen"] = new JObject { ["width"] = Screen.width, ["height"] = Screen.height, ["frame"] = Time.frameCount },
                ["resources"] = resources,
                ["root"] = Element(gallery.panel.visualTree),
                ["gallery"] = Element(gallery),
                ["title"] = Element(gallery.Q<Label>(className: "gallery-title")),
                ["scroll"] = Element(gallery.Q<ScrollView>("gallery-scroll")),
                ["ancestors"] = ancestors,
                ["sections"] = new JArray(gallery.Sections.Select(pair => new JObject { ["name"] = pair.Key, ["element"] = Element(pair.Value), ["title"] = Element(pair.Value.Q<Label>(className: "gallery-section-title")) })),
                ["images"] = images,
                ["runtimeAssets"] = Asset(UiTheme.Assets),
                ["theme"] = Asset(UiTheme.Assets ? UiTheme.Assets.theme : null),
                ["console"] = ConsoleCounts()
            };
        }

        private static JObject Element(VisualElement element)
        {
            if (element == null) return null;
            IResolvedStyle style = element.resolvedStyle;
            var sheets = new JArray();
            for (int index = 0; index < element.styleSheets.count; index++) sheets.Add(Asset(element.styleSheets[index]));
            return new JObject
            {
                ["name"] = element.name, ["type"] = element.GetType().FullName,
                ["classes"] = new JArray(element.GetClasses()),
                ["text"] = (element as TextElement)?.text,
                ["styleSheets"] = sheets,
                ["layout"] = Rectangle(element.layout), ["worldBound"] = Rectangle(element.worldBound),
                ["style"] = new JObject
                {
                    ["display"] = style.display.ToString(), ["visibility"] = style.visibility.ToString(),
                    ["background"] = "#" + ColorUtility.ToHtmlStringRGBA(style.backgroundColor), ["color"] = "#" + ColorUtility.ToHtmlStringRGBA(style.color),
                    ["fontSize"] = style.fontSize, ["font"] = Asset(style.unityFontDefinition.fontAsset ? (UnityEngine.Object)style.unityFontDefinition.fontAsset : style.unityFont),
                    ["margin"] = new JArray(style.marginLeft, style.marginTop, style.marginRight, style.marginBottom),
                    ["padding"] = new JArray(style.paddingLeft, style.paddingTop, style.paddingRight, style.paddingBottom),
                    ["border"] = new JArray(style.borderLeftWidth, style.borderTopWidth, style.borderRightWidth, style.borderBottomWidth),
                    ["radius"] = new JArray(style.borderTopLeftRadius, style.borderTopRightRadius, style.borderBottomRightRadius, style.borderBottomLeftRadius),
                    ["opacity"] = style.opacity
                }
            };
        }

        private static JObject Asset(UnityEngine.Object value) => !value ? null : new JObject
        { ["name"] = value.name, ["type"] = value.GetType().FullName, ["path"] = AssetDatabase.GetAssetPath(value), ["instanceId"] = value.GetInstanceID() };

        private static JObject Rectangle(Rect rect) => new JObject { ["x"] = rect.x, ["y"] = rect.y, ["width"] = rect.width, ["height"] = rect.height };
        private static IEnumerable<VisualElement> Descendants(VisualElement root)
        {
            yield return root;
            foreach (VisualElement child in root.hierarchy.Children()) foreach (VisualElement descendant in Descendants(child)) yield return descendant;
        }

        private static JObject ConsoleCounts()
        {
            Type entries = typeof(EditorApplication).Assembly.GetType("UnityEditor.LogEntries");
            MethodInfo counts = entries?.GetMethod("GetCountsByType", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
            if (counts == null) return new JObject { ["available"] = false };
            object[] values = { 0, 0, 0 };
            counts.Invoke(null, values);
            // Unity's API order is (ref errorCount, ref warningCount, ref logCount).
            return new JObject { ["available"] = true, ["errors"] = (int)values[0], ["warnings"] = (int)values[1], ["logs"] = (int)values[2] };
        }

        private static void WriteResult(string request, string status, JObject details)
        {
            details["command"] = request; details["status"] = status; details["atUtc"] = DateTime.UtcNow.ToString("O");
            File.WriteAllText(ResultPath, details.ToString(Formatting.Indented));
        }
    }
}
