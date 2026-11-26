using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Numerics;
using Imoni.Core;
using Imoni.Game.UI.Components;
using Newtonsoft.Json.Linq;
using UnityEngine;
using UnityEngine.UIElements;

namespace Imoni.Game.UI.Screens
{
    /// <summary>Reusable BottomSheets bind authoritative projections and dispatch IGameServer commands.</summary>
    public sealed class GamePanels : VisualElement
    {
        private readonly GameScreen owner;
        private readonly ScreenContext app;
        private readonly Dictionary<string, BottomSheet> sheets = new Dictionary<string, BottomSheet>();
        private readonly Dictionary<string, InventoryRow> inventoryRows = new Dictionary<string, InventoryRow>();
        private readonly Dictionary<string, ShopRow> shopRows = new Dictionary<string, ShopRow>();
        private readonly Dictionary<string, UpgradeRow> upgradeRows = new Dictionary<string, UpgradeRow>();
        private readonly Dictionary<string, MemberRow> members = new Dictionary<string, MemberRow>();
        private readonly ScrollView inventoryList;
        private readonly InventorySelectionControls selection;
        private readonly VisualElement shadow;
        private string selectedKey;
        private GameSnapshot snapshot;
        public string CurrentPanel { get; private set; } = "none";
        public bool IsOpen => CurrentPanel != "none";
        public string SelectedInventoryKey => selectedKey;
        public string SaleQuantityText => selection.QuantityInput.value;
        public event Action PanelChanged;
        private GameConfig Config => UiTheme.Config;
        private IGameServer Server => owner.Server;
        private string Actor => owner.LocalActorId;

        public GamePanels(GameScreen owner, ScreenContext app)
        {
            this.owner = owner; this.app = app; name = "GamePanels"; AddToClassList("game-panels"); pickingMode = PickingMode.Ignore;
            UiTheme.Apply(this, "UI/GamePanels"); Resources.Load<VisualTreeAsset>("UI/GamePanels").CloneTree(this);
            sheets.Add("warehouse", this.Q<BottomSheet>("WarehouseSheet")); sheets.Add("shop", this.Q<BottomSheet>("ShopSheet"));
            sheets.Add("upgrade", this.Q<BottomSheet>("UpgradeSheet")); sheets.Add("menu", this.Q<BottomSheet>("MenuSheet"));
            foreach (var entry in sheets)
            {
                entry.Value.style.height = app.Data["ui"]["panels"][entry.Key + "HeightPx"].Value<float>();
                entry.Value.CloseRequested += Close;
                entry.Value.RegisterCallback<PointerDownEvent>(evt => evt.StopPropagation());
            }
            shadow = this.Q<VisualElement>("PanelShadow");
            inventoryList = this.Q<ScrollView>("InventoryList"); selection = this.Q<InventorySelectionControls>("InventorySelection");
            selection.DecreaseProtectionRequested += () => Protect(ProtectionChange.Decrease);
            selection.IncreaseProtectionRequested += () => Protect(ProtectionChange.Increase);
            selection.ToggleProtectionRequested += () =>
            { InventoryPanelRow row = SelectedRow(); if (row != null) Protect(row.AllProtected ? ProtectionChange.None : ProtectionChange.All); };
            selection.PercentageRequested += SelectPercentage;
            selection.QuantityChanged += _ => RefreshSelection();
            selection.SellRequested += SellSelected;
            this.Q<ImoniButton>("SellAllUnprotected").clicked += SellAll;
            this.Q<ImoniButton>("ReturnHome").clicked += ReturnHome;
            this.Q<ImoniButton>("ViewHelp").clicked += () => app.OpenHelp();
            var warehouseHelp = new ImoniButton { name = "WarehouseHelp", Variant = ImoniButtonVariant.Icon, Icon = "help_question", tooltip = "倉庫の遊び方" };
            warehouseHelp.AddToClassList("game-warehouse-help");
            warehouseHelp.clicked += () => app.OpenHelp(2);
            sheets["warehouse"].HeaderActions.Add(warehouseHelp);
            this.Q<ImoniText>("ShopPriceHint").text = "購入価格は売却単価の" + Config.BuyPriceMultiplier.ToString(CultureInfo.InvariantCulture) + "倍";
            Refresh(owner.Snapshot);
        }

        public void Open(string panelName)
        {
            if (panelName == "none") { Close(); return; }
            if (!sheets.ContainsKey(panelName)) throw new ArgumentException("Unknown Game panel: " + panelName);
            CurrentPanel = panelName;
            foreach (var sheet in sheets) sheet.Value.IsOpen = sheet.Key == panelName;
            shadow.style.height = app.Data["ui"]["panels"][panelName + "HeightPx"].Value<float>() + app.Data["ui"]["panels"]["shadowHeightPx"].Value<float>();
            shadow.style.display = DisplayStyle.Flex; PanelChanged?.Invoke(); Refresh(Server.Snapshot());
        }
        public void Close()
        {
            CurrentPanel = "none"; foreach (BottomSheet sheet in sheets.Values) sheet.Close();
            shadow.style.display = DisplayStyle.None; PanelChanged?.Invoke();
        }
        public void Toggle(string panelName) { if (CurrentPanel == panelName) Close(); else Open(panelName); }

