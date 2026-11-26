using Imoni.Core;
using Imoni.Core.Effects;
using Imoni.Game.UI.Components;
using UnityEngine.UIElements;

namespace Imoni.Game.UI.Screens
{
    /// <summary>One controller for the shared title/home settings; controls only emit user intent.</summary>
    internal static class SettingsPresenter
    {
        public static void Attach(VisualElement screen, ScreenContext app)
        {
            var dialog = screen.Q<SettingsDialog>("settings-dialog");
            var picker = new AccountPicker();
            dialog.AccountContent.Add(picker);
            void Refresh() => dialog.Bind(app.Profile.SoundEnabled, app.Profile.BgmVolume, app.Profile.EffectsVolume);
            screen.Q<ImoniButton>("settings").clicked += () => { Refresh(); dialog.Open(); };
            dialog.CloseRequested += dialog.Close;
            dialog.SoundToggleRequested += () => { app.Profile.SoundEnabled = !app.Profile.SoundEnabled; app.SaveProfile(); Refresh(); };
            dialog.BgmStepRequested += direction =>
            {
                app.Profile.BgmVolume = ImoniAudioPlan.AdjustVolume(app.Data, app.Profile.BgmVolume, direction);
                app.SaveProfile(); Refresh();
            };
            dialog.EffectsStepRequested += direction =>
            {
                app.Profile.EffectsVolume = ImoniAudioPlan.AdjustVolume(app.Data, app.Profile.EffectsVolume, direction);
                app.SaveProfile(); Refresh();
            };
            dialog.AccountSwitchRequested += () =>
            {
                app.SaveProfile();
                picker.Bind(app.Accounts.Ids, app.Accounts.ActiveId, id => app.Accounts.Get(app.Data, id), (int)app.Number("profileNameMaxLength"));
                dialog.ShowAccount(true);
            };
            picker.SwitchRequested += app.SwitchAccount;
            picker.CreateRequested += app.CreateAccount;
            picker.BackRequested += () => dialog.ShowAccount(false);
            Refresh();
        }
    }
}
