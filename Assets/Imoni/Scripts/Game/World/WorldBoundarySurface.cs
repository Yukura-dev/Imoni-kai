using System;
using Imoni.Core;
using UnityEngine;

namespace Imoni.Game.World
{
    /// <summary>One reusable presentation mesh. Tile centers retain the authoritative Core mask.</summary>
    internal sealed class WorldBoundarySurface : IDisposable
    {
        private readonly GameObject root;
        private readonly Mesh mesh;
        private readonly Material material;
        private readonly Texture2D mask;
        private readonly Color32[] pixels;
        private readonly int width, height;

        internal WorldBoundarySurface(Transform parent, string name, WorldMap world, Shader shader,
            Texture2D artwork, Color tint, float featherTiles, int sortingOrder)
        {
            if (!shader) throw new InvalidOperationException("Run Imoni > Setup Project to bind the world boundary shader.");
            width = world.Width; height = world.Height;
            root = new GameObject(name); root.transform.SetParent(parent, false);
            mesh = new Mesh { name = name + " Mesh", hideFlags = HideFlags.HideAndDontSave };
            mesh.vertices = new[] { Vector3.zero, new Vector3(width, 0), new Vector3(width, -height), new Vector3(0, -height) };
            mesh.uv = new[] { Vector2.zero, new Vector2(width, 0), new Vector2(width, height), new Vector2(0, height) };
            mesh.triangles = new[] { 0, 1, 2, 0, 2, 3 }; mesh.RecalculateBounds();
            root.AddComponent<MeshFilter>().sharedMesh = mesh;
            mask = new Texture2D(width, height, TextureFormat.RGBA32, false, true)
            {
                name = name + " Mask", filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp,
                hideFlags = HideFlags.HideAndDontSave
            };
            pixels = new Color32[width * height];
            material = new Material(shader) { name = name + " Material", hideFlags = HideFlags.HideAndDontSave };
            material.SetTexture("_TileTex", artwork); material.SetTexture("_BoundaryMask", mask);
            material.SetColor("_Tint", tint); material.SetVector("_WorldSize", new Vector4(width, height, 0, 0));
            material.SetFloat("_Feather", featherTiles);
            MeshRenderer renderer = root.AddComponent<MeshRenderer>(); renderer.sharedMaterial = material; renderer.sortingOrder = sortingOrder;
        }

        internal void Rebuild(Func<GridPosition, bool> covered)
        {
            for (int y = 0; y < height; y++) for (int x = 0; x < width; x++)
            {
                byte value = covered(new GridPosition(x, y)) ? byte.MaxValue : byte.MinValue;
                pixels[y * width + x] = new Color32(value, value, value, byte.MaxValue);
            }
            mask.SetPixels32(pixels); mask.Apply(false, false);
        }

        public void Dispose()
        {
            if (root) UnityEngine.Object.Destroy(root);
            if (mesh) UnityEngine.Object.Destroy(mesh);
            if (material) UnityEngine.Object.Destroy(material);
            if (mask) UnityEngine.Object.Destroy(mask);
        }
    }
}
