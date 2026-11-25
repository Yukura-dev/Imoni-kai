using System;
using System.IO;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Imoni.UI.Editor
{
    /// <summary>Builds the optional pre-round loading scene without changing the startup Loading scene.</summary>
    public static class ImoniWorldLoadingBuilder
    {
        private const string ScenePath = "Assets/Scenes/WorldLoading.unity";
        private const string PrefabPath = "Assets/Prefabs/UI/WorldLoadingScreen.prefab";
        private static readonly Color Ink = C("4A2E22"), Cream = C("FFF3D6"), Gold = C("F7B92B"), Green = C("34421C"), Rust = C("8B2310");
        private static Sprite rounded;
        private static TMP_FontAsset heading, body, heavy;

        [MenuItem("Imoni/World Loading/Build World Loading Screen")]
        public static void Build()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Stop Play Mode first.");
            var active = SceneManager.GetActiveScene();
            if (active.isDirty) throw new InvalidOperationException("Save the current scene before building WorldLoading.");
            heading = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/Fonts/Home/Home_DelaGothicOne.asset");
            body = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/Fonts/Home/Home_ZenMaruBold.asset");
            heavy = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/Fonts/Home/Home_ZenMaruBlack.asset");
            rounded = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/ImoniUI/Home/home_rounded_white.png");
            var potSprite = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/ImoniUI/Home/home_pot.png");
            var buttonPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/UI/PrimaryButton.prefab");
            if (!heading || !body || !heavy || !rounded || !potSprite || !buttonPrefab)
                throw new InvalidOperationException("Shared UI fonts, sprites or PrimaryButton prefab are missing.");
            string characters = "ワールドを読み込み中…準備ができたよ!チーム「鍋底同盟」共通自由シード設定確認地形とマップ資源表示移動採集仲間平野森林川14か所タップローカル3人あなたたろうはなこヒントラウンド1スタート!OK0123456789%・";
            characters += File.ReadAllText("Assets/Scripts/UI/WorldLoadingController.cs");
            foreach (var font in new[] { heading, body, heavy }) { font.TryAddCharacters(characters, out _); EditorUtility.SetDirty(font); }

            Directory.CreateDirectory("Assets/Scenes");
            Directory.CreateDirectory("Assets/Prefabs/UI");
            var scene = active.path == ScenePath ? active : File.Exists(ScenePath)
                ? EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single)
                : EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            if (!scene.GetRootGameObjects().SelectMany(go => go.GetComponentsInChildren<Camera>(true)).Any())
            {
                var camera = new GameObject("Main Camera", typeof(Camera)).GetComponent<Camera>();
                camera.tag = "MainCamera";
                camera.orthographic = true;
                camera.clearFlags = CameraClearFlags.SolidColor;
                camera.backgroundColor = Gold;
                camera.transform.position = new Vector3(0, 0, -10);
            }
            var canvas = scene.GetRootGameObjects().SelectMany(go => go.GetComponentsInChildren<Canvas>(true)).FirstOrDefault(c => c.isRootCanvas);
            if (!canvas) canvas = new GameObject("Canvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster)).GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            if (!canvas.GetComponent<GraphicRaycaster>()) canvas.gameObject.AddComponent<GraphicRaycaster>();
            var scaler = canvas.GetComponent<CanvasScaler>() ?? canvas.gameObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(390, 844);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.Expand;
            scaler.referencePixelsPerUnit = 100;
            var eventSystem = scene.GetRootGameObjects().SelectMany(go => go.GetComponentsInChildren<EventSystem>(true)).FirstOrDefault();
            if (!eventSystem)
            {
                eventSystem = new GameObject("EventSystem", typeof(EventSystem)).GetComponent<EventSystem>();
                ConfigureInput(eventSystem.gameObject.AddComponent<InputSystemUIInputModule>());
            }
            else if (!eventSystem.GetComponent<BaseInputModule>())
                ConfigureInput(eventSystem.gameObject.AddComponent<InputSystemUIInputModule>());
            foreach (var name in new[] { "WorldLoadingBackground", "WorldLoadingScreen" })
            {
                var old = canvas.transform.Find(name);
                if (old) UnityEngine.Object.DestroyImmediate(old.gameObject);
            }
            var background = Img(canvas.transform, "WorldLoadingBackground", 0, 0, 390, 844, Gold);
            Stretch(background.rectTransform);
            var root = Rect(canvas.transform, "WorldLoadingScreen", 0, 0, 390, 844);
            root.gameObject.AddComponent<HomeScreenFit>();
            var ui = root.gameObject.AddComponent<WorldLoadingController>();
            BuildPattern(root);
            var pot = Img(root, "Pot", 125, 70, 140, 120, Color.white);
            pot.sprite = potSprite; pot.preserveAspect = true;
            pot.rectTransform.pivot = new Vector2(.5f, .5f);
            pot.rectTransform.anchoredPosition = new Vector2(195, -130);
            ui.pot = pot.rectTransform;
            var title = Label(root, "Headline", "ワールドを読み込み中…", 20, 198, 350, 39, 28, heading, Rust);
            var shadow = title.gameObject.AddComponent<Shadow>(); shadow.effectColor = Cream; shadow.effectDistance = new Vector2(3, -3);
            ui.headline = title;
            ui.sessionText = Label(root, "Session", "チーム「鍋底同盟」 · 共通シード", 20, 245, 350, 18, 13, heavy, Ink);

            var panel = Panel(root, "ProgressCard", 20, 280, 350, 278, Cream, Ink, 4, 22);
            ui.steps = new WorldLoadingController.StepView[5];
            string[] labels = { "シード設定を確認", "地形とマップを読み込み", "資源表示を準備", "移動と採集の準備", "仲間の表示を準備" };
            for (int i = 0; i < 5; i++)
            {
                float y = 18 + i * 38;
                var step = new WorldLoadingController.StepView();
                step.label = Label(panel.transform, "Step" + i + "Label", labels[i], 54, y, 178, 28, 14, heavy, i == 0 ? Ink : C("8C6A4A"), TextAlignmentOptions.MidlineLeft);
                step.note = Label(panel.transform, "Step" + i + "Note", "", 232, y, 98, 28, 10, body, C("6B4A3A"), TextAlignmentOptions.MidlineRight);
                var done = Img(panel.transform, "Step" + i + "Done", 20, y + 2, 24, 24, Green, 12);
                Line(done.transform, "CheckShort", new Vector2(7, 12.5f), new Vector2(10.2f, 15.7f), Cream, 2.8f);
                Line(done.transform, "CheckLong", new Vector2(10.2f, 15.7f), new Vector2(17, 9), Cream, 2.8f);
                step.doneRoot = done.gameObject; done.gameObject.SetActive(false);
                var pending = Panel(panel.transform, "Step" + i + "Pending", 20, y + 2, 24, 24, Cream, C("C9A27A"), 2.5f, 12);
                step.pendingRoot = pending.gameObject; pending.gameObject.SetActive(i != 0);
                var spinner = Rect(panel.transform, "Step" + i + "Active", 32, y + 14, 24, 24);
                spinner.pivot = new Vector2(.5f, .5f);
                Panel(spinner, "Ring", 0, 0, 24, 24, Cream, C("E9D5AE"), 3, 12);
                for (int arc = 0; arc < 8; arc++)
                {
                    float a = (-90 + arc * 12f) * Mathf.Deg2Rad;
                    float b = a + 14f * Mathf.Deg2Rad;
                    Line(spinner, "Arc" + arc, new Vector2(12 + Mathf.Cos(a) * 10.5f, 12 + Mathf.Sin(a) * 10.5f),
                        new Vector2(12 + Mathf.Cos(b) * 10.5f, 12 + Mathf.Sin(b) * 10.5f), Rust, 3f);
                }
                step.activeRoot = spinner.gameObject; step.spinner = spinner; spinner.gameObject.SetActive(i == 0);
                ui.steps[i] = step;
            }
            var progress = Panel(panel.transform, "ProgressTrack", 20, 220, 310, 22, Color.white, Ink, 3, 11);
            var fill = Img(progress.transform, "Progress", 3, 3, 304, 16, C("6B9B3A"));
            fill.sprite = rounded; fill.fillAmount = 0;
            fill.gameObject.AddComponent<RoundedProgressFill>().Configure((RectTransform)progress.transform.Find("Fill"));
            ui.progressFill = fill;
            ui.percentText = Label(panel.transform, "Percent", "0%", 20, 248, 310, 16, 12, heavy, Ink, TextAlignmentOptions.MidlineRight);

            ui.members = new WorldLoadingController.MemberView[3];
            var memberNames = new[] { "あなた", "たろう", "はなこ" };
            var memberColors = new[] { C("8FAFC4"), C("C9A27A"), C("6B9B3A") };
            for (int i = 0; i < 3; i++)
            {
                var member = Rect(root, "Member" + i, 76 + i * 84, 580, 70, 88);
                var avatar = Panel(member, "Avatar", 13, 0, 44, 44, memberColors[i], Ink, 3, 22);
                var fade = avatar.gameObject.AddComponent<CanvasGroup>(); fade.alpha = .5f;
                Label(member, "Name", memberNames[i], 0, 50, 70, 16, 11, heavy, Ink);
                var badge = Img(member, "Badge", 6, 70, 58, 18, C("EFD38A"), 9);
                var state = Label(member, "State", "準備中", 6, 70, 58, 18, 10, heavy, Ink);
                ui.members[i] = new WorldLoadingController.MemberView { avatar = fade, badge = badge, state = state };
            }
            var hint = Panel(root, "Hint", 20, 718, 350, 86, Cream, Ink, 3, 16);
            Label(hint.transform, "Caption", "ヒント", 16, 10, 318, 17, 11, heavy, Rust, TextAlignmentOptions.MidlineLeft);
            ui.tipText = Label(hint.transform, "Tip", "行きたい場所をタップして移動。素材の近くでは自動で採集するよ", 16, 31, 318, 42, 13, body, Ink, TextAlignmentOptions.TopLeft);
            ui.tipText.textWrappingMode = TextWrappingModes.Normal;
            ui.hintRoot = hint.gameObject;
            var start = Rect(root, "StartGroup", 20, 740, 350, 69);
            Img(start, "Shadow", 0, 5, 350, 64, C("1E2610"), 32);
            var buttonObject = (GameObject)PrefabUtility.InstantiatePrefab(buttonPrefab, start);
            buttonObject.name = "StartRoundButton";
            SetRect((RectTransform)buttonObject.transform, 0, 0, 350, 64);
            var button = buttonObject.GetComponent<Button>(); button.onClick = new Button.ButtonClickedEvent();
            button.navigation = new Navigation { mode = Navigation.Mode.None };
            var buttonImage = buttonObject.GetComponent<Image>(); buttonImage.sprite = rounded; buttonImage.type = Image.Type.Sliced;
            buttonImage.pixelsPerUnitMultiplier = 1; buttonImage.color = Green;
            var buttonText = buttonObject.GetComponentInChildren<TextMeshProUGUI>();
            SetRect(buttonText.rectTransform, 0, 0, 350, 64);
            Style(buttonText, "ラウンド1 スタート!", 22, heading, Cream, TextAlignmentOptions.Midline);
            ui.startButton = button; ui.startButtonText = buttonText; ui.startRoot = start.gameObject;
            UnityEventTools.AddPersistentListener(button.onClick, ui.StartRound);
            start.gameObject.SetActive(false);

            foreach (var component in root.GetComponentsInChildren<Component>(true))
                if (component && PrefabUtility.IsPartOfPrefabInstance(component)) PrefabUtility.RecordPrefabInstancePropertyModifications(component);
            foreach (var node in root.GetComponentsInChildren<Transform>(true))
                if (PrefabUtility.IsPartOfPrefabInstance(node.gameObject)) PrefabUtility.RecordPrefabInstancePropertyModifications(node.gameObject);
            PrefabUtility.SaveAsPrefabAssetAndConnect(root.gameObject, PrefabPath, InteractionMode.AutomatedAction);
            var scenes = EditorBuildSettings.scenes.ToList();
            var entry = scenes.FirstOrDefault(s => s.path == ScenePath);
            if (entry == null) scenes.Add(new EditorBuildSettingsScene(ScenePath, true)); else entry.enabled = true;
            EditorBuildSettings.scenes = scenes.ToArray();
            EditorSceneManager.SaveScene(scene, ScenePath);
            AssetDatabase.SaveAssets();
            foreach (var text in root.GetComponentsInChildren<TextMeshProUGUI>())
                if (text && text.isActiveAndEnabled && text.canvasRenderer) text.SetAllDirty();
            Canvas.ForceUpdateCanvases();
            Selection.activeGameObject = root.gameObject;
            Debug.Log("IMONI_WORLD_LOADING_BUILT: WorldLoading scene and prefab saved; Game preload waits for StartRound.");
        }

        private static void BuildPattern(Transform root)
        {
            var pattern = Rect(root, "TaroPattern", 0, 0, 390, 844);
            pattern.gameObject.AddComponent<RectMask2D>();
            var pale = C("FFE3A3");
            for (int y = 0; y < 9; y++)
                for (int x = 0; x < 5; x++)
                {
                    var a = Panel(pattern, "TaroA", x * 96 + 11, y * 104 + 12, 22, 28, Gold, pale, 2, 11).rectTransform;
                    a.localRotation = Quaternion.Euler(0, 0, 18);
                    var b = Panel(pattern, "TaroB", x * 96 + 57, y * 104 + 64, 26, 20, Gold, pale, 2, 10).rectTransform;
                    b.localRotation = Quaternion.Euler(0, 0, -24);
                    Panel(pattern, "DotA", x * 96 + 68, y * 104 + 18, 8, 8, Gold, pale, 1.5f, 4);
                    Panel(pattern, "DotB", x * 96 + 27, y * 104 + 81, 6, 6, Gold, pale, 1.5f, 3);
                }
        }

        private static void ConfigureInput(InputSystemUIInputModule module)
        {
            const string inputPath = "Assets/InputSystem_Actions.inputactions";
            var asset = AssetDatabase.LoadAssetAtPath<InputActionAsset>(inputPath);
            if (!asset) throw new InvalidOperationException("Project input actions are missing.");
            var references = AssetDatabase.LoadAllAssetsAtPath(inputPath).OfType<InputActionReference>().ToArray();
            InputActionReference Action(string name)
            {
                var reference = references.FirstOrDefault(r => r.action != null && r.action.actionMap.name == "UI" && r.action.name == name);
                if (!reference) throw new InvalidOperationException("UI input action is missing: " + name);
                return reference;
            }
            // Reuse persistent imported references so input survives saving and reopening the scene.
            module.actionsAsset = asset;
            module.point = Action("Point"); module.move = Action("Navigate");
            module.submit = Action("Submit"); module.cancel = Action("Cancel");
            module.leftClick = Action("Click"); module.rightClick = Action("RightClick");
            module.middleClick = Action("MiddleClick"); module.scrollWheel = Action("ScrollWheel");
            module.trackedDevicePosition = Action("TrackedDevicePosition");
            module.trackedDeviceOrientation = Action("TrackedDeviceOrientation");
        }

        private static RectTransform Rect(Transform parent, string name, float x, float y, float w, float h)
        {
            var rect = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
            rect.gameObject.layer = 5; rect.SetParent(parent, false); SetRect(rect, x, y, w, h); return rect;
        }
        private static void SetRect(RectTransform rect, float x, float y, float w, float h)
        {
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0, 1);
            rect.anchoredPosition = new Vector2(x, -y); rect.sizeDelta = new Vector2(w, h);
            rect.localScale = Vector3.one; rect.localRotation = Quaternion.identity;
        }
        private static void Stretch(RectTransform rect) { rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one; rect.offsetMin = rect.offsetMax = Vector2.zero; }
        private static Image Img(Transform parent, string name, float x, float y, float w, float h, Color color, float radius = 0)
        {
            var image = Rect(parent, name, x, y, w, h).gameObject.AddComponent<Image>();
            image.color = color; image.raycastTarget = false;
            if (radius > 0) { image.sprite = rounded; image.type = Image.Type.Sliced; image.pixelsPerUnitMultiplier = 32 / radius; }
            return image;
        }
        private static Image Panel(Transform parent, string name, float x, float y, float w, float h, Color fill, Color border, float stroke, float radius)
        {
            var image = Img(parent, name, x, y, w, h, border, radius);
            Img(image.transform, "Fill", stroke, stroke, w - stroke * 2, h - stroke * 2, fill, Mathf.Max(.5f, radius - stroke));
            return image;
        }
        private static void Line(Transform parent, string name, Vector2 from, Vector2 to, Color color, float width)
        {
            Vector2 delta = to - from;
            var line = Img(parent, name, (from.x + to.x) * .5f, (from.y + to.y) * .5f, delta.magnitude, width, color, width * .5f).rectTransform;
            line.pivot = new Vector2(.5f, .5f); line.localRotation = Quaternion.Euler(0, 0, Mathf.Atan2(-delta.y, delta.x) * Mathf.Rad2Deg);
        }
        private static TextMeshProUGUI Label(Transform parent, string name, string value, float x, float y, float w, float h, float size, TMP_FontAsset font, Color color, TextAlignmentOptions alignment = TextAlignmentOptions.Midline)
        {
            var text = Rect(parent, name, x, y, w, h).gameObject.AddComponent<TextMeshProUGUI>();
            Style(text, value, size, font, color, alignment); return text;
        }
        private static void Style(TextMeshProUGUI text, string value, float size, TMP_FontAsset font, Color color, TextAlignmentOptions alignment)
        {
            text.text = value; text.font = font; text.fontSharedMaterial = font.material; text.fontSize = size;
            text.color = color; text.alignment = alignment; text.enableAutoSizing = false; text.fontStyle = FontStyles.Normal;
            text.textWrappingMode = TextWrappingModes.NoWrap; text.raycastTarget = false;
        }
        private static Color C(string hex) { ColorUtility.TryParseHtmlString("#" + hex, out var color); return color; }
    }
}
