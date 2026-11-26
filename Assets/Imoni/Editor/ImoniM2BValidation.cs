using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Xml;
using Imoni.Core;
using Imoni.Game;
using Imoni.Game.Audio;
using Imoni.Game.UI;
using Imoni.Game.UI.Components;
using Imoni.Game.UI.Screens;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;
using Avatar = Imoni.Game.UI.Components.Avatar;
using ProgressBar = Imoni.Game.UI.Components.ProgressBar;
using Formatting = Newtonsoft.Json.Formatting;
using BigInteger = System.Numerics.BigInteger;

namespace Imoni.Editor
{
    /// <summary>Out-of-game integration checks isolated from the player's save and live screen.</summary>
    public static class ImoniM2BValidation
    {
        private static readonly string[] Screens =
        { "Main", "Tutorial1", "Tutorial2", "Tutorial3", "Home", "Lobby", "Waiting", "Loading", "Member", "Ranking", "Game" };
        private static readonly FieldInfo ScreenTimers = typeof(ScreenView).GetField("timers", BindingFlags.Instance | BindingFlags.NonPublic);
        private static readonly FieldInfo ScreenDisposed = typeof(ScreenView).GetField("disposed", BindingFlags.Instance | BindingFlags.NonPublic);

        [MenuItem("Imoni/Validate M2b Screens")]
        public static void Validate()
        {
            string savePath = LibraryPath("M2B-validation-profile.json");
            byte[] previousFixture = File.Exists(savePath) ? File.ReadAllBytes(savePath) : null;
            float previousVolume = AudioListener.volume;
            AudioService audio = AudioService.Instance;
            bool previousSound = audio && audio.SoundEnabled;
            double previousBgm = audio ? audio.BgmVolume : 0, previousEffects = audio ? audio.EffectsVolume : 0;
            var report = new JObject
            {
                ["passed"] = false,
                ["validatedAtUtc"] = DateTime.UtcNow.ToString("O"),
                ["unityVersion"] = Application.unityVersion,
                ["scope"] = "Detached resource checks plus a temporary hidden attached tree for field/input integration. Owned-title and local-ranking fixtures are restricted to the temporary profile; waiting/loading completion uses Core presentation values. Real-time animation, pointer hit testing, and screenshots are checked separately.",
                ["isolatedSavePath"] = savePath
            };
            try
            {
                Require(UiTheme.Assets && UiTheme.Config != null && UiTheme.Data?["outGame"] != null,
                    "Run Imoni > Setup Project with the M2b GameConfig before validation.");
                Require(ScreenTimers != null && ScreenDisposed != null, "ScreenView lifetime fields are unavailable.");
                if (File.Exists(savePath)) File.Delete(savePath);
                report["uxmlComposition"] = ValidateUxml();
                report["screens"] = ValidateScreens(savePath);
                report["flow"] = ValidateFlow(savePath);
                if (EditorApplication.isPlaying) report["attachedStyles"] = InspectAttached();
                report["passed"] = true;
            }
            catch (Exception exception)
            {
                report["failure"] = exception.ToString();
                throw;
            }
            finally
            {
                if (audio) audio.SetPreferences(previousSound, previousBgm, previousEffects);
                AudioListener.volume = previousVolume;
                if (previousFixture != null) File.WriteAllBytes(savePath, previousFixture);
                else if (File.Exists(savePath)) File.Delete(savePath);
                if (File.Exists(savePath + ".tmp")) File.Delete(savePath + ".tmp");
                File.WriteAllText(LibraryPath("M2BValidation.json"), report.ToString(Formatting.Indented));
            }
        }

        public static void ValidateAttached()
        {
            var report = new JObject { ["passed"] = false, ["validatedAtUtc"] = DateTime.UtcNow.ToString("O") };
            try { report["attachedStyles"] = InspectAttached(); report["passed"] = true; }
            catch (Exception exception) { report["failure"] = exception.ToString(); throw; }
            finally { File.WriteAllText(LibraryPath("M2BAttachedValidation.json"), report.ToString(Formatting.Indented)); }
        }

