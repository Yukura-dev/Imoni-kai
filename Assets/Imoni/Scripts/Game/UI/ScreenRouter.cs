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
        private VisualElement returnFocus;
        private bool underlyingEnabled;
        public string CurrentScreen { get; private set; }
        public VisualElement CurrentView { get; private set; }
        public string OverlayScreen { get; private set; }
        public string OverlaySourceScreen { get; private set; }
        public VisualElement OverlayView { get; private set; }
        public bool HasOverlay => OverlayView != null;
        public string VisibleScreen => OverlayScreen ?? CurrentScreen;
        public event Action<string> ScreenChanged;
        public event Action<string> OverlayChanged;
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
            ClearOverlay(false);
            if (CurrentView is IDisposable disposable) disposable.Dispose();
            host.Clear();
            host.Add(next);
            CurrentView = next;
            CurrentScreen = name;
            ScreenChanged?.Invoke(name);
            return true;
        }
        /// <summary>Present a full safe-area page without destroying its caller's live UI or game session.</summary>
        public bool ShowOverlay(string name)
        {
            if (CurrentView == null || name == null || !routes.TryGetValue(name, out Func<VisualElement> create)) return false;
            if (OverlayScreen == name) return true;
            VisualElement next = create() ?? throw new InvalidOperationException("The overlay factory returned no view: " + name);
            ClearOverlay(false);
            returnFocus = host.panel?.focusController.focusedElement as VisualElement;
            underlyingEnabled = CurrentView.enabledSelf;
            CurrentView.SetEnabled(false);
            next.style.position = Position.Absolute;
            next.style.left = 0; next.style.right = 0; next.style.top = 0; next.style.bottom = 0;
            OverlayView = next; OverlayScreen = name; OverlaySourceScreen = CurrentScreen;
            host.Add(next); next.BringToFront();
            OverlayChanged?.Invoke(VisibleScreen);
            return true;
        }
        public void CloseOverlay() => ClearOverlay(true);
        private void ClearOverlay(bool notify)
        {
            if (OverlayView == null) return;
            if (OverlayView is IDisposable disposable) disposable.Dispose();
            OverlayView.RemoveFromHierarchy(); OverlayView = null; OverlayScreen = null; OverlaySourceScreen = null;
            CurrentView?.SetEnabled(underlyingEnabled);
            if (returnFocus?.panel != null && returnFocus.enabledInHierarchy) returnFocus.Focus();
            returnFocus = null;
            if (notify) OverlayChanged?.Invoke(VisibleScreen);
        }
        public void Dispose()
        {
            ClearOverlay(false);
            if (CurrentView is IDisposable disposable) disposable.Dispose();
            host.Clear(); routes.Clear(); CurrentView = null; CurrentScreen = null;
        }
    }
}
