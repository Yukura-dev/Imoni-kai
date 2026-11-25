using System;
using Imoni.Core;
using Imoni.Game.UI.Components;
using Newtonsoft.Json.Linq;
using UnityEngine;
using UnityEngine.UIElements;
using Avatar = Imoni.Game.UI.Components.Avatar;
using ProgressBar = Imoni.Game.UI.Components.ProgressBar;

namespace Imoni.Game.UI.Screens
{
    internal static class LobbyScreenData
    {
        internal static LobbyFlowConfig Config(ScreenContext app) => LobbyFlowConfig.FromJson(app.Data.ToString());
        internal static string CommonSeed(ScreenContext app) => app.Data["outGame"]["lobby"]["commonSeedLabel"].Value<string>();
        internal static string Seed(ScreenContext app) => app.FreeSeed ? "自由シード" : "共通シード " + CommonSeed(app);
        internal static MemberRowViewModel Member(ScreenContext app, int index, StatusBadgeState state)
        {
            if (index == 0) return new MemberRowViewModel
            {
                Name = app.Profile.Name + "(あなた)", First = app.Profile.FirstTitle, Second = app.Profile.SecondTitle,
                BandColor = app.Profile.BandColor, OutfitColor = app.Profile.OutfitColor, State = state
            };
            return index == 1
                ? new MemberRowViewModel { Name = "たろう", First = "伝説の", Second = "鍋奉行", OutfitColor = "#C9A27A", State = state }
                : new MemberRowViewModel { Name = "はなこ", First = "山の", Second = "採集王", OutfitColor = "#6B9B3A", State = state };
        }
        internal static void Show(VisualElement element, bool visible) => element.style.display = visible ? DisplayStyle.Flex : DisplayStyle.None;
        internal static void SetAvatar(VisualElement row, int index)
        {
            Avatar avatar = row.Q<Avatar>();
            if (avatar == null) return;
            // HTML's 44px avatar plus 3px border corresponds to a 50px border-box in UI Toolkit.
            avatar.Size = 50;
            avatar.AddToClassList("avatar--lobby-placeholder");
            avatar.style.backgroundColor = UiTheme.Color(index == 0 ? "#8FAFC4" : index == 1 ? "#C9A27A" : "#6B9B3A");
        }
    }

    public sealed class LobbyScreen : ScreenView
    {
        private readonly LobbyFlowConfig config;
        private readonly InputEntry code;
        private readonly ErrorMessage error;
        private readonly MemberRow[] members;
        private IVisualElementScheduledItem copyReset;
        private bool ready;
        public LobbyScreen(ScreenContext app) : base(app, "UI/Lobby", "UI/LobbyScreens")
        {
            config = LobbyScreenData.Config(app);
            code = Find<InputEntry>("InviteCodeInput"); code.maxLength = config.CodeMaxLength;
            code.AddToClassList("lobby-code-input--empty");
            error = Find<ErrorMessage>("JoinError"); LobbyScreenData.Show(error, false);
            Find<ScreenHeader>("LobbyHeader").BackRequested += () => { App.JoinedTeam = false; App.MemberCount = 1; App.Go("Home"); };
            Find<ImoniText>("CreateDescription").text = "新しいチームを作って、招待コードで仲間を呼ぼう(最大" + config.TeamSize + "人)";
            Find<ImoniText>("MockCodeHint").text = "モック用:" + config.SuccessCode + " で参加 / " + config.BusyCode + " は挑戦中のチーム";
            code.RegisterValueChangedCallback(change =>
            {
                code.SetValueWithoutNotify((change.newValue ?? string.Empty).ToUpperInvariant());
                code.EnableInClassList("lobby-code-input--empty", string.IsNullOrEmpty(change.newValue));
                error.Text = string.Empty; LobbyScreenData.Show(error, false);
            });
            Find<ImoniButton>("CreateTeam").clicked += () => { App.JoinedTeam = false; App.MemberCount = 1; App.InviteCode = config.SuccessCode; App.Go("Waiting"); };
            Find<ImoniButton>("JoinTeam").clicked += Join;
            members = new[] { Find<MemberRow>("TeamMember0"), Find<MemberRow>("TeamMember1"), Find<MemberRow>("TeamMember2") };
            InviteCodeCard invite = Find<InviteCodeCard>("TeamCode"); invite.TeamName = App.TeamName; invite.Code = App.InviteCode;
            Find<ImoniText>("TeamMemberCount").text = "メンバー " + config.TeamSize + "/" + config.TeamSize;
            ImoniButton copy = Find<ImoniButton>("CopyTeamCode");
            copy.clicked += () =>
            {
                GUIUtility.systemCopyBuffer = App.InviteCode; copy.text = "コピーしました!";
                copyReset?.Pause(); copyReset = Later(() => copy.text = "招待コードをコピー", App.Number("inviteCopiedSeconds"));
            };
            SegmentedControl seed = Find<SegmentedControl>("SeedOptions");
            seed.Options = "共通シード\n<size=10>今週 " + LobbyScreenData.CommonSeed(App) + "・共通シード部門</size>|自由シード\n<size=10>ランダム生成・自由シード部門</size>";
            seed.Selected = App.FreeSeed ? 1 : 0; seed.Changed += selected => App.FreeSeed = selected != 0;
            Find<ImoniText>("ModeDescription").text = LobbyPresentation.ModeDescription(UiTheme.Config);
            Find<ImoniButton>("ReadyButton").clicked += () => { ready = true; RefreshTeam(); };
            Find<ImoniButton>("CancelReady").clicked += () => { ready = false; RefreshTeam(); };
            Find<ImoniButton>("LobbyStart").clicked += () => { if (ready) App.Go("Loading"); };
            RefreshTeam();
        }
        private void Join()
        {
            InviteJoinResult result = LobbyPresentation.Join(config, code.value);
            if (result == InviteJoinResult.Success)
            { App.JoinedTeam = true; App.MemberCount = config.TeamSize; App.InviteCode = config.SuccessCode; ready = false; RefreshTeam(); return; }
            error.Text = result == InviteJoinResult.Empty ? "招待コードを入力してください"
                : result == InviteJoinResult.InProgress ? "このチームは挑戦中のため参加できません。挑戦が終わるまで待ってね"
                : "チームが見つかりません。コードを確かめてね";
            LobbyScreenData.Show(error, true);
        }
        private void RefreshTeam()
        {
            LobbyScreenData.Show(Find<VisualElement>("LobbyChoose"), !App.JoinedTeam);
            LobbyScreenData.Show(Find<VisualElement>("LobbyTeam"), App.JoinedTeam);
            LobbyScreenData.Show(Find<ImoniButton>("ReadyButton"), !ready);
            LobbyScreenData.Show(Find<VisualElement>("ReadyActions"), ready);
            for (int index = 0; index < members.Length; index++)
            {
                members[index].Bind(LobbyScreenData.Member(App, index, index == 0 && !ready ? StatusBadgeState.Waiting : StatusBadgeState.Ready));
                LobbyScreenData.SetAvatar(members[index], index);
            }
        }
    }

