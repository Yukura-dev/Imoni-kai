using System;
using System.IO;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace Imoni.UI.Editor
{
    /// <summary>Builds editable native UI from Game.dc.html, with nested shared button prefabs.</summary>
    public static class ImoniGameBuilder
    {
        const string ScenePath = "Assets/Scenes/Game.unity";
        const string ScreenPath = "Assets/Prefabs/UI/GameScreen.prefab";
        const string Primary = "Assets/Prefabs/UI/PrimaryButton.prefab";
        const string Red = "Assets/Prefabs/UI/RedButton.prefab";
        const string YellowButton = "Assets/Prefabs/UI/YellowButton.prefab";
        static readonly Color Ink = C("4A2E22"), Cream = C("FFF3D6"), Gold = C("F7B92B"), Green = C("34421C"), Rust = C("8B2310"), Blue = C("2F5D8A");
        static TMP_FontAsset heading, body, heavy;
        static Sprite rounded;
        static GameScreenController ui;
        static readonly string[] Materials = { "薪", "里芋", "ねぎ", "キノコ" };
        static readonly string[] Qualities = { "普通", "良質", "希少" };

        [MenuItem("Imoni/Game/Build Game Screen")]
        public static void Build()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Stop Play Mode first.");
            var active = SceneManager.GetActiveScene();
            if (active.isDirty && active.path != ScenePath) throw new InvalidOperationException("Save the current scene before building Game.");
            var scene = active.path == ScenePath ? active : EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            heading = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/Fonts/Home/Home_DelaGothicOne.asset");
            body = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/Fonts/Home/Home_ZenMaruBold.asset");
            heavy = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/Fonts/Home/Home_ZenMaruBlack.asset");
            rounded = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/ImoniUI/Home/home_rounded_white.png");
            if (!heading || !body || !heavy || !rounded) throw new InvalidOperationException("Shared UI fonts or rounded sprite are missing.");
            ImportSprites();
            string chars = "ラウンド残り時間必須薪里芋ねぎキノコボーナスあとで発表達成率最高品質連続倉庫売却強化メニュー共有資金個人移動速度採集量運所持保護可単価普通良質希少解除全部数量操作履歴まだ素材がないよ。マップをタップして素材の近くへ移動しようあなたゆくらさんたろうはなこ待機中森奥開放拠点成長秒チームホームへ戻る挑戦は中断芋煮会のだタイムアップ結果必要達未成功失敗一度準備再挑戦通常取引購入確認相談使効果自分だけに発公開された素材すべて鍋投入不足満たしました円％%0123456789/.,:!?()（）×＋−→・★☆LvR";
            chars += File.ReadAllText("Assets/Scripts/UI/GameScreenController.cs");
            foreach (var font in new[] { heading, body, heavy })
            {
                font.TryAddCharacters(chars, out _);
                EditorUtility.SetDirty(font);
            }
            var canvas = scene.GetRootGameObjects().SelectMany(x => x.GetComponentsInChildren<Canvas>(true)).First(c => c.isRootCanvas);
            Undo.RegisterFullObjectHierarchyUndo(canvas.gameObject, "Build Game UI");
            PreservePrototype(scene, canvas);
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = canvas.GetComponent<CanvasScaler>();
            scaler.referenceResolution = new Vector2(390, 844);
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.Expand;
            var background = Img(canvas.transform, "GameBackground", 0, 0, 390, 844, Gold);
            Stretch(background.rectTransform);
            var root = Rect(canvas.transform, "GameScreen", 0, 0, 390, 844);
            root.gameObject.AddComponent<HomeScreenFit>();
            ui = root.gameObject.AddComponent<GameScreenController>();
            BuildHeader(root);
            BuildMap(root);
            BuildNavigation(root);
            BuildWarehouse(root);
            BuildShop(root);
            BuildUpgrade(root);
            BuildMenu(root);
            BuildEnd(root);
            ui.warehousePanel.SetActive(false);
            ui.shopPanel.SetActive(false);
            ui.upgradePanel.SetActive(false);
            ui.menuPanel.SetActive(false);
            ui.endedPanel.SetActive(false);
            ui.selectionPanel.SetActive(false);
            ui.popRoot.SetActive(false);
            foreach (var component in root.GetComponentsInChildren<Component>(true))
                if (component && PrefabUtility.IsPartOfPrefabInstance(component)) PrefabUtility.RecordPrefabInstancePropertyModifications(component);
            foreach (var node in root.GetComponentsInChildren<Transform>(true))
                if (PrefabUtility.IsPartOfPrefabInstance(node.gameObject)) PrefabUtility.RecordPrefabInstancePropertyModifications(node.gameObject);
            Canvas.ForceUpdateCanvases();
            foreach (var text in root.GetComponentsInChildren<TextMeshProUGUI>(true)) text.ForceMeshUpdate(true);
            PrefabUtility.SaveAsPrefabAssetAndConnect(root.gameObject, ScreenPath, InteractionMode.AutomatedAction);
            var scenes = EditorBuildSettings.scenes.ToList();
            var entry = scenes.FirstOrDefault(x => x.path == ScenePath);
            if (entry == null) scenes.Add(new EditorBuildSettingsScene(ScenePath, true)); else entry.enabled = true;
            EditorBuildSettings.scenes = scenes.ToArray();
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();
            // Prefab reconnection can replace renderers cached by the live TMP labels.
            foreach (var text in root.GetComponentsInChildren<TextMeshProUGUI>())
            {
                if (text && text.isActiveAndEnabled && text.canvasRenderer) text.SetAllDirty();
            }
            Canvas.ForceUpdateCanvases();
            Selection.activeGameObject = root.gameObject;
            Debug.Log("IMONI_GAME_BUILT: GameScreen prefab saved; shared buttons, harvest, warehouse, upgrades and menu connected. Original prototype preserved inactive.");
        }

        [MenuItem("Imoni/Game/Update UI From New Mock")]
        public static void UpdateInterface()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Stop Play Mode first.");
            heading = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/Fonts/Home/Home_DelaGothicOne.asset");
            body = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/Fonts/Home/Home_ZenMaruBold.asset");
            heavy = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/Fonts/Home/Home_ZenMaruBlack.asset");
            rounded = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/ImoniUI/Home/home_rounded_white.png");
            ImportSprites();
            string characters = File.ReadAllText("Assets/Editor/ImoniGameBuilder.cs") + File.ReadAllText("Assets/Scripts/UI/GameScreenController.cs");
            foreach (var font in new[] { heading, body, heavy })
            {
                font.TryAddCharacters(characters, out _);
                EditorUtility.SetDirty(font);
            }
            var root = PrefabUtility.LoadPrefabContents(ScreenPath);
            try
            {
                ui = root.GetComponent<GameScreenController>();
                // Keep the user's map, player, resource positions and motion components intact.
                foreach (var name in new[] { "HUD", "GameNavigation", "Warehouse", "Shop", "Upgrades", "TeamMenu", "RoundFinished" })
                {
                    var old = root.transform.Find(name);
                    if (old) Object.DestroyImmediate(old.gameObject);
                }
                BuildHeader(root.transform);
                root.transform.Find("HUD").SetAsFirstSibling();
                BuildNavigation(root.transform);
                BuildWarehouse(root.transform);
                BuildShop(root.transform);
                BuildUpgrade(root.transform);
                BuildMenu(root.transform);
                BuildEnd(root.transform);
                ui.warehousePanel.SetActive(false);
                ui.shopPanel.SetActive(false);
                ui.upgradePanel.SetActive(false);
                ui.menuPanel.SetActive(false);
                ui.endedPanel.SetActive(false);
                ui.selectionPanel.SetActive(false);
                var forestLabel = ui.world.Find("ForestLabel").GetComponent<TMP_Text>();
                forestLabel.text = "R2で探索範囲が拡大";
                forestLabel.fontSize = 11;
                SetRect((RectTransform)ui.world.Find("ForestLabelBackground"), 429, 752, 144, 23);
                SetRect(forestLabel.rectTransform, 429, 752, 144, 23);
                foreach (var component in root.GetComponentsInChildren<Component>(true))
                    if (component && PrefabUtility.IsPartOfPrefabInstance(component)) PrefabUtility.RecordPrefabInstancePropertyModifications(component);
                foreach (var node in root.GetComponentsInChildren<Transform>(true))
                    if (PrefabUtility.IsPartOfPrefabInstance(node.gameObject)) PrefabUtility.RecordPrefabInstancePropertyModifications(node.gameObject);
                PrefabUtility.SaveAsPrefabAsset(root, ScreenPath);
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
            AssetDatabase.SaveAssets();
            Debug.Log("IMONI_V2_GAME_UI_UPDATED: preserved Field/World, player, camera and harvesting; updated HUD and panels.");
        }

        public static void UpdateRoundEnd()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Stop Play Mode first.");
            heading = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/Fonts/Home/Home_DelaGothicOne.asset");
            body = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/Fonts/Home/Home_ZenMaruBold.asset");
            heavy = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/Fonts/Home/Home_ZenMaruBlack.asset");
            rounded = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/ImoniUI/Home/home_rounded_white.png");
            var root = PrefabUtility.LoadPrefabContents(ScreenPath);
            try
            {
                ui = root.GetComponent<GameScreenController>();
                ui.roundText = root.transform.Find("HUD/RoundLabel").GetComponent<TMP_Text>();
                var old = root.transform.Find("RoundFinished");
                if (old) Object.DestroyImmediate(old.gameObject);
                BuildEnd(root.transform);
                ui.endedPanel.SetActive(false);
                foreach (var component in ui.endedPanel.GetComponentsInChildren<Component>(true))
                    if (component && PrefabUtility.IsPartOfPrefabInstance(component)) PrefabUtility.RecordPrefabInstancePropertyModifications(component);
                foreach (var node in ui.endedPanel.GetComponentsInChildren<Transform>(true))
                    if (PrefabUtility.IsPartOfPrefabInstance(node.gameObject)) PrefabUtility.RecordPrefabInstancePropertyModifications(node.gameObject);
                PrefabUtility.SaveAsPrefabAsset(root, ScreenPath);
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
            AssetDatabase.SaveAssets();
            Debug.Log("IMONI_ROUND_END_UPDATED: results button replaces Home; all other Game UI and Field preserved.");
        }

        static void PreservePrototype(Scene scene, Canvas canvas)
        {
            foreach (var name in new[] { "GameScreen", "GameBackground" })
            {
                var old = canvas.transform.Find(name);
                if (old) Undo.DestroyObjectImmediate(old.gameObject);
            }
            var legacy = canvas.transform.Find("LegacyPrototype");
            if (!legacy)
            {
                var children = Enumerable.Range(0, canvas.transform.childCount).Select(i => canvas.transform.GetChild(i)).ToArray();
                legacy = Rect(canvas.transform, "LegacyPrototype", 0, 0, 390, 844);
                Stretch((RectTransform)legacy);
                foreach (var child in children) Undo.SetTransformParent(child, legacy, "Preserve original Game prototype");
                foreach (var root in scene.GetRootGameObjects().Where(x => x.GetComponent<TeamInventory>()))
                    Undo.SetTransformParent(root.transform, legacy, "Preserve original inventory");
            }
            legacy.gameObject.SetActive(false);
        }

        static void BuildHeader(Transform root)
        {
            var header = Rect(root, "HUD", 0, 0, 390, 200);
            Img(header, "Background", 0, 0, 390, 200, Gold);
            Img(header, "BottomBorder", 0, 197, 390, 3, Ink);
            Panel(header, "RoundBadge", 12, 52, 104, 32, Ink, Ink, 0, 16);
            ui.roundText = Text(header, "RoundLabel", "ラウンド 1/5", 20, 52, 88, 32, 14, heavy, Cream);
            ui.timerText = Text(header, "Timer", "5:00", 120, 43.5f, 154, 49, 34, heading, Ink);
            var funds = Panel(header, "Funds", 280, 50.5f, 98, 35, Cream, Ink, 3, 17.5f);
            SpriteImage(funds.transform, "Coin", "coin", 10, 8.5f, 18, 18);
            var fundLabel = Text(funds.transform, "Value", "1,000G", 31, 0, 55, 35, 14, heavy, Ink, TextAlignmentOptions.MidlineRight);
            ui.fundsTexts = new TMP_Text[] { fundLabel };
            Panel(header, "RequiredBadge", 12, 98, 34, 47.77f, Green, Green, 0, 8);
            Text(header, "RequiredLabel", "必\n須", 12, 98, 34, 47.77f, 11, heavy, Cream);
            ui.requirements = new GameScreenController.RequirementView[3];
            for (int i = 0; i < 3; i++)
            {
                var card = Panel(header, "Required" + i, 52 + i * 110.6667f, 98, 104.6667f, 47.77f, Cream, Ink, 2, 10);
                var label = Text(card.transform, "Name", Materials[i], 10, 9.875f, 45, 19, 13, heavy, Ink, TextAlignmentOptions.MidlineLeft);
                var count = Text(card.transform, "Count", "0/" + new[] { 60, 20, 15 }[i], 42, 9.875f, 52.6667f, 19, 12, heavy, Ink, TextAlignmentOptions.MidlineRight);
                var track = Img(card.transform, "ProgressTrack", 10, 31.875f, 84.6667f, 6, C("E9D5AE"), 3);
                var progress = Filled(card.transform, "Progress", 10, 31.875f, 84.6667f, 6, C("6B9B3A"), track.rectTransform);
                progress.fillAmount = 0;
                ui.requirements[i] = new GameScreenController.RequirementView { background = card.transform.Find("Fill").GetComponent<Image>(), label = label, count = count, progress = progress };
            }
            var bonus = Panel(header, "Bonus", 12, 153.7656f, 366, 35.2344f, Cream, Ink, 2, 10);
            Panel(bonus.transform, "Badge", 12, 7.61f, 56, 20, C("B5652B"), Ink, 0, 6);
            Text(bonus.transform, "BadgeLabel", "ボーナス", 12, 7.61f, 56, 20, 11, heavy, Cream);
            ui.bonusText = Text(bonus.transform, "Description", "あと 2:00 で発表!", 76, 0, 177, 35.2344f, 13, heavy, Ink, TextAlignmentOptions.MidlineLeft);
            ui.bonusProgressText = Text(bonus.transform, "Progress", "", 205, 0, 149, 35.2344f, 11, heavy, Rust, TextAlignmentOptions.MidlineRight);
        }

        static void BuildMap(Transform root)
        {
            var map = Rect(root, "Field", 0, 200, 390, 560);
            map.gameObject.AddComponent<RectMask2D>();
            var world = Rect(map, "World", 0, 0, 780, 1120);
            ui.world = world;
            Img(world, "Ground", 0, 0, 780, 1120, C("DCE8C4"));
            SpriteImage(world, "Forest", "terrain_forest", 391, 0, 389, 550);
            SpriteImage(world, "River", "terrain_river", 0, 560, 780, 82).preserveAspect = false;
            SpriteImage(world, "Bridge", "terrain_bridge", 361.5f, 570.5f, 47, 73);
            SpriteImage(world, "Base", "base", 237.5f, 442.5f, 55, 55);
            var lockedColor = C("7D7466"); lockedColor.a = .55f;
            Img(world, "LockedForest", 417, 652, 363, 468, lockedColor);
            SpriteImage(world, "LockIcon", "lock", 487, 720, 28, 28);
            Panel(world, "ForestLabelBackground", 445, 752, 112, 23, Ink, Ink, 0, 6);
            Text(world, "ForestLabel", "R2で探索範囲が拡大", 445, 752, 112, 23, 11, heavy, Color.white);
            Panel(world, "BaseLabelBackground", 239, 502, 38, 22, Cream, Ink, 2, 6);
            Text(world, "BaseLabel", "拠点", 239, 502, 38, 22, 11, heavy, Ink);
            Mate(world, "Taro", "mate_taro", "たろう 採集中", 461, 480);
            Mate(world, "Hanako", "mate_hanako", "はなこ 採集中", 277, 330);
            ui.materialSprites = new[] { Sprite("tree"), Sprite("taro"), Sprite("negi"), Sprite("mushroom") };
            ui.stumpSprite = Sprite("stump");
            int[] types = { 0,0,0,0,3,3,1,1,1,2,2,0,1,2 };
            Vector2[] positions = { new(236,30),new(322,70),new(262,150),new(334,180),new(300,110),new(214,96),new(36,56),new(110,100),new(30,246),new(126,24),new(160,190),new(110,384),new(40,440),new(140,470) };
            ui.resources = new GameScreenController.ResourceView[positions.Length];
            for (int i = 0; i < positions.Length; i++)
            {
                var p = positions[i] + new Vector2(195, 280);
                var button = BoxButton(world, "Resource" + i, Primary, "", p.x, p.y, 48, 48, 3, 12, Color.clear, Color.clear, 12, heavy, Ink);
                var icon = Img(button.transform, "ResourceIcon", 5, 4, 38, 40, Color.white);
                icon.sprite = ui.materialSprites[types[i]];
                var bar = Panel(button.transform, "HarvestProgress", -2, -12, 52, 8, Cream, Ink, 2, 4);
                var fill = Filled(bar.transform, "Progress", 2, 2, 48, 4, Rust, (RectTransform)bar.transform.Find("Fill"));
                fill.fillAmount = 0;
                bar.gameObject.SetActive(false);
                var growth = Panel(button.transform, "GrowthChip", -9, 46, 66, 16, Color.white, Ink, 1.5f, 4);
                var grow = Text(growth.transform, "GrowthLabel", "", 0, 0, 66, 16, 9, heavy, Ink);
                growth.gameObject.SetActive(false);
                ui.resources[i] = new GameScreenController.ResourceView { button = button, material = types[i], icon = icon, selection = button.GetComponent<Image>(), selectionFill = button.transform.Find("Fill").GetComponent<Image>(), progressRoot = bar.gameObject, progressFill = fill, growthRoot = growth.gameObject, growthLabel = grow };
                UnityEventTools.AddIntPersistentListener(button.onClick, ui.SelectResource, i);
                button.navigation = new Navigation { mode = Navigation.Mode.None };
                foreach (var graphic in button.GetComponentsInChildren<Graphic>(true))
                    graphic.raycastTarget = false;
            }
            ui.destinationMarker = Rect(world, "DestinationMarker", 0, 0, 20, 20);
            for (int i = 0; i < 24; i++)
            {
                float angle = i * Mathf.PI * 2f / 24f;
                var segment = Img(ui.destinationMarker, "Ring" + i, 0, 0, 2.8f, 2f, C("5F8B3D")).rectTransform;
                segment.anchorMin = segment.anchorMax = segment.pivot = new Vector2(.5f, .5f);
                segment.anchoredPosition = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * 8.5f;
                segment.localRotation = Quaternion.Euler(0, 0, angle * Mathf.Rad2Deg + 90f);
            }
            ui.destinationMarker.gameObject.SetActive(false);
            ui.player = Rect(world, "Player", 253, 476, 40, 58);
            BuildPlayerVisuals(ui.player);
            Panel(ui.player, "NameBackground", 0, 43, 40, 15, Green, Green, 0, 5);
            Text(ui.player, "Name", "あなた", 0, 43, 40, 15, 10, heavy, Cream);
            var pop = Panel(world, "HarvestFeedback", 213, 446, 210, 28, Color.white, Ink, 2, 10);
            ui.popRoot = pop.gameObject; ui.popRect = pop.rectTransform;
            ui.popBackground = pop.transform.Find("Fill").GetComponent<Image>();
            ui.popText = Text(pop.transform, "Text", "", 8, 0, 194, 28, 13, heavy, Ink);
            var inputSurface = Img(map, "MapInput", 0, 0, 390, 560, Color.clear);
            inputSurface.raycastTarget = true;
            var mapInput = inputSurface.gameObject.AddComponent<GameMapInput>();
            mapInput.controller = ui;
            mapInput.world = world;
            var streak = Panel(map, "Streak", 10, 10, 149, 36, Ink, Ink, 0, 18);
            Text(streak.transform, "Caption", "最高品質 連続", 12, 0, 70, 36, 11, body, Cream);
            ui.streakText = Text(streak.transform, "Count", "0", 86, 0, 20, 36, 18, heading, Gold);
            ui.streakMultiplierText = Text(streak.transform, "Multiplier", "×1.00", 108, 0, 34, 36, 11, heavy, Cream);
            var toast = Panel(map, "ActivityToast", 12, 510, 366, 38, Cream, Ink, 2, 12);
            Img(toast.transform, "Dot", 14, 15, 8, 8, C("6B9B3A"), 4);
            ui.toastRoot = toast.gameObject;
            ui.toastText = Text(toast.transform, "Text", "ラウンド1開始!必須素材が公開されたよ", 30, 0, 323, 38, 13, heavy, Ink, TextAlignmentOptions.MidlineLeft);
            ui.toastText.overflowMode = TextOverflowModes.Ellipsis;
            var mapCamera = map.gameObject.AddComponent<GameMapCamera>();
            mapCamera.viewport = map;
            mapCamera.world = world;
            mapCamera.player = ui.player;
            mapCamera.SnapToPlayer();
        }

        static void BuildPlayerVisuals(RectTransform player)
        {
            var shadow = Img(player, "GroundShadow", 20, 41, 30, 8, new Color(Ink.r, Ink.g, Ink.b, .18f), 4).rectTransform;
            shadow.pivot = new Vector2(.5f, .5f);
            var visuals = Rect(player, "Visuals", 20, 42, 0, 0);
            var body = SpriteImage(visuals, "Character", "player", 0, 0, 36, 42).rectTransform;
            body.pivot = new Vector2(.5f, 0);
            var left = Hand(visuals, "LeftHand", -18, 13);
            var right = Hand(visuals, "RightHand", 18, 13);
            var motion = player.gameObject.AddComponent<GameCharacterMotion>();
            motion.controller = ui;
            motion.body = body;
            motion.leftHand = left;
            motion.rightHand = right;
            motion.groundShadow = shadow;
        }

        static RectTransform Hand(Transform parent, string name, float x, float y)
        {
            var hand = Panel(parent, name, x, -y, 10, 10, Cream, Ink, 1.6f, 5).rectTransform;
            hand.pivot = new Vector2(.5f, .5f);
            Img(hand, "Highlight", 2.3f, 2.1f, 2.4f, 2.4f, new Color(1f, 1f, 1f, .65f), 1.2f);
            return hand;
        }

        static void Mate(Transform map, string name, string sprite, string caption, float x, float y)
        {
            var mate = Rect(map, name, x, y, 72, 51);
            SpriteImage(mate, "Character", sprite, 21, 0, 30, 34);
            Panel(mate, "NameBackground", 0, 35, 72, 16, Color.white, Ink, 1.5f, 5);
            Text(mate, "Name", caption, 0, 35, 72, 16, 10, heavy, Ink);
        }

        static void BuildNavigation(Transform root)
        {
            var nav = Rect(root, "GameNavigation", 0, 760, 390, 84);
            Img(nav, "Background", 0, 0, 390, 84, Cream);
            Img(nav, "Border", 0, 0, 390, 3, Ink);
            string[] captions = { "倉庫", "ショップ", "強化", "メニュー" }, icons = { "nav_warehouse", "nav_shop", "nav_upgrade", "nav_menu" };
            UnityAction[] actions = { ui.OpenWarehouse, ui.OpenShop, ui.OpenUpgrade, ui.OpenMenu };
            int[] panels = { 1, 4, 2, 3 };
            ui.navigation = new GameScreenController.NavigationView[4];
            for (int i = 0; i < 4; i++)
            {
                var button = BoxButton(nav, captions[i] + "Button", YellowButton, captions[i], 10 + i * 94.5f, 11, 86.5f, 59, 3, 14, Gold, Ink, 12, heavy, Ink);
                SetRect(Label(button).rectTransform, 3, 33, 80.5f, 21);
                var icon = SpriteImage(button.transform, "Icon", icons[i], 32.25f, 9, 22, 22);
                icon.color = Ink;
                ui.navigation[i] = new GameScreenController.NavigationView { background = button.transform.Find("Fill").GetComponent<Image>(), icon = icon, label = Label(button), panel = panels[i] };
                UnityEventTools.AddPersistentListener(button.onClick, actions[i]);
            }
        }

        static void BuildWarehouse(Transform root)
        {
            var sheet = Sheet(root, "Warehouse", 620);
            ui.warehousePanel = sheet.gameObject;
            var content = Rect(sheet, "Content", 14, 18, 362, 584);
            var layout = content.gameObject.AddComponent<VerticalLayoutGroup>();
            layout.spacing = 10; layout.childControlHeight = true; layout.childControlWidth = true;
            layout.childForceExpandHeight = false; layout.childForceExpandWidth = true;
            var top = Rect(content, "Header", 0, 0, 362, 44); Fixed(top, 44);
            Text(top, "Title", "共有倉庫", 0, 0, 180, 44, 22, heading, Green, TextAlignmentOptions.MidlineLeft);
            Text(top, "FundsCaption", "資金", 205, 0, 30, 44, 13, heavy, Ink, TextAlignmentOptions.MidlineRight);
            AddFunds(Text(top, "FundsValue", "1,000G", 240, 0, 72, 44, 13, heavy, Ink, TextAlignmentOptions.MidlineRight));
            CloseButton(top, 318, 0);
            var columns = Rect(content, "Columns", 0, 0, 362, 15); Fixed(columns, 15);
            string[] columnLabels = { "素材", "所持", "保護", "売却可", "単価" };
            float[] columnX = { 8,106,169,232,295 }, widths = { 94,59,59,59,59 };
            for (int i = 0; i < 5; i++) Text(columns, "Column" + i, columnLabels[i], columnX[i], 0, widths[i], 15, 10, heavy, C("6B4A3A"), i == 0 ? TextAlignmentOptions.MidlineLeft : TextAlignmentOptions.MidlineRight);
            var scroll = Rect(content, "InventoryScroll", 0, 0, 362, 199);
            var flex = scroll.gameObject.AddComponent<LayoutElement>(); flex.flexibleHeight = 1; flex.minHeight = 40;
            var scrollImage = scroll.gameObject.AddComponent<Image>(); scrollImage.color = Color.clear;
            var scroller = scroll.gameObject.AddComponent<ScrollRect>(); scroller.horizontal = false; scroller.movementType = ScrollRect.MovementType.Clamped;
            var viewport = Rect(scroll, "Viewport", 0, 0, 362, 199); Stretch(viewport); viewport.gameObject.AddComponent<RectMask2D>();
            var rows = Rect(viewport, "Rows", 0, 0, 362, 0);
            rows.anchorMin = new Vector2(0, 1); rows.anchorMax = Vector2.one; rows.pivot = new Vector2(.5f, 1); rows.anchoredPosition = Vector2.zero; rows.sizeDelta = Vector2.zero;
            var rowLayout = rows.gameObject.AddComponent<VerticalLayoutGroup>(); rowLayout.spacing = 4; rowLayout.childControlWidth = true; rowLayout.childControlHeight = true; rowLayout.childForceExpandHeight = false;
            rows.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            scroller.viewport = viewport; scroller.content = rows;
            var empty = Rect(rows, "EmptyInventory", 0, 0, 362, 80); Fixed(empty, 80);
            var emptyLabel = Text(empty, "Message", "マップをタップして素材の近くへ移動しよう", 0, 20, 362, 20, 14, body, Ink);
            ui.emptyInventoryRoot = empty.gameObject;
            ui.inventoryRows = new GameScreenController.InventoryRowView[12];
            Color[] qualityColors = { Color.white, C("DCE8C4"), C("C8DCEB") };
            Color[] qualityText = { Ink, Green, C("1F3E5C") };
            for (int i = 0; i < 12; i++)
            {
                int m = i / 3, q = i % 3;
                var button = BoxButton(rows, "Inventory" + i, Primary, "", 0, 0, 362, 44, 2, 10, Color.white, C("E9D5AE"), 13, heavy, Ink); Fixed(button.transform, 44);
                var name = Text(button.transform, "Material", Materials[m], 10, 0, m == 3 ? 39 : (m == 0 ? 14 : 26), 44, 13, heavy, Ink, TextAlignmentOptions.MidlineLeft);
                float badgeX = m == 0 ? 28 : m == 3 ? 53 : 40;
                var badge = Panel(button.transform, "QualityBadge", badgeX, 14, 30, 17, qualityColors[q], Ink, 1, 5);
                var quality = Text(badge.transform, "Text", Qualities[q], 0, 0, 30, 17, 10, heavy, qualityText[q]);
                TMP_Text Number(string n, float x, Color color) => Text(button.transform, n, "0", x, 0, 59, 44, 13, heavy, color, TextAlignmentOptions.MidlineRight);
                ui.inventoryRows[i] = new GameScreenController.InventoryRowView { button = button, border = button.GetComponent<Image>(), background = button.transform.Find("Fill").GetComponent<Image>(), qualityBackground = badge.transform.Find("Fill").GetComponent<Image>(), name = name, quality = quality, quantity = Number("Quantity",106,Ink), protectedCount = Number("Protected",169,Blue), sellable = Number("Sellable",232,Ink), price = Number("Price",293,Ink) };
                UnityEventTools.AddIntPersistentListener(button.onClick, ui.SelectInventory, i);
                button.gameObject.SetActive(false);
            }
            var selection = Panel(content, "SelectedMaterial", 0, 0, 362, 162, Color.white, Ink, 3, 14); Fixed(selection.transform, 162);
            ui.selectionPanel = selection.gameObject;
            ui.selectedNameText = Text(selection.transform, "Name", "薪(普通)", 13, 13, 130, 36, 14, heavy, Ink, TextAlignmentOptions.MidlineLeft);
            Text(selection.transform, "ProtectCaption", "保護", 149, 13, 26, 36, 11, heavy, Blue);
            var less = BoxButton(selection.transform, "ProtectLess", Primary, "−", 177, 13, 36, 36, 2, 8, Color.white, Blue, 16, heavy, Blue);
            UnityEventTools.AddPersistentListener(less.onClick, ui.ProtectLess);
            ui.selectedProtectedText = Text(selection.transform, "ProtectedValue", "0", 217, 13, 28, 36, 14, heavy, Blue);
            var more = BoxButton(selection.transform, "ProtectMore", Primary, "＋", 249, 13, 36, 36, 2, 8, Color.white, Blue, 16, heavy, Blue);
            UnityEventTools.AddPersistentListener(more.onClick, ui.ProtectMore);
            var all = BoxButton(selection.transform, "ToggleProtection", Primary, "全部保護", 289, 13, 60, 36, 0, 8, Blue, Blue, 11, heavy, Color.white);
            ui.toggleProtectionText = Label(all); UnityEventTools.AddPersistentListener(all.onClick, ui.ToggleProtection);
            for (int i = 0; i < 4; i++)
            {
                var percent = BoxButton(selection.transform, "Percent" + i, Primary, (i + 1) * 25 + "%", 13 + i * 68, 57, 62, 36, 2, 8, Cream, Ink, 12, heavy, Ink);
                UnityEventTools.AddIntPersistentListener(percent.onClick, ui.SetSalePercent, (i + 1) * 25);
            }
            ui.sellQuantityInput = NumberInput(selection.transform, 285, 57, 64, 36);
            UnityEventTools.AddPersistentListener(ui.sellQuantityInput.onValueChanged, ui.SetSaleQuantity);
            var sell = SharedButton(selection.transform, "SellSelected", Red, "0個 売却 +0", 13, 101, 336, 48, 16, heavy, Cream);
            FlatPill(sell, Rust);
            ui.sellSelectedButton = sell; ui.sellSelectedText = Label(sell);
            UnityEventTools.AddPersistentListener(sell.onClick, ui.SellSelected);
            var sellAll = BoxButton(content, "SellUnprotected", Red, "保護以外をまとめて売却", 0, 0, 362, 44, 3, 22, Cream, Rust, 14, heavy, Rust); Fixed(sellAll.transform, 44);
            ui.sellAllButton = sellAll; UnityEventTools.AddPersistentListener(sellAll.onClick, ui.SellAll);
            var history = Rect(content, "History", 0, 0, 362, 70);
            var historyLayout = history.gameObject.AddComponent<VerticalLayoutGroup>(); historyLayout.spacing = 2; historyLayout.childControlHeight = true; historyLayout.childControlWidth = true; historyLayout.childForceExpandHeight = false;
            var historyCaption = Text(history, "Caption", "操作履歴", 0, 0, 362, 16, 11, heavy, C("6B4A3A"), TextAlignmentOptions.MidlineLeft); Fixed(historyCaption.transform, 16);
            ui.historyText = Text(history, "Entries", "・[0:00] ラウンド1開始!必須素材が公開されたよ", 0, 18, 362, 52, 11, body, Ink, TextAlignmentOptions.TopLeft);
            ui.historyText.overflowMode = TextOverflowModes.Ellipsis;
            ui.historyText.textWrappingMode = TextWrappingModes.NoWrap;
            ui.historyText.lineSpacing = 2;
        }

        static void BuildShop(Transform root)
        {
            var sheet = Sheet(root, "Shop", 540);
            ui.shopPanel = sheet.gameObject;
            Text(sheet, "Title", "ショップ", 14, 18, 180, 44, 22, heading, Green, TextAlignmentOptions.MidlineLeft);
            Text(sheet, "FundsCaption", "共有資金", 205, 18, 58, 44, 12, heavy, Ink);
            AddFunds(Text(sheet, "FundsValue", "1,000G", 264, 18, 62, 44, 13, heavy, Ink, TextAlignmentOptions.MidlineRight));
            CloseButton(sheet, 332, 18);
            Text(sheet, "Help", "素材を購入して共有倉庫へ。購入品は普通品質。売却は倉庫から", 14, 72, 362, 17, 12, body, Ink, TextAlignmentOptions.MidlineLeft);
            ui.shopItems = new GameScreenController.ShopItemView[4];
            int[] prices = { 6, 30, 18, 90 };
            for (int i = 0; i < 4; i++)
            {
                var card = Panel(sheet, "ShopItem" + i, 14, 99 + i * 80, 362, 70, Color.white, Ink, 3, 16);
                var name = Text(card.transform, "Material", Materials[i], 15, 14, 120, 23, 16, heavy, Ink, TextAlignmentOptions.MidlineLeft);
                var unit = Text(card.transform, "UnitPrice", prices[i] + "G / 個", 15, 39, 120, 17, 11, body, C("6B4A3A"), TextAlignmentOptions.MidlineLeft);
                var ten = BoxButton(card.transform, "BuyTen", Primary, "×10  " + (prices[i] * 10).ToString("N0") + "G", 163, 13, 88, 44, 0, 12, Green, Green, 12, heavy, Cream);
                var fifty = BoxButton(card.transform, "BuyFifty", Primary, "×50  " + (prices[i] * 50).ToString("N0") + "G", 259, 13, 88, 44, 0, 12, Green, Green, 12, heavy, Cream);
                UnityEventTools.AddIntPersistentListener(ten.onClick, ui.PurchaseShopItem, i * 2);
                UnityEventTools.AddIntPersistentListener(fifty.onClick, ui.PurchaseShopItem, i * 2 + 1);
                ui.shopItems[i] = new GameScreenController.ShopItemView {
                    name = name, unitPrice = unit, buyTenButton = ten, buyFiftyButton = fifty,
                    buyTenLabel = Label(ten), buyFiftyLabel = Label(fifty),
                    buyTenBackground = ten.GetComponent<Image>(), buyFiftyBackground = fifty.GetComponent<Image>()
                };
            }
            Text(sheet, "PricingNote", "購入価格は売却単価の3倍", 14, 421, 362, 20, 11, body, C("6B4A3A"), TextAlignmentOptions.MidlineLeft);
        }

        static void BuildUpgrade(Transform root)
        {
            var sheet = Sheet(root, "Upgrades", 560); ui.upgradePanel = sheet.gameObject;
            Text(sheet, "Title", "個人強化", 14, 18, 180, 44, 22, heading, Green, TextAlignmentOptions.MidlineLeft);
            Text(sheet, "FundsCaption", "共有資金", 205, 18, 58, 44, 12, heavy, Ink);
            AddFunds(Text(sheet, "FundsValue", "1,000G", 264, 18, 62, 44, 13, heavy, Ink, TextAlignmentOptions.MidlineRight));
            CloseButton(sheet, 332, 18);
            Text(sheet, "Help", "強化は自分だけに効果。資金はチーム共通だから相談して使おう", 14, 72, 362, 17, 12, body, Ink, TextAlignmentOptions.MidlineLeft);
            string[] names = { "移動速度", "採集速度", "採集量", "採集運" };
            string[] effects = { "移動 ×1.00", "採集 1.60秒", "獲得量 ×1.0", "最高品質 8.0%" };
            string[] next = { "移動 ×1.25", "採集 1.28秒", "獲得量 ×1.5", "最高品質 12.8%" };
            ui.upgrades = new GameScreenController.UpgradeView[4];
            for (int i = 0; i < 4; i++)
            {
                var card = Panel(sheet, "Upgrade" + i, 14, 99 + i * 88, 362, 78, Color.white, Ink, 3, 16);
                Text(card.transform, "Name", names[i], 15, 12, i < 2 ? 64 : 48, 24, 16, heavy, Ink, TextAlignmentOptions.MidlineLeft);
                var level = Text(card.transform, "Level", "Lv.0", i < 2 ? 87 : 71, 12, 60, 24, 15, heading, Rust, TextAlignmentOptions.MidlineLeft);
                var current = Text(card.transform, "CurrentEffect", effects[i], 15, 44, 88, 20, 12, body, C("6B4A3A"), TextAlignmentOptions.MidlineLeft);
                var after = Text(card.transform, "NextEffect", "→ " + next[i], 103, 44, 138, 20, 12, heavy, Green, TextAlignmentOptions.MidlineLeft);
                var buy = BoxButton(card.transform, "BuyUpgrade", Primary, "強化", 251, 13, 96, 52, 0, 12, Green, Green, 13, heavy, Cream);
                SetRect(Label(buy).rectTransform, 0, 7, 96, 18);
                var price = Text(buy.transform, "Price", "150G", 0, 25, 96, 20, 13, heavy, Cream);
                ui.upgrades[i] = new GameScreenController.UpgradeView { button = buy, background = buy.GetComponent<Image>(), level = level, currentEffect = current, nextEffect = after, price = price };
                UnityEventTools.AddIntPersistentListener(buy.onClick, ui.BuyUpgrade, i);
            }
        }

        static void BuildMenu(Transform root)
        {
            var sheet = Sheet(root, "TeamMenu", 440); ui.menuPanel = sheet.gameObject;
            Text(sheet, "Title", "チーム", 14, 18, 280, 44, 22, heading, Green, TextAlignmentOptions.MidlineLeft);
            CloseButton(sheet, 332, 18);
            string[] names = { "ゆくらさん(あなた)", "たろう", "はなこ" }, activities = { "待機中", "採集中・薪", "採集中・里芋" };
            ui.members = new GameScreenController.MemberView[3];
            for (int i = 0; i < 3; i++)
            {
                var row = Panel(sheet, "Member" + i, 14, 72 + i * 50, 362, 40, Color.white, Ink, 2, 12);
                var dot = Img(row.transform, "Online", 12, 15, 10, 10, C("6B9B3A"), 5);
                var name = Text(row.transform, "Name", names[i], 32, 0, 192, 40, 14, heavy, Ink, TextAlignmentOptions.MidlineLeft);
                var activity = Text(row.transform, "Activity", activities[i], 222, 0, 128, 40, 11, body, C("6B4A3A"), TextAlignmentOptions.MidlineRight);
                var avatar = i == 0 ? null : ui.world.Find(i == 1 ? "Taro" : "Hanako");
                CanvasGroup group = avatar ? avatar.GetComponent<CanvasGroup>() : null;
                if (avatar && !group) group = avatar.gameObject.AddComponent<CanvasGroup>();
                ui.members[i] = new GameScreenController.MemberView { name = name, activity = activity, statusDot = dot, worldAvatar = group,
                    worldLabel = avatar ? avatar.GetComponentInChildren<TMP_Text>() : null, displayName = names[i], connectedActivity = activities[i], connected = true };
                if (i == 0) ui.ownActivityText = activity;
            }
            var back = BoxButton(sheet, "ReturnHome", Primary, "ホームへ戻る(挑戦は中断)", 14, 368, 362, 54, 3, 27, Cream, Ink, 15, heavy, Ink);
            Navigate(back, "Home");
        }

        static void BuildEnd(Transform root)
        {
            var overlay = Img(root, "RoundFinished", 0, 0, 390, 844, new Color(Ink.r, Ink.g, Ink.b, .72f)); overlay.raycastTarget = true;
            ui.endedPanel = overlay.gameObject;
            var card = Panel(overlay.transform, "Summary", 24, 268, 342, 308, Gold, Ink, 4, 26);
            Text(card.transform, "TimeUp", "5:00 タイムアップ", 24, 24, 294, 24, 14, heavy, Ink);
            ui.endTitleText = Text(card.transform, "Title", "芋煮会の時間だ!", 24, 59, 294, 52, 28, heading, Rust);
            ui.endSummaryText = Text(card.transform, "SummaryText", "", 24, 123, 294, 78, 14, body, Ink);
            ui.endSummaryText.textWrappingMode = TextWrappingModes.Normal;
            var result = SharedButton(card.transform, "ShowRoundResult", Primary, "結果を見る", 24, 224, 294, 56, 18, heavy, Cream);
            FlatPill(result, Green);
            UnityEventTools.AddPersistentListener(result.onClick, ui.ShowRoundResult);
        }

        static RectTransform Sheet(Transform root, string name, float height)
        {
            var sheet = Rect(root, name, 0, 844 - height, 390, height);
            var shadow = new Color(Ink.r, Ink.g, Ink.b, .18f);
            Img(sheet, "TopShadow", 0, -8, 390, height + 8, shadow, 26);
            Img(sheet, "Border", 0, 0, 390, height + 26, Ink, 26);
            var fill = Img(sheet, "Surface", 0, 4, 390, height + 22, Cream, 26); fill.raycastTarget = true;
            Img(sheet, "SquareBottom", 0, 40, 390, height - 40, Cream).raycastTarget = true;
            return sheet;
        }

        static void CloseButton(Transform parent, float x, float y)
        {
            var close = SharedButton(parent, "ClosePanel", Primary, "", x, y, 44, 44, 12, heavy, Ink);
            close.GetComponent<Image>().color = Color.clear;
            SpriteImage(close.transform, "CloseIcon", "close", 11, 11, 22, 22);
            UnityEventTools.AddPersistentListener(close.onClick, ui.ClosePanel);
        }

        static TMP_InputField NumberInput(Transform parent, float x, float y, float w, float h)
        {
            var frame = Panel(parent, "SaleQuantity", x, y, w, h, Color.white, Ink, 2, 8); frame.raycastTarget = true;
            var input = frame.gameObject.AddComponent<TMP_InputField>();
            var viewport = Rect(frame.transform, "TextArea", 6, 2, w - 12, h - 4); viewport.gameObject.AddComponent<RectMask2D>();
            var label = Text(viewport, "Text", "0", 0, 0, w - 12, h - 4, 14, heavy, Ink, TextAlignmentOptions.MidlineRight);
            input.textViewport = viewport; input.textComponent = label; input.targetGraphic = frame;
            input.contentType = TMP_InputField.ContentType.IntegerNumber; input.characterLimit = 7; input.text = "0";
            return input;
        }

        static Button SharedButton(Transform parent, string name, string source, string caption, float x, float y, float w, float h, float size, TMP_FontAsset font, Color color)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(source);
            if (!prefab) throw new InvalidOperationException("Missing shared button: " + source);
            var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent); instance.name = name;
            SetRect((RectTransform)instance.transform, x, y, w, h);
            var button = instance.GetComponent<Button>(); button.onClick = new Button.ButtonClickedEvent();
            var label = Label(button); SetRect(label.rectTransform, 0, 0, w, h);
            label.text = caption; label.font = font; label.fontSharedMaterial = font.material; label.fontSize = size; label.color = color;
            label.fontStyle = FontStyles.Normal; label.alignment = TextAlignmentOptions.Midline; label.enableAutoSizing = false;
            label.textWrappingMode = TextWrappingModes.NoWrap; label.raycastTarget = false;
            var colors = button.colors; colors.disabledColor = Color.white; button.colors = colors;
            return button;
        }

        static Button BoxButton(Transform parent, string name, string source, string caption, float x, float y, float w, float h, float stroke, float radius, Color fill, Color border, float size, TMP_FontAsset font, Color color)
        {
            var button = SharedButton(parent, name, source, caption, x, y, w, h, size, font, color);
            var image = button.GetComponent<Image>(); image.sprite = rounded; image.type = Image.Type.Sliced; image.pixelsPerUnitMultiplier = 32 / radius; image.color = stroke > 0 ? border : fill;
            if (stroke > 0) { var inside = Img(button.transform, "Fill", stroke, stroke, w - stroke * 2, h - stroke * 2, fill, Mathf.Max(1, radius - stroke)); inside.transform.SetAsFirstSibling(); }
            return button;
        }
        static void FlatPill(Button button, Color color)
        {
            var image = button.GetComponent<Image>(); image.sprite = rounded; image.type = Image.Type.Sliced;
            image.pixelsPerUnitMultiplier = 64 / ((RectTransform)button.transform).rect.height; image.color = color;
        }
        static void Navigate(Button button, string scene)
        {
            var navigation = button.gameObject.AddComponent<SceneNavigationButton>();
            var serialized = new SerializedObject(navigation); serialized.FindProperty("targetSceneName").stringValue = scene; serialized.ApplyModifiedPropertiesWithoutUndo();
            UnityEventTools.AddPersistentListener(button.onClick, navigation.Navigate);
        }
        static TextMeshProUGUI Label(Button button) => button.GetComponentInChildren<TextMeshProUGUI>(true);
        static void AddFunds(TMP_Text label) => ui.fundsTexts = ui.fundsTexts.Concat(new[] { label }).ToArray();
        static void Fixed(Transform node, float height) { var layout = node.gameObject.AddComponent<LayoutElement>(); layout.minHeight = layout.preferredHeight = height; layout.flexibleHeight = 0; }
        static RectTransform Rect(Transform parent, string name, float x, float y, float w, float h)
        {
            var rect = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>(); rect.gameObject.layer = 5; rect.SetParent(parent, false); SetRect(rect, x, y, w, h); return rect;
        }
        static void SetRect(RectTransform rect, float x, float y, float w, float h)
        {
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0, 1); rect.anchoredPosition = new Vector2(x, -y); rect.sizeDelta = new Vector2(w, h); rect.localScale = Vector3.one; rect.localRotation = Quaternion.identity;
        }
        static void Stretch(RectTransform rect) { rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one; rect.offsetMin = rect.offsetMax = Vector2.zero; }
        static Image Img(Transform parent, string name, float x, float y, float w, float h, Color color, float radius = 0)
        {
            var image = Rect(parent, name, x, y, w, h).gameObject.AddComponent<Image>(); image.color = color; image.raycastTarget = false;
            if (radius > 0) { image.sprite = rounded; image.type = Image.Type.Sliced; image.pixelsPerUnitMultiplier = 32 / radius; } return image;
        }
        static Image Panel(Transform parent, string name, float x, float y, float w, float h, Color fill, Color border, float stroke, float radius)
        {
            var panel = Img(parent, name, x, y, w, h, stroke > 0 ? border : fill, radius);
            if (stroke > 0) Img(panel.transform, "Fill", stroke, stroke, w - stroke * 2, h - stroke * 2, fill, Mathf.Max(1, radius - stroke)); return panel;
        }
        static Image Filled(Transform parent, string name, float x, float y, float w, float h, Color color, RectTransform track)
        {
            var image = Img(parent, name, x, y, w, h, color);
            image.sprite = rounded;
            image.fillAmount = 0;
            image.gameObject.AddComponent<RoundedProgressFill>().Configure(track);
            return image;
        }
        static Image SpriteImage(Transform parent, string name, string asset, float x, float y, float w, float h)
        {
            var image = Img(parent, name, x, y, w, h, Color.white); image.sprite = Sprite(asset); image.preserveAspect = true; return image;
        }
        static Sprite Sprite(string name)
        {
            var sprite = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/ImoniUI/Game/game_" + name + ".png");
            if (!sprite) throw new InvalidOperationException("Missing Game sprite: " + name); return sprite;
        }
        static TextMeshProUGUI Text(Transform parent, string name, string value, float x, float y, float w, float h, float size, TMP_FontAsset font, Color color, TextAlignmentOptions alignment = TextAlignmentOptions.Midline)
        {
            var text = Rect(parent, name, x, y, w, h).gameObject.AddComponent<TextMeshProUGUI>();
            text.text = value; text.font = font; text.fontSize = size; text.fontSharedMaterial = font.material; text.color = color; text.alignment = alignment;
            text.raycastTarget = false; text.textWrappingMode = TextWrappingModes.NoWrap; return text;
        }
        static Color C(string hex) { ColorUtility.TryParseHtmlString("#" + hex, out var color); return color; }
        static void ImportSprites()
        {
            foreach (var file in Directory.GetFiles("Assets/ImoniUI/Game", "*.png"))
            {
                var path = file.Replace('\\', '/'); AssetDatabase.ImportAsset(path);
                var importer = (TextureImporter)AssetImporter.GetAtPath(path); importer.textureType = TextureImporterType.Sprite;
                importer.spriteImportMode = SpriteImportMode.Single; importer.spritePixelsPerUnit = 300;
                importer.mipmapEnabled = false; importer.alphaIsTransparency = true; importer.textureCompression = TextureImporterCompression.Uncompressed;
                importer.npotScale = TextureImporterNPOTScale.None; importer.filterMode = FilterMode.Bilinear; importer.wrapMode = TextureWrapMode.Clamp; importer.SaveAndReimport();
            }
        }
    }
}
