using System;
using System.Collections.Generic;
using System.Linq;
using Imoni.Core.Effects;
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace Imoni.Game.Effects
{
    /// <summary>Plays the supplied WAVs from presentation events; owns no round rules or scheduled work.</summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(ImoniEffectView))]
    public sealed class ImoniEffectAudio : MonoBehaviour
    {
        public ImoniEffectView view;
        public AudioClip drone;
        public AudioClip beat;
        public AudioClip hihat;
        public AudioClip hit;
        public AudioClip[] levelUps;
        public AudioClip successFinale;
        public AudioClip failureFinale;

        private JObject settings;
        private AudioSource droneSource;
        private AudioLowPassFilter droneFilter;
        private readonly List<AudioSource> voices = new List<AudioSource>();
        private readonly Dictionary<AudioClip, AudioClip> octaveClips = new Dictionary<AudioClip, AudioClip>();
        private readonly Dictionary<AudioSource, double> voiceStarted = new Dictionary<AudioSource, double>();
        private bool running, subscribed;
        private double droneStartedAt;

        private void OnEnable()
        {
            if (!view) view = GetComponent<ImoniEffectView>();
            if (!view || subscribed) return;
            view.EffectEvent += OnEffectEvent;
            view.SoundChanged += OnSoundChanged;
            view.Replayed += OnReplay;
            view.Hidden += OnHidden;
            subscribed = true;
        }

        private void OnDisable()
        {
            StopAll(); running = false;
            if (!subscribed || !view) return;
            view.EffectEvent -= OnEffectEvent;
            view.SoundChanged -= OnSoundChanged;
            view.Replayed -= OnReplay;
            view.Hidden -= OnHidden;
            subscribed = false;
        }

        private void OnDestroy()
        {
            foreach (var clip in octaveClips.Values) if (clip) Destroy(clip);
            octaveClips.Clear();
        }

        private void OnReplay()
        {
            StopAll();
            settings = (JObject)view.Config.Data["audio"] ?? throw new InvalidOperationException("GameConfig.imoniEffect.audio is missing.");
            EnsureSources();
            running = true;
        }

        private void OnHidden() { running = false; StopAll(); }

        private void OnSoundChanged(bool enabled)
        {
            if (!enabled) { StopAll(); return; }
            if (running && view.Timeline != null && !view.Timeline.FinaleAtMs.HasValue && view.Timeline.NowMs >= view.Config.V("droneStartMs"))
                StartDrone(view.Timeline.NowMs);
        }

        private void OnEffectEvent(ImoniEffectEvent effectEvent)
        {
            if (!running || !view.SoundEnabled || settings == null) return;
            var timeline = view.Timeline;
            // After a long render pause, do not play a backlog of overlapping historical impacts.
            if (effectEvent.Kind == "drone")
            {
                if (!timeline.FinaleAtMs.HasValue) StartDrone(effectEvent.AtMs);
                return;
            }
            if (timeline.NowMs - effectEvent.AtMs > N("staleEventMs")) return;
            switch (effectEvent.Kind)
            {
                case "beat":
                    Play(effectEvent.Level >= N("hihatMinLevel") ? hihat : beat, 1, N("beatVolume"));
                    break;
                case "hit":
                    var step = Math.Min(effectEvent.Hits, (int)N("hitStepMax"));
                    var penta = settings["pentatonic"].Values<double>().ToArray();
                    var semitones = N("levelSemitones") * effectEvent.Level + penta[step % penta.Length] + N("octaveSemitones") * (step / penta.Length);
                    Play(hit, Math.Pow(N("octaveRatio"), semitones / N("octaveSemitones")), N("hitVolume"));
                    break;
                case "level":
                    var index = effectEvent.Level - 1;
                    if (levelUps != null && index >= 0 && index < levelUps.Length) Play(levelUps[index], 1, N("levelVolume"));
                    break;
                case "finale":
                    Play(timeline.Failed ? failureFinale : successFinale, 1, N("finaleVolume"));
                    break;
            }
        }

        private void Update()
        {
            if (!running || settings == null || !view || view.Timeline == null || !view.SoundEnabled || !droneSource) return;
            var timeline = view.Timeline;
            if (!droneSource.isPlaying) return;
            var level = timeline.Level;
            droneSource.pitch = (float)Math.Pow(N("octaveRatio"), N("levelSemitones") * level / N("octaveSemitones"));
            droneFilter.cutoffFrequency = (float)(N("lowPassBaseHz") + N("lowPassPerLevelHz") * level);
            var envelope = Math.Min(1, Math.Max(0, (timeline.NowMs - droneStartedAt) / N("droneAttackMs")));
            if (timeline.FinaleAtMs.HasValue)
                envelope *= Math.Max(0, 1 - (timeline.NowMs - timeline.FinaleAtMs.Value) / N(timeline.Failed ? "failureFadeMs" : "successFadeMs"));
            droneSource.volume = (float)Math.Min(1, (N("droneBaseVolume") + N("droneVolumePerLevel") * level) * envelope);
            if (timeline.FinaleAtMs.HasValue && envelope <= 0) droneSource.Stop();
        }

        private void StartDrone(double at)
        {
            if (!drone || !droneSource || droneSource.isPlaying) return;
            droneSource.clip = drone; droneSource.volume = 0; droneSource.loop = true;
            droneStartedAt = at; droneSource.Play();
        }

        private void EnsureSources()
        {
            if (!droneSource)
            {
                droneSource = CreateSource("Imoni Drone", (int)N("dronePriority"));
                droneFilter = droneSource.gameObject.AddComponent<AudioLowPassFilter>();
                droneFilter.lowpassResonanceQ = (float)N("lowPassResonance");
            }
            while (voices.Count < (int)N("voiceCount")) voices.Add(CreateSource("Imoni Effect Voice " + voices.Count, (int)N("effectPriority")));
        }

        private AudioSource CreateSource(string objectName, int priority)
        {
            var child = new GameObject(objectName);
            child.transform.SetParent(transform, false);
            var source = child.AddComponent<AudioSource>();
            source.playOnAwake = false; source.loop = false; source.spatialBlend = 0;
            source.dopplerLevel = 0; source.priority = priority;
            return source;
        }

        private void Play(AudioClip clip, double pitch, double volume)
        {
            if (!clip || voices.Count == 0) return;
            if (pitch > N("sourcePitchLimit"))
            {
                // The final pentatonic hits exceed a source's pitch range. A frequency-doubled
                // copy preserves the requested pitch without changing the provided WAV on disk.
                var octave = OctaveClip(clip);
                if (octave) { clip = octave; pitch /= N("octaveRatio"); }
            }
            var source = voices.FirstOrDefault(s => !s.isPlaying) ?? voices.OrderBy(s => voiceStarted.TryGetValue(s, out var started) ? started : 0).First();
            source.Stop(); source.clip = clip; source.pitch = (float)pitch; source.volume = (float)volume;
            voiceStarted[source] = view.Timeline.NowMs;
            source.Play();
        }

        private AudioClip OctaveClip(AudioClip original)
        {
            if (octaveClips.TryGetValue(original, out var cached)) return cached;
            if (original.loadState != AudioDataLoadState.Loaded) original.LoadAudioData();
            var samples = new float[original.samples * original.channels];
            if (!original.GetData(samples, 0)) return null;
            var clip = AudioClip.Create(original.name + " (presentation octave)", original.samples, original.channels, (int)(original.frequency * N("octaveRatio")), false);
            clip.SetData(samples, 0); octaveClips.Add(original, clip);
            return clip;
        }

        private void StopAll()
        {
            if (droneSource) droneSource.Stop();
            foreach (var source in voices) if (source) source.Stop();
            voiceStarted.Clear();
        }

        private double N(string key) => settings[key]?.Value<double>() ?? throw new InvalidOperationException("Missing effect audio setting: " + key);
    }
}
