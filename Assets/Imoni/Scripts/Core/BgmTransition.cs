using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace Imoni.Core.Audio
{
    /// <summary>Linear envelopes from an explicit clock; retargeting starts at the currently audible weights.</summary>
    public sealed class BgmTransition
    {
        private readonly Dictionary<string, double> weights = new Dictionary<string, double>(StringComparer.Ordinal);
        private readonly Dictionary<string, double> startingWeights = new Dictionary<string, double>(StringComparer.Ordinal);
        private readonly ReadOnlyDictionary<string, double> view;
        public IReadOnlyDictionary<string, double> Weights => view;
        public string TargetKey { get; private set; }
        public double ElapsedSeconds { get; private set; }
        public double DurationSeconds { get; private set; }
        public bool IsTransitioning => ElapsedSeconds < DurationSeconds;

        public BgmTransition() { view = new ReadOnlyDictionary<string, double>(weights); }

        public bool Request(string targetKey, double seconds)
        {
            ValidateKey(targetKey); ValidateTime(seconds, nameof(seconds));
            // Navigation within a music context does not rewind the track or prolong its envelope.
            if (string.Equals(TargetKey, targetKey, StringComparison.Ordinal)) return false;
            if (seconds == 0) { Reset(targetKey); return true; }
            TargetKey = targetKey; DurationSeconds = seconds; ElapsedSeconds = 0;
            startingWeights.Clear();
            foreach (var pair in weights) if (pair.Value > 0) startingWeights.Add(pair.Key, pair.Value);
            if (targetKey != null && !startingWeights.ContainsKey(targetKey)) startingWeights.Add(targetKey, 0);
            weights.Clear();
            bool changing = false;
            foreach (var pair in startingWeights)
            {
                weights.Add(pair.Key, pair.Value);
                if (pair.Value != (pair.Key == targetKey ? 1 : 0)) changing = true;
            }
            if (!changing) Reset(targetKey);
            return true;
        }

        public void Tick(double unscaledDeltaSeconds)
        {
            ValidateTime(unscaledDeltaSeconds, nameof(unscaledDeltaSeconds));
            if (!IsTransitioning || unscaledDeltaSeconds == 0) return;
            ElapsedSeconds = Math.Min(DurationSeconds, ElapsedSeconds + unscaledDeltaSeconds);
            double ratio = ElapsedSeconds / DurationSeconds;
            if (ElapsedSeconds == DurationSeconds)
            {
                weights.Clear();
                if (TargetKey != null) weights.Add(TargetKey, 1);
                startingWeights.Clear(); return;
            }
            foreach (var pair in startingWeights)
            {
                double target = pair.Key == TargetKey ? 1 : 0;
                weights[pair.Key] = pair.Value + (target - pair.Value) * ratio;
            }
        }

        /// <summary>Immediate set/stop, used only by explicit transport controls and initialization.</summary>
        public void Reset(string trackKey = null)
        {
            ValidateKey(trackKey);
            TargetKey = trackKey; ElapsedSeconds = DurationSeconds = 0;
            startingWeights.Clear(); weights.Clear();
            if (trackKey != null) weights.Add(trackKey, 1);
        }

        private static void ValidateKey(string key)
        { if (key != null && string.IsNullOrWhiteSpace(key)) throw new ArgumentException("A BGM key must be nonempty; use null to stop.", nameof(key)); }
        private static void ValidateTime(double value, string parameter)
        { if (double.IsNaN(value) || double.IsInfinity(value) || value < 0) throw new ArgumentOutOfRangeException(parameter, "BGM time must be finite and nonnegative."); }
    }
}
