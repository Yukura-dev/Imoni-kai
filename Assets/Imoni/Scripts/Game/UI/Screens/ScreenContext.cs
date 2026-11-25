using System;
using System.IO;
using System.Linq;
using Imoni.Core;
using Imoni.Game.UI.Components;
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace Imoni.Game.UI.Screens
{
    public sealed class ScreenContext
    {
        private readonly string savePath;
        public ScreenRouter Router { get; }
        public Bootstrap Bootstrap { get; }
        public IGameServer Server { get; private set; }
        public ProfileData Profile { get; private set; }
        public JObject Data => UiTheme.Data;
        public bool FreeSeed { get; set; }
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
            Server = new LocalGameServer(UiTheme.Config, chosen);
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
        private void ApplySound() => AudioListener.volume = Profile.SoundEnabled ? 1 : 0;
        public ProfileViewModel ProfileView()
        {
            JToken fixture = Data["outGame"]["profile"];
            return new ProfileViewModel
            {
                Name = Profile.Name, First = Profile.FirstTitle, Second = Profile.SecondTitle,
                MemberNumber = "No." + fixture["memberNumber"].Value<string>(),
                Detail = "チーム " + TeamName + " / 最高到達 R" + fixture["bestRound"],
                BadgeSummary = "達成したバッジ " + fixture["badges"].Count(item => item["acquired"].Value<bool>()) + "/" + fixture["badges"].Count(),
                Badges = fixture["badges"].Select(item => new BadgeViewModel { Text = item["text"].Value<string>(), Acquired = item["acquired"].Value<bool>() }).ToArray()
            };
        }
    }
}
