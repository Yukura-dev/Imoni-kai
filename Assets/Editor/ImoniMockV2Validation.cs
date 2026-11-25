using System;
using System.Linq;
using System.Reflection;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace Imoni.UI.Editor
{
    /// <summary>Checks saved V2 prefabs in isolation. No test state is saved or scene navigation invoked.</summary>
    public static class ImoniMockV2Validation
    {
        private const BindingFlags InstanceFields = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
        private static readonly string[] SharedButtons =
        {
            "Assets/Prefabs/UI/PrimaryButton.prefab",
            "Assets/Prefabs/UI/RedButton.prefab",
            "Assets/Prefabs/UI/YellowButton.prefab"
        };

        [MenuItem("Imoni/Mock V2/Validate Saved Screens")]
        public static void Validate()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("Stop Play Mode before validating saved prefabs.");
            using var sessionState = ImoniRoundFlowValidation.PreserveSessionState();
            ImoniRoundSession.BeginNewChallenge();
            var random = UnityEngine.Random.state;
            try
            {
                WithPrefab("GameScreen", ValidateGame);
                WithPrefab("LobbyScreen", ValidateLobby);
                WithPrefab("HomeScreen", ValidateHome);
                WithPrefab("WorldLoadingScreen", ValidateLoading);
                Debug.Log("IMONI_MOCK_V2_VALIDATED: Four saved screen prefabs; shared button wiring; eight shop offers and insufficient funds; protected sales; level-zero upgrades; bonus tiers; panel and timeout guards; tap movement, camera and hand contact; 17 rounded bars; lobby join errors, invitation waiting and ready states; Home labels and loading references passed. Test state was not saved.");
            }
            finally { UnityEngine.Random.state = random; }
        }

        private static void WithPrefab(string name, Action<GameObject> validate)
        {
            string path = "Assets/Prefabs/UI/" + name + ".prefab";
            Require(AssetDatabase.LoadAssetAtPath<GameObject>(path), "Missing saved prefab: " + path);
            var root = PrefabUtility.LoadPrefabContents(path);
            try
            {
                Require(root.GetComponentsInChildren<Component>(true).All(component => component), name + " has a missing script.");
                Require(root.GetComponent<HomeScreenFit>(), name + " has no safe-area fit component.");
                foreach (var text in root.GetComponentsInChildren<TMP_Text>(true))
                    Require(text.font, name + "/" + text.name + " has no font.");
                validate(root);
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
        }

        private static void ValidateGame(GameObject root)
        {
            var c = root.GetComponent<GameScreenController>();
            Require(c, "Game controller is missing.");
            Require(c.requirements?.Length == 3 && c.resources?.Length == 14 && c.inventoryRows?.Length == 12, "Game requirement/resource/inventory rows are incomplete.");
            Require(c.upgrades?.Length == 4 && c.shopItems?.Length == 4 && c.navigation?.Length == 4, "Game upgrades/shop/navigation are incomplete.");
            Require(c.materialSprites?.Length == 4 && c.materialSprites.All(sprite => sprite) && c.stumpSprite, "Game material artwork is missing.");
            Require(c.warehousePanel && c.upgradePanel && c.menuPanel && c.shopPanel && c.endedPanel, "Game panel reference is missing.");
            Require(c.timerText && c.bonusText && c.bonusProgressText && c.historyText && c.fundsTexts?.Length > 0 && c.fundsTexts.All(text => text), "Game HUD references are missing.");
            Require(c.sellQuantityInput && c.sellSelectedButton && c.sellAllButton && c.selectionPanel, "Warehouse references are missing.");
            var inventoryScroll = c.warehousePanel.GetComponentInChildren<ScrollRect>(true);
            Require(inventoryScroll && inventoryScroll.viewport && inventoryScroll.content, "The flexible warehouse list is missing its scroll viewport or content.");
            Require(c.sellQuantityInput.onValueChanged.GetPersistentEventCount() > 0, "Sale quantity has no saved input handler.");
            foreach (var view in c.requirements)
                Require(view != null && view.background && view.progress && view.label && view.count, "A required material view is incomplete.");
            foreach (var view in c.resources)
                Require(view != null && view.button && view.icon && view.selection && view.selectionFill && view.progressFill && view.progressRoot && view.growthRoot && view.growthLabel, "A harvest resource view is incomplete.");
            foreach (var view in c.inventoryRows)
                Require(view != null && view.button && view.border && view.background && view.qualityBackground && view.name && view.quality && view.quantity && view.protectedCount && view.sellable && view.price, "An inventory row is incomplete.");
            foreach (var view in c.upgrades)
                Require(view != null && view.button && view.background && view.level && view.currentEffect && view.nextEffect && view.price, "An upgrade view is incomplete.");
            for (int material = 0; material < c.shopItems.Length; material++)
            {
                var view = c.shopItems[material];
                Require(view != null && view.name && view.unitPrice && view.buyTenLabel && view.buyFiftyLabel && view.buyTenButton && view.buyFiftyButton && view.buyTenBackground && view.buyFiftyBackground, "A shop material row is incomplete.");
                RequireIntHandler(view.buyTenButton, c, "PurchaseShopItem", material * 2);
                RequireIntHandler(view.buyFiftyButton, c, "PurchaseShopItem", material * 2 + 1);
            }
            foreach (var button in root.GetComponentsInChildren<Button>(true)) ValidateSharedButton(button);
            Require(c.navigation[1].panel == 4 && c.navigation[1].label.text == "ショップ", "The second navigation action is not the shop.");

            Call(c, "Start");
            Require(Get<int>(c, "funds") == 1000 && Get<int[]>(c, "levels").All(level => level == 0), "A new round must start with 1000G and level-zero upgrades.");
            Require(Mathf.Approximately(c.MovementSpeed, 140f) && Mathf.Approximately((float)Call(c, "HarvestDuration"), 1.6f), "Level-zero movement or harvesting speed changed.");
            Require(c.fundsTexts.All(text => text.text == "1,000G"), "Starting funds are not formatted as 1,000G.");
            Require(!c.endedPanel.activeSelf && c.CanMoveOnMap, "A new round is not ready for map input.");

            ValidateRoundedBars(c);
            ValidateMovementAndMotion(c);
            ValidateShop(c);
            ValidateSalesAndUpgrades(c);
            ValidateBonusAndPanels(c);
            ValidateRoundEnd(c);
        }

        private static void ValidateRoundedBars(GameScreenController c)
        {
            var images = c.requirements.Select(view => view.progress).Concat(c.resources.Select(view => view.progressFill)).ToArray();
            Require(c.GetComponentsInChildren<RoundedProgressFill>(true).Length == 17, "Expected 17 rounded progress components.");
            foreach (var image in images)
            {
                var fill = image.GetComponent<RoundedProgressFill>();
                Require(fill, image.name + " has lost its rounded progress component.");
                var track = Get<RectTransform>(fill, "track");
                Require(track && track != image.rectTransform, "Rounded progress has an invalid track reference.");
                float original = image.fillAmount;
                foreach (float progress in new[] { 0f, .01f, .5f, 1f })
                {
                    image.fillAmount = progress;
                    fill.Refresh();
                    Require(image.type == Image.Type.Sliced, "A progress image reverted to stretched Filled rendering.");
                    Require(Mathf.Abs(image.rectTransform.rect.width - track.rect.width * progress) < .001f, "Progress width does not match its track.");
                    Require(image.enabled == (progress > 0), "Zero progress visibility is incorrect.");
                }
                image.fillAmount = original;
                fill.Refresh();
            }
        }

        private static void ValidateMovementAndMotion(GameScreenController c)
        {
            var input = c.GetComponentInChildren<GameMapInput>(true);
            var camera = c.GetComponentInChildren<GameMapCamera>(true);
            var motion = c.GetComponentInChildren<GameCharacterMotion>(true);
            Require(c.world && c.player && c.destinationMarker && c.popRect, "World/player/feedback references are missing.");
            Require(input && camera && motion && camera.viewport, "Map input, camera or character motion is missing.");
            Require(input.controller == c && input.world == c.world && camera.world == c.world && camera.player == c.player && motion.controller == c, "Map or motion references point to a different controller.");
            Require(motion.body && motion.leftHand && motion.rightHand && motion.groundShadow, "Character body, round hands or shadow are missing.");
            Require(camera.viewport.parent == c.transform && c.world.parent == camera.viewport && camera.viewport.GetComponent<RectMask2D>(), "The field no longer clips a scrolling world beneath a fixed HUD.");
            RequireNear(camera.viewport.rect.size, new Vector2(390, 560), "Field viewport size changed.");
            RequireNear(c.world.rect.size, new Vector2(780, 1120), "Harvest world size changed.");
            Require(c.player.parent == c.world && c.destinationMarker.parent == c.world && c.popRect.parent == c.world, "Navigation targets or harvest feedback left the world.");
            Require(input.GetComponent<Image>() && input.GetComponent<Image>().raycastTarget && input.transform.parent == camera.viewport, "Map input no longer receives viewport presses.");
            foreach (var resource in c.resources)
            {
                Require(resource.button.transform.parent == c.world, "A harvest resource left the scrolling world.");
                Require(resource.button.GetComponentsInChildren<Graphic>(true).All(graphic => !graphic.raycastTarget), "A resource intercepts free map taps.");
            }
            Call(motion, "OnEnable");
            var restBody = motion.body.anchoredPosition;
            var restLeft = motion.leftHand.anchoredPosition;
            var restRight = motion.rightHand.anchoredPosition;
            Vector2 empty = new Vector2(80, -140), feet = new Vector2(20, -42);
            var pointer = new PointerEventData(null) { button = PointerEventData.InputButton.Left };
            pointer.position = RectTransformUtility.WorldToScreenPoint(null, c.world.TransformPoint(empty));
            input.OnPointerDown(pointer);
            Require(c.IsMoving && !c.IsCollecting, "A ground press did not start movement.");
            RequireNear(Get<Vector2>(c, "moveDestination"), empty - feet, "A tap no longer targets the player's feet.");
            Vector2 playerBeforePose = c.player.anchoredPosition;
            Call(motion, "ApplyPose", .1f);
            Require((motion.body.anchoredPosition - restBody).sqrMagnitude > .01f, "Walking no longer bounces the character artwork.");
            RequireNear(c.player.anchoredPosition, playerBeforePose, "Character bounce moved the gameplay/camera target.");
            Call(c, "AdvanceMovement", 100f);
            RequireNear(c.player.anchoredPosition + feet, empty, "The character did not stop at the ground tap.");
            Require(!c.IsMoving && !c.IsCollecting, "Ground arrival unexpectedly started harvesting.");

            var resourceIcon = c.resources[0].icon.rectTransform;
            Vector2 center = c.world.InverseTransformPoint(resourceIcon.TransformPoint(resourceIcon.rect.center));
            c.MoveToWorldPoint(center);
            Call(c, "AdvanceMovement", 100f);
            Require(c.IsCollecting && Get<int>(c, "target") == 0, "Resource arrival did not begin harvesting the intended material.");
            Require(c.TryGetHarvestWorldPosition(out Vector3 aim) && (aim - resourceIcon.TransformPoint(resourceIcon.rect.center)).sqrMagnitude < .0001f, "The harvest motion no longer aims at the material center.");
            Call(motion, "OnDisable");
            Set(c, "collectElapsed", (float)Call(c, "HarvestDuration") * .5f);
            Call(motion, "ApplyPose", .2f);
            float handDistance = Mathf.Min((motion.leftHand.TransformPoint(motion.leftHand.rect.center) - aim).sqrMagnitude, (motion.rightHand.TransformPoint(motion.rightHand.rect.center) - aim).sqrMagnitude);
            Require(handDistance < .0001f, "Neither round hand reaches the material center at contact.");
            Set(c, "collectElapsed", (float)Call(c, "HarvestDuration"));
            Call(motion, "ApplyPose", 1f);
            RequireNear(motion.leftHand.anchoredPosition, restLeft, "The left hand does not retract after harvesting.");
            RequireNear(motion.rightHand.anchoredPosition, restRight, "The right hand does not retract after harvesting.");
            c.MoveToWorldPoint(empty);
            Require(!c.IsCollecting && !c.TryGetHarvestWorldPosition(out _), "A ground tap did not interrupt harvesting.");
            Call(c, "AdvanceMovement", 100f);
            Call(motion, "OnDisable");

            var hud = c.transform.Find("HUD") as RectTransform;
            Require(hud && !hud.IsChildOf(c.world), "HUD is missing or scrolls with the world.");
            Vector2 hudPosition = hud.anchoredPosition;
            c.player.anchoredPosition = new Vector2(400, -600);
            camera.SnapToPlayer();
            RequireNear(c.world.anchoredPosition, new Vector2(195, -280) - (c.player.anchoredPosition + camera.playerFocusOffset), "Camera no longer follows the local player.");
            RequireNear(hud.anchoredPosition, hudPosition, "Camera follow moved the HUD.");
        }

        private static void ValidateShop(GameScreenController c)
        {
            int[] quantities = Get<int[]>(c, "quantities"), protection = Get<int[]>(c, "protectedAmounts");
            int[] unitPrices = { 6, 30, 18, 90 };
            for (int offer = 0; offer < 8; offer++)
            {
                int material = offer / 2, count = offer % 2 == 0 ? 10 : 50, price = unitPrices[material] * count;
                Array.Clear(quantities, 0, quantities.Length);
                Array.Clear(protection, 0, protection.Length);
                Set(c, "funds", 10000);
                c.PurchaseShopItem(offer);
                Require(Get<int>(c, "funds") == 10000 - price, "Shop offer " + offer + " charged the wrong price.");
                Require(quantities[material * 3] == count && quantities.Sum() == count, "Shop purchases must add only the requested ordinary-quality material.");
                Require(c.historyText.text.Contains("購入 -") && c.historyText.text.Contains("[0:00]"), "Purchase history is missing its timestamp or cost.");
                Array.Clear(quantities, 0, quantities.Length);
                Set(c, "funds", price - 1);
                Call(c, "RefreshAll");
                Button button = offer % 2 == 0 ? c.shopItems[material].buyTenButton : c.shopItems[material].buyFiftyButton;
                Require(!button.interactable, "An unaffordable shop offer remains enabled.");
                c.PurchaseShopItem(offer);
                Require(quantities.Sum() == 0 && Get<int>(c, "funds") == price - 1, "An unaffordable purchase changed inventory or funds.");
            }
            int funds = Get<int>(c, "funds");
            c.PurchaseShopItem(-1);
            c.PurchaseShopItem(8);
            Require(Get<int>(c, "funds") == funds && quantities.Sum() == 0, "An invalid shop offer changed state.");
        }

        private static void ValidateSalesAndUpgrades(GameScreenController c)
        {
            int[] quantities = Get<int[]>(c, "quantities"), protection = Get<int[]>(c, "protectedAmounts"), levels = Get<int[]>(c, "levels");
            Array.Clear(quantities, 0, quantities.Length);
            Array.Clear(protection, 0, protection.Length);
            Set(c, "funds", 1000);
            quantities[0] = 10;
            quantities[3] = 8;
            Call(c, "RefreshAll");
            c.SelectInventory(0);
            c.ProtectMore();
            c.SetSaleQuantity("999999");
            Require(protection[0] == 5 && Get<int>(c, "sellQuantity") == 5 && c.sellQuantityInput.text == "5", "Protected stock or sale clamping changed.");
            c.SellSelected();
            Require(quantities[0] == 5 && protection[0] == 5 && Get<int>(c, "funds") == 1010, "Selected sale consumed protected stock or credited the wrong amount.");
            c.SellAll();
            c.SellAll();
            Require(quantities[0] == 5 && quantities[3] == 0 && protection[0] == 5 && Get<int>(c, "funds") == 1090, "Bulk sale did not preserve locked stock or was applied twice.");
            for (int i = 0; i < 4; i++)
            {
                Set(c, "funds", 149);
                c.BuyUpgrade(i);
                Require(levels[i] == 0 && Get<int>(c, "funds") == 149, "An unaffordable upgrade was purchased.");
                Set(c, "funds", 1000);
                c.BuyUpgrade(i);
                Require(levels[i] == 1 && Get<int>(c, "funds") == 850 && c.upgrades[i].price.text == "225G", "The first upgrade must cost 150G and the next 225G.");
                if (i == 0) Require(Mathf.Approximately(c.MovementSpeed, 175), "The first movement upgrade changed its expected effect.");
                if (i == 1) Require(Mathf.Approximately((float)Call(c, "HarvestDuration"), 1.28f), "The first harvest-speed upgrade changed its expected effect.");
            }
        }

        private static void ValidateBonusAndPanels(GameScreenController c)
        {
            int[] quantities = Get<int[]>(c, "quantities");
            Set(c, "bonusRevealed", true);
            int[] counts = { 29, 30, 45, 60, 90 }, multipliers = { 0, 1, 2, 4, 8 };
            for (int i = 0; i < counts.Length; i++)
            {
                quantities[9] = counts[i];
                Call(c, "RefreshHUD");
                Require(c.bonusProgressText.text.EndsWith("報酬×" + multipliers[i]), "A bonus reward threshold is incorrect.");
            }
            c.OpenShop();
            Require(c.shopPanel.activeSelf && Get<int>(c, "activePanel") == 4 && !c.CanMoveOnMap, "Shop does not open as panel 4 and block map input.");
            Vector2 destination = Get<Vector2>(c, "moveDestination");
            c.MoveToWorldPoint(new Vector2(500, -700));
            RequireNear(Get<Vector2>(c, "moveDestination"), destination, "A map tap behind the shop changed the destination.");
            c.OpenWarehouse();
            Require(c.warehousePanel.activeSelf && !c.shopPanel.activeSelf, "Warehouse did not replace the shop.");
            c.OpenUpgrade();
            Require(c.upgradePanel.activeSelf && !c.warehousePanel.activeSelf, "Upgrades did not replace the warehouse.");
            c.OpenMenu();
            Require(c.menuPanel.activeSelf && !c.upgradePanel.activeSelf, "Menu did not replace upgrades.");
            c.ClosePanel();
            Require(c.CanMoveOnMap && !c.menuPanel.activeSelf && !c.shopPanel.activeSelf, "Closing panels did not restore map input.");
        }

        private static void ValidateRoundEnd(GameScreenController c)
        {
            c.OpenShop();
            Set(c, "remainingTime", 0f);
            Call(c, "Update");
            Require(Get<bool>(c, "ended") && c.endedPanel.activeSelf && c.timerText.text == "0:00", "Time expiration did not show the round-end state.");
            Require(!c.shopPanel.activeSelf && !c.warehousePanel.activeSelf && !c.upgradePanel.activeSelf && !c.menuPanel.activeSelf, "A gameplay sheet remained open at timeout.");
            int funds = Get<int>(c, "funds");
            int[] quantities = (int[])Get<int[]>(c, "quantities").Clone(), protection = (int[])Get<int[]>(c, "protectedAmounts").Clone(), levels = (int[])Get<int[]>(c, "levels").Clone();
            Vector2 position = c.player.anchoredPosition, destination = Get<Vector2>(c, "moveDestination");
            c.PurchaseShopItem(0); c.BuyUpgrade(0); c.SellAll(); c.SellSelected(); c.ProtectMore(); c.ToggleProtection();
            c.OpenShop(); c.OpenWarehouse(); c.OpenUpgrade(); c.OpenMenu();
            c.MoveToWorldPoint(new Vector2(80, -140));
            Call(c, "AdvanceMovement", 100f);
            Require(Get<int>(c, "funds") == funds && Get<int[]>(c, "quantities").SequenceEqual(quantities) && Get<int[]>(c, "protectedAmounts").SequenceEqual(protection) && Get<int[]>(c, "levels").SequenceEqual(levels), "An action changed finances or inventory after round end.");
            RequireNear(c.player.anchoredPosition, position, "The player moved after round end.");
            RequireNear(Get<Vector2>(c, "moveDestination"), destination, "A new destination was accepted after round end.");
            Require(!c.IsMoving && !c.IsCollecting && !c.destinationMarker.gameObject.activeSelf && !c.TryGetHarvestWorldPosition(out _), "Movement, harvest target or marker survived round end.");
            Require(!c.shopPanel.activeSelf && c.shopItems.All(view => !view.buyTenButton.interactable && !view.buyFiftyButton.interactable), "Shop remained actionable after round end.");
        }

        private static void ValidateLobby(GameObject root)
        {
            var c = root.GetComponent<LobbyScreenController>();
            Require(c, "Lobby controller is missing.");
            foreach (var button in root.GetComponentsInChildren<Button>(true)) ValidateSharedButton(button);
            foreach (string field in new[] { "headerRoot", "choosingRoot", "waitingRoot", "teamRoot", "homeNavigation", "codeInput", "joinErrorRoot", "joinErrorLabel", "notReadyActions", "readyActions", "ownStatusBackground", "ownStatusLabel", "inviteCodeLabel", "copyCodeLabel", "startButton", "gameNavigation", "waitingHeadline", "waitingSubline", "waitingCountLabel", "waitingCodeLabel", "waitingCopyLabel", "waitingShareLabel", "waitingSeedLabel", "waitingContinueLabel", "waitingContinueButton" })
                Require(Get<Object>(c, field), "Lobby." + field + " is missing.");
            Require(Get<GameObject[]>(c, "waitingFilledSlots")?.Length == 3 && Get<GameObject[]>(c, "waitingEmptySlots")?.Length == 3, "Lobby waiting slots are incomplete.");
            Call(c, "OnEnable");
            Require(c.CurrentView == "Choosing" && !c.IsReady && Get<GameObject>(c, "choosingRoot").activeSelf, "Lobby does not start in create/join selection.");
            c.ToggleReady();
            Require(!c.IsReady, "Ready can be toggled before entering a team.");
            foreach (string code in new[] { "", "IMO-000", "IMO-777" })
            {
                c.OnCodeChanged(code);
                c.JoinTeam();
                Require(c.CurrentView == "Choosing" && Get<GameObject>(c, "joinErrorRoot").activeSelf && !string.IsNullOrWhiteSpace(Get<TMP_Text>(c, "joinErrorLabel").text), "Lobby accepted an empty, invalid or active-team invite code.");
            }
            c.OnCodeChanged("imo-482");
            Require(Get<TMP_InputField>(c, "codeInput").text == "IMO-482" && !Get<GameObject>(c, "joinErrorRoot").activeSelf, "Invite typing did not uppercase the code and clear the error.");
            c.JoinTeam();
            Require(c.CurrentView == "Team" && c.JoinedCount == 3 && !c.IsReady, "A valid invite did not enter team preparation.");
            c.ToggleReady();
            Require(c.IsReady && Get<GameObject>(c, "readyActions").activeSelf && Get<Button>(c, "startButton").interactable, "Ready did not enable the start action.");
            c.ToggleReady();
            Require(!c.IsReady && Get<GameObject>(c, "notReadyActions").activeSelf && !Get<Button>(c, "startButton").interactable, "Cancel ready did not disable start.");
            c.Back(); // Team -> Choosing; never invoke Back from Choosing because that navigates.
            c.CreateTeam();
            Require(c.CurrentView == "Waiting" && c.JoinedCount == 1 && !Get<Button>(c, "waitingContinueButton").interactable, "Create team did not begin waiting with one member.");
            c.EnterTeamPreparation();
            Require(c.CurrentView == "Waiting", "An incomplete team skipped waiting.");
            Set(c, "waitingElapsed", 5.3f);
            Call(c, "Update");
            Require(c.JoinedCount == 3 && Get<Button>(c, "waitingContinueButton").interactable, "The local invitation sequence did not enable preparation after everyone joined.");
            c.EnterTeamPreparation();
            Require(c.CurrentView == "Team" && !c.IsReady, "A full waiting team did not enter preparation safely.");
        }

        private static void ValidateHome(GameObject root)
        {
            var labels = root.GetComponentsInChildren<TMP_Text>(true).Select(text => text.text).ToArray();
            foreach (string caption in new[] { "通常クリア", "未達成", "ランキング", "未登録" })
                Require(labels.Contains(caption), "Home is missing the V2 label: " + caption);
            var play = root.transform.Find("PlayButton");
            var nav = root.transform.Find("BottomNavigation");
            Require(play && play.GetComponent<Button>(), "Home PlayButton is missing.");
            ValidateSharedButton(play.GetComponent<Button>());
            Require(nav && PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(nav.gameObject) == "Assets/Prefabs/UI/BottomNavigation.prefab", "Home bottom navigation is no longer a nested prefab.");
            Require(nav.GetComponentsInChildren<Button>(true).Length == 3, "Home bottom navigation no longer has three actions.");
            // Existing Home settings/nav placeholders are retained; only Play has an implemented transition.
            Require(!root.transform.Find("PlayButtonShadow"), "An independent duplicate Play shadow returned.");
        }

        private static void ValidateLoading(GameObject root)
        {
            var c = root.GetComponent<WorldLoadingController>();
            Require(c, "World loading controller is missing.");
            Require(c.headline && c.sessionText && c.percentText && c.tipText && c.startButtonText && c.pot && c.progressFill && c.hintRoot && c.startRoot && c.startButton, "World loading references are incomplete.");
            Require(c.steps?.Length == 5 && c.members?.Length == 3, "World loading needs five steps and three member states.");
            foreach (var step in c.steps)
                Require(step != null && step.label && step.note && step.doneRoot && step.activeRoot && step.pendingRoot && step.spinner, "A world-loading step reference is missing.");
            foreach (var member in c.members)
                Require(member != null && member.avatar && member.badge && member.state, "A world-loading member reference is missing.");
            Require(c.progressFill.GetComponent<RoundedProgressFill>(), "World loading progress has lost rounded ends.");
            var buttons = root.GetComponentsInChildren<Button>(true);
            Require(buttons.Length > 0, "World loading has no continue button.");
            foreach (var button in buttons) ValidateSharedButton(button);
            // Rendering progress is safe in the isolated prefab. Start would load a real scene.
            Call(c, "RenderProgress", 55);
            Require(c.percentText.text == "55%" && Mathf.Approximately(c.progressFill.fillAmount, .55f) && c.steps[2].activeRoot.activeSelf, "World loading progress or current step is not connected.");
            Call(c, "RenderProgress", 100);
            Require(c.members.All(member => member.state.text == "OK") && c.steps.All(step => step.doneRoot.activeSelf), "Completed loading presentation did not finish every step/member.");
        }

        private static void ValidateSharedButton(Button button)
        {
            string source = PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(button.gameObject);
            Require(PrefabUtility.IsPartOfPrefabInstance(button) && SharedButtons.Contains(source), button.name + " is not an existing shared button prefab: " + source);
            Require(button.onClick.GetPersistentEventCount() > 0, button.name + " has no saved click action.");
            for (int i = 0; i < button.onClick.GetPersistentEventCount(); i++)
                Require(button.onClick.GetPersistentTarget(i) && !string.IsNullOrEmpty(button.onClick.GetPersistentMethodName(i)) && button.onClick.GetPersistentListenerState(i) != UnityEventCallState.Off, button.name + " has a broken or disabled click action.");
        }

        private static void RequireIntHandler(Button button, Object target, string method, int argument)
        {
            var serialized = new SerializedObject(button);
            var calls = serialized.FindProperty("m_OnClick.m_PersistentCalls.m_Calls");
            for (int i = 0; i < button.onClick.GetPersistentEventCount(); i++)
            {
                if (button.onClick.GetPersistentTarget(i) != target || button.onClick.GetPersistentMethodName(i) != method) continue;
                Require(calls != null && calls.GetArrayElementAtIndex(i).FindPropertyRelative("m_Arguments.m_IntArgument").intValue == argument, button.name + " has the wrong saved shop offer index.");
                return;
            }
            Require(false, button.name + " has no " + method + " callback.");
        }

        private static T Get<T>(object target, string field) => (T)FindField(target, field).GetValue(target);
        private static void Set(object target, string field, object value) => FindField(target, field).SetValue(target, value);
        private static FieldInfo FindField(object target, string field) => target.GetType().GetField(field, InstanceFields) ?? throw new MissingFieldException(target.GetType().Name, field);
        private static object Call(object target, string method, params object[] arguments)
        {
            var info = target.GetType().GetMethod(method, InstanceFields);
            if (info == null) throw new MissingMethodException(target.GetType().Name, method);
            try { return info.Invoke(target, arguments); }
            catch (TargetInvocationException exception) { throw exception.InnerException ?? exception; }
        }
        private static void RequireNear(Vector2 actual, Vector2 expected, string message) => Require((actual - expected).sqrMagnitude < .0001f, message + " Expected " + expected + ", actual " + actual + ".");
        private static void Require(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException("Mock V2 validation failed: " + message);
        }
    }
}
