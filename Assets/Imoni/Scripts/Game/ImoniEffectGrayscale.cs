using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace Imoni.Game.Effects
{
    /// <summary>Cached presentation-only GPU copies. Never edits the supplied sprite assets.</summary>
    public sealed class ImoniEffectGrayscale : IDisposable
    {
        private readonly Shader shader;
        private readonly Dictionary<Texture2D, RenderTexture> copies = new Dictionary<Texture2D, RenderTexture>();
        private Material material;
        private static readonly int GrayProperty = Shader.PropertyToID("_Gray");
        public float Amount { get; private set; }
        public int TextureCount => copies.Count;

        public ImoniEffectGrayscale(Shader shader) { this.shader = shader; }

        public bool SetAmount(float amount)
        {
            amount = Mathf.Clamp01(amount);
            if (Mathf.Approximately(Amount, amount)) return false;
            Amount = amount;
            if (Amount <= 0) return true;
            EnsureMaterial(); material.SetFloat(GrayProperty, Amount);
            foreach (var entry in copies) Blit(entry.Key, entry.Value);
            return true;
        }

        public StyleBackground Background(Texture2D source)
        {
            if (!source || Amount <= 0) return new StyleBackground(source);
            if (!copies.TryGetValue(source, out RenderTexture copy))
            {
                EnsureMaterial();
                copy = new RenderTexture(source.width, source.height, 0, RenderTextureFormat.ARGB32)
                {
                    name = source.name + " (effect grayscale)", hideFlags = HideFlags.HideAndDontSave,
                    filterMode = source.filterMode, wrapMode = TextureWrapMode.Clamp,
                    useMipMap = false, autoGenerateMips = false
                };
                copy.Create(); copies.Add(source, copy);
                material.SetFloat(GrayProperty, Amount); Blit(source, copy);
            }
            return new StyleBackground(UnityEngine.UIElements.Background.FromRenderTexture(copy));
        }

        public Color Color(Color source)
        {
            float luminance = source.r * .2126f + source.g * .7152f + source.b * .0722f;
            return UnityEngine.Color.Lerp(source, new Color(luminance, luminance, luminance, source.a), Amount);
        }

        private void EnsureMaterial()
        {
            if (material) return;
            if (!shader) throw new InvalidOperationException("Run Imoni > Setup Project to bind the grayscale shader.");
            material = new Material(shader) { name = "Imoni Effect Grayscale", hideFlags = HideFlags.HideAndDontSave };
        }

        private void Blit(Texture2D source, RenderTexture destination)
        {
            RenderTexture previous = RenderTexture.active;
            try { Graphics.Blit(source, destination, material); }
            finally { RenderTexture.active = previous; }
        }

        public void Dispose()
        {
            foreach (RenderTexture texture in copies.Values)
            {
                if (!texture) continue;
                texture.Release(); UnityEngine.Object.Destroy(texture);
            }
            copies.Clear();
            if (material) UnityEngine.Object.Destroy(material);
            material = null;
        }
    }
}