        private static JArray ValidateUxml()
        {
            var result = new JArray();
            var forbidden = new HashSet<string>(new[] { "Label", "Button", "Image", "TextField" }, StringComparer.Ordinal);
            foreach (string screen in Screens)
            {
                string path = "Assets/Imoni/Resources/UI/" + screen + ".uxml";
                var document = new XmlDocument(); document.Load(path);
                int components = 0;
                foreach (XmlNode node in document.SelectNodes("//*"))
                {
                    Require(node.NamespaceURI != "UnityEngine.UIElements" || !forbidden.Contains(node.LocalName),
                        screen + " UXML contains a raw " + node.LocalName + "; use the shared custom control.");
                    if (node.NamespaceURI == "Imoni.Game.UI.Components") components++;
                }
                Require(screen == "Game" || components > 0, screen + " must compose custom controls.");
                VisualTreeAsset asset = Resources.Load<VisualTreeAsset>("UI/" + screen);
                Require(asset, "Missing UXML resource: " + screen);
                TemplateContainer clone = asset.CloneTree();
                Require(clone.hierarchy.childCount > 0, screen + " did not clone a root element.");
                result.Add(new JObject { ["screen"] = screen, ["customControlTags"] = components, ["cloneSucceeded"] = true });
            }
            return result;
        }

        private static JArray ValidateScreens(string savePath)
        {
            var result = new JArray();
            var host = new VisualElement();
            using (var router = new ScreenRouter(host))
            {
                var app = new ScreenContext(router, savePath);
                ScreenCatalog.Register(router, app);
                ScreenView previous = null;
                IVisualElementScheduledItem[] previousTimers = Array.Empty<IVisualElementScheduledItem>();
                foreach (string screen in Screens)
                {
                    Require(router.Show(screen), "ScreenCatalog has no route: " + screen);
                    if (previous != null) AssertDisposed(previous, previousTimers);
                    Require(router.CurrentView is ScreenView && host.childCount == 1 && host[0] == router.CurrentView,
                        screen + " did not create exactly one ScreenView.");
                    previous = (ScreenView)router.CurrentView; previousTimers = Timers(previous);
                    result.Add(new JObject { ["screen"] = screen, ["controller"] = previous.GetType().Name, ["lifetimeTimers"] = previousTimers.Length });
                }
                router.Dispose();
                AssertDisposed(previous, previousTimers);
            }
            Require(host.childCount == 0, "The isolated router did not clear its host.");
            return result;
        }

