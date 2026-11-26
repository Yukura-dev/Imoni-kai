using System;
using Newtonsoft.Json;

namespace Imoni.Game.UI
{
    /// <summary>Presentation-only copy exported from mock/Help.dc.html's SECTIONS.</summary>
    [Serializable]
    public sealed class HelpContent
    {
        public string Title { get; set; }
        public string BackText { get; set; }
        public string PreviousText { get; set; }
        public string NextText { get; set; }
        public string DoneText { get; set; }
        public HelpSection[] Sections { get; set; } = Array.Empty<HelpSection>();
        public HelpQualityChip[] QualityChips { get; set; } = Array.Empty<HelpQualityChip>();

        public static HelpContent Load()
        {
            if (!UiTheme.Assets || !UiTheme.Assets.helpContent)
                throw new InvalidOperationException("Missing HelpContent.json. Run Imoni > Setup Project.");
            var content = JsonConvert.DeserializeObject<HelpContent>(UiTheme.Assets.helpContent.text);
            if (content == null || content.Sections.Length == 0)
                throw new InvalidOperationException("HelpContent.json requires sections.");
            return content;
        }
    }

    [Serializable]
    public sealed class HelpSection
    {
        public string Tab { get; set; }
        public string Icon { get; set; }
        public string Title { get; set; }
        public string Lead { get; set; }
        public HelpStep[] Steps { get; set; } = Array.Empty<HelpStep>();
    }

    [Serializable]
    public sealed class HelpStep
    {
        public string Icon { get; set; }
        public string Color { get; set; }
        public string Title { get; set; }
        public string Desc { get; set; }
        public string Note { get; set; }
        public bool Chips { get; set; }
    }

    [Serializable]
    public sealed class HelpQualityChip
    {
        public string Quality { get; set; }
        public bool Locked { get; set; }
    }
}
