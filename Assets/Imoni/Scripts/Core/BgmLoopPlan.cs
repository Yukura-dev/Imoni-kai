using System;
using Newtonsoft.Json.Linq;

namespace Imoni.Core.Audio
{
    /// <summary>Validated sample boundaries for separately scheduled intro and seamless loop assets.</summary>
    public sealed class BgmLoopPlan
    {
        public int IntroSampleCount { get; }
        public int LoopEndSample { get; }
        public int SampleRate { get; }
        public int LoopSampleCount => LoopEndSample - IntroSampleCount;
        public double IntroSeconds => (double)IntroSampleCount / SampleRate;
        public double LoopSeconds => (double)LoopSampleCount / SampleRate;

        public BgmLoopPlan(JObject gameConfig)
        {
            JObject source = gameConfig?["audio"]?["gameMain"] as JObject
                ?? throw new ArgumentException("GameConfig.audio.gameMain is required.", nameof(gameConfig));
            IntroSampleCount = RequiredPositiveInteger(source, "loopStartSample");
            LoopEndSample = RequiredPositiveInteger(source, "loopEndSample");
            SampleRate = RequiredPositiveInteger(source, "sampleRate");
            if (LoopEndSample <= IntroSampleCount) throw new ArgumentException("gameMain loopEndSample must be after loopStartSample.");
        }

        private static int RequiredPositiveInteger(JObject source, string key)
        {
            JToken token = source[key];
            if (token == null || token.Type != JTokenType.Integer) throw new ArgumentException("Missing gameMain sample setting: " + key);
            long value = token.Value<long>();
            if (value <= 0 || value > int.MaxValue) throw new ArgumentException("gameMain sample settings must be positive 32-bit integers: " + key);
            return (int)value;
        }
    }
}
