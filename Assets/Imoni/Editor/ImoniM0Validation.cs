using System;
using System.IO;
using System.Linq;
using Imoni.Game;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TextCore.LowLevel;
using UnityEngine.TextCore.Text;
using UnityEngine.Tilemaps;
using UnityEngine.UIElements;
using Object = UnityEngine.Object;

namespace Imoni.Editor
{
    /// <summary>Checks the saved M0 foundation without changing setup or legacy scenes.</summary>
    public static class ImoniM0Validation
    {
        private const string Root = "Assets/Imoni";
        private const string BootPath = Root + "/Scenes/Boot.unity";
        private const string PanelPath = Root + "/UI/ImoniPanelSettings.asset";
        private const string ThemePath = Root + "/UI/UnityDefaultRuntimeTheme.tss";
        private const string ConfigPath = Root + "/Config/GameConfig.json";
        private const string ReportPath = "Library/M0Validation.json";

        private static readonly (string Source, string Asset)[] Fonts =
        {
            ("DelaGothicOne-Regular.ttf", "DelaGothicOne SDF"),
            ("ZenMaruGothic-Bold.ttf", "ZenMaruGothic-Bold SDF"),
            ("ZenMaruGothic-Medium.ttf", "ZenMaruGothic-Medium SDF"),
            ("ZenMaruGothic-Black.ttf", "ZenMaruGothic-Black SDF")
        };

        [MenuItem("Imoni/Validate M0")]
        public static void Validate()
        {
            // Remove stale success evidence before checking the current project.
            if (File.Exists(ReportPath))
                File.Delete(ReportPath);
            JObject snapshot = Inspect();
            var report = new JObject
            {
                ["status"] = "passed",
                ["scope"] = "M0 saved assets and scene wiring; runtime Play Mode is checked separately",
                ["snapshot"] = snapshot
            };
            Directory.CreateDirectory("Library");
            File.WriteAllText(ReportPath, report.ToString(Formatting.Indented));
            Debug.Log("Imoni M0 validation passed. Report: " + ReportPath);
        }

        /// <summary>Compare before/after Setup Project to check GUID and object-count stability.</summary>
        public static string Snapshot()
        {
            return Inspect().ToString(Formatting.None);
        }

