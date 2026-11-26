using System;
using System.IO;
using System.Linq;
using Imoni.Core;
using Imoni.Game.Audio;
using Imoni.Game.UI.Components;
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace Imoni.Game.UI.Screens
{
    public sealed class ScreenContext
    {
        private readonly string savePath;
        private bool preparedRoundForLoading;
        private RankingSeed challengeDivision;
        private string challengeTeamName;
        public string ChallengeId { get; private set; }
        public ScreenRouter Router { get; }
        public Bootstrap Bootstrap { get; }
        public IGameServer Server { get; private set; }
        public ProfileData Profile { get; private set; }
        public JObject Data => UiTheme.Data;
        public bool FreeSeed { get; set; }
        public bool EndlessSelected { get; set; }
        public bool EndlessUnlocked => Profile.Progression.NormalClears > 0;
        public bool JoinedTeam { get; set; }
        public int MemberCount { get; set; } = 1;
        public string TeamName { get; set; } = "鍋底同盟";
        public string InviteCode { get; set; } = "IMO-482";
        public string SavePath => savePath;
        public ScreenContext(ScreenRouter router, string path = null, Bootstrap bootstrap = null)
        {
            Router = router ?? throw new ArgumentNullException(nameof(router));
            Bootstrap = bootstrap;
            savePath = path ?? Path.Combine(Application.persistentDataPath, "imoni-profile.json");
            Profile = ProfilePersistence.Load(Data, File.Exists(savePath) ? File.ReadAllText(savePath) : null);
            ApplySound();
        }
        public void Go(string destination) => Router.Show(destination);
        public double Number(string uiKey) => UiTheme.Number(uiKey);
        public void PrepareGame(int? seed = null)
        {
            int chosen = seed ?? (FreeSeed ? unchecked((int)DateTime.UtcNow.Ticks)
                : Data["outGame"]["lobby"]["commonSeed"].Value<int>());
            if (!EndlessUnlocked) EndlessSelected = false;
            Server = new LocalGameServer(UiTheme.Config, chosen, endless: EndlessSelected);
            BeginChallengeIdentity(chosen);
            SaveReachedRound();
            preparedRoundForLoading = false;
        }
        public void PrepareLoadingGame()
        {
            if (!preparedRoundForLoading || Server == null) PrepareGame();
            preparedRoundForLoading = false;
        }
        public void StartNextRound()
        {
            Server.StartNextRound();
            SaveReachedRound();
            preparedRoundForLoading = false;
            Go("Game");
        }
        public void RetryChallenge()
        {
            int oldSeed = Server.Snapshot().Seed;
            int newSeed = unchecked((int)DateTime.UtcNow.Ticks);
            if (newSeed == oldSeed) newSeed = unchecked(newSeed + 1);
            Server.Retry(newSeed);
            FreeSeed = true;
            BeginChallengeIdentity(newSeed);
            SaveReachedRound();
            preparedRoundForLoading = true;
            Go("Loading");
        }
        public void GrantResultRewards(RoundResult result, string actorId)
        {
            Profile = PreviewResultRewards(result, actorId).Profile;
            Profile = LocalRanking.Register(Data, Profile, result, ChallengeId, challengeTeamName,
                UiTheme.Config.TeamSize, challengeDivision, DateTimeOffset.UtcNow);
            SaveProfile();
        }
        public ProgressionOutcome PreviewResultRewards(RoundResult result, string actorId) =>
            PlayerProgression.Apply(Data, Profile, result, actorId, ChallengeId + ":" + result.RoundNumber);
        private void BeginChallengeIdentity(int seed)
        {
            ChallengeId = Guid.NewGuid().ToString("N");
            challengeTeamName = TeamName;
            challengeDivision = FreeSeed || seed != Data["outGame"]["lobby"]["commonSeed"].Value<int>() ? RankingSeed.Free : RankingSeed.Common;
        }
        private void SaveReachedRound()
        {
            Profile = PlayerProgression.ReachRound(Data, Profile, Server.Snapshot().RoundNumber);
            SaveProfile();
        }
        public void StartEndless()
        {
            Server.StartEndless();
            SaveReachedRound();
            EndlessSelected = true;
            Go("Game");
        }
        public void FinishEndless()
        {
            Server.FinishEndless();
            Profile = LocalRanking.Register(Data, Profile, Server.Snapshot().LastResult, ChallengeId, challengeTeamName,
                UiTheme.Config.TeamSize, challengeDivision, DateTimeOffset.UtcNow, finishEndless: true);
            SaveProfile();
            Go("Home");
        }
        public void SaveProfile()
        {
            Profile = ProfilePersistence.Normalize(Data, Profile);
            string parent = Path.GetDirectoryName(savePath);
            if (!string.IsNullOrEmpty(parent)) Directory.CreateDirectory(parent);
            string temporary = savePath + ".tmp";
            File.WriteAllText(temporary, ProfilePersistence.Save(Data, Profile));
            if (File.Exists(savePath)) File.Replace(temporary, savePath, null);
            else File.Move(temporary, savePath);
            ApplySound();
        }
        private void ApplySound() => AudioService.Instance?.SetPreferences(Profile.SoundEnabled, Profile.SoundVolume);
        public ProfileViewModel ProfileView()
        {
            JToken fixture = Data["outGame"]["profile"];
            return new ProfileViewModel
            {
                Name = Profile.Name, First = Profile.FirstTitle, Second = Profile.SecondTitle,
                MemberNumber = "No." + fixture["memberNumber"].Value<string>(),
                Detail = "チーム " + TeamName + " / 最高到達 R" + Profile.Progression.BestRound,
                BadgeSummary = "達成したバッジ " + Profile.OwnedBadges.Length + "/" + Data["progression"]["badgeRules"].Count(),
                Badges = Data["progression"]["badgeRules"].Select(item => new BadgeViewModel { Text = item["text"].Value<string>(), Acquired = Profile.OwnedBadges.Contains(item["id"].Value<string>()) }).ToArray()
            };
        }
    }
}
