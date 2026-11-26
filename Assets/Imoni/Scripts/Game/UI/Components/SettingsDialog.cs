using System;
using Newtonsoft.Json.Linq;
using UnityEngine.UIElements;

namespace Imoni.Game.UI.Components
{
    /// <summary>Shared Main/Home presentation. Persistence and account switching belong to the screen context.</summary>
    [UxmlElement]
    public partial class SettingsDialog : VisualElement
    {
        private readonly Card card = new Card { Header = "設定" };
        private readonly ImoniButton sound = new ImoniButton { name = "sound-toggle", Variant = ImoniButtonVariant.Outline };
        private readonly AudioVolumeControl bgm = new AudioVolumeControl { name = "bgm-volume", Caption = "BGM" };
        private readonly AudioVolumeControl effects = new AudioVolumeControl { name = "effects-volume", Caption = "効果音" };
        private readonly ImoniButton account = new ImoniButton { name = "account-switch", Variant = ImoniButtonVariant.Outline, text = "アカウント切り替え" };
        private readonly ImoniButton help = new ImoniButton { name = "settings-help", Variant = ImoniButtonVariant.Outline, text = "遊び方", Icon = "help_question" };
        private readonly ImoniButton close = new ImoniButton { name = "settings-close", Variant = ImoniButtonVariant.Primary, text = "閉じる" };
        public VisualElement AudioContent { get; } = new VisualElement { name = "settings-audio-content" };
        public VisualElement AccountContent { get; } = new VisualElement { name = "settings-account-content" };
        public bool IsOpen { get; private set; }
        public event Action SoundToggleRequested;
        public event Action<int> BgmStepRequested;
        public event Action<int> EffectsStepRequested;
        public event Action AccountSwitchRequested;
        public event Action HelpRequested;
        public event Action CloseRequested;

        public SettingsDialog()
        {
            AddToClassList("settings-dialog"); UiTheme.Apply(this, "UI/Components/AudioSettings");
            card.AddToClassList("settings-dialog__card");
            account.AddToClassList("settings-dialog__action"); close.AddToClassList("settings-dialog__action");
            help.AddToClassList("settings-dialog__action");
            AudioContent.Add(sound); AudioContent.Add(bgm); AudioContent.Add(effects); AudioContent.Add(help); AudioContent.Add(account);
            card.Add(AudioContent); card.Add(AccountContent); card.Add(close); Add(card);
            sound.clicked += () => SoundToggleRequested?.Invoke();
            bgm.StepRequested += direction => BgmStepRequested?.Invoke(direction);
            effects.StepRequested += direction => EffectsStepRequested?.Invoke(direction);
            account.clicked += () => AccountSwitchRequested?.Invoke();
            help.clicked += () => HelpRequested?.Invoke();
            close.clicked += () => CloseRequested?.Invoke();
            Close();
        }
        public void Bind(bool enabled, double bgmVolume, double effectsVolume)
        {
            sound.text = enabled ? "音: ON" : "音: OFF";
            sound.Icon = enabled ? "icon_sound_on" : "icon_sound_off";
            JToken data = UiTheme.Data["audio"];
            double minimum = data["minVolume"].Value<double>(), maximum = data["maxVolume"].Value<double>();
            double scale = data["volumePercentScale"].Value<double>();
            bgm.Bind(bgmVolume, minimum, maximum, scale); effects.Bind(effectsVolume, minimum, maximum, scale);
        }
        public void Open() { IsOpen = true; ShowAccount(false); style.display = DisplayStyle.Flex; BringToFront(); }
        public void Close() { IsOpen = false; style.display = DisplayStyle.None; ShowAccount(false); }
        public void ShowAccount(bool visible)
        {
            card.Header = visible ? "アカウント切り替え" : "設定";
            AudioContent.style.display = visible ? DisplayStyle.None : DisplayStyle.Flex;
            AccountContent.style.display = visible ? DisplayStyle.Flex : DisplayStyle.None;
        }
    }
}
