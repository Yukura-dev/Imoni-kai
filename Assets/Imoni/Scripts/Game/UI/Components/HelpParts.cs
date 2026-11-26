using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace Imoni.Game.UI.Components
{
    /// <summary>Help controls only render the supplied content and emit navigation intents.</summary>
    [UxmlElement]
    public partial class HelpTabs : VisualElement
    {
        private readonly List<ImoniButton> buttons = new List<ImoniButton>();
        private int selected;
        public event Action<int> Changed;
        [UxmlAttribute] public int Selected
        {
            get => selected;
            set
            {
                selected = value;
                for (int index = 0; index < buttons.Count; index++)
                    buttons[index].EnableInClassList("help-tabs__tab--selected", index == selected);
            }
        }
        public HelpTabs() { AddToClassList("help-tabs"); UiTheme.Apply(this, "UI/Components/HelpParts"); }
        public void Bind(IReadOnlyList<HelpSection> sections, int selectedIndex)
        {
            Clear(); buttons.Clear();
            for (int index = 0; index < sections.Count; index++)
            {
                int captured = index;
                var button = new ImoniButton { name = "HelpTab_" + index, Variant = ImoniButtonVariant.Icon, tooltip = sections[index].Tab };
                button.AddToClassList("help-tabs__tab");
                if (index != 0) button.AddToClassList("help-tabs__tab--spaced");
                button.Add(UiTheme.Icon(sections[index].Icon, "help-tabs__icon"));
                button.Add(UiTheme.Label(sections[index].Tab, "help-tabs__label"));
                button.clicked += () => Changed?.Invoke(captured);
                Add(button); buttons.Add(button);
            }
            Selected = selectedIndex;
        }
    }

    [UxmlElement]
    public partial class HelpStepRow : VisualElement
    {
        private readonly VisualElement symbol = new VisualElement();
        private readonly VisualElement icon = UiTheme.Icon(string.Empty, "help-step__icon");
        private readonly Label number = UiTheme.Label(string.Empty, "help-step__number");
        private readonly Label title = UiTheme.Label(string.Empty, "help-step__title");
        private readonly Label description = UiTheme.Label(string.Empty, "help-step__description");
        private readonly VisualElement qualities = new VisualElement();
        private readonly Label note = UiTheme.Label(string.Empty, "help-step__note");
        public HelpStepRow()
        {
            AddToClassList("help-step"); UiTheme.Apply(this, "UI/Components/HelpParts");
            symbol.AddToClassList("help-step__symbol"); symbol.Add(icon); symbol.Add(number);
            var text = new VisualElement(); text.AddToClassList("help-step__text");
            qualities.AddToClassList("help-step__qualities");
            text.Add(title); text.Add(description); text.Add(qualities); text.Add(note); Add(symbol); Add(text);
        }
        public void Bind(HelpStep step, int ordinal, IReadOnlyList<HelpQualityChip> chips)
        {
            number.text = ordinal.ToString(); title.text = step.Title;
            // UI Toolkit supports the rich-text line-height tag, unlike a USS line-height property.
            // 13px × 1.6 is the supplied mock's paragraph spacing.
            description.text = "<line-height=20.8>" + step.Desc + "</line-height>";
            icon.style.backgroundImage = UiTheme.Texture(step.Icon);
            UiTheme.SetVariant(symbol, "help-step__symbol--", step.Color);
            note.text = step.Note ?? string.Empty;
            note.style.display = string.IsNullOrEmpty(step.Note) ? DisplayStyle.None : DisplayStyle.Flex;
            qualities.Clear(); qualities.style.display = step.Chips ? DisplayStyle.Flex : DisplayStyle.None;
            if (!step.Chips) return;
            foreach (HelpQualityChip item in chips)
            {
                var chip = new QualityChip { Quality = item.Quality, ShowMultiplier = true, Locked = item.Locked };
                chip.AddToClassList("help-step__quality"); qualities.Add(chip);
            }
        }
    }

    [UxmlElement]
    public partial class HelpPageCard : VisualElement
    {
        private readonly Label heading = UiTheme.Label(string.Empty, "help-page__title");
        private readonly Label page = UiTheme.Label(string.Empty, "help-page__page");
        private readonly Label lead = UiTheme.Label(string.Empty, "help-page__lead");
        private readonly VisualElement steps = new VisualElement();
        public ScrollView BodyScroll { get; } = new ScrollView(ScrollViewMode.Vertical);
        public HelpPageCard()
        {
            AddToClassList("help-page"); UiTheme.Apply(this, "UI/Components/HelpParts");
            var card = new Card(); card.AddToClassList("help-page__card");
            var header = new VisualElement(); header.AddToClassList("help-page__header"); header.Add(heading); header.Add(page);
            BodyScroll.name = "HelpBodyScroll"; BodyScroll.AddToClassList("help-page__scroll");
            BodyScroll.horizontalScrollerVisibility = ScrollerVisibility.Hidden;
            BodyScroll.verticalScrollerVisibility = ScrollerVisibility.Hidden;
            BodyScroll.contentContainer.usageHints &= ~UsageHints.GroupTransform;
            steps.AddToClassList("help-page__steps"); BodyScroll.Add(lead); BodyScroll.Add(steps);
            card.Add(header); card.Add(BodyScroll); Add(card);
        }
        public void Bind(HelpSection section, int index, int count, IReadOnlyList<HelpQualityChip> chips)
        {
            heading.text = section.Title; page.text = (index + 1) + " / " + count;
            lead.text = "<line-height=20.8>" + section.Lead + "</line-height>";
            steps.Clear();
            for (int stepIndex = 0; stepIndex < section.Steps.Length; stepIndex++)
            {
                var row = new HelpStepRow(); row.Bind(section.Steps[stepIndex], stepIndex + 1, chips);
                if (stepIndex != 0) row.AddToClassList("help-step--spaced");
                steps.Add(row);
            }
            BodyScroll.scrollOffset = Vector2.zero;
        }
    }

    [UxmlElement]
    public partial class HelpPager : VisualElement
    {
        private readonly ImoniButton previous = new ImoniButton { name = "HelpPrevious", Variant = ImoniButtonVariant.Outline };
        private readonly ImoniButton next = new ImoniButton { name = "HelpNext", Variant = ImoniButtonVariant.Primary };
        public event Action PreviousRequested;
        public event Action NextRequested;
        public HelpPager()
        {
            AddToClassList("help-pager"); UiTheme.Apply(this, "UI/Components/HelpParts");
            previous.AddToClassList("help-pager__previous"); next.AddToClassList("help-pager__next");
            Add(previous); Add(next);
            previous.clicked += () => PreviousRequested?.Invoke(); next.clicked += () => NextRequested?.Invoke();
        }
        public void Bind(HelpContent content, int index)
        {
            previous.text = content.PreviousText; previous.Disabled = index == 0;
            next.text = index == content.Sections.Length - 1 ? content.DoneText : content.NextText;
        }
    }
}
