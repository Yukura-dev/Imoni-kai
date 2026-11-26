using System;
using System.Globalization;
using System.Linq;
using System.Reflection;
using Imoni.Core.Effects;
using UnityEditor;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;
using BigInteger = System.Numerics.BigInteger;

namespace Imoni.UI.Editor
{
    /// <summary>Checks round transitions without loading gameplay scenes or saving test state.</summary>
    public static class ImoniRoundFlowValidation
    {
        private const BindingFlags Instance = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
        private static readonly string[] ButtonSources =
        {
            "Assets/Prefabs/UI/PrimaryButton.prefab", "Assets/Prefabs/UI/YellowButton.prefab", "Assets/Prefabs/UI/RedButton.prefab"
        };

        /// <summary>Use around editor previews/tests that call Game.Start or finish a round.</summary>
        public static IDisposable PreserveSessionState() => new SessionScope();

        private sealed class SessionScope : IDisposable
        {
            private readonly FieldInfo[] fields = typeof(ImoniRoundSession).GetFields(BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic)
                .Where(field => !field.IsLiteral && !field.IsInitOnly).ToArray();
            private readonly object[] values;
            public SessionScope() { values = fields.Select(field => field.GetValue(null)).ToArray(); }
            public void Dispose() { for (int i = 0; i < fields.Length; i++) fields[i].SetValue(null, values[i]); }
        }

        [MenuItem("Imoni/Round Flow/Validate Saved Flow")]
        public static void Validate()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("Stop Play Mode before checking the saved round flow.");
            using (PreserveSessionState())
            {
                var random = UnityEngine.Random.state;
                GameObject game = null, result = null;
                try
                {
                    game = Load("GameScreen");
                    result = Load("RoundResultScreen");
                    var controller = game.GetComponent<GameScreenController>();
                    var presentation = result.GetComponent<RoundResultController>();
                    Require(controller && presentation, "Game or result controller is missing.");
                    ValidateWiring(controller, presentation);
                    ValidateGameAndSuccessfulRound(controller, presentation);
                    ValidateCarryAndFailure(presentation);
                    ValidateRetryAndFinalRound();
                    Debug.Log("IMONI_ROUND_FLOW_VALIDATED: Game results route to RoundResult; success/failure snapshots, one-time rewards, immutable inventory snapshots, effect resource wiring and Core deadlines, result/skip callbacks, next-round carry, retry reset, final-round guard, shared result buttons and preserved gameplay components passed. No scenes navigated or test state saved. UI Toolkit playback/replay and visual appearance require the Game-view recording check.");
                }
                finally
                {
                    if (result) PrefabUtility.UnloadPrefabContents(result);
                    if (game) PrefabUtility.UnloadPrefabContents(game);
                    UnityEngine.Random.state = random;
                }
            }
        }

        private static GameObject Load(string name)
        {
            string path = "Assets/Prefabs/UI/" + name + ".prefab";
            Require(AssetDatabase.LoadAssetAtPath<GameObject>(path), "Missing saved prefab: " + path);
            var root = PrefabUtility.LoadPrefabContents(path);
            try
            {
                Require(root.GetComponentsInChildren<Component>(true).All(component => component), name + " has a missing script.");
                return root;
            }
            catch
            {
                PrefabUtility.UnloadPrefabContents(root);
                throw;
            }
        }

