using System;
using System.Collections.Generic;
using System.Linq;
using Imoni.Core.Effects;
using Imoni.Game.UI;
using Newtonsoft.Json.Linq;
using UnityEngine;
using UnityEngine.Audio;

namespace Imoni.Game.Audio
{
    public sealed class AudioPlaybackEvent
    {
        public double AtMs { get; internal set; }
        public double DspTimeSeconds { get; internal set; }
        public string Kind { get; internal set; }
        public string ClipName { get; internal set; }
        public double Pitch { get; internal set; }
        public double SourcePitch { get; internal set; }
        public string SourceClipName { get; internal set; }
        public double Gain { get; internal set; }
        public double EffectiveGain { get; internal set; }
        public double CutoffHz { get; internal set; }
        public bool Played { get; internal set; }
    }

    /// <summary>One audio owner: all supplied clips pass through the generated Master mixer and its compressor.</summary>
    [DisallowMultipleComponent]
    public sealed class AudioService : MonoBehaviour
    {
        public static AudioService Instance { get; private set; }
        public bool SoundEnabled { get; private set; }
        public bool Enabled => SoundEnabled;
        public double Volume { get; private set; }
        public AudioMixer Mixer => assets ? assets.audioMixer : null;
        public AudioMixerGroup MasterGroup => assets ? assets.audioMaster : null;
        public IReadOnlyList<AudioPlaybackEvent> EventLog => eventLog.AsReadOnly();
        public int ActiveVoices => voices.Count(source => source && source.isPlaying) + (droneSource && droneSource.isPlaying ? 1 : 0);
        public AudioSource DroneSource => droneSource;
        public AudioLowPassFilter DroneFilter => droneFilter;
        public IReadOnlyList<AudioSource> Voices => voices.AsReadOnly();
        public event Action<bool, double> PreferencesChanged;

        private UIRuntimeAssets assets;
        private JObject config;
        private ImoniAudioPlan plan;
        private AudioSource droneSource;
        private AudioLowPassFilter droneFilter;
        private readonly List<AudioSource> voices = new List<AudioSource>();
        private readonly List<AudioPlaybackEvent> eventLog = new List<AudioPlaybackEvent>();
        private readonly Dictionary<AudioSource, double> voiceStarted = new Dictionary<AudioSource, double>();
        private readonly Dictionary<AudioClip, AudioClip> octaveClips = new Dictionary<AudioClip, AudioClip>();
        private double droneStartedAt;
        private int droneLevel = -1;
        private bool preferencesDirty;

