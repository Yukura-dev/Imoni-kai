using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace Imoni.Game.UI.Components
{
    public enum ImoniButtonVariant { Primary, Danger, Yellow, Outline, Small, Icon, Action }
    public enum CardVariant { Cream, White, Dark, Dashed, Shadow }
    public enum TabSelection { Home, Ranking, Member }
    public enum SegmentedVariant { Pill, Tabs, Seed }
    public enum InviteCodeVariant { Compact, Large }

    internal static class NavigationVisuals
    {
        internal static void Variant<T>(VisualElement element, string prefix, T value) where T : Enum
        {
            foreach (T option in Enum.GetValues(typeof(T)))
                element.EnableInClassList(prefix + option.ToString().ToLowerInvariant(), EqualityComparer<T>.Default.Equals(option, value));
        }

        internal static VisualElement Icon(string key, string className)
        {
            var icon = new VisualElement { pickingMode = PickingMode.Ignore };
            icon.AddToClassList(className);
            icon.style.backgroundImage = UiTheme.Texture(key);
            return icon;
        }

        internal static Label Label(string text, string className)
        {
            var label = new Label(text) { pickingMode = PickingMode.Ignore };
            label.AddToClassList(className);
            return label;
        }
    }

    /// <summary>Shared button; clicks carry intent and never execute game rules.</summary>
    [UxmlElement]
    public partial class ImoniButton : Button
    {
        private readonly VisualElement iconElement;
        private ImoniButtonVariant variant;
        private string icon = string.Empty;
        private bool selected;

        [UxmlAttribute]
        public ImoniButtonVariant Variant
        {
            get => variant;
            set { variant = value; NavigationVisuals.Variant(this, "btn-", value); RefreshIcon(); }
        }

        [UxmlAttribute]
        public string Icon
        {
            get => icon;
            set { icon = value ?? string.Empty; RefreshIcon(); }
        }

        [UxmlAttribute]
        public bool Disabled { get => !enabledSelf; set => SetEnabled(!value); }

        [UxmlAttribute]
        public bool Selected
        {
            get => selected;
            set { selected = value; EnableInClassList("btn-action--on", value); }
        }

        public ImoniButton()
        {
            AddToClassList("btn");
            AddToClassList("imoni-button");
            iconElement = NavigationVisuals.Icon(string.Empty, "imoni-button__icon");
            hierarchy.Add(iconElement);
            UiTheme.Apply(this, "UI/Components/Navigation");
            Variant = ImoniButtonVariant.Primary;
        }

        private void RefreshIcon()
        {
            if (iconElement == null) return;
            bool visible = !string.IsNullOrEmpty(icon);
            iconElement.style.display = visible ? DisplayStyle.Flex : DisplayStyle.None;
            iconElement.style.backgroundImage = visible ? UiTheme.Texture(icon) : null;
            EnableInClassList("imoni-button--has-icon", visible);
        }
    }

    [UxmlElement]
    public partial class Card : VisualElement
    {
        private readonly Label headerLabel;
        private readonly VisualElement body;
        private CardVariant variant;
        public override VisualElement contentContainer => body ?? this;

        [UxmlAttribute]
        public CardVariant Variant
        {
            get => variant;
            set { variant = value; NavigationVisuals.Variant(this, "card--", value); }
        }

        [UxmlAttribute]
        public string Header
        {
            get => headerLabel.text;
            set
            {
                headerLabel.text = value ?? string.Empty;
                headerLabel.style.display = string.IsNullOrEmpty(value) ? DisplayStyle.None : DisplayStyle.Flex;
            }
        }

        public Card()
        {
            AddToClassList("card");
            AddToClassList("imoni-card");
            headerLabel = NavigationVisuals.Label(string.Empty, "card-header");
            body = new VisualElement { name = "card-content" };
            body.AddToClassList("imoni-card__content");
            hierarchy.Add(headerLabel);
            hierarchy.Add(body);
            UiTheme.Apply(this, "UI/Components/Navigation");
            Header = string.Empty;
            Variant = CardVariant.Cream;
        }
    }

    [UxmlElement]
    public partial class ScreenHeader : VisualElement
    {
        private readonly Button back;
        private readonly Label backLabel;
        private readonly Label titleLabel;
        public VisualElement RightContent { get; }
        public event Action BackRequested;

        [UxmlAttribute]
        public string Title
        {
            get => titleLabel.text;
            set { titleLabel.text = value ?? string.Empty; titleLabel.style.display = string.IsNullOrEmpty(value) ? DisplayStyle.None : DisplayStyle.Flex; }
        }

        [UxmlAttribute]
        public string BackText
        {
            get => backLabel.text;
            set { backLabel.text = value ?? string.Empty; backLabel.style.display = string.IsNullOrEmpty(value) ? DisplayStyle.None : DisplayStyle.Flex; }
        }

        public ScreenHeader()
        {
            AddToClassList("screen-header");
            back = new Button(() => BackRequested?.Invoke()) { tooltip = "戻る" };
            back.AddToClassList("screen-header__back");
            back.Add(NavigationVisuals.Icon("icon_back", "screen-header__icon"));
            backLabel = NavigationVisuals.Label(string.Empty, "screen-header__back-label");
            back.Add(backLabel);
            titleLabel = NavigationVisuals.Label(string.Empty, "screen-header__title");
            RightContent = new VisualElement { name = "header-right" };
            RightContent.AddToClassList("screen-header__right");
            Add(back); Add(titleLabel); Add(RightContent);
            UiTheme.Apply(this, "UI/Components/Navigation");
            BackText = string.Empty;
            Title = string.Empty;
        }
    }

    [UxmlElement]
    public partial class StepTitle : VisualElement
    {
        private readonly Label titleLabel;
        private readonly Label stepLabel;
        private int step = 1;
        private int count = 3;
        private bool stacked;

        [UxmlAttribute] public string Title { get => titleLabel.text; set => titleLabel.text = value ?? string.Empty; }
        [UxmlAttribute] public int Step { get => step; set { step = value; RefreshStep(); } }
        [UxmlAttribute] public int Count { get => count; set { count = value; RefreshStep(); } }
        [UxmlAttribute]
        public bool Stacked
        {
            get => stacked;
            set
            {
                stacked = value;
                EnableInClassList("step-title--stacked", value);
                if (stepLabel == null) return;
                stepLabel.RemoveFromHierarchy();
                Insert(value ? 0 : 1, stepLabel);
            }
        }

        public StepTitle()
        {
            AddToClassList("step-title");
            titleLabel = NavigationVisuals.Label(string.Empty, "step-title__title");
            stepLabel = NavigationVisuals.Label(string.Empty, "step-title__step");
            Add(titleLabel); Add(stepLabel);
            UiTheme.Apply(this, "UI/Components/Navigation");
            RefreshStep();
        }

        private void RefreshStep()
        {
            if (stepLabel != null) stepLabel.text = "(" + step + "/" + count + ")";
        }
    }

    [UxmlElement]
    public partial class PagerDots : VisualElement
    {
        private int count = 3;
        private int index;
        [UxmlAttribute] public int Count { get => count; set { count = Math.Max(0, value); Rebuild(); } }
        [UxmlAttribute] public int Index { get => index; set { index = value; RefreshSelection(); } }

        public PagerDots()
        {
            AddToClassList("pager-dots");
            pickingMode = PickingMode.Ignore;
            UiTheme.Apply(this, "UI/Components/Navigation");
            Rebuild();
        }

        private void Rebuild()
        {
            Clear();
            for (int dotIndex = 0; dotIndex < count; dotIndex++)
            {
                var dot = new VisualElement { pickingMode = PickingMode.Ignore };
                dot.AddToClassList("pager-dots__dot");
                if (dotIndex != 0) dot.AddToClassList("pager-dots__dot--spaced");
                Add(dot);
            }
            RefreshSelection();
        }

        private void RefreshSelection()
        {
            for (int dotIndex = 0; dotIndex < childCount; dotIndex++)
                this[dotIndex].EnableInClassList("pager-dots__dot--active", dotIndex == index);
        }
    }

    [UxmlElement]
    public partial class TabBar : VisualElement
    {
        private readonly Dictionary<TabSelection, Button> buttons = new Dictionary<TabSelection, Button>();
        private TabSelection selected;
        public event Action<string> NavigateRequested;
        [UxmlAttribute] public TabSelection Selected { get => selected; set { selected = value; Refresh(); } }

        public TabBar()
        {
            AddToClassList("tabbar");
            AddToClassList("imoni-tabbar");
            AddTab(TabSelection.Home, "ホーム", "icon_home", true);
            AddTab(TabSelection.Ranking, "ランキング", "icon_ranking", false);
            AddTab(TabSelection.Member, "会員証", "icon_member_card", false);
            UiTheme.Apply(this, "UI/Components/Navigation");
            Refresh();
        }

        private void AddTab(TabSelection destination, string label, string icon, bool first)
        {
            var button = new Button(() => NavigateRequested?.Invoke(destination.ToString())) { name = destination.ToString() };
            button.AddToClassList("tabbar__item");
            if (first) button.AddToClassList("tabbar__item--first");
            button.Add(NavigationVisuals.Icon(icon, "tabbar__icon"));
            button.Add(NavigationVisuals.Label(label, "tabbar__label"));
            buttons.Add(destination, button);
            Add(button);
        }

        private void Refresh()
        {
            foreach (var pair in buttons) pair.Value.EnableInClassList("tabbar__item--on", pair.Key == selected);
        }
    }

    [UxmlElement]
    public partial class SegmentedControl : VisualElement
    {
        private string options = string.Empty;
        private int selected;
        private SegmentedVariant variant;
        public event Action<int> Changed;
        [UxmlAttribute] public string Options { get => options; set { options = value ?? string.Empty; Rebuild(); } }
        [UxmlAttribute] public int Selected { get => selected; set { selected = value; RefreshSelection(); } }
        [UxmlAttribute] public SegmentedVariant Variant { get => variant; set { variant = value; NavigationVisuals.Variant(this, "segmented--", value); } }

        public SegmentedControl()
        {
            AddToClassList("segmented");
            UiTheme.Apply(this, "UI/Components/Navigation");
            Variant = SegmentedVariant.Pill;
        }

        public void Bind(IReadOnlyList<string> labels, int selectedIndex)
        {
            Options = labels == null ? string.Empty : string.Join("|", labels);
            Selected = selectedIndex;
        }

        private void Rebuild()
        {
            Clear();
            if (string.IsNullOrEmpty(options)) return;
            string[] labels = options.Split('|');
            for (int itemIndex = 0; itemIndex < labels.Length; itemIndex++)
            {
                int captured = itemIndex;
                var button = new Button(() => { Selected = captured; Changed?.Invoke(captured); }) { text = labels[itemIndex] };
                button.AddToClassList("segmented__option");
                if (itemIndex != 0) button.AddToClassList("segmented__option--spaced");
                Add(button);
            }
            RefreshSelection();
        }

        private void RefreshSelection()
        {
            for (int itemIndex = 0; itemIndex < childCount; itemIndex++)
                this[itemIndex].EnableInClassList("segmented__option--selected", itemIndex == selected);
        }
    }

    [UxmlElement]
    public partial class InviteCodeCard : VisualElement
    {
        private readonly Label codeLabel;
        private readonly Label compactCode;
        private readonly Label teamLabel;
        private readonly Label compactTeam;
        private readonly ImoniButton copyButton;
        private readonly VisualElement large;
        private readonly VisualElement compact;
        private InviteCodeVariant variant;
        private IVisualElementScheduledItem copiedReset;
        private bool copied;
        public event Action CopyRequested;
        public event Action ShareRequested;

        [UxmlAttribute]
        public string Code
        {
            get => codeLabel.text;
            set { codeLabel.text = value ?? string.Empty; compactCode.text = value ?? string.Empty; }
        }
        [UxmlAttribute]
        public string TeamName
        {
            get => compactTeam.text;
            set { compactTeam.text = value ?? string.Empty; teamLabel.text = "チーム「" + (value ?? string.Empty) + "」"; }
        }
        [UxmlAttribute]
        public InviteCodeVariant Variant
        {
            get => variant;
            set
            {
                variant = value;
                NavigationVisuals.Variant(this, "invite-code--", value);
                compact.style.display = value == InviteCodeVariant.Compact ? DisplayStyle.Flex : DisplayStyle.None;
                large.style.display = value == InviteCodeVariant.Large ? DisplayStyle.Flex : DisplayStyle.None;
            }
        }

        public InviteCodeCard()
        {
            AddToClassList("invite-code");
            large = new VisualElement();
            var heading = new VisualElement(); heading.AddToClassList("invite-code__heading");
            heading.Add(NavigationVisuals.Label("招待コード", "invite-code__heading-label"));
            teamLabel = NavigationVisuals.Label(string.Empty, "invite-code__team-caption"); heading.Add(teamLabel);
            codeLabel = NavigationVisuals.Label(string.Empty, "invite-code__number");
            var actions = new VisualElement(); actions.AddToClassList("invite-code__actions");
            copyButton = new ImoniButton { Variant = ImoniButtonVariant.Yellow, text = "コードをコピー" };
            copyButton.AddToClassList("invite-code__copy");
            copyButton.clicked += () => CopyRequested?.Invoke();
            var share = new ImoniButton { Variant = ImoniButtonVariant.Outline, text = "共有する" };
            share.AddToClassList("invite-code__share");
            share.clicked += () => ShareRequested?.Invoke();
            actions.Add(copyButton); actions.Add(share);
            large.Add(heading); large.Add(codeLabel); large.Add(actions);
            compact = new VisualElement(); compact.AddToClassList("invite-code__compact");
            var teamColumn = new VisualElement();
            teamColumn.Add(NavigationVisuals.Label("チーム", "invite-code__compact-caption"));
            compactTeam = NavigationVisuals.Label(string.Empty, "invite-code__compact-team"); teamColumn.Add(compactTeam);
            var codeColumn = new VisualElement(); codeColumn.AddToClassList("invite-code__compact-code-column");
            codeColumn.Add(NavigationVisuals.Label("招待コード", "invite-code__compact-caption"));
            compactCode = NavigationVisuals.Label(string.Empty, "invite-code__compact-number"); codeColumn.Add(compactCode);
            compact.Add(teamColumn); compact.Add(codeColumn);
            Add(large); Add(compact);
            UiTheme.Apply(this, "UI/Components/Navigation");
            Variant = InviteCodeVariant.Large;
            Code = string.Empty;
            TeamName = string.Empty;
            RegisterCallback<AttachToPanelEvent>(_ => StartCopiedReset());
            RegisterCallback<DetachFromPanelEvent>(_ => copiedReset?.Pause());
        }

        public void ShowCopied()
        {
            copiedReset?.Pause();
            copied = true;
            copyButton.text = "コピーしました!";
            StartCopiedReset();
        }

        private void StartCopiedReset()
        {
            double seconds = UiTheme.Number("inviteCopiedSeconds");
            if (!copied || panel == null || seconds <= 0) return;
            copiedReset?.Pause();
            copiedReset = schedule.Execute(() =>
            {
                copied = false;
                copyButton.text = "コードをコピー";
            }).StartingIn((long)(seconds * 1000));
        }
    }

    [UxmlElement]
    public partial class Toast : VisualElement
    {
        private readonly Label message;
        private IVisualElementScheduledItem hide;
        private double visibleSeconds;
        private bool transient;
        [UxmlAttribute] public string Text { get => message.text; set => message.text = value ?? string.Empty; }
        public Toast()
        {
            AddToClassList("imoni-toast");
            var marker = new VisualElement { pickingMode = PickingMode.Ignore }; marker.AddToClassList("imoni-toast__marker");
            message = NavigationVisuals.Label(string.Empty, "imoni-toast__text");
            Add(marker); Add(message);
            UiTheme.Apply(this, "UI/Components/Navigation");
            RegisterCallback<AttachToPanelEvent>(_ => StartHideTimer());
            RegisterCallback<DetachFromPanelEvent>(_ => hide?.Pause());
        }

        public void Show(string text, double seconds)
        {
            hide?.Pause();
            Text = text;
            style.display = DisplayStyle.Flex;
            visibleSeconds = Math.Max(0, seconds);
            transient = true;
            StartHideTimer();
        }

        private void StartHideTimer()
        {
            if (!transient || panel == null) return;
            hide?.Pause();
            hide = schedule.Execute(() =>
            {
                transient = false;
                style.display = DisplayStyle.None;
            }).StartingIn((long)(visibleSeconds * 1000));
        }
    }

    [UxmlElement]
    public partial class ErrorMessage : VisualElement
    {
        private readonly Label message;
        [UxmlAttribute] public string Text { get => message.text; set => message.text = value ?? string.Empty; }
        public ErrorMessage()
        {
            AddToClassList("error-message");
            message = NavigationVisuals.Label(string.Empty, "error-message__text");
            Add(message);
            UiTheme.Apply(this, "UI/Components/Navigation");
        }
    }
}