        private static void ValidateWiring(GameScreenController game, RoundResultController result)
        {
            Require(game.endedPanel, "The game timeout overlay is missing.");
            var resultButton = game.transform.Find("RoundFinished/Summary/ShowRoundResult")?.GetComponent<Button>();
            Require(resultButton, "The timeout overlay has no ShowRoundResult button.");
            RequireHandler(resultButton, game, "ShowRoundResult");
            Require(game.endedPanel.GetComponentsInChildren<SceneNavigationButton>(true).Length == 0, "The timeout overlay still contains a direct scene-navigation action.");
            Require(game.endedPanel.GetComponentsInChildren<Button>(true).Length == 1, "The timeout overlay must offer one result action.");
            Require(result.successRoot && result.failureRoot, "Result roots are incomplete.");
            if (result.effectView) ValidateEffectWiring(result);
            else
            {
                Require(result.cookingRoot && result.pot && result.cookingNextRoot && result.cookingProgress, "Legacy cooking roots are incomplete.");
                Require(result.scoreText && result.levelText && result.cookingTitle && result.cookingSubtitle, "Legacy cooking labels are incomplete.");
                Require(result.fallingIngredients?.Length == 4 && result.fallingIngredients.All(item => item), "Legacy cooking ingredient references are incomplete.");
                Require(result.cookingProgress.GetComponent<RoundedProgressFill>(), "Legacy cooking progress is not rounded.");
            }
            Require(result.successRoundText && result.rankText && result.successTitle && result.bonusText && result.scoreBreakdownText && result.earnedScoreText && result.rewardText && result.totalScoreText && result.nextRoundHint && result.nextRoundButtonText, "Success result labels are incomplete.");
            Require(result.failureRoundText && result.failureReachedText && result.failureScoreText, "Failure result labels are incomplete.");
            Require(result.successRequirements?.Length == 3 && result.failureRequirements?.Length == 3 && result.bonusStages?.Length == 4 && result.bonusStages.All(label => label), "Result requirement/bonus rows are incomplete.");
            foreach (var view in result.successRequirements.Concat(result.failureRequirements))
                Require(view != null && view.label && view.count && view.fill && view.fill.GetComponent<RoundedProgressFill>(), "A result material row has missing labels or rounded progress.");
            var buttons = result.GetComponentsInChildren<Button>(true);
            foreach (var button in buttons) ValidateButton(button);
            ValidateButton(resultButton);
            var actions = result.effectView ? new[] { "NextRound", "Retry", "ReturnHome" } : new[] { "Skip", "Replay", "ShowResult", "NextRound", "Retry", "ReturnHome" };
            foreach (string method in actions)
                Require(buttons.Any(button => HasHandler(button, result, method)), "Result screen is missing its " + method + " action.");
        }

        private static void ValidateEffectWiring(RoundResultController result)
        {
            var view = result.effectView;
            Require(view.document && view.document.panelSettings && view.gameConfig && view.visualTree && view.themeStyle && view.effectStyle,
                "The UI Toolkit effect is missing its document, panel, configuration, or layout resources. Run Imoni > Setup Project.");
            Require(!result.cookingRoot || !result.cookingRoot.activeSelf, "The legacy cooking Canvas is still enabled alongside the UI Toolkit effect.");
            var config = ImoniEffectConfig.FromJson(view.gameConfig.text);
            var panel = view.document.panelSettings;
            Require(panel.scaleMode == UnityEngine.UIElements.PanelScaleMode.ScaleWithScreenSize &&
                panel.referenceResolution == new Vector2Int((int)config.Data["layout"]["screen"][2], (int)config.Data["layout"]["screen"][3]) &&
                Mathf.Approximately(panel.match, 0), "Effect PanelSettings must use the configured 390x844 reference and Match 0.");
            var tree = view.visualTree.CloneTree();
            foreach (var name in new[] { "SoundButton", "SkipButton", "ResultButton", "ReplayButton" })
                Require(UnityEngine.UIElements.UQueryExtensions.Q<UnityEngine.UIElements.Button>(tree, name) != null, "The effect UXML is missing " + name + ".");
            foreach (var name in new[] { "World", "Pillar", "RainbowGlow", "Sunburst", "PotGroup", "BrothGlow", "Flash", "LevelBanner", "Finale", "MissingLabel" })
                Require(UnityEngine.UIElements.UQueryExtensions.Q<UnityEngine.UIElements.VisualElement>(tree, name) != null, "The effect UXML is missing " + name + ".");
            var textures = view.textures?.Where(texture => texture).Select(texture => texture.name).ToArray() ?? Array.Empty<string>();
            foreach (var name in new[] { "fx_rainbow_scroll", "fx_sunburst", "fx_pillar", "fx_rainbow_glow", "fx_ring_glow", "fx_broth_glow", "fx_flash", "fx_sparkle", "pot_effect", "icon_sound_on", "icon_sound_off" })
                Require(textures.Contains(name), "The effect is missing supplied artwork: " + name);
            foreach (var name in new[] { "taro", "negi", "mushroom", "beef", "konnyaku" })
                Require(textures.Contains("ingredient_" + name + "_glow"), "Missing glowing ingredient: " + name);
            foreach (var name in new[] { "yellow", "pink", "blue", "green", "purple", "orange" })
                Require(textures.Contains("fx_star_" + name), "Missing glowing star: " + name);
            foreach (var score in new[] { BigInteger.One, new BigInteger(18420), BigInteger.Pow(10, 15) })
            {
                var timeline = new ImoniEffectTimeline(config, score, score, false);
                timeline.AdvanceTo(config.Number("maxTotalSec") * 1000);
                Require(timeline.ResultVisible && timeline.Score == score, "Success effect missed its score or ten-second result deadline.");
            }
            var failure = new ImoniEffectTimeline(config, new BigInteger(7350), new BigInteger(18420), true);
            failure.AdvanceTo(config.Number("maxTotalSec") * 1000);
            Require(failure.ResultVisible && failure.Level == config.FailMaxLevel && failure.Evaluate().Flash == 0,
                "Failure effect must finish at Lv.3 without a final white flash and reveal its result in ten seconds.");
        }

