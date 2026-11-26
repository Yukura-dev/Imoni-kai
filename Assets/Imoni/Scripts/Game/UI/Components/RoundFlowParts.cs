using System;
using System.Collections.Generic;
using System.Linq;
using Imoni.Core;
using UnityEngine.UIElements;

namespace Imoni.Game.UI.Components
{
    internal static class RoundFlowVisuals
    {
        internal static void Apply(VisualElement element, string className)
        { element.AddToClassList(className); UiTheme.Apply(element, "UI/Components/RoundFlow"); }
        internal static Label Text(string text, string className) => UiTheme.Label(text, className);
    }

    [UxmlElement]
    public partial class RoundHeader : VisualElement
    {
        private readonly Label round = RoundFlowVisuals.Text(string.Empty, "round-header__round");
        private readonly Label status = RoundFlowVisuals.Text(string.Empty, "round-header__status");
        [UxmlAttribute] public string RoundText { get => round.text; set => round.text = value; }
        [UxmlAttribute] public string StatusText { get => status.text; set { status.text = value; status.style.display = string.IsNullOrEmpty(value) ? DisplayStyle.None : DisplayStyle.Flex; } }
        [UxmlAttribute] public bool Failure { get => ClassListContains("round-header--failure"); set => EnableInClassList("round-header--failure", value); }
        public RoundHeader() { RoundFlowVisuals.Apply(this, "round-header"); Add(round); Add(status); }
    }

    [UxmlElement]
    public partial class RankHero : VisualElement
    {
        private readonly Label rank = RoundFlowVisuals.Text(string.Empty, "rank-hero__rank");
        private readonly Label headline = RoundFlowVisuals.Text("うまい芋煮が\nできた!", "rank-hero__headline");
        [UxmlAttribute] public string Rank { get => rank.text; set => rank.text = value; }
        [UxmlAttribute] public string Headline { get => headline.text; set => headline.text = value; }
        public RankHero()
        {
            RoundFlowVisuals.Apply(this, "rank-hero");
            var circle = new VisualElement(); circle.AddToClassList("rank-hero__circle");
            circle.Add(RoundFlowVisuals.Text("ランク", "rank-hero__caption")); circle.Add(rank);
            Add(circle); Add(headline);
        }
    }

    [UxmlElement]
    public partial class RequiredGoalsCard : Card
    {
        private readonly List<MaterialGoalChip> rows = new List<MaterialGoalChip>();
        private bool failure;
        [UxmlAttribute] public bool Failure { get => failure; set { failure = value; EnableInClassList("round-goals--failure", value); foreach (var row in rows) row.Variant = value ? MaterialGoalVariant.FailureRow : MaterialGoalVariant.Row; } }
        public RequiredGoalsCard()
        {
            RoundFlowVisuals.Apply(this, "round-goals"); AddToClassList("round-card");
            Add(RoundFlowVisuals.Text("必須素材(品質換算)", "round-card__heading"));
        }
        public void Bind(IReadOnlyList<MaterialGoalView> goals)
        {
            // The mock has short names; the configured rare mountain vegetable needs a wider shared name column.
            EnableInClassList("round-goals--wide-name", goals.Any(goal => goal.Name.Length > 4));
            while (rows.Count > goals.Count) { rows[rows.Count - 1].RemoveFromHierarchy(); rows.RemoveAt(rows.Count - 1); }
            while (rows.Count < goals.Count) { var row = new MaterialGoalChip(); row.AddToClassList("round-goals__row"); rows.Add(row); Add(row); }
            for (int index = 0; index < rows.Count; index++) { rows[index].Variant = failure ? MaterialGoalVariant.FailureRow : MaterialGoalVariant.Row; rows[index].Bind(goals[index]); }
        }
    }

