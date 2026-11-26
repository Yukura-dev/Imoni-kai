using System;
using System.Collections.Generic;
using System.Linq;
using Imoni.Core;
using Imoni.Game.UI.Components;
using Imoni.Game.World;
using Newtonsoft.Json.Linq;
using UnityEngine;
using UnityEngine.UIElements;

namespace Imoni.Game.UI.Screens
{
    /// <summary>Composition of reusable controls plus the world viewport. All game decisions are projected by Core.</summary>
    public sealed class GameScreen : ScreenView
    {
        private readonly List<MaterialGoalChip> goals = new List<MaterialGoalChip>();
        private readonly VisualElement viewport;
        private readonly Toast toast;
        private VisualElement themeRoot;
        private StyleColor previousBackground;
        private long lastOperation;
        private long lastEvent;
        public IGameServer Server => App.Server;
        public GameSnapshot Snapshot { get; private set; }
        public GameHudView Hud { get; private set; }
        public GameWorldView WorldView { get; private set; }
        public string LocalActorId { get; private set; }
        public GamePanels Panels { get; }
        public RoundTimeUpDialog TimeUpDialog { get; }
        public ReconnectNotice ReconnectDialog { get; }
        public bool IsReconnecting => Snapshot.Phase == RoundPhase.Collecting && !Snapshot.Players.First(player => player.Id == LocalActorId).Connected;
        public bool IsTimeUp => Snapshot.LastResult != null && (Snapshot.Phase == RoundPhase.Succeeded || Snapshot.Phase == RoundPhase.Failed || Snapshot.Phase == RoundPhase.Cleared);
        public string CurrentPanel => Panels.CurrentPanel;
        public void OpenPanel(string panelName) { if (Snapshot.Phase == RoundPhase.Collecting && !IsReconnecting) Panels.Open(panelName); }
        public void ClosePanel() => Panels.Close();
#if UNITY_EDITOR
        public bool ReviewPaused { get; set; }
#endif
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        private IVisualElementScheduledItem reconnectJob;
        private string reconnectActor, reconnectResource;
        private GridPosition? reconnectGround;
        public bool ReconnectDemoPending => reconnectActor != null;

        /// <summary>Development-only connection loss; all mutations still pass through IGameServer.</summary>
        public void BeginReconnectDemo(string actorId = null)
        {
            if (Snapshot.Phase != RoundPhase.Collecting || reconnectActor != null) return;
            actorId = actorId ?? LocalActorId;
            PlayerSnapshot actor = Snapshot.Players.First(player => player.Id == actorId);
            if (!actor.Connected) return;
            reconnectActor = actorId; reconnectResource = !actor.IsBot ? actor.TargetResourceId : null;
            reconnectGround = actorId == LocalActorId && actor.Action == PlayerAction.Moving && actor.TargetResourceId == null ? WorldView?.LastTappedGround : null;
            Server.SetConnected(actorId, false);
            if (actorId == LocalActorId) Panels.Close();
            RefreshSnapshot();
            reconnectJob = schedule.Execute(FinishReconnectDemo).StartingIn((long)(App.Number("reconnectDemoSeconds") * TimeSpan.FromSeconds(1).TotalMilliseconds));
        }

        private void FinishReconnectDemo()
        {
            if (reconnectActor == null) return;
            string actorId = reconnectActor; reconnectActor = null;
            Server.SetConnected(actorId, true);
            if (Server.Snapshot().Phase == RoundPhase.Collecting)
            {
                if (reconnectResource != null) Server.TrySelectResource(actorId, reconnectResource);
                else if (reconnectGround.HasValue) Server.TryMoveTo(actorId, reconnectGround.Value);
            }
            reconnectResource = null; reconnectGround = null; RefreshSnapshot();
            if (!Panels.IsOpen && Snapshot.Phase == RoundPhase.Collecting) toast.Show("再接続しました", App.Number("toastSeconds"));
        }
#endif

