using System;
using System.IO;
using System.Linq;
using System.Reflection;
using Imoni.Core;
using Imoni.Core.Effects;
using Imoni.Game;
using Imoni.Game.Audio;
using Imoni.Game.UI;
using Imoni.Game.UI.Components;
using Imoni.Game.UI.Screens;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;
using Object = UnityEngine.Object;

namespace Imoni.Editor
{
    /// <summary>Explicit Editor-only checks using an isolated save, attached UI tree and audio owner.</summary>
    public static class ImoniM8ExtraValidation
    {
        private const BindingFlags InstanceFlags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

        public static JObject Run()
        {
            string reportPath = LibraryPath("M8-extra-validation.json");
            string fixturePath = LibraryPath("M8-extra-profile-" + Guid.NewGuid().ToString("N") + ".json");
            var report = new JObject { ["passed"] = false, ["validatedAtUtc"] = DateTime.UtcNow.ToString("O"),
                ["unityVersion"] = Application.unityVersion, ["isolatedSavePath"] = fixturePath,
                ["scope"] = "Attached UI intent handlers and temporary audio sources. No real save writes, no live screen navigation, no pointer-hit or audible-listening claim." };
            Bootstrap boot = Object.FindFirstObjectByType<Bootstrap>();
            AudioService previousService = AudioService.Instance;
            bool previousEnabled = previousService && previousService.SoundEnabled;
            double previousBgm = previousService ? previousService.BgmVolume : 0;
            double previousEffects = previousService ? previousService.EffectsVolume : 0;
            float previousListenerVolume = AudioListener.volume;
            bool previousListenerPause = AudioListener.pause;
            string realSave = boot?.Context?.SavePath;
            byte[] realBefore = !string.IsNullOrEmpty(realSave) && File.Exists(realSave) ? File.ReadAllBytes(realSave) : null;
            GameObject audioOwner = null;
            AudioClip probe = null;
            VisualElement host = null;
            try
            {
                Require(EditorApplication.isPlaying && boot && boot.UiDocument && boot.UiDocument.rootVisualElement.panel != null,
                    "Open Boot in Play Mode before running M8 extra validation.");
                Require(previousService && UiTheme.Assets && UiTheme.Data != null, "The configured Boot audio service is unavailable.");
                audioOwner = new GameObject("M8 temporary audio validation") { hideFlags = HideFlags.HideAndDontSave };
                AudioService service = AudioService.Ensure(audioOwner, UiTheme.Assets, UiTheme.Data);
                // Checks inspect routing/playback state; the synthetic probe and all other sources stay inaudible.
                AudioListener.volume = 0; AudioListener.pause = false;
                host = new VisualElement { name = "M8ExtraValidationHost", pickingMode = PickingMode.Ignore, focusable = false };
                host.style.position = Position.Absolute;
                host.style.left = (float)(-2 * UiTheme.Number("referenceWidth")); host.style.top = (float)(-2 * UiTheme.Number("referenceHeight"));
                host.style.width = (float)UiTheme.Number("referenceWidth"); host.style.height = (float)UiTheme.Number("referenceHeight");
                host.style.opacity = 0; UiTheme.InstallRoot(host); boot.UiDocument.rootVisualElement.Add(host);
                Require(host.panel != null, "The temporary input host did not attach.");
                report["ui"] = ValidateUi(host, fixturePath, service);
                report["mixer"] = ImoniAudioSetup.Describe(UiTheme.Assets);
                Require(report["mixer"].Value<bool>("ready"), "The generated BGM/SFX child groups are missing.");
                report["audio"] = ValidateAudio(service, out probe);
                report["passed"] = true;
            }
            catch (Exception exception)
            {
                report["failure"] = exception.ToString();
                throw;
            }
            finally
            {
                try
                {
                    host?.RemoveFromHierarchy();
                    if (audioOwner) Object.DestroyImmediate(audioOwner);
                    if (probe) Object.DestroyImmediate(probe);
                    if (previousService)
                    {
                        previousService.Initialize(UiTheme.Assets, UiTheme.Data);
                        previousService.SetPreferences(previousEnabled, previousBgm, previousEffects);
                        FlushMixer(previousService);
                    }
                    report["audioSettingsRestored"] = AudioService.Instance == previousService && (!previousService ||
                        previousService.SoundEnabled == previousEnabled && Near(previousService.BgmVolume, previousBgm) && Near(previousService.EffectsVolume, previousEffects));
                    byte[] realAfter = !string.IsNullOrEmpty(realSave) && File.Exists(realSave) ? File.ReadAllBytes(realSave) : null;
                    bool unchanged = realBefore == null ? realAfter == null : realAfter != null && realBefore.SequenceEqual(realAfter);
                    report["realSaveUnchanged"] = unchanged;
                    if (!unchanged || !report.Value<bool>("audioSettingsRestored")) report["passed"] = false;
                }
                catch (Exception restoreFailure)
                {
                    report["passed"] = false; report["cleanupFailure"] = restoreFailure.ToString();
                }
                finally
                {
                    AudioListener.volume = previousListenerVolume; AudioListener.pause = previousListenerPause;
                    if (File.Exists(fixturePath)) File.Delete(fixturePath);
                    if (File.Exists(fixturePath + ".tmp")) File.Delete(fixturePath + ".tmp");
                    report["temporarySaveRemoved"] = !File.Exists(fixturePath) && !File.Exists(fixturePath + ".tmp");
                    Directory.CreateDirectory(Path.GetDirectoryName(reportPath));
                    File.WriteAllText(reportPath, report.ToString(Formatting.Indented));
                }
            }
            return report;
        }