        public void Refresh(GameSnapshot state)
        {
            snapshot = state;
            if (!IsOpen) return;
            string funds = Format.Big(snapshot.Funds, Config) + Config.CurrencyUnit;
            sheets["warehouse"].FundsText = "資金 " + funds;
            sheets["shop"].FundsText = sheets["upgrade"].FundsText = "共有資金 " + funds;
            switch (CurrentPanel)
            {
                case "warehouse": RefreshInventory(); break;
                case "shop": RefreshShop(); break;
                case "upgrade": RefreshUpgrades(); break;
                case "menu": RefreshMembers(); break;
            }
        }

        private void RefreshInventory()
        {
            IReadOnlyList<InventoryPanelRow> rows = GamePanelProjection.Inventory(Config, snapshot);
            foreach (string removed in inventoryRows.Keys.Where(key => !rows.Any(row => row.Key == key)).ToArray())
            { inventoryRows[removed].RemoveFromHierarchy(); inventoryRows.Remove(removed); }
            for (int index = 0; index < rows.Count; index++)
            {
                InventoryPanelRow data = rows[index];
                if (!inventoryRows.TryGetValue(data.Key, out InventoryRow row))
                {
                    row = new InventoryRow { name = "Inventory_" + data.MaterialId + "_" + data.QualityId };
                    row.InventorySelectionRequested += key => SelectInventoryKey(key); inventoryRows.Add(data.Key, row); inventoryList.Add(row);
                }
                row.Bind(new InventoryRowView { Id = data.Key, Name = data.Name, QualityId = data.QualityId,
                    OwnedText = data.QuantityText, ProtectedText = data.ProtectedText, SellableText = data.SellableText,
                    UnitPriceText = data.UnitPriceText, Selected = data.Key == selectedKey });
                if (inventoryList.contentContainer.IndexOf(row) != index + 1) inventoryList.contentContainer.Insert(index + 1, row);
            }
            this.Q<ImoniText>("EmptyInventory").style.display = rows.Count == 0 ? DisplayStyle.Flex : DisplayStyle.None;
            this.Q<ImoniButton>("SellAllUnprotected").Disabled = !GamePanelProjection.CanOperate(snapshot, Actor) || !rows.Any(row => row.Sellable > 0);
            this.Q<OperationHistory>("WarehouseHistory").Bind(GamePanelProjection.History(Config, snapshot, ActorName));
            if (!rows.Any(row => row.Key == selectedKey)) selectedKey = null;
            RefreshSelection();
        }
        public void SelectInventory(string materialId, string qualityId) => SelectInventoryKey(materialId + "|" + qualityId);
        private void SelectInventoryKey(string key)
        {
            snapshot = Server.Snapshot(); selectedKey = key;
            InventoryPanelRow row = SelectedRow();
            selection.SetQuantityWithoutNotify(GamePanelProjection.PercentageQuantity(Config, row, app.Data["ui"]["panels"]["initialSalePercent"].Value<int>()).ToString(CultureInfo.InvariantCulture));
            Refresh(snapshot);
        }
        private InventoryPanelRow SelectedRow() => GamePanelProjection.Inventory(Config, snapshot).FirstOrDefault(row => row.Key == selectedKey);
        private void RefreshSelection()
        {
            InventoryPanelRow row = SelectedRow(); selection.style.display = row == null ? DisplayStyle.None : DisplayStyle.Flex;
            if (row == null) return;
            SaleSelection sale = GamePanelProjection.SelectSale(Config, snapshot, Actor, row.MaterialId, row.QualityId, SaleQuantityText);
            selection.Bind(new InventorySelectionView { Name = row.Name + "(" + row.QualityName + ")", ProtectedText = row.ProtectedText,
                ToggleProtectionText = row.ToggleProtectionLabel, SaleLabel = sale.Label, Error = sale.Error,
                CanOperate = GamePanelProjection.CanOperate(snapshot, Actor), CanSell = sale.CanSell });
        }
        private void SelectPercentage(int percent)
        {
            snapshot = Server.Snapshot();
            selection.SetQuantityWithoutNotify(GamePanelProjection.PercentageQuantity(Config, SelectedRow(), percent).ToString(CultureInfo.InvariantCulture)); RefreshSelection();
        }
        private void Protect(ProtectionChange change)
        {
            snapshot = Server.Snapshot(); InventoryPanelRow row = SelectedRow();
            if (row == null || !GamePanelProjection.CanOperate(snapshot, Actor)) return;
            Command(() => Server.SetProtection(Actor, row.MaterialId, row.QualityId, GamePanelProjection.ProtectionQuantity(Config, row, change)));
        }
        private void SellSelected()
        {
            snapshot = Server.Snapshot(); InventoryPanelRow row = SelectedRow(); if (row == null) return;
            SaleSelection sale = GamePanelProjection.SelectSale(Config, snapshot, Actor, row.MaterialId, row.QualityId, SaleQuantityText);
            if (!sale.CanSell) { RefreshSelection(); return; }
            Command(() => Server.Sell(Actor, row.MaterialId, row.QualityId, sale.Quantity));
            selection.SetQuantityWithoutNotify("0"); RefreshSelection();
        }
        private void SellAll()
        {
            if (!GamePanelProjection.CanOperate(Server.Snapshot(), Actor)) return;
            Command(() => Server.SellAllUnprotected(Actor)); selection.SetQuantityWithoutNotify("0"); RefreshSelection();
        }

