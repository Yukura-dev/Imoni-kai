using System;
using Imoni.Game.UI;
using Newtonsoft.Json.Linq;
using UnityEngine;
using UnityEngine.UIElements;

namespace Imoni.Game.World
{
    public sealed class WorldViewStyle
    {
        private readonly JToken data;
        internal readonly float TileSize;
        internal WorldViewStyle()
        {
            data = UiTheme.Data["ui"]["worldView"] ?? throw new InvalidOperationException("Missing ui.worldView configuration.");
            TileSize = (float)UiTheme.Config.World.TileSizePx;
        }
        internal float Number(string key) => data[key]?.Value<float>() ?? throw new InvalidOperationException("Missing ui.worldView." + key);
        internal float Growth(int stage) => data["growthScales"][stage].Value<float>();
        internal float Units(string key) => Number(key) / TileSize;
        public static void Size(SpriteRenderer renderer, float width, float height)
        {
            if (!renderer.sprite) return;
            // The existing Multiple sprites are automatically cropped, independently for each avatar layer.
            // Scale the original PNG canvas, then restore its pivot location; fitting bounds would distort
            // individual layers and make resource artwork larger than the mock's full 38x40 canvas.
            Sprite sprite = renderer.sprite;
            float textureWidth = sprite.texture.width, textureHeight = sprite.texture.height;
            renderer.transform.localScale = new Vector3(width * sprite.pixelsPerUnit / textureWidth, height * sprite.pixelsPerUnit / textureHeight, 1);
            renderer.transform.localPosition = new Vector3(
                (sprite.rect.x + sprite.pivot.x - textureWidth / 2) / textureWidth * width,
                (sprite.rect.y + sprite.pivot.y - textureHeight / 2) / textureHeight * height, 0);
        }
        internal static void Position(VisualElement element, Vector2 point)
        {
            element.style.left = point.x; element.style.top = point.y;
        }
        internal static void Visible(VisualElement element, bool visible) => element.style.display = visible ? DisplayStyle.Flex : DisplayStyle.None;
    }
}
