using System;
using System.IO;
using System.Reflection;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace Imoni.UI.Editor
{
    /// <summary>Renders the saved native UI prefabs without modifying the user's scenes.</summary>
    public static class ImoniMockV2Preview
    {
        [MenuItem("Imoni/Capture Updated UI Previews")]
        public static void Capture()
        {
            using var session = ImoniRoundFlowValidation.PreserveSessionState();
            Render("HomeScreen", "home");
            Render("LobbyScreen", "lobby_choose", root => Invoke(root.GetComponent<LobbyScreenController>(), "OnEnable"));
            Render("LobbyScreen", "lobby_waiting", root => {
                var c = root.GetComponent<LobbyScreenController>(); Invoke(c, "OnEnable"); c.CreateTeam();
            });
            Render("LobbyScreen", "lobby_ready", root => {
                var c = root.GetComponent<LobbyScreenController>(); Invoke(c, "OnEnable");
                root.GetComponentInChildren<TMP_InputField>(true).SetTextWithoutNotify("IMO-482");
                c.JoinTeam(); c.ToggleReady();
            });
            Render("WorldLoadingScreen", "world_loading");
            Render("GameScreen", "game", root => Invoke(root.GetComponent<GameScreenController>(), "Start"));
            Render("GameScreen", "shop", root => {
                var c = root.GetComponent<GameScreenController>(); Invoke(c, "Start"); c.OpenShop();
            });
            Render("GameScreen", "warehouse", root => {
                var c = root.GetComponent<GameScreenController>(); Invoke(c, "Start");
                c.PurchaseShopItem(1); c.PurchaseShopItem(2); c.PurchaseShopItem(4);
                c.OpenWarehouse(); c.SelectInventory(0);
            });
            Render("GameScreen", "upgrade", root => {
                var c = root.GetComponent<GameScreenController>(); Invoke(c, "Start"); c.OpenUpgrade();
            });
            Render("GameScreen", "team_menu", root => {
                var c = root.GetComponent<GameScreenController>(); Invoke(c, "Start"); c.SetMemberConnected(2, false); c.OpenMenu();
            });
            Debug.Log("IMONI_V2_PREVIEWS_SAVED: Library/MockV2Preview");
        }

        internal static void Render(string prefabName, string name, Action<GameObject> prepare = null)
        {
            var scene = EditorSceneManager.NewPreviewScene();
            RenderTexture target = null;
            Texture2D pixels = null;
            var previousTarget = RenderTexture.active;
            var random = UnityEngine.Random.state;
            try
            {
                var cameraObject = new GameObject("PreviewCamera", typeof(Camera));
                SceneManager.MoveGameObjectToScene(cameraObject, scene);
                var camera = cameraObject.GetComponent<Camera>();
                camera.scene = scene;
                camera.transform.position = new Vector3(0, 0, -100);
                camera.orthographic = true; camera.orthographicSize = 422;
                camera.clearFlags = CameraClearFlags.SolidColor;
                camera.backgroundColor = new Color32(247, 185, 43, 255);
                camera.cullingMask = 1 << 5;
                var canvasObject = new GameObject("PreviewCanvas", typeof(RectTransform), typeof(Canvas));
                canvasObject.layer = 5;
                SceneManager.MoveGameObjectToScene(canvasObject, scene);
                var canvas = canvasObject.GetComponent<Canvas>();
                canvas.renderMode = RenderMode.WorldSpace; canvas.worldCamera = camera;
                ((RectTransform)canvas.transform).sizeDelta = new Vector2(390, 844);
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/UI/" + prefabName + ".prefab");
                if (!prefab) throw new InvalidOperationException("Missing preview prefab " + prefabName);
                var root = Object.Instantiate(prefab, canvas.transform, false);
                var fit = root.GetComponent<HomeScreenFit>();
                if (fit) fit.enabled = false;
                var rect = (RectTransform)root.transform;
                rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(.5f, .5f);
                rect.anchoredPosition = Vector2.zero; rect.sizeDelta = new Vector2(390, 844); rect.localScale = Vector3.one;
                prepare?.Invoke(root);
                foreach (var fill in root.GetComponentsInChildren<RoundedProgressFill>(true)) fill.Refresh();
                LayoutRebuilder.ForceRebuildLayoutImmediate(rect);
                Canvas.ForceUpdateCanvases();
                foreach (var text in root.GetComponentsInChildren<TMP_Text>(true)) text.ForceMeshUpdate(true);
                target = new RenderTexture(780, 1688, 24, RenderTextureFormat.ARGB32);
                target.antiAliasing = 4; target.Create(); camera.targetTexture = target;
                camera.Render();
                RenderTexture.active = target;
                pixels = new Texture2D(target.width, target.height, TextureFormat.RGB24, false);
                pixels.ReadPixels(new Rect(0, 0, target.width, target.height), 0, 0); pixels.Apply();
                Directory.CreateDirectory("Library/MockV2Preview");
                File.WriteAllBytes("Library/MockV2Preview/" + name + ".png", pixels.EncodeToPNG());
            }
            finally
            {
                UnityEngine.Random.state = random;
                RenderTexture.active = previousTarget;
                if (pixels) Object.DestroyImmediate(pixels);
                if (target) { target.Release(); Object.DestroyImmediate(target); }
                EditorSceneManager.ClosePreviewScene(scene);
            }
        }

        private static void Invoke(object instance, string method)
        {
            instance.GetType().GetMethod(method, BindingFlags.Instance | BindingFlags.NonPublic).Invoke(instance, null);
        }
    }
}
