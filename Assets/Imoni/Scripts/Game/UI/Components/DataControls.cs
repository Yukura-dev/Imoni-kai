using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Imoni.Core;
using Newtonsoft.Json.Linq;
using UnityEngine;
using UnityEngine.UIElements;
using BigInteger = System.Numerics.BigInteger;

namespace Imoni.Game.UI.Components
{
    public enum StatCardVariant { Home, Result, GameOver }
    public enum ProgressVariant { Thick, Thin, Rainbow }
    public enum ChecklistState { Done, Active, Pending }
    public enum MaterialGoalVariant { Chip, Row, FailureRow }
    public enum RankingRankStyle { First, Podium, Normal }

    internal static class DataVisuals
    {
        internal static void Apply(VisualElement element, string className)
        { element.AddToClassList(className); UiTheme.Apply(element, "UI/Components/Data"); }
        internal static bool Amount(string text, out BigInteger value) =>
            BigInteger.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out value) && value >= 0;
        internal static string FormatAmount(BigInteger value, bool compact = false) => UiTheme.Config == null ? Format.Exact(value)
            : compact ? Format.Compact(value, UiTheme.Config) : Format.Big(value, UiTheme.Config);
        internal static string Currency => UiTheme.Data?["economy"]?["currencyUnit"]?.Value<string>() ?? "G";
        internal static long AnimationFrame => Math.Max(1, (long)UiTheme.Number("animationFrameMs"));
    }

    [UxmlElement]
    public partial class StatCard : VisualElement
    {
        private readonly Label caption = UiTheme.Label(string.Empty, "stat-card__label");
        private readonly Label number = UiTheme.Label(string.Empty, "stat-card__value");
        private readonly NumericLabelFitter numberFitter;
        private StatCardVariant variant;
        [UxmlAttribute] public string Label { get => caption.text; set => caption.text = value ?? string.Empty; }
        [UxmlAttribute] public string Value { get => number.text; set { number.text = value ?? string.Empty; numberFitter?.Refresh(); } }
        [UxmlAttribute] public StatCardVariant Variant { get => variant; set { variant = value; UiTheme.SetVariant(this, "stat-card--", value); numberFitter?.Refresh(true); } }
        public StatCard()
        {
            DataVisuals.Apply(this, "stat-card"); Add(caption); Add(number);
            numberFitter = new NumericLabelFitter(number); Variant = StatCardVariant.Home;
        }
    }

    [UxmlElement]
    public partial class ProgressBar : VisualElement
    {
        private readonly VisualElement fill = new VisualElement { pickingMode = PickingMode.Ignore };
        private readonly Image rainbow = new Image { pickingMode = PickingMode.Ignore, scaleMode = ScaleMode.StretchToFill };
        private float value;
        private ProgressVariant variant;
        private string fillColor = string.Empty;
        private Vector4 fillRadii = new Vector4(-1, -1, -1, -1);
        [UxmlAttribute] public float Value { get => value; set { this.value = float.IsNaN(value) ? 0 : Mathf.Clamp01(value); fill.style.width = Length.Percent(this.value * 100); } }
        [UxmlAttribute] public ProgressVariant Variant { get => variant; set { variant = value; UiTheme.SetVariant(this, "progress--", value); RefreshRainbow(); } }
        [UxmlAttribute] public string FillColor
        {
            get => fillColor;
            set { fillColor = value ?? string.Empty; fill.style.backgroundColor = string.IsNullOrEmpty(fillColor) ? new StyleColor(StyleKeyword.Null) : new StyleColor(UiTheme.Color(fillColor)); }
        }
        public ProgressBar()
        {
            DataVisuals.Apply(this, "progress"); AddToClassList("bar");
            fill.AddToClassList("progress__fill"); rainbow.AddToClassList("progress__rainbow");
            Add(fill); fill.Add(rainbow); Value = 0; Variant = ProgressVariant.Thick;
            fill.RegisterCallback<GeometryChangedEvent>(_ => UpdateFillGeometry());
            RegisterCallback<GeometryChangedEvent>(_ => UpdateFillGeometry());
            RegisterCallback<AttachToPanelEvent>(_ => RefreshRainbow());
        }
        private void RefreshRainbow()
        {
            rainbow.style.display = variant == ProgressVariant.Rainbow ? DisplayStyle.Flex : DisplayStyle.None;
            rainbow.image = UiTheme.Texture("fx_rainbow_scroll");
            float crop = UiTheme.Data?["imoniEffect"]?["layout"]?["gaugeTextureHalf"]?.Value<float>() ?? 1;
            rainbow.uv = new Rect(0, 1 - crop, 1, crop);
            UpdateFillGeometry();
        }
        private void UpdateFillGeometry()
        {
            float width = fill.contentRect.width, height = fill.contentRect.height;
            if (float.IsNaN(width) || float.IsNaN(height) || width < 0 || height < 0) return;
            // Match the border's inner curve. Reusing its outer radius leaves crescents at full progress.
            float limit = Mathf.Min(width, height) / 2;
            IResolvedStyle outer = resolvedStyle;
            var radii = new Vector4(
                InnerRadius(outer.borderTopLeftRadius, outer.borderTopWidth, outer.borderLeftWidth, limit),
                InnerRadius(outer.borderTopRightRadius, outer.borderTopWidth, outer.borderRightWidth, limit),
                InnerRadius(outer.borderBottomRightRadius, outer.borderBottomWidth, outer.borderRightWidth, limit),
                InnerRadius(outer.borderBottomLeftRadius, outer.borderBottomWidth, outer.borderLeftWidth, limit));
            if (radii != fillRadii)
            {
                fillRadii = radii;
                fill.style.borderTopLeftRadius = radii.x; fill.style.borderTopRightRadius = radii.y;
                fill.style.borderBottomRightRadius = radii.z; fill.style.borderBottomLeftRadius = radii.w;
            }
            // The supplied light texture is vertical. Rotate its image into the horizontal clipped bar.
            rainbow.style.width = height; rainbow.style.height = width;
            rainbow.style.left = (width - height) / 2; rainbow.style.top = (height - width) / 2;
            rainbow.style.rotate = new Rotate(-90);
        }
        private static float InnerRadius(float outerRadius, float borderA, float borderB, float limit) =>
            Mathf.Clamp(outerRadius - Mathf.Max(borderA, borderB), 0, limit);
    }

    [UxmlElement]
    public partial class ChecklistStep : VisualElement
    {
        private readonly ChecklistSymbol symbol = new ChecklistSymbol();
        private readonly Label caption = UiTheme.Label(string.Empty, "checklist-step__label");
        private readonly Label note = UiTheme.Label(string.Empty, "checklist-step__note");
        private IVisualElementScheduledItem animation;
        private ChecklistState state = ChecklistState.Pending;
        [UxmlAttribute] public ChecklistState State { get => state; set { state = value; symbol.State = value; UiTheme.SetVariant(this, "checklist-step--", value); UpdateAnimation(); } }
        [UxmlAttribute] public string Label { get => caption.text; set => caption.text = value ?? string.Empty; }
        [UxmlAttribute] public string Note { get => note.text; set => note.text = value ?? string.Empty; }
        public ChecklistStep()
        {
            DataVisuals.Apply(this, "checklist-step"); Add(symbol); Add(caption); Add(note); State = ChecklistState.Pending;
            RegisterCallback<AttachToPanelEvent>(_ => UpdateAnimation());
            RegisterCallback<DetachFromPanelEvent>(_ => animation?.Pause());
        }
        private void UpdateAnimation()
        {
            animation?.Pause(); symbol.style.rotate = new Rotate(0);
            double duration = UiTheme.Number("checklistSpinSeconds");
            if (state != ChecklistState.Active || duration <= 0 || panel == null) return;
            double start = Time.realtimeSinceStartupAsDouble;
            animation = schedule.Execute(() => symbol.style.rotate = new Rotate((float)((Time.realtimeSinceStartupAsDouble - start) / duration * 360))).Every(DataVisuals.AnimationFrame);
        }
        private sealed class ChecklistSymbol : VisualElement
        {
            private ChecklistState state;
            internal ChecklistState State { set { state = value; MarkDirtyRepaint(); } }
            internal ChecklistSymbol()
            {
                pickingMode = PickingMode.Ignore; AddToClassList("checklist-step__symbol");
                generateVisualContent += context =>
                {
                    // Geometry is the exact 24x24 SVG in Loading.dc.html; dimensions are artwork coordinates.
                    var painter = context.painter2D;
                    var center = new Vector2(12, 12);
                    if (state == ChecklistState.Done)
                    {
                        painter.fillColor = UiTheme.Color("#34421C"); painter.BeginPath(); painter.Arc(center, 11, 0, 360); painter.Fill();
                        painter.strokeColor = UiTheme.Color("#FFF3D6"); painter.lineWidth = 2.8f; painter.lineCap = LineCap.Round; painter.lineJoin = LineJoin.Round;
                        painter.BeginPath(); painter.MoveTo(new Vector2(7, 12.5f)); painter.LineTo(new Vector2(10.2f, 15.7f)); painter.LineTo(new Vector2(17, 9)); painter.Stroke();
                    }
                    else if (state == ChecklistState.Active)
                    {
                        painter.lineWidth = 3.5f; painter.strokeColor = UiTheme.Color("#E9D5AE"); painter.BeginPath(); painter.Arc(center, 9, 0, 360); painter.Stroke();
                        painter.strokeColor = UiTheme.Color("#8B2310"); painter.lineCap = LineCap.Round; painter.BeginPath(); painter.Arc(center, 9, -90, 0); painter.Stroke();
                    }
                    else
                    {
                        painter.lineWidth = 3; painter.strokeColor = UiTheme.Color("#C9A27A");
                        float arcLength = 9 * Mathf.PI * 2;
                        for (float distance = 0; distance < arcLength; distance += 7)
                        { painter.BeginPath(); painter.Arc(center, 9, distance / arcLength * 360, Math.Min(distance + 3, arcLength) / arcLength * 360); painter.Stroke(); }
                    }
                };
            }
        }
    }

    [UxmlElement]
    public partial class QualityChip : Label
    {
        private string quality = "normal";
        private bool showMultiplier;
        private bool locked;
        [UxmlAttribute] public string Quality { get => quality; set { quality = value ?? "normal"; Refresh(); } }
        [UxmlAttribute] public bool ShowMultiplier { get => showMultiplier; set { showMultiplier = value; Refresh(); } }
        [UxmlAttribute] public bool Locked { get => locked; set { locked = value; Refresh(); } }
        public QualityChip()
        {
            DataVisuals.Apply(this, "quality-chip"); AddToClassList("q"); pickingMode = PickingMode.Ignore;
            generateVisualContent += context =>
            {
                if (locked) LobbyDashedOutline.Draw(context, new Rect(0, 0, layout.width, layout.height), false,
                    resolvedStyle.borderTopLeftRadius, 2, resolvedStyle.borderTopColor);
            };
            RegisterCallback<AttachToPanelEvent>(_ => Refresh()); Refresh();
        }
        private void Refresh()
        {
            JToken visual = UiTheme.Data?["qualities"]?.FirstOrDefault(item => item["id"]?.Value<string>() == quality);
            QualityDefinition definition = UiTheme.Config?.Qualities.FirstOrDefault(item => item.Id == quality);
            text = locked ? "???" : definition?.Name ?? quality;
            if (!locked && showMultiplier && definition != null) text += " ×" + Format.Big(definition.Multiplier, UiTheme.Config);
            style.backgroundColor = locked || visual == null ? new StyleColor(StyleKeyword.Null) : new StyleColor(UiTheme.Color(visual["bg"].Value<string>()));
            style.color = locked || visual == null ? new StyleColor(StyleKeyword.Null) : new StyleColor(UiTheme.Color(visual["fg"].Value<string>()));
            EnableInClassList("quality-chip--detail", showMultiplier);
            EnableInClassList("quality-chip--locked", locked);
            MarkDirtyRepaint();
        }
    }

    [UxmlElement]
    public partial class MaterialGoalChip : VisualElement
    {
        private readonly Label caption = UiTheme.Label(string.Empty, "material-goal__name");
        private readonly Label count = UiTheme.Label(string.Empty, "material-goal__count");
        private readonly ProgressBar progress = new ProgressBar { Variant = ProgressVariant.Thin };
        private readonly NumericLabelFitter countFitter;
        private string material = "wood", current = "0", required = "1";
        private string countText;
        private bool stacked;
        private float normalCountSize, normalCaptionSize;
        private IVisualElementScheduledItem layoutJob;
        private MaterialGoalView bound;
        private MaterialGoalVariant variant;
        private string CurrentCount => variant == MaterialGoalVariant.Chip ? bound.CurrentCompactText : bound.CurrentText;
        private string RequiredCount => variant == MaterialGoalVariant.Chip ? bound.RequiredCompactText : bound.RequiredText;
        [UxmlAttribute] public string Material { get => material; set { material = value; RefreshFromAttributes(); } }
        [UxmlAttribute] public string Current { get => current; set { current = value; RefreshFromAttributes(); } }
        [UxmlAttribute] public string Required { get => required; set { required = value; RefreshFromAttributes(); } }
        [UxmlAttribute] public MaterialGoalVariant Variant { get => variant; set { variant = value; UiTheme.SetVariant(this, "material-goal--", value); countFitter?.Refresh(true); Present(); ScheduleCountLayout(); } }
        public MaterialGoalChip()
        {
            DataVisuals.Apply(this, "material-goal"); progress.AddToClassList("material-goal__bar");
            Add(caption); Add(progress); Add(count); countFitter = new NumericLabelFitter(count); Variant = MaterialGoalVariant.Chip;
            RegisterCallback<AttachToPanelEvent>(_ => { if (bound == null) RefreshFromAttributes(); ScheduleCountLayout(); });
            RegisterCallback<GeometryChangedEvent>(_ => ScheduleCountLayout());
            RegisterCallback<DetachFromPanelEvent>(_ => layoutJob?.Pause());
        }
        public void Bind(MaterialGoalView view) { bound = view ?? throw new ArgumentNullException(nameof(view)); Present(); }
        private void RefreshFromAttributes()
        {
            if (UiTheme.Config == null || !UiTheme.Config.Materials.Any(item => item.Id == material)
                || !DataVisuals.Amount(current, out BigInteger amount) || !DataVisuals.Amount(required, out BigInteger target) || target.IsZero) return;
            Bind(DisplayProjection.Goal(UiTheme.Config, material, amount, target));
        }
        private void Present()
        {
            if (bound == null) return;
            caption.text = bound.Name;
            tooltip = bound.Name + " " + bound.ExactCountsText;
            string counts = CurrentCount + "/" + RequiredCount + (variant == MaterialGoalVariant.FailureRow && !bound.Achieved ? " 不足" : string.Empty);
            if (countText != counts) { countText = counts; ApplyCountText(); ScheduleCountLayout(); }
            progress.Value = (float)bound.Progress;
            EnableInClassList("material-goal--achieved", bound.Achieved);
            EnableInClassList("material-goal--short", variant == MaterialGoalVariant.FailureRow && !bound.Achieved);
            progress.FillColor = variant == MaterialGoalVariant.FailureRow && !bound.Achieved ? "#8B2310" : "#6B9B3A";
        }

        private void ApplyCountText()
        {
            count.text = stacked && bound != null ? CurrentCount + "\n/" + RequiredCount : countText;
            countFitter.Refresh();
        }

        private void ScheduleCountLayout()
        {
            if (panel == null) return;
            layoutJob?.Pause(); layoutJob = schedule.Execute(UpdateCountLayout);
        }

        private void UpdateCountLayout()
        {
            if (bound == null) return;
            bool needsStacked = false;
            if (variant == MaterialGoalVariant.Chip && contentRect.width > 0)
            {
                float countSize = count.resolvedStyle.fontSize, captionSize = caption.resolvedStyle.fontSize;
                if (countSize <= 0 || captionSize <= 0) return;
                if (normalCountSize <= 0) normalCountSize = countSize;
                if (normalCaptionSize <= 0) normalCaptionSize = captionSize;
                float countWidth = count.MeasureTextSize(countText, 0, VisualElement.MeasureMode.Undefined, 0, VisualElement.MeasureMode.Undefined).x * normalCountSize / countSize;
                float captionWidth = caption.MeasureTextSize(caption.text, 0, VisualElement.MeasureMode.Undefined, 0, VisualElement.MeasureMode.Undefined).x * normalCaptionSize / captionSize;
                needsStacked = countWidth + captionWidth > contentRect.width;
            }
            if (stacked == needsStacked) return;
            stacked = needsStacked; EnableInClassList("material-goal--stacked", stacked);
            float minimum = (float)UiTheme.Number("numericLabelMinimumFontSize");
            caption.style.fontSize = stacked ? new StyleLength(minimum) : new StyleLength(StyleKeyword.Null);
            countFitter.SetMaximumSize(stacked ? minimum : 0); ApplyCountText();
        }
    }

    [UxmlElement]
    public partial class BonusTierChips : VisualElement
    {
        private string current = "0", required = "1";
        private bool explicitlyBound;
        [UxmlAttribute] public string Current { get => current; set { current = value; RefreshFromAttributes(); } }
        [UxmlAttribute] public string Required { get => required; set { required = value; RefreshFromAttributes(); } }
        public BonusTierChips()
        { DataVisuals.Apply(this, "bonus-tiers"); RegisterCallback<AttachToPanelEvent>(_ => { if (!explicitlyBound) RefreshFromAttributes(); }); }
        public void Bind(IReadOnlyList<BonusTierView> tiers)
        {
            if (tiers == null) throw new ArgumentNullException(nameof(tiers));
            explicitlyBound = true; Clear();
            foreach (BonusTierView tier in tiers)
            {
                var chip = UiTheme.Label(tier.ThresholdText + " " + tier.MultiplierText, "bonus-tiers__chip");
                chip.EnableInClassList("bonus-tiers__chip--reached", tier.Reached);
                chip.EnableInClassList("bonus-tiers__chip--current", tier.Current);
                if (childCount != 0) chip.AddToClassList("bonus-tiers__chip--spaced");
                Add(chip);
            }
        }
        private void RefreshFromAttributes()
        {
            if (UiTheme.Config == null || !DataVisuals.Amount(current, out BigInteger amount) || !DataVisuals.Amount(required, out BigInteger target) || target.IsZero) return;
            Bind(DisplayProjection.BonusTiers(UiTheme.Config, amount, target)); explicitlyBound = false;
        }
    }

    [UxmlElement]
    public partial class BigNumberLabel : Label
    {
        private BigInteger displayed;
        private string value = "0";
        private bool compact;
        private IVisualElementScheduledItem animation;
        [UxmlAttribute] public string Value { get => value; set { if (DataVisuals.Amount(value, out BigInteger amount)) Bind(amount); } }
        [UxmlAttribute] public bool Compact { get => compact; set { compact = value; RefreshNumber(); } }
        public BigNumberLabel()
        {
            DataVisuals.Apply(this, "big-number"); AddToClassList("num-big"); pickingMode = PickingMode.Ignore;
            RegisterCallback<AttachToPanelEvent>(_ => RefreshNumber());
            RegisterCallback<DetachFromPanelEvent>(_ => animation?.Pause()); Bind(BigInteger.Zero);
        }
        public void Bind(BigInteger amount)
        {
            animation?.Pause(); displayed = amount; value = amount.ToString(CultureInfo.InvariantCulture); RefreshNumber();
        }
        private void RefreshNumber() { text = DataVisuals.FormatAmount(displayed, compact); tooltip = Format.Exact(displayed); }
        public void CountTo(BigInteger target, float seconds = -1)
        {
            animation?.Pause();
            double duration = seconds < 0 ? UiTheme.Number("countUpSeconds") : seconds;
            if (duration <= 0 || panel == null) { Bind(target); return; }
            BigInteger from = displayed; double start = Time.realtimeSinceStartupAsDouble;
            value = target.ToString(CultureInfo.InvariantCulture);
            animation = schedule.Execute(() =>
            {
                decimal fraction = (decimal)Math.Min(1, Math.Max(0, (Time.realtimeSinceStartupAsDouble - start) / duration));
                displayed = DisplayProjection.Interpolate(from, target, fraction); RefreshNumber();
                if (fraction == 1) animation.Pause();
            }).Every(DataVisuals.AnimationFrame);
        }
    }

    [UxmlElement]
    public partial class FundsPill : VisualElement
    {
        private readonly VisualElement coin = UiTheme.Icon("icon_coin", "funds-pill__coin");
        private readonly BigNumberLabel amount = new BigNumberLabel { Compact = true };
        private readonly Label unit = UiTheme.Label(string.Empty, "funds-pill__unit");
        [UxmlAttribute] public string Value { get => amount.Value; set { amount.Value = value; RefreshTooltip(); } }
        public FundsPill()
        {
            DataVisuals.Apply(this, "funds-pill"); amount.AddToClassList("funds-pill__amount");
            Add(coin); Add(amount); Add(unit); unit.text = DataVisuals.Currency;
            RegisterCallback<AttachToPanelEvent>(_ => { coin.style.backgroundImage = UiTheme.Texture("icon_coin"); unit.text = DataVisuals.Currency; RefreshTooltip(); });
        }
        public void Bind(BigInteger value) { amount.Bind(value); RefreshTooltip(); }
        private void RefreshTooltip() { tooltip = amount.tooltip + DataVisuals.Currency; }
    }

    [UxmlElement]
    public partial class BottomSheet : VisualElement
    {
        private readonly VisualElement body = new VisualElement();
        private readonly Label title = UiTheme.Label(string.Empty, "bottom-sheet__title");
        private readonly Label funds = UiTheme.Label(string.Empty, "bottom-sheet__funds");
        private bool open = true, inline;
        public event Action CloseRequested;
        public VisualElement HeaderActions { get; } = new VisualElement();
        public override VisualElement contentContainer => body;
        [UxmlAttribute] public string Title { get => title.text; set => title.text = value ?? string.Empty; }
        [UxmlAttribute] public string FundsText { get => funds.text; set { funds.text = value ?? string.Empty; funds.style.display = string.IsNullOrEmpty(value) ? DisplayStyle.None : DisplayStyle.Flex; } }
        [UxmlAttribute] public bool IsOpen { get => open; set { open = value; style.display = value ? DisplayStyle.Flex : DisplayStyle.None; } }
        [UxmlAttribute] public bool Inline { get => inline; set { inline = value; EnableInClassList("bottom-sheet--inline", value); } }
        public BottomSheet()
        {
            DataVisuals.Apply(this, "bottom-sheet"); AddToClassList("sheet");
            var header = new VisualElement(); header.AddToClassList("bottom-sheet__header");
            var close = new ImoniButton { Variant = ImoniButtonVariant.Icon, Icon = "icon_close", tooltip = "閉じる" };
            close.AddToClassList("bottom-sheet__close"); close.clicked += () => { CloseRequested?.Invoke(); Close(); };
            HeaderActions.AddToClassList("bottom-sheet__header-actions");
            header.Add(title); header.Add(HeaderActions); header.Add(funds); header.Add(close); body.AddToClassList("bottom-sheet__content");
            hierarchy.Add(header); hierarchy.Add(body); FundsText = string.Empty;
        }
        public void Open() => IsOpen = true;
        public void Close() => IsOpen = false;
    }

    public sealed class InventoryRowView
    {
        public string Id { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string QualityId { get; set; } = "normal";
        public string OwnedText { get; set; } = string.Empty;
        public string ProtectedText { get; set; } = string.Empty;
        public string SellableText { get; set; } = string.Empty;
        public string UnitPriceText { get; set; } = string.Empty;
        public bool Selected { get; set; }
    }
    public sealed class PurchaseOptionView
    {
        public string Id { get; set; } = string.Empty;
        public string Label { get; set; } = string.Empty;
        public string QuantityText { get; set; } = string.Empty;
        public string PriceText { get; set; } = string.Empty;
        public bool Enabled { get; set; } = true;
    }
    public sealed class ShopRowView
    {
        public string Name { get; set; } = string.Empty;
        public string UnitText { get; set; } = string.Empty;
        public IReadOnlyList<PurchaseOptionView> Options { get; set; } = Array.Empty<PurchaseOptionView>();
    }
    public sealed class UpgradeRowView
    {
        public string Name { get; set; } = string.Empty;
        public string LevelText { get; set; } = string.Empty;
        public string CurrentEffect { get; set; } = string.Empty;
        public string NextEffect { get; set; } = string.Empty;
        public string PriceText { get; set; } = string.Empty;
        public bool Enabled { get; set; } = true;
    }
    public sealed class RankingRowView
    {
        public string RankText { get; set; } = string.Empty;
        public string Team { get; set; } = string.Empty;
        public string Detail { get; set; } = string.Empty;
        public string ScoreText { get; set; } = string.Empty;
        public RankingRankStyle Style { get; set; } = RankingRankStyle.Normal;
        public bool Alternating { get; set; }
    }

    [UxmlElement]
    public partial class InventoryRow : Button
    {
        private readonly Label caption = UiTheme.Label(string.Empty, "inventory-row__name");
        private readonly QualityChip quality = new QualityChip();
        private readonly Label owned = UiTheme.Label(string.Empty, "inventory-row__number");
        private readonly Label locked = UiTheme.Label(string.Empty, "inventory-row__number");
        private readonly Label sellable = UiTheme.Label(string.Empty, "inventory-row__number");
        private readonly Label price = UiTheme.Label(string.Empty, "inventory-row__number");
        private string id = string.Empty;
        public event Action<string> InventorySelectionRequested;
        [UxmlAttribute] public bool Selected { get => ClassListContains("inventory-row--selected"); set => EnableInClassList("inventory-row--selected", value); }
        public InventoryRow()
        {
            DataVisuals.Apply(this, "inventory-row");
            var description = new VisualElement(); description.AddToClassList("inventory-row__description"); description.pickingMode = PickingMode.Ignore;
            description.Add(caption); description.Add(quality); locked.AddToClassList("inventory-row__protected");
            hierarchy.Add(description); hierarchy.Add(owned); hierarchy.Add(locked); hierarchy.Add(sellable); hierarchy.Add(price);
            clicked += () => InventorySelectionRequested?.Invoke(id);
        }
        public void Bind(InventoryRowView view)
        {
            if (view == null) throw new ArgumentNullException(nameof(view));
            id = view.Id; caption.text = view.Name; quality.Quality = view.QualityId; owned.text = view.OwnedText;
            locked.text = view.ProtectedText; sellable.text = view.SellableText; price.text = view.UnitPriceText; Selected = view.Selected;
        }
    }

    [UxmlElement]
    public partial class ShopRow : VisualElement
    {
        private readonly Label caption = UiTheme.Label(string.Empty, "purchase-row__name");
        private readonly Label unit = UiTheme.Label(string.Empty, "purchase-row__unit");
        private readonly VisualElement options = new VisualElement();
        private readonly Dictionary<string, ImoniButton> optionButtons = new Dictionary<string, ImoniButton>();
        public event Action<string> BuyRequested;
        public ShopRow()
        {
            DataVisuals.Apply(this, "purchase-row"); AddToClassList("shop-row");
            var description = new VisualElement(); description.AddToClassList("purchase-row__description"); description.Add(caption); description.Add(unit);
            options.AddToClassList("shop-row__options"); Add(description); Add(options);
        }
        public void Bind(ShopRowView view)
        {
            if (view == null) throw new ArgumentNullException(nameof(view));
            caption.text = view.Name; unit.text = view.UnitText;
            foreach (string removed in optionButtons.Keys.Where(id => !view.Options.Any(option => option.Id == id)).ToArray())
            { optionButtons[removed].RemoveFromHierarchy(); optionButtons.Remove(removed); }
            foreach (PurchaseOptionView option in view.Options)
            {
                if (!optionButtons.TryGetValue(option.Id, out ImoniButton button))
                {
                    button = new ImoniButton { Variant = ImoniButtonVariant.Small, name = "Buy_" + option.Id };
                    button.AddToClassList("purchase-row__button"); string optionId = option.Id;
                    button.clicked += () => BuyRequested?.Invoke(optionId); options.Add(button); optionButtons.Add(option.Id, button);
                }
                button.text = string.IsNullOrEmpty(option.Label) ? "×" + option.QuantityText + "  " + option.PriceText : option.Label;
                button.Disabled = !option.Enabled;
            }
        }
    }

    [UxmlElement]
    public partial class UpgradeRow : VisualElement
    {
        private readonly Label caption = UiTheme.Label(string.Empty, "purchase-row__name");
        private readonly Label level = UiTheme.Label(string.Empty, "upgrade-row__level");
        private readonly Label current = UiTheme.Label(string.Empty, "upgrade-row__current");
        private readonly Label next = UiTheme.Label(string.Empty, "upgrade-row__next");
        private readonly ImoniButton buy = new ImoniButton { Variant = ImoniButtonVariant.Small };
        [UxmlAttribute] public bool InlineEffects { get => ClassListContains("upgrade-row--inline-effects"); set => EnableInClassList("upgrade-row--inline-effects", value); }
        public event Action UpgradeRequested;
        public UpgradeRow()
        {
            DataVisuals.Apply(this, "purchase-row"); AddToClassList("upgrade-row");
            var description = new VisualElement(); description.AddToClassList("purchase-row__description");
            var heading = new VisualElement(); heading.AddToClassList("upgrade-row__heading"); heading.Add(caption); heading.Add(level);
            var effects = new VisualElement(); effects.AddToClassList("upgrade-row__effects"); effects.Add(current); effects.Add(next);
            description.Add(heading); description.Add(effects);
            buy.name = "UpgradePurchase";
            buy.AddToClassList("purchase-row__button"); Add(description); Add(buy); buy.clicked += () => UpgradeRequested?.Invoke();
        }
        public void Bind(UpgradeRowView view)
        {
            if (view == null) throw new ArgumentNullException(nameof(view));
            caption.text = view.Name; level.text = view.LevelText; current.text = view.CurrentEffect; next.text = view.NextEffect;
            buy.text = view.PriceText; buy.Disabled = !view.Enabled;
        }
    }

    [UxmlElement]
    public partial class RankingRow : VisualElement
    {
        private readonly Label rank = UiTheme.Label(string.Empty, "ranking-row__rank");
        private readonly Label team = UiTheme.Label(string.Empty, "ranking-row__team");
        private readonly Label detail = UiTheme.Label(string.Empty, "ranking-row__detail");
        private readonly Label score = UiTheme.Label(string.Empty, "ranking-row__score");
        private RankingRankStyle rankStyle;
        [UxmlAttribute] public string RankText { get => rank.text; set => rank.text = value; }
        [UxmlAttribute] public string Team { get => team.text; set => team.text = value; }
        [UxmlAttribute] public string Detail { get => detail.text; set => detail.text = value; }
        [UxmlAttribute] public string ScoreText { get => score.text; set => score.text = value; }
        [UxmlAttribute] public RankingRankStyle RankStyle { get => rankStyle; set { rankStyle = value; UiTheme.SetVariant(this, "ranking-row--rank-", value); } }
        [UxmlAttribute] public bool Alternating { get => ClassListContains("ranking-row--alternating"); set => EnableInClassList("ranking-row--alternating", value); }
        public RankingRow()
        {
            DataVisuals.Apply(this, "ranking-row"); var info = new VisualElement(); info.AddToClassList("ranking-row__info");
            info.Add(team); info.Add(detail); Add(rank); Add(info); Add(score); RankStyle = RankingRankStyle.Normal;
        }
        public void Bind(RankingRowView view)
        {
            if (view == null) throw new ArgumentNullException(nameof(view));
            RankText = view.RankText; Team = view.Team; Detail = view.Detail; ScoreText = view.ScoreText; RankStyle = view.Style; Alternating = view.Alternating;
        }
    }
}
