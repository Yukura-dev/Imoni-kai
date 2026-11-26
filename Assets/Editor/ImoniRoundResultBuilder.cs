using System;
using System.IO;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace Imoni.UI.Editor
{
    /// <summary>Editable native UI for the cooking presentation and round results from mock 2.</summary>
    public static class ImoniRoundResultBuilder
    {
        private const string ScenePath = "Assets/Scenes/RoundResult.unity";
        private const string PrefabPath = "Assets/Prefabs/UI/RoundResultScreen.prefab";
        private const string Primary = "Assets/Prefabs/UI/PrimaryButton.prefab";
        private const string YellowButton = "Assets/Prefabs/UI/YellowButton.prefab";
        private static readonly Color Ink = C("4A2E22"), Cream = C("FFF3D6"), Gold = C("F7B92B"), Green = C("34421C"), Rust = C("8B2310");
        private static Sprite rounded, potSprite;
        private static TMP_FontAsset heading, body, heavy;
        private static RoundResultController ui;

        [MenuItem("Imoni/Round Result/Build Round Result Screen")]
        public static void Build()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Stop Play Mode first.");
            var active = SceneManager.GetActiveScene();
            if (active.isDirty) throw new InvalidOperationException("Save the current scene before building RoundResult.");
            // The legacy generator must not replace the reviewed UI Toolkit effect with its old Canvas artwork.
            if (File.Exists("Assets/Imoni/Config/GameConfig.json") && AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath))
            {
                global::Imoni.Editor.ImoniLegacyEffectSetup.SetupProject();
                return;
            }
            heading = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/Fonts/Home/Home_DelaGothicOne.asset");
            body = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/Fonts/Home/Home_ZenMaruBold.asset");
            heavy = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/Fonts/Home/Home_ZenMaruBlack.asset");
            rounded = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/ImoniUI/Home/home_rounded_white.png");
            potSprite = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/ImoniUI/Home/home_pot.png");
            if (!heading || !body || !heavy || !rounded || !potSprite)
                throw new InvalidOperationException("Shared UI fonts and sprites are missing.");
            string characters = File.ReadAllText("Assets/Editor/ImoniRoundResultBuilder.cs") + File.ReadAllText("Assets/Scripts/UI/RoundResultController.cs");
            foreach (var font in new[] { heading, body, heavy })
            {
                var pending = new string(characters.Where(c => !char.IsControl(c) && !font.HasCharacter(c)).Distinct().ToArray());
                if (pending.Length > 0) font.TryAddCharacters(pending, out _);
                EditorUtility.SetDirty(font);
            }
            Directory.CreateDirectory("Assets/Scenes");
            Directory.CreateDirectory("Assets/Prefabs/UI");
            var scene = active.path == ScenePath ? active : File.Exists(ScenePath)
                ? EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single)
                : EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            if (!scene.GetRootGameObjects().SelectMany(go => go.GetComponentsInChildren<Camera>(true)).Any())
            {
                var camera = new GameObject("Main Camera", typeof(Camera)).GetComponent<Camera>();
                camera.tag = "MainCamera"; camera.orthographic = true;
                camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = Gold;
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
            else if (!eventSystem.GetComponent<BaseInputModule>()) ConfigureInput(eventSystem.gameObject.AddComponent<InputSystemUIInputModule>());
            foreach (var name in new[] { "RoundResultBackground", "RoundResultScreen" })
            {
                var old = canvas.transform.Find(name);
                if (old) Object.DestroyImmediate(old.gameObject);
            }
            var background = Img(canvas.transform, "RoundResultBackground", 0, 0, 390, 844, Gold);
            Stretch(background.rectTransform);
            var root = Rect(canvas.transform, "RoundResultScreen", 0, 0, 390, 844);
            root.gameObject.AddComponent<HomeScreenFit>();
            root.gameObject.AddComponent<RectMask2D>();
            ui = root.gameObject.AddComponent<RoundResultController>();
            BuildCooking(root);
            BuildSuccess(root);
            BuildFailure(root);
            ui.successRoot.SetActive(false);
            ui.failureRoot.SetActive(false);
            ui.cookingNextRoot.SetActive(false);
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
            Debug.Log("IMONI_ROUND_RESULT_BUILT: Cooking, success and failure UI saved with shared button prefabs and rounded progress bars.");
        }

        private static void BuildCooking(Transform root)
        {
            var cooking = Rect(root, "Cooking", 0, 0, 390, 844);
            ui.cookingRoot = cooking.gameObject;
            var colors = new[] { C("FF6FB1"), C("FFB347"), C("FFE95C"), C("6FE3A0"), C("5CC8FF"), C("A57CFF"), C("FF6FB1") };
            var rainbow = Rect(cooking, "RainbowBackground", 0, 0, 390, 844);
            for (int i = 0; i < 48; i++)
            {
                float t = i / 47f * (colors.Length - 1);
                int index = Mathf.Min(colors.Length - 2, Mathf.FloorToInt(t));
                Img(rainbow, "ColorBand" + i, 0, i * 844f / 48, 390, 844f / 48 + 1, Color.Lerp(colors[index], colors[index + 1], t - index));
            }
            var rays = Rect(cooking, "Sunburst", 195, 551, 0, 0);
            for (int i = 0; i < 18; i++)
            {
                var ray = Img(rays, "Ray" + i, 0, 0, 14, 1250, new Color(1, 1, 1, .16f)).rectTransform;
                ray.pivot = new Vector2(.5f, .5f); ray.localRotation = Quaternion.Euler(0, 0, i * 20);
            }
            for (int i = 0; i < 20; i++)
            {
                float x = 10 + (i * 79 % 370), y = 235 + (i * 91 % 430);
                var sparkle = Rect(cooking, "Sparkle" + i, x, y, 0, 0);
                var horizontal = Img(sparkle, "Horizontal", 0, 0, i % 3 == 0 ? 22 : 12, 3, new Color(1, 1, 1, .8f), 1.5f).rectTransform;
                horizontal.pivot = new Vector2(.5f, .5f);
                var vertical = Img(sparkle, "Vertical", 0, 0, 3, i % 3 == 0 ? 22 : 12, new Color(1, 1, 1, .8f), 1.5f).rectTransform;
                vertical.pivot = new Vector2(.5f, .5f);
                sparkle.localRotation = Quaternion.Euler(0, 0, i * 23);
            }
            var cookingBadge = Img(cooking, "CookingBadge", 14, 44, 140, 40, Cream, 20);
            Label(cookingBadge.transform, "Caption", "芋煮会の時間！", 0, 0, 140, 40, 14, heavy, Ink);
            SharedButton(cooking, "Skip", Primary, "スキップ", 292, 44, 84, 40, 12, heavy, Cream, Ink, ui.Skip);
            var score = Panel(cooking, "ScoreCard", 14, 92, 362, 110, Cream, Ink, 3, 18);
            Label(score.transform, "Caption", "芋煮スコア", 17, 10, 110, 20, 12, heavy, Ink, TextAlignmentOptions.MidlineLeft);
            ui.levelText = Label(score.transform, "Level", "Lv.0 ことこと…", 130, 10, 215, 20, 12, heavy, Rust, TextAlignmentOptions.MidlineRight);
            ui.scoreText = Label(score.transform, "Score", "0", 17, 34, 328, 48, 40, heading, Rust, TextAlignmentOptions.MidlineRight);
            ui.cookingProgress = Progress(score.transform, "Progress", 17, 90, 328, 10, C("A57CFF"));
            ui.cookingTitle = Label(cooking, "CookingTitle", "芋煮を\n調理中…", 20, 254, 350, 134, 46, heading, Rust);
            var titleShadow = ui.cookingTitle.gameObject.AddComponent<Shadow>();
            titleShadow.effectColor = Rust; titleShadow.effectDistance = new Vector2(4, -4);
            ui.cookingSubtitle = Label(cooking, "CookingSubtitle", "採れた素材を鍋に入れよう", 20, 395, 350, 48, 15, heavy, Ink);
            ui.cookingSubtitle.textWrappingMode = TextWrappingModes.Normal;
            ui.fallingIngredients = new RectTransform[4];
            string[] ingredientSprites = { "taro", "negi", "mushroom", "taro" };
            for (int i = 0; i < 4; i++)
            {
                var ingredient = Img(cooking, "FallingIngredient" + i, 64 + i * 72, 225, 42, 44, Color.white);
                ingredient.sprite = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/ImoniUI/Game/game_" + ingredientSprites[i] + ".png");
                if (!ingredient.sprite) throw new InvalidOperationException("Missing ingredient sprite " + ingredientSprites[i]);
                ingredient.preserveAspect = true;
                ingredient.rectTransform.pivot = new Vector2(.5f, .5f);
                ui.fallingIngredients[i] = ingredient.rectTransform;
            }
            var pot = Img(cooking, "Pot", 195, 562, 280, 220, Color.white);
            pot.sprite = potSprite; pot.preserveAspect = true;
            pot.rectTransform.pivot = new Vector2(.5f, .5f);
            ui.pot = pot.rectTransform;
            var next = Rect(cooking, "NextActions", 24, 704, 342, 112);
            ui.cookingNextRoot = next.gameObject;
            SharedButton(next, "ShowResult", Primary, "結果を見る", 0, 0, 342, 60, 22, heading, Cream, Green, ui.ShowResult, true);
            SharedButton(next, "Replay", Primary, "もう一回見る", 0, 68, 342, 44, 14, heavy, Ink, Cream, ui.Replay, false, Ink, 3);
        }

        private static void BuildSuccess(Transform root)
        {
            var success = Rect(root, "Success", 0, 0, 390, 844);
            ui.successRoot = success.gameObject;
            Img(success, "Background", 0, 0, 390, 844, Gold);
            var badge = Img(success, "RoundBadge", 16, 52, 165, 28, Ink, 14);
            ui.successRoundText = Label(badge.transform, "Round", "ラウンド 1/5 結果", 0, 0, 165, 28, 13, heavy, Cream);
            Label(success, "SuccessCaption", "芋煮会 成功!", 235, 52, 139, 28, 12, heavy, Ink, TextAlignmentOptions.MidlineRight);
            var rank = Panel(success, "RankBadge", 16, 92, 100, 100, Rust, Ink, 4, 50);
            Label(rank.transform, "Caption", "ランク", 0, 15, 100, 20, 11, heavy, Cream);
            ui.rankText = Label(rank.transform, "Rank", "A", 0, 34, 100, 55, 40, heading, Cream);
            ui.successTitle = Label(success, "Title", "うまい芋煮が\nできた!", 130, 92, 244, 100, 29, heading, Green, TextAlignmentOptions.MidlineLeft);
            ui.successRequirements = Requirements(success, "RequiredMaterials", 16, 204, 358, false);
            var bonus = Panel(success, "Bonus", 16, 338, 358, 87, Cream, Ink, 3, 16);
            ui.bonusText = Label(bonus.transform, "Summary", "ボーナス:キノコ 0/30   0% 報酬×1", 15, 10, 328, 22, 12, heavy, Ink, TextAlignmentOptions.MidlineLeft);
            ui.bonusStages = new TMP_Text[4];
            string[] stages = { "100% ×1", "150% ×2", "200% ×4", "300% ×8" };
            for (int i = 0; i < 4; i++)
            {
                var stage = Img(bonus.transform, "Stage" + i, 15 + i * 83, 46, 79, 25, Color.white, 8);
                var outline = stage.gameObject.AddComponent<Outline>(); outline.effectColor = Ink; outline.effectDistance = new Vector2(1, -1);
                ui.bonusStages[i] = Label(stage.transform, "Label", stages[i], 0, 0, 79, 25, 11, heavy, C("6B4A3A"));
            }
            var breakdown = Panel(success, "ScoreBreakdown", 16, 437, 358, 116, Cream, Ink, 3, 16);
            Label(breakdown.transform, "Heading", "スコア内訳（0.5×R + 0.5×B×T）", 15, 10, 328, 21, 13, heavy, Ink, TextAlignmentOptions.MidlineLeft);
            ui.scoreBreakdownText = Label(breakdown.transform, "Values", "素材ポイント R\nボーナスポイント B × T", 15, 36, 328, 46, 12, heavy, Ink, TextAlignmentOptions.MidlineLeft);
            ui.scoreBreakdownText.textWrappingMode = TextWrappingModes.Normal;
            Label(breakdown.transform, "SoldMaterialNote", "売却した素材はスコアに含まれません", 15, 88, 328, 17, 10, body, C("6B4A3A"), TextAlignmentOptions.MidlineLeft);
            var rewards = Img(success, "Rewards", 16, 565, 358, 128, Ink, 18);
            Label(rewards.transform, "EarnedCaption", "獲得スコア", 14, 10, 158, 18, 11, body, Cream, TextAlignmentOptions.MidlineLeft);
            ui.earnedScoreText = Label(rewards.transform, "EarnedScore", "0", 14, 29, 158, 34, 22, heading, Gold, TextAlignmentOptions.MidlineLeft);
            Label(rewards.transform, "FundsCaption", "獲得資金", 186, 10, 158, 18, 11, body, Cream, TextAlignmentOptions.MidlineLeft);
            ui.rewardText = Label(rewards.transform, "Funds", "+0G", 186, 29, 158, 34, 22, heading, Cream, TextAlignmentOptions.MidlineLeft);
            Label(rewards.transform, "TotalCaption", "累積スコア", 14, 76, 158, 17, 11, body, Cream, TextAlignmentOptions.MidlineLeft);
            ui.totalScoreText = Label(rewards.transform, "Total", "0", 14, 95, 158, 24, 16, heavy, Cream, TextAlignmentOptions.MidlineLeft);
            Label(rewards.transform, "ModeCaption", "通常モード", 186, 76, 158, 17, 11, body, Cream, TextAlignmentOptions.MidlineLeft);
            Label(rewards.transform, "Mode", "5ラウンドに挑戦", 186, 95, 158, 24, 14, heavy, Cream, TextAlignmentOptions.MidlineLeft);
            ui.nextRoundHint = Label(success, "NextRoundHint", "資金と強化は引き継ぎ、素材はリセット", 16, 704, 358, 34, 12, body, Ink);
            ui.nextRoundHint.textWrappingMode = TextWrappingModes.Normal;
            var next = SharedButton(success, "NextRound", Primary, "ラウンド2へ", 16, 749, 358, 60, 22, heading, Cream, Green, ui.NextRound, true);
            ui.nextRoundButtonText = next.GetComponentInChildren<TMP_Text>();
        }

        private static void BuildFailure(Transform root)
        {
            var failure = Rect(root, "Failure", 0, 0, 390, 844);
            ui.failureRoot = failure.gameObject;
            Img(failure, "Background", 0, 0, 390, 844, Ink);
            var badge = Img(failure, "RoundBadge", 18, 64, 165, 28, Cream, 14);
            ui.failureRoundText = Label(badge.transform, "Round", "ラウンド 1/5 結果", 0, 0, 165, 28, 13, heavy, Ink);
            Label(failure, "Title", "芋煮会 失敗…", 18, 107, 354, 49, 34, heading, Gold, TextAlignmentOptions.MidlineLeft);
            var explanation = Label(failure, "Description", "必須素材が足りなくて、\n芋煮が完成しなかった。", 18, 170, 354, 46, 14, body, Cream, TextAlignmentOptions.MidlineLeft);
            explanation.textWrappingMode = TextWrappingModes.Normal;
            ui.failureRequirements = Requirements(failure, "RequiredMaterials", 18, 230, 354, true);
            var reset = Panel(failure, "ResetInfo", 18, 374, 354, 160, Ink, Gold, 2, 18);
            Label(reset.transform, "Heading", "リセットされるもの", 16, 13, 322, 22, 14, heavy, Gold, TextAlignmentOptions.MidlineLeft);
            Label(reset.transform, "Details", "・共有倉庫の素材 / 共有資金\n・全員の個人強化 / 連続記録\n・R1からもう一度挑戦", 16, 41, 322, 58, 13, body, Cream, TextAlignmentOptions.MidlineLeft);
            Label(reset.transform, "RankingNote", "今回の挑戦はランキングに登録されません", 16, 106, 322, 20, 12, heavy, Gold, TextAlignmentOptions.MidlineLeft);
            Label(reset.transform, "KeepNote", "過去の記録・会員証・二つ名はそのまま", 16, 132, 322, 17, 11, body, C("DCC9A8"), TextAlignmentOptions.MidlineLeft);
            var reached = Img(failure, "Reached", 18, 548, 173, 72, C("5E3D2E"), 14);
            Label(reached.transform, "Caption", "今回の到達", 12, 10, 149, 18, 11, body, Cream, TextAlignmentOptions.MidlineLeft);
            ui.failureReachedText = Label(reached.transform, "Round", "R1", 12, 29, 149, 33, 20, heading, Cream, TextAlignmentOptions.MidlineLeft);
            var score = Img(failure, "ReferenceScore", 199, 548, 173, 72, C("5E3D2E"), 14);
            Label(score.transform, "Caption", "累積スコア（参考）", 12, 10, 149, 18, 11, body, Cream, TextAlignmentOptions.MidlineLeft);
            ui.failureScoreText = Label(score.transform, "Score", "0", 12, 29, 149, 33, 20, heading, Cream, TextAlignmentOptions.MidlineLeft);
            SharedButton(failure, "Retry", YellowButton, "R1から再挑戦", 18, 690, 354, 60, 22, heading, Ink, Gold, ui.Retry, true);
            var home = SharedButton(failure, "ReturnHome", Primary, "ホームへ戻る", 18, 764, 354, 44, 14, heavy, Cream, Color.clear, ui.ReturnHome);
            home.GetComponentInChildren<TMP_Text>().fontStyle = FontStyles.Underline;
        }

        private static RoundResultController.RequirementView[] Requirements(Transform parent, string name, float x, float y, float width, bool failure)
        {
            var panel = Panel(parent, name, x, y, width, failure ? 130 : 122, Cream, failure ? Cream : Ink, failure ? 0 : 3, 16);
            Label(panel.transform, "Heading", "必須素材（品質換算）", 15, 10, width - 30, 22, 13, heavy, Ink, TextAlignmentOptions.MidlineLeft);
            var views = new RoundResultController.RequirementView[3];
            string[] names = { "薪", "里芋", "ねぎ" };
            for (int i = 0; i < 3; i++)
            {
                float rowY = 39 + i * 25;
                float trackX = failure ? 79 : 75, trackWidth = failure ? 148 : 184;
                var label = Label(panel.transform, "Material" + i, names[i], 15, rowY, 56, 20, 13, heavy, Ink, TextAlignmentOptions.MidlineLeft);
                var fill = Progress(panel.transform, "RequiredProgress" + i, trackX, rowY + 6, trackWidth, 8, C("6B9B3A"));
                var count = Label(panel.transform, "Count" + i, "0/0", failure ? 235 : 267, rowY, failure ? 104 : 76, 20, 12, heavy, Ink, TextAlignmentOptions.MidlineRight);
                views[i] = new RoundResultController.RequirementView { label = label, count = count, fill = fill };
            }
            return views;
        }

        private static Image Progress(Transform parent, string name, float x, float y, float w, float h, Color color)
        {
            var track = Img(parent, name + "Track", x, y, w, h, C("E9D5AE"), h * .5f);
            var fill = Img(parent, name, x, y, w, h, color);
            fill.sprite = rounded; fill.fillAmount = 0;
            fill.gameObject.AddComponent<RoundedProgressFill>().Configure(track.rectTransform);
            return fill;
        }

        private static Button SharedButton(Transform parent, string name, string source, string caption, float x, float y, float w, float h,
            float size, TMP_FontAsset font, Color foreground, Color background, UnityAction action, bool shadow = false, Color border = default, float stroke = 0)
        {
            var group = Rect(parent, name + "Group", x, y, w, h + (shadow ? 5 : 0));
            if (shadow) Img(group, "Shadow", 0, 5, w, h, source == YellowButton ? C("8C6A2E") : C("1E2610"), h * .5f);
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(source);
            if (!prefab) throw new InvalidOperationException("Missing shared button: " + source);
            var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, group);
            instance.name = name;
            SetRect((RectTransform)instance.transform, 0, 0, w, h);
            var button = instance.GetComponent<Button>();
            button.onClick = new Button.ButtonClickedEvent();
            button.navigation = new Navigation { mode = Navigation.Mode.None };
            var image = instance.GetComponent<Image>();
            image.sprite = rounded; image.type = Image.Type.Sliced; image.pixelsPerUnitMultiplier = 64f / h;
            image.color = stroke > 0 ? border : background;
            if (stroke > 0)
            {
                var fill = Img(instance.transform, "Fill", stroke, stroke, w - stroke * 2, h - stroke * 2, background, h * .5f - stroke);
                fill.transform.SetAsFirstSibling();
            }
            var label = instance.GetComponentInChildren<TextMeshProUGUI>();
            SetRect(label.rectTransform, 0, 0, w, h);
            Style(label, caption, size, font, foreground, TextAlignmentOptions.Midline);
            UnityEventTools.AddPersistentListener(button.onClick, action);
            return button;
        }

        private static void ConfigureInput(InputSystemUIInputModule module)
        {
            const string path = "Assets/InputSystem_Actions.inputactions";
            var asset = AssetDatabase.LoadAssetAtPath<InputActionAsset>(path);
            if (!asset) throw new InvalidOperationException("Project input actions are missing.");
            var references = AssetDatabase.LoadAllAssetsAtPath(path).OfType<InputActionReference>().ToArray();
            InputActionReference Action(string name)
            {
                var reference = references.FirstOrDefault(r => r.action != null && r.action.actionMap.name == "UI" && r.action.name == name);
                if (!reference) throw new InvalidOperationException("UI input action is missing: " + name);
                return reference;
            }
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
            var image = Img(parent, name, x, y, w, h, stroke > 0 ? border : fill, radius);
            if (stroke > 0) Img(image.transform, "Fill", stroke, stroke, w - stroke * 2, h - stroke * 2, fill, Mathf.Max(.5f, radius - stroke));
            return image;
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
        private static Color C(string value) { ColorUtility.TryParseHtmlString("#" + value, out var color); return color; }
    }
}
