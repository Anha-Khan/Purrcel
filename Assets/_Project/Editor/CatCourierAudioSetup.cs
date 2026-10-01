using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using CatCourier.Audio;
using CatCourier.Core;
using UnityEditor;
using UnityEngine;
using UnityEngine.Audio;

namespace CatCourier.EditorTools
{
    /// <summary>
    /// Wires the synthesised audio in Assets/_Project/Audio into the AudioLibrary and
    /// builds the mixer that AudioManager routes through.
    ///
    /// AudioManager degrades safely when these are missing, which is why the silent
    /// build passed every test: nothing asserted that a clip existed. This tool is the
    /// content step, kept separate from the readiness checks that verify it.
    /// </summary>
    public static class CatCourierAudioSetup
    {
        private const string SfxFolder = "Assets/_Project/Audio/Sfx";
        private const string MusicFolder = "Assets/_Project/Audio/Music";
        private const string LibraryPath = "Assets/_Project/Config/AudioLibrary.asset";
        private const string MixerPath = "Assets/_Project/Config/AudioMixer.asset";

        /// <summary>Buses AudioManager routes into. Must match PersistentSystems' field names.</summary>
        private static readonly string[] MixerGroups = { "Music", "Sfx", "Ambient" };

        [MenuItem("Purrcel/Setup/Apply Audio", priority = 9)]
        public static void ApplyAudio()
        {
            Directory.CreateDirectory(Path.GetDirectoryName(LibraryPath)!);
            ConfigureImportSettings();
            var mixer = EnsureMixer(out var groups);
            var library = EnsureLibrary();
            WireLibrary(library);
            WireBootScene(mixer, groups);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            var wired = SfxIdNames().Count(clip => LoadClip(SfxFolder, clip) != null);
            var music = MusicIdNames().Count(clip => LoadClip(MusicFolder, clip) != null);
            Debug.Log($"[CatCourier] Audio wired: {wired}/{SfxIdNames().Count} SFX, " +
                      $"{music}/{MusicIdNames().Count} music, mixer groups {mixer != null}.");
        }

        /// <summary>Batch entry point for the verification scripts.</summary>
        public static void ApplyAudioToLog()
        {
            ApplyAudio();
        }

        /// <summary>
        /// Music is long and loops, so it stays compressed. SFX are short and play with
        /// random pitch, so decompressing them on load avoids a decode stall on the
        /// first coin pickup.
        /// </summary>
        private static void ConfigureImportSettings()
        {
            foreach (var path in FindClips(SfxFolder))
            {
                if (AssetImporter.GetAtPath(path) is not AudioImporter importer)
                {
                    continue;
                }

                var settings = importer.defaultSampleSettings;
                settings.loadType = AudioClipLoadType.DecompressOnLoad;
                settings.compressionFormat = AudioCompressionFormat.Vorbis;
                importer.defaultSampleSettings = settings;
                importer.forceToMono = true;
                importer.SaveAndReimport();
            }

            foreach (var path in FindClips(MusicFolder))
            {
                if (AssetImporter.GetAtPath(path) is not AudioImporter importer)
                {
                    continue;
                }

                var settings = importer.defaultSampleSettings;
                settings.loadType = AudioClipLoadType.Streaming;
                settings.compressionFormat = AudioCompressionFormat.Vorbis;
                importer.defaultSampleSettings = settings;
                importer.forceToMono = true;
                importer.SaveAndReimport();
            }
        }

        private static IEnumerable<string> FindClips(string folder)
        {
            if (!Directory.Exists(folder))
            {
                yield break;
            }

            foreach (var path in Directory.GetFiles(folder, "*.wav").OrderBy(p => p))
            {
                yield return path.Replace('\\', '/');
            }
        }