        private static JObject Inspect()
        {
            Require(!EditorApplication.isPlayingOrWillChangePlaymode,
                "Exit Play Mode before validating the saved M0 foundation.");
            JObject settings = JObject.Parse(Load<UnityEngine.TextAsset>(ConfigPath).text)["setup"] as JObject;
            Require(settings != null, "GameConfig.json is missing setup settings.");

            int width = Read<int>(settings, "panel.referenceWidth");
            int height = Read<int>(settings, "panel.referenceHeight");
            Require(width == 390 && height == 844, "M0 reference resolution must be 390 x 844.");
            Require(Mathf.Approximately(Read<float>(settings, "panel.match"), 0),
                "M0 must match the reference width (setup.panel.match = 0).");

            PanelSettings panel = Load<PanelSettings>(PanelPath);
            Require(panel.scaleMode == PanelScaleMode.ScaleWithScreenSize, "Panel scaling mode is incorrect.");
            Require(panel.referenceResolution == new Vector2Int(width, height), "Panel reference resolution is incorrect.");
            Require(panel.screenMatchMode == PanelScreenMatchMode.MatchWidthOrHeight, "Panel match mode is incorrect.");
            Require(Mathf.Approximately(panel.match, Read<float>(settings, "panel.match")), "Panel match differs from config.");
            Require(Mathf.Approximately(panel.sortingOrder, Read<float>(settings, "panel.sortingOrder")),
                "Panel sorting order differs from config.");
            Require(panel.themeStyleSheet == Load<ThemeStyleSheet>(ThemePath), "Panel has no generated runtime theme.");

            var generated = new JArray { AssetIdentity(BootPath), AssetIdentity(PanelPath), AssetIdentity(ThemePath) };
            JArray fonts = InspectFonts(settings, generated);
            JArray assemblies = InspectAssemblies();
            EditorBuildSettingsScene[] buildScenes = EditorBuildSettings.scenes;
            Require(buildScenes.Length > 0 && buildScenes[0].path == BootPath && buildScenes[0].enabled,
                "Boot must be the first enabled Build Settings scene.");
            Require(buildScenes.Count(scene => scene.path == BootPath) == 1, "Build Settings contains duplicate Boot scenes.");
            Require(PlayerSettings.defaultInterfaceOrientation == UIOrientation.Portrait, "Default orientation is not Portrait.");
            Require(PlayerSettings.allowedAutorotateToPortrait && !PlayerSettings.allowedAutorotateToPortraitUpsideDown &&
                !PlayerSettings.allowedAutorotateToLandscapeLeft && !PlayerSettings.allowedAutorotateToLandscapeRight,
                "Portrait rotation settings are incorrect.");
            Require(PlayerSettings.defaultScreenWidth == width && PlayerSettings.defaultScreenHeight == height,
                "Default screen dimensions differ from the panel reference resolution.");

            Scene boot = SceneManager.GetSceneByPath(BootPath);
            bool openedForValidation = !boot.IsValid() || !boot.isLoaded;
            if (openedForValidation)
                boot = EditorSceneManager.OpenScene(BootPath, OpenSceneMode.Additive);
            try
            {
                Require(!boot.isDirty, "Save Boot before validating its persistent references.");
                JObject scene = InspectScene(boot, panel, settings);
                return new JObject
                {
                    ["generatedAssets"] = generated,
                    ["panel"] = new JObject
                    {
                        ["referenceWidth"] = width, ["referenceHeight"] = height,
                        ["match"] = panel.match, ["sortingOrder"] = panel.sortingOrder,
                        ["scaleMode"] = panel.scaleMode.ToString(), ["theme"] = ThemePath
                    },
                    ["fonts"] = fonts,
                    ["assemblies"] = assemblies,
                    ["boot"] = scene,
                    ["buildScenes"] = new JArray(buildScenes.Select(item => new JObject
                    {
                        ["path"] = item.path, ["enabled"] = item.enabled
                    })),
                    ["orientation"] = PlayerSettings.defaultInterfaceOrientation.ToString()
                };
            }
            finally
            {
                if (openedForValidation && boot.IsValid() && boot.isLoaded)
                    EditorSceneManager.CloseScene(boot, true);
            }
        }

