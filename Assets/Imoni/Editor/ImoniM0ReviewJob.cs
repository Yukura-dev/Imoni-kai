using System;
using System.IO;
using System.Reflection;
using Imoni.Game;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Imoni.Editor
{
    /// <summary>Local, bounded M0 verification jobs. No player/gameplay state is read or changed.</summary>
    [InitializeOnLoad]
    public static class ImoniM0ReviewJob
    {
        private const string RequestPath = "Library/ImoniM0Review.request";
        private const string ResultPath = "Library/ImoniM0Review.result";
        private const string ScreenshotPath = "Library/M0Boot390x844.png";
        private static double playStarted;
        private static bool screenshotRequested;

        static ImoniM0ReviewJob()
        {
            EditorApplication.update += Tick;
            EditorApplication.playModeStateChanged += OnPlayModeChanged;
        }

        private static void Tick()
        {
            if (SessionState.GetBool("ImoniM0.ReviewPlay", false) && EditorApplication.isPlaying)
            {
                CheckPlay();
                return;
            }
            if (EditorApplication.isCompiling || EditorApplication.isUpdating ||
                EditorApplication.isPlayingOrWillChangePlaymode || !File.Exists(RequestPath)) return;
            string command;
            try
            {
                command = File.ReadAllText(RequestPath).Trim();
                if (command.Length == 0) return;
                File.Delete(RequestPath);
            }
            catch (IOException)
            {
                // A local writer may still own the request; retry on the next editor update.
                return;
            }
            try
            {
                if (command == "setup") ImoniSetup.SetupProject();
                else if (command == "validate") ImoniM0Validation.Validate();
                else if (command == "m2a-validate") ImoniM2AValidation.Validate();
                else if (command == "gallery") { ConfigureGameView(); ImoniSetup.OpenComponentGallery(); }
                else if (command == "play")
                {
                    if (SceneManager.GetActiveScene().path != "Assets/Imoni/Scenes/Boot.unity")
                        throw new InvalidOperationException("Open Boot with Imoni > Setup Project first.");
                    ConfigureGameView();
                    if (File.Exists(ScreenshotPath)) File.Delete(ScreenshotPath);
                    SessionState.SetBool("ImoniM0.ReviewPlay", true);
                    EditorApplication.isPlaying = true;
                }
                else throw new InvalidOperationException("Unknown M0 review command: " + command);
                File.WriteAllText(ResultPath, command + ": complete");
            }
            catch (Exception exception)
            {
                File.WriteAllText(ResultPath, command + ": " + exception);
                Debug.LogException(exception);
            }
        }

        private static void OnPlayModeChanged(PlayModeStateChange state)
        {
            if (state == PlayModeStateChange.EnteredPlayMode)
            {
                playStarted = EditorApplication.timeSinceStartup;
                screenshotRequested = false;
            }
            if (state == PlayModeStateChange.EnteredEditMode) SessionState.SetBool("ImoniM0.ReviewPlay", false);
        }

        private static void CheckPlay()
        {
            var elapsed = EditorApplication.timeSinceStartup - playStarted;
            if (elapsed < 1) return;
            try
            {
                var bootstrap = UnityEngine.Object.FindFirstObjectByType<Bootstrap>();
                var config = JObject.Parse(File.ReadAllText("Assets/Imoni/Config/GameConfig.json"));
                int width = (int)config["setup"]["panel"]["referenceWidth"];
                int height = (int)config["setup"]["panel"]["referenceHeight"];
                if (!bootstrap || Screen.width != width || Screen.height != height)
                    throw new InvalidOperationException($"Boot/viewport mismatch: {Screen.width}x{Screen.height}.");
                if (!screenshotRequested)
                {
                    ScreenCapture.CaptureScreenshot(ScreenshotPath);
                    screenshotRequested = true;
                    return;
                }
                if (!File.Exists(ScreenshotPath))
                {
                    if (elapsed > 20) throw new TimeoutException("Game-view screenshot was not saved.");
                    return;
                }
                var bytes = File.ReadAllBytes(ScreenshotPath);
                if (bytes.Length < 24) return;
                int pngWidth = ReadPngInt(bytes, 16), pngHeight = ReadPngInt(bytes, 20);
                if (pngWidth != width || pngHeight != height) throw new InvalidOperationException("Screenshot dimensions differ from Game view.");
                File.WriteAllText("Library/M0PlayValidation.json", new JObject {
                    ["passed"] = true, ["scene"] = SceneManager.GetActiveScene().path,
                    ["screenWidth"] = Screen.width, ["screenHeight"] = Screen.height,
                    ["screenshot"] = ScreenshotPath, ["pngWidth"] = pngWidth, ["pngHeight"] = pngHeight
                }.ToString());
                File.WriteAllText(ResultPath, "play: verified Boot at " + width + "x" + height);
                SessionState.SetBool("ImoniM0.ReviewPlay", false);
                EditorApplication.isPlaying = false;
            }
            catch (Exception exception)
            {
                File.WriteAllText(ResultPath, "play: " + exception);
                Debug.LogException(exception);
                SessionState.SetBool("ImoniM0.ReviewPlay", false);
                EditorApplication.isPlaying = false;
            }
        }

        private static int ReadPngInt(byte[] bytes, int offset) =>
            bytes[offset] << 24 | bytes[offset + 1] << 16 | bytes[offset + 2] << 8 | bytes[offset + 3];

        private static void ConfigureGameView()
        {
            var flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance;
            var assembly = typeof(UnityEditor.Editor).Assembly;
            var type = assembly.GetType("UnityEditor.GameView");
            var view = EditorWindow.GetWindow(type);
            var sizesType = assembly.GetType("UnityEditor.GameViewSizes");
            var sizes = typeof(ScriptableSingleton<>).MakeGenericType(sizesType).GetProperty("instance").GetValue(null);
            var groupType = assembly.GetType("UnityEditor.GameViewSizeGroupType");
            var group = sizesType.GetMethod("GetGroup").Invoke(sizes, new[] { Enum.Parse(groupType, "Standalone") });
            var labels = (string[])group.GetType().GetMethod("GetDisplayTexts", flags).Invoke(group, null);
            var panel = JObject.Parse(File.ReadAllText("Assets/Imoni/Config/GameConfig.json"))["setup"]["panel"];
            int width = (int)panel["referenceWidth"], height = (int)panel["referenceHeight"], index = -1;
            for (int i = 0; i < labels.Length; i++)
            {
                var size = group.GetType().GetMethod("GetGameViewSize", flags).Invoke(group, new object[] { i });
                var sizeType = size.GetType();
                if ((int)sizeType.GetProperty("width", flags).GetValue(size) == width &&
                    (int)sizeType.GetProperty("height", flags).GetValue(size) == height &&
                    sizeType.GetProperty("sizeType", flags).GetValue(size).ToString() == "FixedResolution") { index = i; break; }
            }
            if (index < 0)
            {
                var sizeType = assembly.GetType("UnityEditor.GameViewSize");
                var kind = assembly.GetType("UnityEditor.GameViewSizeType");
                var size = Activator.CreateInstance(sizeType, flags | BindingFlags.CreateInstance, null,
                    new object[] { Enum.Parse(kind, "FixedResolution"), width, height, "Imoni M0" }, null);
                group.GetType().GetMethod("AddCustomSize", flags).Invoke(group, new[] { size });
                index = labels.Length;
            }
            type.GetProperty("selectedSizeIndex", flags).SetValue(view, index);
            view.Focus(); view.Repaint();
        }
    }
}
