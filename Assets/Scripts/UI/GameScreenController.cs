using System;
using System.Collections.Generic;
using System.Globalization;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Imoni.UI
{
    /// <summary>Local play state and presentation for the editable Game screen.</summary>
    [DisallowMultipleComponent]
    public sealed class GameScreenController : MonoBehaviour
    {
        [Serializable]
        public sealed class RequirementView
        {
            public Image background, progress;
            public TMP_Text label, count;
        }

        [Serializable]
        public sealed class ResourceView
        {
            public Button button;
            public int material;
            public Image icon, selection, selectionFill, progressFill;
            public GameObject progressRoot, growthRoot;
            public TMP_Text growthLabel;
        }

        [Serializable]
        public sealed class InventoryRowView
        {
            public Button button;
            public Image border, background, qualityBackground;
            public TMP_Text name, quality, quantity, protectedCount, sellable, price;
        }

        [Serializable]
        public sealed class UpgradeView
        {
            public Button button;
            public Image background;
            public TMP_Text level, currentEffect, nextEffect, price;
        }

        [Serializable]
        public sealed class ShopItemView
        {
            public TMP_Text name, unitPrice, buyTenLabel, buyFiftyLabel;
            public Button buyTenButton, buyFiftyButton;
            public Image buyTenBackground, buyFiftyBackground;
        }

        [Serializable]
        public sealed class MemberView
        {
            public TMP_Text name, activity, worldLabel;
            public Image statusDot;
            public CanvasGroup worldAvatar;
            public string displayName, connectedActivity;
            public bool connected = true;
        }

        [Serializable]
        public sealed class NavigationView
        {
            public Image background, icon;
            public TMP_Text label;
            public int panel;
        }

        [Header("HUD")]
        public TMP_Text timerText, bonusText, bonusProgressText, streakText, streakMultiplierText;
        public TMP_Text roundText;
        public TMP_Text[] fundsTexts;
        public RequirementView[] requirements;
        [Header("Field")]
        public RectTransform player, popRect, world, destinationMarker;
        public ResourceView[] resources;
        public Sprite[] materialSprites;
        public Sprite stumpSprite;
        public GameObject toastRoot, popRoot;
        public TMP_Text toastText, popText;
        public Image popBackground;
        [Header("Panels")]
        public GameObject warehousePanel, upgradePanel, menuPanel, shopPanel, endedPanel;
        public NavigationView[] navigation;
        public TMP_Text ownActivityText, historyText;
        public MemberView[] members;
        [Header("Warehouse")]
        public InventoryRowView[] inventoryRows;
        public GameObject emptyInventoryRoot, selectionPanel;
        public TMP_Text selectedNameText, selectedProtectedText, toggleProtectionText, sellSelectedText;
        public TMP_InputField sellQuantityInput;
        public Button sellSelectedButton, sellAllButton;
        public RectTransform warehouseViewport;
        public float warehouseListHeight = 342f, warehouseListSelectedHeight = 170f;
        [Header("Shop")]
        public ShopItemView[] shopItems;
        [Header("Upgrades and result")]
        public UpgradeView[] upgrades;
        public TMP_Text endTitleText, endSummaryText;

        private static readonly string[] MaterialNames = { "薪", "里芋", "ねぎ", "キノコ" };
        private static readonly string[] QualityNames = { "普通", "良質", "希少" };
        private static readonly string[] UpgradeNames = { "移動速度", "採集速度", "採集量", "採集運" };
        private static readonly int[] BaseAmounts = { 3, 2, 2, 1 };
        private static readonly int[] BasePrices = { 2, 10, 6, 30 };
        private static readonly int[] QualityMultipliers = { 1, 2, 5 };
        private static readonly int[] RequiredAmounts = { 60, 20, 15 };
        private static readonly CultureInfo Japanese = CultureInfo.GetCultureInfo("ja-JP");
        private static readonly Color Ink = Hex("4A2E22"), Green = Hex("34421C"), Cream = Hex("FFF3D6");
        private static readonly Color Yellow = Hex("F7B92B"), Red = Hex("8B2310"), Muted = Hex("6B4A3A");
        private static readonly Color[] QualityBackgrounds = { Color.white, Hex("DCE8C4"), Hex("C8DCEB") };
        private static readonly Color[] QualityForegrounds = { Ink, Green, Hex("1F3E5C") };

        private readonly int[] quantities = new int[12];
        private readonly int[] protectedAmounts = new int[12];
        private readonly int[] levels = { 0, 0, 0, 0 };
        private readonly List<string> history = new List<string>(3);
        private int[] stocks;
        private float[] regrowAt;
        private float remainingTime = 300f;
        private float elapsedTime;
        private int funds = 1000, streak, activePanel, selectedInventory = -1, sellQuantity;
        private int target = -1, pendingResource = -1, lastSecond = -1;
        private float collectElapsed, toastUntil, popUntil;
        private bool moving, collecting, ended, bonusRevealed;
        private bool showingRoundResult;
        private ImoniRoundSession.RoundStartState roundState;
        private Vector2 moveDestination;
        private Vector2 lastMovementDirection = Vector2.right;
        private static readonly Vector2 PlayerFeet = new Vector2(20f, -42f);
        private const float HarvestReach = 44f;
        private static readonly Vector2[] HarvestStandingOffsets =
        {
            new Vector2(-36f, -13f), new Vector2(36f, -13f),
            new Vector2(0f, 60f), new Vector2(0f, -72f),
            new Vector2(-36f, 60f), new Vector2(36f, 60f),
            new Vector2(-36f, -72f), new Vector2(36f, -72f)
        };

        public bool CanMoveOnMap => stocks != null && !ended && activePanel == 0 && player && world;
        public int RoundNumber => roundState?.RoundNumber ?? 1;
        public bool IsMoving => moving && !ended;
        public bool IsCollecting => collecting && !ended;
        public float MovementSpeed => 140f * MoveMultiplier();
        public float HarvestProgress => IsCollecting ? Mathf.Clamp01(collectElapsed / HarvestDuration()) : 0f;
        public bool TryGetHarvestWorldPosition(out Vector3 position)
        {
            position = Vector3.zero;
            if (!IsCollecting || !HasResourceStock(target) || !world) return false;
            position = world.TransformPoint(ResourcePosition(target));
            return true;
        }
        public Vector2 MotionDirection
        {
            get
            {
                if (IsCollecting && player && target >= 0 && target < resources.Length && resources[target]?.button != null)
                {
                    // Aim from hand height so a resource under the feet still has a direction.
                    Vector2 direction = ResourcePosition(target) - (player.anchoredPosition + PlayerFeet + Vector2.up * 13f);
                    if (direction.sqrMagnitude > .0001f) return direction.normalized;
                }
                return lastMovementDirection;
            }
        }

        private void Start()
        {
            roundState = ImoniRoundSession.ConsumeRoundStart();
            funds = roundState.Funds;
            int[] carriedLevels = roundState.UpgradeLevels;
            Array.Copy(carriedLevels, levels, levels.Length);
            int count = resources == null ? 0 : resources.Length;
            stocks = new int[count];
            regrowAt = new float[count];
            for (int i = 0; i < count; i++) stocks[i] = 3;
            AddHistory("ラウンド" + RoundNumber + "開始！必須素材が公開されたよ");
            RefreshAll();
        }

        private void Update()
        {
            if (stocks == null || ended) return;
            float delta = Time.deltaTime;
            remainingTime = Mathf.Max(0, remainingTime - delta);
            elapsedTime = 300f - remainingTime;
            if (remainingTime <= 0)
            {
                EndRound();
                return;
            }

            bool resourceChanged = false;
            for (int i = 0; i < stocks.Length; i++)
            {
                if (stocks[i] == 0 && elapsedTime >= regrowAt[i])
                {
                    stocks[i] = 3;
                    resourceChanged = true;
                }
            }
            if (resourceChanged) RefreshResources();

            AdvanceMovement(delta);

            if (!bonusRevealed && elapsedTime >= 120f)
            {
                bonusRevealed = true;
                AddHistory("ボーナス素材「キノコ」が発表された！");
                RefreshHUD();
            }
            int second = Mathf.CeilToInt(remainingTime);
            if (second != lastSecond)
            {
                lastSecond = second;
                RefreshClock();
                RefreshGrowthLabels();
            }
            SetActive(toastRoot, activePanel == 0 && Time.time < toastUntil);
            SetActive(popRoot, Time.time < popUntil);
        }

        public void OpenWarehouse() => TogglePanel(1);
        public void OpenUpgrade() => TogglePanel(2);
        public void OpenMenu() => TogglePanel(3);
        public void OpenShop() => TogglePanel(4);

        // Two offers per material: wood 10/50, taro 10/50, negi 10/50, mushroom 10/50.
        public void PurchaseShopItem(int index)
        {
            if (ended || index < 0 || index >= MaterialNames.Length * 2) return;
            int material = index / 2;
            int count = index % 2 == 0 ? 10 : 50;
            int price = BasePrices[material] * 3 * count;
            int inventoryIndex = material * 3;
            if (funds < price || quantities[inventoryIndex] > int.MaxValue - count) return;
            funds -= price;
            quantities[inventoryIndex] += count;
            AddHistory("あなたが" + MaterialNames[material] + "×" + count + "(普通)を購入 -" + Format(price) + "G");
            RefreshAll();
        }

        public void SetMemberConnected(int index, bool connected)
        {
            if (members == null || index < 0 || index >= members.Length || members[index] == null) return;
            MemberView member = members[index];
            if (member.connected == connected) return;
            member.connected = connected;
            AddHistory(MemberName(index, member) + (connected ? "が再接続しました" : "が切断しました(再接続待ち)"));
            RefreshActivity();
        }

        public void ClosePanel()
        {
            activePanel = 0;
            RefreshPanels();
        }

        private void TogglePanel(int panel)
        {
            if (ended) return;
            activePanel = activePanel == panel ? 0 : panel;
            RefreshAll();
        }

        public void SelectResource(int index)
        {
            // Retained for saved prefab events; map presses resolve the same resource center.
            if (!CanMoveOnMap || index < 0 || index >= resources.Length || resources[index]?.button == null) return;
            MoveToWorldPoint(ResourcePosition(index));
        }

        public void MoveToWorldPoint(Vector2 point)
        {
            if (!CanMoveOnMap || float.IsNaN(point.x) || float.IsNaN(point.y) ||
                float.IsInfinity(point.x) || float.IsInfinity(point.y)) return;

            Vector2 feet = ClampPlayerPosition(point - PlayerFeet) + PlayerFeet;
            int requested = FindResourceAtPoint(feet);
            int nearby = requested >= 0 ? requested : FindNearbyResource(feet);
            // Empty ground keeps its exact destination. Gathering must leave the artwork clear.
            if (nearby >= 0 && (requested >= 0 || !IsClearHarvestPosition(feet)))
            {
                Vector2 approach = requested >= 0 ? player.anchoredPosition + PlayerFeet : feet;
                if (!TryFindHarvestStandingPoint(nearby, approach, out feet)) return;
                requested = nearby;
            }
            // Resolve once; camera scrolling never changes the destination or selected resource.
            moveDestination = feet - PlayerFeet;
            pendingResource = requested;
            target = -1;
            moving = true;
            collecting = false;
            collectElapsed = 0;
            if (destinationMarker)
            {
                Vector2 size = destinationMarker.rect.size;
                destinationMarker.anchoredPosition = moveDestination + PlayerFeet + new Vector2(-size.x * .5f, size.y * .5f);
                destinationMarker.gameObject.SetActive(true);
            }
            RefreshResources();
            RefreshActivity();
        }

        private void AdvanceMovement(float delta)
        {
            if (ended) return;
            if (moving && player)
            {
                Vector2 direction = moveDestination - player.anchoredPosition;
                if (direction.sqrMagnitude > .0001f) lastMovementDirection = direction.normalized;
                player.anchoredPosition = Vector2.MoveTowards(player.anchoredPosition, moveDestination, MovementSpeed * delta);
                if (Vector2.SqrMagnitude(player.anchoredPosition - moveDestination) < .0001f)
                {
                    moving = false;
                    if (destinationMarker) destinationMarker.gameObject.SetActive(false);
                    target = pendingResource >= 0
                        ? (HasResourceStock(pendingResource) ? pendingResource : -1)
                        : FindNearbyResource(player.anchoredPosition + PlayerFeet);
                    pendingResource = -1;
                    if (target >= 0 && !IsClearHarvestPosition(player.anchoredPosition + PlayerFeet)) target = -1;
                    collecting = target >= 0;
                    collectElapsed = 0;
                    RefreshResources();
                    RefreshActivity();
                }
            }
            else if (collecting && target >= 0 && target < resources.Length)
            {
                collectElapsed += delta;
                float duration = HarvestDuration();
                if (resources[target].progressFill) resources[target].progressFill.fillAmount = Mathf.Clamp01(collectElapsed / duration);
                if (collectElapsed >= duration) Harvest();
            }
        }

        private Vector2 ResourcePosition(int index)
        {
            if (resources[index].icon && world)
            {
                RectTransform icon = resources[index].icon.rectTransform;
                return world.InverseTransformPoint(icon.TransformPoint(icon.rect.center));
            }
            var rect = (RectTransform)resources[index].button.transform;
            return rect.anchoredPosition + new Vector2(rect.rect.width * .5f, -rect.rect.height * .5f);
        }

        private Vector2 ClampPlayerPosition(Vector2 position) => new Vector2(
            Mathf.Clamp(position.x, 0f, Mathf.Max(0f, world.rect.width - player.rect.width)),
            Mathf.Clamp(position.y, Mathf.Min(0f, player.rect.height - world.rect.height), 0f));

        private bool HasResourceStock(int index) => stocks != null && index >= 0 && index < stocks.Length &&
            resources[index]?.button != null && stocks[index] > 0;

        private int FindResourceAtPoint(Vector2 point)
        {
            int nearest = -1;
            float closest = float.PositiveInfinity;
            for (int i = 0; i < resources.Length; i++)
            {
                if (!HasResourceStock(i)) continue;
                var rect = (RectTransform)resources[i].button.transform;
                Vector2 local = rect.InverseTransformPoint(world.TransformPoint(point));
                if (!rect.rect.Contains(local)) continue;
                float distance = (point - ResourcePosition(i)).sqrMagnitude;
                if (distance >= closest) continue;
                closest = distance;
                nearest = i;
            }
            return nearest;
        }

        private bool TryFindHarvestStandingPoint(int index, Vector2 approach, out Vector2 point)
        {
            point = Vector2.zero;
            float closest = float.PositiveInfinity;
            Vector2 center = ResourcePosition(index);
            // Prefer either side, where the character, nameplate and gathering bar stay readable.
            // Crowded clusters may need a slightly wider ring to leave the other materials clear.
            for (int ring = 0; ring < 3; ring++)
            {
                for (int i = 0; i < HarvestStandingOffsets.Length; i++)
                {
                    if (i == 2 && !float.IsPositiveInfinity(closest)) return true;
                    Vector2 offset = HarvestStandingOffsets[i];
                    if (offset.x != 0) offset.x += Mathf.Sign(offset.x) * ring * 24f;
                    if (i >= 2) offset.y += Mathf.Sign(offset.y) * ring * 24f;
                    Vector2 candidate = center + offset;
                    Vector2 clamped = ClampPlayerPosition(candidate - PlayerFeet) + PlayerFeet;
                    if ((candidate - clamped).sqrMagnitude > .0001f || !IsClearHarvestPosition(candidate)) continue;
                    float distance = (candidate - approach).sqrMagnitude;
                    if (distance >= closest) continue;
                    closest = distance;
                    point = candidate;
                }
                if (!float.IsPositiveInfinity(closest)) return true;
            }
            return false;
        }

        private bool IsClearHarvestPosition(Vector2 feet)
        {
            // Body, resting hands and nameplate, including room for the small gathering lean.
            var character = new Rect(
                feet + new Vector2(-14f, -16f),
                new Vector2(28f, 62f)
            );
            foreach (ResourceView resource in resources)
            {
                if (resource?.icon == null) continue;
                RectTransform icon = resource.icon.rectTransform;
                Vector2 a = world.InverseTransformPoint(icon.TransformPoint(icon.rect.min));
                Vector2 b = world.InverseTransformPoint(icon.TransformPoint(icon.rect.max));
                var material = Rect.MinMaxRect(Mathf.Min(a.x, b.x) - 2f, Mathf.Min(a.y, b.y) - 2f,
                    Mathf.Max(a.x, b.x) + 2f, Mathf.Max(a.y, b.y) + 2f);
                if (character.Overlaps(material)) return false;
            }
            return true;
        }

        private int FindNearbyResource(Vector2 feet)
        {
            int nearest = -1;
            float closest = HarvestReach * HarvestReach;
            for (int i = 0; i < resources.Length; i++)
            {
                if (!HasResourceStock(i)) continue;
                float distance = Vector2.SqrMagnitude(feet - ResourcePosition(i));
                if (distance > closest) continue;
                closest = distance;
                nearest = i;
            }
            return nearest;
        }

        private void Harvest()
        {
            if (target < 0 || stocks[target] <= 0) return;
            int index = target;
            int material = Mathf.Clamp(resources[index].material, 0, 3);
            float roll = UnityEngine.Random.value;
            float rareChance = RareChance(levels[3]);
            int quality = roll < rareChance ? 2 : roll < rareChance + .3f ? 1 : 0;
            streak = quality == 2 ? streak + 1 : 0;
            int amount = Mathf.Max(1, Mathf.RoundToInt(BaseAmounts[material] * (1 + .5f * levels[2]) * StreakMultiplier()));
            quantities[material * 3 + quality] += amount;
            stocks[index]--;
            collectElapsed = 0;
            if (stocks[index] <= 0)
            {
                regrowAt[index] = elapsedTime + 40f;
                target = -1;
                collecting = false;
            }
            string gain = MaterialNames[material] + "×" + amount + "(" + QualityNames[quality] + ")";
            ShowPop("+" + amount + " " + MaterialNames[material] + "(" + QualityNames[quality] + ")", quality);
            AddHistory("あなたが" + gain + "を採集");
            RefreshAll();
        }

        public void SelectInventory(int index)
        {
            if (ended || index < 0 || index >= quantities.Length || quantities[index] <= 0) return;
            selectedInventory = index;
            sellQuantity = Sellable(index);
            RefreshWarehouse();
        }

        public void ProtectLess() => ChangeProtection(-5);
        public void ProtectMore() => ChangeProtection(5);

        private void ChangeProtection(int delta)
        {
            if (ended || !HasSelection()) return;
            int i = selectedInventory;
            protectedAmounts[i] = Mathf.Clamp(protectedAmounts[i] + delta, 0, quantities[i]);
            sellQuantity = Mathf.Min(sellQuantity, Sellable(i));
            RefreshWarehouse();
        }

        public void ToggleProtection()
        {
            if (ended || !HasSelection()) return;
            int i = selectedInventory;
            protectedAmounts[i] = protectedAmounts[i] >= quantities[i] ? 0 : quantities[i];
            sellQuantity = Mathf.Min(sellQuantity, Sellable(i));
            RefreshWarehouse();
        }

        public void SetSalePercent(int percent)
        {
            if (ended || !HasSelection()) return;
            sellQuantity = Mathf.FloorToInt(Sellable(selectedInventory) * Mathf.Clamp(percent, 0, 100) / 100f);
            RefreshWarehouse();
        }

        public void SetSaleQuantity(string value)
        {
            if (ended || !HasSelection()) return;
            int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out int requested);
            sellQuantity = Mathf.Clamp(requested, 0, Sellable(selectedInventory));
            RefreshWarehouse();
        }

        public void SellSelected()
        {
            if (ended || !HasSelection()) return;
            int i = selectedInventory;
            int count = Mathf.Clamp(sellQuantity, 0, Sellable(i));
            if (count == 0) return;
            int gain = count * Price(i);
            quantities[i] -= count;
            funds += gain;
            sellQuantity = Mathf.Min(sellQuantity, Sellable(i));
            AddHistory("あなたが" + MaterialNames[i / 3] + "×" + count + "(" + QualityNames[i % 3] + ")を売却 +" + Format(gain) + "G");
            RefreshAll();
        }

        public void SellAll()
        {
            if (ended) return;
            int count = 0, gain = 0;
            for (int i = 0; i < quantities.Length; i++)
            {
                int amount = Sellable(i);
                count += amount;
                gain += amount * Price(i);
                quantities[i] = protectedAmounts[i];
            }
            if (count == 0) return;
            funds += gain;
            sellQuantity = 0;
            AddHistory("あなたが素材" + count + "個をまとめて売却 +" + Format(gain) + "G");
            RefreshAll();
        }

        public void BuyUpgrade(int index)
        {
            if (ended || index < 0 || index >= levels.Length) return;
            int price = UpgradePrice(levels[index]);
            if (funds < price) return;
            funds -= price;
            levels[index]++;
            AddHistory("あなたが" + UpgradeNames[index] + "を強化(Lv." + levels[index] + ") -" + Format(price) + "G");
            RefreshAll();
        }

        private void RefreshAll()
        {
            RefreshHUD();
            RefreshResources();
            RefreshWarehouse();
            RefreshShop();
            RefreshUpgrades();
            RefreshPanels();
            RefreshActivity();
        }

        private void RefreshClock()
        {
            int seconds = Mathf.CeilToInt(remainingTime);
            Text(timerText, Clock(seconds));
            if (timerText) timerText.color = seconds <= 30 ? Red : Ink;
            if (!bonusRevealed)
            {
                Text(bonusText, "あと " + Clock(Mathf.Max(0, Mathf.CeilToInt(120f - elapsedTime))) + " で発表!");
                SetActive(bonusProgressText ? bonusProgressText.gameObject : null, false);
            }
            else
            {
                int current = Converted(3), percent = Mathf.RoundToInt(current / 30f * 100);
                Text(bonusText, "キノコ " + Format(current) + "/30");
                Text(bonusProgressText, "達成率 " + percent + "% 報酬×" + BonusMultiplier(percent));
                SetActive(bonusProgressText ? bonusProgressText.gameObject : null, true);
            }
        }

        private void RefreshHUD()
        {
            RefreshClock();
            Text(roundText, "ラウンド" + RoundNumber + "/" + ImoniRoundSession.MaximumRounds);
            if (fundsTexts != null) foreach (TMP_Text label in fundsTexts) Text(label, Format(funds) + "G");
            Text(streakText, streak.ToString());
            Text(streakMultiplierText, "×" + StreakMultiplier().ToString("F2", CultureInfo.InvariantCulture));
            if (requirements == null) return;
            for (int i = 0; i < Mathf.Min(3, requirements.Length); i++)
            {
                RequirementView view = requirements[i];
                if (view == null) continue;
                int current = Converted(i);
                bool complete = current >= RequiredAmounts[i];
                if (view.background) view.background.color = complete ? Green : Cream;
                if (view.progress) view.progress.fillAmount = Mathf.Clamp01(current / (float)RequiredAmounts[i]);
                Text(view.label, MaterialNames[i]);
                Text(view.count, Format(current) + "/" + RequiredAmounts[i]);
                if (view.label) view.label.color = complete ? Cream : Ink;
                if (view.count) view.count.color = complete ? Cream : Ink;
            }
        }

        private void RefreshResources()
        {
            if (resources == null || stocks == null) return;
            for (int i = 0; i < resources.Length; i++)
            {
                ResourceView view = resources[i];
                if (view == null) continue;
                bool selected = i == target;
                if (view.selection) view.selection.color = selected ? Red : Color.clear;
                if (view.selectionFill) view.selectionFill.color = selected ? new Color(Cream.r, Cream.g, Cream.b, .85f) : Color.clear;
                if (view.icon)
                {
                    if (stocks[i] == 0) view.icon.sprite = stumpSprite;
                    else if (materialSprites != null && view.material >= 0 && view.material < materialSprites.Length)
                        view.icon.sprite = materialSprites[view.material];
                }
                if (view.button) view.button.interactable = !ended;
                SetActive(view.progressRoot, selected && collecting && !ended);
                if (view.progressFill) view.progressFill.fillAmount = Mathf.Clamp01(collectElapsed / HarvestDuration());
            }
            RefreshGrowthLabels();
        }

        private void RefreshGrowthLabels()
        {
            if (resources == null || stocks == null) return;
            for (int i = 0; i < resources.Length; i++)
            {
                ResourceView view = resources[i];
                if (view == null || !view.growthLabel) continue;
                SetActive(view.growthRoot ? view.growthRoot : view.growthLabel.gameObject, stocks[i] == 0);
                if (stocks[i] == 0) Text(view.growthLabel, "成長中 " + Mathf.Max(0, Mathf.CeilToInt(regrowAt[i] - elapsedTime)) + "秒");
            }
        }

        private void RefreshWarehouse()
        {
            bool any = false;
            if (inventoryRows != null)
            {
                for (int i = 0; i < Mathf.Min(inventoryRows.Length, 12); i++)
                {
                    InventoryRowView view = inventoryRows[i];
                    if (view == null) continue;
                    bool visible = quantities[i] > 0;
                    any |= visible;
                    SetActive(view.button ? view.button.gameObject : null, visible);
                    if (!visible) continue;
                    bool selected = selectedInventory == i;
                    if (view.border) view.border.color = selected ? Red : Hex("E9D5AE");
                    if (view.background) view.background.color = selected ? Hex("FFF7E6") : Color.white;
                    if (view.qualityBackground) view.qualityBackground.color = QualityBackgrounds[i % 3];
                    Text(view.name, MaterialNames[i / 3]);
                    Text(view.quality, QualityNames[i % 3]);
                    if (view.quality) view.quality.color = QualityForegrounds[i % 3];
                    Text(view.quantity, Format(quantities[i]));
                    Text(view.protectedCount, Format(protectedAmounts[i]));
                    Text(view.sellable, Format(Sellable(i)));
                    Text(view.price, Format(Price(i)));
                }
            }
            else
            {
                for (int i = 0; i < quantities.Length; i++) any |= quantities[i] > 0;
            }
            SetActive(emptyInventoryRoot, !any);
            bool hasSelection = HasSelection();
            SetActive(selectionPanel, hasSelection);
            if (warehouseViewport)
                warehouseViewport.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, hasSelection ? warehouseListSelectedHeight : warehouseListHeight);
            if (hasSelection)
            {
                int i = selectedInventory;
                sellQuantity = Mathf.Clamp(sellQuantity, 0, Sellable(i));
                Text(selectedNameText, MaterialNames[i / 3] + "(" + QualityNames[i % 3] + ")");
                Text(selectedProtectedText, Format(protectedAmounts[i]));
                Text(toggleProtectionText, protectedAmounts[i] >= quantities[i] ? "保護解除" : "全部保護");
                if (sellQuantityInput) sellQuantityInput.SetTextWithoutNotify(sellQuantity.ToString());
                Text(sellSelectedText, sellQuantity + "個 売却 +" + Format(sellQuantity * Price(i)) + "G");
            }
            if (sellSelectedButton)
            {
                sellSelectedButton.interactable = hasSelection && sellQuantity > 0 && !ended;
                var background = sellSelectedButton.GetComponent<Image>();
                if (background) background.color = sellSelectedButton.interactable ? Red : Hex("B8A586");
            }
            bool anythingSellable = false;
            for (int i = 0; i < quantities.Length; i++) anythingSellable |= Sellable(i) > 0;
            if (sellAllButton) sellAllButton.interactable = anythingSellable && !ended;
            Text(historyText, string.Join("\n", history.ConvertAll(entry => "・" + entry)));
        }

        private void RefreshUpgrades()
        {
            if (upgrades == null) return;
            for (int i = 0; i < Mathf.Min(upgrades.Length, 4); i++)
            {
                UpgradeView view = upgrades[i];
                if (view == null) continue;
                int price = UpgradePrice(levels[i]);
                bool available = funds >= price && !ended;
                Text(view.level, "Lv." + levels[i]);
                Text(view.currentEffect, UpgradeEffect(i, levels[i]));
                Text(view.nextEffect, "→ " + UpgradeEffect(i, levels[i] + 1));
                Text(view.price, Format(price) + "G");
                if (view.button) view.button.interactable = available;
                if (view.background) view.background.color = available ? Green : Hex("E9D5AE");
                if (view.price) view.price.color = available ? Cream : Muted;
                if (view.button)
                    foreach (var label in view.button.GetComponentsInChildren<TMP_Text>(true))
                        label.color = available ? Cream : Muted;
            }
        }

        private void RefreshShop()
        {
            if (shopItems == null) return;
            for (int material = 0; material < Mathf.Min(shopItems.Length, MaterialNames.Length); material++)
            {
                ShopItemView view = shopItems[material];
                if (view == null) continue;
                int unitPrice = BasePrices[material] * 3;
                Text(view.name, MaterialNames[material]);
                Text(view.unitPrice, Format(unitPrice) + "G / 個");
                RefreshShopOffer(view.buyTenButton, view.buyTenBackground, view.buyTenLabel, 10, unitPrice);
                RefreshShopOffer(view.buyFiftyButton, view.buyFiftyBackground, view.buyFiftyLabel, 50, unitPrice);
            }
        }

        private void RefreshShopOffer(Button button, Image background, TMP_Text label, int count, int unitPrice)
        {
            int price = unitPrice * count;
            bool available = !ended && funds >= price;
            if (button) button.interactable = available;
            if (background) background.color = available ? Green : Hex("E9D5AE");
            Text(label, "×" + count + "  " + Format(price) + "G");
            if (label) label.color = available ? Cream : Muted;
        }

        private void RefreshPanels()
        {
            SetActive(warehousePanel, activePanel == 1 && !ended);
            SetActive(upgradePanel, activePanel == 2 && !ended);
            SetActive(menuPanel, activePanel == 3 && !ended);
            SetActive(shopPanel, activePanel == 4 && !ended);
            SetActive(endedPanel, ended);
            SetActive(toastRoot, !ended && activePanel == 0 && Time.time < toastUntil);
            if (navigation == null) return;
            foreach (NavigationView view in navigation)
            {
                if (view == null) continue;
                bool selected = activePanel == view.panel && activePanel != 0;
                if (view.background) view.background.color = selected ? Green : Yellow;
                if (view.label) view.label.color = selected ? Cream : Ink;
                if (view.icon) view.icon.color = selected ? Cream : Ink;
            }
        }

        private void RefreshActivity()
        {
            string ownActivity = moving ? "移動中" : collecting ? "採集中" : "待機中";
            Text(ownActivityText, ownActivity);
            if (members == null) return;
            for (int i = 0; i < members.Length; i++)
            {
                MemberView member = members[i];
                if (member == null) continue;
                string memberName = MemberName(i, member);
                string activity = i == 0 ? ownActivity :
                    string.IsNullOrEmpty(member.connectedActivity) ? "採集中" : member.connectedActivity;
                Text(member.name, memberName);
                Text(member.activity, member.connected ? activity : "切断中・再接続待ち");
                if (member.statusDot) member.statusDot.color = member.connected ? Hex("6B9B3A") : Hex("B8A586");
                if (member.worldAvatar) member.worldAvatar.alpha = member.connected ? 1f : .45f;
                Text(member.worldLabel, memberName + " " + (member.connected ? activity.Split('・')[0] : "切断中"));
            }
        }

        private static string MemberName(int index, MemberView member)
        {
            if (!string.IsNullOrEmpty(member.displayName)) return member.displayName;
            return index == 0 ? "ゆくらさん(あなた)" : index == 1 ? "たろう" : "はなこ";
        }

        private void EndRound()
        {
            if (ended) return;
            ended = true;
            remainingTime = 0;
            moving = collecting = false;
            target = -1;
            pendingResource = -1;
            if (destinationMarker) destinationMarker.gameObject.SetActive(false);
            activePanel = 0;
            ImoniRoundSession.CompleteRound(roundState, quantities, funds, levels);
            bool success = true;
            for (int i = 0; i < 3; i++) success &= Converted(i) >= RequiredAmounts[i];
            Text(endTitleText, "芋煮会の時間だ!");
            Text(endSummaryText, (success ? "必須素材を達成！" : "必須素材があと少し！") + "\n薪 " + Format(Converted(0)) + "/60　里芋 " + Format(Converted(1)) + "/20　ねぎ " + Format(Converted(2)) + "/15");
            SetActive(popRoot, false);
            RefreshAll();
        }

        public void ShowRoundResult()
        {
            if (!ended || showingRoundResult || ImoniRoundSession.LastResult == null) return;
            if (!Application.CanStreamedLevelBeLoaded("RoundResult"))
            {
                Debug.LogError("RoundResult scene is not registered in the build scene list.", this);
                return;
            }
            showingRoundResult = true;
            try
            {
                if (SceneManager.LoadSceneAsync("RoundResult", LoadSceneMode.Single) == null)
                    throw new InvalidOperationException("RoundResult scene load did not start.");
            }
            catch (Exception exception)
            {
                showingRoundResult = false;
                Debug.LogException(exception, this);
            }
        }

        private void AddHistory(string message)
        {
            string entry = "[" + Clock(Mathf.Max(0, Mathf.FloorToInt(elapsedTime))) + "] " + message;
            history.Insert(0, entry);
            if (history.Count > 3) history.RemoveAt(history.Count - 1);
            Text(toastText, entry);
            Text(historyText, string.Join("\n", history.ConvertAll(entry => "・" + entry)));
            toastUntil = Time.time + 3f;
        }

        private void ShowPop(string message, int quality)
        {
            Text(popText, message);
            if (popBackground) popBackground.color = QualityBackgrounds[quality];
            if (popText) popText.color = QualityForegrounds[quality];
            if (popRect && player && world)
                popRect.anchoredPosition = new Vector2(
                    Mathf.Clamp(player.anchoredPosition.x - 40f, 4f, Mathf.Max(4f, world.rect.width - popRect.rect.width - 4f)),
                    Mathf.Clamp(player.anchoredPosition.y + 30f, -world.rect.height + popRect.rect.height, 0f));
            popUntil = Time.time + 1.3f;
            SetActive(popRoot, true);
        }

        private bool HasSelection() => selectedInventory >= 0 && selectedInventory < quantities.Length && quantities[selectedInventory] > 0;
        private int Sellable(int index) => Mathf.Max(0, quantities[index] - protectedAmounts[index]);
        private static int Price(int index) => BasePrices[index / 3] * QualityMultipliers[index % 3];
        private int Converted(int material)
        {
            int total = 0;
            for (int q = 0; q < 3; q++) total += quantities[material * 3 + q] * QualityMultipliers[q];
            return total;
        }
        private float MoveMultiplier() => 1f + .25f * levels[0];
        private float HarvestDuration() => 1.6f / (1f + .25f * levels[1]);
        private float StreakMultiplier() => Mathf.Pow(2f, streak / 10f);
        private static float RareChance(int level) => Mathf.Min(1f, .08f * (1f + .6f * level));
        private static int UpgradePrice(int level) => (int)Math.Min(int.MaxValue, Math.Floor(150d * Math.Pow(1.5d, level) + .5d));
        private static int BonusMultiplier(int percent) => percent >= 300 ? 8 : percent >= 200 ? 4 : percent >= 150 ? 2 : percent >= 100 ? 1 : 0;
        private static string UpgradeEffect(int upgrade, int level)
        {
            switch (upgrade)
            {
                case 0: return "移動 ×" + (1f + .25f * level).ToString("F2", CultureInfo.InvariantCulture);
                case 1: return "採集 " + (1.6f / (1f + .25f * level)).ToString("F2", CultureInfo.InvariantCulture) + "秒";
                case 2: return "獲得量 ×" + (1f + .5f * level).ToString("F1", CultureInfo.InvariantCulture);
                default: return "最高品質 " + (RareChance(level) * 100f).ToString("F1", CultureInfo.InvariantCulture) + "%";
            }
        }
        private static string Clock(int seconds) => seconds / 60 + ":" + (seconds % 60).ToString("D2");
        private static string Format(int value) => value.ToString("N0", Japanese);
        private static Color Hex(string value) { ColorUtility.TryParseHtmlString("#" + value, out Color color); return color; }
        private static void Text(TMP_Text targetText, string value) { if (targetText) targetText.text = value; }
        private static void SetActive(GameObject targetObject, bool value) { if (targetObject && targetObject.activeSelf != value) targetObject.SetActive(value); }
    }
}