        private void RefreshShop()
        {
            foreach (ShopPanelRow data in GamePanelProjection.Shop(Config, snapshot, Actor))
            {
                if (!shopRows.TryGetValue(data.MaterialId, out ShopRow row))
                {
                    row = new ShopRow { name = "Shop_" + data.MaterialId }; string material = data.MaterialId;
                    row.BuyRequested += key => Buy(material, key); shopRows.Add(material, row); this.Q<VisualElement>("ShopRows").Add(row);
                }
                row.Bind(new ShopRowView { Name = data.Name, UnitText = data.UnitPriceText,
                    Options = data.Offers.Select(offer => new PurchaseOptionView { Id = data.MaterialId + "_" + offer.Quantity.ToString(CultureInfo.InvariantCulture),
                        Label = offer.Label, Enabled = offer.CanBuy }).ToArray() });
            }
        }
        private void Buy(string material, string key)
        {
            ShopPanelRow row = GamePanelProjection.Shop(Config, Server.Snapshot(), Actor).First(item => item.MaterialId == material);
            ShopOffer offer = row.Offers.FirstOrDefault(item => material + "_" + item.Quantity.ToString(CultureInfo.InvariantCulture) == key);
            if (offer?.CanBuy == true) Command(() => Server.Buy(Actor, material, offer.Quantity));
        }
        private void RefreshUpgrades()
        {
            foreach (UpgradePanelRow data in GamePanelProjection.Upgrades(Config, snapshot, Actor))
            {
                if (!upgradeRows.TryGetValue(data.Id, out UpgradeRow row))
                {
                    row = new UpgradeRow { name = "UpgradeRow_" + data.Id, InlineEffects = true }; string id = data.Id;
                    row.Q<ImoniButton>("UpgradePurchase").name = "Upgrade_" + id;
                    row.UpgradeRequested += () => Upgrade(id); upgradeRows.Add(id, row); this.Q<VisualElement>("UpgradeRows").Add(row);
                }
                row.Bind(new UpgradeRowView { Name = data.Name, LevelText = "Lv." + data.Level, CurrentEffect = data.CurrentEffect + " →",
                    NextEffect = data.NextEffect, PriceText = "強化\n" + data.PriceText, Enabled = data.CanBuy });
            }
        }
        private void Upgrade(string id)
        {
            if (GamePanelProjection.Upgrades(Config, Server.Snapshot(), Actor).First(row => row.Id == id).CanBuy)
                Command(() => Server.Upgrade(Actor, id));
        }
        private void RefreshMembers()
        {
            foreach (MemberPanelView data in GamePanelProjection.Members(Config, snapshot))
            {
                if (!members.TryGetValue(data.ActorId, out MemberRow row))
                { row = new MemberRow { name = "GameMember_" + data.ActorId }; members.Add(data.ActorId, row); this.Q<VisualElement>("GameMemberRows").Add(row); }
                row.BindConnection(data.ActorId == Actor ? app.Profile.Name + "(あなた)" : ActorName(data.ActorId), data.DisplayText, data.Connected);
            }
        }
        private string ActorName(string actorId)
        {
            int index = snapshot.Players.ToList().FindIndex(player => player.Id == actorId);
            return index == 0 ? "あなた" : LobbyScreenData.Member(app, index, StatusBadgeState.Waiting).Name;
        }
        private void ReturnHome()
        {
            Server.AbortChallenge(Actor); Close(); app.Go("Home");
        }
        private void Command(Action command)
        {
            // A server can reject an intent if inventory/funds changed after the displayed snapshot.
            try { command(); }
            catch (ArgumentException) { }
            catch (InvalidOperationException) { }
            owner.RefreshSnapshot();
        }
    }
}
