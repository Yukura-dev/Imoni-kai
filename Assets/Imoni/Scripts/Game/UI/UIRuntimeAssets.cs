using System;
using UnityEngine;
using UnityEngine.UIElements;

namespace Imoni.Game.UI
{
    /// <summary>Editor-generated references keep the supplied art and theme available in player builds.</summary>
    public sealed class UIRuntimeAssets : ScriptableObject
    {
        public TextAsset config;
        public StyleSheet theme;
        public NamedTexture[] textures = Array.Empty<NamedTexture>();
    }

    [Serializable]
    public sealed class NamedTexture
    {
        public string key;
        public Texture2D texture;
    }
}