        private static JObject ValidateFlow(string savePath)
        {
            var checks = new JArray();
            using (var attached = new AttachedValidationHost())
            using (var router = new ScreenRouter(attached.Host))
            {
                var app = new ScreenContext(router, savePath);
                ScreenCatalog.Register(router, app);
                Require(router.Show("Main"), "Main could not open.");
                var main = Current<MainScreen>(router);
                Require(main.ProgressPercent == 0, "Main must start at zero progress.");
                double increment = app.Number("mainLoadingStepPercent");
                Require(increment > 0, "mainLoadingStepPercent must be positive.");
                MethodInfo advance = typeof(MainScreen).GetMethod("AdvanceLoading", BindingFlags.Instance | BindingFlags.NonPublic);
                Require(advance != null, "Main's progress callback was not found.");
                int ticks = (int)Math.Ceiling(100 / increment);
                for (int index = 0; index < ticks; index++) advance.Invoke(main, null);
                Require(main.ProgressPercent == 100 && Find<ProgressBar>(main, "loading-progress").Value == 1 &&
                    Find<ImoniButton>(main, "start").style.display.value == DisplayStyle.Flex &&
                    Find<ImoniText>(main, "loading-label").style.display.value == DisplayStyle.None,
                    "Main did not replace the loading label with the start button at completion.");
                ClickRoute(router, Find<ImoniButton>(main, "start"), "Tutorial1");
                checks.Add("Main configured progress and start -> Tutorial1");

                var registration = Current<Tutorial1Screen>(router);
                TextField name = Find<MemberCard>(registration, "registration-card").Q<TextField>();
                Require(name != null && name.maxLength == (int)app.Number("profileNameMaxLength"), "Registration name limit is not configured.");
                const string testName = "検証いも";
                Input(name, testName);
                Require(app.Profile.Name == testName, "Name editing did not update the profile draft.");
                ClickRoute(router, Find<ImoniButton>(registration, "next"), "Tutorial2");
                Require(File.Exists(savePath), "Registration did not save the isolated profile.");
                Require(ProfilePersistence.Load(app.Data, File.ReadAllText(savePath)).Name == testName, "The registered name was not saved.");
                var tutorial2 = Current<Tutorial2Screen>(router);
                Require(!string.IsNullOrWhiteSpace(Find<TutorialTimelineStep>(tutorial2, "timeline-bonus").Time), "Tutorial2 did not bind configured times.");
                ClickRoute(router, Find<ImoniButton>(tutorial2, "next"), "Tutorial3");
                var tutorial3 = Current<Tutorial3Screen>(router);
                Require(Find<VisualElement>(tutorial3, "quality-chips").Query<QualityChip>().ToList().Count == app.Data["qualities"].Count(),
                    "Tutorial3 does not show every configured quality.");
                ClickRoute(router, Find<ImoniButton>(tutorial3, "next"), "Home");
                Require(app.Profile.TutorialCompleted, "Finishing Tutorial3 did not save tutorial completion.");
                checks.Add("Tutorial1 name save -> Tutorial2 -> Tutorial3 quality list -> Home completion save");

                var home = Current<HomeScreen>(router);
                bool originalSound = app.Profile.SoundEnabled;
                Click(Find<ImoniButton>(home, "settings"));
                Require(Find<VisualElement>(home, "settings-dialog").style.display.value == DisplayStyle.Flex, "Home settings did not open.");
                Click(Find<ImoniButton>(home, "sound-toggle"));
                Require(app.Profile.SoundEnabled != originalSound, "The sound control did not change the saved preference.");
                Click(Find<ImoniButton>(home, "settings-close"));
                Require(Find<VisualElement>(home, "settings-dialog").style.display.value == DisplayStyle.None, "Home settings did not close.");
                // M7 starts with one owned part per side. Grant just the alternatives needed by
                // this temporary UI fixture rather than treating the whole catalog as initially owned.
                string firstTitle = app.Data["titles"]["firstParts"].Values<string>().First(text => text != app.Profile.FirstTitle);
                string secondTitle = app.Data["titles"]["secondParts"].Values<string>().First(text => text != app.Profile.SecondTitle);
                app.Profile.OwnedFirstTitles = app.Profile.OwnedFirstTitles.Concat(new[] { firstTitle }).Distinct().ToArray();
                app.Profile.OwnedSecondTitles = app.Profile.OwnedSecondTitles.Concat(new[] { secondTitle }).Distinct().ToArray();
                app.SaveProfile();
                ClickRoute(router, Tab(home, "Member"), "Member");
                var member = Current<MemberScreen>(router);
                TitlePartPicker picker = Find<TitlePartPicker>(member, "title-picker");
                Click(picker.Query<Button>(className: "title-part-picker__part--first").ToList().First(button => button.text == firstTitle));
                Click(picker.Query<Button>(className: "title-part-picker__part--second").ToList().First(button => button.text == secondTitle));
                Require(app.Profile.FirstTitle == firstTitle && app.Profile.SecondTitle == secondTitle, "Owned title choices were not saved.");
                Require(picker.Query<Button>(className: "title-part-picker__part--locked").ToList().All(button => !button.enabledSelf), "Unowned title parts must be disabled.");
                AvatarEditor editor = Find<AvatarEditor>(member, "avatar-editor");
                List<VisualElement> swatchRows = editor.Query<VisualElement>(className: "avatar-editor__swatches").ToList();
                Require(swatchRows.Count == 2, "Avatar editor must have band and outfit palettes.");
                ColorSwatch band = swatchRows[0].Query<ColorSwatch>().ToList().First(swatch => swatch.Color != app.Profile.BandColor);
                ColorSwatch outfit = swatchRows[1].Query<ColorSwatch>().ToList().First(swatch => swatch.Color != app.Profile.OutfitColor);
                Click(band); Click(outfit);
                Require(app.Profile.BandColor == band.Color && app.Profile.OutfitColor == outfit.Color, "Avatar colour choices were not saved.");
                MemberCard card = Find<MemberCard>(member, "member-card"); card.Flip();
                Require(card.BackFacing, "The member card did not reveal its badges.");
                card.Flip(); Require(!card.BackFacing, "The member card did not return to the front.");
                checks.Add("Home sound/settings and Member title/colour/flip controls");

                ClickRoute(router, Tab(member, "Ranking"), "Ranking");
                var ranking = Current<RankingScreen>(router);
                Require(ranking.Query<RankingRow>().ToList().Count == 0 && Find<RankingList>(ranking, "ranking-list").EmptyText.Contains("まだ記録がありません"),
                    "A new local profile must show an empty ranking, not the old mock teams.");
                LocalRankingRecord[] rankingFixture = RankingFixture(app);
                app.Profile.Rankings = rankingFixture; app.SaveProfile();
                Require(router.Show("Ranking"), "The seeded local ranking could not reopen.");
                ranking = Current<RankingScreen>(router);
                AssertRanking(ranking, rankingFixture[0]);
                Click(Find<SegmentedControl>(ranking, "ranking-seed")[1] as Button);
                Require(Find<ImoniText>(ranking, "ranking-caption").text == "端末内 / 自由シード部門", "Free seed ranking did not update its local caption.");
                AssertRanking(ranking, rankingFixture[1]);
                Click(Find<SegmentedControl>(ranking, "ranking-mode")[1] as Button);
                AssertRanking(ranking, rankingFixture[3]);
                Click(Find<SegmentedControl>(ranking, "ranking-seed")[0] as Button);
                AssertRanking(ranking, rankingFixture[2]);
                ClickRoute(router, Tab(ranking, "Home"), "Home");
                home = Current<HomeScreen>(router);
                Require(Find<ImoniText>(home, "home-name").text == testName && Find<TitleChips>(home, "home-titles").First == firstTitle &&
                    Find<Avatar>(home, "home-avatar").BandColor == band.Color, "Home did not reflect profile edits after navigation.");
                using (var reloadRouter = new ScreenRouter(new VisualElement()))
                {
                    var reloaded = new ScreenContext(reloadRouter, savePath);
                    Require(reloaded.Profile.Name == testName && reloaded.Profile.FirstTitle == firstTitle && reloaded.Profile.SecondTitle == secondTitle &&
                        reloaded.Profile.BandColor == band.Color && reloaded.Profile.OutfitColor == outfit.Color &&
                        reloaded.Profile.SoundEnabled != originalSound && reloaded.Profile.TutorialCompleted && reloaded.Profile.Rankings.Length == rankingFixture.Length,
                        "Profile reload lost one or more edited fields or local ranking fixtures.");
                }
                checks.Add("Member/Ranking/Home tabs, empty local ranking then all four seeded boards, profile and records survive reload");

                ClickRoute(router, Find<ImoniButton>(home, "play"), "Lobby");
                var lobby = Current<LobbyScreen>(router);
                LobbyFlowConfig flow = LobbyFlowConfig.FromJson(app.Data.ToString());
                InputEntry code = Find<InputEntry>(lobby, "InviteCodeInput");
                CheckJoinError(lobby, code, string.Empty, "招待コードを入力してください");
                CheckJoinError(lobby, code, new string('X', flow.CodeMaxLength), "チームが見つかりません。コードを確かめてね");
                CheckJoinError(lobby, code, flow.BusyCode, "このチームは挑戦中のため参加できません。挑戦が終わるまで待ってね");
                Input(code, flow.SuccessCode.ToLowerInvariant());
                Require(code.value == flow.SuccessCode && Find<ErrorMessage>(lobby, "JoinError").style.display.value == DisplayStyle.None,
                    "Editing the code did not normalize case and clear the error.");
                Click(Find<ImoniButton>(lobby, "JoinTeam"));
                Require(app.JoinedTeam && app.MemberCount == flow.TeamSize && Find<VisualElement>(lobby, "LobbyTeam").style.display.value == DisplayStyle.Flex &&
                    Find<VisualElement>(lobby, "LobbyChoose").style.display.value == DisplayStyle.None, "Successful join did not show the team view.");
                ClickRoute(router, Find<ScreenHeader>(lobby, "LobbyHeader").Q<Button>(className: "screen-header__back"), "Home");
                Require(!app.JoinedTeam && app.MemberCount == 1, "Leaving the lobby did not reset team membership.");
                home = Current<HomeScreen>(router);
                ClickRoute(router, Find<ImoniButton>(home, "play"), "Lobby");
                lobby = Current<LobbyScreen>(router);
                Require(!app.JoinedTeam && app.MemberCount == 1 &&
                    Find<VisualElement>(lobby, "LobbyChoose").style.display.value == DisplayStyle.Flex &&
                    Find<VisualElement>(lobby, "LobbyTeam").style.display.value == DisplayStyle.None,
                    "Re-entering the lobby after leaving a joined team did not show team selection.");
                checks.Add("Joined lobby -> back Home -> Play restores team selection and resets membership");
                code = Find<InputEntry>(lobby, "InviteCodeInput");
                Input(code, flow.SuccessCode);
                Click(Find<ImoniButton>(lobby, "JoinTeam"));
                Require(app.JoinedTeam && app.MemberCount == flow.TeamSize, "Rejoining after leaving the lobby failed.");
                Click(Find<ImoniButton>(lobby, "ReadyButton"));
                Require(Find<VisualElement>(lobby, "ReadyActions").style.display.value == DisplayStyle.Flex, "Ready did not reveal the start/cancel actions.");
                Click(Find<ImoniButton>(lobby, "CancelReady"));
                Require(Find<VisualElement>(lobby, "ReadyActions").style.display.value == DisplayStyle.None && Find<ImoniButton>(lobby, "ReadyButton").style.display.value == DisplayStyle.Flex,
                    "Cancel-ready did not restore the ready button.");
                Click(Find<SegmentedControl>(lobby, "SeedOptions")[1] as Button);
                Require(app.FreeSeed, "The lobby seed choice did not update the context.");
                Click(Find<ImoniButton>(lobby, "ReadyButton"));
                ClickRoute(router, Find<ImoniButton>(lobby, "LobbyStart"), "Loading");
                CompleteLoading(router, app, flow);
                checks.Add("Play -> Lobby empty/bad/busy/success, ready/cancel, free seed, Loading -> Game HUD");

                app.JoinedTeam = false; app.FreeSeed = false; router.Show("Lobby");
                lobby = Current<LobbyScreen>(router);
                ClickRoute(router, Find<ImoniButton>(lobby, "CreateTeam"), "Waiting");
                var waiting = Current<WaitingScreen>(router);
                Require(Find<ImoniButton>(waiting, "WaitingStart").Disabled && app.MemberCount == 1, "A new waiting room must not start before teammates join.");
                ClickRoute(router, Find<ScreenHeader>(waiting, "WaitingHeader").Q<Button>(className: "screen-header__back"), "Lobby");
                Require(!app.JoinedTeam, "Cancel waiting did not reset team membership.");
                ClickRoute(router, Find<ImoniButton>(router.CurrentView, "CreateTeam"), "Waiting");
                waiting = Current<WaitingScreen>(router);
                waiting.Present(LobbyPresentation.Waiting(flow, flow.BotJoinSeconds.Last()));
                Require(!Find<ImoniButton>(waiting, "WaitingStart").Disabled && app.MemberCount == flow.TeamSize &&
                    waiting.Query<WaitingMemberSlot>().ToList().All(slot => slot.Filled), "A full waiting room did not enable its start action.");
                ClickRoute(router, Find<ImoniButton>(waiting, "WaitingStart"), "Loading");
                CompleteLoading(router, app, flow);
                checks.Add("Create -> Waiting disabled, cancel, configured full presentation -> Loading -> Game HUD");
                Require(router.CurrentView.Q("GameRoot") != null && app.Server != null &&
                    app.Server.World.Resources.Count > 0, "The Game route must present the generated world and HUD.");
            }
            return new JObject { ["passedChecks"] = checks, ["checkCount"] = checks.Count, ["livePlayerSaveTouched"] = false };
        }

