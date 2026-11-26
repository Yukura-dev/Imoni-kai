using System.Linq;
using UnityEngine;
using UnityEngine.UIElements;

namespace Imoni.Game.UI.Components
{
    /// <summary>Fits presentation text to its existing cell without changing the formatted value.</summary>
    internal sealed class NumericLabelFitter
    {
        private readonly Label label;
        private IVisualElementScheduledItem pending;
        private float normalSize, maximumSize;

        public NumericLabelFitter(Label label)
        {
            this.label = label;
            label.RegisterCallback<AttachToPanelEvent>(_ => Refresh(true));
            label.RegisterCallback<DetachFromPanelEvent>(_ => pending?.Pause());
            label.RegisterCallback<GeometryChangedEvent>(_ => Refresh());
        }

        public void Refresh(bool changedStyle = false)
        {
            if (changedStyle)
            {
                normalSize = 0;
                label.style.fontSize = new StyleLength(StyleKeyword.Null);
            }
            label.tooltip = label.text;
            if (label.panel == null) return;
            pending?.Pause();
            pending = label.schedule.Execute(Fit);
        }

        public void SetMaximumSize(float value) { maximumSize = value; Refresh(true); }

        private void Fit()
        {
            bool numeric = !string.IsNullOrEmpty(label.text) && label.text.Any(char.IsDigit);
            bool multiline = numeric && label.text.Contains("\n");
            label.style.whiteSpace = numeric ? new StyleEnum<WhiteSpace>(multiline ? WhiteSpace.Normal : WhiteSpace.NoWrap) : new StyleEnum<WhiteSpace>(StyleKeyword.Null);
            if (!numeric) { label.style.fontSize = new StyleLength(StyleKeyword.Null); return; }
            float currentSize = label.resolvedStyle.fontSize;
            float available = label.contentRect.width;
            if (currentSize <= 0 || available <= 0 || float.IsNaN(available)) return;
            if (normalSize <= 0) normalSize = currentSize;
            float measured = multiline ? label.text.Split('\n').Max(Measure) : Measure(label.text);
            if (measured <= 0 || float.IsNaN(measured)) return;
            float minimum = (float)UiTheme.Number("numericLabelMinimumFontSize");
            float ceiling = maximumSize > 0 ? Mathf.Min(normalSize, maximumSize) : normalSize;
            float size = Mathf.Min(ceiling, Mathf.Max(minimum, Mathf.Floor(currentSize * available / measured)));
            if (!Mathf.Approximately(currentSize, size)) label.style.fontSize = size;
        }

        private float Measure(string text) => label.MeasureTextSize(text, 0, VisualElement.MeasureMode.Undefined, 0, VisualElement.MeasureMode.Undefined).x;
    }
}