    public sealed class WaitingScreen : ScreenView
    {
        private readonly LobbyFlowConfig config;
        private readonly WaitingMemberSlot[] slots;
        private readonly ImoniButton start;
        private double began;
        private bool full;
        private double becameFull;
        public WaitingScreen(ScreenContext app) : base(app, "UI/Waiting", "UI/LobbyScreens")
        {
            config = LobbyScreenData.Config(app); began = Time.realtimeSinceStartupAsDouble;
            App.MemberCount = 1;
            ScreenHeader header = Find<ScreenHeader>("WaitingHeader");
            header.BackRequested += () => { App.JoinedTeam = false; App.MemberCount = 1; App.Go("Lobby"); };
            var seed = new ImoniText { text = LobbyScreenData.Seed(App) }; seed.AddToClassList("waiting-seed-chip"); header.RightContent.Add(seed);
            InviteCodeCard invite = Find<InviteCodeCard>("WaitingCode"); invite.TeamName = App.TeamName; invite.Code = App.InviteCode;
            invite.CopyRequested += () => { GUIUtility.systemCopyBuffer = App.InviteCode; invite.ShowCopied(); };
            Toast toast = Find<Toast>("WaitingToast"); LobbyScreenData.Show(toast, false);
            invite.ShareRequested += () =>
            {
                GUIUtility.systemCopyBuffer = "チーム「" + App.TeamName + "」 招待コード:" + App.InviteCode;
                toast.Show("招待コードをコピーしました!", App.Number("toastSeconds"));
            };
            slots = new[] { Find<WaitingMemberSlot>("WaitingMember0"), Find<WaitingMemberSlot>("WaitingMember1"), Find<WaitingMemberSlot>("WaitingMember2") };
            for (int index = 0; index < slots.Length; index++)
            { slots[index].Bind(LobbyScreenData.Member(App, index, index == 0 ? StatusBadgeState.Host : StatusBadgeState.Joined)); LobbyScreenData.SetAvatar(slots[index], index); }
            start = Find<ImoniButton>("WaitingStart"); start.clicked += () => { if (full) { App.JoinedTeam = true; App.Go("Loading"); } };
            RegisterCallback<AttachToPanelEvent>(_ => began = Time.realtimeSinceStartupAsDouble);
            Refresh(); Every(Refresh, (long)App.Number("animationFrameMs"));
        }
        private void Refresh()
        {
            WaitingPresentation view = LobbyPresentation.Waiting(config, (decimal)Math.Max(0, Time.realtimeSinceStartupAsDouble - began));
            Present(view);
        }
        public void Present(WaitingPresentation view)
        {
            if (view == null) throw new ArgumentNullException(nameof(view));
            App.MemberCount = view.MemberCount;
            if (view.Full && !full) becameFull = Time.realtimeSinceStartupAsDouble;
            full = view.Full;
            for (int index = 0; index < slots.Length; index++) { slots[index].Filled = index < view.MemberCount; slots[index].Dots = new string('.', view.DotCount); }
            Find<ImoniText>("WaitingMemberCount").text = view.MemberCount + "/" + config.TeamSize + "人";
            Find<ImoniText>("WaitingHeadline").text = view.Full ? "全員そろったよ!" : "メンバーを待っています";
            Find<ImoniText>("WaitingSubline").text = view.Full ? "準備ができたらスタートしよう" : "チームは" + config.TeamSize + "人で挑戦するよ";
            start.Disabled = !view.Full; start.text = view.Full ? "全員そろった!スタート" : "あと" + view.Remaining + "人そろうとスタートできます";
            if (full) LobbyMotion.Pop(start, becameFull);
        }
    }

