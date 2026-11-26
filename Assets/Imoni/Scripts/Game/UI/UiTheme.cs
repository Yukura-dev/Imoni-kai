using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
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
        private static readonly ConditionalWeakTable<VisualElement, StyleRequests> requests = new ConditionalWeakTable<VisualElement, StyleRequests>();
        public static readonly string[] ComponentStyles = { "UI/Components/Navigation", "UI/Components/Profile", "UI/Components/Data", "UI/Components/Effects", "UI/Components/ScreenPrimitives", "UI/Components/OnboardingParts", "UI/Components/LobbyParts", "UI/Components/ProfileScreenParts", "UI/Components/GamePanelParts", "UI/Components/RoundFlow" };
        private sealed class StyleRequests { public readonly HashSet<string> Paths = new HashSet<string>(); }
        public static UIRuntimeAssets Assets => assets ? assets : (assets = Resources.Load<UIRuntimeAssets>("UI/UIRuntimeAssets"));
        public static GameConfig Config => config ?? (Assets && Assets.config ? (config = GameConfig.FromJson(Assets.config.text)) : null);
        public static JObject Data => data ?? (Assets && Assets.config ? (data = JObject.Parse(Assets.config.text)) : null);
        public static double Number(string key) => Data?["ui"]?[key]?.Value<double>() ?? 0;
        public static Texture2D Texture(string key) => string.IsNullOrEmpty(key) || !Assets
            ? null : Assets.textures.FirstOrDefault(item => item.key == key)?.texture;
        public static Color Color(string html) => ColorUtility.TryParseHtmlString(html, out Color color) ? color : UnityEngine.Color.clear;

        public static void InstallRoot(VisualElement root)
        {
            Add(root, Assets ? Assets.theme : null);
            foreach (string path in ComponentStyles) Add(root, Sheet(path));
            Add(root, Sheet("UI/ScreenLayout"));
            root.AddToClassList("imoni-app");
        }

        public static void Apply(VisualElement element, string stylePath = null)
        {
            if (!requests.TryGetValue(element, out StyleRequests state))
            {
                state = new StyleRequests(); requests.Add(element, state);
                element.RegisterCallback<AttachToPanelEvent>(_ => ResolveFallback(element));
            }
            if (!string.IsNullOrEmpty(stylePath)) state.Paths.Add(stylePath);
            // Constructors run before parenting: decide only after the actual ancestor chain exists.
            if (element.panel != null) ResolveFallback(element);
        }

        private static StyleSheet Sheet(string stylePath)
        {
            if (!sheets.TryGetValue(stylePath, out StyleSheet sheet) || !sheet)
            { sheet = Resources.Load<StyleSheet>(stylePath); if (sheet) sheets[stylePath] = sheet; }
            return sheet;
        }
        private static void Add(VisualElement element, StyleSheet sheet)
        { if (sheet && !element.styleSheets.Contains(sheet)) element.styleSheets.Add(sheet); }
        private static void ResolveFallback(VisualElement element)
        {
            if (!requests.TryGetValue(element, out StyleRequests state)) return;
            ResolveSheet(element, Assets ? Assets.theme : null);
            foreach (string path in state.Paths) ResolveSheet(element, Sheet(path));
        }
        private static void ResolveSheet(VisualElement element, StyleSheet sheet)
        {
            if (!sheet) return;
            for (VisualElement parent = element.hierarchy.parent; parent != null; parent = parent.hierarchy.parent)
                if (parent.styleSheets.Contains(sheet))
                { if (element.styleSheets.Contains(sheet)) element.styleSheets.Remove(sheet); return; }
            Add(element, sheet);
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