        private static void PrepareResultWithoutRendering(RoundResultController result)
        {
            // Loading prefab contents is an asset-wiring check, not a live UI panel. Do not call Begin/Replay here.
            Set(result, "snapshot", ImoniRoundSession.LastResult);
            Call(result, "PopulateResult");
        }

        private static void ValidateGameAndSuccessfulRound(GameScreenController game, RoundResultController result)
        {
            ImoniRoundSession.BeginNewChallenge();
            Require(!ImoniRoundSession.PrepareNextRound(), "A next round was queued before any result existed.");
            Call(game, "Start");
            Require(game.RoundNumber == 1 && Get<int>(game, "funds") == 1000, "A fresh Game did not start round one with 1000G.");
            Require(game.resources?.Length == 14 && game.GetComponentsInChildren<RoundedProgressFill>(true).Length == 17, "The harvest scene lost resources or rounded bars.");
            var input = game.GetComponentInChildren<GameMapInput>(true);
            var camera = game.GetComponentInChildren<GameMapCamera>(true);
            var motion = game.GetComponentInChildren<GameCharacterMotion>(true);
            Require(input && camera && motion && input.controller == game && input.world == game.world && camera.world == game.world && camera.player == game.player, "Map input/camera references changed.");
            Require(motion.controller == game && motion.body && motion.leftHand && motion.rightHand && motion.groundShadow, "Walking/harvesting artwork references changed.");
            RequireNear(camera.viewport.rect.size, new Vector2(390, 560), "Field viewport changed.");
            RequireNear(game.world.rect.size, new Vector2(780, 1120), "Harvest world changed.");
            Vector2 point = new Vector2(80, -140);
            game.MoveToWorldPoint(point);
            Call(game, "AdvanceMovement", 100f);
            RequireNear(game.player.anchoredPosition + new Vector2(20, -42), point, "Tap movement no longer stops with the player's feet on the target.");

            int[] inventory = Get<int[]>(game, "quantities"), levels = Get<int[]>(game, "levels");
            Array.Clear(inventory, 0, inventory.Length);
            inventory[0] = 60; inventory[3] = 20; inventory[6] = 15; inventory[9] = 45;
            levels[0] = 2; levels[1] = 1; levels[2] = 3; levels[3] = 0;
            Set(game, "funds", 760);
            Set(game, "remainingTime", 0f);
            Call(game, "Update");
            var snapshot = ImoniRoundSession.LastResult;
            Require(snapshot != null && snapshot.Success && snapshot.RoundNumber == 1 && !snapshot.IsFinalRound, "The completed Game did not capture a successful round-one snapshot.");
            Require(snapshot.ConvertedAmounts.Take(3).SequenceEqual(new[] { 60, 20, 15 }), "The result snapshot lost actual harvested amounts.");
            Require(snapshot.BonusPercent == 150 && snapshot.BonusMultiplier == 2, "Result bonus tier does not match collected mushrooms.");
            Require(snapshot.RoundScore == (snapshot.MaterialPoints + snapshot.BonusPoints * snapshot.BonusMultiplier) / 2, "The result score does not follow the displayed half-material/half-bonus formula.");
            Require(snapshot.FundsAfterReward == 760 + snapshot.RewardFunds && snapshot.UpgradeLevels.SequenceEqual(levels), "The result lost funds or upgrade levels.");
            long total = ImoniRoundSession.TotalScore;
            int fundsAfter = snapshot.FundsAfterReward;
            Call(game, "EndRound");
            Call(game, "Update");
            Require(ReferenceEquals(snapshot, ImoniRoundSession.LastResult) && total == ImoniRoundSession.TotalScore && fundsAfter == ImoniRoundSession.LastResult.FundsAfterReward, "Ending the same Game twice awarded score or funds twice.");
            var convertedCopy = snapshot.ConvertedAmounts;
            convertedCopy[0] = -1;
            inventory[0] = 0;
            Require(snapshot.ConvertedAmounts[0] == 60, "The result snapshot is mutable through its array getter or live inventory.");
            Require(game.endedPanel.activeSelf && !game.IsMoving && !game.IsCollecting, "Game timeout did not freeze gameplay before presenting results.");
            if (result.effectView) PrepareResultWithoutRendering(result);
            else
            {
                Call(result, "Start");
                Require(result.cookingRoot.activeSelf && !result.successRoot.activeSelf && !result.failureRoot.activeSelf, "Results do not begin with the cooking scene.");
                Call(result, "AdvanceCooking", RoundResultController.CookingDurationSeconds);
                Require(result.cookingNextRoot.activeSelf, "Finished cooking did not reveal the result actions.");
            }
            result.ShowResult();
            Require(result.successRoot.activeSelf && !result.failureRoot.activeSelf && (!result.cookingRoot || !result.cookingRoot.activeSelf), "A successful cooking result did not show the success page.");
            Require(result.earnedScoreText.text.Contains(snapshot.RoundScore.ToString("N0", CultureInfo.GetCultureInfo("ja-JP"))), "Success page displays a placeholder score instead of the snapshot.");
            if (!result.effectView)
            {
                result.Replay();
                Require(result.cookingRoot.activeSelf && !result.successRoot.activeSelf, "Replay did not reopen only the cooking scene.");
            }
            result.Skip();
            Require(result.successRoot.activeSelf && ImoniRoundSession.TotalScore == total && ImoniRoundSession.LastResult.FundsAfterReward == fundsAfter, "Replay/skip awarded score or funds again.");
        }