        private static JArray InspectFonts(JObject settings, JArray generated)
        {
            string stylesheet = File.ReadAllText(Root + "/UI/Theme.uss");
            var result = new JArray();
            foreach (var font in Fonts)
            {
                string path = Root + "/Fonts/Generated/" + font.Asset + ".asset";
                FontAsset asset = Load<FontAsset>(path);
                Font source = Load<Font>(Root + "/Fonts/" + font.Source);
                Require(asset.name == font.Asset && asset.sourceFontFile == source, path + ": name/source font mismatch.");
                Require(asset.atlasPopulationMode == AtlasPopulationMode.Dynamic && asset.isMultiAtlasTexturesEnabled,
                    path + ": Dynamic + Multi Atlas must be enabled.");
                Require(Mathf.Approximately(asset.faceInfo.pointSize, Read<float>(settings, "font.samplingPointSize")) &&
                    asset.atlasPadding == Read<int>(settings, "font.atlasPadding") &&
                    asset.atlasWidth == Read<int>(settings, "font.atlasWidth") &&
                    asset.atlasHeight == Read<int>(settings, "font.atlasHeight") &&
                    asset.atlasRenderMode == GlyphRenderMode.SDFAA, path + ": atlas settings differ from config.");
                Require(asset.material != null && asset.atlasTextures != null && asset.atlasTextures.Length > 0 &&
                    asset.atlasTextures.All(texture => texture != null), path + ": missing material or atlas texture.");
                Require(stylesheet.Contains("project://database/" + path), path + ": Theme.uss has no matching font URL.");
                Object[] subAssets = AssetDatabase.LoadAllAssetsAtPath(path);
                Object[] owned = new Object[] { asset.material }.Concat(asset.atlasTextures.Cast<Object>()).ToArray();
                foreach (Object subAsset in owned)
                    Require(AssetDatabase.IsSubAsset(subAsset) && AssetDatabase.GetAssetPath(subAsset) == path &&
                        subAssets.Contains(subAsset), path + ": material/atlas is not a persisted subasset.");
                Require(asset.material.shader != null, path + ": font material has no shader.");
                Require(asset.material.mainTexture == asset.atlasTextures[0], path + ": material is not bound to its first atlas.");
                generated.Add(AssetIdentity(path));
                result.Add(new JObject
                {
                    ["path"] = path, ["source"] = AssetDatabase.GetAssetPath(source),
                    ["population"] = asset.atlasPopulationMode.ToString(), ["multiAtlas"] = asset.isMultiAtlasTexturesEnabled,
                    ["atlasWidth"] = asset.atlasWidth, ["atlasHeight"] = asset.atlasHeight,
                    ["samplingPointSize"] = asset.faceInfo.pointSize, ["padding"] = asset.atlasPadding,
                    ["subAssets"] = new JArray(owned.OrderBy(item => item.GetType().FullName)
                        .ThenBy(item => item.name).Select(ObjectIdentity))
                });
            }
            return result;
        }

        private static JArray InspectAssemblies()
        {
            var specifications = new[]
            {
                (Path: Root + "/Scripts/Core/Imoni.Core.asmdef", Name: "Imoni.Core"),
                (Path: Root + "/Scripts/Game/Imoni.Game.asmdef", Name: "Imoni.Game"),
                (Path: Root + "/Editor/Imoni.Editor.asmdef", Name: "Imoni.Editor"),
                (Path: Root + "/Tests/EditMode/Imoni.Tests.asmdef", Name: "Imoni.Tests")
            };
            var result = new JArray();
            foreach (var specification in specifications)
            {
                JObject definition = JObject.Parse(File.ReadAllText(specification.Path));
                Require(definition.Value<string>("name") == specification.Name, specification.Path + ": wrong assembly name.");
                if (specification.Name == "Imoni.Core")
                    Require(definition.Value<bool>("noEngineReferences"), "Imoni.Core must not reference UnityEngine.");
                if (specification.Name == "Imoni.Editor" || specification.Name == "Imoni.Tests")
                    Require(definition["includePlatforms"] is JArray platforms && platforms.Count == 1 &&
                        platforms[0].Value<string>() == "Editor", specification.Path + ": must be Editor-only.");
                if (specification.Name != "Imoni.Core")
                    Require(definition["references"] is JArray references && references.Values<string>().Contains("Imoni.Core"),
                        specification.Path + ": missing Imoni.Core reference.");
                result.Add(AssetIdentity(specification.Path));
            }
            return result;
        }

