#if UNITY_EDITOR
using System;
using System.IO;
using System.Reflection;
using PeakPlunder.Audio;
using UnityEditor;
using UnityEngine;
using UnityEngine.Audio;

namespace PeakPlunder.EditorTools
{
    /// <summary>
    /// Editor utility to create PeakPlunder runtime assets (AudioMixer, SoundLibrary placeholder, etc.)
    /// that cannot be generated from dynamic MCP scripts due to internal-type reflection restrictions.
    /// Invoke via: Tools > PeakPlunder > Bootstrap Assets
    /// </summary>
    public static class PeakPlunderAssetBootstrap
    {
        private const string MIXER_DIR  = "Assets/Sandbox/Audio";
        private const string MIXER_PATH = "Assets/Sandbox/Audio/PeakPlunderMixer.mixer";

        [MenuItem("Tools/PeakPlunder/Bootstrap Assets")]
        public static void BootstrapAll()
        {
            CreateAudioMixer();
            PopulateSoundLibrary();
            Debug.Log("[PeakPlunder] Bootstrap complete.");
        }

        private const string SOUND_LIB_PATH = "Assets/Sandbox/Audio/DefaultSoundLibrary.asset";

        [MenuItem("Tools/PeakPlunder/Populate SoundLibrary")]
        public static void PopulateSoundLibrary()
        {
            var lib = AssetDatabase.LoadAssetAtPath<SoundLibrary>(SOUND_LIB_PATH);
            if (lib == null)
            {
                lib = ScriptableObject.CreateInstance<SoundLibrary>();
                AssetDatabase.CreateAsset(lib, SOUND_LIB_PATH);
                Debug.Log($"[PeakPlunder] Created SoundLibrary at {SOUND_LIB_PATH}");
            }

            var mixer = AssetDatabase.LoadAssetAtPath<AudioMixer>(MIXER_PATH);
            AudioMixerGroup seGroup          = FindGroup(mixer, "SE");
            AudioMixerGroup environmentGroup = FindGroup(mixer, "Environment");
            AudioMixerGroup voiceGroup       = FindGroup(mixer, "Voice");

            var ids = (SoundId[])Enum.GetValues(typeof(SoundId));
            var entries = new SoundLibrary.SoundEntry[ids.Length - 1]; // exclude None
            int ei = 0;
            foreach (var id in ids)
            {
                if (id == SoundId.None) continue;

                var entry = new SoundLibrary.SoundEntry
                {
                    id              = id,
                    clip            = null,
                    spatialBlend    = ClassifyIs2D(id) ? 0f : 1f,
                    volume          = 1f,
                    loop            = IsLoopSe(id),
                    pitchVariation  = HasPitchVariation(id) ? 0.05f : 0f,
                    mixerGroup      = ClassifyMixerGroup(id, seGroup, environmentGroup, voiceGroup),
                    maxConcurrent   = DefaultConcurrency(id),
                };
                entries[ei++] = entry;
            }

            var entriesField = typeof(SoundLibrary).GetField(
                "_entries",
                BindingFlags.NonPublic | BindingFlags.Instance);
            entriesField?.SetValue(lib, entries);

            EditorUtility.SetDirty(lib);
            AssetDatabase.SaveAssets();
            Debug.Log($"[PeakPlunder] Populated {ei} SoundLibrary entries (clips left null — assign manually).");
        }

        private static AudioMixerGroup FindGroup(AudioMixer mixer, string name)
        {
            if (mixer == null) return null;
            var groups = mixer.FindMatchingGroups(name);
            foreach (var g in groups)
                if (g.name == name) return g;
            return null;
        }

        private static bool ClassifyIs2D(SoundId id)
        {
            var s = id.ToString();
            return s.StartsWith("Ui") || s.StartsWith("Result") || s == "StaminaWarning" || s == "StaminaEmpty";
        }

        private static bool IsLoopSe(SoundId id)
        {
            var s = id.ToString();
            return s.EndsWith("Ambient") || s == "WinchLoop" || s == "StaminaWarning" || s == "HeliHover";
        }

        private static bool HasPitchVariation(SoundId id)
        {
            var s = id.ToString();
            return s.StartsWith("Footstep") || s == "RagdollImpact" || s.StartsWith("Rockfall") || s == "ItemImpact";
        }

