using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Imoni.Game;
using Imoni.Game.UI;
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
using TextAsset = UnityEngine.TextAsset;

namespace Imoni.Editor
{
    /// <summary>Creates Unity-owned assets through Editor APIs; safe to run again.</summary>
    public static class ImoniSetup
    {
        private const string Root = "Assets/Imoni";
        private const string ConfigPath = Root + "/Config/GameConfig.json";
        private const string FontFolder = Root + "/Fonts/Generated";
        private const string PanelPath = Root + "/UI/ImoniPanelSettings.asset";
        private const string DefaultThemePath = Root + "/UI/UnityDefaultRuntimeTheme.tss";
        private const string BootPath = Root + "/Scenes/Boot.unity";

        // These names are the project://database references already defined in Theme.uss.
        private static readonly (string Source, string Asset)[] Fonts =
        {
            ("DelaGothicOne-Regular.ttf", "DelaGothicOne SDF"),
            ("ZenMaruGothic-Bold.ttf", "ZenMaruGothic-Bold SDF"),
            ("ZenMaruGothic-Medium.ttf", "ZenMaruGothic-Medium SDF"),
            ("ZenMaruGothic-Black.ttf", "ZenMaruGothic-Black SDF")
        };

        [MenuItem("Imoni/Setup Project")]
        public static void SetupProject()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("Exit Play Mode before running Imoni > Setup Project.");

            // Validate inputs before switching scenes or creating any assets.
            SetupSettings settings = ReadSettings();
            Font[] sourceFonts = Fonts.Select(font => LoadRequired<Font>(Root + "/Fonts/" + font.Source)).ToArray();
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                return;

            EnsureFolder(FontFolder);
            EnsureFolder(Root + "/UI");
            EnsureFolder(Root + "/Scenes");
            for (int index = 0; index < Fonts.Length; index++)
                EnsureFont(sourceFonts[index], Fonts[index].Asset, settings);

            PanelSettings panel = EnsurePanel(settings);
            ImoniPotSetup.EnsureAssets();
            EnsureUiRuntimeAssets();
            ImoniWorldSetup.EnsureAssets();
            EnsureBootScene(panel, settings);
            RegisterBootScene();
            SetPortrait(settings);
            ImoniAndroidSetup.Configure();

            AssetDatabase.SaveAssets();
            // Resolve Theme.uss URLs that were imported before their FontAssets existed.
            AssetDatabase.ImportAsset(Root + "/UI/Theme.uss", ImportAssetOptions.ForceUpdate);
            Selection.activeObject = AssetDatabase.LoadAssetAtPath<SceneAsset>(BootPath);
            Debug.Log("Imoni setup complete. Boot and ComponentGallery resources are ready.");
        }

        [MenuItem("Imoni/Open Component Gallery")]
        public static void OpenComponentGallery()
        {
            if (EditorApplication.isPlaying)
            {
                var bootstrap = Object.FindFirstObjectByType<Bootstrap>();
                if (bootstrap) bootstrap.ShowComponentGallery();
                return;
            }
            SetupProject();
            if (SceneManager.GetActiveScene().path == BootPath)
            {
                SessionState.SetString("Imoni.StartScreen", "ComponentGallery");
                EditorApplication.delayCall += () => EditorApplication.isPlaying = true;
            }
        }

        [MenuItem("Imoni/Open App")]
        public static void OpenApp()
        {
            if (EditorApplication.isPlaying)
            { Object.FindFirstObjectByType<Bootstrap>()?.Router?.Show("Main"); return; }
            SetupProject();
            SessionState.SetString("Imoni.StartScreen", "Main");
            EditorApplication.delayCall += () => EditorApplication.isPlaying = true;
        }

