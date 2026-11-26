using Imoni.Core;
using Imoni.Game.UI;
using UnityEngine;
using UnityEngine.UIElements;

namespace Imoni.Game.World
{
    /// <summary>Only interpolates published movement; inventory, arrival and gathering remain in Core.</summary>
    public sealed class CharacterView : MonoBehaviour
    {
        [SerializeField] private Transform body;
        [SerializeField] private SpriteRenderer[] layers;
        [SerializeField] private Transform leftHand, rightHand;
        [SerializeField] private SpriteRenderer[] handSprites;
        private WorldViewStyle style;
        private Label nameTag;
        private string displayName;
        private bool self;
        private float width, height;
        private float walkClock;
        private Color outfit, band;
        public string ActorId { get; private set; }
        public Vector3 DisplayPosition => transform.position;

        internal void Initialize(string id, string actorName, bool isSelf, string outfitColor, string bandColor,
            WorldRuntimeAssets assets, WorldViewStyle settings, VisualElement overlay)
        {
            ActorId = id; displayName = actorName; self = isSelf; style = settings;
            width = style.Units(isSelf ? "playerWidthPx" : "teammateWidthPx");
            height = style.Units(isSelf ? "playerHeightPx" : "teammateHeightPx");
            outfit = UiTheme.Color(outfitColor); band = UiTheme.Color(bandColor);
            for (int i = 0; i < layers.Length; i++)
            { layers[i].sprite = assets.avatarLayers[i]; WorldViewStyle.Size(layers[i], width, height); }
            for (int i = 0; i < handSprites.Length; i++)
            {
                handSprites[i].sprite = assets.circle;
                // The inner circle is inset by the same outline ratio as the supplied avatar artwork.
                float diameter = style.Units("handDiameterPx") * (i % 2 == 0 ? 1 : style.Number("handInnerScale"));
                WorldViewStyle.Size(handSprites[i], diameter, diameter);
            }
            nameTag = UiTheme.Label(actorName, "world-character__name");
            nameTag.EnableInClassList("world-character__name--self", self);
            nameTag.name = "Actor_" + id; overlay.Add(nameTag);
        }

        internal void Present(PlayerSnapshot player, Vector3 position, Vector3? target, float deltaSeconds)
        {
            if (player.Action == PlayerAction.Moving) walkClock += deltaSeconds;
            else walkClock = 0;
            transform.position = position;
            float bob = player.Action == PlayerAction.Moving
                ? Mathf.Abs(Mathf.Sin(walkClock / style.Number("walkCycleSeconds") * Mathf.PI)) * style.Units("walkBobHeightPx") : 0;
            body.localPosition = Vector3.up * bob;
            float opacity = player.Connected ? 1 : style.Number("disconnectedOpacity");
            int order = GameWorldView.SortOrder(-position.y - 0.5f) + 1;
            for (int i = 0; i < layers.Length; i++)
            {
                Color tint = i == 0 ? outfit : i == 2 ? band : Color.white; tint.a *= opacity;
                layers[i].color = tint; layers[i].sortingOrder = order + i;
            }
            Vector3 left = new Vector3(-style.Units("handRestOffsetPx"), -height / 2 + style.Units("handHeightPx"), 0);
            Vector3 right = new Vector3(-left.x, left.y, 0);
            if (player.Action == PlayerAction.Gathering && target.HasValue && player.ActionDurationSeconds > 0)
            {
                float progress = 1 - (float)(player.ActionRemainingSeconds / player.ActionDurationSeconds);
                float extension = Mathf.Abs(Mathf.Sin(progress * style.Number("gatherSwingCycles") * Mathf.PI));
                Vector3 direction = target.Value - position;
                Vector3 handTarget = direction * style.Number("handReachFraction");
                if (direction.x < 0) left = Vector3.Lerp(left, handTarget, extension);
                else right = Vector3.Lerp(right, handTarget, extension);
            }
            leftHand.localPosition = left + Vector3.up * bob; rightHand.localPosition = right + Vector3.up * bob;
            for (int i = 0; i < handSprites.Length; i++)
            {
                Color tint = UiTheme.Color(i % 2 == 0 ? "#4A2E22" : "#FBE6C8"); tint.a = opacity;
                int handOrder = player.Action == PlayerAction.Gathering && target.HasValue
                    ? Mathf.Max(order, GameWorldView.SortOrder(-target.Value.y - 0.5f) + 1) : order;
                handSprites[i].color = tint; handSprites[i].sortingOrder = handOrder + layers.Length + i % 2;
            }
            nameTag.text = self ? "あなた" : displayName + " " + (!player.Connected ? "切断中" : player.Action == PlayerAction.Gathering ? "採集中" : player.Action == PlayerAction.Moving ? "移動中" : "待機中");
            nameTag.style.opacity = opacity;
        }
        internal void PositionOverlay(Vector2 point, bool visible)
        {
            WorldViewStyle.Visible(nameTag, visible);
            WorldViewStyle.Position(nameTag, point + Vector2.up * (height * style.TileSize / 2 + style.Number("labelOffsetPx")));
        }
        internal void Release() { nameTag?.RemoveFromHierarchy(); }
#if UNITY_EDITOR
        public void Configure(Transform bodyRoot, SpriteRenderer[] bodyLayers, Transform left, Transform right, SpriteRenderer[] hands)
        { body = bodyRoot; layers = bodyLayers; leftHand = left; rightHand = right; handSprites = hands; }
#endif
    }
}
