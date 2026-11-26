using System;
using System.Collections.Generic;
using System.Globalization;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Imoni.Game.Effects;
using BigInteger = System.Numerics.BigInteger;

namespace Imoni.UI
{
    /// <summary>Presents a settled round. Replaying or skipping the cooking never settles it again.</summary>
    [DisallowMultipleComponent]
    public sealed class RoundResultController : MonoBehaviour
    {
        [Serializable]
        public sealed class RequirementView
        {
            public TMP_Text label, count;
            public Image fill;
        }

        public GameObject cookingRoot, successRoot, failureRoot;
        public ImoniEffectView effectView;
        public RectTransform pot;
        public RectTransform[] fallingIngredients;
        public TMP_Text scoreText, levelText, cookingTitle, cookingSubtitle;
        public Image cookingProgress;
        public GameObject cookingNextRoot;
        public TMP_Text successRoundText, rankText, successTitle, bonusText, scoreBreakdownText;
        public TMP_Text earnedScoreText, rewardText, totalScoreText, nextRoundHint, nextRoundButtonText;
        public RequirementView[] successRequirements, failureRequirements;
        public TMP_Text[] bonusStages;
        public TMP_Text failureRoundText, failureReachedText, failureScoreText;

        public const float CookingDurationSeconds = 7f;
        private static readonly long[] LevelThresholds = { 1000, 3000, 6000, 10000, 15000 };
        private static readonly string[] LevelNames = { "ことこと…", "いい匂い!", "ぐつぐつ!", "大成功の予感!", "伝説の芋煮!!", "天下一の芋煮!!!" };
        private static readonly string[] MaterialNames = { "薪", "里芋", "ねぎ" };
        private static readonly int[] BonusThresholds = { 100, 150, 200, 300 };
        private static readonly int[] BonusMultipliers = { 1, 2, 4, 8 };
        private static readonly CultureInfo Japanese = CultureInfo.GetCultureInfo("ja-JP");
        private static readonly Color Green = new Color32(52, 66, 28, 255), Cream = new Color32(255, 243, 214, 255);
        private static readonly Color Rust = new Color32(139, 35, 16, 255), Muted = new Color32(107, 74, 58, 255);
        private ImoniRoundSession.RoundResultSnapshot snapshot;
        private float elapsed;
        private bool cookingFinished, resultVisible, navigating, nextRoundPrepared;
        private Vector2 potRest;
        private Vector3 potScale;
        private Quaternion potRotation;
        private Vector2[] ingredientRest;
        private Vector3[] ingredientScale;
        private Quaternion[] ingredientRotation;
        private Image[] cookingArtwork;
        private Color[] artworkColors;

        private void Start()
        {
            snapshot = ImoniRoundSession.LastResult;
            CachePoses();
            PopulateResult();
            if (snapshot == null) ShowResult();
            else Replay();
        }

        private void CachePoses()
        {
            if (pot) { potRest = pot.anchoredPosition; potScale = pot.localScale; potRotation = pot.localRotation; }
            int count = fallingIngredients?.Length ?? 0;
            ingredientRest = new Vector2[count]; ingredientScale = new Vector3[count]; ingredientRotation = new Quaternion[count];
            for (int i = 0; i < count; i++)
            {
                if (!fallingIngredients[i]) continue;
                ingredientRest[i] = fallingIngredients[i].anchoredPosition;
                ingredientScale[i] = fallingIngredients[i].localScale;
                ingredientRotation[i] = fallingIngredients[i].localRotation;
            }
            var artwork = new List<Image>();
            var rainbow = cookingRoot ? cookingRoot.transform.Find("RainbowBackground") : null;
            if (rainbow) artwork.AddRange(rainbow.GetComponentsInChildren<Image>(true));
            if (pot && pot.GetComponent<Image>()) artwork.Add(pot.GetComponent<Image>());
            cookingArtwork = artwork.ToArray();
            artworkColors = new Color[cookingArtwork.Length];
            for (int i = 0; i < cookingArtwork.Length; i++) artworkColors[i] = cookingArtwork[i].color;
        }