        private static void EnsureUiRuntimeAssets()
        {
            EnsureFolder(Root + "/Resources/UI");
            const string path = Root + "/Resources/UI/UIRuntimeAssets.asset";
            UIRuntimeAssets assets = LoadGenerated<UIRuntimeAssets>(path);
            if (!assets) { assets = ScriptableObject.CreateInstance<UIRuntimeAssets>(); AssetDatabase.CreateAsset(assets, path); }
            assets.config = LoadRequired<TextAsset>(ConfigPath);
            assets.helpContent = LoadRequired<TextAsset>(Root + "/Config/HelpContent.json");
            assets.theme = LoadRequired<StyleSheet>(Root + "/UI/Theme.uss");
            assets.imoniEffectTree = LoadRequired<VisualTreeAsset>(Root + "/UI/ImoniEffect.uxml");
            assets.imoniEffectStyle = LoadRequired<StyleSheet>(Root + "/UI/ImoniEffect.uss");
            assets.imoniEffectGrayscaleShader = LoadRequired<Shader>(Root + "/Shaders/ImoniEffectGrayscale.shader");
            assets.imoniEffectAudio = Directory.GetFiles(Root + "/Audio/SFX", "*.wav")
                .OrderBy(file => file, StringComparer.Ordinal)
                .Select(file => LoadRequired<AudioClip>(file.Replace('\\', '/'))).ToArray();
            assets.textures = Directory.GetFiles(Root + "/Art", "*.png", SearchOption.AllDirectories)
                .OrderBy(file => file, StringComparer.Ordinal)
                .Select(file => new NamedTexture { key = Path.GetFileNameWithoutExtension(file), texture = LoadRequired<Texture2D>(file.Replace('\\', '/')) }).ToArray();
            ImoniAudioSetup.EnsureAssets(assets, JObject.Parse(File.ReadAllText(ConfigPath)));
            ImoniBgmSetup.EnsureAssets(assets, JObject.Parse(File.ReadAllText(ConfigPath)));
            EditorUtility.SetDirty(assets);
            AssetDatabase.ImportAsset(Root + "/Resources/UI/ComponentGallery.uxml", ImportAssetOptions.ForceUpdate);
        }

        private static SetupSettings ReadSettings()
        {
            TextAsset config = LoadRequired<TextAsset>(ConfigPath);
            JObject setup = JObject.Parse(config.text)["setup"] as JObject
                ?? throw new InvalidOperationException(ConfigPath + " must contain a setup object.");
            var settings = new SetupSettings
            {
                ReferenceWidth = ReadInt(setup, "panel.referenceWidth", positive: true),
                ReferenceHeight = ReadInt(setup, "panel.referenceHeight", positive: true),
                Match = ReadFloat(setup, "panel.match"),
                SortingOrder = ReadFloat(setup, "panel.sortingOrder"),
                SamplingPointSize = ReadInt(setup, "font.samplingPointSize", positive: true),
                AtlasPadding = ReadInt(setup, "font.atlasPadding", positive: false),
                AtlasWidth = ReadInt(setup, "font.atlasWidth", positive: true),
                AtlasHeight = ReadInt(setup, "font.atlasHeight", positive: true),
                OrthographicSize = ReadFloat(setup, "camera.orthographicSize"),
                CameraPosition = new Vector3(ReadFloat(setup, "camera.positionX"),
                    ReadFloat(setup, "camera.positionY"), ReadFloat(setup, "camera.positionZ")),
                NearClipPlane = ReadFloat(setup, "camera.nearClipPlane"),
                FarClipPlane = ReadFloat(setup, "camera.farClipPlane"),
                CellSize = new Vector3(ReadFloat(setup, "grid.cellSizeX"),
                    ReadFloat(setup, "grid.cellSizeY"), ReadFloat(setup, "grid.cellSizeZ"))
            };
            if (settings.Match < 0 || settings.Match > 1 || settings.OrthographicSize <= 0 ||
                settings.NearClipPlane <= 0 || settings.FarClipPlane <= settings.NearClipPlane ||
                settings.CellSize.x <= 0 || settings.CellSize.y <= 0 || settings.CellSize.z < 0 ||
                settings.AtlasPadding >= settings.AtlasWidth || settings.AtlasPadding >= settings.AtlasHeight)
                throw new InvalidOperationException(ConfigPath + " has invalid setup panel, camera, grid, or font dimensions.");
            return settings;
        }

