using System;
using System.IO;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace Imoni.UI.Editor
{
    /// <summary>Editable uGUI version of Lobby.dc.html; all controls reuse the shared button prefabs.</summary>
    public static class ImoniLobbyBuilder
    {
        private const string LobbyPath = "Assets/Scenes/Lobby.unity";
        private const string ScreenPath = "Assets/Prefabs/UI/LobbyScreen.prefab";
        private const string MemberPath = "Assets/Prefabs/UI/LobbyMemberRow.prefab";
        private const string Primary = "Assets/Prefabs/UI/PrimaryButton.prefab";
        private const string RedButton = "Assets/Prefabs/UI/RedButton.prefab";
        private const string YellowButton = "Assets/Prefabs/UI/YellowButton.prefab";
        private static readonly Color Ink = C("4A2E22"), Cream = C("FFF3D6"), Yellow = C("F7B92B"), Green = C("34421C");
        private static Sprite rounded;
        private static TMP_FontAsset heading, body, heavy;

        [MenuItem("Imoni/Lobby/Build Lobby Screen")]
        public static void Build()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Stop Play Mode first.");
            var active = SceneManager.GetActiveScene();
            if (active.path != LobbyPath && active.isDirty) throw new InvalidOperationException("Save the currently open scene first.");
            var scene = active.path == LobbyPath ? active : EditorSceneManager.OpenScene(LobbyPath, OpenSceneMode.Single);
            rounded = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/ImoniUI/Home/home_rounded_white.png");
            heading = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/Fonts/Home/Home_DelaGothicOne.asset");
            body = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/Fonts/Home/Home_ZenMaruBold.asset");
            heavy = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/Fonts/Home/Home_ZenMaruBlack.asset");
            if (!rounded || !heading || !body || !heavy) throw new InvalidOperationException("Shared UI resources are missing.");
            ImportLobbySprites();
            string chars = File.ReadAllText("Assets/Editor/ImoniLobbyBuilder.cs") + File.ReadAllText("Assets/Scripts/UI/LobbyScreenController.cs");
            foreach (var font in new[] { heading, body, heavy })
            {
                string pending = new string(chars.Where(c => !char.IsControl(c) && !font.HasCharacter(c)).Distinct().ToArray());
                if (pending.Length > 0 && !font.TryAddCharacters(pending, out var missing)) throw new InvalidOperationException("Missing glyphs: " + missing);
                EditorUtility.SetDirty(font);
            }
            var canvas = scene.GetRootGameObjects().SelectMany(o => o.GetComponentsInChildren<Canvas>(true)).First();
            Undo.RegisterFullObjectHierarchyUndo(canvas.gameObject, "Build Lobby UI");
            for (int i = canvas.transform.childCount - 1; i >= 0; i--) Undo.DestroyObjectImmediate(canvas.transform.GetChild(i).gameObject);
            var scaler = canvas.GetComponent<CanvasScaler>();
            scaler.referenceResolution = new Vector2(390, 844);
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.Expand;
            var background = Image(canvas.transform, "Background", 0, 0, 390, 844, Yellow);
            Stretch(background.rectTransform);
            var root = Rect(canvas.transform, "LobbyScreen", 0, 0, 390, 844);
            root.gameObject.AddComponent<HomeScreenFit>();
            var controller = root.gameObject.AddComponent<LobbyScreenController>();

            BuildViews(root, controller);
            foreach (var component in root.GetComponentsInChildren<Component>(true))
                if (component && PrefabUtility.IsPartOfPrefabInstance(component)) PrefabUtility.RecordPrefabInstancePropertyModifications(component);
            foreach (var node in root.GetComponentsInChildren<Transform>(true))
                if (PrefabUtility.IsPartOfPrefabInstance(node.gameObject)) PrefabUtility.RecordPrefabInstancePropertyModifications(node.gameObject);
            Canvas.ForceUpdateCanvases();
            foreach (var text in root.GetComponentsInChildren<TextMeshProUGUI>(true)) text.ForceMeshUpdate(true);
            PrefabUtility.SaveAsPrefabAssetAndConnect(root.gameObject, ScreenPath, InteractionMode.AutomatedAction);
            var buildScenes = EditorBuildSettings.scenes.ToList();
            foreach (var path in new[] { LobbyPath, "Assets/Scenes/WorldLoading.unity", "Assets/Scenes/Game.unity" })
            {
                if (!File.Exists(path)) continue;
                var entry = buildScenes.FirstOrDefault(s => s.path == path);
                if (entry == null) buildScenes.Add(new EditorBuildSettingsScene(path, true));
                else entry.enabled = true;
            }
            EditorBuildSettings.scenes = buildScenes.ToArray();
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();
            foreach (var text in root.GetComponentsInChildren<TextMeshProUGUI>())
                if (text && text.isActiveAndEnabled && text.canvasRenderer) text.SetAllDirty();
            Canvas.ForceUpdateCanvases();
            Selection.activeGameObject = root.gameObject;
            Debug.Log("IMONI_LOBBY_BUILT: V2 create/join, invitation waiting, common/free seed and ready states saved with shared button and member prefabs.");
        }

        private static void BuildViews(RectTransform root, LobbyScreenController controller)
        {
            var serialized = new SerializedObject(controller);
            var header = Rect(root, "Header", 0, 0, 390, 110);
            SetRef(serialized, "headerRoot", header.gameObject);
            var back = FlatButton(header, "BackToHomeButton", Primary, "", 18, 52, 44, 44, 14, Ink, Color.clear, 0);
            var arrow = Image(back.transform, "BackIcon", 10, 10, 24, 24, Color.white);
            arrow.sprite = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/ImoniUI/Lobby/lobby_back.png");
            SetRef(serialized, "homeNavigation", BindNavigation(back, "Home", false));
            UnityEventTools.AddPersistentListener(back.onClick, controller.Back);
            Text(header, "Title", "ロビー", 70, 55.5f, 280, 37, 26, heading, Green, TextAlignmentOptions.MidlineLeft);

            var choosing = Rect(root, "CreateOrJoin", 0, 0, 390, 844);
            SetRef(serialized, "choosingRoot", choosing.gameObject);
            var create = Panel(choosing, "CreateTeamCard", 18, 110, 354, 183, Cream, Ink, 4, 22);
            Text(create.transform, "Heading", "チームを作る", 20, 20, 314, 26, 17, heavy, Ink, TextAlignmentOptions.MidlineLeft);
            Paragraph(create.transform, "Description", "新しいチームを作って、招待コードで\n仲間を呼ぼう（最大3人）", 20, 56, 314, 40, 13, Ink);
            var createButton = SharedButton(create.transform, "CreateTeamButton", Primary, "チームを作成", 20, 108, 314, 61, 18, heavy, Cream, 56);
            createButton.GetComponent<UnityEngine.UI.Image>().pixelsPerUnitMultiplier = 3;
            UnityEventTools.AddPersistentListener(createButton.onClick, controller.CreateTeam);

            var join = Panel(choosing, "JoinTeamCard", 18, 307, 354, 267, Cream, Ink, 4, 22);
            Text(join.transform, "Heading", "招待コードで参加", 20, 20, 314, 26, 17, heavy, Ink, TextAlignmentOptions.MidlineLeft);
            Text(join.transform, "InputCaption", "招待コード", 20, 56, 314, 18, 12, heavy, Ink, TextAlignmentOptions.MidlineLeft);
            var input = CodeInput(join.transform, 20, 78, 314, 48);
            SetRef(serialized, "codeInput", input);
            UnityEventTools.AddPersistentListener(input.onValueChanged, controller.OnCodeChanged);
            var joinButton = FlatButton(join.transform, "JoinTeamButton", YellowButton, "参加する", 20, 140, 314, 52, 17, Ink, Yellow, 26, Ink, 3);
            UnityEventTools.AddPersistentListener(joinButton.onClick, controller.JoinTeam);
            Paragraph(join.transform, "JoinHint", "仲間から届いた7文字の招待コードを\n入力してください", 20, 207, 314, 36, 12, C("6B4A3A"));
            var error = Panel(choosing, "JoinError", 18, 588, 354, 64, C("FBE3DC"), C("8B2310"), 2, 10);
            var errorText = Paragraph(error.transform, "Message", "", 12, 8, 330, 48, 13, C("8B2310"));
            SetRef(serialized, "joinErrorRoot", error.gameObject);
            SetRef(serialized, "joinErrorLabel", errorText);
            error.gameObject.SetActive(false);

            BuildTeam(root, controller, serialized);
            BuildWaiting(root, controller, serialized);
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void BuildTeam(RectTransform root, LobbyScreenController controller, SerializedObject serialized)
        {
            var teamRoot = Rect(root, "TeamPreparation", 0, 0, 390, 844);
            SetRef(serialized, "teamRoot", teamRoot.gameObject);
            var team = Panel(teamRoot, "TeamCard", 18, 110, 354, 75, Ink, Ink, 0, 18);
            Text(team.transform, "TeamCaption", "チーム", 16, 14, 160, 16, 11, body, Cream, TextAlignmentOptions.MidlineLeft);
            Text(team.transform, "TeamName", "鍋底同盟", 16, 32, 160, 29, 20, heavy, Cream, TextAlignmentOptions.MidlineLeft);
            Text(team.transform, "InviteCaption", "招待コード", 200, 14, 138, 16, 11, body, Cream, TextAlignmentOptions.MidlineRight);
            var invite = Text(team.transform, "InviteCode", "IMO-482", 197, 32, 141, 29, 20, heading, Yellow, TextAlignmentOptions.MidlineRight);
            invite.characterSpacing = 10;
            SetRef(serialized, "inviteCodeLabel", invite);

            var members = Panel(teamRoot, "MembersCard", 18, 199, 354, 311, Cream, Ink, 4, 22);
            Text(members.transform, "MemberCount", "メンバー 3/3", 18, 14, 318, 21, 15, heavy, Ink, TextAlignmentOptions.MidlineLeft);
            EnsureMemberPrefab();
            var own = CompactMember(members.transform, "LocalMember", "芋煮会の初心者", "ゆくらさん(あなた)", C("8FAFC4"), 42, false);
            CompactMember(members.transform, "TaroMember", "伝説の鍋奉行", "たろう", C("C9A27A"), 112, true);
            CompactMember(members.transform, "HanakoMember", "山の採集王", "はなこ", C("6B9B3A"), 182, true);
            var copy = FlatButton(members.transform, "CopyInviteCodeButton", Primary, "招待コードをコピー", 18, 254, 318, 40, 14, Ink, Cream, 20, Ink, 3);
            UnityEventTools.AddPersistentListener(copy.onClick, controller.CopyInviteCode);
            SetRef(serialized, "ownStatusBackground", own.transform.Find("Status").GetComponent<UnityEngine.UI.Image>());
            SetRef(serialized, "ownStatusLabel", own.transform.Find("Status/Label").GetComponent<TMP_Text>());
            SetRef(serialized, "copyCodeLabel", copy.GetComponentInChildren<TMP_Text>());

            var modes = Panel(teamRoot, "ModeCard", 18, 524, 354, 154, Cream, Ink, 3, 18);
            Text(modes.transform, "Heading", "モード・シード", 17, 12, 320, 22, 14, heavy, Ink, TextAlignmentOptions.MidlineLeft);
            var common = FlatButton(modes.transform, "CommonSeedButton", Primary, "共通シード\n<size=9>今週 #2026-47・共通シード部門</size>", 17, 43, 156, 52, 13, Cream, Green, 12, Green, 3);
            var free = FlatButton(modes.transform, "FreeSeedButton", YellowButton, "自由シード\n<size=10>ランダム生成・自由シード部門</size>", 181, 43, 156, 52, 13, Green, Color.white, 12, Green, 3);
            UnityEventTools.AddIntPersistentListener(common.onClick, controller.SelectSeed, 0);
            UnityEventTools.AddIntPersistentListener(free.onClick, controller.SelectSeed, 1);
            SetRefs(serialized, "seedBackgrounds", common.transform.Find("Fill").GetComponent<UnityEngine.UI.Image>(), free.transform.Find("Fill").GetComponent<UnityEngine.UI.Image>());
            SetRefs(serialized, "seedLabels", common.GetComponentInChildren<TMP_Text>(), free.GetComponentInChildren<TMP_Text>());
            Paragraph(modes.transform, "ModeHint", "どちらもランキング対象（部門は別）。\n通常モード5分×5ラウンド\nエンドレスは通常クリアで解放", 17, 104, 320, 39, 10.5f, C("6B4A3A"));

            var notReady = Rect(teamRoot, "NotReadyActions", 18, 740, 354, 69);
            var ready = SharedButton(notReady, "ReadyButton", Primary, "準備完了", 0, 0, 354, 69, 24, heading, Cream, 64);
            ready.GetComponent<UnityEngine.UI.Image>().pixelsPerUnitMultiplier = 3;
            UnityEventTools.AddPersistentListener(ready.onClick, controller.ToggleReady);
            var actions = Rect(teamRoot, "ReadyActions", 18, 710, 354, 116);
            var start = SharedButton(actions, "StartGameButton", RedButton, "ワールド生成してスタート", 0, 0, 354, 69, 22, heading, Cream, 64);
            SetSprite(start, "Assets/ImoniUI/btn_pill_red.png", 3);
            var gameNavigation = BindNavigation(start, "WorldLoading", false);
            UnityEventTools.AddPersistentListener(start.onClick, controller.StartGame);
            var cancel = FlatButton(actions, "CancelReadyButton", Primary, "準備を取り消す", 0, 72, 354, 44, 14, Ink, Color.clear, 0);
            cancel.GetComponentInChildren<TextMeshProUGUI>().fontStyle = FontStyles.Underline;
            UnityEventTools.AddPersistentListener(cancel.onClick, controller.ToggleReady);
            SetRef(serialized, "notReadyActions", notReady.gameObject);
            SetRef(serialized, "readyActions", actions.gameObject);
            SetRef(serialized, "startButton", start);
            SetRef(serialized, "gameNavigation", gameNavigation);
            actions.gameObject.SetActive(false);
            teamRoot.gameObject.SetActive(false);
        }

        private static void BuildWaiting(RectTransform root, LobbyScreenController controller, SerializedObject serialized)
        {
            var waiting = Rect(root, "WaitingForMembers", 0, 0, 390, 844);
            SetRef(serialized, "waitingRoot", waiting.gameObject);
            var cancel = FlatButton(waiting, "CancelWaiting", Primary, "‹ キャンセル", 18, 52, 118, 44, 14, Ink, Color.clear, 0);
            UnityEventTools.AddPersistentListener(cancel.onClick, controller.Back);
            var seed = Panel(waiting, "SeedBadge", 199, 60, 173, 28, Cream, Ink, 2, 14);
            SetRef(serialized, "waitingSeedLabel", Text(seed.transform, "Label", "共通シード #2026-47", 3, 0, 167, 28, 11, heavy, Ink));
            var pot = Image(waiting, "PotIllustration", 18, 111, 72, 75, Color.white);
            pot.sprite = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/ImoniUI/Home/home_pot.png");
            pot.preserveAspect = true;
            SetRef(serialized, "waitingHeadline", Text(waiting, "Headline", "メンバーを\n待っています", 102, 106, 270, 61, 22, heading, Green, TextAlignmentOptions.MidlineLeft));
            SetRef(serialized, "waitingSubline", Text(waiting, "Subline", "チームは3人で挑戦するよ", 102, 170, 270, 20, 13, body, Ink, TextAlignmentOptions.MidlineLeft));
            var invite = Panel(waiting, "InviteCard", 18, 204, 354, 166, Ink, Ink, 0, 22);
            Text(invite.transform, "Caption", "招待コード", 16, 14, 130, 20, 13, heavy, Cream, TextAlignmentOptions.MidlineLeft);
            Text(invite.transform, "TeamName", "チーム「鍋底同盟」", 170, 14, 168, 20, 11, body, C("DCC9A8"), TextAlignmentOptions.MidlineRight);
            var code = Text(invite.transform, "Code", "IMO-482", 16, 40, 322, 61, 39, heading, Yellow);
            code.characterSpacing = 8;
            SetRef(serialized, "waitingCodeLabel", code);
            var copy = FlatButton(invite.transform, "CopyCode", YellowButton, "コードをコピー", 16, 108, 157, 44, 14, Ink, Yellow, 22);
            var share = FlatButton(invite.transform, "ShareCode", Primary, "共有する", 181, 108, 157, 44, 13, Cream, Ink, 22, Yellow, 2);
            UnityEventTools.AddPersistentListener(copy.onClick, controller.CopyInviteCode);
            UnityEventTools.AddPersistentListener(share.onClick, controller.ShareInviteCode);
            SetRef(serialized, "waitingCopyLabel", copy.GetComponentInChildren<TMP_Text>());
            SetRef(serialized, "waitingShareLabel", share.GetComponentInChildren<TMP_Text>());

            var members = Panel(waiting, "WaitingMembers", 18, 384, 354, 263, Cream, Ink, 4, 22);
            Text(members.transform, "Heading", "メンバー", 18, 14, 200, 25, 15, heavy, Ink, TextAlignmentOptions.MidlineLeft);
            SetRef(serialized, "waitingCountLabel", Text(members.transform, "Count", "1/3人", 220, 14, 116, 25, 18, heading, C("8B2310"), TextAlignmentOptions.MidlineRight));
            string[] names = { "ゆくらさん(あなた)", "たろう", "はなこ" };
            string[] titles = { "芋煮会の初心者", "伝説の鍋奉行", "山の採集王" };
            Color[] avatars = { C("8FAFC4"), C("C9A27A"), C("6B9B3A") };
            Object[] filledSlots = new Object[3], emptySlots = new Object[3], emptyLabels = new Object[3];
            for (int i = 0; i < 3; i++)
            {
                var member = CompactMember(members.transform, "Member" + i, titles[i], names[i], avatars[i], 47 + i * 68, true);
                member.transform.Find("Status/Label").GetComponent<TMP_Text>().text = i == 0 ? "ホスト" : "参加!";
                filledSlots[i] = member;
                member.SetActive(i == 0);
                var empty = Panel(members.transform, "EmptySlot" + i, 18, 47 + i * 68, 318, 62, C("FBEBC8"), C("8C6A2E"), 2, 14);
                Panel(empty.transform, "Avatar", 10, 10, 42, 42, C("FBEBC8"), C("8C6A2E"), 2, 21);
                emptyLabels[i] = Text(empty.transform, "WaitingLabel", "参加を待っています...", 64, 8, 242, 46, 13, heavy, C("5C4518"), TextAlignmentOptions.MidlineLeft);
                emptySlots[i] = empty.gameObject;
                empty.gameObject.SetActive(i != 0);
            }
            SetRefs(serialized, "waitingFilledSlots", filledSlots);
            SetRefs(serialized, "waitingEmptySlots", emptySlots);
            SetRefs(serialized, "waitingEmptyLabels", emptyLabels);
            var next = FlatButton(waiting, "ContinueToPreparation", RedButton, "あと2人そろうとスタートできます", 18, 714, 354, 64, 16, C("5C4518"), C("EFD38A"), 32, C("8C6A2E"), 3);
            var nextColors = next.colors;
            nextColors.disabledColor = Color.white;
            next.colors = nextColors;
            next.interactable = false;
            UnityEventTools.AddPersistentListener(next.onClick, controller.EnterTeamPreparation);
            SetRef(serialized, "waitingContinueButton", next);
            SetRef(serialized, "waitingContinueBackground", next.transform.Find("Fill").GetComponent<UnityEngine.UI.Image>());
            SetRef(serialized, "waitingContinueLabel", next.GetComponentInChildren<TMP_Text>());
            Paragraph(waiting, "InviteHint", "招待コードを仲間に送ってね。\n挑戦が始まると途中参加はできません", 18, 789, 354, 32, 11, Ink).alignment = TextAlignmentOptions.Midline;
            waiting.gameObject.SetActive(false);
        }

        private static GameObject CompactMember(Transform parent, string name, string title, string label, Color avatar, float y, bool ready)
        {
            var row = Member(parent, name, title, label, avatar, y, ready);
            SetRect((RectTransform)row.transform, 18, y, 318, 62);
            SetRect((RectTransform)row.transform.Find("Fill"), 2, 2, 314, 58);
            SetRect((RectTransform)row.transform.Find("Avatar"), 10, 10, 42, 42);
            SetRect((RectTransform)row.transform.Find("Avatar/Fill"), 3, 3, 36, 36);
            var titleLabel = row.transform.Find("Title").GetComponent<TMP_Text>();
            SetRect(titleLabel.rectTransform, 64, 10, 165, 17);
            var nameLabel = row.transform.Find("Name").GetComponent<TMP_Text>();
            SetRect(nameLabel.rectTransform, 64, 29, 175, 23);
            nameLabel.fontSize = 14;
            var status = (RectTransform)row.transform.Find("Status");
            SetRect(status, 306, 31, 60, 25);
            status.pivot = new Vector2(1, .5f);
            return row;
        }

        private static Button FlatButton(Transform parent, string name, string prefab, string caption, float x, float y, float w, float h,
            float size, Color foreground, Color background, float radius, Color border = default, float stroke = 0)
        {
            var button = SharedButton(parent, name, prefab, caption, x, y, w, h, size, heavy, foreground);
            var image = button.GetComponent<UnityEngine.UI.Image>();
            image.sprite = rounded;
            image.type = UnityEngine.UI.Image.Type.Sliced;
            image.pixelsPerUnitMultiplier = radius > 0 ? 32 / radius : 1;
            image.color = stroke > 0 ? border : background;
            if (stroke > 0)
            {
                var fill = Image(button.transform, "Fill", stroke, stroke, w - 2 * stroke, h - 2 * stroke, background, Mathf.Max(1, radius - stroke));
                fill.transform.SetAsFirstSibling();
            }
            button.navigation = new Navigation { mode = Navigation.Mode.None };
            return button;
        }

        private static TMP_InputField CodeInput(Transform parent, float x, float y, float w, float h)
        {
            var panel = Panel(parent, "InviteCodeInput", x, y, w, h, Color.white, Green, 3, 10);
            panel.raycastTarget = true;
            var input = panel.gameObject.AddComponent<TMP_InputField>();
            var area = Rect(panel.transform, "TextArea", 12, 3, w - 24, h - 6);
            area.gameObject.AddComponent<RectMask2D>();
            var text = Text(area, "Text", "", 0, 0, w - 24, h - 6, 20, heavy, Ink, TextAlignmentOptions.MidlineLeft);
            text.characterSpacing = 10;
            var placeholder = Text(area, "Placeholder", "IMO-000", 0, 0, w - 24, h - 6, 20, heavy, C("A08E78"), TextAlignmentOptions.MidlineLeft);
            placeholder.characterSpacing = 10;
            input.textViewport = area;
            input.textComponent = text;
            input.placeholder = placeholder;
            input.targetGraphic = panel;
            input.fontAsset = heavy;
            input.characterLimit = 7;
            input.contentType = TMP_InputField.ContentType.Standard;
            input.lineType = TMP_InputField.LineType.SingleLine;
            input.caretColor = Green;
            input.customCaretColor = true;
            input.selectionColor = new Color(.42f, .61f, .23f, .3f);
            return input;
        }

        private static TextMeshProUGUI Paragraph(Transform parent, string name, string value, float x, float y, float w, float h, float size, Color color)
        {
            var text = Text(parent, name, value, x, y, w, h, size, heavy, color, TextAlignmentOptions.MidlineLeft);
            text.textWrappingMode = TextWrappingModes.Normal;
            return text;
        }

        private static void SetRefs(SerializedObject target, string name, params Object[] values)
        {
            var property = target.FindProperty(name);
            property.arraySize = values.Length;
            for (int i = 0; i < values.Length; i++) property.GetArrayElementAtIndex(i).objectReferenceValue = values[i];
        }

        private static void EnsureMemberPrefab()
        {
            if (AssetDatabase.LoadAssetAtPath<GameObject>(MemberPath)) return;
            var row = Panel(null, "LobbyMemberRow", 0, 0, 318, 74, Color.white, Ink, 2, 14);
            Panel(row.transform, "Avatar", 12, 12, 50, 50, C("8FAFC4"), Ink, 3, 25);
            Text(row.transform, "Title", "芋煮会の初心者", 74, 16, 160, 16, 11, heavy, Green, TextAlignmentOptions.MidlineLeft);
            Text(row.transform, "Name", "ゆくらさん(あなた)", 74, 34, 160, 24, 16, heavy, Ink, TextAlignmentOptions.MidlineLeft);
            var status = Image(row.transform, "Status", 306, 37, 56, 25, C("EFD38A"), 12.5f);
            status.rectTransform.pivot = new Vector2(1, .5f);
            var text = Text(status.transform, "Label", "準備中", 0, 0, 56, 25, 12, heavy, Ink);
            Stretch(text.rectTransform);
            PrefabUtility.SaveAsPrefabAsset(row.gameObject, MemberPath);
            Object.DestroyImmediate(row.gameObject);
        }

        private static GameObject Member(Transform parent, string name, string title, string label, Color avatar, float y, bool ready)
        {
            var source = AssetDatabase.LoadAssetAtPath<GameObject>(MemberPath);
            var row = (GameObject)PrefabUtility.InstantiatePrefab(source, parent);
            row.name = name;
            SetRect((RectTransform)row.transform, 18, y, 318, 74);
            row.transform.Find("Title").GetComponent<TMP_Text>().text = title;
            row.transform.Find("Name").GetComponent<TMP_Text>().text = label;
            row.transform.Find("Avatar/Fill").GetComponent<UnityEngine.UI.Image>().color = avatar;
            var badge = row.transform.Find("Status").GetComponent<UnityEngine.UI.Image>();
            badge.color = ready ? Green : C("EFD38A");
            badge.rectTransform.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, ready ? 60.05f : 56);
            var badgeLabel = row.transform.Find("Status/Label").GetComponent<TMP_Text>();
            badgeLabel.text = ready ? "準備OK" : "準備中";
            badgeLabel.color = ready ? Cream : Ink;
            foreach (var component in row.GetComponentsInChildren<Component>()) PrefabUtility.RecordPrefabInstancePropertyModifications(component);
            PrefabUtility.RecordPrefabInstancePropertyModifications(row);
            return row;
        }

        private static Button SharedButton(Transform parent, string name, string asset, string caption, float x, float y, float width, float height,
            float size, TMP_FontAsset font, Color color, float faceHeight = 0)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(asset);
            if (!prefab) throw new InvalidOperationException("Missing button prefab: " + asset);
            var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent);
            instance.name = name;
            SetRect((RectTransform)instance.transform, x, y, width, height);
            var label = instance.GetComponentInChildren<TextMeshProUGUI>();
            SetRect(label.rectTransform, 0, 0, width, faceHeight > 0 ? faceHeight : height);
            label.text = caption;
            label.font = font;
            label.fontSharedMaterial = font.material;
            label.fontSize = size;
            label.enableAutoSizing = false;
            label.fontStyle = FontStyles.Normal;
            label.color = color;
            label.alignment = TextAlignmentOptions.Midline;
            label.textWrappingMode = TextWrappingModes.NoWrap;
            label.richText = true;
            label.raycastTarget = false;
            return instance.GetComponent<Button>();
        }

        private static SceneNavigationButton BindNavigation(Button button, string target, bool direct)
        {
            var handler = button.gameObject.AddComponent<SceneNavigationButton>();
            var settings = new SerializedObject(handler);
            settings.FindProperty("targetSceneName").stringValue = target;
            settings.ApplyModifiedPropertiesWithoutUndo();
            if (direct) UnityEventTools.AddPersistentListener(button.onClick, handler.Navigate);
            return handler;
        }

        private static void SetSprite(Button button, string path, float scale)
        {
            var image = button.GetComponent<UnityEngine.UI.Image>();
            image.sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);
            image.type = UnityEngine.UI.Image.Type.Sliced;
            image.pixelsPerUnitMultiplier = scale;
            if (!image.sprite) throw new InvalidOperationException("Missing sprite: " + path);
        }

        private static RectTransform Rect(Transform parent, string name, float x, float y, float width, float height)
        {
            var rect = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
            rect.gameObject.layer = 5;
            rect.SetParent(parent, false);
            SetRect(rect, x, y, width, height);
            return rect;
        }

        private static void SetRect(RectTransform rect, float x, float y, float width, float height)
        {
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0, 1);
            rect.anchoredPosition = new Vector2(x, -y);
            rect.sizeDelta = new Vector2(width, height);
            rect.localScale = Vector3.one;
            rect.localRotation = Quaternion.identity;
        }

        private static void Stretch(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = rect.offsetMax = Vector2.zero;
        }

        private static UnityEngine.UI.Image Image(Transform parent, string name, float x, float y, float w, float h, Color color, float radius = 0)
        {
            var image = Rect(parent, name, x, y, w, h).gameObject.AddComponent<UnityEngine.UI.Image>();
            image.color = color;
            image.raycastTarget = false;
            if (radius > 0) { image.sprite = rounded; image.type = UnityEngine.UI.Image.Type.Sliced; image.pixelsPerUnitMultiplier = 32 / radius; }
            return image;
        }

        private static UnityEngine.UI.Image Panel(Transform parent, string name, float x, float y, float w, float h, Color fill, Color border, float stroke, float radius)
        {
            var image = Image(parent, name, x, y, w, h, stroke > 0 ? border : fill, radius);
            if (stroke > 0) Image(image.transform, "Fill", stroke, stroke, w - 2 * stroke, h - 2 * stroke, fill, radius - stroke);
            return image;
        }

        private static TextMeshProUGUI Text(Transform parent, string name, string value, float x, float y, float w, float h, float size, TMP_FontAsset font, Color color,
            TextAlignmentOptions alignment = TextAlignmentOptions.Midline)
        {
            var text = Rect(parent, name, x, y, w, h).gameObject.AddComponent<TextMeshProUGUI>();
            text.text = value; text.font = font; text.fontSize = size; text.color = color; text.alignment = alignment;
            text.textWrappingMode = TextWrappingModes.NoWrap; text.raycastTarget = false;
            return text;
        }

        private static void SetRef(SerializedObject target, string name, Object value) => target.FindProperty(name).objectReferenceValue = value;
        private static Color C(string value) { ColorUtility.TryParseHtmlString("#" + value, out var color); return color; }

        private static void ImportLobbySprites()
        {
            foreach (var path in Directory.GetFiles("Assets/ImoniUI/Lobby", "*.png"))
            {
                var importer = (TextureImporter)AssetImporter.GetAtPath(path.Replace('\\', '/'));
                importer.textureType = TextureImporterType.Sprite;
                importer.spriteImportMode = SpriteImportMode.Single;
                importer.spritePixelsPerUnit = 300;
                importer.mipmapEnabled = false;
                importer.alphaIsTransparency = true;
                importer.textureCompression = TextureImporterCompression.Uncompressed;
                importer.npotScale = TextureImporterNPOTScale.None;
                importer.filterMode = FilterMode.Bilinear;
                importer.wrapMode = TextureWrapMode.Clamp;
                importer.SaveAndReimport();
            }
        }
    }
}