        private void Update() => AdvanceCooking(Time.unscaledDeltaTime);

        private void AdvanceCooking(float delta)
        {
            if (effectView) return; // UI Toolkit owns the elapsed-time presentation; settlement stays here.
            if (snapshot == null || resultVisible || cookingFinished || navigating) return;
            elapsed = Mathf.Min(CookingDurationSeconds, elapsed + Mathf.Max(0f, delta));
            float progress = elapsed / CookingDurationSeconds;
            long displayedScore = progress >= 1f ? snapshot.RoundScore : (long)Math.Round(snapshot.RoundScore * (double)progress * progress);
            Text(scoreText, Format(displayedScore));
            int level = 0;
            while (level < LevelThresholds.Length && displayedScore >= LevelThresholds[level]) level++;
            Text(levelText, "Lv." + level + " " + LevelNames[level]);
            if (cookingProgress) cookingProgress.fillAmount = progress;
            if (pot)
            {
                float pulse = Mathf.Sin(elapsed * (8f + level));
                pot.anchoredPosition = potRest + Vector2.up * Mathf.Abs(pulse) * 3f;
                pot.localScale = potScale * (1f + Mathf.Abs(pulse) * .04f);
                pot.localRotation = potRotation * Quaternion.Euler(0, 0, pulse * 2f);
            }
            for (int i = 0; i < (fallingIngredients?.Length ?? 0); i++)
            {
                var item = fallingIngredients[i];
                if (!item || !item.parent || !pot) continue;
                float flight = (elapsed - .2f - i * .24f) / 1.15f;
                bool visible = flight >= 0 && elapsed < CookingDurationSeconds - .65f;
                item.gameObject.SetActive(visible);
                if (!visible) continue;
                float p = Mathf.Repeat(flight, 1f);
                float eased = .3f * p + .7f * p * p;
                Vector3 mouth = pot.TransformPoint(pot.rect.center + Vector2.up * pot.rect.height * .18f);
                Vector3 localTarget = item.parent.InverseTransformPoint(mouth);
                Vector3 localCenter = item.parent.InverseTransformPoint(item.TransformPoint(item.rect.center));
                Vector2 target = item.anchoredPosition + (Vector2)(localTarget - localCenter);
                Vector2 from = target + new Vector2(Mathf.Sin(i * 2.7f + Mathf.Floor(flight)) * 105f, 360f);
                item.anchoredPosition = Vector2.Lerp(from, target, eased);
                item.localScale = ingredientScale[i] * (1f - p * .35f);
                item.localRotation = ingredientRotation[i] * Quaternion.Euler(0, 0, (i % 2 == 0 ? 1 : -1) * (p * 360f + i * 35f));
            }
            if (progress < 1f) return;
            cookingFinished = true;
            RestorePot();
            if (!snapshot.Success)
                for (int i = 0; i < cookingArtwork.Length; i++)
                {
                    float gray = artworkColors[i].grayscale * .72f;
                    if (cookingArtwork[i]) cookingArtwork[i].color = new Color(gray, gray, gray, artworkColors[i].a);
                }
            Text(cookingTitle, snapshot.Success ? (snapshot.IsFinalRound ? "5ラウンド\nクリア!" : "芋煮会\n大成功!") : "芋煮会\n失敗…");
            Text(cookingSubtitle, snapshot.Success ? "みんなの素材が、おいしい芋煮になったよ" : MissingMaterials());
            SetActive(cookingNextRoot, true);
        }

        public void Skip()
        {
            if (navigating) return;
            AdvanceCooking(CookingDurationSeconds);
            ShowResult();
        }