        private static JObject ValidateUi(VisualElement host, string savePath, AudioService service)
        {
            var checks = new JArray();
            using (var router = new ScreenRouter(host))
            {
                var app = new ScreenContext(router, savePath);
                ScreenCatalog.Register(router, app);
                Require(router.Show("Main"), "Main is not registered.");
                SettingsDialog titleDialog = OpenSettings(router.CurrentView);
                Require(titleDialog.AudioContent.style.display.value == DisplayStyle.Flex, "Main did not open audio settings.");
                double originalBgm = app.Profile.BgmVolume, originalEffects = app.Profile.EffectsVolume;
                Click(Find<ImoniButton>(Find<AudioVolumeControl>(titleDialog, "bgm-volume"), "volume-decrease"));
                double expectedBgm = ImoniAudioPlan.AdjustVolume(app.Data, originalBgm, -1);
                Require(Near(app.Profile.BgmVolume, expectedBgm) && Near(app.Profile.EffectsVolume, originalEffects), "The BGM control changed the wrong channel.");
                Click(Find<ImoniButton>(Find<AudioVolumeControl>(titleDialog, "effects-volume"), "volume-decrease"));
                double expectedEffects = ImoniAudioPlan.AdjustVolume(app.Data, originalEffects, -1);
                Require(Near(app.Profile.BgmVolume, expectedBgm) && Near(app.Profile.EffectsVolume, expectedEffects), "The effect control changed the wrong channel.");
                Require(Near(service.BgmVolume, expectedBgm) && Near(service.EffectsVolume, expectedEffects), "Saved volume controls did not reach AudioService.");
                bool originalEnabled = app.Profile.SoundEnabled;
                Click(Find<ImoniButton>(titleDialog, "sound-toggle"));
                Require(app.Profile.SoundEnabled != originalEnabled && service.SoundEnabled == app.Profile.SoundEnabled, "The sound switch was not saved/applied.");
                Click(Find<ImoniButton>(titleDialog, "sound-toggle"));
                AssertReload(savePath, expectedBgm, expectedEffects, originalEnabled, null, null);
                Click(Find<ImoniButton>(titleDialog, "settings-close")); Require(!titleDialog.IsOpen, "Main settings did not close.");
                checks.Add("Main SettingsDialog opens/closes; separate BGM/effect buttons and global sound toggle save and apply.");

                CompleteMainLoading(router);
                Click(Find<ImoniButton>(router.CurrentView, "start"));
                Require(router.CurrentScreen == "Tutorial1", "A first launch did not start Tutorial1 directly.");
                const string firstName = "検証いも一";
                TextField registration = Find<MemberCard>(router.CurrentView, "registration-card").Q<TextField>();
                Input(registration, firstName); CompleteTutorial(router);
                Require(app.Profile.TutorialCompleted && app.Profile.Name == firstName, "Tutorial registration did not persist.");
                string firstId = app.Accounts.ActiveId;
                SettingsDialog homeDialog = OpenSettings(router.CurrentView);
                Require(homeDialog.GetType() == titleDialog.GetType(), "Main and Home use different settings controls.");
                checks.Add("First launch Main -> Tutorial1 -> Tutorial2 -> Tutorial3 -> Home; Home opens the shared SettingsDialog.");

                Click(Find<ImoniButton>(homeDialog, "account-switch"));
                Require(homeDialog.AccountContent.style.display.value == DisplayStyle.Flex && homeDialog.AudioContent.style.display.value == DisplayStyle.None,
                    "Account switching did not open within the settings dialog.");
                AccountPicker picker = homeDialog.AccountContent.Q<AccountPicker>();
                Require(picker != null && !Find<ImoniButton>(picker, "account-" + firstId).enabledSelf, "The active account must be disabled.");
                Input(Find<InputEntry>(picker, "account-name"), "   ");
                Require(!Find<ImoniButton>(picker, "account-create").enabledSelf, "Whitespace must not create an account.");
                const string secondName = "検証いも二";
                Input(Find<InputEntry>(picker, "account-name"), secondName);
                Click(Find<ImoniButton>(picker, "account-create"));
                Require(router.CurrentScreen == "Tutorial1" && app.Accounts.Ids.Count == 2 && app.Accounts.ActiveId != firstId,
                    "Creating a local account did not start its tutorial.");
                string secondId = app.Accounts.ActiveId;
                Require(app.Profile.Name == secondName && !app.Profile.TutorialCompleted && Near(app.Profile.BgmVolume, expectedBgm) && Near(app.Profile.EffectsVolume, expectedEffects),
                    "The new account did not inherit sound settings with fresh tutorial progress.");
                CompleteTutorial(router);
                homeDialog = OpenSettings(router.CurrentView);
                Click(Find<ImoniButton>(Find<AudioVolumeControl>(homeDialog, "bgm-volume"), "volume-decrease"));
                double secondBgm = ImoniAudioPlan.AdjustVolume(app.Data, expectedBgm, -1);
                Click(Find<ImoniButton>(homeDialog, "account-switch"));
                Click(Find<ImoniButton>(homeDialog, "account-" + firstId));
                Require(router.CurrentScreen == "Home" && app.Accounts.ActiveId == firstId && app.Profile.Name == firstName &&
                    Near(app.Profile.BgmVolume, expectedBgm) && Near(app.Profile.EffectsVolume, expectedEffects), "Switching accounts did not restore the first profile.");
                homeDialog = OpenSettings(router.CurrentView);
                Click(Find<ImoniButton>(homeDialog, "account-switch"));
                Click(Find<ImoniButton>(homeDialog, "account-" + secondId));
                Require(router.CurrentScreen == "Home" && app.Accounts.ActiveId == secondId && app.Profile.Name == secondName && Near(app.Profile.BgmVolume, secondBgm),
                    "Switching back lost the second profile or its independent BGM volume.");
                homeDialog = OpenSettings(router.CurrentView);
                Click(Find<ImoniButton>(homeDialog, "account-switch")); Click(Find<ImoniButton>(homeDialog, "account-back"));
                Require(homeDialog.AudioContent.style.display.value == DisplayStyle.Flex && homeDialog.AccountContent.style.display.value == DisplayStyle.None,
                    "Account back did not return to audio settings.");
                Click(Find<ImoniButton>(homeDialog, "settings-close"));
                AssertReload(savePath, secondBgm, expectedEffects, originalEnabled, secondId, secondName);
                checks.Add("Settings account slot: blank-name guard, create, first/second profile switching, independent volume persistence, back to audio.");

                Require(router.Show("Main"), "Main could not reopen."); CompleteMainLoading(router);
                Click(Find<ImoniButton>(router.CurrentView, "start"));
                Require(router.CurrentScreen == "Home", "A registered profile did not start Home directly.");
                checks.Add("Registered launch Main -> Home without Login.");
                return new JObject { ["checks"] = checks, ["accounts"] = app.Accounts.Ids.Count, ["activeName"] = app.Profile.Name,
                    ["savedBgmVolume"] = app.Profile.BgmVolume, ["savedEffectsVolume"] = app.Profile.EffectsVolume, ["screen"] = router.CurrentScreen };
            }
        }