        private static void ValidateCarryAndFailure(RoundResultController result)
        {
            var previous = ImoniRoundSession.LastResult;
            Require(ImoniRoundSession.PrepareNextRound() && ImoniRoundSession.PendingRoundNumber == 2, "A successful result did not queue round two.");
            Require(!ImoniRoundSession.PrepareNextRound() && ImoniRoundSession.PendingRoundNumber == 2, "Repeated Next changed the queued round.");
            GameObject nextGame = null;
            try
            {
                // A fresh Game consumes the pending state once and clears the displayed result.
                nextGame = Load("GameScreen");
                var game = nextGame.GetComponent<GameScreenController>();
                Call(game, "Start");
                Require(game.RoundNumber == 2 && Get<int>(game, "funds") == previous.FundsAfterReward, "Fresh Game did not consume the queued round-two state.");
                Require(ImoniRoundSession.LastResult == null, "A new round retained the previous presentation snapshot.");
                Require(Get<int[]>(game, "quantities").All(value => value == 0) && Get<int[]>(game, "protectedAmounts").All(value => value == 0), "A new round retained warehouse materials or protection counts.");
                Require(Get<int[]>(game, "levels").SequenceEqual(previous.UpgradeLevels), "A new round discarded upgrades.");
                Set(game, "remainingTime", 0f);
                Call(game, "Update");
            }
            finally { if (nextGame) PrefabUtility.UnloadPrefabContents(nextGame); }
            var failed = ImoniRoundSession.LastResult;
            Require(failed != null && !failed.Success && failed.RoundNumber == 2 && failed.RewardFunds == 0, "An empty second round did not produce a failure without a reward.");
            Require(!ImoniRoundSession.PrepareNextRound(), "A failed round can advance to the next round.");
            if (result.effectView) PrepareResultWithoutRendering(result); else Call(result, "Start");
            result.Skip();
            Require(result.failureRoot.activeSelf && !result.successRoot.activeSelf && (!result.cookingRoot || !result.cookingRoot.activeSelf), "Failure cooking did not lead to the failure page.");
            long total = ImoniRoundSession.TotalScore;
            if (!result.effectView) result.Replay();
            result.ShowResult();
            Require(result.failureRoot.activeSelf && ImoniRoundSession.TotalScore == total, "Replaying a failure changed cumulative score.");
        }

