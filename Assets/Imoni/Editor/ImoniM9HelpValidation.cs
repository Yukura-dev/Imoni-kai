using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Imoni.Core;
using Imoni.Game;
using Imoni.Game.UI;
using Imoni.Game.UI.Components;
using Imoni.Game.UI.Screens;
using Imoni.Game.World;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace Imoni.Editor
{
    /// <summary>Explicit, frame-separated checks of the real Boot help handlers using a Library-only review save.</summary>
    [InitializeOnLoad]
    public static class ImoniM9HelpValidation
    {
        private const string Request = "Library/M9HelpReview.request";
        private const string Result = "Library/M9HelpReview.result";
        private const string Report = "Library/M9-help-validation.json";
        private static readonly Queue<Action> steps = new Queue<Action>();
        private static Bootstrap boot;
        private static JObject report;
        private static JArray checks;
        private static JArray overlayEvents;
        private static double nextAt, started;
        private static int previousFrame;
        private static VisualElement home;
        private static HelpScreen help;
        private static SettingsDialog settings;
        private static GameScreen game;
        private static GameWorldView world;
        private static IGameServer server;
        private static decimal elapsedBefore;
        private static string accountBefore, profileBefore, selectedBefore, quantityBefore;
        private static string[] accountsBefore;
        private static byte[] saveBefore;
        private static Action<string> overlayListener;
        public static bool IsRunning { get; private set; }
        static ImoniM9HelpValidation() { EditorApplication.update += Update; }

        public static void Begin()
        {
            Require(!IsRunning, "Help validation is already running.");
            boot = UnityEngine.Object.FindFirstObjectByType<Bootstrap>();
            Require(EditorApplication.isPlaying && boot?.Context != null, "Open Boot in Play Mode first.");
            string library = Path.GetFullPath("Library") + Path.DirectorySeparatorChar;
            Require(Path.GetFullPath(boot.Context.SavePath).StartsWith(library, StringComparison.OrdinalIgnoreCase),
                "Help validation requires Imoni.ReviewSavePath inside Library. It never navigates a real saved session.");
            checks = new JArray(); overlayEvents = new JArray();
            report = new JObject { ["status"] = "running", ["passed"] = false,
                ["validatedAtUtc"] = DateTime.UtcNow.ToString("O"), ["unityVersion"] = Application.unityVersion,
                ["isolatedSavePath"] = boot.Context.SavePath,
                ["scope"] = "Actual attached UI handlers, real Bootstrap Update across frames, real map input gate. Review save only; no touch hit-area or Android hardware claim.",
                ["checks"] = checks, ["overlayEvents"] = overlayEvents };
            overlayListener = screen => overlayEvents.Add(new JObject { ["visible"] = screen,
                ["underlying"] = boot.Router.CurrentScreen, ["source"] = boot.Router.OverlaySourceScreen });
            boot.Router.OverlayChanged += overlayListener;
            EnqueueScenario(); started = nextAt = EditorApplication.timeSinceStartup;
            previousFrame = Time.frameCount; IsRunning = true;
            File.WriteAllText(Report, report.ToString());
        }

        private static void Update()
        {
            if (EditorApplication.isCompiling || EditorApplication.isUpdating) return;
            try
            {
                if (!IsRunning && File.Exists(Request))
                { File.Delete(Request); Begin(); }
                if (!IsRunning) return;
                Require(EditorApplication.isPlaying && boot?.Context != null, "Play Mode ended during Help validation.");
                Require(EditorApplication.timeSinceStartup - started < 90, "Help validation timed out.");
                if (EditorApplication.timeSinceStartup < nextAt || Time.frameCount < previousFrame + 3) return;
                if (steps.Count == 0) { Finish(null); return; }
                steps.Dequeue()(); previousFrame = Time.frameCount; nextAt = EditorApplication.timeSinceStartup + .2;
            }
            catch (Exception exception) { Finish(exception); }
        }

        private static void EnqueueScenario()
        {
            steps.Clear();
            steps.Enqueue(() =>
            {
                boot.Router.Show("Home"); home = boot.Router.CurrentView;
                Click(Find<Button>(home, "help")); help = Overlay();
                Check("Home ? opens first section without replacing Home", help.SectionIndex == 0 && ReferenceEquals(home, boot.Router.CurrentView));
                Check("Previous is disabled on first section", !Find<Button>(help, "HelpPrevious").enabledInHierarchy);
                var card = Find<HelpPageCard>(help, "HelpCard");
                // Exercise the supported small-height case so scrolling can be verified on every review device.
                card.style.height = 250; card.style.flexGrow = 0; card.style.flexShrink = 0;
            });
            steps.Enqueue(() =>
            {
                ScrollView body = help.Q<HelpPageCard>().BodyScroll;
                Check("Only body card can scroll on small displays", body.verticalScroller.highValue > 40);
                body.scrollOffset = new Vector2(0, 40);
            });
            steps.Enqueue(() =>
            {
                ScrollView body = help.Q<HelpPageCard>().BodyScroll;
                Check("Body scroll is real after layout", body.scrollOffset.y > 0);
                Click(Find<Button>(help, "HelpTab_1"));
                Check("Tab navigation resets body scroll", help.SectionIndex == 1 && body.scrollOffset.y == 0);
                Click(Find<Button>(help, "HelpPrevious"));
                Check("Previous returns to first section and disables itself", help.SectionIndex == 0 && !Find<Button>(help, "HelpPrevious").enabledInHierarchy);
                for (int index = 1; index < HelpContent.Load().Sections.Length; index++)
                {
                    Click(Find<Button>(help, "HelpNext"));
                    Check("Next opens section " + index, help.SectionIndex == index);
                }
                Check("Final button says わかった!", Find<Button>(help, "HelpNext").text == HelpContent.Load().DoneText);
                for (int index = 0; index < HelpContent.Load().Sections.Length; index++)
                {
                    Click(Find<Button>(help, "HelpTab_" + index));
                    Check("Tab selects section " + index, help.SectionIndex == index);
                }
                Click(Find<Button>(help, "HelpNext"));
                Check("Final button restores original Home", !boot.Router.HasOverlay && ReferenceEquals(home, boot.Router.CurrentView) && home.enabledInHierarchy);
            });
            steps.Enqueue(() =>
            {
                Click(Find<Button>(home, "settings")); settings = Find<SettingsDialog>(home, "settings-dialog");
                boot.Context.SaveProfile();
                accountBefore = boot.Context.Accounts.ActiveId; accountsBefore = boot.Context.Accounts.Ids.ToArray();
                profileBefore = ProfilePersistence.Save(UiTheme.Data, boot.Context.Profile);
                saveBefore = File.ReadAllBytes(boot.Context.SavePath);
                Click(Find<Button>(settings, "account-switch"));
                Find<InputEntry>(settings, "account-name").value = "入力途中";
                Click(Find<Button>(settings, "account-back"));
                Click(Find<Button>(settings, "settings-help"));
                Check("Settings opens Help at first section", Overlay().SectionIndex == 0 && settings.IsOpen);
            });
            steps.Enqueue(() =>
            {
                Back();
                Check("Settings dialog instance and account draft are preserved", ReferenceEquals(settings, Find<SettingsDialog>(home, "settings-dialog"))
                    && settings.IsOpen && Find<InputEntry>(settings, "account-name").value == "入力途中");
                Check("Active local account, all accounts, profile and persisted bytes remain unchanged",
                    boot.Context.Accounts.ActiveId == accountBefore && boot.Context.Accounts.Ids.SequenceEqual(accountsBefore)
                    && ProfilePersistence.Save(UiTheme.Data, boot.Context.Profile) == profileBefore
                    && saveBefore.SequenceEqual(File.ReadAllBytes(boot.Context.SavePath)));
                Click(Find<Button>(settings, "settings-close"));
                boot.Context.PrepareGame(); boot.Router.Show("Game"); game = boot.Router.CurrentView as GameScreen;
            });
            steps.Enqueue(() =>
            {
                Require(game?.WorldView != null, "World did not attach after Game transition.");
                world = game.WorldView; server = game.Server; game.OpenPanel("menu");
                Click(Find<Button>(game, "ViewHelp"));
                elapsedBefore = server.Snapshot().ElapsedSeconds;
                Check("Game menu opens Help without replacing live state", Overlay().SectionIndex == 0
                    && ReferenceEquals(game, boot.Router.CurrentView) && ReferenceEquals(server, boot.Context.Server)
                    && ReferenceEquals(world, game.WorldView) && game.CurrentPanel == "menu");
                Check("Help blocks map input", !world.InputEnabled && !world.TryTapPanelPoint(world.TilePanelPoint(server.World.Start)));
            });
            steps.Enqueue(() =>
            {
                decimal elapsedAfter = server.Snapshot().ElapsedSeconds;
                report["gameElapsedBeforeHelpWait"] = elapsedBefore; report["gameElapsedAfterHelpWait"] = elapsedAfter;
                Check("Bootstrap continues Core Tick while Help is open", elapsedAfter > elapsedBefore);
                Back();
                Check("Back restores same game, world, server and menu", ReferenceEquals(game, boot.Router.CurrentView)
                    && ReferenceEquals(world, game.WorldView) && ReferenceEquals(server, game.Server) && game.CurrentPanel == "menu");
                game.Panels.Close();
                string material = UiTheme.Config.Materials[0].Id;
                server.Buy(game.LocalActorId, material, System.Numerics.BigInteger.One);
                game.RefreshSnapshot(); game.OpenPanel("warehouse"); game.Panels.SelectInventory(material, UiTheme.Config.PurchasedQualityId);
                game.Panels.Q<InventorySelectionControls>().QuantityInput.value = "1";
            });
            steps.Enqueue(() =>
            {
                selectedBefore = game.Panels.SelectedInventoryKey; quantityBefore = game.Panels.SaleQuantityText;
                Require(!string.IsNullOrEmpty(selectedBefore), "Warehouse has no selected row.");
                Click(Find<Button>(game, "WarehouseHelp"));
                Check("Warehouse ? opens section 2", Overlay().SectionIndex == 2 && game.CurrentPanel == "warehouse");
                Back();
                Check("Warehouse selection, entered quantity and original panel are preserved", game.CurrentPanel == "warehouse"
                    && game.Panels.SelectedInventoryKey == selectedBefore && game.Panels.SaleQuantityText == quantityBefore);
                game.Panels.Close(); boot.Context.OpenHelp();
                Check("Help blocks input even without an underlying open panel", !world.InputEnabled);
                boot.Context.CloseHelp();
                Check("Map input is restored after Help closes", world.InputEnabled);
                boot.Router.Show("Home");
            });
        }

        private static HelpScreen Overlay()
        { var view = boot.Router.OverlayView as HelpScreen; Require(view != null && boot.Router.VisibleScreen == "Help", "Help overlay is missing."); return view; }
        private static void Back() => Click(Overlay().Q<ScreenHeader>().Q<Button>(className: "screen-header__back"));
        private static T Find<T>(VisualElement root, string name) where T : VisualElement
        { T element = root.Q<T>(name); Require(element != null, "Missing UI control: " + name); return element; }
        private static void Click(Button button)
        {
            Require(button != null && button.enabledInHierarchy, "Cannot click a missing or disabled button.");
            typeof(Clickable).GetMethod("Invoke", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
                .Invoke(button.clickable, new object[] { null });
        }
        private static void Check(string name, bool passed)
        { checks.Add(new JObject { ["name"] = name, ["passed"] = passed }); Require(passed, name); }
        private static void Require(bool passed, string message)
        { if (!passed) throw new InvalidOperationException(message); }
        private static void Finish(Exception failure)
        {
            if (boot?.Router != null && overlayListener != null) boot.Router.OverlayChanged -= overlayListener;
            overlayListener = null; steps.Clear(); IsRunning = false;
            report ??= new JObject(); report["status"] = failure == null ? "complete" : "failed";
            report["passed"] = failure == null; report["checkCount"] = checks?.Count ?? 0;
            if (failure != null) report["failure"] = failure.ToString();
            File.WriteAllText(Report, report.ToString()); File.WriteAllText(Result, report.ToString());
        }
    }
}