        private static JObject ValidateAudio(AudioService service, out AudioClip probe)
        {
            probe = null;
            JObject config = UiTheme.Data;
            Require(!UiTheme.Assets.backgroundMusic && !service.BgmSource.clip && !service.BgmSource.isPlaying && !service.PlayBgm(),
                "An unconfigured BGM must remain silent and PlayBgm must return false.");
            Require(service.BgmSource.outputAudioMixerGroup == service.BgmGroup && service.DroneSource.outputAudioMixerGroup == service.EffectsGroup &&
                service.Voices.All(source => source.outputAudioMixerGroup == service.EffectsGroup), "Runtime audio sources use the wrong mixer groups.");
            double maximum = config["audio"]["maxVolume"].Value<double>();
            Require(config["audio"]["minVolume"].Value<double>() == 0, "The audio mute fixture expects a zero-volume endpoint.");
            int sampleRate = UiTheme.Assets.imoniEffectAudio.First(clip => clip).frequency;
            probe = AudioClip.Create("M8 temporary silent BGM probe", sampleRate, 1, sampleRate, false);
            probe.SetData(new float[sampleRate], 0);
            var states = new JArray();

            service.SetPreferences(true, maximum, 0); FlushMixer(service); service.SetBgmClip(probe);
            Require(service.BgmSource.isPlaying && service.BgmSource.loop && !service.BgmSource.mute && service.DroneSource.mute && service.Voices.All(source => source.mute),
                "BGM playback was not independent from zero effect volume.");
            AssertMixer(service, "masterVolumeParameter", 0);
            AssertMixer(service, "bgmVolumeParameter", ImoniAudioPlan.MasterDecibels(config, true, maximum));
            AssertMixer(service, "effectsVolumeParameter", config["audio"]["minDecibels"].Value<double>());
            states.Add(AudioState(service, "BGM playing, SFX muted"));

            service.SetPreferences(true, 0, maximum); FlushMixer(service); service.StartDrone(0, 1000, 0);
            Require(service.BgmSource.isPlaying && service.BgmSource.mute && service.DroneSource.isPlaying && !service.DroneSource.mute,
                "Zero BGM volume muted effects or stopped the background playback position.");
            AssertMixer(service, "bgmVolumeParameter", config["audio"]["minDecibels"].Value<double>());
            AssertMixer(service, "effectsVolumeParameter", ImoniAudioPlan.MasterDecibels(config, true, maximum));
            states.Add(AudioState(service, "BGM muted, SFX playing"));

            service.BeginEffect();
            Require(service.BgmSource.isPlaying && !service.DroneSource.isPlaying && service.Voices.All(source => !source.isPlaying), "Beginning/replaying an effect stopped BGM.");
            states.Add(AudioState(service, "BeginEffect stopped only SFX"));
            service.StartDrone(0, 1000, 0); service.StopBgm();
            Require(!service.BgmSource.isPlaying && service.DroneSource.isPlaying, "Stopping BGM stopped an active effect.");
            states.Add(AudioState(service, "StopBgm kept SFX playing"));
            service.PlayBgm(); service.StopEffects();
            Require(service.BgmSource.isPlaying && !service.DroneSource.isPlaying, "StopEffects stopped BGM.");
            service.SetPreferences(false, maximum, maximum); FlushMixer(service);
            Require(service.BgmSource.isPlaying && service.BgmSource.mute && service.DroneSource.mute && service.Voices.All(source => source.mute), "The global sound switch did not mute both channels.");
            AssertMixer(service, "masterVolumeParameter", config["audio"]["minDecibels"].Value<double>());
            states.Add(AudioState(service, "Global mute retains BGM position"));
            service.SetPreferences(true, maximum, maximum); FlushMixer(service); service.SetBgmClip(null);
            Require(!service.BgmSource.clip && !service.BgmSource.isPlaying && !service.PlayBgm(), "Clearing the optional BGM did not return to silence.");
            states.Add(AudioState(service, "Null BGM is silent"));
            return new JObject { ["unconfiguredBgmSilent"] = true, ["sourceRoutingCorrect"] = true, ["probeSampleRate"] = sampleRate,
                ["probePcm"] = "one second of generated silence; playback state verified without an audible soundtrack", ["states"] = states };
        }

