using System;
using Imoni.Game.UI.Components;

namespace Imoni.Game.UI.Screens
{
    /// <summary>Help overlays the original screen; closing it never recreates the game or its panels.</summary>
    public sealed class HelpScreen : ScreenView
    {
        private readonly HelpContent content;
        private readonly HelpTabs tabs;
        private readonly HelpPageCard card;
        private readonly HelpPager pager;
        public int SectionIndex { get; private set; }
        public HelpScreen(ScreenContext app) : base(app, "UI/Help", "UI/HelpScreen")
        {
            content = HelpContent.Load();
            var header = Find<ScreenHeader>("HelpHeader"); header.Title = content.Title; header.BackText = content.BackText;
            header.BackRequested += App.CloseHelp;
            tabs = Find<HelpTabs>("HelpTabs"); card = Find<HelpPageCard>("HelpCard"); pager = Find<HelpPager>("HelpPager");
            tabs.Bind(content.Sections, 0); tabs.Changed += SelectSection;
            pager.PreviousRequested += () => SelectSection(SectionIndex - 1);
            pager.NextRequested += () =>
            {
                if (SectionIndex == content.Sections.Length - 1) App.CloseHelp();
                else SelectSection(SectionIndex + 1);
            };
            SelectSection(0);
        }
        public void SelectSection(int index)
        {
            SectionIndex = Math.Max(0, Math.Min(content.Sections.Length - 1, index));
            tabs.Selected = SectionIndex;
            card.Bind(content.Sections[SectionIndex], SectionIndex, content.Sections.Length, content.QualityChips);
            pager.Bind(content, SectionIndex);
        }
    }
}
