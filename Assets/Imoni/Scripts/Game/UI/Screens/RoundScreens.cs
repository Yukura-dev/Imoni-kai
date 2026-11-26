using System;
using System.Linq;
using Imoni.Core;
using Imoni.Game.UI.Components;
using UnityEngine.UIElements;

namespace Imoni.Game.UI.Screens
{
    /// <summary>Success and normal-clear use the same reusable result layout and frozen Core result.</summary>
    public sealed class ResultScreen : ScreenView
    {
        public RoundResultView Result { get; }
        public ResultScreen(ScreenContext app) : base(app, "UI/Result", "UI/RoundScreens")
        {
            RoundResult settled = App.Server?.Snapshot().LastResult ?? throw new InvalidOperationException("Result requires a settled round.");
            string actor = App.Server.Snapshot().Players.First(player => !player.IsBot).Id;
            ProgressionOutcome rewards = App.PreviewResultRewards(settled, actor);
            Result = RoundResultProjection.Present(UiTheme.Config, settled, actor, App.Profile.OwnedFirstTitles, App.Profile.OwnedSecondTitles, rewards.Titles);
            var header = Find<RoundHeader>("ResultHeader"); header.RoundText = Result.RoundText;
            var hero = Find<RankHero>("ResultHero"); hero.Rank = Result.Rank;
            Find<RequiredGoalsCard>("ResultRequired").Bind(Result.Required);
            Find<ResultBonusCard>("ResultBonus").Bind(Result);
            Find<ScoreBreakdownCard>("ResultBreakdown").Bind(Result);
            Find<ResultSummary>("ResultSummary").Bind(Result, Result.TitleRewardText);
            var next = Find<ImoniButton>("ResultNext"); next.text = Result.NextButtonText;
            if (Result.IsClear)
            {
                header.StatusText = "通常モード クリア!"; hero.Headline = "みんなで芋煮会\n大成功!";
                Find<ImoniText>("ResultNextNote").text = UiTheme.Config.RoundsToClear + "ラウンド連続成功!みんなでおいしい芋煮ができた!";
            }
            next.clicked += () => { if (Result.IsClear) App.Go("Home"); else if (Result.CanNextRound) App.StartNextRound(); };
            var endless = Find<ImoniButton>("ResultEndless");
            endless.style.display = Result.IsClear ? DisplayStyle.Flex : DisplayStyle.None;
            endless.clicked += () => App.Go("Endless");
            var finish = Find<ImoniButton>("ResultFinishEndless");
            finish.style.display = settled.IsEndless && settled.RoundNumber > UiTheme.Config.RoundsToClear ? DisplayStyle.Flex : DisplayStyle.None;
            finish.clicked += App.FinishEndless;
            Find<VisualElement>("ResultActions").EnableInClassList("result-actions--multiple", Result.IsClear || settled.IsEndless && settled.RoundNumber > UiTheme.Config.RoundsToClear);
            App.GrantResultRewards(settled, actor);
        }
    }

    public sealed class GameOverScreen : ScreenView
    {
        public RoundResultView Result { get; }
        public GameOverScreen(ScreenContext app) : base(app, "UI/GameOver", "UI/RoundScreens")
        {
            RoundResult settled = App.Server?.Snapshot().LastResult ?? throw new InvalidOperationException("Game over requires a settled round.");
            Result = RoundResultProjection.Present(UiTheme.Config, settled);
            Find<RoundHeader>("GameOverHeader").RoundText = Result.RoundText;
            Find<RequiredGoalsCard>("GameOverRequired").Bind(Result.Required);
            Find<StatCard>("GameOverRound").Value = "R" + Result.RoundNumber;
            Find<StatCard>("GameOverCumulative").Value = Result.CumulativeScoreText;
            Find<ImoniButton>("GameOverRetry").clicked += () => { if (Result.CanRetry) App.RetryChallenge(); };
            Find<ImoniButton>("GameOverHome").clicked += () => App.Go("Home");
            string actor = App.Server.Snapshot().Players.First(player => !player.IsBot).Id;
            App.GrantResultRewards(settled, actor);
        }
    }
}
