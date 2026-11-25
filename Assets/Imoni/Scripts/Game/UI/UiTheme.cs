using System;
using System.Collections.Generic;
using System.Linq;
using Imoni.Core;
using Newtonsoft.Json.Linq;
using UnityEngine;
using UnityEngine.UIElements;

namespace Imoni.Game.UI
{
    public static class UiTheme
    {
        private static UIRuntimeAssets assets;
        private static GameConfig config;
        private static JObject data;
        private static readonly Dictionary<string, StyleSheet> sheets = new Dictionary<string, StyleSheet>();
        public static UIRuntimeAssets Assets => assets ? assets : (assets = Resources.Load<UIRuntimeAssets>("UI/UIRuntimeAssets"));
        public static GameConfig Config => config ?? (Assets && Assets.config ? (config = GameConfig.FromJson(Assets.config.text)) : null);
        public static JObject Data => data ?? (Assets && Assets.config ? (data = JObject.Parse(Assets.config.text)) : null);
        public static double Number(string key) => Data?["ui"]?[key]?.Value<double>() ?? 0;
        public static Texture2D Texture(string key) => string.IsNullOrEmpty(key) || !Assets
            ? null : Assets.textures.FirstOrDefault(item => item.key == key)?.texture;
        public static Color Color(string html) => ColorUtility.TryParseHtmlString(html, out Color color) ? color : UnityEngine.Color.clear;

        public static void Apply(VisualElement element, string stylePath = null)
        {
            if (Assets && Assets.theme && !element.styleSheets.Contains(Assets.theme)) element.styleSheets.Add(Assets.theme);
            if (string.IsNullOrEmpty(stylePath)) return;
            if (!sheets.TryGetValue(stylePath, out StyleSheet sheet) || !sheet)
            { sheet = Resources.Load<StyleSheet>(stylePath); if (sheet) sheets[stylePath] = sheet; }
            if (sheet && !element.styleSheets.Contains(sheet)) element.styleSheets.Add(sheet);
        }

        public static Label Label(string text, string className = null)
        {
            var label = new Label(text) { pickingMode = PickingMode.Ignore };
            if (!string.IsNullOrEmpty(className)) label.AddToClassList(className);
            return label;
        }

        public static VisualElement Icon(string key, string className = null)
        {
            var icon = new VisualElement { pickingMode = PickingMode.Ignore };
            icon.AddToClassList("imoni-icon");
            if (!string.IsNullOrEmpty(className)) icon.AddToClassList(className);
            icon.style.backgroundImage = Texture(key);
            return icon;
        }

        public static void SetVariant(VisualElement element, string prefix, object value)
        {
            foreach (string name in element.GetClasses().Where(name => name.StartsWith(prefix, StringComparison.Ordinal)).ToArray())
                element.RemoveFromClassList(name);
            element.AddToClassList(prefix + value.ToString().ToLowerInvariant());
        }
    }
}