        public GameScreen(ScreenContext app) : base(app, "UI/Game", "UI/GameScreen")
        {
            if (App.Server == null) App.PrepareGame();
            Snapshot = Server.Snapshot();
            LocalActorId = Snapshot.Players.First(player => !player.IsBot).Id;
            viewport = Find<VisualElement>("GameMapViewport");
            toast = Find<Toast>("GameToast");
            toast.style.display = DisplayStyle.None;
            // Transparent overlay labels must not intercept a resource/ground tap.
            foreach (VisualElement element in Find<VisualElement>("GameOverlay").Query<VisualElement>().ToList())
                element.pickingMode = PickingMode.Ignore;
            JToken layout = App.Data["ui"]["worldView"];
            float hud = layout["hudHeightPx"].Value<float>(), bottom = layout["bottomHeightPx"].Value<float>();
            Find<VisualElement>("GameHud").style.height = hud;
            Find<VisualElement>("GameActions").style.height = bottom;
            viewport.style.top = hud; viewport.style.bottom = bottom;
            Find<VisualElement>("GameOverlay").style.top = hud;
            Find<VisualElement>("GameOverlay").style.bottom = bottom;
            Panels = new GamePanels(this, app); Find<VisualElement>("GameRoot").Add(Panels);
            Panels.PanelChanged += OnPanelChanged;
            Find<ImoniButton>("OpenWarehouse").clicked += () => TogglePanel("warehouse");
            Find<ImoniButton>("OpenShop").clicked += () => TogglePanel("shop");
            Find<ImoniButton>("OpenUpgrades").clicked += () => TogglePanel("upgrade");
            Find<ImoniButton>("OpenGameMenu").clicked += () => TogglePanel("menu");
            TimeUpDialog = new RoundTimeUpDialog { name = "GameTimeUp" };
            TimeUpDialog.style.display = DisplayStyle.None;
            TimeUpDialog.ResultsButton.clicked += () => { if (IsTimeUp) App.Go(Snapshot.LastResult.Evaluation.Success ? "Imoni" : "ImoniFail"); };
            Find<VisualElement>("GameRoot").Add(TimeUpDialog);
            ReconnectDialog = new ReconnectNotice();
            ReconnectDialog.ReturnHomeRequested += () => { Server.AbortChallenge(LocalActorId); App.Go("Home"); };
            Find<VisualElement>("GameRoot").Add(ReconnectDialog);
            RegisterCallback<AttachToPanelEvent>(_ => AttachWorld());
            RefreshSnapshot();
        }

        private void AttachWorld()
        {
            for (VisualElement parent = hierarchy.parent; parent != null; parent = parent.hierarchy.parent)
                if (parent.ClassListContains("imoni-app"))
                {
                    themeRoot = parent; previousBackground = parent.style.backgroundColor;
                    parent.style.backgroundColor = Color.clear; break;
                }
            // Isolated UI validation has no camera/Bootstrap and still presents the authoritative HUD.
            if (WorldView == null && App.Bootstrap != null)
                WorldView = new GameWorldView(Server, App.Bootstrap, viewport, App.Profile);
            OnPanelChanged();
            RefreshSnapshot();
        }

        public void Advance(float seconds)
        {
#if UNITY_EDITOR
            if (ReviewPaused) return;
#endif
            if (float.IsNaN(seconds) || float.IsInfinity(seconds) || seconds <= 0) return;
            if (Snapshot.Phase == RoundPhase.Collecting) Server.Tick((decimal)seconds);
            RefreshSnapshot(seconds);
        }

