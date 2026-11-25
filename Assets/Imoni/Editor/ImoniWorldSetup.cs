using System;
using System.IO;
using System.Linq;
using Imoni.Game.World;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.Tilemaps;

namespace Imoni.Editor
{
    /// <summary>Idempotent Editor-API generation. Existing imported sprite subassets and GUIDs are preserved.</summary>
    public static class ImoniWorldSetup
    {
        private const string Root = "Assets/Imoni";
        private const string Prefabs = Root + "/Prefabs/World";
        private const string TileSetPath = Prefabs + "/TileSet.asset";
        private const string AssetsPath = Root + "/Resources/World/WorldRuntimeAssets.asset";
        private static JObject config;

        public static void EnsureAssets()
        {
            config = JObject.Parse(File.ReadAllText(Root + "/Config/GameConfig.json"));
            EnsureFolder(Prefabs); EnsureFolder(Root + "/Resources/World");
            WorldRuntimeAssets references = AssetDatabase.LoadAssetAtPath<WorldRuntimeAssets>(AssetsPath);
            if (!references) { references = ScriptableObject.CreateInstance<WorldRuntimeAssets>(); AssetDatabase.CreateAsset(references, AssetsPath); }
            WorldTileSet set = AssetDatabase.LoadAssetAtPath<WorldTileSet>(TileSetPath);
            if (!set) { set = ScriptableObject.CreateInstance<WorldTileSet>(); AssetDatabase.CreateAsset(set, TileSetPath); }
            set.plain = EnsureTile(set, "Plain", "tile_plain", Color.white);
            set.forest = EnsureTile(set, "Forest", "tile_forest", Color.white);
            set.water = EnsureTile(set, "Water", "tile_water", Color.white);
            set.bridge = EnsureTile(set, "Bridge", "tile_bridge", Color.white);
            set.field = EnsureTile(set, "Field", "tile_field", Color.white);
            set.mountain = EnsureTile(set, "Mountain", "tile_plain", ColorHtml("#CABCA1"));
            set.swamp = EnsureTile(set, "Swamp", "tile_forest", ColorHtml("#9AB09A"));
            set.locked = EnsureTile(set, "Locked", "tile_locked_overlay", Color.white);
            EditorUtility.SetDirty(set);
            references.tileSet = set;
            references.resources = new[]
            {
                Art("wood", "resource_tree"), Art("taro", "resource_taro"), Art("negi", "resource_negi"), Art("mushroom", "resource_mushroom"),
                new ResourceArt { materialId = "fish", sprite = Sprite("Tiles", "tile_water"), tint = Color.white, label = "魚" },
                new ResourceArt { materialId = "sansai", sprite = Sprite("Game", "resource_taro"), tint = ColorHtml("#679958") }
            };
            references.stump = Sprite("Game", "resource_stump"); references.circle = Sprite("UI", "circle_white"); references.camp = Sprite("Game", "pot_small");
            references.avatarLayers = new[] { "avatar_1_outfit_white", "avatar_2_base", "avatar_3_band_white", "avatar_4_band_line" }.Select(name => Sprite("Game", name)).ToArray();
            references.resourceNode = BuildResource(references.resources[0].sprite);
            references.character = BuildCharacter(references);
            references.gatherPopup = BuildPopup();
            references.lockedAreaOverlay = BuildLocked();
            EditorUtility.SetDirty(references); AssetDatabase.SaveAssets();
        }

