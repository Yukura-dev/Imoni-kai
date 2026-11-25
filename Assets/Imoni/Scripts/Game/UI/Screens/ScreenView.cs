using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace Imoni.Game.UI.Screens
{
    public abstract class ScreenView : VisualElement, IDisposable
    {
        protected ScreenContext App { get; }
        public VisualElement View => this;
        private readonly List<IVisualElementScheduledItem> timers = new List<IVisualElementScheduledItem>();
        private bool disposed;
        protected ScreenView(ScreenContext app, string resource, string stylePath)
        {
            App = app ?? throw new ArgumentNullException(nameof(app));
            name = GetType().Name; AddToClassList("screen-view");
            UiTheme.Apply(this, stylePath);
            var tree = Resources.Load<VisualTreeAsset>(resource.StartsWith("UI/", StringComparison.Ordinal) ? resource : "UI/" + resource);
            if (!tree) throw new InvalidOperationException("Missing screen UXML: " + resource);
            tree.CloneTree(this);
        }
        protected T Find<T>(string elementName) where T : VisualElement => this.Q<T>(elementName) ?? throw new InvalidOperationException(name + " is missing " + elementName);
        protected IVisualElementScheduledItem Every(Action action, long milliseconds)
        { var timer = schedule.Execute(() => { if (!disposed) action(); }).Every(Math.Max(1, milliseconds)); timers.Add(timer); return timer; }
        protected IVisualElementScheduledItem Later(Action action, double seconds)
        { var timer = schedule.Execute(() => { if (!disposed) action(); }).StartingIn((long)(seconds * 1000)); timers.Add(timer); return timer; }
        protected virtual void OnDispose() { }
        public void Dispose()
        { if (disposed) return; disposed = true; foreach (var timer in timers) timer.Pause(); timers.Clear(); OnDispose(); }
    }
}