    [UxmlElement]
    public partial class ResultBonusCard : Card
    {
        private readonly Label caption = RoundFlowVisuals.Text(string.Empty, "round-card__heading");
        private readonly Label status = RoundFlowVisuals.Text(string.Empty, "result-bonus__status");
        private readonly BonusTierChips tiers = new BonusTierChips();
        private readonly VisualElement headingRow = new VisualElement();
        private readonly NumericLabelFitter captionFitter;
        private readonly NumericLabelFitter statusFitter;
        private IVisualElementScheduledItem layoutJob;
        public ResultBonusCard()
        {
            RoundFlowVisuals.Apply(this, "result-bonus"); AddToClassList("round-card");
            headingRow.AddToClassList("result-bonus__heading"); headingRow.Add(caption); headingRow.Add(status); Add(headingRow);
            tiers.AddToClassList("result-bonus__tiers"); Add(tiers);
            captionFitter = new NumericLabelFitter(caption); statusFitter = new NumericLabelFitter(status);
            RegisterCallback<AttachToPanelEvent>(_ => ScheduleLayout());
            RegisterCallback<DetachFromPanelEvent>(_ => layoutJob?.Pause());
            headingRow.RegisterCallback<GeometryChangedEvent>(_ => ScheduleLayout());
        }
        public void Bind(RoundResultView view)
        {
            caption.text = "ボーナス:" + view.BonusText; status.text = view.BonusStatusText; tiers.Bind(view.BonusTiers);
            captionFitter.Refresh(); statusFitter.Refresh(); ScheduleLayout();
        }
        private void ScheduleLayout()
        {
            if (panel == null) return;
            layoutJob?.Pause(); layoutJob = schedule.Execute(UpdateLayout);
        }
        private void UpdateLayout()
        {
            float width = headingRow.contentRect.width;
            if (width <= 0 || caption.resolvedStyle.fontSize <= 0 || status.resolvedStyle.fontSize <= 0) return;
            float minimum = (float)UiTheme.Number("numericLabelMinimumFontSize");
            float minimumCaption = caption.MeasureTextSize(caption.text, 0, VisualElement.MeasureMode.Undefined, 0, VisualElement.MeasureMode.Undefined).x * minimum / caption.resolvedStyle.fontSize;
            float minimumStatus = status.MeasureTextSize(status.text, 0, VisualElement.MeasureMode.Undefined, 0, VisualElement.MeasureMode.Undefined).x * minimum / status.resolvedStyle.fontSize;
            bool stacked = Math.Ceiling(minimumCaption) + Math.Ceiling(minimumStatus) > width;
            status.style.maxWidth = stacked ? width : width - (float)Math.Ceiling(minimumCaption);
            if (ClassListContains("result-bonus--stacked") == stacked) return;
            EnableInClassList("result-bonus--stacked", stacked);
            captionFitter.Refresh(true); statusFitter.Refresh(true);
        }
    }

    [UxmlElement]
    public partial class ScoreBreakdownCard : Card
    {
        private readonly Label heading = RoundFlowVisuals.Text(string.Empty, "round-card__heading");
        private readonly Label required = RoundFlowVisuals.Text(string.Empty, "score-breakdown__value");
        private readonly Label bonus = RoundFlowVisuals.Text(string.Empty, "score-breakdown__value");
        private readonly Label bonusCaption = RoundFlowVisuals.Text(string.Empty, "score-breakdown__caption");
        private readonly NumericLabelFitter requiredFitter;
        private readonly NumericLabelFitter bonusFitter;
        public ScoreBreakdownCard()
        {
            RoundFlowVisuals.Apply(this, "score-breakdown"); AddToClassList("round-card"); Add(heading);
            Add(Row(RoundFlowVisuals.Text("素材ポイント R", "score-breakdown__caption"), required)); Add(Row(bonusCaption, bonus));
            Add(RoundFlowVisuals.Text("売却分は対象外・表示は端数切り捨て", "score-breakdown__note"));
            requiredFitter = new NumericLabelFitter(required); bonusFitter = new NumericLabelFitter(bonus);
        }
        private static VisualElement Row(Label caption, Label value)
        { var row = new VisualElement(); row.AddToClassList("score-breakdown__row"); row.Add(caption); row.Add(value); return row; }
        public void Bind(RoundResultView view)
        {
            heading.text = view.BreakdownTitle;
            required.text = view.RequiredPointsText + " → " + view.RequiredWeightedText;
            bonusCaption.text = "ボーナスポイント B × T(" + view.BonusMultiplierText + ")";
            bonus.text = view.BonusPointsText + " → " + view.BonusWeightedText;
            requiredFitter.Refresh(); bonusFitter.Refresh();
        }
    }

