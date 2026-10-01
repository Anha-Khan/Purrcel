using System;
using UnityEngine;
using CatCourier.Core;

namespace CatCourier.Audio
{
    [CreateAssetMenu(fileName = "AudioLibrary", menuName = "Purrcel/Audio Library")]
    public sealed class AudioLibrary : ScriptableObject
    {
        [Serializable]
        public sealed class ClipEntry<T> where T : struct
        {
            public T id;
            public AudioClip clip;

            [Range(0f, 1f)] public float volume = 1f;

            [Range(0f, 1f)] public float pitchVariance;
        }

        [SerializeField] private ClipEntry<SfxId>[] sfx = Array.Empty<ClipEntry<SfxId>>();
        [SerializeField] private ClipEntry<MusicId>[] music = Array.Empty<ClipEntry<MusicId>>();

        public AudioClip GetSfx(SfxId id) => FindSfx(id)?.clip;

        public AudioClip GetMusic(MusicId id) => FindMusic(id)?.clip;

        public bool TryGetSfx(SfxId id, out AudioClip clip, out float volume, out float pitchVariance)
        {
            var entry = FindSfx(id);
            clip = entry?.clip;
            volume = entry != null ? Mathf.Clamp01(entry.volume) : 1f;
            pitchVariance = entry != null ? Mathf.Clamp01(entry.pitchVariance) : 0f;
            return clip != null;
        }

        public bool TryGetMusic(MusicId id, out AudioClip clip, out float volume)
        {
            var entry = FindMusic(id);
            clip = entry?.clip;
            volume = entry != null ? Mathf.Clamp01(entry.volume) : 1f;
            return clip != null;
        }

        private ClipEntry<SfxId> FindSfx(SfxId id)
        {
            if (sfx == null)
            {
                return null;
            }

            foreach (var entry in sfx)
            {
                if (entry != null && entry.id == id)
                {
                    return entry;
                }
            }

            return null;
        }

        private ClipEntry<MusicId> FindMusic(MusicId id)
        {
            if (music == null)
            {
                return null;
            }

            foreach (var entry in music)
            {
                if (entry != null && entry.id == id)
                {
                    return entry;
                }
            }

            return null;
        }
    }
}
