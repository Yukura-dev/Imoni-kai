using System;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Imoni.UI.Editor
{
    public static class ImoniHomeNavigationSetup
    {
        private const string HomePrefabPath = "Assets/Prefabs/UI/HomeScreen.prefab";
        private const string LobbyPath = "Assets/Scenes/Lobby.unity";

        [MenuItem("Imoni/Home/Connect Play To Lobby")]
        public static void Setup()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("Stop Play Mode before connecting scene navigation.");
            if (!AssetDatabase.LoadAssetAtPath<SceneAsset>(LobbyPath)) CreateLobby();
            var scenes = EditorBuildSettings.scenes.ToList();
            var lobby = scenes.FirstOrDefault(scene => scene.path == LobbyPath);
            if (lobby == null) scenes.Add(new EditorBuildSettingsScene(LobbyPath, true));
            else lobby.enabled = true;
            EditorBuildSettings.scenes = scenes.ToArray();

            var home = PrefabUtility.LoadPrefabContents(HomePrefabPath);
            try
            {
                var play = home.transform.Find("PlayButton");
                if (!play) throw new InvalidOperationException("Home PlayButton was not found.");
                BindPlayButton(play.gameObject);
                PrefabUtility.SaveAsPrefabAsset(home, HomePrefabPath);
            }
            finally { PrefabUtility.UnloadPrefabContents(home); }
            AssetDatabase.SaveAssets();
            Debug.Log("IMONI_LOBBY_CONNECTED: Home PlayButton -> Lobby; Lobby is enabled in the build scene list.");
        }

        public static void BindPlayButton(GameObject play) => Bind(play, "Lobby");

        private static void Bind(GameObject target, string sceneName)
        {
            var button = target.GetComponent<Button>();
            if (!button) throw new InvalidOperationException("Scene navigation requires a Button.");
            var handler = target.GetComponent<SceneNavigationButton>();
            if (!handler) handler = target.AddComponent<SceneNavigationButton>();
            var settings = new SerializedObject(handler);
            settings.FindProperty("targetSceneName").stringValue = sceneName;
            settings.ApplyModifiedPropertiesWithoutUndo();
            var alreadyBound = Enumerable.Range(0, button.onClick.GetPersistentEventCount()).Any(i =>
                button.onClick.GetPersistentTarget(i) == handler && button.onClick.GetPersistentMethodName(i) == nameof(SceneNavigationButton.Navigate));
            if (!alreadyBound) UnityEventTools.AddPersistentListener(button.onClick, handler.Navigate);
            EditorUtility.SetDirty(handler);
            EditorUtility.SetDirty(button);
            if (PrefabUtility.IsPartOfPrefabInstance(button))
                PrefabUtility.RecordPrefabInstancePropertyModifications(button);
        }

        private static void CreateLobby()
        {
            var previous = SceneManager.GetActiveScene();
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
            SceneManager.SetActiveScene(scene);
            try
            {
                var yellow = new Color32(247, 185, 43, 255);
                var cameraObject = new GameObject("Main Camera", typeof(Camera), typeof(AudioListener));
                cameraObject.tag = "MainCamera";
                var camera = cameraObject.GetComponent<Camera>();
                camera.clearFlags = CameraClearFlags.SolidColor;
                camera.backgroundColor = yellow;
                camera.orthographic = true;
                camera.transform.position = new Vector3(0, 0, -10);

                var canvasObject = new GameObject("Canvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
                canvasObject.layer = 5;
                canvasObject.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
                var scaler = canvasObject.GetComponent<CanvasScaler>();
                scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
                scaler.referenceResolution = new Vector2(390, 844);
                scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.Expand;

                var events = new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
                events.GetComponent<InputSystemUIInputModule>().AssignDefaultActions();
                var artboard = new GameObject("LobbyScreen", typeof(RectTransform), typeof(HomeScreenFit));
                artboard.layer = 5;
                artboard.transform.SetParent(canvasObject.transform, false);
                var heading = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/Fonts/Home/Home_DelaGothicOne.asset");
                if (!heading) throw new InvalidOperationException("The Home heading font is missing.");

                var titleObject = new GameObject("Title", typeof(RectTransform), typeof(TextMeshProUGUI));
                titleObject.layer = 5;
                titleObject.transform.SetParent(artboard.transform, false);
                Center((RectTransform)titleObject.transform, new Vector2(0, 80), new Vector2(354, 60));
                var title = titleObject.GetComponent<TextMeshProUGUI>();
                title.text = "ロビー";
                title.font = heading;
                title.fontSize = 34;
                title.color = new Color32(139, 35, 16, 255);
                title.alignment = TextAlignmentOptions.Midline;
                title.raycastTarget = false;

                var primary = AssetDatabase.LoadAssetAtPath<GameObject>(ImoniHomePrefabTools.PrimaryPath);
                var back = (GameObject)PrefabUtility.InstantiatePrefab(primary, artboard.transform);
                back.name = "BackToHomeButton";
                Center((RectTransform)back.transform, new Vector2(0, -35), new Vector2(354, 69));
                back.GetComponent<Image>().pixelsPerUnitMultiplier = 3;
                var label = back.GetComponentInChildren<TextMeshProUGUI>();
                label.text = "ホームへ戻る";
                label.font = heading;
                label.fontSharedMaterial = heading.material;
                label.fontSize = 24;
                label.raycastTarget = false;
                label.textWrappingMode = TextWrappingModes.NoWrap;
                Bind(back, "Home");
                foreach (var component in back.GetComponentsInChildren<Component>())
                    if (component && PrefabUtility.IsPartOfPrefabInstance(component))
                        PrefabUtility.RecordPrefabInstancePropertyModifications(component);
                PrefabUtility.RecordPrefabInstancePropertyModifications(back);
                Canvas.ForceUpdateCanvases();
                foreach (var text in artboard.GetComponentsInChildren<TextMeshProUGUI>()) text.ForceMeshUpdate();
                EditorSceneManager.SaveScene(scene, LobbyPath);
            }
            finally
            {
                SceneManager.SetActiveScene(previous);
                EditorSceneManager.CloseScene(scene, true);
            }
        }

        private static void Center(RectTransform rect, Vector2 position, Vector2 size)
        {
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(.5f, .5f);
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
            rect.localScale = Vector3.one;
        }
    }
}
