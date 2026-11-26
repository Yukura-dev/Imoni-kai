using System;
using System.IO;
using System.Linq;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

namespace Imoni.Editor
{
    /// <summary>Config-driven iPhone settings. Xcode signing and installation remain on the user's Mac.</summary>
    public static class ImoniIosSetup
    {
        private static JToken Settings => JObject.Parse(File.ReadAllText("Assets/Imoni/Config/GameConfig.json"))["setup"]["ios"];

        public static void Configure()
        {
            JToken ios = Settings;
            PlayerSettings.SetApplicationIdentifier(NamedBuildTarget.iOS, ios["applicationIdentifier"].Value<string>());
            PlayerSettings.SetScriptingBackend(NamedBuildTarget.iOS, ScriptingImplementation.IL2CPP);
            PlayerSettings.iOS.buildNumber = ios["buildNumber"].Value<string>();
            PlayerSettings.iOS.targetOSVersionString = ios["minimumVersion"].Value<string>();
            PlayerSettings.iOS.targetDevice = (iOSTargetDevice)Enum.Parse(typeof(iOSTargetDevice), ios["targetDevice"].Value<string>());
            PlayerSettings.iOS.sdkVersion = (iOSSdkVersion)Enum.Parse(typeof(iOSSdkVersion), ios["sdkVersion"].Value<string>());
            PlayerSettings.iOS.appleEnableAutomaticSigning = ios["automaticSigning"].Value<bool>();
            // Do not replace the developer's locally selected Apple Team.
            // Without the optional iOS module Unity filters Metal out and emits a warning.
            // Apply the API override when Setup runs in the export-capable Editor on the Mac.
            if (BuildPipeline.IsBuildTargetSupported(BuildTargetGroup.iOS, BuildTarget.iOS))
            {
                PlayerSettings.SetUseDefaultGraphicsAPIs(BuildTarget.iOS, false);
                PlayerSettings.SetGraphicsAPIs(BuildTarget.iOS, ios["graphicsApis"].Values<string>()
                    .Select(value => (GraphicsDeviceType)Enum.Parse(typeof(GraphicsDeviceType), value)).ToArray());
            }
        }

        [MenuItem("Imoni/Build/iPhone Xcode Project")]
        public static void ExportXcode()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("Exit Play Mode before exporting iPhone.");
            if (!BuildPipeline.IsBuildTargetSupported(BuildTargetGroup.iOS, BuildTarget.iOS))
                throw new InvalidOperationException("Install iOS Build Support for Unity 6000.3.21f1 in Unity Hub first. See docs/IOS_DEVICE_SETUP.md.");
            if (EditorUserBuildSettings.activeBuildTarget != BuildTarget.iOS)
                throw new InvalidOperationException("Select iOS in File > Build Profiles, then Switch Platform. Run this menu after import/compilation finishes.");
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            ImoniSetup.SetupProject();
            string path = Settings["exportPath"].Value<string>();
            Directory.CreateDirectory(path);
            BuildReport report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
            {
                scenes = new[] { "Assets/Imoni/Scenes/Boot.unity" },
                locationPathName = path, target = BuildTarget.iOS, options = BuildOptions.None
            });
            bool generated = File.Exists(Path.Combine(path, "Unity-iPhone.xcodeproj/project.pbxproj"));
            var result = new JObject
            {
                ["result"] = report.summary.result.ToString(), ["errors"] = report.summary.totalErrors,
                ["warnings"] = report.summary.totalWarnings, ["seconds"] = report.summary.totalTime.TotalSeconds,
                ["path"] = Path.GetFullPath(path), ["xcodeProjectGenerated"] = generated,
                ["signedApplicationBuilt"] = false, ["physicalDeviceTested"] = false
            };
            File.WriteAllText("Library/iOS-export.json", result.ToString());
            if (report.summary.result != BuildResult.Succeeded || report.summary.totalErrors > 0 || !generated)
                throw new InvalidOperationException("iOS export failed. See Library/iOS-export.json.");
            Debug.Log("iPhone Xcode project exported to " + Path.GetFullPath(path) + ". Open it on your Mac, select your signing Team and iPhone, then Run.");
        }

        public static JObject InspectSettings()
        {
            return new JObject
            {
                ["unityVersion"] = Application.unityVersion,
                ["iOSBuildSupportInstalled"] = BuildPipeline.IsBuildTargetSupported(BuildTargetGroup.iOS, BuildTarget.iOS),
                ["activeTarget"] = EditorUserBuildSettings.activeBuildTarget.ToString(),
                ["applicationIdentifier"] = PlayerSettings.GetApplicationIdentifier(NamedBuildTarget.iOS),
                ["backend"] = PlayerSettings.GetScriptingBackend(NamedBuildTarget.iOS).ToString(),
                ["buildNumber"] = PlayerSettings.iOS.buildNumber,
                ["minimumVersion"] = PlayerSettings.iOS.targetOSVersionString,
                ["device"] = PlayerSettings.iOS.targetDevice.ToString(), ["sdk"] = PlayerSettings.iOS.sdkVersion.ToString(),
                ["automaticSigning"] = PlayerSettings.iOS.appleEnableAutomaticSigning,
                ["graphicsApis"] = new JArray(PlayerSettings.GetGraphicsAPIs(BuildTarget.iOS).Select(api => api.ToString())),
                ["orientation"] = PlayerSettings.defaultInterfaceOrientation.ToString(),
                ["xcodeExportValidated"] = false, ["signedApplicationBuilt"] = false, ["physicalDeviceTested"] = false
            };
        }
    }
}