        /// <summary>
        /// Creates the mixer asset with Music/Sfx/Ambient groups.
        ///
        /// AudioMixer has a default constructor but no CreateGroup, and a bare
        /// AssetDatabase.CreateAsset(mixer) produces a mixer with no groups at all.
        /// AudioMixerController is the type that owns groups and it is internal, so it
        /// has to be reached by reflection. This is the only version that produces a
        /// mixer AudioManager can actually route through.
        /// </summary>
        private static AudioMixer EnsureMixer(out Dictionary<string, AudioMixerGroup> groups)
        {
            groups = new Dictionary<string, AudioMixerGroup>();
            var controllerType = typeof(EditorApplication).Assembly.GetType("UnityEditor.Audio.AudioMixerController");
            if (controllerType == null)
            {
                Debug.LogError("[CatCourier] AudioMixerController not found; mixer groups cannot be created.");
                return AssetDatabase.LoadAssetAtPath<AudioMixer>(MixerPath);
            }

            const BindingFlags staticFlags = BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;
            var create = controllerType.GetMethod("CreateMixerControllerAtPath", staticFlags);
            if (create == null)
            {
                Debug.LogError("[CatCourier] CreateMixerControllerAtPath not found; mixer groups cannot be created.");
                return AssetDatabase.LoadAssetAtPath<AudioMixer>(MixerPath);
            }

            // The returned object is AudioMixerController, which loads as an AudioMixer.
            var controller = create.Invoke(null, new object[] { MixerPath });
            if (controller is not AudioMixer mixer)
            {
                Debug.LogError("[CatCourier] Mixer controller at " + MixerPath + " did not load as an AudioMixer.");
                return null;
            }

            var createNewGroup = controllerType.GetMethod(
                "CreateNewGroup",
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (createNewGroup == null)
            {
                Debug.LogError("[CatCourier] CreateNewGroup not found; only the Master group will exist.");
                return mixer;
            }

            // Take each group from CreateNewGroup's return value. FindMatchingGroups reads
            // a snapshot taken when the mixer was loaded, so a group created in this same
            // call is invisible to it and the reference would come back null.
            foreach (var groupName in MixerGroups)
            {
                var existing = mixer.FindMatchingGroups(groupName);
                if (existing.Length > 0)
                {
                    groups[groupName] = existing[0];
                    continue;
                }

                if (createNewGroup.Invoke(controller, new object[] { groupName, false })
                    is AudioMixerGroup created)
                {
                    groups[groupName] = created;
                }
                else
                {
                    Debug.LogError($"[CatCourier] Mixer group '{groupName}' was not created.");
                }
            }

            EditorUtility.SetDirty(mixer);
            AssetDatabase.SaveAssets();
            return mixer;
        }

        /// <summary>
        /// Points PersistentSystems at the mixer and its three groups. Without this the
        /// mixer asset exists and every source still falls back to the Master group, so
        /// the routing change would be invisible.
        /// </summary>
        private static void WireBootScene(AudioMixer mixer, Dictionary<string, AudioMixerGroup> groups)
        {
            if (mixer == null)
            {
                return;
            }

            var bootPath = "Assets/_Project/Scenes/Boot.unity";
            var scene = UnityEditor.SceneManagement.EditorSceneManager.OpenScene(bootPath, UnityEditor.SceneManagement.OpenSceneMode.Single);
            var systems = UnityEngine.Object.FindObjectOfType<PersistentSystems>();
            if (systems == null)
            {
                return;
            }

            var serialized = new SerializedObject(systems);
            serialized.FindProperty("audioMixer").objectReferenceValue = mixer;
            serialized.FindProperty("musicMixerGroup").objectReferenceValue = Group(groups, "Music");
            serialized.FindProperty("sfxMixerGroup").objectReferenceValue = Group(groups, "Sfx");
            serialized.FindProperty("ambientMixerGroup").objectReferenceValue = Group(groups, "Ambient");
            serialized.ApplyModifiedPropertiesWithoutUndo();
            UnityEditor.SceneManagement.EditorSceneManager.SaveScene(scene);
        }

        private static AudioMixerGroup Group(Dictionary<string, AudioMixerGroup> groups, string name)
        {
            return groups.TryGetValue(name, out var group) ? group : null;
        }

        private static AudioLibrary EnsureLibrary()
        {
            var library = AssetDatabase.LoadAssetAtPath<AudioLibrary>(LibraryPath);
            if (library == null)
            {
                library = ScriptableObject.CreateInstance<AudioLibrary>();
                AssetDatabase.CreateAsset(library, LibraryPath);
            }

            return library;
        }

        private static void WireLibrary(AudioLibrary library)
        {
            var serialized = new SerializedObject(library);

            var sfxProperty = serialized.FindProperty("sfx");
            var sfxNames = SfxIdNames();
            sfxProperty.arraySize = sfxNames.Count;
            for (var i = 0; i < sfxNames.Count; i++)
            {
                var element = sfxProperty.GetArrayElementAtIndex(i);
                element.FindPropertyRelative("id").enumValueIndex = i;
                element.FindPropertyRelative("clip").objectReferenceValue =
                    LoadClip(SfxFolder, sfxNames[i]);
                element.FindPropertyRelative("volume").floatValue = 0.8f;
                element.FindPropertyRelative("pitchVariance").floatValue = 0.12f;
            }

            var musicProperty = serialized.FindProperty("music");
            var musicNames = MusicIdNames();
            musicProperty.arraySize = musicNames.Count;
            for (var i = 0; i < musicNames.Count; i++)
            {
                var element = musicProperty.GetArrayElementAtIndex(i);
                element.FindPropertyRelative("id").enumValueIndex = i;
                element.FindPropertyRelative("clip").objectReferenceValue =
                    LoadClip(MusicFolder, musicNames[i]);
                element.FindPropertyRelative("volume").floatValue = 0.45f;
                element.FindPropertyRelative("pitchVariance").floatValue = 0f;
            }

            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(library);
        }

        private static AudioClip LoadClip(string folder, string name)
        {
            return AssetDatabase.LoadAssetAtPath<AudioClip>($"{folder}/{name}.wav");
        }

        /// <summary>Clip file names must match the enum, in enum order.</summary>
        private static List<string> SfxIdNames() => Enum.GetNames(typeof(SfxId)).ToList();

        private static List<string> MusicIdNames() => Enum.GetNames(typeof(MusicId)).ToList();
    }
}