        public void Replay()
        {
            if (snapshot == null || navigating) return;
            if (effectView)
            {
                elapsed = 0f;
                cookingFinished = resultVisible = false;
                SetActive(cookingRoot, false); SetActive(successRoot, false); SetActive(failureRoot, false);
                var score = new BigInteger(snapshot.RoundScore);
                // A failed round uses the same success-scale thresholds, not its smaller final score.
                var config = Newtonsoft.Json.Linq.JObject.Parse(effectView.gameConfig.text);
                decimal fraction = (decimal)config["effectReview"]["failureScoreRatio"];
                var reference = snapshot.Success ? score : new BigInteger(decimal.Ceiling(snapshot.RoundScore / fraction));
                effectView.Begin(score, BigInteger.Max(BigInteger.One, reference), !snapshot.Success,
                    snapshot.Success ? "" : MissingMaterialNames(), ShowResult, (int)config["effectReview"]["seed"]);
                return;
            }
            elapsed = 0f;
            cookingFinished = resultVisible = false;
            SetActive(cookingRoot, true); SetActive(successRoot, false); SetActive(failureRoot, false);
            SetActive(cookingNextRoot, false);
            RestorePot();
            for (int i = 0; i < cookingArtwork.Length; i++)
                if (cookingArtwork[i]) cookingArtwork[i].color = artworkColors[i];
            for (int i = 0; i < (fallingIngredients?.Length ?? 0); i++)
            {
                var item = fallingIngredients[i];
                if (!item) continue;
                item.anchoredPosition = ingredientRest[i]; item.localScale = ingredientScale[i]; item.localRotation = ingredientRotation[i];
                item.gameObject.SetActive(false);
            }
            Text(cookingTitle, "みんなの素材を\n鍋に投入!");
            Text(cookingSubtitle, "倉庫に残った素材がスコアになるよ");
            AdvanceCooking(0f);
        }

        public void ShowResult()
        {
            if (navigating) return;
            if (effectView) effectView.Hide();
            resultVisible = true;
            SetActive(cookingRoot, false);
            SetActive(successRoot, snapshot != null && snapshot.Success);
            SetActive(failureRoot, snapshot == null || !snapshot.Success);
        }

        public void NextRound()
        {
            if (!resultVisible || navigating || snapshot == null || !snapshot.Success) return;
            if (snapshot.IsFinalRound) { ReturnHome(); return; }
            if (!CanLoad("WorldLoading")) return;
            if (!nextRoundPrepared)
            {
                if (!ImoniRoundSession.PrepareNextRound()) return;
                nextRoundPrepared = true;
            }
            Navigate("WorldLoading");
        }

        public void Retry()
        {
            if (!resultVisible || navigating || (snapshot != null && snapshot.Success) || !CanLoad("WorldLoading")) return;
            ImoniRoundSession.PrepareRetry();
            Navigate("WorldLoading");
        }

        public void ReturnHome()
        {
            if (navigating || !CanLoad("Home")) return;
            ImoniRoundSession.Reset();
            Navigate("Home");
        }

        private bool CanLoad(string scene)
        {
            if (Application.CanStreamedLevelBeLoaded(scene)) return true;
            Debug.LogError(scene + " scene is not registered in the build scene list.", this);
            return false;
        }

        private void Navigate(string scene)
        {
            navigating = true;
            try
            {
                if (SceneManager.LoadSceneAsync(scene, LoadSceneMode.Single) == null)
                    throw new InvalidOperationException(scene + " scene load did not start.");
            }
            catch (Exception exception)
            {
                navigating = false;
                Debug.LogException(exception, this);
            }
        }

