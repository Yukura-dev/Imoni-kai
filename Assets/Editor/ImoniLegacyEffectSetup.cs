using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Imoni.Game.Effects;
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
    public static class ImoniLegacyEffectSetup
    {
        private const string Root = "Assets/Imoni";
        private const string ConfigPath = Root + "/Config/GameConfig.json";
        private const string FontFolder = Root + "/Fonts/Generated";
        private const string PanelPath = Root + "/UI/ImoniPanelSettings.asset";
        private const string DefaultThemePath = Root + "/UI/UnityDefaultRuntimeTheme.tss";

        // These names are the project://database references already defined in Theme.uss.
        private static readonly (string Source, string Asset)[] Fonts =
        {
            ("DelaGothicOne-Regular.ttf", "DelaGothicOne SDF"),
            ("ZenMaruGothic-Bold.ttf", "ZenMaruGothic-Bold SDF"),
            ("ZenMaruGothic-Medium.ttf", "ZenMaruGothic-Medium SDF"),
            ("ZenMaruGothic-Black.ttf", "ZenMaruGothic-Black SDF")
        };

        [MenuItem("Imoni/Legacy/Setup Effect")]
        public static void SetupProject()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("Exit Play Mode before setup.");
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            var settings = ReadSettings();
            EnsureFolder(FontFolder); EnsureFolder(Root + "/UI"); EnsureFolder(Root + "/Scenes");
            foreach (var font in Fonts)
                EnsureFont(LoadRequired<Font>(Root + "/Fonts/" + font.Source), font.Asset, settings);
            var panel = EnsurePanel(settings);
            AssetDatabase.SaveAssets();
            AssetDatabase.ImportAsset(Root + "/UI/Theme.uss", ImportAssetOptions.ForceSynchronousImport);
            AssetDatabase.ImportAsset(Root + "/UI/ImoniEffect.uss", ImportAssetOptions.ForceSynchronousImport);
            PatchResultPrefab(panel);
            PatchResultScene(panel);
            CreatePreviewScene(panel);
            AssetDatabase.SaveAssets();
            Debug.Log("IMONI_EFFECT_SETUP_COMPLETE: UI Toolkit effect connected; harvesting and result settlement preserved.");
        }

        private const string ResultPrefabPath = "Assets/Prefabs/UI/RoundResultScreen.prefab";
        private const string ResultScenePath = "Assets/Scenes/RoundResult.unity";
        private const string PreviewPath = Root + "/Scenes/ImoniEffectPreview.unity";

        private static ImoniEffectView ConfigureView(GameObject root, PanelSettings panel)
        {
            var document = EnsureComponent<UIDocument>(root);
            document.panelSettings = panel;
            var view = EnsureComponent<ImoniEffectView>(root);
            view.document = document;
            view.gameConfig = LoadRequired<TextAsset>(ConfigPath);
            view.visualTree = LoadRequired<VisualTreeAsset>(Root + "/UI/ImoniEffect.uxml");
            view.themeStyle = LoadRequired<StyleSheet>(Root + "/UI/Theme.uss");
            view.effectStyle = LoadRequired<StyleSheet>(Root + "/UI/ImoniEffect.uss");
            view.textures = AssetDatabase.FindAssets("t:Texture2D", new[] { Root + "/Art/Sprites" })
                .Select(guid => AssetDatabase.LoadAssetAtPath<Texture2D>(AssetDatabase.GUIDToAssetPath(guid)))
                .Where(texture => texture != null).ToArray();
            var audio = EnsureComponent<ImoniEffectAudio>(root);
            audio.view = view;
            audio.drone = LoadRequired<AudioClip>(Root + "/Audio/SFX/bgm_drone_loop.wav");
            audio.beat = LoadRequired<AudioClip>(Root + "/Audio/SFX/sfx_beat.wav");
            audio.hihat = LoadRequired<AudioClip>(Root + "/Audio/SFX/sfx_beat_hihat.wav");
            audio.hit = LoadRequired<AudioClip>(Root + "/Audio/SFX/sfx_hit.wav");
            audio.levelUps = Enumerable.Range(1, 5).Select(level => LoadRequired<AudioClip>(Root + "/Audio/SFX/sfx_levelup_L" + level + ".wav")).ToArray();
            audio.successFinale = LoadRequired<AudioClip>(Root + "/Audio/SFX/sfx_finale_success.wav");
            audio.failureFinale = LoadRequired<AudioClip>(Root + "/Audio/SFX/sfx_finale_fail.wav");
            EditorUtility.SetDirty(audio);
            EditorUtility.SetDirty(document); EditorUtility.SetDirty(view);
            return view;
        }

        private static void PatchController(global::Imoni.UI.RoundResultController controller, PanelSettings panel)
        {
            var child = controller.transform.Find("ImoniEffect");
            var effect = child != null ? child.gameObject : new GameObject("ImoniEffect");
            if (child == null) effect.transform.SetParent(controller.transform, false);
            controller.effectView = ConfigureView(effect, panel);
            if (controller.cookingRoot) controller.cookingRoot.SetActive(false);
            EditorUtility.SetDirty(controller);
            if (PrefabUtility.IsPartOfPrefabInstance(controller))
                PrefabUtility.RecordPrefabInstancePropertyModifications(controller);
        }

        private static void PatchResultPrefab(PanelSettings panel)
        {
            var root = PrefabUtility.LoadPrefabContents(ResultPrefabPath);
            try
            {
                PatchController(root.GetComponent<global::Imoni.UI.RoundResultController>(), panel);
                PrefabUtility.SaveAsPrefabAsset(root, ResultPrefabPath);
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
        }

        private static void PatchResultScene(PanelSettings panel)
        {
            var scene = SceneManager.GetSceneByPath(ResultScenePath);
            bool opened = !scene.isLoaded;
            if (opened) scene = EditorSceneManager.OpenScene(ResultScenePath, OpenSceneMode.Additive);
            try
            {
                foreach (var controller in scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<global::Imoni.UI.RoundResultController>(true)))
                    PatchController(controller, panel);
                if (!scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<AudioListener>(true)).Any())
                {
                    var camera = scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<Camera>(true)).FirstOrDefault();
                    if (camera) EnsureComponent<AudioListener>(camera.gameObject);
                }
                EditorSceneManager.MarkSceneDirty(scene);
                EditorSceneManager.SaveScene(scene);
            }
            finally { if (opened) EditorSceneManager.CloseScene(scene, true); }
        }

        private static void CreatePreviewScene(PanelSettings panel)
        {
            var previous = SceneManager.GetActiveScene();
            var scene = SceneManager.GetSceneByPath(PreviewPath);
            bool opened = !scene.isLoaded;
            if (opened) scene = File.Exists(PreviewPath)
                ? EditorSceneManager.OpenScene(PreviewPath, OpenSceneMode.Additive)
                : EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
            try
            {
                var camera = EnsureComponent<Camera>(FindOrCreateRoot(scene, "Main Camera"));
                camera.clearFlags = CameraClearFlags.SolidColor;
                camera.backgroundColor = Color.black;
                camera.orthographic = true;
                camera.tag = "MainCamera";
                EnsureComponent<AudioListener>(camera.gameObject);
                var root = FindOrCreateRoot(scene, "ImoniEffectPreview");
                var view = ConfigureView(root, panel);
                var preview = EnsureComponent<ImoniEffectPreview>(root);
                preview.view = view;
                EditorUtility.SetDirty(preview);
                EditorSceneManager.SaveScene(scene, PreviewPath);
            }
            finally
            {
                if (opened) EditorSceneManager.CloseScene(scene, true);
                if (previous.IsValid() && previous.isLoaded) SceneManager.SetActiveScene(previous);
            }
        }

        [MenuItem("Imoni/Effect/Record Success and Failure")]
        public static void RecordBoth()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Exit Play Mode first.");
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            EditorSceneManager.OpenScene(PreviewPath, OpenSceneMode.Single);
            SetGameViewSize();
            SessionState.SetBool("ImoniEffect.Capture", true);
            EditorApplication.isPlaying = true;
        }

        private static void SetGameViewSize()
        {
            var flags = System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance;
            var assembly = typeof(UnityEditor.Editor).Assembly;
            var gameViewType = assembly.GetType("UnityEditor.GameView");
            var view = EditorWindow.GetWindow(gameViewType);
            var sizesType = assembly.GetType("UnityEditor.GameViewSizes");
            var sizes = typeof(ScriptableSingleton<>).MakeGenericType(sizesType).GetProperty("instance").GetValue(null);
            var groupType = assembly.GetType("UnityEditor.GameViewSizeGroupType");
            var group = sizesType.GetMethod("GetGroup").Invoke(sizes, new[] { Enum.Parse(groupType, "Standalone") });
            var labels = (string[])group.GetType().GetMethod("GetDisplayTexts", flags).Invoke(group, null);
            var review = (JObject)JObject.Parse(LoadRequired<TextAsset>(ConfigPath).text)["effectReview"];
            int width = (int)review["width"], height = (int)review["height"];
            int index = -1;
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
                var kindType = assembly.GetType("UnityEditor.GameViewSizeType");
                var size = Activator.CreateInstance(sizeType, flags | System.Reflection.BindingFlags.CreateInstance, null,
                    new object[] { Enum.Parse(kindType, "FixedResolution"), width, height, "Imoni Effect Review" }, null);
                group.GetType().GetMethod("AddCustomSize", flags).Invoke(group, new[] { size });
                index = labels.Length;
            }
            gameViewType.GetProperty("selectedSizeIndex", flags).SetValue(view, index);
            view.Focus(); view.Repaint();
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
                asset.atlasTextures.Length == 0 || asset.atlasTextures.Any(texture => texture == null);

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

            asset.name = name;
            asset.atlasPopulationMode = AtlasPopulationMode.Dynamic;
            asset.isMultiAtlasTexturesEnabled = true;
            SaveFontSubAsset(asset.material, asset, name + " Material");
            foreach (Texture2D texture in asset.atlasTextures)
                SaveFontSubAsset(texture, asset, name + " Atlas");
            asset.ReadFontAssetDefinition();
            EditorUtility.SetDirty(asset);
        }

        private static void SaveFontSubAsset(Object subAsset, FontAsset font, string name)
        {
            if (subAsset == null)
                throw new InvalidOperationException("FontAsset " + font.name + " has a missing atlas or material.");
            if (!AssetDatabase.Contains(subAsset))
            {
                subAsset.name = name;
                subAsset.hideFlags = HideFlags.None;
                AssetDatabase.AddObjectToAsset(subAsset, font);
            }
            EditorUtility.SetDirty(subAsset);
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