    [UxmlElement]
    public partial class ResultSummary : Card
    {
        private readonly StatCard score = new StatCard { Variant = StatCardVariant.Result, Label = "獲得スコア" };
        private readonly StatCard funds = new StatCard { Variant = StatCardVariant.Result, Label = "獲得資金" };
        private readonly StatCard cumulative = new StatCard { Variant = StatCardVariant.Result, Label = "累積スコア" };
        private readonly StatCard title = new StatCard { Variant = StatCardVariant.Result, Label = "新しい二つ名" };
        public ResultSummary()
        {
            RoundFlowVisuals.Apply(this, "result-summary"); Variant = CardVariant.Dark;
            funds.AddToClassList("result-summary__funds"); cumulative.AddToClassList("result-summary__small"); title.AddToClassList("result-summary__small");
            Add(Row(score, funds)); var lower = Row(cumulative, title); lower.AddToClassList("result-summary__lower"); Add(lower);
        }
        private static VisualElement Row(StatCard left, StatCard right)
        { var row = new VisualElement(); row.AddToClassList("result-summary__row"); right.AddToClassList("result-summary__right"); row.Add(left); row.Add(right); return row; }
        public void Bind(RoundResultView view, string titleText)
        {
            score.Value = view.ScoreText; funds.Value = view.FundsRewardText; cumulative.Value = view.CumulativeScoreText; title.Value = titleText;
            title.tooltip = string.IsNullOrEmpty(view.NewTitleText) ? titleText : view.NewTitleText;
        }
    }

    [UxmlElement]
    public partial class FailureResetCard : Card
    {
        public FailureResetCard()
        {
            RoundFlowVisuals.Apply(this, "failure-reset"); Variant = CardVariant.Dashed;
            var outline = new LobbyDashedOutline(false, 18, 3, null, 6, 3); outline.AddToClassList("failure-reset__outline"); hierarchy.Insert(0, outline);
            Add(RoundFlowVisuals.Text("リセットされるもの", "failure-reset__heading"));
            Add(RoundFlowVisuals.Text("・共有倉庫の素材 / 共有資金", "failure-reset__line"));
            Add(RoundFlowVisuals.Text("・全員の個人強化 / 連続記録", "failure-reset__line"));
            Add(RoundFlowVisuals.Text("・マップ(新しいランダムマップで再挑戦)", "failure-reset__line"));
            var ranking = RoundFlowVisuals.Text("・今回の挑戦はランキングに登録されません", "failure-reset__line"); ranking.AddToClassList("failure-reset__warning"); Add(ranking);
            Add(RoundFlowVisuals.Text("過去のランキング記録・会員証・二つ名はそのまま", "failure-reset__note"));
        }
    }

    [UxmlElement]
    public partial class RoundTimeUpDialog : VisualElement
    {
        private readonly Label caption = RoundFlowVisuals.Text(string.Empty, "round-timeup__caption");
        public ImoniButton ResultsButton { get; } = new ImoniButton { name = "TimeUpResults", Variant = ImoniButtonVariant.Primary, text = "結果を見る" };
        [UxmlAttribute] public string TimeText { get => caption.text; set => caption.text = value; }
        public RoundTimeUpDialog()
        {
            RoundFlowVisuals.Apply(this, "round-timeup"); var card = new Card(); card.AddToClassList("round-timeup__card");
            card.Add(caption); card.Add(RoundFlowVisuals.Text("芋煮会の時間だ!", "round-timeup__headline"));
            card.Add(RoundFlowVisuals.Text("倉庫の素材をすべて鍋に投入したよ", "round-timeup__copy"));
            ResultsButton.AddToClassList("round-timeup__button"); card.Add(ResultsButton); Add(card);
        }
    }
}
