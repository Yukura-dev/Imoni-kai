using UnityEngine;

public class LoadingBarUI : MonoBehaviour
{
    [SerializeField] private RectTransform fill;
    [SerializeField] private float maxWidth = 268f;

    public void SetProgress(float progress)
    {
        progress = Mathf.Clamp01(progress);

        fill.gameObject.SetActive(progress > 0f);

        float width = Mathf.Lerp(20f, maxWidth, progress); //width=20+248\times0.65=181.2

        fill.SetSizeWithCurrentAnchors(
            RectTransform.Axis.Horizontal,
            width
        );
    }

    void Start()
    {
        SetProgress(0.65f); //テスト用：65%
    }
}
