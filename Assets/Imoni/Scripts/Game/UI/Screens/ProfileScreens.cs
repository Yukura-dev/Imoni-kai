using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Numerics;
using Imoni.Core;
using Imoni.Core.Effects;
using Imoni.Game.UI.Components;
using Newtonsoft.Json.Linq;
using UnityEngine.UIElements;

namespace Imoni.Game.UI.Screens
{
    public sealed class HomeScreen : ScreenView
    {
        public HomeScreen(ScreenContext app) : base(app, "UI/Home", "UI/ProfileScreens")
        {
            Find<ImoniText>("home-name").text = App.Profile.Name;
            var titles = Find<TitleChips>("home-titles");
            titles.First = App.Profile.FirstTitle;
            titles.Second = App.Profile.SecondTitle;
            var avatar = Find<Avatar>("home-avatar");
            avatar.BandColor = App.Profile.BandColor;
            avatar.OutfitColor = App.Profile.OutfitColor;
            Find<ImoniText>("home-team").text = "チーム「" + App.TeamName + "」 / " + App.Data["team"]["size"] + "人";
            Find<StatCard>("best-round").Value = "R" + App.Profile.Progression.BestRound;
            Find<StatCard>("normal-clears").Value = App.Profile.Progression.NormalClears > 0 ? Format.Big(App.Profile.Progression.NormalClears, UiTheme.Config) + "回" : "未達成";
            Find<StatCard>("ranking-records").Value = App.Profile.Rankings.Length > 0 ? "登録済み" : "未登録";
            Find<ImoniText>("endless-locked").text = "エンドレス:" + App.Data["round"]["roundsToClear"] + "連続成功で解放";
            Find<ImoniButton>("play").clicked += () => App.Go("Lobby");
            var modeSelector = Find<SegmentedControl>("home-mode-selector");
            modeSelector.style.display = App.EndlessUnlocked ? DisplayStyle.Flex : DisplayStyle.None;
            this.Q<VisualElement>(className: "home-modes").style.display = App.EndlessUnlocked ? DisplayStyle.None : DisplayStyle.Flex;
            modeSelector.Selected = App.EndlessSelected ? 1 : 0;
            modeSelector.Changed += selected => App.EndlessSelected = selected != 0 && App.EndlessUnlocked;
            Find<TabBar>("tabs").NavigateRequested += App.Go;

            SettingsPresenter.Attach(this, App);
        }
    }

    public sealed class MemberScreen : ScreenView
    {
        private readonly MemberCard card;
        private readonly TitlePartPicker picker;

        public MemberScreen(ScreenContext app) : base(app, "UI/Member", "UI/ProfileScreens")
        {
            card = Find<MemberCard>("member-card");
            picker = Find<TitlePartPicker>("title-picker");
            var editor = Find<AvatarEditor>("avatar-editor");
            editor.BandColor = App.Profile.BandColor;
            editor.OutfitColor = App.Profile.OutfitColor;
            editor.BandColorChanged += color => { App.Profile.BandColor = color; App.SaveProfile(); };
            editor.OutfitColorChanged += color => { App.Profile.OutfitColor = color; App.SaveProfile(); };
            picker.Picked += (section, text) =>
            {
                if (section == TitlePartSection.First) App.Profile.FirstTitle = text;
                else App.Profile.SecondTitle = text;
                App.SaveProfile();
                RefreshProfile();
            };
            Find<TabBar>("tabs").NavigateRequested += App.Go;
            RefreshProfile();
        }

        private void RefreshProfile()
        {
            card.Bind(App.ProfileView());
            JToken titles = App.Data["titles"];
            List<TitlePartViewModel> first = Parts(titles["firstParts"], App.Profile.OwnedFirstTitles, App.Profile.FirstTitle);
            List<TitlePartViewModel> second = Parts(titles["secondParts"], App.Profile.OwnedSecondTitles, App.Profile.SecondTitle);
            picker.Bind(first, second, "前半 " + first.Count(part => part.Owned) + "/" + first.Count
                + " / 後半 " + second.Count(part => part.Owned) + "/" + second.Count + " 収集");
        }

