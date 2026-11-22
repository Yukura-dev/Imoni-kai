using System;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace Imoni.UI.Editor
{
    /// <summary>Reuses the project's shared UI prefabs without rebuilding the home scene.</summary>
    public static class ImoniHomePrefabTools
    {
        public const string PrimaryPath = "Assets/Prefabs/UI/PrimaryButton.prefab";
        public const string NavigationPath = "Assets/Prefabs/UI/BottomNavigation.prefab";
        private const string HomePath = "Assets/Prefabs/UI/HomeScreen.prefab";

        [MenuItem("Imoni/Home/Reuse Shared UI Prefabs")]
        public static void UpgradeHomePrefab()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("Stop Play Mode before editing the Home prefab.");
            var home = PrefabUtility.LoadPrefabContents(HomePath);
            try
            {
                var oldPlay = home.transform.Find("PlayButton");
                var oldShadow = home.transform.Find("PlayButtonShadow");
                if (!oldPlay || PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(oldPlay.gameObject) != PrimaryPath)
                {
                    var sibling = oldPlay ? oldPlay.GetSiblingIndex() : home.transform.childCount;
                    var heading = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/Fonts/Home/Home_DelaGothicOne.asset");
                    var play = CreatePlayButton(home.transform, heading);
                    play.transform.SetSiblingIndex(sibling);
                    if (oldPlay)
                    {
                        var oldButton = oldPlay.GetComponent<Button>();
                        if (oldButton)
                        {
                            var button = play.GetComponent<Button>();
                            button.onClick = oldButton.onClick;
                            PrefabUtility.RecordPrefabInstancePropertyModifications(button);
                        }
                        Object.DestroyImmediate(oldPlay.gameObject);
                    }
                }
                if (oldShadow) Object.DestroyImmediate(oldShadow.gameObject);
                UseBottomNavigationPrefab(home.transform);
                PrefabUtility.SaveAsPrefabAsset(home, HomePath);
            }
            finally { PrefabUtility.UnloadPrefabContents(home); }
            AssetDatabase.SaveAssets();
            Validate();
            Debug.Log("IMONI_SHARED_UI_SAVED: PlayButton uses PrimaryButton (including its shadow); BottomNavigation is a nested prefab.");
        }

        public static GameObject CreatePlayButton(Transform parent, TMP_FontAsset heading)
        {
            var source = AssetDatabase.LoadAssetAtPath<GameObject>(PrimaryPath);
            if (!source || !heading) throw new InvalidOperationException("PrimaryButton or the Home heading font is missing.");
            var instance = (GameObject)PrefabUtility.InstantiatePrefab(source, parent);
            instance.name = "PlayButton";
            // The source sprite already includes the lower shadow. No separate shadow object is needed.
            SetRect((RectTransform)instance.transform, 18, 632, 354, 78);
            var image = instance.GetComponent<Image>();
            image.pixelsPerUnitMultiplier = 8f / 3f;
            PrefabUtility.RecordPrefabInstancePropertyModifications(image);
            PrefabUtility.RecordPrefabInstancePropertyModifications(instance);

            var label = instance.GetComponentInChildren<TextMeshProUGUI>(true);
            if (!label) throw new InvalidOperationException("PrimaryButton must contain a TMP label.");
            SetRect(label.rectTransform, 130, 16, 130, 40);
            label.text = "プレイ開始";
            label.font = heading;
            label.fontSharedMaterial = heading.material;
            label.fontSize = 26;
            label.enableAutoSizing = false;
            label.fontStyle = FontStyles.Normal;
            label.alignment = TextAlignmentOptions.Midline;
            label.textWrappingMode = TextWrappingModes.NoWrap;
            label.overflowMode = TextOverflowModes.Overflow;
            label.raycastTarget = false;
            PrefabUtility.RecordPrefabInstancePropertyModifications(label);

            var iconObject = new GameObject("PlayIcon", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            iconObject.layer = 5;
            iconObject.transform.SetParent(instance.transform, false);
            SetRect((RectTransform)iconObject.transform, 94, 23, 26, 26);
            var icon = iconObject.GetComponent<Image>();
            icon.sprite = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/ImoniUI/Home/home_play.png");
            icon.preserveAspect = true;
            icon.raycastTarget = false;
            return instance;
        }

        public static void UseBottomNavigationPrefab(Transform homeRoot)
        {
            var oldNav = homeRoot.Find("BottomNavigation");
            if (!oldNav) throw new InvalidOperationException("Home has no BottomNavigation to extract.");
            if (PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(oldNav.gameObject) == NavigationPath) return;
            var sibling = oldNav.GetSiblingIndex();
            var source = AssetDatabase.LoadAssetAtPath<GameObject>(NavigationPath);
            if (!source)
            {
                var copy = Object.Instantiate(oldNav.gameObject, homeRoot);
                copy.name = "BottomNavigation";
                try
                {
                    PlaceNavigation((RectTransform)copy.transform);
                    source = PrefabUtility.SaveAsPrefabAsset(copy, NavigationPath);
                }
                finally { Object.DestroyImmediate(copy); }
            }
            if (!source) throw new InvalidOperationException("BottomNavigation prefab could not be saved.");
            var nav = (GameObject)PrefabUtility.InstantiatePrefab(source, homeRoot);
            nav.name = "BottomNavigation";
            PlaceNavigation((RectTransform)nav.transform);
            nav.transform.SetSiblingIndex(sibling);
            PrefabUtility.RecordPrefabInstancePropertyModifications(nav);
            PrefabUtility.RecordPrefabInstancePropertyModifications(nav.transform);
            Object.DestroyImmediate(oldNav.gameObject);
        }

        private static void PlaceNavigation(RectTransform rect)
        {
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(.5f, 0);
            rect.anchoredPosition = Vector2.zero;
            rect.sizeDelta = new Vector2(390, 79);
            rect.localScale = Vector3.one;
            rect.localRotation = Quaternion.identity;
        }

        private static void SetRect(RectTransform rect, float x, float y, float width, float height)
        {
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0, 1);
            rect.anchoredPosition = new Vector2(x, -y);
            rect.sizeDelta = new Vector2(width, height);
            rect.localScale = Vector3.one;
            rect.localRotation = Quaternion.identity;
            if (PrefabUtility.IsPartOfPrefabInstance(rect)) PrefabUtility.RecordPrefabInstancePropertyModifications(rect);
        }

        [MenuItem("Imoni/Home/Validate Shared UI Prefabs")]
        public static void Validate()
        {
            var home = PrefabUtility.LoadPrefabContents(HomePath);
            try
            {
                var play = home.transform.Find("PlayButton");
                var nav = home.transform.Find("BottomNavigation");
                if (!play || PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(play.gameObject) != PrimaryPath)
                    throw new InvalidOperationException("PlayButton is not linked to PrimaryButton.");
                if (home.transform.Find("PlayButtonShadow"))
                    throw new InvalidOperationException("A duplicate independent button shadow remains.");
                if (!nav || PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(nav.gameObject) != NavigationPath)
                    throw new InvalidOperationException("BottomNavigation is not linked to its prefab.");
                if (nav.GetComponentsInChildren<Button>(true).Length != 3 || !play.GetComponent<Button>())
                    throw new InvalidOperationException("A UI button is missing.");
                var originalImage = AssetDatabase.LoadAssetAtPath<GameObject>(PrimaryPath).GetComponent<Image>();
                if (play.GetComponent<Image>().sprite != originalImage.sprite)
                    throw new InvalidOperationException("PlayButton is not using the shared button sprite.");
                foreach (var text in home.GetComponentsInChildren<TextMeshProUGUI>(true))
                    if (!text.font) throw new InvalidOperationException("A text font is missing: " + text.name);
                Debug.Log("IMONI_SHARED_UI_VALIDATED: PrimaryButton link, shared shadow sprite, three navigation buttons, and fonts are valid.");
            }
            finally { PrefabUtility.UnloadPrefabContents(home); }
        }
    }
}