        private static JObject InspectScene(Scene scene, PanelSettings panel, JObject settings)
        {
            GameObject[] roots = scene.GetRootGameObjects();
            GameObject[] objects = roots.SelectMany(root => root.GetComponentsInChildren<Transform>(true))
                .Select(transform => transform.gameObject).ToArray();
            foreach (GameObject item in objects)
                Require(GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(item) == 0,
                    "Boot has a missing script on " + item.name + ".");
            Camera camera = SingleComponent<Camera>(objects);
            UIDocument document = SingleComponent<UIDocument>(objects);
            Grid grid = SingleComponent<Grid>(objects);
            Tilemap tilemap = SingleComponent<Tilemap>(objects);
            TilemapRenderer renderer = SingleComponent<TilemapRenderer>(objects);
            Bootstrap bootstrap = SingleComponent<Bootstrap>(objects);
            Require(bootstrap.UiDocument == document && bootstrap.WorldCamera == camera &&
                bootstrap.WorldGrid == grid && bootstrap.WorldTilemap == tilemap, "Bootstrap references do not point to the Boot scene objects.");
            Require(document.panelSettings == panel, "Boot UIDocument has the wrong PanelSettings.");
            Require(camera.CompareTag("MainCamera") && camera.orthographic, "Boot camera must be the orthographic MainCamera.");
            Require(Mathf.Approximately(camera.orthographicSize, Read<float>(settings, "camera.orthographicSize")) &&
                Mathf.Approximately(camera.nearClipPlane, Read<float>(settings, "camera.nearClipPlane")) &&
                Mathf.Approximately(camera.farClipPlane, Read<float>(settings, "camera.farClipPlane")), "Boot camera settings differ from config.");
            Require(camera.transform.position == new Vector3(Read<float>(settings, "camera.positionX"),
                Read<float>(settings, "camera.positionY"), Read<float>(settings, "camera.positionZ")), "Boot camera position differs from config.");
            Require(grid.cellSize == new Vector3(Read<float>(settings, "grid.cellSizeX"),
                Read<float>(settings, "grid.cellSizeY"), Read<float>(settings, "grid.cellSizeZ")), "Boot grid cell size differs from config.");
            Require(tilemap.transform.parent == grid.transform && renderer.gameObject == tilemap.gameObject,
                "World Tilemap must be directly under World Grid and have a TilemapRenderer.");
            return new JObject
            {
                ["path"] = scene.path, ["rootCount"] = roots.Length, ["objectCount"] = objects.Length,
                ["roots"] = new JArray(roots.Select(root => root.name).OrderBy(name => name)),
                ["objects"] = new JArray(objects.OrderBy(ObjectPath).Select(item => new JObject
                {
                    ["path"] = ObjectPath(item),
                    ["components"] = new JArray(item.GetComponents<Component>().Select(component => component.GetType().FullName).OrderBy(name => name))
                })),
                ["references"] = new JArray(new Object[] { bootstrap, document, camera, grid, tilemap, renderer }.Select(ObjectIdentity))
            };
        }

        private static T SingleComponent<T>(GameObject[] objects) where T : Component
        {
            T[] components = objects.SelectMany(item => item.GetComponents<T>()).ToArray();
            Require(components.Length == 1, "Boot must contain exactly one " + typeof(T).Name + "; found " + components.Length + ".");
            return components[0];
        }

        private static string ObjectPath(GameObject item)
        {
            return item.transform.parent == null ? item.name : ObjectPath(item.transform.parent.gameObject) + "/" + item.name;
        }

        private static JObject AssetIdentity(string path)
        {
            Require(AssetDatabase.LoadMainAssetAtPath(path) != null, "Missing generated/imported asset: " + path);
            string guid = AssetDatabase.AssetPathToGUID(path);
            Require(!string.IsNullOrEmpty(guid), "Asset has no GUID: " + path);
            return new JObject { ["path"] = path, ["guid"] = guid };
        }

        private static JObject ObjectIdentity(Object item)
        {
            return new JObject
            {
                ["name"] = item.name, ["type"] = item.GetType().FullName,
                ["globalId"] = GlobalObjectId.GetGlobalObjectIdSlow(item).ToString()
            };
        }

        private static T Load<T>(string path) where T : Object
        {
            T asset = AssetDatabase.LoadAssetAtPath<T>(path);
            Require(asset != null, "Missing " + typeof(T).Name + ": " + path);
            return asset;
        }

        private static T Read<T>(JObject settings, string path)
        {
            JToken value = settings.SelectToken(path);
            Require(value != null, "GameConfig.setup is missing " + path + ".");
            return value.Value<T>();
        }

        private static void Require(bool condition, string message)
        {
            if (!condition)
                throw new InvalidOperationException("M0 validation: " + message);
        }
    }
}
