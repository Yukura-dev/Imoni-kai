using System;
using System.IO;
using System.Linq;
using System.Reflection;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;
using UnityEngine.Rendering;

namespace Imoni.Editor
{
    /// <summary>Reproducible portrait Android settings and a provisional icon from the supplied pot artwork.</summary>
    public static class ImoniAndroidSetup
    {
        private const string IconPath = "Assets/Imoni/Art/Logo/android_icon.png";
        public static void Configure()
        {
            JObject config = JObject.Parse(File.ReadAllText("Assets/Imoni/Config/GameConfig.json"));
            JToken android = config["setup"]["android"];
            if (android == null) throw new InvalidOperationException("Missing setup.android configuration.");
            PlayerSettings.SetApplicationIdentifier(NamedBuildTarget.Android, android["applicationIdentifier"].Value<string>());
            PlayerSettings.Android.bundleVersionCode = android["versionCode"].Value<int>();
            PlayerSettings.Android.minSdkVersion = (AndroidSdkVersions)android["minimumApiLevel"].Value<int>();
            PlayerSettings.Android.targetSdkVersion = (AndroidSdkVersions)android["targetApiLevel"].Value<int>();
            PlayerSettings.SetScriptingBackend(NamedBuildTarget.Android, ScriptingImplementation.IL2CPP);
            PlayerSettings.Android.targetArchitectures = AndroidArchitecture.ARM64;
            PlayerSettings.Android.renderOutsideSafeArea = true;
            ConfigureRendering(android);
            PlayerSettings.defaultInterfaceOrientation = UIOrientation.Portrait;
            PlayerSettings.allowedAutorotateToPortrait = true;
            PlayerSettings.allowedAutorotateToPortraitUpsideDown = false;
            PlayerSettings.allowedAutorotateToLandscapeLeft = false;
            PlayerSettings.allowedAutorotateToLandscapeRight = false;
            PlayerSettings.productName = "みんなで芋煮会";
            EnsureIcon(config, android);
        }

        private static void ConfigureRendering(JToken settings)
        {
            PlayerSettings.SetUseDefaultGraphicsAPIs(BuildTarget.Android, false);
            PlayerSettings.SetGraphicsAPIs(BuildTarget.Android, settings["graphicsApis"].Values<string>()
                .Select(value => (GraphicsDeviceType)Enum.Parse(typeof(GraphicsDeviceType), value)).ToArray());
            PlayerSettings.Android.optimizedFramePacing = settings["optimizedFramePacing"].Value<bool>();
        }

        private static void EnsureIcon(JObject config, JToken android)
        {
            int size = android["iconSize"].Value<int>();
            string key = android["iconTexture"].Value<string>();
            string sourcePath = Directory.GetFiles("Assets/Imoni/Art", key + ".png", SearchOption.AllDirectories).Single();
            Texture2D source = AssetDatabase.LoadAssetAtPath<Texture2D>(sourcePath.Replace('\\', '/'));
            RenderTexture previous = RenderTexture.active;
            RenderTexture target = RenderTexture.GetTemporary(size, size, 0, RenderTextureFormat.ARGB32);
            Texture2D icon = new Texture2D(size, size, TextureFormat.RGBA32, false);
            try
            {
                RenderTexture.active = target;
                ColorUtility.TryParseHtmlString(config["qualities"].First(q => q["id"].Value<string>() == "legend")["bg"].Value<string>(), out Color color);
                GL.Clear(true, true, color);
                GL.PushMatrix();
                GL.LoadPixelMatrix(0, size, size, 0);
                float height = size * (float)source.height / source.width;
                Graphics.DrawTexture(new Rect(0, (size - height) / 2, size, height), source);
                GL.PopMatrix();
                icon.ReadPixels(new Rect(0, 0, size, size), 0, 0); icon.Apply();
                byte[] bytes = icon.EncodeToPNG();
                if (!File.Exists(IconPath) || !File.ReadAllBytes(IconPath).SequenceEqual(bytes)) File.WriteAllBytes(IconPath, bytes);
            }
            finally { RenderTexture.active = previous; RenderTexture.ReleaseTemporary(target); UnityEngine.Object.DestroyImmediate(icon); }
            AssetDatabase.ImportAsset(IconPath, ImportAssetOptions.ForceSynchronousImport);
            var importer = (TextureImporter)AssetImporter.GetAtPath(IconPath);
            if (importer.textureType != TextureImporterType.Default || importer.mipmapEnabled || importer.textureCompression != TextureImporterCompression.Uncompressed)
            {
                importer.textureType = TextureImporterType.Default;
                importer.mipmapEnabled = false;
                importer.textureCompression = TextureImporterCompression.Uncompressed;
                importer.SaveAndReimport();
            }
            Texture2D texture = AssetDatabase.LoadAssetAtPath<Texture2D>(IconPath);
            // The default icon is used at all Android launcher densities when a platform override is absent.
            PlayerSettings.SetIcons(NamedBuildTarget.Unknown, new[] { texture }, IconKind.Any);
        }