        private static int ReadInt(JObject setup, string path, bool positive)
        {
            JToken token = setup.SelectToken(path);
            if (token == null || token.Type != JTokenType.Integer)
                throw new InvalidOperationException(ConfigPath + ": setup." + path + " must be an integer.");
            int value = token.Value<int>();
            if (positive ? value <= 0 : value < 0)
                throw new InvalidOperationException(ConfigPath + ": setup." + path + " is out of range.");
            return value;
        }

        private static float ReadFloat(JObject setup, string path)
        {
            JToken token = setup.SelectToken(path);
            if (token == null || (token.Type != JTokenType.Float && token.Type != JTokenType.Integer))
                throw new InvalidOperationException(ConfigPath + ": setup." + path + " must be a number.");
            float value = token.Value<float>();
            if (float.IsNaN(value) || float.IsInfinity(value))
                throw new InvalidOperationException(ConfigPath + ": setup." + path + " must be finite.");
            return value;
        }

        private static void EnsureFont(Font source, string name, SetupSettings settings)
        {
            string path = FontFolder + "/" + name + ".asset";
            FontAsset asset = LoadGenerated<FontAsset>(path);
            bool needsCreation = asset == null || asset.sourceFontFile != source ||
                !Mathf.Approximately(asset.faceInfo.pointSize, settings.SamplingPointSize) ||
                asset.atlasPadding != settings.AtlasPadding || asset.atlasWidth != settings.AtlasWidth ||
                asset.atlasHeight != settings.AtlasHeight || asset.atlasRenderMode != GlyphRenderMode.SDFAA ||
                asset.material == null || asset.atlasTextures == null ||
                asset.atlasTextures.Length < asset.atlasTextureCount ||
                asset.atlasTextures.Take(asset.atlasTextureCount).Any(texture => texture == null);

            if (needsCreation)
            {
                FontAsset replacement = FontAsset.CreateFontAsset(source, settings.SamplingPointSize,
                    settings.AtlasPadding, GlyphRenderMode.SDFAA, settings.AtlasWidth, settings.AtlasHeight,
                    AtlasPopulationMode.Dynamic, true);
                if (replacement == null)
                    throw new InvalidOperationException("Could not create FontAsset for " + source.name + ". Check Include Font Data in its importer.");
                replacement.name = name;

                if (asset == null)
                {
                    asset = replacement;
                    AssetDatabase.CreateAsset(asset, path);
                }
                else
                {
                    // Preserve the main asset GUID and every USS/scene reference when settings change.
                    Object[] previousSubAssets = AssetDatabase.LoadAllAssetsAtPath(path)
                        .Where(item => item is Material || item is Texture2D).ToArray();
                    EditorUtility.CopySerialized(replacement, asset);
                    // FontAsset.OnDestroy owns its textures/material: transfer them before cleanup.
                    replacement.atlasTextures = Array.Empty<Texture2D>();
                    replacement.material = null;
                    Object.DestroyImmediate(replacement);
                    foreach (Object subAsset in previousSubAssets)
                        Object.DestroyImmediate(subAsset, true);
                }
            }

            bool changed = needsCreation;
            if (asset.name != name) { asset.name = name; changed = true; }
            if (asset.atlasPopulationMode != AtlasPopulationMode.Dynamic) { asset.atlasPopulationMode = AtlasPopulationMode.Dynamic; changed = true; }
            if (!asset.isMultiAtlasTexturesEnabled) { asset.isMultiAtlasTexturesEnabled = true; changed = true; }
            // TextCore's build preprocessor otherwise clears the deliberately prewarmed
            // characters and atlases. This option has no public setter in Unity 6000.3.
            using (var fontSettings = new SerializedObject(asset))
            {
                SerializedProperty clearOnBuild = fontSettings.FindProperty("m_ClearDynamicDataOnBuild");
                if (clearOnBuild == null) throw new InvalidOperationException("TextCore's Clear Dynamic Data on Build setting was not found.");
                if (clearOnBuild.boolValue)
                {
                    clearOnBuild.boolValue = false;
                    fontSettings.ApplyModifiedPropertiesWithoutUndo();
                    changed = true;
                }
            }
            if (name == "DelaGothicOne SDF")
            {
                // The fallback face terminates the chain; never create a Zen/Dela cycle.
                if (asset.fallbackFontAssetTable != null && asset.fallbackFontAssetTable.Count > 0)
                { asset.fallbackFontAssetTable.Clear(); changed = true; }
            }
            else
            {
                FontAsset fallback = LoadRequired<FontAsset>(FontFolder + "/DelaGothicOne SDF.asset");
                if (asset.fallbackFontAssetTable == null) asset.fallbackFontAssetTable = new List<FontAsset>();
                if (!asset.fallbackFontAssetTable.Contains(fallback))
                { asset.fallbackFontAssetTable.Add(fallback); changed = true; }
            }
            bool warmed = ImoniFontWarmup.PrepareUi(asset, name);
            changed |= warmed;
            changed |= SaveFontSubAsset(asset.material, asset, name + " Material", changed);
            // TextCore grows the backing array geometrically; unused capacity can be null.
            foreach (Texture2D texture in asset.atlasTextures.Take(asset.atlasTextureCount))
                changed |= SaveFontSubAsset(texture, asset, name + " Atlas", changed);
            if (changed)
            {
                asset.ReadFontAssetDefinition();
                EditorUtility.SetDirty(asset);
                AssetDatabase.SaveAssetIfDirty(asset);
                // Newly created atlas subassets must be imported once after being saved. A
                // repeated Setup with the same known glyphs must not request another reimport.
                AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate | ImportAssetOptions.ForceSynchronousImport);
            }
        }

        private static bool SaveFontSubAsset(Object subAsset, FontAsset font, string name, bool changed)
        {
            if (subAsset == null)
                throw new InvalidOperationException("FontAsset " + font.name + " has a missing atlas or material.");
            if (!AssetDatabase.Contains(subAsset))
            {
                subAsset.name = name;
                subAsset.hideFlags = HideFlags.None;
                AssetDatabase.AddObjectToAsset(subAsset, font);
                changed = true;
            }
            if (changed) EditorUtility.SetDirty(subAsset);
            return changed;
        }

        private static PanelSettings EnsurePanel(SetupSettings settings)
        {
            // Same default theme import as Unity's Create > UI Toolkit > Panel Settings command.
            if (!File.Exists(DefaultThemePath))
                File.WriteAllText(DefaultThemePath, "@import url(\"unity-theme://default\");\n");
            AssetDatabase.ImportAsset(DefaultThemePath, ImportAssetOptions.ForceSynchronousImport);

            PanelSettings panel = LoadGenerated<PanelSettings>(PanelPath);
            if (panel == null)
            {
                panel = ScriptableObject.CreateInstance<PanelSettings>();
                AssetDatabase.CreateAsset(panel, PanelPath);
            }
            panel.scaleMode = PanelScaleMode.ScaleWithScreenSize;
            panel.referenceResolution = new Vector2Int(settings.ReferenceWidth, settings.ReferenceHeight);
            panel.screenMatchMode = PanelScreenMatchMode.MatchWidthOrHeight;
            panel.match = settings.Match;
            panel.sortingOrder = settings.SortingOrder;
            panel.themeStyleSheet = LoadRequired<ThemeStyleSheet>(DefaultThemePath);
            EditorUtility.SetDirty(panel);
            return panel;
        }

        private static void EnsureBootScene(PanelSettings panel, SetupSettings settings)
        {
            Scene scene = AssetDatabase.LoadAssetAtPath<SceneAsset>(BootPath) != null
                ? EditorSceneManager.OpenScene(BootPath, OpenSceneMode.Single)
                : EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            GameObject cameraObject = FindOrCreateRoot(scene, "Main Camera");
            Camera camera = EnsureComponent<Camera>(cameraObject);
            EnsureComponent<AudioListener>(cameraObject);
            cameraObject.tag = "MainCamera";
            cameraObject.transform.SetPositionAndRotation(settings.CameraPosition, Quaternion.identity);
            camera.orthographic = true;
            camera.orthographicSize = settings.OrthographicSize;
            camera.nearClipPlane = settings.NearClipPlane;
            camera.farClipPlane = settings.FarClipPlane;

            UIDocument document = EnsureComponent<UIDocument>(FindOrCreateRoot(scene, "UI Document"));
            document.panelSettings = panel;
            Grid grid = EnsureComponent<Grid>(FindOrCreateRoot(scene, "World Grid"));
            grid.cellSize = settings.CellSize;
            Transform tilemapTransform = grid.transform.Find("World Tilemap");
            GameObject tilemapObject;
            if (tilemapTransform == null)
            {
                tilemapObject = new GameObject("World Tilemap");
                tilemapObject.transform.SetParent(grid.transform, false);
            }
            else
            {
                tilemapObject = tilemapTransform.gameObject;
            }
            Tilemap tilemap = EnsureComponent<Tilemap>(tilemapObject);
            EnsureComponent<TilemapRenderer>(tilemapObject);
            Bootstrap bootstrap = EnsureComponent<Bootstrap>(FindOrCreateRoot(scene, "Bootstrap"));
            bootstrap.SetSceneReferences(document, camera, grid, tilemap);
            EditorUtility.SetDirty(bootstrap);
            EditorSceneManager.MarkSceneDirty(scene);
            if (!EditorSceneManager.SaveScene(scene, BootPath))
                throw new InvalidOperationException("Could not save " + BootPath + ".");
        }

        private static GameObject FindOrCreateRoot(Scene scene, string name)
        {
            GameObject existing = scene.GetRootGameObjects().SingleOrDefault(item => item.name == name);
            if (existing != null)
                return existing;
            var created = new GameObject(name);
            SceneManager.MoveGameObjectToScene(created, scene);
            return created;
        }

        private static T EnsureComponent<T>(GameObject target) where T : Component
        {
            T component = target.GetComponent<T>();
            return component != null ? component : target.AddComponent<T>();
        }

        private static void RegisterBootScene()
        {
            var scenes = new List<EditorBuildSettingsScene> { new EditorBuildSettingsScene(BootPath, true) };
            scenes.AddRange(EditorBuildSettings.scenes.Where(scene => scene.path != BootPath));
            EditorBuildSettings.scenes = scenes.ToArray();
        }

        private static void SetPortrait(SetupSettings settings)
        {
            PlayerSettings.defaultInterfaceOrientation = UIOrientation.Portrait;
            PlayerSettings.allowedAutorotateToPortrait = true;
            PlayerSettings.allowedAutorotateToPortraitUpsideDown = false;
            PlayerSettings.allowedAutorotateToLandscapeLeft = false;
            PlayerSettings.allowedAutorotateToLandscapeRight = false;
            PlayerSettings.defaultScreenWidth = settings.ReferenceWidth;
            PlayerSettings.defaultScreenHeight = settings.ReferenceHeight;
        }

        private static T LoadRequired<T>(string path) where T : Object
        {
            T asset = AssetDatabase.LoadAssetAtPath<T>(path);
            return asset != null ? asset : throw new InvalidOperationException("Missing or unimported " + typeof(T).Name + ": " + path);
        }

        private static T LoadGenerated<T>(string path) where T : Object
        {
            Object asset = AssetDatabase.LoadMainAssetAtPath(path);
            if (asset != null && !(asset is T))
                throw new InvalidOperationException(path + " already exists but is not a " + typeof(T).Name + ".");
            return asset as T;
        }

        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path))
                return;
            int separator = path.LastIndexOf('/');
            string parent = path.Substring(0, separator);
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, path.Substring(separator + 1));
        }

        private sealed class SetupSettings
        {
            public int ReferenceWidth;
            public int ReferenceHeight;
            public float Match;
            public float SortingOrder;
            public int SamplingPointSize;
            public int AtlasPadding;
            public int AtlasWidth;
            public int AtlasHeight;
            public float OrthographicSize;
            public Vector3 CameraPosition;
            public float NearClipPlane;
            public float FarClipPlane;
            public Vector3 CellSize;
        }
    }
}
