using UnityEngine;

namespace Imoni.UI
{
    /// <summary>Keeps the 390 x 844 home artboard inside the device safe area.</summary>
    [ExecuteAlways]
    [DisallowMultipleComponent]
    [RequireComponent(typeof(RectTransform))]
    public sealed class HomeScreenFit : MonoBehaviour
    {
        [SerializeField] private Vector2 referenceSize = new Vector2(390, 844);
        private RectTransform rect;
        private Canvas canvas;

        private void OnEnable() => Fit();
        private void LateUpdate() => Fit();

        private void Fit()
        {
            if (!rect) rect = (RectTransform)transform;
            if (!canvas) canvas = GetComponentInParent<Canvas>();
            if (!canvas || Screen.width <= 0 || Screen.height <= 0) return;
            var canvasRect = (RectTransform)canvas.rootCanvas.transform;
            if (canvasRect.rect.width <= 0 || canvasRect.rect.height <= 0) return;
            var safe = Screen.safeArea;
            var unitsPerPixel = new Vector2(canvasRect.rect.width / Screen.width,
                canvasRect.rect.height / Screen.height);
            var scale = Mathf.Min(safe.width * unitsPerPixel.x / referenceSize.x,
                safe.height * unitsPerPixel.y / referenceSize.y);
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(.5f, .5f);
            rect.sizeDelta = referenceSize;
            rect.anchoredPosition = Vector2.Scale(safe.center - new Vector2(Screen.width, Screen.height) * .5f,
                unitsPerPixel);
            var desiredScale = new Vector3(scale, scale, 1);
            if ((rect.localScale - desiredScale).sqrMagnitude > .000001f) rect.localScale = desiredScale;
        }
    }
}
