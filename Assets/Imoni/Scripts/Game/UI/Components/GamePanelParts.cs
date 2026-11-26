using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json.Linq;
using UnityEngine.UIElements;

namespace Imoni.Game.UI.Components
{
    [UxmlElement]
    public partial class InventoryTableHeader : VisualElement
    {
        public InventoryTableHeader()
        {
            AddToClassList("inventory-table-header"); UiTheme.Apply(this, "UI/Components/GamePanelParts");
            foreach (string caption in new[] { "素材", "所持", "保護", "売却可", "単価" })
            {
                var label = UiTheme.Label(caption, childCount == 0 ? "inventory-table-header__material" : "inventory-table-header__number"); Add(label);
            }
        }
    }

    public sealed class InventorySelectionView
    {
        public string Name { get; set; }
        public string ProtectedText { get; set; }
        public string ToggleProtectionText { get; set; }
        public string SaleLabel { get; set; }
        public string Error { get; set; }
        public bool CanOperate { get; set; }
        public bool CanSell { get; set; }
    }

    /// <summary>Warehouse controls emit intent; quantities and permissions are supplied by Core.</summary>
    [UxmlElement]
    public partial class InventorySelectionControls : VisualElement
    {
        private readonly Label title = UiTheme.Label(string.Empty, "inventory-selection__name");
        private readonly Label protection = UiTheme.Label(string.Empty, "inventory-selection__protected");
        private readonly ImoniButton decrease, increase, toggle, sell;
        private readonly List<ImoniButton> percentages = new List<ImoniButton>();
        public InputEntry QuantityInput { get; }
        public event Action DecreaseProtectionRequested;
        public event Action IncreaseProtectionRequested;
        public event Action ToggleProtectionRequested;
        public event Action<int> PercentageRequested;
        public event Action<string> QuantityChanged;
        public event Action SellRequested;

        public InventorySelectionControls()
        {
            AddToClassList("inventory-selection"); UiTheme.Apply(this, "UI/Components/GamePanelParts");
            var heading = new VisualElement(); heading.AddToClassList("inventory-selection__heading"); heading.Add(title);
            var controls = new VisualElement(); controls.AddToClassList("inventory-selection__protection");
            controls.Add(UiTheme.Label("保護", "inventory-selection__protection-caption"));
            decrease = Button("ProtectDecrease", "−", "inventory-selection__step");
            increase = Button("ProtectIncrease", "＋", "inventory-selection__step");
            string step = UiTheme.Data?["ui"]?["panels"]?["protectionStep"]?.ToString();
            decrease.tooltip = "保護を" + step + "減らす"; increase.tooltip = "保護を" + step + "増やす";
            toggle = Button("ToggleProtection", "全部保護", "inventory-selection__toggle");
            decrease.clicked += () => DecreaseProtectionRequested?.Invoke(); increase.clicked += () => IncreaseProtectionRequested?.Invoke();
            toggle.clicked += () => ToggleProtectionRequested?.Invoke();
            controls.Add(decrease); controls.Add(protection); controls.Add(increase); controls.Add(toggle); heading.Add(controls); Add(heading);
            var quantities = new VisualElement(); quantities.AddToClassList("inventory-selection__quantities");
            foreach (int percent in UiTheme.Data["ui"]["panels"]["salePercentages"].Values<int>())
            {
                var button = Button("SalePercent" + percent, percent + "%", "inventory-selection__percent");
                button.clicked += () => PercentageRequested?.Invoke(percent); percentages.Add(button); quantities.Add(button);
            }
            QuantityInput = new InputEntry { name = "SaleQuantity", value = "0" }; QuantityInput.AddToClassList("inventory-selection__input");
            QuantityInput.RegisterValueChangedCallback(evt => QuantityChanged?.Invoke(evt.newValue)); quantities.Add(QuantityInput); Add(quantities);
            sell = new ImoniButton { name = "SaleSelected", Variant = ImoniButtonVariant.Danger };
            sell.AddToClassList("inventory-selection__sell"); sell.clicked += () => SellRequested?.Invoke(); Add(sell);
        }
        public void SetQuantityWithoutNotify(string value) => QuantityInput.SetValueWithoutNotify(value);
        public void Bind(InventorySelectionView view)
        {
            title.text = view.Name; protection.text = view.ProtectedText; toggle.text = view.ToggleProtectionText;
            decrease.Disabled = increase.Disabled = toggle.Disabled = !view.CanOperate;
            QuantityInput.SetEnabled(view.CanOperate); foreach (var button in percentages) button.Disabled = !view.CanOperate;
            sell.text = view.SaleLabel; sell.Disabled = !view.CanSell;
            QuantityInput.tooltip = view.Error ?? string.Empty;
            QuantityInput.EnableInClassList("inventory-selection__input--invalid", !string.IsNullOrEmpty(view.Error));
        }
        private static ImoniButton Button(string name, string text, string className)
        { var button = new ImoniButton { name = name, text = text, Variant = ImoniButtonVariant.Small }; button.AddToClassList(className); return button; }
    }

    [UxmlElement]
    public partial class OperationHistory : VisualElement
    {
        private readonly List<Label> lines = new List<Label>();
        public OperationHistory()
        {
            AddToClassList("operation-history"); UiTheme.Apply(this, "UI/Components/GamePanelParts");
            Add(UiTheme.Label("操作履歴", "operation-history__caption"));
        }
        public void Bind(IReadOnlyList<string> messages)
        {
            while (lines.Count < messages.Count) { var label = UiTheme.Label(string.Empty, "operation-history__line"); lines.Add(label); Add(label); }
            for (int index = 0; index < lines.Count; index++)
            { lines[index].style.display = index < messages.Count ? DisplayStyle.Flex : DisplayStyle.None; if (index < messages.Count) lines[index].text = "・" + messages[index]; }
        }
    }
}