        private static LocalRankingRecord[] RankingFixture(ScreenContext app)
        {
            int common = app.Data["outGame"]["lobby"]["commonSeed"].Value<int>();
            int clear = app.Data["round"]["roundsToClear"].Value<int>();
            var records = new List<LocalRankingRecord>();
            foreach (RankingMode mode in new[] { RankingMode.Normal, RankingMode.Endless })
                foreach (RankingSeed division in new[] { RankingSeed.Common, RankingSeed.Free })
                    records.Add(new LocalRankingRecord { ChallengeId = "m2b-validation-" + mode + "-" + division,
                        TeamName = app.TeamName, TeamSize = UiTheme.Config.TeamSize, Mode = mode, SeedDivision = division,
                        Seed = division == RankingSeed.Common ? common : unchecked(common + 1),
                        RoundNumber = mode == RankingMode.Normal ? clear : clear + 1,
                        Score = new BigInteger(12345 * (records.Count + 1)),
                        RecordedAtUtc = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero) });
            return records.ToArray();
        }
        private static void AssertRanking(RankingScreen screen, LocalRankingRecord expected)
        {
            var rows = screen.Query<RankingRow>().ToList();
            Require(rows.Count == 1 && rows[0].ScoreText == Format.Big(expected.Score, UiTheme.Config),
                "The " + expected.Mode + "/" + expected.SeedDivision + " board did not show its local record.");
        }

