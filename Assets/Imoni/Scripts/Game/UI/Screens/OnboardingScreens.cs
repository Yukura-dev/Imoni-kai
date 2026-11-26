using System;
using System.Globalization;
using System.Linq;
using Imoni.Game.UI.Components;
using Newtonsoft.Json.Linq;
using UnityEngine.UIElements;
using ProgressBar = Imoni.Game.UI.Components.ProgressBar;

namespace Imoni.Game.UI.Screens
{
    public sealed class MainScreen : ScreenView
    {
        private readonly ProgressBar progress;
        private readonly ImoniText status;
        private readonly ImoniButton start;
        public double ProgressPercent { get; private set; }

        public MainScreen(ScreenContext app) : base(app, "UI/Main", "UI/OnboardingScreens")
        {
            progress = Find<ProgressBar>("loading-progress");
            status = Find<ImoniText>("loading-label");
            start = Find<ImoniButton>("start");
            start.clicked += () => App.Go(App.Profile.TutorialCompleted ? "Home" : "Tutorial1");
            SettingsPresenter.Attach(this, App);
            Later(AdvanceLoading, App.Number("mainLoadingTickMs") / 1000);
        }

        private void AdvanceLoading()
        {
            ProgressPercent = Math.Min(100, ProgressPercent + App.Number("mainLoadingStepPercent"));
            progress.Value = (float)(ProgressPercent / 100);
            status.text = "よみこみ中… " + ProgressPercent.ToString("0", CultureInfo.InvariantCulture) + "%";
            bool complete = ProgressPercent >= 100;
            status.style.display = complete ? DisplayStyle.None : DisplayStyle.Flex;
            start.style.display = complete ? DisplayStyle.Flex : DisplayStyle.None;
            if (!complete) Later(AdvanceLoading, App.Number("mainLoadingTickMs") / 1000);
        }
    }

    public sealed class Tutorial1Screen : ScreenView
    {
        public Tutorial1Screen(ScreenContext app) : base(app, "UI/Tutorial1", "UI/OnboardingScreens")
        {
            MemberCard card = Find<MemberCard>("registration-card");
            TextField nameField = card.Q<TextField>();
            nameField.maxLength = (int)App.Number("profileNameMaxLength");
            ProfileViewModel profile = App.ProfileView();
            profile.MemberNumber = "会員番号 " + profile.MemberNumber.Replace("No. ", "No.");
            card.Bind(profile);
            card.NameChanged += value => App.Profile.Name = value;
            Find<ImoniButton>("next").clicked += () => { App.SaveProfile(); App.Go("Tutorial2"); };
        }
    }

    public sealed class Tutorial2Screen : ScreenView
    {
        public Tutorial2Screen(ScreenContext app) : base(app, "UI/Tutorial2", "UI/OnboardingScreens")
        {
            JObject round = (JObject)App.Data["round"];
            double duration = round["durationSec"].Value<double>();
            Find<StepTitle>("round-title").Title = "チームで素材を集めて、\n" +
                (duration / 60).ToString("0.#", CultureInfo.InvariantCulture) + "分後の芋煮会を成功させよう!";
            TutorialTimelineStep bonus = Find<TutorialTimelineStep>("timeline-bonus");
            bonus.Time = Clock(round["bonusRevealSec"].Value<double>());
            string[] thresholds = App.Data["bonusTiers"].Select(tier => tier["minPercent"].ToString() + "%").ToArray();
            bonus.Description = "目標の" + thresholds[0] + "で報酬。" + string.Join("・", thresholds.Skip(1)) + "でさらに倍!";
            Find<TutorialTimelineStep>("timeline-finish").Time = Clock(duration);
            Find<ImoniText>("clear-condition").text = round["roundsToClear"] + "ラウンド連続成功でクリア!立派な芋煮会をしよう！";
            Find<ImoniButton>("next").clicked += () => App.Go("Tutorial3");
        }

        private static string Clock(double seconds) => TimeSpan.FromSeconds(seconds).ToString(@"m\:ss", CultureInfo.InvariantCulture);
    }

    public sealed class Tutorial3Screen : ScreenView
    {
        public Tutorial3Screen(ScreenContext app) : base(app, "UI/Tutorial3", "UI/OnboardingScreens")
        {
            VisualElement qualities = Find<VisualElement>("quality-chips");
            bool hidden = false;
            string visibleThrough = App.Data["ui"]["tutorialVisibleQualityThrough"].Value<string>();
            foreach (JToken quality in App.Data["qualities"])
            {
                string id = quality["id"].Value<string>();
                qualities.Add(new QualityChip { Quality = id, ShowMultiplier = true, Locked = hidden });
                if (id == visibleThrough) hidden = true;
            }
            foreach (JToken upgrade in App.Data["upgrades"]["items"])
            {
                UpgradeHint hint = this.Q<UpgradeHint>("upgrade-" + upgrade["id"].Value<string>());
                if (hint != null) hint.Heading = upgrade["name"].Value<string>();
            }
            Find<ImoniButton>("next").clicked += () =>
            {
                App.Profile.TutorialCompleted = true;
                App.SaveProfile();
                App.Go("Home");
            };
        }
    }
}
