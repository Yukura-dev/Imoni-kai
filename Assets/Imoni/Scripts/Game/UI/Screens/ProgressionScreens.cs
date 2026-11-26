using System;
using Imoni.Core;
using Imoni.Game.UI.Components;

namespace Imoni.Game.UI.Screens
{
    public sealed class LoginScreen : ScreenView
    {
        public LoginScreen(ScreenContext app) : base(app, "UI/Login", "UI/ProgressionScreens")
        {
            Find<MemberCard>("LoginMember").Bind(App.ProfileView());
            Find<ImoniButton>("LoginContinue").text = App.Profile.TutorialCompleted ? "この会員で続ける" : "会員登録をはじめる";
            Find<ImoniButton>("LoginContinue").clicked += () => App.Go(App.Profile.TutorialCompleted ? "Home" : "Tutorial1");
            Find<ImoniButton>("LoginTutorial").clicked += () => App.Go("Tutorial1");
        }
    }

    public sealed class EndlessScreen : ScreenView
    {
        public EndlessScreen(ScreenContext app) : base(app, "UI/Endless", "UI/ProgressionScreens")
        {
            GameSnapshot state = App.Server?.Snapshot();
            if (state == null || state.Phase != RoundPhase.Cleared || state.IsEndless)
                throw new InvalidOperationException("Endless transition requires a completed normal challenge.");
            Find<StatCard>("EndlessScore").Value = Format.Big(state.CumulativeScore, UiTheme.Config);
            Find<StatCard>("EndlessFunds").Value = Format.Big(state.Funds, UiTheme.Config) + UiTheme.Config.CurrencyUnit;
            Find<ImoniText>("EndlessDescription").text = "ラウンド" + (state.RoundNumber + 1) + "から、同じマップで続けよう。\n資金・強化・累積スコアを引き継ぎます。";
            Find<ImoniButton>("EndlessStart").text = "ラウンド" + (state.RoundNumber + 1) + "へ";
            Find<ImoniButton>("EndlessStart").clicked += App.StartEndless;
            Find<ImoniButton>("EndlessHome").clicked += () => App.Go("Home");
        }
    }
}
