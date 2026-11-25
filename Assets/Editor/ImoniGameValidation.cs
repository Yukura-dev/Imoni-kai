using System;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace Imoni.UI.Editor
{
    /// <summary>Checks the saved screen in an isolated prefab contents scene; never saves test state.</summary>
    public static class ImoniGameValidation
    {
        private const string PrefabPath = "Assets/Prefabs/UI/GameScreen.prefab";
        private const BindingFlags PrivateInstance = BindingFlags.Instance | BindingFlags.NonPublic;
        private static readonly string[] SharedButtons =
        {
            "Assets/Prefabs/UI/PrimaryButton.prefab",
            "Assets/Prefabs/UI/RedButton.prefab",
            "Assets/Prefabs/UI/YellowButton.prefab"
        };

        [MenuItem("Imoni/Game/Validate Game Screen")]
        public static void Validate()
        {
            ImoniMockV2Validation.Validate();
        }

        private static void ValidateLegacy()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("Stop Play mode before validating the saved Game screen.");
            if (!AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath))
                throw new InvalidOperationException("Build GameScreen.prefab before running validation.");

            GameObject root = null;
            UnityEngine.Random.State randomState = UnityEngine.Random.state;
            try
            {
                root = PrefabUtility.LoadPrefabContents(PrefabPath);
                GameScreenController controller = root.GetComponent<GameScreenController>();
                Require(controller, "GameScreenController is missing from the prefab root.");
                Require(root.GetComponentsInChildren<Component>(true).All(component => component), "A component or script is missing.");
                ValidateReferences(controller);
                Button[] buttons = root.GetComponentsInChildren<Button>(true);
                Require(buttons.Length > 0, "No buttons were found.");
                foreach (Button button in buttons) ValidateButton(button);
                Require(controller.sellQuantityInput.onValueChanged.GetPersistentEventCount() > 0, "Sale quantity input has no saved change handler.");

                Call(controller, "Start");
                Require(!controller.endedPanel.activeSelf, "The result panel must be hidden at round start.");
                ValidateMovement(controller);
                ValidateCharacterMotion(controller);
                int[] quantities = Get<int[]>(controller, "quantities");
                int[] protectedAmounts = Get<int[]>(controller, "protectedAmounts");
                int[] levels = Get<int[]>(controller, "levels");
                Array.Clear(quantities, 0, quantities.Length);
                Array.Clear(protectedAmounts, 0, protectedAmounts.Length);
                Set(controller, "funds", 500);
                quantities[0] = 10; // Normal wood: price 2.
                quantities[3] = 8;  // Normal taro: price 10.
                Call(controller, "RefreshAll");

                controller.OpenWarehouse();
                Require(controller.warehousePanel.activeSelf, "Warehouse did not open.");
                controller.SelectInventory(0);
                controller.ProtectMore();
                Require(protectedAmounts[0] == 5, "ProtectMore must protect five units.");
                controller.SetSaleQuantity("999999");
                Require(Get<int>(controller, "sellQuantity") == 5, "Requested sale quantity was not clamped to unprotected stock.");
                Require(controller.sellQuantityInput.text == "5", "Clamped sale quantity was not reflected in the input.");
                controller.SellSelected();
                Require(quantities[0] == 5 && protectedAmounts[0] == 5, "Selected sale consumed protected stock.");
                Require(Get<int>(controller, "funds") == 510, "Selected sale credited an incorrect amount.");
                controller.SellAll();
                Require(quantities[0] == 5 && protectedAmounts[0] == 5, "Bulk sale consumed protected wood.");
                Require(quantities[3] == 0 && Get<int>(controller, "funds") == 590, "Bulk sale did not sell unprotected taro exactly once.");
                controller.SellAll();
                Require(Get<int>(controller, "funds") == 590 && quantities[0] == 5, "Bulk sale with only protected stock changed inventory or funds.");

                Set(controller, "funds", 0);
                int previousLevel = levels[0];
                controller.BuyUpgrade(0);
                Require(levels[0] == previousLevel && Get<int>(controller, "funds") == 0, "Insufficient funds still allowed an upgrade.");
                Set(controller, "funds", 500);
                controller.BuyUpgrade(0);
                Require(levels[0] == previousLevel + 1 && Get<int>(controller, "funds") == 400, "Upgrade did not consume 100 funds and add one level.");

                controller.OpenUpgrade();
                Require(controller.upgradePanel.activeSelf && !controller.warehousePanel.activeSelf, "Opening upgrades did not switch panels.");
                controller.OpenMenu();
                Require(controller.menuPanel.activeSelf && !controller.upgradePanel.activeSelf, "Opening the menu did not switch panels.");
                Set(controller, "remainingTime", 0f);
                Call(controller, "Update");
                Require(Get<bool>(controller, "ended"), "Timer expiration did not end the round.");
                Require(controller.endedPanel.activeSelf, "Timer expiration did not show the result panel.");
                Require(!controller.warehousePanel.activeSelf && !controller.upgradePanel.activeSelf && !controller.menuPanel.activeSelf, "Timer expiration left a gameplay panel open.");
                Require(controller.timerText.text == "0:00", "Timer expiration did not display 0:00.");
                int endedFunds = Get<int>(controller, "funds");
                controller.BuyUpgrade(0);
                controller.SellAll();
                Require(Get<int>(controller, "funds") == endedFunds, "A financial action changed state after the round ended.");
                Vector2 endedDestination = Get<Vector2>(controller, "moveDestination");
                Vector2 endedPosition = controller.player.anchoredPosition;
                controller.MoveToWorldPoint(new Vector2(80f, -140f));
                Call(controller, "AdvanceMovement", 100f);
                RequireNear(Get<Vector2>(controller, "moveDestination"), endedDestination, "A map command changed the destination after the round ended.");
                RequireNear(controller.player.anchoredPosition, endedPosition, "The player moved after the round ended.");
                Require(!Get<bool>(controller, "moving") && !controller.destinationMarker.gameObject.activeSelf, "Movement or its marker remained active after the round ended.");
                Require(!controller.TryGetHarvestWorldPosition(out _), "A finished round still exposes a gathering target.");

                Debug.Log("IMONI_GAME_VALIDATED: Saved UI references, " + buttons.Length + " nested shared buttons and persistent handlers, exact ground taps, clear gathering positions, hand contact at resource centers, collection interruption, pointer coordinates, camera bounds and fixed HUD, walking and gathering poses, protected sales, sale quantity bounds, upgrade costs, panel switching and timeout passed. Test state was not saved.");
            }
            finally
            {
                UnityEngine.Random.state = randomState;
                if (root) PrefabUtility.UnloadPrefabContents(root);
            }
        }

        private static void ValidateMovement(GameScreenController controller)
        {
            GameMapCamera camera = controller.GetComponentInChildren<GameMapCamera>(true);
            GameMapInput input = controller.GetComponentInChildren<GameMapInput>(true);
            ValidateMapStructure(controller, camera, input);

            Vector2 originalPlayer = controller.player.anchoredPosition;
            Vector2 originalWorld = controller.world.anchoredPosition;
            Vector2 originalDestination = Get<Vector2>(controller, "moveDestination");
            Vector2 originalMarker = controller.destinationMarker.anchoredPosition;
            bool originalMarkerActive = controller.destinationMarker.gameObject.activeSelf;
            bool originalMoving = Get<bool>(controller, "moving");
            bool originalCollecting = Get<bool>(controller, "collecting");
            int originalTarget = Get<int>(controller, "target");
            int originalPending = Get<int>(controller, "pendingResource");
            Vector2 originalDirection = Get<Vector2>(controller, "lastMovementDirection");
            int originalPanel = Get<int>(controller, "activePanel");
            float originalProgress = Get<float>(controller, "collectElapsed");
            float[] originalFills = controller.resources.Select(resource => resource.progressFill.fillAmount).ToArray();
            int[] stocks = Get<int[]>(controller, "stocks");
            int originalStock = stocks[0];
            Vector2 feetOffset = new Vector2(20f, -42f);
            try
            {
                Vector2 emptyPoint = new Vector2(80f, -140f);
                controller.MoveToWorldPoint(emptyPoint);
                Require(Get<bool>(controller, "moving") && !Get<bool>(controller, "collecting"), "A map command did not begin free movement.");
                RequireNear(Get<Vector2>(controller, "moveDestination"), emptyPoint - feetOffset, "The destination did not place the player's feet at the tap.");
                Require(controller.destinationMarker.gameObject.activeSelf, "A map command did not show its destination marker.");
                RequireNear(controller.destinationMarker.anchoredPosition + new Vector2(controller.destinationMarker.rect.width * .5f, -controller.destinationMarker.rect.height * .5f), emptyPoint, "The marker was not centered on the tap.");
                controller.world.anchoredPosition += new Vector2(-75f, 120f);
                RequireNear(Get<Vector2>(controller, "moveDestination"), emptyPoint - feetOffset, "Scrolling the map changed the destination.");
                Call(controller, "AdvanceMovement", 100f);
                RequireNear(controller.player.anchoredPosition + feetOffset, emptyPoint, "Movement did not stop at the exact empty-ground tap.");
                Require(!Get<bool>(controller, "moving") && !Get<bool>(controller, "collecting") && Get<int>(controller, "target") == -1, "An empty-ground destination began collection or kept moving.");
                Require(!controller.destinationMarker.gameObject.activeSelf, "The destination marker remained visible after arrival.");

                Vector2 resourceCenter = ResourceCenter(controller, 0);
                Vector2 nearbyPoint = resourceCenter + new Vector2(8f, 4f);
                controller.MoveToWorldPoint(nearbyPoint);
                Call(controller, "AdvanceMovement", 100f);
                RequirePlayerClearOfResources(controller);
                Require(Get<bool>(controller, "collecting") && Get<int>(controller, "target") == 0, "Arrival near a resource did not start gathering the nearest resource.");
                Require(controller.TryGetHarvestWorldPosition(out Vector3 targetWorld) && (targetWorld - controller.resources[0].icon.rectTransform.TransformPoint(controller.resources[0].icon.rectTransform.rect.center)).sqrMagnitude < .0001f, "The gathering target is not the selected material's visual center.");
                Call(controller, "AdvanceMovement", .5f);
                Require(Get<float>(controller, "collectElapsed") > 0f && controller.resources[0].progressFill.fillAmount > 0f, "Collection did not advance after arrival.");
                controller.MoveToWorldPoint(emptyPoint);
                Require(Get<bool>(controller, "moving") && !Get<bool>(controller, "collecting") && Get<int>(controller, "target") == -1, "A new tap did not interrupt gathering.");
                Require(Get<int>(controller, "pendingResource") == -1 && !controller.TryGetHarvestWorldPosition(out _), "A ground tap retained the previous gathering target.");
                Require(Get<float>(controller, "collectElapsed") == 0f && !controller.resources[0].progressRoot.activeSelf, "A new tap left the previous collection progress active.");
                Require(controller.destinationMarker.gameObject.activeSelf, "A replacement destination did not show its marker.");

                Vector2 alternatePoint = emptyPoint + new Vector2(70f, -50f);
                controller.MoveToWorldPoint(alternatePoint);
                RequireNear(Get<Vector2>(controller, "moveDestination"), alternatePoint - feetOffset, "A new tap during movement did not replace the destination.");
                foreach (Vector2 invalid in new[] { new Vector2(float.NaN, 0f), new Vector2(0f, float.NaN), new Vector2(float.PositiveInfinity, 0f), new Vector2(0f, float.NegativeInfinity) })
                {
                    controller.MoveToWorldPoint(invalid);
                    RequireNear(Get<Vector2>(controller, "moveDestination"), alternatePoint - feetOffset, "A non-finite map coordinate changed the destination.");
                }

                controller.MoveToWorldPoint(new Vector2(-10000f, 10000f));
                Call(controller, "AdvanceMovement", 100f);
                RequireNear(controller.player.anchoredPosition, Vector2.zero, "The top-left destination was not clamped inside the world.");
                controller.MoveToWorldPoint(new Vector2(10000f, -10000f));
                Call(controller, "AdvanceMovement", 100f);
                RequireNear(controller.player.anchoredPosition, new Vector2(controller.world.rect.width - controller.player.rect.width, controller.player.rect.height - controller.world.rect.height), "The bottom-right destination was not clamped inside the world.");

                stocks[0] = 0;
                controller.MoveToWorldPoint(nearbyPoint);
                Call(controller, "AdvanceMovement", 100f);
                Require(!Get<bool>(controller, "collecting") && Get<int>(controller, "target") == -1, "Arrival selected a depleted resource.");
                stocks[0] = originalStock;

                controller.MoveToWorldPoint(resourceCenter);
                Require(Get<int>(controller, "pendingResource") == 0, "Clicking a material did not retain its intended gathering target.");
                stocks[0] = 0;
                Call(controller, "AdvanceMovement", 100f);
                Require(!controller.IsMoving && !controller.IsCollecting && Get<int>(controller, "target") == -1 && !controller.TryGetHarvestWorldPosition(out _), "A resource depleted during travel still began gathering or switched targets.");
                stocks[0] = originalStock;

                ValidateHarvestSpacing(controller);

                ValidatePointerInput(controller, input, feetOffset);
                controller.OpenMenu();
                Vector2 blockedDestination = Get<Vector2>(controller, "moveDestination");
                Require(!controller.CanMoveOnMap, "An open panel did not block map input.");
                controller.MoveToWorldPoint(emptyPoint);
                RequireNear(Get<Vector2>(controller, "moveDestination"), blockedDestination, "A map command changed the destination behind an open panel.");
                input.OnPointerDown(new PointerEventData(null) { position = Vector2.zero, button = PointerEventData.InputButton.Left });
                RequireNear(Get<Vector2>(controller, "moveDestination"), blockedDestination, "Pointer input changed the destination behind an open panel.");
                controller.ClosePanel();
                ValidateCamera(controller, camera);
            }
            finally
            {
                controller.player.anchoredPosition = originalPlayer;
                controller.world.anchoredPosition = originalWorld;
                controller.destinationMarker.anchoredPosition = originalMarker;
                controller.destinationMarker.gameObject.SetActive(originalMarkerActive);
                Set(controller, "moveDestination", originalDestination);
                Set(controller, "moving", originalMoving);
                Set(controller, "collecting", originalCollecting);
                Set(controller, "target", originalTarget);
                Set(controller, "pendingResource", originalPending);
                Set(controller, "lastMovementDirection", originalDirection);
                Set(controller, "activePanel", originalPanel);
                Set(controller, "collectElapsed", originalProgress);
                stocks[0] = originalStock;
                Call(controller, "RefreshAll");
                for (int i = 0; i < originalFills.Length; i++) controller.resources[i].progressFill.fillAmount = originalFills[i];
            }
        }

        private static void ValidateHarvestSpacing(GameScreenController controller)
        {
            Vector2 feetOffset = new Vector2(20f, -42f);
            Vector2[] approaches = { Vector2.left, Vector2.right, Vector2.up, Vector2.down };
            for (int index = 0; index < controller.resources.Length; index++)
            {
                Vector2 center = ResourceCenter(controller, index);
                foreach (Vector2 approach in approaches)
                {
                    Vector2 start = center + approach * 140f - feetOffset;
                    controller.player.anchoredPosition = new Vector2(
                        Mathf.Clamp(start.x, 0f, controller.world.rect.width - controller.player.rect.width),
                        Mathf.Clamp(start.y, controller.player.rect.height - controller.world.rect.height, 0f));
                    controller.MoveToWorldPoint(center);
                    Require(!controller.TryGetHarvestWorldPosition(out _), "A gathering target was exposed before arrival.");
                    Call(controller, "AdvanceMovement", 100f);
                    Require(!controller.IsMoving && controller.IsCollecting && Get<int>(controller, "target") == index, "Clicking material " + index + " from " + approach + " did not stop to gather that material.");
                    RequirePlayerClearOfResources(controller);
                    Vector2 position = controller.player.anchoredPosition;
                    Require(position.x >= 0f && position.x <= controller.world.rect.width - controller.player.rect.width && position.y <= 0f && position.y >= controller.player.rect.height - controller.world.rect.height, "The gathering position is outside the world.");
                    Require(controller.TryGetHarvestWorldPosition(out Vector3 aim) && ((Vector2)controller.world.InverseTransformPoint(aim) - center).sqrMagnitude < .0001f, "Gathering selected the wrong visual center.");
                }
            }
        }

        private static Vector2 ResourceCenter(GameScreenController controller, int index)
        {
            RectTransform icon = controller.resources[index].icon.rectTransform;
            return controller.world.InverseTransformPoint(icon.TransformPoint(icon.rect.center));
        }

        private static void RequirePlayerClearOfResources(GameScreenController controller)
        {
            var motion = controller.player.GetComponent<GameCharacterMotion>();
            RectTransform nameplate = controller.player.Find("NameBackground") as RectTransform;
            Rect body = RectInWorld(controller.world, motion.body);
            Rect label = RectInWorld(controller.world, nameplate);
            for (int index = 0; index < controller.resources.Length; index++)
            {
                Rect material = RectInWorld(controller.world, controller.resources[index].icon.rectTransform);
                Require(!body.Overlaps(material) && !label.Overlaps(material), "The stopped player or nameplate overlaps material " + index + ".");
            }
        }

        private static Rect RectInWorld(RectTransform world, RectTransform rect)
        {
            var corners = new Vector3[4];
            rect.GetWorldCorners(corners);
            Vector2 minimum = new Vector2(float.PositiveInfinity, float.PositiveInfinity);
            Vector2 maximum = new Vector2(float.NegativeInfinity, float.NegativeInfinity);
            foreach (Vector3 corner in corners)
            {
                Vector2 point = world.InverseTransformPoint(corner);
                minimum = Vector2.Min(minimum, point);
                maximum = Vector2.Max(maximum, point);
            }
            return Rect.MinMaxRect(minimum.x, minimum.y, maximum.x, maximum.y);
        }

        private static void ValidateMapStructure(GameScreenController controller, GameMapCamera camera, GameMapInput input)
        {
            Require(camera && input, "The saved map camera or pointer input component is missing.");
            CheckPublicReferences(camera, "GameMapCamera");
            CheckPublicReferences(input, "GameMapInput");
            Require(camera.world == controller.world && camera.player == controller.player && input.world == controller.world && input.controller == controller, "Map camera or input references point to a different screen.");
            RectTransform viewport = camera.viewport;
            Require(viewport.parent == controller.transform && controller.world.parent == viewport, "Field must be fixed under GameScreen, with World inside it.");
            Require(viewport.GetComponent<RectMask2D>(), "Field is missing its clipping mask.");
            RequireNear(viewport.rect.size, new Vector2(390f, 560f), "The saved field viewport has the wrong size.");
            RequireNear(controller.world.rect.size, new Vector2(780f, 1120f), "The saved world has the wrong size.");
            Require(controller.player.parent == controller.world && controller.destinationMarker.parent == controller.world && controller.popRect.parent == controller.world, "Player, destination marker and harvest feedback must use world coordinates.");
            Require(input.transform.parent == viewport && input.transform.GetSiblingIndex() > controller.world.GetSiblingIndex(), "Map input must cover the scrolling world from a fixed viewport child.");
            Image inputSurface = input.GetComponent<Image>();
            Require(inputSurface && inputSurface.raycastTarget, "The map input surface does not receive pointer hits.");
            RequireNear(((RectTransform)input.transform).rect.size, viewport.rect.size, "The map input surface does not cover the viewport.");
            Require(controller.toastRoot.transform.parent == viewport && controller.streakText.transform.parent.parent == viewport, "Toast and streak must stay outside the scrolling world.");
            foreach (GameScreenController.ResourceView resource in controller.resources)
            {
                Require(resource.button.transform.parent == controller.world, "A resource is outside the scrolling world.");
                Require(resource.button.navigation.mode == Navigation.Mode.None, "A resource still captures navigation input.");
                Require(resource.button.GetComponentsInChildren<Graphic>(true).All(graphic => !graphic.raycastTarget), "A resource graphic intercepts map pointer input.");
            }
        }

        private static void ValidatePointerInput(GameScreenController controller, GameMapInput input, Vector2 feetOffset)
        {
            Vector3 originalScale = controller.transform.localScale;
            try
            {
                // The isolated prefab has no Canvas: null pressEventCamera exercises the overlay-canvas path.
                controller.transform.localScale = originalScale * 1.35f;
                controller.world.anchoredPosition = new Vector2(-50f, 80f);
                Vector2 point = new Vector2(100f, -180f);
                var pointer = new PointerEventData(null) { button = PointerEventData.InputButton.Left };
                pointer.position = RectTransformUtility.WorldToScreenPoint(null, controller.world.TransformPoint(point));
                input.OnPointerDown(pointer);
                RequireNear(Get<Vector2>(controller, "moveDestination"), point - feetOffset, "Left/touch pointer coordinates ignored screen scaling or world scrolling.");
                point += new Vector2(50f, -30f);
                pointer.button = PointerEventData.InputButton.Right;
                pointer.position = RectTransformUtility.WorldToScreenPoint(null, controller.world.TransformPoint(point));
                input.OnPointerDown(pointer);
                RequireNear(Get<Vector2>(controller, "moveDestination"), point - feetOffset, "Right-click did not set the exact world destination.");
                pointer.button = PointerEventData.InputButton.Middle;
                pointer.position = Vector2.zero;
                input.OnPointerDown(pointer);
                input.OnPointerDown(null);
                RequireNear(Get<Vector2>(controller, "moveDestination"), point - feetOffset, "An unsupported or null pointer changed the destination.");
            }
            finally { controller.transform.localScale = originalScale; }
        }

        private static void ValidateCamera(GameScreenController controller, GameMapCamera camera)
        {
            RectTransform hud = controller.transform.Find("HUD") as RectTransform;
            RectTransform navigation = controller.transform.Find("GameNavigation") as RectTransform;
            Require(hud && navigation, "The fixed HUD or game navigation is missing.");
            Transform[] fixedElements = { hud, navigation, controller.streakText.transform.parent, controller.toastRoot.transform };
            Vector3[] positions = fixedElements.Select(element => element.position).ToArray();
            Vector2 center = new Vector2(controller.world.rect.width * .5f, -controller.world.rect.height * .5f);
            controller.player.anchoredPosition = center - camera.playerFocusOffset;
            camera.SnapToPlayer();
            RequireNear(controller.world.anchoredPosition + controller.player.anchoredPosition + camera.playerFocusOffset, new Vector2(camera.viewport.rect.width * .5f, -camera.viewport.rect.height * .5f), "The camera did not center the local player inside the field.");
            float right = controller.world.rect.width - controller.player.rect.width;
            float bottom = controller.player.rect.height - controller.world.rect.height;
            float overflowX = controller.world.rect.width - camera.viewport.rect.width;
            float overflowY = controller.world.rect.height - camera.viewport.rect.height;
            Vector2[] corners = { Vector2.zero, new Vector2(right, 0f), new Vector2(0f, bottom), new Vector2(right, bottom) };
            Vector2[] expected = { Vector2.zero, new Vector2(-overflowX, 0f), new Vector2(0f, overflowY), new Vector2(-overflowX, overflowY) };
            for (int i = 0; i < corners.Length; i++)
            {
                controller.player.anchoredPosition = corners[i];
                camera.SnapToPlayer();
                RequireNear(controller.world.anchoredPosition, expected[i], "Camera corner " + i + " exposed space beyond the map.");
                for (int j = 0; j < fixedElements.Length; j++)
                    Require((fixedElements[j].position - positions[j]).sqrMagnitude < .0001f, fixedElements[j].name + " moved with the map camera.");
            }
        }

        private static void ValidateCharacterMotion(GameScreenController controller)
        {
            GameCharacterMotion motion = controller.player.GetComponent<GameCharacterMotion>();
            Require(motion, "The player motion component is missing.");
            CheckPublicReferences(motion, "GameCharacterMotion");
            Require(motion.controller == controller, "Character motion does not use the saved game controller.");
            Require(motion.body.parent == motion.leftHand.parent && motion.body.parent == motion.rightHand.parent && motion.body.parent.parent == controller.player, "The body and hands must share a visual child beneath Player.");
            Require(motion.groundShadow.parent == controller.player, "The ground shadow must remain independent of the bouncing body.");
            Require(controller.player.GetComponentsInChildren<Graphic>(true).All(graphic => !graphic.raycastTarget), "Character graphics intercept map input.");
            RectTransform name = controller.player.Find("Name") as RectTransform;
            RectTransform nameBackground = controller.player.Find("NameBackground") as RectTransform;
            Require(name && nameBackground, "The fixed player nameplate is missing.");

            RectTransform[] animated = { motion.body, motion.leftHand, motion.rightHand, motion.groundShadow };
            Vector2[] restPositions = animated.Select(rect => rect.anchoredPosition).ToArray();
            Quaternion[] restRotations = animated.Select(rect => rect.localRotation).ToArray();
            Vector3[] restScales = animated.Select(rect => rect.localScale).ToArray();
            RectTransform[] fixedRects = { controller.player, controller.world, name, nameBackground };
            Vector2[] fixedPositions = fixedRects.Select(rect => rect.anchoredPosition).ToArray();
            Vector2 originalPlayer = controller.player.anchoredPosition;
            Quaternion[] fixedRotations = fixedRects.Select(rect => rect.localRotation).ToArray();
            Vector3[] fixedScales = fixedRects.Select(rect => rect.localScale).ToArray();
            bool originalMoving = Get<bool>(controller, "moving"), originalCollecting = Get<bool>(controller, "collecting"), originalEnded = Get<bool>(controller, "ended");
            int originalTarget = Get<int>(controller, "target");
            int originalPending = Get<int>(controller, "pendingResource");
            float originalProgress = Get<float>(controller, "collectElapsed");
            Vector2 originalDestination = Get<Vector2>(controller, "moveDestination");
            Vector2 originalDirection = Get<Vector2>(controller, "lastMovementDirection");
            Vector2 originalMarker = controller.destinationMarker.anchoredPosition;
            bool originalMarkerActive = controller.destinationMarker.gameObject.activeSelf;
            try
            {
                CallMotion(motion, "ApplyPose", 0f); // Capture the saved neutral artwork before changing state.
                controller.MoveToWorldPoint(controller.player.anchoredPosition + new Vector2(100f, -82f));
                CallMotion(motion, "ApplyPose", .1f);
                Require(motion.body.anchoredPosition.y > restPositions[0].y + .1f, "Walking did not lift the character artwork.");
                Require((motion.leftHand.anchoredPosition - restPositions[1]).sqrMagnitude > .1f && (motion.rightHand.anchoredPosition - restPositions[2]).sqrMagnitude > .1f, "Walking did not animate both hands.");
                RequirePose(fixedRects, fixedPositions, fixedRotations, fixedScales, "Walking animation moved a navigation target or nameplate.");

                Vector2[] pausedPositions = animated.Select(rect => rect.anchoredPosition).ToArray();
                Quaternion[] pausedRotations = animated.Select(rect => rect.localRotation).ToArray();
                Vector3[] pausedScales = animated.Select(rect => rect.localScale).ToArray();
                CallMotion(motion, "ApplyPose", 0f);
                RequirePose(animated, pausedPositions, pausedRotations, pausedScales, "A zero-delta frame advanced character animation.");

                Set(controller, "moving", false);
                Set(controller, "collecting", false);
                CallMotion(motion, "ApplyPose", 1f);
                RequirePose(animated, restPositions, restRotations, restScales, "Stopping did not return the character to its neutral pose.");

                Vector2 resourceCenter = ResourceCenter(controller, 0);
                controller.MoveToWorldPoint(resourceCenter + new Vector2(8f, 4f));
                Call(controller, "AdvanceMovement", 100f);
                Require(controller.IsCollecting, "The motion check did not reach a gathering state.");
                RequirePlayerClearOfResources(controller);
                fixedPositions[0] = controller.player.anchoredPosition;
                Set(controller, "collectElapsed", .8f);
                CallMotion(motion, "ApplyPose", .2f);
                Require(controller.TryGetHarvestWorldPosition(out Vector3 materialCenter), "The gathering pose has no material center.");
                Require(Mathf.Min((HandCenter(motion.leftHand) - materialCenter).sqrMagnitude, (HandCenter(motion.rightHand) - materialCenter).sqrMagnitude) < .0001f, "The gathering hand stopped short of the material center.");
                RequirePose(fixedRects, fixedPositions, fixedRotations, fixedScales, "Gathering animation moved a navigation target or nameplate.");
                Vector2 leftGathering = motion.leftHand.anchoredPosition, rightGathering = motion.rightHand.anchoredPosition;

                controller.MoveToWorldPoint(new Vector2(80f, -140f));
                Require(controller.IsMoving && !controller.IsCollecting, "A new movement command did not leave the gathering state.");
                Require(!controller.TryGetHarvestWorldPosition(out _), "A new movement command retained a hand target.");
                CallMotion(motion, "ApplyPose", .1f);
                Require((motion.leftHand.anchoredPosition - leftGathering).sqrMagnitude + (motion.rightHand.anchoredPosition - rightGathering).sqrMagnitude > .1f, "The hands remained in their gathering pose after a new movement command.");
                Set(controller, "ended", true);
                CallMotion(motion, "ApplyPose", 1f);
                RequirePose(animated, restPositions, restRotations, restScales, "Round end did not settle character motion.");

                Set(controller, "ended", false);
                CallMotion(motion, "ApplyPose", .1f);
                CallMotion(motion, "OnDisable");
                RequirePose(animated, restPositions, restRotations, restScales, "Disabling character motion did not restore its saved pose.");
                ValidateHandContact(controller, motion, restPositions[1], restPositions[2]);
            }
            finally
            {
                CallMotion(motion, "OnDisable");
                for (int i = 0; i < fixedRects.Length; i++)
                {
                    fixedRects[i].anchoredPosition = i == 0 ? originalPlayer : fixedPositions[i];
                    fixedRects[i].localRotation = fixedRotations[i];
                    fixedRects[i].localScale = fixedScales[i];
                }
                Set(controller, "moving", originalMoving);
                Set(controller, "collecting", originalCollecting);
                Set(controller, "ended", originalEnded);
                Set(controller, "target", originalTarget);
                Set(controller, "pendingResource", originalPending);
                Set(controller, "collectElapsed", originalProgress);
                Set(controller, "moveDestination", originalDestination);
                Set(controller, "lastMovementDirection", originalDirection);
                controller.destinationMarker.anchoredPosition = originalMarker;
                controller.destinationMarker.gameObject.SetActive(originalMarkerActive);
                Call(controller, "RefreshAll");
            }
        }

        private static void ValidateHandContact(GameScreenController controller, GameCharacterMotion motion, Vector2 leftRest, Vector2 rightRest)
        {
            Vector3 originalScale = controller.transform.localScale;
            Vector2 originalWorld = controller.world.anchoredPosition;
            try
            {
                foreach (float scale in new[] { 1f, 1.35f })
                {
                    controller.transform.localScale = originalScale * scale;
                    controller.world.anchoredPosition = new Vector2(-91f, 137f);
                    foreach (int index in new[] { 0, 7, 13 })
                    {
                        foreach (float side in new[] { -1f, 1f })
                        {
                            CallMotion(motion, "OnDisable");
                            Vector2 center = ResourceCenter(controller, index);
                            controller.player.anchoredPosition = center + Vector2.right * side * 140f - new Vector2(20f, -42f);
                            controller.MoveToWorldPoint(center);
                            Call(controller, "AdvanceMovement", 100f);
                            Require(controller.IsCollecting && Get<int>(controller, "target") == index, "The scaled hand test did not reach the requested material.");
                            Set(controller, "collectElapsed", 0f);
                            CallMotion(motion, "ApplyPose", 1f);
                            RequireNear(motion.leftHand.anchoredPosition, leftRest, "The left hand did not begin at rest.");
                            RequireNear(motion.rightHand.anchoredPosition, rightRest, "The right hand did not begin at rest.");
                            Require(controller.TryGetHarvestWorldPosition(out Vector3 aim), "The scaled hand test lost its material center.");
                            RectTransform lead = (HandCenter(motion.leftHand) - aim).sqrMagnitude < (HandCenter(motion.rightHand) - aim).sqrMagnitude ? motion.leftHand : motion.rightHand;
                            RectTransform support = lead == motion.leftHand ? motion.rightHand : motion.leftHand;
                            float duration = (float)typeof(GameScreenController).GetMethod("HarvestDuration", PrivateInstance).Invoke(controller, null);
                            Set(controller, "collectElapsed", duration * .5f);
                            CallMotion(motion, "ApplyPose", .2f);
                            Require((HandCenter(lead) - aim).sqrMagnitude < .0001f, "The nearest hand missed the material center under scale or scrolling.");
                            Require((HandCenter(support) - aim).sqrMagnitude > 4f, "Both hands merged at the material center.");
                            RequirePlayerClearOfResources(controller);
                            Set(controller, "collectElapsed", duration);
                            CallMotion(motion, "ApplyPose", 1f);
                            RequireNear(motion.leftHand.anchoredPosition, leftRest, "The left hand did not return after contact.");
                            RequireNear(motion.rightHand.anchoredPosition, rightRest, "The right hand did not return after contact.");
                        }
                    }
                }
            }
            finally
            {
                controller.transform.localScale = originalScale;
                controller.world.anchoredPosition = originalWorld;
                CallMotion(motion, "OnDisable");
            }
        }

        private static Vector3 HandCenter(RectTransform hand) => hand.TransformPoint(hand.rect.center);

        private static void CallMotion(GameCharacterMotion motion, string name, params object[] arguments)
        {
            MethodInfo method = typeof(GameCharacterMotion).GetMethod(name, PrivateInstance);
            if (method == null) throw new MissingMethodException(typeof(GameCharacterMotion).FullName, name);
            try { method.Invoke(motion, arguments); }
            catch (TargetInvocationException exception) { throw exception.InnerException ?? exception; }
        }

        private static void RequirePose(RectTransform[] targets, Vector2[] positions, Quaternion[] rotations, Vector3[] scales, string message)
        {
            for (int i = 0; i < targets.Length; i++)
                Require((targets[i].anchoredPosition - positions[i]).sqrMagnitude < .0001f && Quaternion.Angle(targets[i].localRotation, rotations[i]) < .01f && (targets[i].localScale - scales[i]).sqrMagnitude < .0001f, targets[i].name + ": " + message);
        }

        private static void ValidateReferences(GameScreenController controller)
        {
            Require(controller.requirements != null && controller.requirements.Length == 3, "Expected three requirement views.");
            Require(controller.resources != null && controller.resources.Length == 14, "Expected fourteen resource views.");
            Require(controller.inventoryRows != null && controller.inventoryRows.Length == 12, "Expected twelve inventory quality rows.");
            Require(controller.upgrades != null && controller.upgrades.Length == 4, "Expected four upgrade views.");
            Require(controller.navigation != null && controller.navigation.Length == 4, "Expected four navigation views.");
            Require(controller.materialSprites != null && controller.materialSprites.Length == 4, "Expected four material sprites.");
            Require(controller.fundsTexts != null && controller.fundsTexts.Length > 0, "Funds labels are missing.");
            CheckPublicReferences(controller, "GameScreenController");
        }

        private static void CheckPublicReferences(object view, string label)
        {
            Require(view != null, label + " is null.");
            foreach (FieldInfo field in view.GetType().GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.DeclaredOnly))
            {
                if (field.Name == "warehouseViewport") continue; // Optional when the list uses flexible layout.
                object value = field.GetValue(view);
                if (typeof(Object).IsAssignableFrom(field.FieldType))
                {
                    Require((Object)value, label + "." + field.Name + " is missing.");
                }
                else if (field.FieldType.IsArray)
                {
                    Array array = value as Array;
                    Require(array != null, label + "." + field.Name + " is null.");
                    for (int i = 0; i < array.Length; i++)
                    {
                        object item = array.GetValue(i);
                        string itemLabel = label + "." + field.Name + "[" + i + "]";
                        if (typeof(Object).IsAssignableFrom(field.FieldType.GetElementType())) Require((Object)item, itemLabel + " is missing.");
                        else CheckPublicReferences(item, itemLabel);
                    }
                }
            }
        }

        private static void ValidateButton(Button button)
        {
            string label = button.name;
            string source = PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(button.gameObject);
            Require(PrefabUtility.IsPartOfPrefabInstance(button), label + " is not a nested prefab instance.");
            Require(SharedButtons.Contains(source), label + " does not use an existing shared button prefab: " + source);
            int count = button.onClick.GetPersistentEventCount();
            Require(count > 0, label + " has no saved click handler.");
            for (int i = 0; i < count; i++)
            {
                Require(button.onClick.GetPersistentTarget(i), label + " has a missing click target.");
                Require(!string.IsNullOrEmpty(button.onClick.GetPersistentMethodName(i)), label + " has an empty click method.");
                Require(button.onClick.GetPersistentListenerState(i) != UnityEventCallState.Off, label + " has a disabled click handler.");
            }
        }

        private static T Get<T>(GameScreenController controller, string field) => (T)FindField(field).GetValue(controller);
        private static void Set(GameScreenController controller, string field, object value) => FindField(field).SetValue(controller, value);
        private static FieldInfo FindField(string name) => typeof(GameScreenController).GetField(name, PrivateInstance) ?? throw new MissingFieldException(typeof(GameScreenController).FullName, name);
        private static void Call(GameScreenController controller, string name, params object[] arguments)
        {
            MethodInfo method = typeof(GameScreenController).GetMethod(name, PrivateInstance);
            if (method == null) throw new MissingMethodException(typeof(GameScreenController).FullName, name);
            try { method.Invoke(controller, arguments); }
            catch (TargetInvocationException exception) { throw exception.InnerException ?? exception; }
        }
        private static void RequireNear(Vector2 actual, Vector2 expected, string message)
        {
            Require(Vector2.SqrMagnitude(actual - expected) < .0001f, message + " Expected " + expected + ", actual " + actual + ".");
        }
        private static void Require(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException("Game screen validation failed: " + message);
        }
    }
}