        private static void CompleteLoading(ScreenRouter router, ScreenContext app, LobbyFlowConfig flow)
        {
            var loading = Current<LoadingScreen>(router);
            Require(Find<ImoniButton>(loading, "LoadingStart").style.display.value == DisplayStyle.None, "Loading must hide start before completion.");
            loading.Present(LobbyPresentation.Loading(flow, flow.LoadingSeconds, 4));
            Require(Find<ProgressBar>(loading, "LoadingProgress").Value == 1 &&
                loading.Query<ChecklistStep>().ToList().All(step => step.State == ChecklistState.Done) &&
                loading.Query<LoadingMember>().ToList().All(member => member.Ready) &&
                Find<ImoniButton>(loading, "LoadingStart").style.display.value == DisplayStyle.Flex, "Completed loading did not present progress, steps, teammates, and start consistently.");
            ClickRoute(router, Find<ImoniButton>(loading, "LoadingStart"), "Game");
        }

        private static void CheckJoinError(LobbyScreen lobby, InputEntry input, string code, string expected)
        {
            Input(input, code); Click(Find<ImoniButton>(lobby, "JoinTeam"));
            ErrorMessage error = Find<ErrorMessage>(lobby, "JoinError");
            Require(error.Text == expected && error.style.display.value == DisplayStyle.Flex, "Unexpected invitation error for code '" + code + "'.");
        }

