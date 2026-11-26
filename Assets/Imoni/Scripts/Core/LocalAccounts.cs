using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Imoni.Core
{
    /// <summary>Local profiles in one atomic save. Existing top-level profile fields remain readable.</summary>
    public sealed class LocalAccounts
    {
        private readonly Dictionary<string, ProfileData> profiles = new Dictionary<string, ProfileData>(StringComparer.Ordinal);
        public string ActiveId { get; private set; }
        public IReadOnlyList<string> Ids => profiles.Keys.ToArray();
        private LocalAccounts() { }
        public static LocalAccounts Load(JObject config, string json)
        {
            var accounts = new LocalAccounts();
            JObject saved = null;
            if (!string.IsNullOrWhiteSpace(json))
                try { saved = JObject.Parse(json); } catch (JsonException) { }
            if (saved?["AccountProfiles"] is JObject stored)
                foreach (JProperty item in stored.Properties())
                    if (!string.IsNullOrWhiteSpace(item.Name) && item.Value is JObject)
                        accounts.profiles[item.Name] = ProfilePersistence.Load(config, item.Value.ToString());
            JToken activeToken = saved?["ActiveAccountId"];
            string active = activeToken?.Type == JTokenType.String ? activeToken.Value<string>() : null;
            if (string.IsNullOrWhiteSpace(active))
            {
                active = "local-default";
                // A damaged selection must not replace an already saved member with the top-level copy.
                if (accounts.profiles.ContainsKey(active))
                    do { active = "local-recovered-" + Guid.NewGuid().ToString("N"); }
                    while (accounts.profiles.ContainsKey(active));
            }
            accounts.ActiveId = active;
            // The top-level active profile is authoritative, including migration from old saves.
            accounts.profiles[accounts.ActiveId] = ProfilePersistence.Load(config, json);
            return accounts;
        }
        public ProfileData Get(JObject config, string id)
        {
            if (!profiles.TryGetValue(id, out ProfileData profile)) throw new ArgumentException("Unknown local account.", nameof(id));
            return ProfilePersistence.Load(config, ProfilePersistence.Save(config, profile));
        }
        public void UpdateActive(JObject config, ProfileData profile) => profiles[ActiveId] = ProfilePersistence.Normalize(config, profile);
        public ProfileData Switch(JObject config, string id, ProfileData current)
        {
            if (!profiles.ContainsKey(id)) throw new ArgumentException("Unknown local account.", nameof(id));
            UpdateActive(config, current); ActiveId = id;
            return Get(config, id);
        }
        public ProfileData Create(JObject config, string id, ProfileData current, string name)
        {
            if (string.IsNullOrWhiteSpace(id) || profiles.ContainsKey(id)) throw new ArgumentException("A new account ID is required.", nameof(id));
            UpdateActive(config, current);
            var profile = ProfilePersistence.Normalize(config, new ProfileData { Name = name,
                SoundEnabled = current.SoundEnabled, SoundVolume = current.SoundVolume,
                BgmVolume = current.BgmVolume, EffectsVolume = current.EffectsVolume });
            profiles.Add(id, profile); ActiveId = id;
            return Get(config, id);
        }
        public string Save(JObject config, ProfileData current)
        {
            UpdateActive(config, current);
            var root = JObject.Parse(ProfilePersistence.Save(config, current));
            root["ActiveAccountId"] = ActiveId;
            root["AccountProfiles"] = new JObject(profiles.Select(item => new JProperty(item.Key,
                JObject.Parse(ProfilePersistence.Save(config, item.Value)))));
            return root.ToString(Formatting.Indented);
        }
    }
}
