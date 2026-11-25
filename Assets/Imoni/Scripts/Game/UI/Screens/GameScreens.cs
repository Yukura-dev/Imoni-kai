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
#if UNITY_EDITOR
        public bool ReviewPaused { get; set; }
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
            ImoniText bonusStatus = Find<ImoniText>("GameBonusStatus"); bonusStatus.text = Hud.BonusStatusText;
            bonusStatus.style.display = Hud.BonusRevealed ? DisplayStyle.Flex : DisplayStyle.None;
            Find<ImoniText>("GameStreak").text = Hud.StreakText;
            Find<ImoniText>("GameStreakMultiplier").text = Hud.StreakMultiplierText;
            UpdateHistory();
        }

        private void UpdateHistory()
        {
            RoundEvent roundEvent = Snapshot.Events.LastOrDefault(item => item.Sequence > lastEvent);
            OperationRecord gather = Snapshot.History.LastOrDefault(item => item.Sequence > lastOperation && item.Kind == OperationKind.Gather);
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
            WorldView?.Dispose(); WorldView = null;
            if (themeRoot != null) themeRoot.style.backgroundColor = previousBackground;
        }
    }
}
