using System;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Imoni.UI
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Button))]
    public sealed class SceneNavigationButton : MonoBehaviour
    {
        [SerializeField] private string targetSceneName = "Lobby";
        private bool isLoading;

        // Connected through Button.onClick in the owning screen prefab/scene.
        public void Navigate()
        {
            if (isLoading) return;
            if (string.IsNullOrWhiteSpace(targetSceneName) || !Application.CanStreamedLevelBeLoaded(targetSceneName))
            {
                Debug.LogError($"遷移先のシーン「{targetSceneName}」がビルドのScene Listに登録されていません。", this);
                return;
            }

            var button = GetComponent<Button>();
            isLoading = true;
            button.interactable = false;
            try
            {
                // Loading starts immediately; blocking repeated clicks prevents duplicate scene loads.
                if (SceneManager.LoadSceneAsync(targetSceneName, LoadSceneMode.Single) == null)
                    throw new InvalidOperationException($"Could not load scene: {targetSceneName}");
            }
            catch (Exception exception)
            {
                isLoading = false;
                button.interactable = true;
                Debug.LogException(exception, this);
            }
        }
    }
}
