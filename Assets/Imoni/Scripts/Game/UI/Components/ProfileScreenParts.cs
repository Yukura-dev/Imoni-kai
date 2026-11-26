using System;
using System.Collections.Generic;
using UnityEngine.UIElements;

namespace Imoni.Game.UI.Components
{
    /// <summary>Scrollable presentation of already ordered ranking rows; it never ranks or scores teams.</summary>
    [UxmlElement]
    public partial class RankingList : VisualElement
    {
        private readonly ScrollView scroll;
        private readonly Label empty = UiTheme.Label("まだ記録がありません", "ranking-list__empty");
        [UxmlAttribute] public string EmptyText { get => empty.text; set => empty.text = value ?? string.Empty; }

        public RankingList()
        {
            AddToClassList("ranking-list");
            UiTheme.Apply(this, "UI/Components/ProfileScreenParts");
            scroll = new ScrollView(ScrollViewMode.Vertical)
            {
                horizontalScrollerVisibility = ScrollerVisibility.Hidden,
                verticalScrollerVisibility = ScrollerVisibility.Hidden
            };
            scroll.AddToClassList("ranking-list__scroll");
            // Keep each row's clip in panel coordinates when Safe Area padding
            // moves a scaled panel. The default grouped scroll transform can
            // otherwise discard the leading row even with correct world bounds.
            scroll.contentContainer.usageHints &= ~UsageHints.GroupTransform;
            Add(scroll);
            Add(empty);
        }

        public void Bind(IEnumerable<RankingRowView> rows)
        {
            scroll.Clear();
            foreach (RankingRowView view in rows ?? Array.Empty<RankingRowView>())
            {
                var row = new RankingRow();
                row.Bind(view);
                scroll.Add(row);
            }
            empty.style.display = scroll.childCount == 0 ? DisplayStyle.Flex : DisplayStyle.None;
            scroll.style.display = scroll.childCount == 0 ? DisplayStyle.None : DisplayStyle.Flex;
        }
    }
}
