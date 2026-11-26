using System;
using System.Collections.Generic;
using Newtonsoft.Json.Linq;

namespace Imoni.Core.Audio
{
    public sealed class BgmSelection
    {
        public string TrackKey { get; }
        public double FadeSeconds { get; }
        public BgmSelection(string trackKey, double fadeSeconds) { TrackKey = trackKey; FadeSeconds = fadeSeconds; }
    }

    /// <summary>Screen-to-track policy. Clip assets and playback belong to the Game layer.</summary>
    public sealed class BgmPlan
    {
        private readonly Dictionary<string, string> homeTracks = new Dictionary<string, string>(StringComparer.Ordinal);
        public string Style { get; }
        public string GameTrackKey { get; }
        public double CrossfadeSeconds { get; }
        public double EffectFadeOutSeconds { get; }

        public BgmPlan(JObject gameConfig)
        {
            JObject audio = gameConfig?["audio"] as JObject ?? throw new ArgumentException("GameConfig.audio is required.", nameof(gameConfig));
            Style = RequiredText(audio, "bgmStyle");
            CrossfadeSeconds = RequiredDuration(audio, "bgmCrossfadeSeconds");
            EffectFadeOutSeconds = RequiredDuration(audio, "bgmEffectFadeOutSeconds");
            JObject tracks = audio["bgmTracks"] as JObject ?? throw new ArgumentException("audio.bgmTracks is required.");
            GameTrackKey = RequiredText(tracks, "game");
            foreach (string style in new[] { "wa", "band" })
            {
                JObject source = tracks[style] as JObject
                    ?? throw new ArgumentException("Missing audio.bgmTracks." + style);
                homeTracks.Add(style, RequiredText(source, "home"));
            }
            RequireStyle(Style);
        }

        public BgmSelection Select(string screen, string originScreen = null, string styleOverride = null)
        {
            string style = styleOverride ?? Style;
            RequireStyle(style);
            string context = screen == "Help" ? originScreen : screen;
            if (context == "Imoni" || context == "ImoniFail") return new BgmSelection(null, EffectFadeOutSeconds);
            return new BgmSelection(context == "Game" ? GameTrackKey : homeTracks[style], CrossfadeSeconds);
        }

        private void RequireStyle(string value)
        { if (!homeTracks.ContainsKey(value)) throw new ArgumentException("audio.bgmStyle must be wa or band."); }
        private static string RequiredText(JObject source, string key)
        {
            JToken token = source[key];
            string value = token?.Type == JTokenType.String ? token.Value<string>() : null;
            if (string.IsNullOrWhiteSpace(value)) throw new ArgumentException("Missing BGM setting: " + key);
            return value;
        }
        private static double RequiredDuration(JObject source, string key)
        {
            JToken token = source[key];
            if (token == null || (token.Type != JTokenType.Float && token.Type != JTokenType.Integer))
                throw new ArgumentException("Missing BGM duration: " + key);
            double value = token.Value<double>();
            if (double.IsNaN(value) || double.IsInfinity(value) || value < 0)
                throw new ArgumentException("BGM duration must be finite and nonnegative: " + key);
            return value;
        }
    }
}