        [MenuItem("Imoni/Build/Android APK")]
        public static void BuildAndroid() => BuildAndroidVariant(null);

        [MenuItem("Imoni/Build/Android GLES3 Frame Pacing On")]
        public static void BuildGlesOn() => BuildAndroidVariant("gles3-on");

        [MenuItem("Imoni/Build/Android GLES3 Frame Pacing Off")]
        public static void BuildGlesOff() => BuildAndroidVariant("gles3-off");

        public static void BuildAndroidVariant(string variant)
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Exit Play Mode before building.");
            ImoniSetup.SetupProject();
            JToken android = JObject.Parse(File.ReadAllText("Assets/Imoni/Config/GameConfig.json"))["setup"]["android"];
            JToken rendering = variant == null ? android : android["validationVariants"]?[variant]
                ?? throw new ArgumentException("Unknown Android validation variant: " + variant);
            ConfigureRendering(rendering);
            try
            {
            // Android is an optional Editor module. Resolve its current API only for this build.
            Type symbols = Type.GetType("UnityEditor.Android.UserBuildSettings+DebugSymbols, UnityEditor.Android.Extensions", true);
            PropertyInfo level = symbols.GetProperty("level", BindingFlags.Public | BindingFlags.Static);
            level.SetValue(null, Enum.Parse(level.PropertyType, "SymbolTable"));
            PropertyInfo format = symbols.GetProperty("format", BindingFlags.Public | BindingFlags.Static);
            format.SetValue(null, Enum.Parse(format.PropertyType, "Zip"));
            string path = variant == null ? "Builds/Android/Imoni.apk" : "Builds/Android/Imoni-" + variant + ".apk";
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            BuildReport report = BuildPipeline.BuildPlayer(new BuildPlayerOptions {
                scenes = new[] { "Assets/Imoni/Scenes/Boot.unity" }, locationPathName = path,
                target = BuildTarget.Android, options = BuildOptions.None
            });
            var result = new JObject { ["result"] = report.summary.result.ToString(), ["errors"] = report.summary.totalErrors,
                ["warnings"] = report.summary.totalWarnings, ["bytes"] = report.summary.totalSize,
                ["seconds"] = report.summary.totalTime.TotalSeconds, ["path"] = Path.GetFullPath(path),
                ["backend"] = "IL2CPP", ["architecture"] = "ARM64", ["orientation"] = PlayerSettings.defaultInterfaceOrientation.ToString(),
                ["graphicsApis"] = new JArray(PlayerSettings.GetGraphicsAPIs(BuildTarget.Android).Select(api => api.ToString())),
                ["optimizedFramePacing"] = PlayerSettings.Android.optimizedFramePacing,
                ["autoGraphicsApi"] = PlayerSettings.GetUseDefaultGraphicsAPIs(BuildTarget.Android),
                ["variant"] = variant ?? "default", ["physicalDeviceTested"] = false,
                ["messages"] = new JArray(report.steps.SelectMany(step => step.messages).Where(message => message.type == LogType.Error || message.type == LogType.Warning)
                    .Select(message => message.content)) };
            File.WriteAllText("Library/M7-android-build.json", result.ToString());
            File.WriteAllText("Library/M8-android-" + (variant ?? "default") + ".json", result.ToString());
            if (report.summary.result != BuildResult.Succeeded || report.summary.totalErrors > 0 || !File.Exists(path))
                throw new InvalidOperationException("Android build failed or APK missing; see Library/M7-android-build.json");
            }
            finally { ConfigureRendering(android); AssetDatabase.SaveAssets(); }
        }
    }
}
