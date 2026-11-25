using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Imoni.UI
{
    /// <summary>Local create/join, invitation waiting and ready-state presentation.</summary>
    [DisallowMultipleComponent]
    public sealed class LobbyScreenController : MonoBehaviour
    {
        [Header("Views")]
        [SerializeField] private GameObject headerRoot, choosingRoot, waitingRoot, teamRoot;
        [SerializeField] private SceneNavigationButton homeNavigation;
        [SerializeField] private TMP_InputField codeInput;
        [SerializeField] private GameObject joinErrorRoot;
        [SerializeField] private TMP_Text joinErrorLabel;
        [Header("Team preparation")]
        [SerializeField] private GameObject notReadyActions, readyActions;
        [SerializeField] private Image ownStatusBackground;
        [SerializeField] private TMP_Text ownStatusLabel, inviteCodeLabel, copyCodeLabel;
        [SerializeField] private Button startButton;
        [SerializeField] private SceneNavigationButton gameNavigation;
        [SerializeField] private Image[] seedBackgrounds;
        [SerializeField] private TMP_Text[] seedLabels;
        [Header("Invitations")]
        [SerializeField] private GameObject[] waitingFilledSlots, waitingEmptySlots;
        [SerializeField] private TMP_Text[] waitingEmptyLabels;
        [SerializeField] private TMP_Text waitingHeadline, waitingSubline, waitingCountLabel;
        [SerializeField] private TMP_Text waitingCodeLabel, waitingCopyLabel, waitingShareLabel, waitingSeedLabel;
        [SerializeField] private TMP_Text waitingContinueLabel;
        [SerializeField] private Button waitingContinueButton;
        [SerializeField] private Image waitingContinueBackground;
        [SerializeField] private string inviteCode = "IMO-482";

        private enum View { Choosing, Waiting, Team }
        private View view;
        private bool isReady;
        private int joinedCount = 1, lastDots = -1;
        private float waitingElapsed;
        private Coroutine copyFeedback;
        private static readonly Color Green = new Color32(52, 66, 28, 255);
        private static readonly Color Cream = new Color32(255, 243, 214, 255);
        private static readonly Color WaitingBackground = new Color32(239, 211, 138, 255);
        private static readonly Color Ink = new Color32(74, 46, 34, 255);

        public bool IsReady => isReady;
        public int JoinedCount => joinedCount;
        public string CurrentView => view.ToString();
        public static string SelectedSeedMode { get; private set; } = "common";

        private void OnEnable()
        {
            isReady = false;
            view = View.Choosing;
            SelectedSeedMode = PlayerPrefs.GetString("Imoni.SeedMode", "common");
            if (SelectedSeedMode != "free") SelectedSeedMode = "common";
            if (inviteCodeLabel) inviteCodeLabel.text = inviteCode;
            if (waitingCodeLabel) waitingCodeLabel.text = inviteCode;
            ClearJoinError();
            SetCopyLabels(false, false);
            RefreshViews();
            RefreshSeed();
        }

        private void Update()
        {
            if (view != View.Waiting) return;
            // The reference's local waiting sequence fills the two companion slots.
            waitingElapsed += Time.unscaledDeltaTime;
            int nextCount = waitingElapsed >= 5.2f ? 3 : waitingElapsed >= 2.5f ? 2 : 1;
            int dots = Mathf.FloorToInt(waitingElapsed * 2f) % 3;
            if (nextCount != joinedCount || dots != lastDots)
            {
                joinedCount = nextCount;
                lastDots = dots;
                RefreshWaiting();
            }
        }

        public void CreateTeam()
        {
            isReady = false;
            joinedCount = 1;
            waitingElapsed = 0;
            lastDots = 0;
            view = View.Waiting;
            ClearJoinError();
            RefreshViews();
            RefreshWaiting();
        }

        public void OnCodeChanged(string value)
        {
            if (codeInput) codeInput.SetTextWithoutNotify((value ?? "").ToUpperInvariant());
            ClearJoinError();
        }

        public void JoinTeam()
        {
            string code = codeInput ? codeInput.text.Trim().ToUpperInvariant() : "";
            if (code.Length == 0) { ShowJoinError("招待コードを入力してください"); return; }
            if (code == "IMO-777") { ShowJoinError("このチームは挑戦中のため参加できません。\n挑戦が終わるまで待ってね"); return; }
            if (code != inviteCode) { ShowJoinError("チームが見つかりません。\nコードを確かめてね"); return; }
            joinedCount = 3;
            EnterTeamPreparation();
        }

        public void EnterTeamPreparation()
        {
            if (joinedCount < 3) return;
            isReady = false;
            view = View.Team;
            ClearJoinError();
            RefreshViews();
        }

        public void Back()
        {
            if (view == View.Choosing)
            {
                if (homeNavigation) homeNavigation.Navigate();
                return;
            }
            isReady = false;
            view = View.Choosing;
            ClearJoinError();
            RefreshViews();
        }

        public void ToggleReady()
        {
            if (view != View.Team) return;
            isReady = !isReady;
            RefreshReadyState();
        }

        public void SelectSeed(int index)
        {
            SelectedSeedMode = index == 1 ? "free" : "common";
            PlayerPrefs.SetString("Imoni.SeedMode", SelectedSeedMode);
            isReady = false;
            RefreshSeed();
            RefreshReadyState();
        }

        public void CopyInviteCode() => CopyInvitation(false);
        public void ShareInviteCode() => CopyInvitation(true);

        private void CopyInvitation(bool share)
        {
            GUIUtility.systemCopyBuffer = share ? "みんなで芋煮会\nチーム「鍋底同盟」に参加しよう！\n招待コード: " + inviteCode : inviteCode;
            if (copyFeedback != null) StopCoroutine(copyFeedback);
            SetCopyLabels(true, share);
            copyFeedback = StartCoroutine(RestoreCopyCaption());
        }

        public void StartGame()
        {
            if (view != View.Team || !isReady || !gameNavigation) return;
            ImoniRoundSession.BeginNewChallenge();
            gameNavigation.Navigate();
        }

        private void RefreshViews()
        {
            if (headerRoot) headerRoot.SetActive(view != View.Waiting);
            if (choosingRoot) choosingRoot.SetActive(view == View.Choosing);
            if (waitingRoot) waitingRoot.SetActive(view == View.Waiting);
            if (teamRoot) teamRoot.SetActive(view == View.Team);
            RefreshReadyState();
        }

        private void RefreshReadyState()
        {
            if (notReadyActions) notReadyActions.SetActive(!isReady);
            if (readyActions) readyActions.SetActive(isReady);
            if (ownStatusBackground) ownStatusBackground.color = isReady ? Green : WaitingBackground;
            if (ownStatusLabel)
            {
                ownStatusLabel.text = isReady ? "準備OK" : "準備中";
                ownStatusLabel.color = isReady ? Cream : Ink;
            }
            if (startButton) startButton.interactable = isReady;
        }

        private void RefreshSeed()
        {
            int selected = SelectedSeedMode == "free" ? 1 : 0;
            if (seedBackgrounds != null)
                for (int i = 0; i < seedBackgrounds.Length; i++)
                    if (seedBackgrounds[i]) seedBackgrounds[i].color = i == selected ? Green : Color.white;
            if (seedLabels != null)
                for (int i = 0; i < seedLabels.Length; i++)
                    if (seedLabels[i]) seedLabels[i].color = i == selected ? Cream : Green;
            if (waitingSeedLabel) waitingSeedLabel.text = selected == 0 ? "共通シード #2026-47" : "自由シード";
        }

        private void RefreshWaiting()
        {
            for (int i = 0; i < 3; i++)
            {
                if (waitingFilledSlots != null && i < waitingFilledSlots.Length && waitingFilledSlots[i]) waitingFilledSlots[i].SetActive(i < joinedCount);
                if (waitingEmptySlots != null && i < waitingEmptySlots.Length && waitingEmptySlots[i]) waitingEmptySlots[i].SetActive(i >= joinedCount);
            }
            if (waitingEmptyLabels != null)
                foreach (var label in waitingEmptyLabels)
                    if (label) label.text = "参加を待っています" + new string('.', lastDots + 1);
            bool full = joinedCount >= 3;
            if (waitingHeadline) waitingHeadline.text = full ? "全員そろったよ!" : "メンバーを\n待っています";
            if (waitingSubline) waitingSubline.text = full ? "モードを選んで準備しよう" : "チームは3人で挑戦するよ";
            if (waitingCountLabel) waitingCountLabel.text = joinedCount + "/3人";
            if (waitingContinueLabel)
            {
                waitingContinueLabel.text = full ? "全員そろった! 準備へ" : "あと" + (3 - joinedCount) + "人そろうとスタートできます";
                waitingContinueLabel.fontSize = full ? 21 : 16;
                waitingContinueLabel.color = full ? Cream : new Color32(92, 69, 24, 255);
            }
            if (waitingContinueBackground) waitingContinueBackground.color = full ? new Color32(139, 35, 16, 255) : WaitingBackground;
            if (waitingContinueButton)
            {
                waitingContinueButton.interactable = full;
                waitingContinueButton.GetComponent<Image>().color = full ? new Color32(139, 35, 16, 255) : new Color32(140, 106, 46, 255);
            }
        }

        private void ClearJoinError()
        {
            if (joinErrorRoot) joinErrorRoot.SetActive(false);
            if (joinErrorLabel) joinErrorLabel.text = "";
        }

        private void ShowJoinError(string message)
        {
            if (joinErrorRoot) joinErrorRoot.SetActive(true);
            if (joinErrorLabel) joinErrorLabel.text = message;
        }

        private void SetCopyLabels(bool copied, bool share)
        {
            if (copyCodeLabel) copyCodeLabel.text = copied && !share ? "コピーしました！" : "招待コードをコピー";
            if (waitingCopyLabel) waitingCopyLabel.text = copied && !share ? "コピーしました！" : "コードをコピー";
            if (waitingShareLabel) waitingShareLabel.text = copied && share ? "招待文をコピー済み" : "共有する";
        }

        private IEnumerator RestoreCopyCaption()
        {
            yield return new WaitForSecondsRealtime(2);
            SetCopyLabels(false, false);
            copyFeedback = null;
        }

        private void OnDisable()
        {
            if (copyFeedback != null) StopCoroutine(copyFeedback);
            copyFeedback = null;
            SetCopyLabels(false, false);
        }
    }
}
