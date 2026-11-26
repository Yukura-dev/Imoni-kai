using System;
using System.Collections.Generic;
using System.Linq;
using Imoni.Core.Audio;
using UnityEngine;

namespace Imoni.Game.Audio
{
    /// <summary>Applies the pure C# fade envelope to pooled loop sources; a retarget never cuts an audible tail.</summary>
    internal sealed class BgmPlaybackController
    {
        private sealed class Voice
        {
            internal string Key;
            internal AudioSource Source;
            internal AudioSource Intro;
            internal AudioClip IntroClip;
            internal float Gain = 1;
            internal bool Started;
            internal double StartedAtDsp;
            internal double LoopAtDsp;
            internal string ObservedPhase;
            internal long ObservedLoop;
        }

        private readonly Func<string, AudioSource> createSource;
        private readonly List<Voice> voices = new List<Voice>();
        private readonly BgmTransition transition = new BgmTransition();
        private Voice selected;
        private bool muted = true;

        internal BgmPlaybackController(Func<string, AudioSource> factory)
        {
            createSource = factory ?? throw new ArgumentNullException(nameof(factory));
            selected = CreateVoice();
            CreateVoice(); // The ordinary A/B crossfade requires no runtime allocation.
        }

        internal AudioSource PrimarySource => selected.Started && selected.IntroClip && AudioSettings.dspTime < selected.LoopAtDsp ? selected.Intro : selected.Source;
        internal AudioClip TargetClip => transition.TargetKey == null ? null : voices.Where(voice => voice.Source && voice.Key == transition.TargetKey).Select(voice => voice.Source.clip).FirstOrDefault();
        internal string TargetKey => transition.TargetKey;
        internal bool IsTransitioning => transition.IsTransitioning;
        internal IReadOnlyList<AudioSource> Sources => voices.SelectMany(voice => voice.Intro ? new[] { voice.Source, voice.Intro } : new[] { voice.Source }).Where(source => source).ToArray();
        internal IReadOnlyDictionary<string, double> Weights => transition.Weights;

        internal bool CrossFade(string trackKey, AudioClip clip, double seconds, float gain, AudioClip intro, double schedulingLeadSeconds)
        {
            if (double.IsNaN(seconds) || double.IsInfinity(seconds) || seconds < 0)
                throw new ArgumentOutOfRangeException(nameof(seconds));
            if (float.IsNaN(gain) || float.IsInfinity(gain) || gain < 0 || gain > 1)
                throw new ArgumentOutOfRangeException(nameof(gain));
            if (double.IsNaN(schedulingLeadSeconds) || double.IsInfinity(schedulingLeadSeconds) || schedulingLeadSeconds < 0)
                throw new ArgumentOutOfRangeException(nameof(schedulingLeadSeconds));
            if (clip && string.IsNullOrWhiteSpace(trackKey)) throw new ArgumentException("A playing track requires a stable catalog key.", nameof(trackKey));
            if (!clip) trackKey = null;
            Voice target = clip ? Acquire(trackKey, clip, intro) : null;
            if (target != null)
            {
                selected = target; target.Gain = gain;
                if (!target.Started) Start(target, schedulingLeadSeconds);
            }
            bool changed = transition.Request(trackKey, seconds);
            Apply();
            return changed;
        }

        internal bool Tick(double seconds)
        {
            bool wasTransitioning = transition.IsTransitioning;
            transition.Tick(seconds); Apply();
            return wasTransitioning && !transition.IsTransitioning;
        }

        internal void SetMuted(bool value)
        { muted = value; foreach (AudioSource source in Sources) source.mute = value; }

        internal void SetImmediate(AudioClip clip, bool play)
        {
            if (selected.Source.clip == clip && !selected.IntroClip)
            {
                if (clip && play) PlaySelected(); else Stop();
                return;
            }
            Stop();
            foreach (Voice voice in voices) { voice.Key = null; voice.IntroClip = null; }
            selected.Source.clip = clip; selected.Source.volume = 1; selected.Gain = 1;
            if (clip)
            {
                selected.Key = "legacy:" + clip.GetInstanceID();
                if (play) PlaySelected();
            }
        }

        internal bool PlaySelected(double schedulingLeadSeconds = 0)
        {
            if (!selected.Source.clip) return false;
            // A caller may resume the selected legacy clip after StopBgm().
            if (selected.Key == null) selected.Key = "legacy:" + selected.Source.clip.GetInstanceID();
            transition.Reset(selected.Key);
            selected.Source.mute = muted;
            Apply();
            if (!selected.Started)
            {
                if (selected.IntroClip) Start(selected, schedulingLeadSeconds);
                else
                {
                    selected.Source.Play(); selected.Started = true;
                    selected.StartedAtDsp = selected.LoopAtDsp = AudioSettings.dspTime;
                    selected.ObservedPhase = "scheduled"; selected.ObservedLoop = 0;
                }
            }
            return true;
        }

        internal void Stop()
        {
            transition.Reset();
            foreach (Voice voice in voices) Stop(voice);
        }

        private Voice CreateVoice()
        {
            AudioSource source = createSource("Imoni Background Music " + (voices.Count + 1));
            source.loop = true; source.volume = 0; source.mute = muted;
            var voice = new Voice { Source = source }; voices.Add(voice); return voice;
        }

