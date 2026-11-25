using UnityEngine;

namespace Imoni.UI
{
    /// <summary>Scrolls the map within its viewport while leaving the HUD in place.</summary>
    public sealed class GameMapCamera : MonoBehaviour
    {
        public RectTransform viewport;
        public RectTransform world;
        public RectTransform player;
        public Vector2 playerFocusOffset = new Vector2(20f, -24f);

        private const float FollowSharpness = 12f;

        private void Start()
        {
            SnapToPlayer();
        }

        private void LateUpdate()
        {
            if (!TryGetFollowPosition(out Vector2 target))
                return;

            float blend = 1f - Mathf.Exp(-FollowSharpness * Time.deltaTime);
            world.anchoredPosition = ClampToViewport(Vector2.Lerp(world.anchoredPosition, target, blend));
        }

        public void SnapToPlayer()
        {
            if (TryGetFollowPosition(out Vector2 target))
                world.anchoredPosition = target;
        }

        private bool TryGetFollowPosition(out Vector2 target)
        {
            target = Vector2.zero;
            if (viewport == null || world == null || player == null)
                return false;

            Vector2 viewportSize = viewport.rect.size;
            Vector2 worldSize = world.rect.size;
            if (viewportSize.x <= 0f || viewportSize.y <= 0f || worldSize.x <= 0f || worldSize.y <= 0f)
                return false;

            Vector2 viewportCenter = new Vector2(viewportSize.x * 0.5f, -viewportSize.y * 0.5f);
            target = ClampToViewport(viewportCenter - (player.anchoredPosition + playerFocusOffset));
            return true;
        }

        private Vector2 ClampToViewport(Vector2 position)
        {
            float horizontalOverflow = Mathf.Max(0f, world.rect.width - viewport.rect.width);
            float verticalOverflow = Mathf.Max(0f, world.rect.height - viewport.rect.height);
            return new Vector2(
                Mathf.Clamp(position.x, -horizontalOverflow, 0f),
                Mathf.Clamp(position.y, 0f, verticalOverflow));
        }
    }
}
