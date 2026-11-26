using System;
using System.Globalization;
using UnityEngine.UIElements;

namespace Imoni.Game.UI.Components
{
    /// <summary>Reusable settings presentation composed from existing buttons and a progress bar.</summary>
    [UxmlElement]
    public partial class AudioVolumeControl : VisualElement
    {
        private readonly ImoniText caption = new ImoniText { text = "音量" };
        private readonly ImoniText amount = new ImoniText();
        private readonly ImoniButton decrease = new ImoniButton { name = "volume-decrease", Variant = ImoniButtonVariant.Small, text = "−", tooltip = "音量を下げる" };
        private readonly ImoniButton increase = new ImoniButton { name = "volume-increase", Variant = ImoniButtonVariant.Small, text = "+", tooltip = "音量を上げる" };
        private readonly ProgressBar bar = new ProgressBar { Variant = ProgressVariant.Thin };
        public event Action<int> StepRequested;
        [UxmlAttribute]
        public string Caption { get => caption.text; set => caption.text = value ?? string.Empty; }
        public AudioVolumeControl()
        {
            AddToClassList("audio-volume"); UiTheme.Apply(this, "UI/Components/AudioSettings");
            var heading = new VisualElement(); heading.AddToClassList("audio-volume__heading");
            heading.Add(caption); heading.Add(amount); Add(heading);
            var controls = new VisualElement(); controls.AddToClassList("audio-volume__controls");
            decrease.AddToClassList("audio-volume__step"); increase.AddToClassList("audio-volume__step");
            bar.AddToClassList("audio-volume__bar"); controls.Add(decrease); controls.Add(bar); controls.Add(increase); Add(controls);
            decrease.clicked += () => StepRequested?.Invoke(-1); increase.clicked += () => StepRequested?.Invoke(1);
        }
        public void Bind(double value, double minimum, double maximum, double percentScale)
        {
            amount.text = Math.Round(value * percentScale).ToString(CultureInfo.InvariantCulture) + "%";
            bar.Value = (float)((value - minimum) / (maximum - minimum));
            decrease.Disabled = value <= minimum; increase.Disabled = value >= maximum;
        }
    }
}