    public sealed class LoadingScreen : ScreenView
    {
        private readonly string[] tips;
        private readonly LobbyFlowConfig config;
        private readonly ChecklistStep[] steps;
        private readonly LoadingMember[] members;
        private readonly string[] notes;
        private double began;
        private bool ready;
        private double becameReady;
        public LoadingScreen(ScreenContext app) : base(app, "UI/Loading", "UI/LobbyScreens")
        {
            App.PrepareGame();
            tips = new[] { "資源をタップすると、自動で歩いて採集してくれるよ", "最高品質を続けて引くと、獲得数とポイントが倍々に増える!",
                "売却した素材は芋煮会のスコアに入らないよ。ロックで守ろう", LobbyPresentation.BonusHint(UiTheme.Config) };
            config = LobbyScreenData.Config(app); began = Time.realtimeSinceStartupAsDouble;
            Find<ImoniText>("LoadingTeam").text = "チーム「" + App.TeamName + "」 · " + LobbyScreenData.Seed(App);
            steps = new[] { Find<ChecklistStep>("LoadingStep0"), Find<ChecklistStep>("LoadingStep1"), Find<ChecklistStep>("LoadingStep2"), Find<ChecklistStep>("LoadingStep3"), Find<ChecklistStep>("LoadingStep4") };
            notes = new[] { App.FreeSeed ? "自由シード" : LobbyScreenData.CommonSeed(App), "平野・森林・河川", App.Server.World.Resources.Count + "か所", "必須素材OK", config.TeamSize + "/" + config.TeamSize + "人" };
            members = new[] { Find<LoadingMember>("LoadingMember0"), Find<LoadingMember>("LoadingMember1"), Find<LoadingMember>("LoadingMember2") };
            for (int index = 0; index < members.Length; index++)
            {
                MemberRowViewModel model = LobbyScreenData.Member(App, index, StatusBadgeState.Waiting);
                members[index].BandColor = model.BandColor; members[index].OutfitColor = model.OutfitColor; LobbyScreenData.SetAvatar(members[index], index);
            }
            Find<ImoniButton>("LoadingStart").clicked += () => { if (ready) App.Go("Game"); };
            var hintOutline = new LobbyDashedOutline(false, 16, 3, "#4A2E22"); hintOutline.AddToClassList("lobby-dashed-overlay");
            Find<Card>("LoadingHint").hierarchy.Insert(0, hintOutline);
            RegisterCallback<AttachToPanelEvent>(_ => began = Time.realtimeSinceStartupAsDouble);
            Refresh(); Every(Refresh, (long)App.Number("animationFrameMs"));
        }
        private void Refresh()
        {
            LoadingPresentation view = LobbyPresentation.Loading(config, (decimal)Math.Max(0, Time.realtimeSinceStartupAsDouble - began), tips.Length);
            Present(view);
        }
        public void Present(LoadingPresentation view)
        {
            if (view == null) throw new ArgumentNullException(nameof(view));
            if (view.Ready && !ready) becameReady = Time.realtimeSinceStartupAsDouble;
            ready = view.Ready;
            for (int index = 0; index < steps.Length; index++)
            {
                ChecklistState state = view.Steps[index] == LoadingStepState.Done ? ChecklistState.Done : view.Steps[index] == LoadingStepState.Active ? ChecklistState.Active : ChecklistState.Pending;
                if (steps[index].State != state) steps[index].State = state;
                steps[index].Note = state == ChecklistState.Done ? notes[index] : string.Empty;
            }
            for (int index = 0; index < members.Length; index++) members[index].Ready = index < view.SyncedMembers;
            Find<ProgressBar>("LoadingProgress").Value = (float)view.Progress;
            Find<ImoniText>("LoadingPercent").text = view.Percent + "%";
            Find<ImoniText>("LoadingHeadline").text = ready ? "準備ができたよ!" : "ワールドを生成中…";
            Find<ImoniText>("LoadingTip").text = tips[view.TipIndex];
            LobbyScreenData.Show(Find<Card>("LoadingHint"), !ready);
            LobbyScreenData.Show(Find<ImoniButton>("LoadingStart"), ready);
            if (ready) LobbyMotion.Pop(Find<ImoniButton>("LoadingStart"), becameReady);
        }
    }
}