        public void RefreshSnapshot(float visualDeltaSeconds = 0)
        {
            Snapshot = Server.Snapshot();
            WorldView?.Present(Snapshot, visualDeltaSeconds);
            Hud = GameHudProjection.Present(UiTheme.Config, Snapshot, LocalActorId, (decimal)App.Number("timerUrgentSeconds"));
            Find<ImoniText>("GameRound").text = Hud.RoundText;
            ImoniText timer = Find<ImoniText>("GameTimer"); timer.text = Hud.TimerText;
            timer.EnableInClassList("game-timer--urgent", Hud.TimerUrgent);
            Find<FundsPill>("GameFunds").Bind(Snapshot.Funds);
            VisualElement required = Find<VisualElement>("GameRequired");
            while (goals.Count > Hud.Required.Count) { goals[goals.Count - 1].RemoveFromHierarchy(); goals.RemoveAt(goals.Count - 1); }
            while (goals.Count < Hud.Required.Count) { var goal = new MaterialGoalChip(); goals.Add(goal); required.Add(goal); }
            for (int index = 0; index < goals.Count; index++) goals[index].Bind(Hud.Required[index]);
            Find<ImoniText>("GameBonus").text = Hud.BonusText;
            Find<ImoniText>("GameBonus").tooltip = Hud.BonusExactText;
            ImoniText bonusStatus = Find<ImoniText>("GameBonusStatus"); bonusStatus.text = Hud.BonusStatusText;
            bonusStatus.style.display = Hud.BonusRevealed ? DisplayStyle.Flex : DisplayStyle.None;
            Find<ImoniText>("GameStreak").text = Hud.StreakText;
            Find<ImoniText>("GameStreakMultiplier").text = Hud.StreakMultiplierText;
            Panels?.Refresh(Snapshot);
            ReconnectDialog.style.display = IsReconnecting ? DisplayStyle.Flex : DisplayStyle.None;
            Find<VisualElement>("GameActions").SetEnabled(!IsReconnecting);
            if (IsTimeUp)
            {
                if (Panels.IsOpen) Panels.Close();
                TimeUpDialog.TimeText = GameHudProjection.Clock(Snapshot.LastResult.ElapsedSeconds) + " タイムアップ";
                TimeUpDialog.style.display = DisplayStyle.Flex;
            }
            if (WorldView != null) WorldView.InputEnabled = Snapshot.Phase == RoundPhase.Collecting && !Panels.IsOpen && !IsReconnecting;
            UpdateHistory();
        }

        private void TogglePanel(string panelName)
        { if (Snapshot.Phase == RoundPhase.Collecting && !IsReconnecting) Panels.Toggle(panelName); }

        private void OnPanelChanged()
        {
            if (WorldView != null) WorldView.InputEnabled = Snapshot.Phase == RoundPhase.Collecting && !Panels.IsOpen && !IsReconnecting;
            Find<ImoniButton>("OpenWarehouse").Selected = CurrentPanel == "warehouse";
            Find<ImoniButton>("OpenShop").Selected = CurrentPanel == "shop";
            Find<ImoniButton>("OpenUpgrades").Selected = CurrentPanel == "upgrade";
            Find<ImoniButton>("OpenGameMenu").Selected = CurrentPanel == "menu";
            if (Panels.IsOpen) toast.style.display = DisplayStyle.None;
        }

        private void UpdateHistory()
        {
            RoundEvent roundEvent = Snapshot.Events.LastOrDefault(item => item.Sequence > lastEvent);
            OperationRecord gather = Snapshot.History.LastOrDefault(item => item.Sequence > lastOperation && item.Kind == OperationKind.Gather);
            if ((Panels != null && Panels.IsOpen) || Snapshot.Phase != RoundPhase.Collecting || IsReconnecting)
            {
                if (Snapshot.Events.Count > 0) lastEvent = Snapshot.Events.Max(item => item.Sequence);
                if (Snapshot.History.Count > 0) lastOperation = Snapshot.History.Max(item => item.Sequence);
                toast.style.display = DisplayStyle.None;
                return;
            }
            if (roundEvent != null)
            {
                lastEvent = roundEvent.Sequence;
                if (roundEvent.Kind == RoundEventKind.Started)
                    toast.Show("[0:00] ラウンド" + Snapshot.RoundNumber + "開始!必須素材が公開されたよ", App.Number("toastSeconds"));
                else if (roundEvent.Kind == RoundEventKind.BonusRevealed && Snapshot.BonusObjective != null)
                    toast.Show("ボーナス素材「" + UiTheme.Config.Material(Snapshot.BonusObjective.MaterialId).Name + "」が発表された!", App.Number("toastSeconds"));
            }
            if (gather != null)
            {
                lastOperation = gather.Sequence;
                int index = Snapshot.Players.ToList().FindIndex(player => player.Id == gather.ActorId);
                string name = index == 0 ? "あなた" : LobbyScreenData.Member(App, index, StatusBadgeState.Collecting).Name;
                toast.Show(GameHudProjection.GatherHistory(UiTheme.Config, gather, name), App.Number("toastSeconds"));
            }
        }

        protected override void OnDispose()
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            reconnectJob?.Pause();
#endif
            WorldView?.Dispose(); WorldView = null;
            if (themeRoot != null) themeRoot.style.backgroundColor = previousBackground;
        }
    }
}