        private static JObject InspectAttached()
        {
            Require(EditorApplication.isPlaying, "Enter Play Mode with Boot open before checking attached styles.");
            Bootstrap bootstrap = UnityEngine.Object.FindFirstObjectByType<Bootstrap>();
            Require(bootstrap && bootstrap.UiDocument && bootstrap.Router != null, "The active Boot Bootstrap was not found.");
            VisualElement root = bootstrap.UiDocument.rootVisualElement;
            VisualElement host = root.Q("ScreenHost");
            Require(host != null && host.panel != null && host.resolvedStyle.width > 0 && host.resolvedStyle.height > 0, "ScreenHost is not attached with a usable layout.");
            Require(SheetCount(host, UiTheme.Assets.theme) == 1, "ScreenHost must own exactly one shared Theme reference.");
            foreach (string path in UiTheme.ComponentStyles)
            {
                StyleSheet sheet = Resources.Load<StyleSheet>(path);
                Require(sheet && SheetCount(host, sheet) == 1, "ScreenHost must own exactly one shared component sheet: " + path);
            }
            List<VisualElement> components = Descendants(bootstrap.Router.CurrentView)
                .Where(element => element.GetType().Namespace == "Imoni.Game.UI.Components").ToList();
            foreach (VisualElement component in components)
                Require(component.styleSheets.count == 0, component.GetType().Name + " owns a duplicate sheet instead of inheriting ScreenHost styles.");
            var standaloneHost = new VisualElement { pickingMode = PickingMode.Ignore };
            standaloneHost.style.position = Position.Absolute;
            standaloneHost.style.left = (float)(-2 * UiTheme.Number("referenceWidth"));
            standaloneHost.style.opacity = 0;
            var standalone = new ImoniButton { text = "検証" };
            standaloneHost.Add(standalone);
            try
            {
                root.Add(standaloneHost);
                Require(SheetCount(standalone, UiTheme.Assets.theme) == 1 &&
                    SheetCount(standalone, Resources.Load<StyleSheet>("UI/Components/Navigation")) == 1 && standalone.styleSheets.count == 2,
                    "A standalone ImoniButton did not install exactly one Theme and one component fallback sheet.");
            }
            finally { standaloneHost.RemoveFromHierarchy(); }
            return new JObject
            {
                ["screen"] = bootstrap.Router.CurrentScreen,
                ["hostWidth"] = host.resolvedStyle.width,
                ["hostHeight"] = host.resolvedStyle.height,
                ["sharedSheets"] = host.styleSheets.count,
                ["componentCount"] = components.Count,
                ["componentOwnedSheets"] = 0,
                ["standaloneFallbackSheets"] = 2
            };
        }

