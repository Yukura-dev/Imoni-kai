using System;
using System.Linq;
using Imoni.Core.Effects;
using Imoni.Game.Effects;
using UnityEngine;
using UnityEngine.UIElements;

namespace Imoni.Game.UI.Screens
{
    /// <summary>Both Imoni routes present the frozen Core result; replay and skip never settle it again.</summary>
    public sealed class ImoniEffectScreen : VisualElement, IDisposable
    {
        private readonly ScreenContext app;
        private readonly GameObject runtime;
        private bool disposed;
        public ImoniEffectView Effect { get; }
        public ImoniEffectView View => Effect;
        public ImoniEffectTimeline Timeline => Effect ? Effect.Timeline : null;

        public ImoniEffectScreen(ScreenContext app)
        {
            this.app = app ?? throw new ArgumentNullException(nameof(app));
            var result = app.Server?.Snapshot().LastResult;
            if (result == null) throw new InvalidOperationException("The Imoni effect requires a settled round.");
            var presentation = ImoniEffectResultProjection.Present(UiTheme.Config, result);
            var assets = UiTheme.Assets;
            name = nameof(ImoniEffectScreen); AddToClassList("screen-view");
            style.flexGrow = 1;
            UiTheme.Apply(this);
            runtime = new GameObject("ImoniEffectRuntime");
            Effect = runtime.AddComponent<ImoniEffectView>();
            Effect.Host = this;
            Effect.gameConfig = assets.config; Effect.visualTree = assets.imoniEffectTree;
            Effect.themeStyle = assets.theme; Effect.effectStyle = assets.imoniEffectStyle;
            Effect.textures = assets.textures.Where(item => item.texture).Select(item => item.texture).ToArray();
            Effect.SoundChanged += SaveSound;
            try
            {
                BindExistingAudio(assets);
                Effect.Begin(presentation.Score, presentation.SuccessReferenceScore, presentation.Failed,
                    presentation.MissingMaterials, () => app.Go(presentation.ResultRoute), presentation.Seed, app.Profile.SoundEnabled);
            }
            catch { Dispose(); throw; }
        }

        private void BindExistingAudio(UIRuntimeAssets assets)
        {
            var clips = assets.imoniEffectAudio ?? Array.Empty<AudioClip>();
            AudioClip Clip(string key) => clips.FirstOrDefault(clip => clip && clip.name == key);
            var audio = runtime.AddComponent<ImoniEffectAudio>();
            audio.view = Effect;
            audio.drone = Clip("bgm_drone_loop"); audio.beat = Clip("sfx_beat");
            audio.hihat = Clip("sfx_beat_hihat"); audio.hit = Clip("sfx_hit");
            audio.levelUps = Enumerable.Range(1, UiTheme.Data["imoniEffect"]["levelThresholdRatios"].Count())
                .Select(level => Clip("sfx_levelup_L" + level)).ToArray();
            audio.successFinale = Clip("sfx_finale_success"); audio.failureFinale = Clip("sfx_finale_fail");
        }

        private void SaveSound(bool enabled)
        {
            app.Profile.SoundEnabled = enabled;
            app.SaveProfile();
        }

        public void Dispose()
        {
            if (disposed) return;
            disposed = true;
            if (Effect)
            {
                Effect.SoundChanged -= SaveSound;
                Effect.Hide();
            }
            if (runtime) UnityEngine.Object.Destroy(runtime);
        }
    }
}