        private void PopulateResult()
        {
            if (snapshot == null)
            {
                Text(failureRoundText, "結果データがありません");
                Text(failureReachedText, "—"); Text(failureScoreText, "0");
                return;
            }
            string round = "ラウンド " + snapshot.RoundNumber + "/" + ImoniRoundSession.MaximumRounds + " 結果";
            Text(successRoundText, round); Text(failureRoundText, round);
            Text(rankText, snapshot.Rank);
            Text(successTitle, snapshot.IsFinalRound ? "5ラウンド\nクリア!" : "うまい芋煮が\nできた!");
            int[] amounts = snapshot.ConvertedAmounts;
            Text(bonusText, "ボーナス:キノコ " + Format(amounts[3]) + "/30  " + snapshot.BonusPercent + "% 報酬×" + snapshot.BonusMultiplier);
            long weightedMaterial = (long)Math.Round(snapshot.MaterialPoints * ImoniRoundSession.MaterialScoreWeight, MidpointRounding.AwayFromZero);
            long weightedBonus = (long)Math.Round(snapshot.BonusPoints * snapshot.BonusMultiplier * ImoniRoundSession.BonusScoreWeight, MidpointRounding.AwayFromZero);
            Text(scoreBreakdownText, "素材ポイント R　" + Format(snapshot.MaterialPoints) + " → " + Format(weightedMaterial) +
                "\nボーナス B×T(×" + snapshot.BonusMultiplier + ")　" + Format(snapshot.BonusPoints) + " → " + Format(weightedBonus));
            Text(earnedScoreText, Format(snapshot.RoundScore));
            Text(rewardText, "+" + Format(snapshot.RewardFunds) + "G");
            Text(totalScoreText, Format(snapshot.TotalScore));
            Text(nextRoundHint, snapshot.IsFinalRound ? "5ラウンドの挑戦をクリアしました" : "資金と強化を引き継ぎ、素材はリセット");
            Text(nextRoundButtonText, snapshot.IsFinalRound ? "ホームへ戻る" : "ラウンド" + (snapshot.RoundNumber + 1) + "へ");
            Text(failureReachedText, "R" + snapshot.RoundNumber);
            Text(failureScoreText, Format(snapshot.TotalScore));
            PopulateRequirements(successRequirements, amounts);
            PopulateRequirements(failureRequirements, amounts);
            for (int i = 0; i < Mathf.Min(bonusStages?.Length ?? 0, BonusThresholds.Length); i++)
            {
                var label = bonusStages[i];
                if (!label) continue;
                bool reached = snapshot.BonusPercent >= BonusThresholds[i];
                bool current = reached && (i == BonusThresholds.Length - 1 || snapshot.BonusPercent < BonusThresholds[i + 1]);
                Text(label, BonusThresholds[i] + "% ×" + BonusMultipliers[i]);
                label.color = reached ? Cream : Muted;
                var background = label.transform.parent ? label.transform.parent.GetComponent<Image>() : null;
                if (background) background.color = current ? Rust : reached ? Green : Color.white;
            }
        }

        private void PopulateRequirements(RequirementView[] views, int[] amounts)
        {
            int[] required = snapshot.RequiredAmounts;
            for (int i = 0; i < Mathf.Min(views?.Length ?? 0, required.Length); i++)
            {
                var view = views[i];
                if (view == null) continue;
                bool enough = amounts[i] >= required[i];
                Text(view.label, MaterialNames[i]);
                Text(view.count, Format(amounts[i]) + "/" + required[i] + (enough ? "" : " 不足"));
                if (view.count) view.count.color = enough ? Green : Rust;
                if (view.fill) { view.fill.fillAmount = Mathf.Clamp01(amounts[i] / (float)required[i]); view.fill.color = enough ? new Color32(107, 155, 58, 255) : Rust; }
            }
        }

        private string MissingMaterials()
        {
            return "必須素材「" + MissingMaterialNames() + "」が足りなかった";
        }

        private string MissingMaterialNames()
        {
            int[] amounts = snapshot.ConvertedAmounts, required = snapshot.RequiredAmounts;
            string missing = "";
            for (int i = 0; i < required.Length; i++)
                if (amounts[i] < required[i]) missing += (missing.Length == 0 ? "" : "・") + MaterialNames[i];
            return missing;
        }

        private void RestorePot() { if (pot) { pot.anchoredPosition = potRest; pot.localScale = potScale; pot.localRotation = potRotation; } }
        private static string Format(long value) => value.ToString("N0", Japanese);
        private static void Text(TMP_Text label, string value) { if (label && label.text != value) label.text = value; }
        private static void SetActive(GameObject target, bool value) { if (target && target.activeSelf != value) target.SetActive(value); }
    }
}