        private static Tile EnsureTile(WorldTileSet set, string name, string imageName, Color tint)
        {
            Tile tile = AssetDatabase.LoadAllAssetsAtPath(TileSetPath).OfType<Tile>().FirstOrDefault(value => value.name == name);
            if (!tile) { tile = ScriptableObject.CreateInstance<Tile>(); tile.name = name; AssetDatabase.AddObjectToAsset(tile, set); }
            tile.sprite = Sprite("Tiles", imageName); tile.color = tint; tile.colliderType = Tile.ColliderType.None;
            // Generated full-canvas sprites preserve the existing imported subassets used by the old prototype.
            Sprite sprite = tile.sprite;
            float width = sprite.texture.width, height = sprite.texture.height;
            Vector3 scale = new Vector3(sprite.pixelsPerUnit / width, sprite.pixelsPerUnit / height, 1);
            Vector3 offset = new Vector3((sprite.rect.x + sprite.pivot.x - width / 2) / width,
                (sprite.rect.y + sprite.pivot.y - height / 2) / height, 0);
            tile.transform = Matrix4x4.TRS(offset, Quaternion.identity, scale);
            tile.flags = TileFlags.LockColor | TileFlags.LockTransform; EditorUtility.SetDirty(tile); return tile;
        }
        private static ResourceNodeView BuildResource(Sprite sprite)
        {
            var root = new GameObject("ResourceNode");
            try
            {
                SpriteRenderer renderer = Renderer(root.transform, "Artwork", sprite);
                WorldViewStyle.Size(renderer, Units("resourceVisualWidthPx"), Units("resourceVisualHeightPx")); renderer.sortingOrder = 100;
                var hit = root.AddComponent<BoxCollider2D>(); hit.isTrigger = true;
                hit.size = Vector2.one * Units("resourceTapSizePx");
                root.AddComponent<ResourceNodeView>().Configure(renderer, hit);
                GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, Prefabs + "/ResourceNode.prefab");
                return prefab.GetComponent<ResourceNodeView>();
            }
            finally { UnityEngine.Object.DestroyImmediate(root); }
        }
        private static CharacterView BuildCharacter(WorldRuntimeAssets assets)
        {
            var root = new GameObject("Character");
            try
            {
                Transform body = Child(root.transform, "Body");
                SpriteRenderer[] layers = assets.avatarLayers.Select((sprite, index) => Renderer(body, "Avatar Layer " + (index + 1), sprite)).ToArray();
                for (int index = 0; index < layers.Length; index++)
                {
                    WorldViewStyle.Size(layers[index], Units("playerWidthPx"), Units("playerHeightPx")); layers[index].sortingOrder = 100 + index;
                    layers[index].color = index == 0 ? ColorHtml(config["outGame"]["profile"]["defaultOutfitColor"].Value<string>())
                        : index == 2 ? ColorHtml(config["outGame"]["profile"]["defaultBandColor"].Value<string>()) : Color.white;
                }
                Transform left = Child(root.transform, "Left Hand"), right = Child(root.transform, "Right Hand");
                left.localPosition = new Vector3(-Units("handRestOffsetPx"), -Units("playerHeightPx") / 2 + Units("handHeightPx"), 0);
                right.localPosition = new Vector3(-left.localPosition.x, left.localPosition.y, 0);
                SpriteRenderer[] hands = new[] { Renderer(left, "Left Outline", assets.circle), Renderer(left, "Left Skin", assets.circle), Renderer(right, "Right Outline", assets.circle), Renderer(right, "Right Skin", assets.circle) };
                for (int index = 0; index < hands.Length; index++)
                {
                    float diameter = Units("handDiameterPx") * (index % 2 == 0 ? 1 : config["ui"]["worldView"]["handInnerScale"].Value<float>());
                    WorldViewStyle.Size(hands[index], diameter, diameter); hands[index].color = ColorHtml(index % 2 == 0 ? "#4A2E22" : "#FBE6C8");
                    hands[index].sortingOrder = 100 + layers.Length + index % 2;
                }
                root.AddComponent<CharacterView>().Configure(body, layers, left, right, hands);
                GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, Prefabs + "/Character.prefab");
                return prefab.GetComponent<CharacterView>();
            }
            finally { UnityEngine.Object.DestroyImmediate(root); }
        }
        private static GatherPopupView BuildPopup()
        {
            var root = new GameObject("GatherPopup");
            try
            {
                root.AddComponent<GatherPopupView>();
                GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, Prefabs + "/GatherPopup.prefab");
                return prefab.GetComponent<GatherPopupView>();
            }
            finally { UnityEngine.Object.DestroyImmediate(root); }
        }
        private static LockedAreaOverlayView BuildLocked()
        {
            var root = new GameObject("LockedAreaOverlay");
            try
            {
                Transform tiles = Child(root.transform, "Locked Tiles");
                var tilemap = tiles.gameObject.AddComponent<Tilemap>();
                var renderer = tiles.gameObject.AddComponent<TilemapRenderer>(); renderer.sortingOrder = short.MaxValue;
                root.AddComponent<LockedAreaOverlayView>().Configure(tilemap);
                GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, Prefabs + "/LockedAreaOverlay.prefab");
                return prefab.GetComponent<LockedAreaOverlayView>();
            }
            finally { UnityEngine.Object.DestroyImmediate(root); }
        }
        private static ResourceArt Art(string materialId, string imageName) => new ResourceArt { materialId = materialId, sprite = Sprite("Game", imageName), tint = Color.white };
        private static float Units(string key) => config["ui"]["worldView"][key].Value<float>() / config["world"]["tileSizePx"].Value<float>();
        private static Color ColorHtml(string html) { ColorUtility.TryParseHtmlString(html, out Color color); return color; }
        private static Transform Child(Transform parent, string name)
        { var child = new GameObject(name); child.transform.SetParent(parent, false); return child.transform; }
        private static SpriteRenderer Renderer(Transform parent, string name, Sprite sprite)
        { var renderer = Child(parent, name).gameObject.AddComponent<SpriteRenderer>(); renderer.sprite = sprite; return renderer; }
        private static Sprite Sprite(string folder, string name)
        {
            string path = Root + "/Art/Sprites/" + folder + "/" + name + ".png";
            Texture2D texture = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
            if (!texture) throw new InvalidOperationException("Missing supplied world artwork: " + path);
            string generatedName = folder + "_" + name;
            Sprite existing = AssetDatabase.LoadAllAssetsAtPath(AssetsPath).OfType<Sprite>().FirstOrDefault(item => item.name == generatedName);
            if (existing && existing.texture == texture && existing.rect == new Rect(0, 0, texture.width, texture.height)) return existing;
            // Auto-slicing split e.g. the three negi stems into separate imported sprites. Preserve those
            // source slices and create a separate full-image sprite for this view, using its original PNG.
            Sprite generated = UnityEngine.Sprite.Create(texture, new Rect(0, 0, texture.width, texture.height),
                new Vector2(0.5f, 0.5f), 1, 0, SpriteMeshType.FullRect, Vector4.zero, false);
            generated.name = generatedName;
            if (existing)
            {
                EditorUtility.CopySerialized(generated, existing); UnityEngine.Object.DestroyImmediate(generated);
                EditorUtility.SetDirty(existing); return existing;
            }
            AssetDatabase.AddObjectToAsset(generated, AssetsPath); return generated;
        }
        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            string parent = Path.GetDirectoryName(path).Replace('\\', '/'); EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, Path.GetFileName(path));
        }
    }
}
