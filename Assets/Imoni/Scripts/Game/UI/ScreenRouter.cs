using System;
using System.Collections.Generic;
using UnityEngine.UIElements;

namespace Imoni.Game.UI
{
    /// <summary>Only registered screens can replace the current view. M2a registers the development gallery.</summary>
    public sealed class ScreenRouter : IDisposable
    {
        private readonly VisualElement host;
        private readonly Dictionary<string, Func<VisualElement>> routes = new Dictionary<string, Func<VisualElement>>(StringComparer.Ordinal);
        public string CurrentScreen { get; private set; }
        public VisualElement CurrentView { get; private set; }
        public event Action<string> ScreenChanged;
        public ScreenRouter(VisualElement host) { this.host = host ?? throw new ArgumentNullException(nameof(host)); }
        public void Register(string name, Func<VisualElement> create)
        {
            if (string.IsNullOrWhiteSpace(name)) throw new ArgumentException("A route name is required.", nameof(name));
            routes[name] = create ?? throw new ArgumentNullException(nameof(create));
        }
        public void RegisterResource(string name, string resourcePath)
        {
            Register(name, () =>
            {
                var tree = UnityEngine.Resources.Load<VisualTreeAsset>(resourcePath);
                if (!tree) throw new InvalidOperationException("Missing screen resource: " + resourcePath);
                var container = tree.CloneTree();
                container.style.flexGrow = 1;
                return container;
            });
        }
        public bool Show(string name)
        {
            if (name == null || !routes.TryGetValue(name, out Func<VisualElement> create)) return false;
            VisualElement next = create() ?? throw new InvalidOperationException("The screen factory returned no view: " + name);
            if (CurrentView is IDisposable disposable) disposable.Dispose();
            host.Clear();
            host.Add(next);
            CurrentView = next;
            CurrentScreen = name;
            ScreenChanged?.Invoke(name);
            return true;
        }
        public void Dispose()
        {
            if (CurrentView is IDisposable disposable) disposable.Dispose();
            host.Clear(); routes.Clear(); CurrentView = null; CurrentScreen = null;
        }
    }
}
