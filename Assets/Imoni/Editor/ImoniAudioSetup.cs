using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Imoni.Game.UI;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.Audio;
using Object = UnityEngine.Object;

namespace Imoni.Editor
{
    /// <summary>Unity creates and serializes the mixer. Internal editor entry points are isolated here for Unity 6.3.</summary>
    public static class ImoniAudioSetup
    {
        public const string MixerPath = "Assets/Imoni/Audio/ImoniMaster.mixer";
        private const BindingFlags Flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static;
        private static Type EditorType(string name) => typeof(AudioImporter).Assembly.GetType("UnityEditor.Audio." + name, true);
        private static object Get(object instance, string property) => instance.GetType().GetProperty(property, Flags).GetValue(instance);
        private static object Invoke(object instance, string method, params object[] args) => instance.GetType().GetMethods(Flags)
            .Single(value => value.Name == method && value.GetParameters().Length == args.Length).Invoke(instance, args);
        private static string Normalize(string value) => new string(value.Where(char.IsLetterOrDigit).Select(char.ToLowerInvariant).ToArray());

        public static void EnsureAssets(UIRuntimeAssets assets, JObject config)
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Exit Play Mode before generating the audio mixer.");
            Type controllerType = EditorType("AudioMixerController");
            var mixer = AssetDatabase.LoadAssetAtPath<AudioMixer>(MixerPath);
            if (!mixer)
                mixer = (AudioMixer)controllerType.GetMethod("CreateMixerControllerAtPath", Flags).Invoke(null, new object[] { MixerPath });
            object master = Get(mixer, "masterGroup");
            var effects = ((IEnumerable)Get(master, "effects")).Cast<object>().ToList();
            object compressor = effects.SingleOrDefault(effect => (string)Get(effect, "effectName") == "Compressor");
            if (compressor == null)
            {
                compressor = Activator.CreateInstance(EditorType("AudioMixerEffectController"), Flags, null, new object[] { "Compressor" }, null);
                Invoke(compressor, "PreallocateGUIDs");
                AssetDatabase.AddObjectToAsset((Object)compressor, mixer);
                Invoke(master, "InsertEffect", compressor, effects.Count);
            }
            compressor.GetType().GetProperty("bypass", Flags).SetValue(compressor, false);
            string[] parameterNames = ParameterNames("Compressor");
            var snapshots = ((IEnumerable)Get(mixer, "snapshots")).Cast<object>().ToArray();
            foreach (JProperty setting in ((JObject)config["audio"]["compressor"]).Properties())
            {
                string nativeName = parameterNames.SingleOrDefault(name => Normalize(name) == Normalize(setting.Name));
                if (nativeName == null) throw new InvalidOperationException("Unknown Compressor parameter " + setting.Name + "; Unity exposes " + string.Join(", ", parameterNames));
                foreach (object snapshot in snapshots)
                {
                    Invoke(compressor, "SetValueForParameter", mixer, snapshot, nativeName, setting.Value.Value<float>());
                    EditorUtility.SetDirty((Object)snapshot);
                }
            }
            object bgm = EnsureChildGroup(mixer, master, config["audio"]["bgmGroupName"].Value<string>());
            object sfx = EnsureChildGroup(mixer, master, config["audio"]["effectsGroupName"].Value<string>());
            ExposeVolume(mixer, master, config["audio"]["masterVolumeParameter"].Value<string>());
            ExposeVolume(mixer, bgm, config["audio"]["bgmVolumeParameter"].Value<string>());
            ExposeVolume(mixer, sfx, config["audio"]["effectsVolumeParameter"].Value<string>());
            assets.audioMixer = mixer; assets.audioMaster = (AudioMixerGroup)master;
            assets.audioBgm = (AudioMixerGroup)bgm; assets.audioEffects = (AudioMixerGroup)sfx;
            string musicPath = config["audio"]["bgmClipPath"].Value<string>();
            assets.backgroundMusic = string.IsNullOrWhiteSpace(musicPath) ? null : AssetDatabase.LoadAssetAtPath<AudioClip>(musicPath);
            if (!string.IsNullOrWhiteSpace(musicPath) && !assets.backgroundMusic)
                throw new InvalidOperationException("Configured BGM clip was not found: " + musicPath);
            EditorUtility.SetDirty((Object)bgm); EditorUtility.SetDirty((Object)sfx);
            EditorUtility.SetDirty((Object)master); EditorUtility.SetDirty((Object)compressor); EditorUtility.SetDirty(mixer); EditorUtility.SetDirty(assets);
            AssetDatabase.SaveAssets();
        }
        private static object EnsureChildGroup(AudioMixer mixer, object master, string name)
        {
            object group = ((IEnumerable)Get(master, "children")).Cast<object>().SingleOrDefault(item => ((Object)item).name == name);
            if (group != null) return group;
            group = Invoke(mixer, "CreateNewGroup", name, false);
            Invoke(mixer, "AddChildToParent", group, master);
            return group;
        }
        private static string[] ParameterNames(string effect)
        {
            Type definitions = EditorType("MixerEffectDefinitions");
            var parameters = (IEnumerable)definitions.GetMethod("GetEffectParameters", Flags).Invoke(null, new object[] { effect });
            return parameters.Cast<object>().Select(value => (string)value.GetType().GetField("name", Flags).GetValue(value)).ToArray();
        }
        private static void ExposeVolume(AudioMixer mixer, object group, string name)
        {
            object guid = Invoke(group, "GetGUIDForVolume");
            PropertyInfo property = mixer.GetType().GetProperty("exposedParameters", Flags);
            Type parameterType = EditorType("ExposedAudioParameter");
            FieldInfo nameField = parameterType.GetField("name", Flags), guidField = parameterType.GetField("guid", Flags);
            var existing = ((IEnumerable)property.GetValue(mixer)).Cast<object>()
                .Where(value => (string)nameField.GetValue(value) != name && !guid.Equals(guidField.GetValue(value))).ToList();
            object parameter = Activator.CreateInstance(parameterType); nameField.SetValue(parameter, name); guidField.SetValue(parameter, guid); existing.Add(parameter);
            Array array = Array.CreateInstance(parameterType, existing.Count);
            for (int index = 0; index < existing.Count; index++) array.SetValue(existing[index], index);
            property.SetValue(mixer, array);
        }
        /// <summary>Reads the generated Unity objects, not GameConfig, so capture reports prove the mixer wiring and effect values.</summary>
        public static JObject Describe(UIRuntimeAssets assets)
        {
            var mixer = assets.audioMixer;
            if (!mixer) return new JObject { ["ready"] = false };
            object master = Get(mixer, "masterGroup"), snapshot = Get(mixer, "TargetSnapshot");
            var effects = new JArray();
            foreach (object effect in (IEnumerable)Get(master, "effects"))
            {
                string name = (string)Get(effect, "effectName"); var parameters = new JObject();
                foreach (string key in ParameterNames(name)) parameters[key] = Convert.ToSingle(Invoke(effect, "GetValueForParameter", mixer, snapshot, key));
                effects.Add(new JObject { ["name"] = name, ["bypass"] = (bool)Get(effect, "bypass"), ["parameters"] = parameters });
            }
            var exposed = new JArray();
            foreach (object item in (IEnumerable)Get(mixer, "exposedParameters")) exposed.Add((string)item.GetType().GetField("name", Flags).GetValue(item));
            var children = ((IEnumerable)Get(master, "children")).Cast<AudioMixerGroup>().ToArray();
            return new JObject { ["ready"] = assets.audioMaster == (AudioMixerGroup)master && children.Contains(assets.audioBgm) && children.Contains(assets.audioEffects),
                ["assetPath"] = AssetDatabase.GetAssetPath(mixer), ["masterGroup"] = ((AudioMixerGroup)master).name,
                ["bgmGroup"] = assets.audioBgm ? assets.audioBgm.name : null, ["effectsGroup"] = assets.audioEffects ? assets.audioEffects.name : null,
                ["masterChildren"] = new JArray(children.Select(child => child.name)),
                ["backgroundMusic"] = assets.backgroundMusic ? AssetDatabase.GetAssetPath(assets.backgroundMusic) : null,
                ["effects"] = effects, ["exposedParameters"] = exposed };
        }
    }
}