        private Voice Acquire(string key, AudioClip clip, AudioClip intro)
        {
            Voice voice = voices.FirstOrDefault(item => item.Key == key);
            if (voice != null && (voice.Source.clip != clip || voice.IntroClip != intro))
                throw new InvalidOperationException("A BGM catalog key cannot change its clip while assigned: " + key);
            if (voice != null) return voice;
            voice = voices.FirstOrDefault(item => !item.Started && Weight(item.Key) <= 0) ?? CreateVoice();
            voice.Key = key; voice.Source.clip = clip; voice.Source.volume = 0; voice.IntroClip = intro;
            if (intro)
            {
                if (!voice.Intro) voice.Intro = createSource("Imoni Background Music Intro " + voices.IndexOf(voice));
                voice.Intro.clip = intro; voice.Intro.loop = false; voice.Intro.volume = 0;
            }
            return voice;
        }

        private double Weight(string key) => key != null && transition.Weights.TryGetValue(key, out double value) ? value : 0;

        private void Apply()
        {
            foreach (Voice voice in voices)
            {
                if (!voice.Source) continue;
                double weight = Weight(voice.Key);
                voice.Source.volume = (float)weight * voice.Gain;
                if (voice.Intro) voice.Intro.volume = voice.Source.volume;
                if (weight <= 0 && voice.Key != transition.TargetKey && voice.Started) Stop(voice);
            }
        }

        private void Start(Voice voice, double lead)
        {
            voice.Source.mute = muted;
            voice.StartedAtDsp = AudioSettings.dspTime + lead;
            voice.LoopAtDsp = voice.StartedAtDsp;
            if (voice.IntroClip)
            {
                voice.Intro.mute = muted;
                // Actual imported sample lengths keep the join independent of frames or rounded decimal durations.
                voice.LoopAtDsp += (double)voice.IntroClip.samples / voice.IntroClip.frequency;
                voice.Intro.PlayScheduled(voice.StartedAtDsp);
                voice.Intro.SetScheduledEndTime(voice.LoopAtDsp);
            }
            voice.Source.PlayScheduled(voice.LoopAtDsp);
            voice.Started = true; voice.ObservedPhase = "scheduled"; voice.ObservedLoop = 0;
        }

        private static void Stop(Voice voice)
        {
            if (voice.Source) { voice.Source.Stop(); voice.Source.volume = 0; }
            if (voice.Intro) { voice.Intro.Stop(); voice.Intro.volume = 0; }
            voice.Started = false;
        }

        // The DSP clock owns playback. Update only observes boundaries for review evidence; it never starts the loop.
        internal void ObservePlayback(Action<string, string> record)
        {
            double now = AudioSettings.dspTime;
            foreach (Voice voice in voices)
            {
                if (!voice.Source || !voice.Started) continue;
                string phase = Phase(voice, now);
                long completedLoops = CompletedLoops(voice, now);
                if (phase != voice.ObservedPhase)
                {
                    voice.ObservedPhase = phase;
                    if (phase == "intro" || phase == "loop") record(phase + "-start", voice.Key);
                }
                if (completedLoops > voice.ObservedLoop) record("loop-repeat", voice.Key);
                voice.ObservedLoop = completedLoops;
            }
        }

        private static string Phase(Voice voice, double now) => !voice.Started ? "stopped" : now < voice.StartedAtDsp ? "scheduled"
            : voice.IntroClip && now < voice.LoopAtDsp ? "intro" : "loop";

        private static long CompletedLoops(Voice voice, double now) => !voice.Started || !voice.Source.clip || now <= voice.LoopAtDsp ? 0
            : (long)Math.Floor((now - voice.LoopAtDsp) / ((double)voice.Source.clip.samples / voice.Source.clip.frequency));

        internal BgmVoiceState[] Snapshot(double volume, bool enabled) => voices.Where(voice => voice.Source && voice.Key != null).Select(voice => new BgmVoiceState
        {
            TrackKey = voice.Key, LoopClip = voice.Source.clip ? voice.Source.clip.name : null,
            IntroClip = voice.IntroClip ? voice.IntroClip.name : null,
            Weight = Weight(voice.Key), Gain = voice.Source.volume,
            EffectiveGain = enabled && !voice.Source.mute ? voice.Source.volume * volume : 0,
            Started = voice.Started, Muted = voice.Source.mute,
            StartedAtDsp = voice.StartedAtDsp, LoopAtDsp = voice.LoopAtDsp,
            IntroSamples = voice.IntroClip ? voice.IntroClip.samples : 0,
            LoopSamples = voice.Source.clip ? voice.Source.clip.samples : 0,
            SampleRate = voice.Source.clip ? voice.Source.clip.frequency : 0,
            Phase = Phase(voice, AudioSettings.dspTime), CompletedLoops = CompletedLoops(voice, AudioSettings.dspTime),
            IntroTimeSamples = voice.IntroClip && voice.Intro ? voice.Intro.timeSamples : 0, LoopTimeSamples = voice.Source.timeSamples,
            IntroIsPlaying = voice.IntroClip && voice.Intro && voice.Intro.isPlaying, LoopIsPlaying = voice.Source.isPlaying
        }).ToArray();
    }
}