        private static List<TitlePartViewModel> Parts(JToken all, IEnumerable<string> owned, string selected)
        {
            var ownedText = new HashSet<string>(owned, StringComparer.Ordinal);
            return all.Values<string>().Select(text => new TitlePartViewModel
            { Id = text, Text = text, Owned = ownedText.Contains(text), Selected = text == selected }).ToList();
        }
    }

    public sealed class RankingScreen : ScreenView
    {
        private int mode;
        private int seed;

        public RankingScreen(ScreenContext app) : base(app, "UI/Ranking", "UI/ProfileScreens")
        {
            Find<SegmentedControl>("ranking-mode").Changed += index => { mode = index; RefreshRanking(); };
            Find<SegmentedControl>("ranking-seed").Changed += index => { seed = index; RefreshRanking(); };
            Find<TabBar>("tabs").NavigateRequested += App.Go;
            RefreshRanking();
        }

        private void RefreshRanking()
        {
            JToken fixture = App.Data["outGame"]["ranking"];
            bool common = seed == 0;
            bool normal = mode == 0;
            var records = LocalRanking.Board(App.Profile, normal ? RankingMode.Normal : RankingMode.Endless,
                common ? RankingSeed.Common : RankingSeed.Free, UiTheme.Config.TeamSize, App.Data["outGame"]["lobby"]["commonSeed"].Value<int>());
            var rows = new List<RankingRowView>();
            for (int index = 0; index < records.Count; index++)
            {
                LocalRankingRecord record = records[index];
                string clear = normal ? App.Data["round"]["roundsToClear"] + "R連続成功" : "最高R" + record.RoundNumber;
                string seedText = common ? string.Empty : " · #" + record.Seed;
                rows.Add(new RankingRowView
                {
                    RankText = (index + 1).ToString(CultureInfo.InvariantCulture), Team = record.TeamName,
                    Detail = clear + seedText + " · " + record.RecordedAtUtc.ToLocalTime().ToString("MM/dd HH:mm", CultureInfo.InvariantCulture),
                    ScoreText = Format.Big(record.Score, UiTheme.Config),
                    Style = index == 0 ? RankingRankStyle.First : index < fixture["podiumRanks"].Value<int>() ? RankingRankStyle.Podium : RankingRankStyle.Normal,
                    Alternating = index % 2 == 0
                });
            }
            Find<RankingList>("ranking-list").Bind(rows);
            Find<RankingList>("ranking-list").EmptyText = normal ? "まだ記録がありません\n通常モードをクリアして登録しよう" : "まだ記録がありません\nR" + (UiTheme.Config.RoundsToClear + 1) + "以降の成功結果から記録して終了しよう";
            Find<ImoniText>("ranking-caption").text = common
                ? "端末内 / 共通シード " + fixture["commonSeed"] : "端末内 / 自由シード部門";
            Find<ImoniText>("ranking-team-size").text = App.Data["team"]["size"] + "人部門";
            Find<ImoniText>("ranking-mine-team").text = App.TeamName + "(あなた)";
            int ownIndex = records.ToList().FindIndex(record => record.TeamName == App.TeamName);
            Find<ImoniText>("ranking-mine-state").text = ownIndex < 0 ? "未登録" : (ownIndex + 1) + "位";
            Find<ImoniText>("ranking-mine-note").text = ownIndex >= 0 ? "最高記録 " + Format.Big(records[ownIndex].Score, UiTheme.Config)
                : normal ? App.Data["round"]["roundsToClear"] + "ラウンド連続成功で登録されます(最高 R" + App.Profile.Progression.BestRound + ")"
                : App.EndlessUnlocked ? "成功結果で記録して終了 / 失敗・中断は未登録" : "通常クリアで解放";
        }
    }
}
