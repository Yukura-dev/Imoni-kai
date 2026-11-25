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
        public bool TutorialCompleted { get; set; }
    }

    /// <summary>Save validation and name/title constraints, independent of Unity or UI controls.</summary>
    public static class ProfilePersistence
    {
        public static ProfileData Load(JObject config, string json)
        {
            ProfileData value = null;
            if (!string.IsNullOrWhiteSpace(json))
                try { value = JsonConvert.DeserializeObject<ProfileData>(json); }
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
            return new ProfileData
            {
                Name = name,
                FirstTitle = Allowed(profile.FirstTitle, defaults["initialOwnedFirst"], config["titles"]["default"]["first"].Value<string>()),
                SecondTitle = Allowed(profile.SecondTitle, defaults["initialOwnedSecond"], config["titles"]["default"]["second"].Value<string>()),
                BandColor = Allowed(profile.BandColor, defaults["bandColors"], defaults["defaultBandColor"].Value<string>()),
                OutfitColor = Allowed(profile.OutfitColor, defaults["outfitColors"], defaults["defaultOutfitColor"].Value<string>()),
                SoundEnabled = profile.SoundEnabled,
                TutorialCompleted = profile.TutorialCompleted
            };
        }
        private static string Allowed(string value, JToken allowed, string fallback) =>
            allowed != null && allowed.Values<string>().Contains(value) ? value : fallback;
    }
}
