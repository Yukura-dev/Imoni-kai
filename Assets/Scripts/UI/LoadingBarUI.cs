using UnityEngine;

public class LoadingBarUI : MonoBehaviour
{
    [SerializeField] private RectTransform fill;
    [SerializeField] private float maxWidth = 268f;

    public void SetProgress(float progress)
    {
        progress = Mathf.Clamp01(progress);

        fill.gameObject.SetActive(progress > 0f);

        float width = maxWidth * progress;

        fill.SetSizeWithCurrentAnchors(
            RectTransform.Axis.Horizontal,
            width
        );
    }

    void Start()
    {
    }
}
