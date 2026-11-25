using UnityEngine;
using UnityEngine.UI;

namespace Imoni.UI
{
    /// <summary>Uses Image.fillAmount as progress while keeping sliced corners round.</summary>
    [ExecuteAlways]
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Image))]
    public sealed class RoundedProgressFill : MonoBehaviour
    {
        [SerializeField] private RectTransform track;
        private Image image;

        public void Configure(RectTransform progressTrack)
        {
            track = progressTrack;
            Refresh();
        }

        private void OnEnable() => Refresh();
        private void LateUpdate() => Refresh();

        public void Refresh()
        {
            if (!image) image = GetComponent<Image>();
            if (!track || track == image.rectTransform) return;

            var rect = image.rectTransform;
            float width = Mathf.Max(0, track.rect.width) * image.fillAmount;
            float radius = Mathf.Min(width, rect.rect.height) * .5f;
            bool visible = radius > 0;
            if (image.enabled != visible) image.enabled = visible;
            if (image.type != Image.Type.Sliced) image.type = Image.Type.Sliced;
            if (!Mathf.Approximately(rect.rect.width, width))
                rect.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, width);

            if (visible && image.sprite)
            {
                float multiplier = image.sprite.border.x / (image.pixelsPerUnit * radius);
                if (!Mathf.Approximately(image.pixelsPerUnitMultiplier, multiplier))
                    image.pixelsPerUnitMultiplier = multiplier;
            }
        }
    }
}
