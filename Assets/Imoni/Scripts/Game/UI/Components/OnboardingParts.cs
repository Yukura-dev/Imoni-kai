using UnityEngine.UIElements;

namespace Imoni.Game.UI.Components
{
    public enum TimelineTone { Start, Bonus, Finish }
    public enum TutorialInfoTone { Green, Red }

    /// <summary>A presentation-only timeline row; the screen supplies configured times and copy.</summary>
    [UxmlElement]
    public partial class TutorialTimelineStep : VisualElement
    {
        private readonly Label time = UiTheme.Label(string.Empty, "tutorial-timeline__time");
        private readonly Label heading = UiTheme.Label(string.Empty, "tutorial-timeline__heading");
        private readonly Label description = UiTheme.Label(string.Empty, "tutorial-timeline__description");
        private TimelineTone tone;
        [UxmlAttribute] public string Time { get => time.text; set => time.text = value ?? string.Empty; }
        [UxmlAttribute] public string Heading { get => heading.text; set => heading.text = value ?? string.Empty; }
        [UxmlAttribute] public string Description { get => description.text; set => description.text = value ?? string.Empty; }
        [UxmlAttribute] public TimelineTone Tone { get => tone; set { tone = value; UiTheme.SetVariant(this, "tutorial-timeline--", value); } }

        public TutorialTimelineStep()
        {
            AddToClassList("tutorial-timeline");
            UiTheme.Apply(this, "UI/Components/OnboardingParts");
            var body = new VisualElement(); body.AddToClassList("tutorial-timeline__body");
            body.Add(heading); body.Add(description); Add(time); Add(body);
            Tone = TimelineTone.Start;
        }
    }

    /// <summary>Shared card presentation for tutorial guidance and success/failure explanations.</summary>
    [UxmlElement]
    public partial class TutorialInfo : Card
    {
        private readonly Label heading = UiTheme.Label(string.Empty, "tutorial-info__heading");
        private readonly Label body = UiTheme.Label(string.Empty, "tutorial-info__body");
        private TutorialInfoTone tone;
        [UxmlAttribute] public string Heading { get => heading.text; set => heading.text = value ?? string.Empty; }
        [UxmlAttribute] public string Body { get => body.text; set => body.text = value ?? string.Empty; }
        [UxmlAttribute] public TutorialInfoTone Tone { get => tone; set { tone = value; UiTheme.SetVariant(this, "tutorial-info-tone--", value); } }
        [UxmlAttribute] public bool Compact { get => ClassListContains("tutorial-info--compact"); set => EnableInClassList("tutorial-info--compact", value); }

        public TutorialInfo()
        {
            AddToClassList("tutorial-info");
            UiTheme.Apply(this, "UI/Components/OnboardingParts");
            Add(heading); Add(body); Tone = TutorialInfoTone.Green;
            var outline = new LobbyDashedOutline(false, 16, 3, "#4A2E22");
            outline.AddToClassList("tutorial-info__outline");
            hierarchy.Add(outline);
        }
    }

    [UxmlElement]
    public partial class UpgradeHint : VisualElement
    {
        private readonly Label heading = UiTheme.Label(string.Empty, "upgrade-hint__heading");
        private readonly Label body = UiTheme.Label(string.Empty, "upgrade-hint__body");
        [UxmlAttribute] public string Heading { get => heading.text; set => heading.text = value ?? string.Empty; }
        [UxmlAttribute] public string Body { get => body.text; set => body.text = value ?? string.Empty; }
        public UpgradeHint()
        {
            AddToClassList("upgrade-hint");
            UiTheme.Apply(this, "UI/Components/OnboardingParts");
            Add(heading); Add(body);
        }
    }
}
