using System;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Imoni.UI.Editor
{
    public static class ImoniMockV2Migration
    {
        public static void OpenHomeForReview() => EditorSceneManager.OpenScene("Assets/Scenes/Home.unity", OpenSceneMode.Single);

        public static void ApplyAndVerify()
        {
            Apply();
            ImoniMockV2Validation.Validate();
            ImoniMockV2Preview.Capture();
            Debug.Log("IMONI_MOCK_V2_COMPLETE: migration, validation and native UI previews completed.");
        }

        [MenuItem("Imoni/Update Existing Screens From Mock 2")]
        public static void Apply()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Stop Play Mode first.");
            if (SceneManager.GetActiveScene().isDirty) throw new InvalidOperationException("Save the current scene before updating UI.");
            UpdateHome();
            ImoniGameBuilder.UpdateInterface();
            ImoniLobbyBuilder.Build();
            ImoniWorldLoadingBuilder.Build();
            EditorSceneManager.OpenScene("Assets/Scenes/Game.unity", OpenSceneMode.Single);
            AssetDatabase.SaveAssets();
            Debug.Log("IMONI_MOCK_V2_MIGRATED: Home, Lobby/Waiting, WorldLoading and Game UI saved.");
        }

        private static void UpdateHome()
        {
            const string path = "Assets/Prefabs/UI/HomeScreen.prefab";
            var home = PrefabUtility.LoadPrefabContents(path);
            try
            {
                SetText(home.transform, "Stats/BestScore/Label", "通常クリア");
                SetText(home.transform, "Stats/BestScore/Value", "未達成");
                SetText(home.transform, "Stats/TeamRank/Label", "ランキング");
                SetText(home.transform, "Stats/TeamRank/Value", "未登録");
                PrefabUtility.SaveAsPrefabAsset(home, path);
            }
            finally { PrefabUtility.UnloadPrefabContents(home); }
        }

        private static void SetText(Transform root, string path, string value)
        {
            var label = root.Find(path).GetComponent<TMP_Text>();
            label.font.TryAddCharacters(value, out _);
            EditorUtility.SetDirty(label.font);
            label.text = value;
            EditorUtility.SetDirty(label);
        }
    }
}
