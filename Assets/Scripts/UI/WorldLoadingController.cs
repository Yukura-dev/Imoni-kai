using System;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Imoni.UI
{
    /// <summary>Preloads the Game scene and waits for an explicit round start.</summary>
    [DisallowMultipleComponent]
    public sealed class WorldLoadingController : MonoBehaviour
    {
        [Serializable]
        public sealed class StepView
        {
            public TMP_Text label, note;
            public GameObject doneRoot, activeRoot, pendingRoot;
            public RectTransform spinner;
        }

        [Serializable]
        public sealed class MemberView
        {
            public CanvasGroup avatar;
            public Image badge;
            public TMP_Text state;
        }

        public TMP_Text headline, sessionText, percentText, tipText, startButtonText;
        public RectTransform pot;
        public Image progressFill;
        public StepView[] steps;
        public MemberView[] members;
        public GameObject hintRoot, startRoot;
        public Button startButton;
        [SerializeField, Min(0f)] private float minimumDisplaySeconds = 1.5f;

        private static readonly int[] StepEnds = { 10, 38, 62, 80, 100 };
        private static readonly string[] StepLabels =
        {
            "シード設定を確認", "地形とマップを読み込み", "資源表示を準備",
            "移動と採集の準備", "仲間の表示を準備"
        };
        private static readonly string[] Tips =
        {
            "行きたい場所をタップして移動。素材の近くでは自動で採集するよ",
            "素材をタップすると、近くまで歩いて採集してくれるよ",
            "売りたくない素材は、共有倉庫で保護しておこう",
            "ボーナス素材は開始2分後に発表。150%以上でさらに報酬アップ"
        };
        private static readonly Color Ink = new Color32(74, 46, 34, 255);
        private static readonly Color Green = new Color32(52, 66, 28, 255);
        private static readonly Color Cream = new Color32(255, 243, 214, 255);
        private static readonly Color Pending = new Color32(140, 106, 74, 255);
        private AsyncOperation loadOperation;
        private Vector2 potRest;
        private float elapsed;
        private int lastPercent = -1, lastTip = -1;
        private bool ready, activating, failed;
        private string seedMode;

        public bool IsReady => ready;

        private void Start()
        {
            if (pot) potRest = pot.anchoredPosition;
            seedMode = PlayerPrefs.GetString("Imoni.SeedMode", LobbyScreenController.SelectedSeedMode);
            seedMode = seedMode == "free" ? "free" : "common";
            BeginLoading();
        }

        private void BeginLoading()
        {
            ready = activating = failed = false;
            elapsed = 0f;
            lastPercent = lastTip = -1;
            SetActive(startRoot, false);
            SetActive(hintRoot, true);
            Text(headline, "ワールドを読み込み中…");
            Text(sessionText, "チーム「鍋底同盟」 · " + (seedMode == "free" ? "自由シード" : "共通シード"));
            Text(startButtonText, "ラウンド" + ImoniRoundSession.PendingRoundNumber + " スタート!");
            if (startButton) startButton.interactable = false;
            RenderProgress(0);
            try
            {
                if (!Application.CanStreamedLevelBeLoaded("Game"))
                    throw new InvalidOperationException("Game scene is not in the build scene list.");
                loadOperation = SceneManager.LoadSceneAsync("Game", LoadSceneMode.Single);
                if (loadOperation == null) throw new InvalidOperationException("Game scene preload did not start.");
                loadOperation.allowSceneActivation = false;
            }
            catch (Exception exception)
            {
                failed = true;
                Text(headline, "読み込みに失敗しました");
                Text(tipText, "ゲーム画面を読み込めませんでした。もう一度お試しください。");
                Text(startButtonText, "もう一度読み込む");
                SetActive(startRoot, true);
                // The retry button occupies the hint's place, so keep the error in the subtitle.
                SetActive(hintRoot, false);
                Text(sessionText, "ゲーム画面を読み込めませんでした");
                if (startButton) startButton.interactable = true;
                Debug.LogException(exception, this);
            }
        }

        private void Update()
        {
            elapsed += Time.unscaledDeltaTime;
            if (pot)
            {
                float phase = elapsed * Mathf.PI * 2f / 1.4f;
                pot.anchoredPosition = potRest + Vector2.up * ((1f - Mathf.Cos(phase)) * 3f);
                pot.localRotation = Quaternion.Euler(0, 0, Mathf.Sin(phase) * 3f);
            }
            if (failed || activating || loadOperation == null) return;
            if (!ready)
            {
                // Unity pauses a prepared scene at 0.9 while scene activation is disabled.
                float loaded = Mathf.Clamp01(loadOperation.progress / .9f);
                float presentation = minimumDisplaySeconds <= 0f ? 1f : Mathf.Clamp01(elapsed / minimumDisplaySeconds);
                int percent = Mathf.FloorToInt(Mathf.Min(loaded, presentation) * 100f);
                if (loaded >= 1f && presentation >= 1f) ready = true;
                RenderProgress(ready ? 100 : Mathf.Min(99, percent));
                if (ready)
                {
                    Text(headline, "準備ができたよ!");
                    SetActive(hintRoot, false);
                    SetActive(startRoot, true);
                    if (startButton) startButton.interactable = true;
                }
            }
            if (steps != null)
                foreach (var step in steps)
                    if (step?.spinner && step.activeRoot && step.activeRoot.activeSelf)
                        step.spinner.localRotation = Quaternion.Euler(0, 0, -elapsed * 450f);
            int tip = Mathf.FloorToInt(elapsed / 2.6f) % Tips.Length;
            if (tip != lastTip)
            {
                lastTip = tip;
                Text(tipText, Tips[tip]);
            }
        }

        public void StartRound()
        {
            if (failed) { BeginLoading(); return; }
            if (!ready || activating || loadOperation == null) return;
            activating = true;
            if (startButton) startButton.interactable = false;
            Text(startButtonText, "スタート中…");
            loadOperation.allowSceneActivation = true;
        }

        private void RenderProgress(int percent)
        {
            if (percent == lastPercent) return;
            lastPercent = percent;
            Text(percentText, percent + "%");
            if (progressFill) progressFill.fillAmount = percent / 100f;
            if (steps != null)
                for (int i = 0; i < Mathf.Min(steps.Length, StepEnds.Length); i++)
                {
                    var step = steps[i];
                    if (step == null) continue;
                    bool done = percent >= StepEnds[i];
                    bool active = !done && percent >= (i == 0 ? 0 : StepEnds[i - 1]);
                    SetActive(step.doneRoot, done);
                    SetActive(step.activeRoot, active);
                    SetActive(step.pendingRoot, !done && !active);
                    Text(step.label, StepLabels[i]);
                    if (step.label) step.label.color = done || active ? Ink : Pending;
                    string note = i == 0 ? (seedMode == "free" ? "自由モード" : "共通モード") :
                        i == 1 ? "平野・森林・川" : i == 2 ? "14か所" : i == 3 ? "タップ移動" : "ローカル3人";
                    Text(step.note, done ? note : "");
                }
            if (members != null)
                for (int i = 0; i < members.Length; i++)
                {
                    var member = members[i];
                    if (member == null) continue;
                    bool done = percent >= (i == 0 ? 82 : i == 1 ? 90 : 100);
                    if (member.avatar) member.avatar.alpha = done ? 1f : .5f;
                    if (member.badge) member.badge.color = done ? Green : new Color32(239, 211, 138, 255);
                    Text(member.state, done ? "OK" : "準備中");
                    if (member.state) member.state.color = done ? Cream : Ink;
                }
        }

        private static void Text(TMP_Text label, string value) { if (label && label.text != value) label.text = value; }
        private static void SetActive(GameObject target, bool value) { if (target && target.activeSelf != value) target.SetActive(value); }
    }
}