        private static AudioMixerGroup ClassifyMixerGroup(SoundId id,
            AudioMixerGroup se, AudioMixerGroup env, AudioMixerGroup voice)
        {
            var s = id.ToString();
            if (s.EndsWith("Ambient") || s.StartsWith("Rockfall") || s == "Avalanche" || s == "IceCrack"
                || s.StartsWith("Trap") || s == "FloorCrumble" || s == "FloorCrumbleWarn" || s.StartsWith("Heli"))
                return env;
            return se;
        }

        private static int DefaultConcurrency(SoundId id)
        {
            var s = id.ToString();
            if (s.StartsWith("Footstep")) return 8;
            if (s.StartsWith("Ui")) return 2;
            if (s.EndsWith("Ambient")) return 1;
            return 4;
        }

        [MenuItem("Tools/PeakPlunder/Create AudioMixer")]
        public static void CreateAudioMixer()
        {
            if (!Directory.Exists(MIXER_DIR))
            {
                Directory.CreateDirectory(MIXER_DIR);
                AssetDatabase.Refresh();
            }

            if (File.Exists(MIXER_PATH))
            {
                Debug.Log($"[PeakPlunder] AudioMixer already exists at {MIXER_PATH}");
                return;
            }

            // Invoke internal UnityEditor.Audio.AudioMixerController.CreateMixerControllerAtPath(string)
            var asm = typeof(AssetDatabase).Assembly;
            var ctrlType = asm.GetType("UnityEditor.Audio.AudioMixerController");
            if (ctrlType == null)
            {
                Debug.LogError("[PeakPlunder] AudioMixerController type not found.");
                return;
            }

            var createMethod = ctrlType.GetMethod(
                "CreateMixerControllerAtPath",
                BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static);

            if (createMethod == null)
            {
                Debug.LogError("[PeakPlunder] CreateMixerControllerAtPath not found.");
                return;
            }

            var mixer = createMethod.Invoke(null, new object[] { MIXER_PATH });
            if (mixer == null)
            {
                Debug.LogError("[PeakPlunder] AudioMixer creation returned null.");
                return;
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            // Add groups: BGM, SE, Voice, Environment (under Master which is auto-created)
            AddGroupsAndExposeParams(mixer, ctrlType);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            var loaded = AssetDatabase.LoadAssetAtPath<AudioMixer>(MIXER_PATH);
            Debug.Log($"[PeakPlunder] AudioMixer created: {(loaded != null ? loaded.name : "null")}");
        }

        private static void AddGroupsAndExposeParams(object mixer, Type ctrlType)
        {
            try
            {
                var editorAsm = typeof(AssetDatabase).Assembly;
                var groupType = editorAsm.GetType("UnityEditor.Audio.AudioMixerGroupController");
                if (groupType == null)
                {
                    Debug.LogError("[PeakPlunder] AudioMixerGroupController type not found.");
                    return;
                }

                // mixer.masterGroup (property) — returns root AudioMixerGroupController
                var masterGroupProp = ctrlType.GetProperty("masterGroup",
                    BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                var masterGroup = masterGroupProp?.GetValue(mixer, null);
                if (masterGroup == null)
                {
                    Debug.LogError("[PeakPlunder] masterGroup not accessible.");
                    return;
                }

                // Method: CreateNewGroup(string name, bool createUndoRecord)
                var createGroupMethod = ctrlType.GetMethod(
                    "CreateNewGroup",
                    BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);

                // Method: AddChildToParent(AudioMixerGroupController child, AudioMixerGroupController parent)
                var addChildMethod = ctrlType.GetMethod(
                    "AddChildToParent",
                    BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);

                // Method: AddGroupToCurrentSnapshot(AudioMixerGroupController group)
                var addToSnapshotMethod = ctrlType.GetMethod(
                    "AddGroupToCurrentSnapshot",
                    BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);

                if (createGroupMethod == null || addChildMethod == null)
                {
                    Debug.LogWarning("[PeakPlunder] Group-creation methods not found. Skipping group add.");
                    return;
                }

                string[] groupNames = { "BGM", "SE", "Voice", "Environment" };
                foreach (var gname in groupNames)
                {
                    var newGroup = createGroupMethod.Invoke(mixer, new object[] { gname, false });
                    addChildMethod.Invoke(mixer, new[] { newGroup, masterGroup });
                    addToSnapshotMethod?.Invoke(mixer, new[] { newGroup });
                    Debug.Log($"[PeakPlunder] Added group '{gname}'");
                }

                // Expose parameters: MasterVolume/BgmVolume/SeVolume/VoiceVolume
                // Each group has effects; the first Attenuation effect's "Volume" parameter can be exposed.
                // Use mixer.AddExposedParameter(GUID, name) / SetName
                ExposeVolumeParams(mixer, ctrlType, masterGroup, groupNames);
            }
            catch (Exception e)
            {
                Debug.LogError($"[PeakPlunder] AddGroupsAndExposeParams error: {e.Message}\n{e.StackTrace}");
            }
        }

        private static void ExposeVolumeParams(object mixer, Type ctrlType, object masterGroup, string[] groupNames)
        {
            Debug.Log("[PeakPlunder] ExposeVolumeParams start");

            // Try a few method candidates for enumerating groups
            MethodInfo getAllGroupsMethod =
                ctrlType.GetMethod("GetAllAudioGroupsSlow",
                    BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)
                ?? ctrlType.GetMethod("GetAllAudioGroups",
                    BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);

            System.Collections.IList allGroups = null;
            if (getAllGroupsMethod != null)
            {
                allGroups = getAllGroupsMethod.Invoke(mixer, null) as System.Collections.IList;
            }

            if (allGroups == null)
            {
                // Fallback: walk masterGroup.children recursively
                allGroups = new System.Collections.Generic.List<object>();
                FlattenGroupTree(masterGroup, allGroups);
            }

            Debug.Log($"[PeakPlunder] Found {allGroups.Count} groups");

            // For each group, find its Attenuation effect and expose the Volume parameter.
            foreach (var g in allGroups)
            {
                if (g == null) continue;
                var nameProp = g.GetType().GetProperty("name");
                string gname = nameProp?.GetValue(g, null) as string;
                if (string.IsNullOrEmpty(gname)) continue;

                string exposedName = MapGroupToExposedParam(gname);
                if (exposedName == null) continue;

                // Effects array
                var effectsField = g.GetType().GetField("effects",
                    BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                if (effectsField == null) continue;
                var effects = effectsField.GetValue(g) as Array;
                if (effects == null || effects.Length == 0) continue;

                var attenuation = effects.GetValue(0); // Attenuation is usually first
                if (attenuation == null) continue;

                // Get parameter GUID for "Volume"
                var getGuidMethod = attenuation.GetType().GetMethod(
                    "GetGUIDForParameter",
                    BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                if (getGuidMethod == null) continue;

                var guid = getGuidMethod.Invoke(attenuation, new object[] { "Volume" });
                if (guid == null) continue;

                // Call mixer.AddExposedParameter(guid, name)
                var addExposedMethod = ctrlType.GetMethod(
                    "AddExposedParameter",
                    BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);

                if (addExposedMethod != null)
                {
                    try
                    {
                        addExposedMethod.Invoke(mixer, new object[] { guid });
                    }
                    catch (Exception e)
                    {
                        Debug.LogWarning($"[PeakPlunder] AddExposedParameter failed for {gname}: {e.Message}");
                        continue;
                    }
                }

                // Call mixer.SetName
                var setNameMethod = ctrlType.GetMethod(
                    "SetNameForExposedParameter",
                    BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                setNameMethod?.Invoke(mixer, new object[] { guid, exposedName });
                Debug.Log($"[PeakPlunder] Exposed '{exposedName}' on group '{gname}'");
            }
        }

        private static void FlattenGroupTree(object root, System.Collections.IList list)
        {
            if (root == null) return;
            list.Add(root);

            var childrenField = root.GetType().GetField("children",
                BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            if (childrenField == null) return;
            var children = childrenField.GetValue(root) as Array;
            if (children == null) return;
            foreach (var c in children) FlattenGroupTree(c, list);
        }

        private static string MapGroupToExposedParam(string groupName)
        {
            switch (groupName)
            {
                case "Master":      return "MasterVolume";
                case "BGM":         return "BgmVolume";
                case "SE":          return "SeVolume";
                case "Voice":       return "VoiceVolume";
                default:            return null;
            }
        }
    }
}
#endif
