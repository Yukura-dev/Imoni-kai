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
            Find<StatCard>("best-round").Value = "R" + App.Data["outGame"]["profile"]["bestRound"];
            Find<ImoniText>("endless-locked").text = "エンドレス:" + App.Data["round"]["roundsToClear"] + "連続成功で解放";
            Find<ImoniButton>("play").clicked += () => App.Go("Lobby");
            Find<TabBar>("tabs").NavigateRequested += App.Go;

            VisualElement overlay = Find<VisualElement>("settings-overlay");
            Find<ImoniButton>("settings").clicked += () => overlay.style.display = DisplayStyle.Flex;
            Find<ImoniButton>("settings-close").clicked += () => overlay.style.display = DisplayStyle.None;
            var sound = Find<ImoniButton>("sound-toggle");
            var volume = Find<AudioVolumeControl>("sound-volume");
            void ShowSound()
            {
                sound.text = App.Profile.SoundEnabled ? "音: ON" : "音: OFF";
                sound.Icon = App.Profile.SoundEnabled ? "icon_sound_on" : "icon_sound_off";
                volume.Bind(App.Profile.SoundVolume, App.Data["audio"]["minVolume"].Value<double>(), App.Data["audio"]["maxVolume"].Value<double>(), App.Data["audio"]["volumePercentScale"].Value<double>());
            }
            sound.clicked += () => { App.Profile.SoundEnabled = !App.Profile.SoundEnabled; App.SaveProfile(); ShowSound(); };
            volume.StepRequested += direction => { App.Profile.SoundVolume = ImoniAudioPlan.AdjustVolume(App.Data, App.Profile.SoundVolume, direction); App.SaveProfile(); ShowSound(); };
            ShowSound();
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
            string section = common ? "common" : "free";
            string[] teams = fixture[section + "Teams"].Values<string>().ToArray();
            JToken scores = fixture[section + (normal ? "Normal" : "Endless")];
            var rows = new List<RankingRowView>();
            for (int index = 0; index < teams.Length; index++)
            {
                JToken score = scores[index];
                string clear = normal ? App.Data["round"]["roundsToClear"] + "R連続成功" : "最高R" + score["round"];
                string seedText = common ? string.Empty : " · シード" + fixture["freeSeeds"][index];
                rows.Add(new RankingRowView
                {
                    RankText = (index + 1).ToString(), Team = teams[index],
                    Detail = clear + seedText + " · " + fixture["dates"][index],
                    ScoreText = Format.Big(BigInteger.Parse(score["score"].Value<string>(), CultureInfo.InvariantCulture), UiTheme.Config),
                    Style = index == 0 ? RankingRankStyle.First : index < fixture["podiumRanks"].Value<int>() ? RankingRankStyle.Podium : RankingRankStyle.Normal,
                    Alternating = index % 2 == 0
                });
            }
            Find<RankingList>("ranking-list").Bind(rows);
            Find<ImoniText>("ranking-caption").text = common
                ? "共通シード部門:今週 " + fixture["commonSeed"] : "自由シード部門:マップはチームごとにランダム";
            Find<ImoniText>("ranking-team-size").text = App.Data["team"]["size"] + "人部門";
            Find<ImoniText>("ranking-mine-team").text = App.TeamName + "(あなた)";
            Find<ImoniText>("ranking-mine-note").text = normal
                ? App.Data["round"]["roundsToClear"] + "ラウンド連続成功で登録されます(最高 R" + App.Data["outGame"]["profile"]["bestRound"] + ")"
                : "通常クリアで解放";
        }
    }
}