        private static void ValidateRetryAndFinalRound()
        {
            ImoniRoundSession.PrepareRetry();
            Require(ImoniRoundSession.LastResult == null && ImoniRoundSession.TotalScore == 0 && ImoniRoundSession.PendingRoundNumber == 1, "Retry did not reset the challenge result and total.");
            var retry = ImoniRoundSession.ConsumeRoundStart();
            Require(retry.RoundNumber == 1 && retry.Funds == 1000 && retry.UpgradeLevels.All(level => level == 0), "Retry did not restore round-one funds and base upgrades.");

            ImoniRoundSession.BeginNewChallenge();
            for (int round = 1; round <= 5; round++)
            {
                var start = ImoniRoundSession.ConsumeRoundStart();
                var inventory = new int[12];
                inventory[0] = inventory[3] = inventory[6] = inventory[9] = 1000000;
                var snapshot = ImoniRoundSession.CompleteRound(start, inventory, start.Funds, start.UpgradeLevels);
                Require(snapshot.Success && snapshot.RoundNumber == round && snapshot.IsFinalRound == (round == 5), "Normal-mode round/final-round state is incorrect.");
                long total = ImoniRoundSession.TotalScore;
                var repeat = ImoniRoundSession.CompleteRound(start, inventory, start.Funds, start.UpgradeLevels);
                Require(ReferenceEquals(snapshot, repeat) && ImoniRoundSession.TotalScore == total, "CompleteRound is not idempotent for the same start state.");
                Require(ImoniRoundSession.PrepareNextRound() == (round < 5), "Normal mode advanced past round five or blocked an earlier success.");
            }
        }

        private static void ValidateButton(Button button)
        {
            Require(PrefabUtility.IsPartOfPrefabInstance(button) && ButtonSources.Contains(PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(button.gameObject)), button.name + " is not a shared button prefab.");
            Require(button.onClick.GetPersistentEventCount() > 0, button.name + " has no saved click action.");
            for (int i = 0; i < button.onClick.GetPersistentEventCount(); i++)
                Require(button.onClick.GetPersistentTarget(i) && button.onClick.GetPersistentListenerState(i) != UnityEventCallState.Off && !string.IsNullOrEmpty(button.onClick.GetPersistentMethodName(i)), button.name + " has a broken click action.");
        }
        private static bool HasHandler(Button button, UnityEngine.Object target, string method) => Enumerable.Range(0, button.onClick.GetPersistentEventCount()).Any(i => button.onClick.GetPersistentTarget(i) == target && button.onClick.GetPersistentMethodName(i) == method);
        private static void RequireHandler(Button button, UnityEngine.Object target, string method) => Require(HasHandler(button, target, method), button.name + " is not bound to " + method + ".");
        private static T Get<T>(object target, string field) => (T)target.GetType().GetField(field, Instance).GetValue(target);
        private static void Set(object target, string field, object value) => target.GetType().GetField(field, Instance).SetValue(target, value);
        private static object Call(object target, string method, params object[] arguments)
        {
            var info = target.GetType().GetMethod(method, Instance) ?? throw new MissingMethodException(target.GetType().Name, method);
            try { return info.Invoke(target, arguments); }
            catch (TargetInvocationException exception) { throw exception.InnerException ?? exception; }
        }
        private static void RequireNear(Vector2 actual, Vector2 expected, string message) => Require((actual - expected).sqrMagnitude < .0001f, message);
        private static void Require(bool value, string message) { if (!value) throw new InvalidOperationException("Round-flow validation failed: " + message); }
    }
}
