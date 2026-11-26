using System;
using System.Globalization;
using System.Linq;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Imoni.Core
{
    public sealed class ProfileData
    {
        public string Name { get; set; }
        public string FirstTitle { get; set; }
        public string SecondTitle { get; set; }
        public string BandColor { get; set; }
        public string OutfitColor { get; set; }
        public bool SoundEnabled { get; set; } = true;
        // Retained for saves written before independent BGM/effects controls existed.
        public double SoundVolume { get; set; } = double.NaN;
        public double BgmVolume { get; set; } = double.NaN;
        public double EffectsVolume { get; set; } = double.NaN;
        public bool TutorialCompleted { get; set; }
        public string[] OwnedFirstTitles { get; set; } = Array.Empty<string>();
        public string[] OwnedSecondTitles { get; set; } = Array.Empty<string>();
        public string[] OwnedBadges { get; set; } = Array.Empty<string>();
        public ProgressionStats Progression { get; set; } = new ProgressionStats();
        public string[] AppliedRoundKeys { get; set; } = Array.Empty<string>();
        public LocalRankingRecord[] Rankings { get; set; } = Array.Empty<LocalRankingRecord>();
    }

    /// <summary>Save validation and name/title constraints, independent of Unity or UI controls.</summary>
    public static class ProfilePersistence
    {
        public static ProfileData Load(JObject config, string json)
        {
            ProfileData value = null;
            if (!string.IsNullOrWhiteSpace(json))
                try
                {
                    JObject saved = JObject.Parse(json);
                    value = saved.ToObject<ProfileData>();
                    // Before ownership arrays existed, a saved selected catalog part was owned.
                    // Migrate just that selection; never reintroduce the old mock's whole inventory.
                    if (value != null && saved[nameof(ProfileData.OwnedFirstTitles)] == null && config["titles"]["firstParts"].Values<string>().Contains(value.FirstTitle))
                        value.OwnedFirstTitles = new[] { value.FirstTitle };
                    if (value != null && saved[nameof(ProfileData.OwnedSecondTitles)] == null && config["titles"]["secondParts"].Values<string>().Contains(value.SecondTitle))
                        value.OwnedSecondTitles = new[] { value.SecondTitle };
                }
                catch (JsonException) { /* A damaged save starts from the configured defaults. */ }
            return Normalize(config, value ?? new ProfileData());
        }

        public static string Save(JObject config, ProfileData profile) => JsonConvert.SerializeObject(Normalize(config, profile), Formatting.Indented);

        public static ProfileData Normalize(JObject config, ProfileData profile)
        {
            if (config == null) throw new ArgumentNullException(nameof(config));
            if (profile == null) throw new ArgumentNullException(nameof(profile));
            JToken defaults = config["outGame"]["profile"];
            string name = string.IsNullOrWhiteSpace(profile.Name) ? defaults["defaultName"].Value<string>() : profile.Name.Trim();
            var characters = new StringInfo(name);
            int max = config["ui"]["profileNameMaxLength"].Value<int>();
            if (characters.LengthInTextElements > max) name = characters.SubstringByTextElements(0, max);
            string[] first = Owned(profile.OwnedFirstTitles, defaults["initialOwnedFirst"], config["titles"]["firstParts"]);
            string[] second = Owned(profile.OwnedSecondTitles, defaults["initialOwnedSecond"], config["titles"]["secondParts"]);
            return new ProfileData
            {
                Name = name,
                FirstTitle = first.Contains(profile.FirstTitle) ? profile.FirstTitle : config["titles"]["default"]["first"].Value<string>(),
                SecondTitle = second.Contains(profile.SecondTitle) ? profile.SecondTitle : config["titles"]["default"]["second"].Value<string>(),
                BandColor = Allowed(profile.BandColor, defaults["bandColors"], defaults["defaultBandColor"].Value<string>()),
                OutfitColor = Allowed(profile.OutfitColor, defaults["outfitColors"], defaults["defaultOutfitColor"].Value<string>()),
                SoundEnabled = profile.SoundEnabled,
                SoundVolume = Effects.ImoniAudioPlan.NormalizeVolume(config, profile.SoundVolume),
                BgmVolume = Effects.ImoniAudioPlan.NormalizeChannelVolume(config, profile.BgmVolume, profile.SoundVolume),
                EffectsVolume = Effects.ImoniAudioPlan.NormalizeChannelVolume(config, profile.EffectsVolume, profile.SoundVolume),
                TutorialCompleted = profile.TutorialCompleted,
                OwnedFirstTitles = first, OwnedSecondTitles = second,
                OwnedBadges = (profile.OwnedBadges ?? Array.Empty<string>()).Where(id => config["progression"]["badgeRules"].Any(rule => rule["id"].Value<string>() == id)).Distinct(StringComparer.Ordinal).ToArray(),
                Progression = PlayerProgression.Normalize(config, profile.Progression),
                AppliedRoundKeys = (profile.AppliedRoundKeys ?? Array.Empty<string>()).Where(key => !string.IsNullOrWhiteSpace(key)).Distinct(StringComparer.Ordinal).ToArray(),
                Rankings = LocalRanking.Normalize(config, profile.Rankings)
            };
        }
        /// <summary>Idempotent local profile grant; a replay of the same detached result cannot duplicate ownership.</summary>
        public static ProfileData ApplyResultRewards(JObject config, ProfileData profile, RoundResult result, string actorId, string roundKey = null) =>
            PlayerProgression.Apply(config, profile, result, actorId, roundKey ?? "legacy:" + result.Seed + ":" + result.RoundNumber).Profile;
        private static string[] Owned(string[] saved, JToken initial, JToken catalog) =>
            (initial?.Values<string>() ?? Enumerable.Empty<string>()).Concat(saved ?? Array.Empty<string>())
                .Where(value => catalog.Values<string>().Contains(value)).Distinct(StringComparer.Ordinal).ToArray();
        private static string Allowed(string value, JToken allowed, string fallback) =>
            allowed != null && allowed.Values<string>().Contains(value) ? value : fallback;
    }
}