        public static AudioService Ensure(GameObject owner, UIRuntimeAssets runtimeAssets, JObject data)
        {
            AudioService service = owner.GetComponent<AudioService>();
            if (!service) service = owner.AddComponent<AudioService>();
            service.Initialize(runtimeAssets, data); return service;
        }
        public void Initialize(UIRuntimeAssets runtimeAssets, JObject data)
        {
            assets = runtimeAssets; config = data; Instance = this;
            plan = new ImoniAudioPlan(ImoniEffectConfig.FromJson(data.ToString()));
            if (!MasterGroup || !Mixer) throw new InvalidOperationException("Run Imoni > Setup Project to generate the Master AudioMixer.");
            if (!droneSource)
            {
                droneSource = CreateSource("Imoni Drone", (int)plan.Number("dronePriority"), Clip("bgm_drone_loop"));
                droneFilter = droneSource.gameObject.AddComponent<AudioLowPassFilter>();
                droneFilter.lowpassResonanceQ = (float)plan.Number("lowPassResonance");
            }
            while (voices.Count < (int)plan.Number("voiceCount")) voices.Add(CreateSource("Imoni Voice " + voices.Count, (int)plan.Number("effectPriority")));
            AudioListener.volume = 1;
        }
        public void SetPreferences(bool enabled, double volume)
        {
            SoundEnabled = enabled; Volume = ImoniAudioPlan.NormalizeVolume(config, volume);
            foreach (AudioSource source in voices) source.mute = !enabled || Volume <= 0;
            if (droneSource) droneSource.mute = !enabled || Volume <= 0;
            preferencesDirty = true;
            if (!enabled) StopAll();
            PreferencesChanged?.Invoke(enabled, Volume);
        }
        private void Update()
        {
            // AudioMixer.SetFloat is applied after Awake/OnEnable, as required by the runtime mixer lifecycle.
            if (!preferencesDirty || !Mixer) return;
            string parameter = config["audio"]["masterVolumeParameter"].Value<string>();
            if (!Mixer.SetFloat(parameter, (float)ImoniAudioPlan.MasterDecibels(config, SoundEnabled, Volume)))
                throw new InvalidOperationException("Missing exposed mixer parameter: " + parameter);
            preferencesDirty = false;
        }
        public void BeginEffect() { StopAll(); eventLog.Clear(); }
        public void ClearEventLog() => eventLog.Clear();
        public void StartDrone(double atMs, double nowMs, int level)
        {
            if (!droneSource || droneSource.isPlaying || !SoundEnabled) return;
            droneStartedAt = atMs; droneLevel = -1;
            droneSource.loop = true; droneSource.volume = 0;
            droneSource.Play(); UpdateDrone(nowMs, level, null, false);
            Log(atMs, "drone", droneSource.clip.name, droneSource.pitch, droneSource.volume, true, droneFilter.cutoffFrequency);
        }
        public void UpdateDrone(double nowMs, int level, double? finaleAtMs, bool failed)
        {
            if (!droneSource || !droneSource.isPlaying) return;
            ImoniDroneState state = plan.Drone(nowMs, level, droneStartedAt, finaleAtMs, failed);
            droneSource.pitch = (float)state.Pitch; droneSource.volume = (float)state.Gain; droneFilter.cutoffFrequency = (float)state.CutoffHz;
            if (level != droneLevel)
            { droneLevel = level; Log(nowMs, "drone-level", droneSource.clip.name, state.Pitch, state.Gain, true, state.CutoffHz); }
            if (state.Finished) { droneSource.Stop(); Log(nowMs, "drone-stop", droneSource.clip.name, state.Pitch, 0, false, state.CutoffHz); }
        }
        public void Play(ImoniAudioCue cue, bool fresh = true)
        {
            if (cue == null) return;
            AudioClip clip = Clip(cue.ClipKey);
            if (!clip) throw new InvalidOperationException("Missing supplied audio clip: " + cue.ClipKey);
            bool audible = fresh && SoundEnabled && Volume > 0;
            AudioPlaybackEvent entry = Log(cue.AtMs, fresh ? cue.Kind : "stale-" + cue.Kind, cue.ClipKey, cue.Pitch, cue.Gain, audible);
            if (!audible) return;
            double pitch = cue.Pitch;
            if (pitch > plan.Number("sourcePitchLimit")) { clip = OctaveClip(clip); pitch /= plan.Number("octaveRatio"); }
            entry.SourceClipName = clip.name; entry.SourcePitch = pitch;
            var source = voices.FirstOrDefault(item => !item.isPlaying) ?? voices.OrderBy(item => voiceStarted.TryGetValue(item, out double at) ? at : 0).First();
            source.Stop(); source.clip = clip; source.pitch = (float)pitch; source.volume = (float)cue.Gain;
            voiceStarted[source] = cue.AtMs; source.Play();
        }
        public void StopAll()
        {
            if (droneSource) droneSource.Stop();
            foreach (AudioSource source in voices) if (source) source.Stop();
            voiceStarted.Clear(); droneLevel = -1;
        }
        private AudioPlaybackEvent Log(double at, string kind, string clip, double pitch, double gain, bool played, double cutoff = 0)
        {
            var entry = new AudioPlaybackEvent { AtMs = at, DspTimeSeconds = AudioSettings.dspTime, Kind = kind, ClipName = clip, SourceClipName = clip, Pitch = pitch, SourcePitch = pitch, Gain = gain,
                EffectiveGain = played && SoundEnabled ? gain * Volume : 0, CutoffHz = cutoff, Played = played && SoundEnabled && Volume > 0 };
            eventLog.Add(entry);
            int capacity = config["audio"]["eventLogCapacity"].Value<int>();
            if (eventLog.Count > capacity) eventLog.RemoveRange(0, eventLog.Count - capacity);
            return entry;
        }
        private AudioClip Clip(string name) => assets.imoniEffectAudio.FirstOrDefault(clip => clip && clip.name == name);
        private AudioSource CreateSource(string objectName, int priority, AudioClip clip = null)
        {
            var child = new GameObject(objectName); child.SetActive(false); child.transform.SetParent(transform, false);
            var source = child.AddComponent<AudioSource>(); source.playOnAwake = false; source.loop = false;
            source.spatialBlend = 0; source.dopplerLevel = 0; source.priority = priority; source.clip = clip;
            source.outputAudioMixerGroup = MasterGroup; child.SetActive(true); return source;
        }
        private AudioClip OctaveClip(AudioClip original)
        {
            if (octaveClips.TryGetValue(original, out AudioClip cached)) return cached;
            if (original.loadState != AudioDataLoadState.Loaded) original.LoadAudioData();
            var samples = new float[original.samples * original.channels];
            if (!original.GetData(samples, 0)) throw new InvalidOperationException("Cannot read supplied hit WAV for its high octave.");
            var clip = AudioClip.Create(original.name + " (presentation octave)", original.samples, original.channels, (int)(original.frequency * plan.Number("octaveRatio")), false);
            clip.SetData(samples, 0); octaveClips.Add(original, clip); return clip;
        }
        private void OnDisable() => StopAll();
        private void OnDestroy()
        { StopAll(); foreach (AudioClip clip in octaveClips.Values) if (clip) Destroy(clip); octaveClips.Clear(); if (Instance == this) Instance = null; }
    }
}
