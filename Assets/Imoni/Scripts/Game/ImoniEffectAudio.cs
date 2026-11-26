using Imoni.Core.Effects;
using Imoni.Game.Audio;
using Imoni.Game.UI;
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace Imoni.Game.Effects
{
    /// <summary>Bridges visual timeline events to the shared audio service; never resettles a round.</summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(ImoniEffectView))]
    public sealed class ImoniEffectAudio : MonoBehaviour
    {
        public ImoniEffectView view;
        public AudioService Service { get; set; }
        // Kept for the preserved legacy preview scene. The production service uses UIRuntimeAssets.
        public AudioClip drone, beat, hihat, hit, successFinale, failureFinale;
        public AudioClip[] levelUps;
        private ImoniAudioPlan plan;
        private bool subscribed, running;
        private void OnEnable()
        {
            if (!view) view = GetComponent<ImoniEffectView>();
            if (!view || subscribed) return;
            view.EffectEvent += OnEffectEvent; view.SoundChanged += OnSoundChanged;
            view.Replayed += OnReplay; view.Hidden += OnHidden; subscribed = true;
        }
        private void OnDisable()
        {
            OnHidden();
            if (!subscribed || !view) return;
            view.EffectEvent -= OnEffectEvent; view.SoundChanged -= OnSoundChanged;
            view.Replayed -= OnReplay; view.Hidden -= OnHidden; subscribed = false;
        }
        private void OnReplay()
        {
            if (!Service) Service = AudioService.Instance;
            if (!Service)
            {
                Service = AudioService.Ensure(gameObject, UiTheme.Assets, UiTheme.Data);
                Service.SetPreferences(view.SoundEnabled, UiTheme.Data["audio"]["defaultVolume"].Value<double>());
            }
            plan = new ImoniAudioPlan(view.Config); Service.BeginEffect(); running = true;
        }
        private void OnHidden() { if (!running) return; running = false; if (Service) Service.StopEffects(); }
        private void OnSoundChanged(bool enabled)
        {
            if (!Service) return;
            Service.SetPreferences(enabled, Service.BgmVolume, Service.EffectsVolume);
            if (enabled && running && view.Timeline != null && !view.Timeline.FinaleAtMs.HasValue && view.Timeline.NowMs >= view.Config.V("droneStartMs"))
                Service.StartDrone(view.Timeline.NowMs, view.Timeline.NowMs, view.Timeline.Level);
        }
        private void OnEffectEvent(ImoniEffectEvent item)
        {
            if (!running || !Service || plan == null) return;
            var timeline = view.Timeline;
            if (item.Kind == "drone")
            {
                if (!timeline.FinaleAtMs.HasValue) Service.StartDrone(item.AtMs, timeline.NowMs, timeline.Level);
                return;
            }
            Service.Play(plan.Cue(item, timeline.Failed), timeline.NowMs - item.AtMs <= plan.Number("staleEventMs"));
        }
        private void Update()
        {
            if (running && Service && view && view.Timeline != null)
                Service.UpdateDrone(view.Timeline.NowMs, view.Timeline.Level, view.Timeline.FinaleAtMs, view.Timeline.Failed);
        }
    }
}
