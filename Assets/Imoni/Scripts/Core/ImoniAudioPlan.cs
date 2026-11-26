using System;
using System.Linq;
using Newtonsoft.Json.Linq;

namespace Imoni.Core.Effects
{
    public sealed class ImoniAudioCue
    {
        public string Kind { get; internal set; }
        public string ClipKey { get; internal set; }
        public double AtMs { get; internal set; }
        public double Pitch { get; internal set; }
        public double Gain { get; internal set; }
    }
    public sealed class ImoniDroneState
    {
        public double Pitch { get; internal set; }
        public double CutoffHz { get; internal set; }
        public double Gain { get; internal set; }
        public bool Finished { get; internal set; }
    }

    /// <summary>Deterministic audio parameters from the same timed events as the visual effect.</summary>
    public sealed class ImoniAudioPlan
    {
        private readonly JObject audio;
        private readonly double[] pentatonic;
        public ImoniAudioPlan(ImoniEffectConfig config)
        {
            audio = (JObject)config.Data["audio"] ?? throw new ArgumentException("Effect audio configuration is required.");
            pentatonic = audio["pentatonic"].Values<double>().ToArray();
        }
        public double Number(string key) => audio[key]?.Value<double>() ?? throw new ArgumentException("Missing audio setting: " + key);
        public ImoniAudioCue Cue(ImoniEffectEvent item, bool failed)
        {
            string clip; double pitch = 1, gain;
            switch (item.Kind)
            {
                case "beat": clip = item.Level >= Number("hihatMinLevel") ? "sfx_beat_hihat" : "sfx_beat"; gain = Number("beatVolume"); break;
                case "hit":
                    int step = Math.Min(item.Hits, (int)Number("hitStepMax"));
                    double semitones = Number("levelSemitones") * item.Level + pentatonic[step % pentatonic.Length]
                        + Number("octaveSemitones") * (step / pentatonic.Length);
                    clip = "sfx_hit"; pitch = Math.Pow(Number("octaveRatio"), semitones / Number("octaveSemitones")); gain = Number("hitVolume"); break;
                case "level": clip = "sfx_levelup_L" + item.Level; gain = Number("levelVolume"); break;
                case "finale": clip = failed ? "sfx_finale_fail" : "sfx_finale_success"; gain = Number("finaleVolume"); break;
                default: return null;
            }
            return new ImoniAudioCue { Kind = item.Kind, ClipKey = clip, AtMs = item.AtMs, Pitch = pitch, Gain = gain };
        }
        public ImoniDroneState Drone(double nowMs, int level, double startedAtMs, double? finaleAtMs, bool failed)
        {
            double envelope = Math.Min(1, Math.Max(0, (nowMs - startedAtMs) / Number("droneAttackMs")));
            double fade = Number(failed ? "failureFadeMs" : "successFadeMs");
            if (finaleAtMs.HasValue)
                envelope *= Math.Max(0, (finaleAtMs.Value + fade - nowMs) / fade);
            return new ImoniDroneState
            {
                Pitch = Math.Pow(Number("octaveRatio"), Number("levelSemitones") * level / Number("octaveSemitones")),
                CutoffHz = Number("lowPassBaseHz") + Number("lowPassPerLevelHz") * level,
                Gain = Math.Min(1, (Number("droneBaseVolume") + Number("droneVolumePerLevel") * level) * envelope),
                Finished = finaleAtMs.HasValue && nowMs >= finaleAtMs.Value + fade
            };
        }
        public static double NormalizeVolume(JObject config, double volume)
        {
            JObject settings = (JObject)config["audio"];
            if (double.IsNaN(volume) || double.IsInfinity(volume)) volume = settings["defaultVolume"].Value<double>();
            return Math.Max(settings["minVolume"].Value<double>(), Math.Min(settings["maxVolume"].Value<double>(), volume));
        }
        public static double MasterDecibels(JObject config, bool enabled, double volume)
        {
            JObject settings = (JObject)config["audio"];
            double minimum = settings["minDecibels"].Value<double>();
            volume = NormalizeVolume(config, volume);
            return !enabled || volume <= 0 ? minimum : Math.Max(minimum, settings["decibelsPerAmplitudeLog10"].Value<double>() * Math.Log10(volume));
        }
        public static double AdjustVolume(JObject config, double current, int direction) =>
            NormalizeVolume(config, (double)((decimal)NormalizeVolume(config, current) + Math.Sign(direction) * config["audio"]["volumeStep"].Value<decimal>()));
    }
}
