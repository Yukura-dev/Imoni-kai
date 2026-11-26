
using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;

public class LoadingSceneController : MonoBehaviour
{
    [SerializeField] private LoadingBarUI loadingBar;
    [SerializeField] private Button tapToStartButton;

    private AsyncOperation loadOperation;
    private bool isReady = false;

    private string GetNextSceneName()
    {
        // TODO: チュートリアル判定
        return "Home";
    }

    private IEnumerator Start()
    {
        // Inspectorの参照を確認
        if (loadingBar == null || tapToStartButton == null)
        {
            Debug.LogError("UIの参照が設定されていません");
            yield break;
        }

        tapToStartButton.interactable = false;
        loadingBar.SetProgress(0f);

        // 非同期読み込みを1回だけ開始
        loadOperation =
            SceneManager.LoadSceneAsync(GetNextSceneName());

        loadOperation.allowSceneActivation = false;

        // 読み込み進捗を監視
        while (loadOperation.progress < 0.9f)
        {
            float progress =
                loadOperation.progress / 0.9f;

            loadingBar.SetProgress(progress);

            yield return null;
        }

        // 読み込み完了
        loadingBar.SetProgress(1f);
        isReady = true;

        tapToStartButton.interactable = true;
    }

    public void OnTapToStart()
    {
        if (!isReady || loadOperation == null)
        {
            Debug.LogWarning("まだ読み込みが完了していません");
            return;
        }

        tapToStartButton.interactable = false;
        loadOperation.allowSceneActivation = true;
    }
}