        private static JObject AudioState(AudioService service, string action) => new JObject { ["action"] = action,
            ["enabled"] = service.SoundEnabled, ["bgmVolume"] = service.BgmVolume, ["effectsVolume"] = service.EffectsVolume,
            ["bgmPlaying"] = service.BgmSource.isPlaying, ["bgmMuted"] = service.BgmSource.mute,
            ["dronePlaying"] = service.DroneSource.isPlaying, ["droneMuted"] = service.DroneSource.mute };
        private static void AssertMixer(AudioService service, string key, double expected)
        {
            string parameter = UiTheme.Data["audio"][key].Value<string>();
            Require(service.Mixer.GetFloat(parameter, out float actual) && Math.Abs(actual - expected) < 0.001, "Incorrect mixer parameter " + parameter + ".");
        }
        private static void FlushMixer(AudioService service)
        {
            MethodInfo update = typeof(AudioService).GetMethod("Update", InstanceFlags);
            Require(update != null, "AudioService mixer update is unavailable."); update.Invoke(service, null);
        }
        private static void AssertReload(string savePath, double bgm, double effects, bool enabled, string id, string name)
        {
            using (var router = new ScreenRouter(new VisualElement()))
            {
                var restored = new ScreenContext(router, savePath);
                Require(Near(restored.Profile.BgmVolume, bgm) && Near(restored.Profile.EffectsVolume, effects) && restored.Profile.SoundEnabled == enabled,
                    "Reload lost sound preferences.");
                if (id != null) Require(restored.Accounts.ActiveId == id && restored.Profile.Name == name && restored.Accounts.Ids.Count == 2, "Reload lost the local account selection.");
            }
        }
        private static SettingsDialog OpenSettings(VisualElement screen)
        {
            Click(Find<ImoniButton>(screen, "settings"));
            SettingsDialog dialog = Find<SettingsDialog>(screen, "settings-dialog");
            Require(dialog.IsOpen && dialog.style.display.value == DisplayStyle.Flex, "The settings dialog did not open.");
            return dialog;
        }
        private static void CompleteMainLoading(ScreenRouter router)
        {
            var main = router.CurrentView as MainScreen;
            Require(main != null, "Expected Main before completing title loading.");
            MethodInfo advance = typeof(MainScreen).GetMethod("AdvanceLoading", InstanceFlags);
            Require(advance != null, "Main loading callback is unavailable.");
            double increment = UiTheme.Number("mainLoadingStepPercent"); Require(increment > 0, "Main loading increment must be positive.");
            int ticks = (int)Math.Ceiling(100 / increment);
            for (int i = 0; i < ticks; i++) advance.Invoke(main, null);
            Require(main.ProgressPercent == 100 && Find<ImoniButton>(main, "start").style.display.value == DisplayStyle.Flex, "Title loading did not enable start.");
        }
        private static void CompleteTutorial(ScreenRouter router)
        {
            foreach (string destination in new[] { "Tutorial2", "Tutorial3", "Home" })
            { Click(Find<ImoniButton>(router.CurrentView, "next")); Require(router.CurrentScreen == destination, "Tutorial did not navigate to " + destination + "."); }
        }
        private static void Click(Button button)
        {
            Require(button != null && button.enabledInHierarchy && button.clickable != null, "Expected an enabled button.");
            MethodInfo invoke = typeof(Clickable).GetMethod("Invoke", InstanceFlags, null, new[] { typeof(EventBase) }, null);
            Require(invoke != null, "Clickable.Invoke is unavailable.");
            using (ClickEvent click = ClickEvent.GetPooled()) { click.target = button; invoke.Invoke(button.clickable, new object[] { click }); }
        }
        private static void Input(TextField field, string value)
        {
            Require(field != null && field.panel != null, "An attached input field is required.");
            string previous = field.value; field.SetValueWithoutNotify(value);
            using (ChangeEvent<string> change = ChangeEvent<string>.GetPooled(previous, field.value)) { change.target = field; field.SendEvent(change); }
        }
        private static T Find<T>(VisualElement root, string name) where T : VisualElement => root.Q<T>(name) ?? throw new InvalidOperationException("Missing control: " + name);
        private static bool Near(double a, double b) => Math.Abs(a - b) < 0.000000001;
        private static string LibraryPath(string file) => Path.GetFullPath(Path.Combine(Application.dataPath, "../Library/" + file));
        private static void Require(bool condition, string message) { if (!condition) throw new InvalidOperationException("M8 extra validation: " + message); }
    }
}
