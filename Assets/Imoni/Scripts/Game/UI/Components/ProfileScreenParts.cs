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
            Add(scroll);
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
        }
    }
}