        private static int SheetCount(VisualElement element, StyleSheet expected)
        {
            int count = 0;
            for (int index = 0; index < element.styleSheets.count; index++)
                if (element.styleSheets[index] == expected) count++;
            return count;
        }

        private static void ClickRoute(ScreenRouter router, Button button, string expected)
        {
            ScreenView previous = router.CurrentView as ScreenView;
            IVisualElementScheduledItem[] timers = previous == null ? Array.Empty<IVisualElementScheduledItem>() : Timers(previous);
            Click(button);
            Require(router.CurrentScreen == expected, "Click did not navigate to " + expected + ".");
            if (previous != null) AssertDisposed(previous, timers);
        }
        private static Button Tab(VisualElement view, string name) => Find<TabBar>(view, "tabs").Q<Button>(name);
        private static T Current<T>(ScreenRouter router) where T : ScreenView => router.CurrentView as T ?? throw new InvalidOperationException("Expected " + typeof(T).Name + ".");
        private static T Find<T>(VisualElement root, string name) where T : VisualElement => root.Q<T>(name) ?? throw new InvalidOperationException("Missing control: " + name);
        private static IVisualElementScheduledItem[] Timers(ScreenView view) => ((IEnumerable<IVisualElementScheduledItem>)ScreenTimers.GetValue(view)).ToArray();
        private static void AssertDisposed(ScreenView view, IReadOnlyList<IVisualElementScheduledItem> timers)
        {
            Require((bool)ScreenDisposed.GetValue(view) && Timers(view).Length == 0 && timers.All(timer => !timer.isActive),
                view.GetType().Name + " left screen timers active after replacement/disposal.");
        }
        private static void Click(Button button)
        {
            Require(button != null && button.enabledInHierarchy && button.clickable != null, "An expected enabled button is missing.");
            MethodInfo invoke = typeof(Clickable).GetMethod("Invoke", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic,
                null, new[] { typeof(EventBase) }, null);
            Require(invoke != null, "Clickable.Invoke is unavailable for detached intent checks.");
            using (ClickEvent click = ClickEvent.GetPooled()) { click.target = button; invoke.Invoke(button.clickable, new object[] { click }); }
        }
        private static void Input(TextField field, string value)
        {
            string previous = field.value;
            field.SetValueWithoutNotify(value);
            // BaseField.value only emits ChangeEvent automatically while attached to a panel.
            using (ChangeEvent<string> change = ChangeEvent<string>.GetPooled(previous, field.value))
            {
                change.target = field;
                field.SendEvent(change);
            }
        }
        private sealed class AttachedValidationHost : IDisposable
        {
            public VisualElement Host { get; }
            public AttachedValidationHost()
            {
                Bootstrap bootstrap = UnityEngine.Object.FindFirstObjectByType<Bootstrap>();
                Require(EditorApplication.isPlaying && bootstrap && bootstrap.UiDocument && bootstrap.UiDocument.rootVisualElement.panel != null,
                    "Open Boot in Play Mode before validating input flows; Unity does not dispatch field changes on detached trees.");
                Host = new VisualElement { name = "M2BValidationHost", pickingMode = PickingMode.Ignore, focusable = false };
                Host.style.position = Position.Absolute;
                Host.style.left = (float)(-2 * UiTheme.Number("referenceWidth"));
                Host.style.top = (float)(-2 * UiTheme.Number("referenceHeight"));
                Host.style.width = (float)UiTheme.Number("referenceWidth");
                Host.style.height = (float)UiTheme.Number("referenceHeight");
                Host.style.opacity = 0;
                UiTheme.InstallRoot(Host);
                bootstrap.UiDocument.rootVisualElement.Add(Host);
                Require(Host.panel != null, "The hidden validation host could not attach to the Boot panel.");
            }
            public void Dispose() => Host.RemoveFromHierarchy();
        }
        private static IEnumerable<VisualElement> Descendants(VisualElement root)
        {
            if (root == null) yield break;
            yield return root;
            for (int index = 0; index < root.hierarchy.childCount; index++)
                foreach (VisualElement child in Descendants(root.hierarchy[index])) yield return child;
        }
        private static string LibraryPath(string name) => Path.GetFullPath(Path.Combine(Application.dataPath, "../Library/" + name));
        private static void Require(bool condition, string message)
        { if (!condition) throw new InvalidOperationException("M2b screen validation: " + message); }
    }
}
