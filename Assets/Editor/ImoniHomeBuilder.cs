using System;
using System.IO;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.TextCore.LowLevel;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace Imoni.UI.Editor
{
    /// <summary>Builds the editable uGUI home from Home.dc.html (390 x 844).</summary>
    public static class ImoniHomeBuilder
    {
        private const string Art = "Assets/ImoniUI/Home/";
        private const string PrefabPath = "Assets/Prefabs/UI/HomeScreen.prefab";
        private static readonly Color Ink = Hex("4A2E22"), Cream = Hex("FFF3D6"),
            Yellow = Hex("F7B92B"), Green = Hex("34421C"), Red = Hex("8B2310");
        private static Sprite rounded;
        private static TMP_FontAsset heading, body, heavy;
        private const string Characters = "芋煮会の初心者ゆくらさんみんなでチーム「鍋底同盟」 /3人最高到達Rベスト84.2万順位128プレイ開始通常クリア未達成登録モードエンドレス:5連続成功解放ホームランキング員証0123456789";

        [MenuItem("Imoni/Home/Build Home Screen")]
        public static void Build()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("Stop Play Mode before building Home.");
            var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
            if (scene.path != "Assets/Scenes/Home.unity")
                throw new InvalidOperationException("Open Assets/Scenes/Home.unity first.");

            Directory.CreateDirectory(Art);
            Directory.CreateDirectory("Assets/Fonts/Home");
            PrepareSprites();
            heading = MakeFont("DelaGothicOne-Regular", "Home_DelaGothicOne");
            body = MakeFont("ZenMaruGothic-Bold", "Home_ZenMaruBold");
            heavy = MakeFont("ZenMaruGothic-Black", "Home_ZenMaruBlack");

            var canvas = scene.GetRootGameObjects().SelectMany(x => x.GetComponentsInChildren<Canvas>(true)).FirstOrDefault();
            if (!canvas) throw new InvalidOperationException("Home Canvas is missing.");
            Undo.RegisterFullObjectHierarchyUndo(canvas.gameObject, "Build Imoni home");
            for (int i = canvas.transform.childCount - 1; i >= 0; i--)
                Undo.DestroyObjectImmediate(canvas.transform.GetChild(i).gameObject);
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.transform.localScale = Vector3.one;
            var scaler = canvas.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(390, 844);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.Expand;
            scaler.referencePixelsPerUnit = 100;

            var bg = Img(canvas.transform, "Background", 0, 0, 390, 844, Yellow);
            bg.rectTransform.anchorMin = Vector2.zero;
            bg.rectTransform.anchorMax = Vector2.one;
            bg.rectTransform.offsetMin = bg.rectTransform.offsetMax = Vector2.zero;

            var root = Rect(canvas.transform, "HomeScreen", 0, 0, 390, 844);
            root.gameObject.AddComponent<HomeScreenFit>();
            var header = Rect(root, "Header", 0, 0, 390, 120);
            Panel(header, "Avatar", 18, 52, 54, 54, Hex("8FAFC4"), Ink, 3, 27);
            Panel(header, "TitlePrefix", 84, 54.5f, 56, 20, Green, Green, 0, 5);
            Text(header, "TitlePrefixText", "芋煮会の", 84, 54.5f, 56, 20, 11, heavy, Cream);
            Panel(header, "TitleSuffix", 143, 54.5f, 45, 20, Red, Red, 0, 5);
            Text(header, "TitleSuffixText", "初心者", 143, 54.5f, 45, 20, 11, heavy, Cream);
            Text(header, "PlayerName", "ゆくらさん", 84, 77.5f, 232, 26, 18, heavy, Ink, TextAlignmentOptions.MidlineLeft);
            var settings = Panel(header, "SettingsButton", 328, 57, 44, 44, Cream, Ink, 3, 12);
            AddButton(settings);
            Icon(settings.transform, "SettingsIcon", "home_settings", 11, 11, 22, 22);

            var hero = Panel(root, "HeroCard", 18, 120, 354, 414, Cream, Ink, 4, 26);
            Icon(hero.transform, "PotIllustration", "home_pot", 62, 92, 230, 150);
            var title = Text(hero.transform, "GameTitle", "みんなで芋煮会", 10, 248, 334, 49, 34, heading, Red);
            title.fontStyle = FontStyles.Bold;
            Text(hero.transform, "TeamName", "チーム「鍋底同盟」 / 3人", 10, 303, 334, 19, 13, body, Ink);

            var stats = Rect(root, "Stats", 18, 548, 354, 70);
            Stat(stats, "BestRound", "最高到達", "R3", 0);
            Stat(stats, "BestScore", "通常クリア", "未達成", 120.6667f);
            Stat(stats, "TeamRank", "ランキング", "未登録", 241.3333f);

            ImoniHomePrefabTools.CreatePlayButton(root, heading);

            var modes = Rect(root, "ModeLabels", 0, 718, 390, 29);
            var normal = Panel(modes, "NormalMode", 60.59375f, 0, 84, 29, Cream, Ink, 2, 14.5f);
            Text(normal.transform, "Label", "通常モード", 0, 0, 84, 29, 12, heavy, Ink);
            var endless = Panel(modes, "EndlessLocked", 152.59375f, 0, 176.796875f, 29, Hex("EFD38A"), Hex("8C6A2E"), 2, 14.5f);
            Text(endless.transform, "Label", "エンドレス:5連続成功で解放", 0, 0, 176.796875f, 29, 12, heavy, Hex("5C4518"));

            var nav = Rect(root, "BottomNavigation", 0, 765, 390, 79);
            Img(nav, "TopBorder", 0, 0, 390, 3, Ink);
            Nav(nav, "HomeButton", "ホーム", "home_nav_home", 0, 128, Yellow);
            Nav(nav, "RankingButton", "ランキング", "home_nav_ranking", 131, 128, Cream);
            Nav(nav, "MemberButton", "会員証", "home_nav_member", 262, 128, Cream);
            Img(nav, "Divider1", 128, 3, 3, 76, Ink);
            Img(nav, "Divider2", 259, 3, 3, 76, Ink);
            ImoniHomePrefabTools.UseBottomNavigationPrefab(root);

            Canvas.ForceUpdateCanvases();
            foreach (var text in root.GetComponentsInChildren<TextMeshProUGUI>()) text.ForceMeshUpdate();
            PrefabUtility.SaveAsPrefabAssetAndConnect(root.gameObject, PrefabPath, InteractionMode.AutomatedAction);
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();
            Selection.activeGameObject = root.gameObject;
            Debug.Log("IMONI_HOME_BUILT: Home scene and editable HomeScreen prefab saved.");
        }

        private static void Stat(Transform parent, string name, string label, string value, float x)
        {
            var panel = Panel(parent, name, x, 0, 112.6667f, 70, Cream, Ink, 3, 14);
            Text(panel.transform, "Label", label, 13, 13, 86.6667f, 16, 11, body, Ink, TextAlignmentOptions.MidlineLeft);
            Text(panel.transform, "Value", value, 13, 31, 86.6667f, 26, 18, heading, Green, TextAlignmentOptions.MidlineLeft);
        }

        private static void Nav(Transform parent, string name, string label, string icon, float x, float w, Color fill)
        {
            var image = Img(parent, name, x, 3, w, 76, fill);
            AddButton(image);
            Icon(image.transform, "Icon", icon, (w - 26) / 2, 15.5f, 26, 26);
            Text(image.transform, "Label", label, 0, 43.5f, w, 17, 12, heavy, Ink);
        }

        private static Button AddButton(Image image)
        {
            image.raycastTarget = true;
            var button = image.gameObject.AddComponent<Button>();
            button.targetGraphic = image;
            var colors = button.colors;
            colors.normalColor = Color.white;
            colors.highlightedColor = new Color(.94f, .94f, .94f);
            colors.pressedColor = new Color(.78f, .78f, .78f);
            colors.selectedColor = Color.white;
            colors.fadeDuration = .1f;
            button.colors = colors;
            return button;
        }

        private static RectTransform Rect(Transform parent, string name, float x, float y, float w, float h)
        {
            var rect = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
            rect.gameObject.layer = 5;
            rect.SetParent(parent, false);
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0, 1);
            rect.anchoredPosition = new Vector2(x, -y);
            rect.sizeDelta = new Vector2(w, h);
            return rect;
        }

        private static Image Img(Transform parent, string name, float x, float y, float w, float h, Color color, float radius = 0)
        {
            var rect = Rect(parent, name, x, y, w, h);
            var image = rect.gameObject.AddComponent<Image>();
            image.color = color;
            image.raycastTarget = false;
            if (radius > 0)
            {
                image.sprite = rounded;
                image.type = Image.Type.Sliced;
                image.pixelsPerUnitMultiplier = 32 / radius;
            }
            return image;
        }

        private static Image Panel(Transform parent, string name, float x, float y, float w, float h, Color fill, Color border, float stroke, float radius)
        {
            var image = Img(parent, name, x, y, w, h, stroke > 0 ? border : fill, radius);
            if (stroke > 0) Img(image.transform, "Fill", stroke, stroke, w - stroke * 2, h - stroke * 2, fill, radius - stroke);
            return image;
        }

        private static void Icon(Transform parent, string name, string file, float x, float y, float w, float h)
        {
            var img = Img(parent, name, x, y, w, h, Color.white);
            img.sprite = AssetDatabase.LoadAssetAtPath<Sprite>(Art + file + ".png");
            if (!img.sprite) throw new InvalidOperationException("Missing home icon " + file);
            img.preserveAspect = true;
        }

        private static TextMeshProUGUI Text(Transform parent, string name, string value, float x, float y, float w, float h,
            float size, TMP_FontAsset font, Color color, TextAlignmentOptions align = TextAlignmentOptions.Midline)
        {
            var text = Rect(parent, name, x, y, w, h).gameObject.AddComponent<TextMeshProUGUI>();
            text.font = font;
            text.text = value;
            text.fontSize = size;
            text.color = color;
            text.alignment = align;
            text.textWrappingMode = TextWrappingModes.NoWrap;
            text.overflowMode = TextOverflowModes.Overflow;
            text.raycastTarget = false;
            text.richText = false;
            return text;
        }

        private static TMP_FontAsset MakeFont(string source, string name)
        {
            var path = "Assets/Fonts/Home/" + name + ".asset";
            var font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(path);
            if (!font)
            {
                font = TMP_FontAsset.CreateFontAsset(AssetDatabase.LoadAssetAtPath<Font>("Assets/Fonts/Source/" + source + ".ttf"),
                    72, 8, GlyphRenderMode.SDFAA, 1024, 1024, AtlasPopulationMode.Dynamic, true);
                font.name = name;
                AssetDatabase.CreateAsset(font, path);
                AssetDatabase.AddObjectToAsset(font.material, font);
                foreach (var atlas in font.atlasTextures) AssetDatabase.AddObjectToAsset(atlas, font);
            }
            if (!font.TryAddCharacters(Characters, out string missing))
                throw new InvalidOperationException(name + " is missing glyphs: " + missing);
            EditorUtility.SetDirty(font);
            return font;
        }

        private static void PrepareSprites()
        {
            var path = Art + "home_rounded_white.png";
            if (!File.Exists(path))
            {
                const int side = 198;
                const float radius = 96;
                var texture = new Texture2D(side, side, TextureFormat.RGBA32, false);
                for (int y = 0; y < side; y++)
                for (int x = 0; x < side; x++)
                {
                    var p = new Vector2(Mathf.Abs(x + .5f - side / 2f), Mathf.Abs(y + .5f - side / 2f));
                    var d = new Vector2(Mathf.Max(p.x - (side / 2f - radius), 0), Mathf.Max(p.y - (side / 2f - radius), 0)).magnitude - radius;
                    texture.SetPixel(x, y, new Color(1, 1, 1, Mathf.Clamp01(.5f - d)));
                }
                texture.Apply();
                File.WriteAllBytes(path, texture.EncodeToPNG());
                Object.DestroyImmediate(texture);
            }
            AssetDatabase.Refresh();
            foreach (var file in Directory.GetFiles(Art, "*.png"))
            {
                var importer = (TextureImporter)AssetImporter.GetAtPath(file.Replace('\\', '/'));
                importer.textureType = TextureImporterType.Sprite;
                importer.spriteImportMode = SpriteImportMode.Single;
                importer.spritePixelsPerUnit = 300;
                importer.mipmapEnabled = false;
                importer.alphaIsTransparency = true;
                importer.textureCompression = TextureImporterCompression.Uncompressed;
                importer.npotScale = TextureImporterNPOTScale.None;
                importer.filterMode = FilterMode.Bilinear;
                importer.wrapMode = TextureWrapMode.Clamp;
                var settings = new TextureImporterSettings();
                importer.ReadTextureSettings(settings);
                settings.spriteMeshType = SpriteMeshType.FullRect;
                importer.SetTextureSettings(settings);
                importer.spriteBorder = file.EndsWith("home_rounded_white.png") ? new Vector4(96, 96, 96, 96) : Vector4.zero;
                importer.SaveAndReimport();
            }
            rounded = AssetDatabase.LoadAssetAtPath<Sprite>(path);
        }

        private static Color Hex(string hex) { ColorUtility.TryParseHtmlString("#" + hex, out var c); return c; }
    }
}
