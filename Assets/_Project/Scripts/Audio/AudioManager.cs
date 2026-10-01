using UnityEngine;
using UnityEngine.Audio;
using CatCourier.Core;

namespace CatCourier.Audio
{
    public enum MusicPlaybackState
    {
        Stopped,
        FadingIn,
        Playing
    }

    public sealed class AudioManager : MonoBehaviour
    {
        public static AudioManager Instance { get; private set; }

        [Header("Mixer groups (optional)")]
        [SerializeField] private AudioMixer mixer;
        [SerializeField] private AudioMixerGroup musicGroup;
        [SerializeField] private AudioMixerGroup sfxGroup;
        [SerializeField] private AudioMixerGroup ambientGroup;

        [Header("Library")]
        [SerializeField] private AudioLibrary library;

        [Header("Playback")]
        [SerializeField, Min(1)] private int sfxVoiceCount = 6;
        [SerializeField] private bool randomizePitch = true;
        [SerializeField, Range(0f, 1f)] private float pausedMusicVolume = 0.3f;

        public AudioLibrary Library => library;
        public MusicId? CurrentMusic { get; private set; }
        public MusicPlaybackState MusicState { get; private set; } = MusicPlaybackState.Stopped;
        public bool IsMuted { get; private set; }

        private AudioSource[] sfxVoices;
        private int sfxCursor;
        private AudioSource musicSlotA;
        private AudioSource musicSlotB;
        private AudioSource musicTo;
        private AudioSource musicFrom;
        private AudioSource ambientSource;
        private float musicTargetVolume = 1f;
        private float incomingVolume;
        private float outgoingVolume;
        private float outgoingTargetVolume;
        private float pausedMultiplier = 1f;
        private GameManager gameManager;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
            PersistIfRoot();
            BuildSources();
        }

        private void PersistIfRoot()
        {
            // Managers are parented under PersistentSystems, which is already DontDestroyOnLoad.
            if (transform.parent == null)
            {
                DontDestroyOnLoad(gameObject);
            }
        }

        public void Configure(AudioLibrary value)
        {
            library = value;
        }

        /// <summary>
        /// Applies an authored mixer after the sources already exist. Safe to call once during boot.
        /// </summary>
        public void ConfigureMixer(AudioMixer value, AudioMixerGroup music, AudioMixerGroup sfx, AudioMixerGroup ambient)
        {
            mixer = value;
            musicGroup = music;
            sfxGroup = sfx;
            ambientGroup = ambient;
            if (mixer == null)
            {
                return;
            }

            AssignGroup(musicSlotA, musicGroup);
            AssignGroup(musicSlotB, musicGroup);
            AssignGroup(ambientSource, ambientGroup);
            if (sfxVoices == null)
            {
                return;
            }

            foreach (var voice in sfxVoices)
            {
                AssignGroup(voice, sfxGroup);
            }
        }

        private static void AssignGroup(AudioSource source, AudioMixerGroup group)
        {
            if (source != null && group != null)
            {
                source.outputAudioMixerGroup = group;
            }
        }

        public void SetMuted(bool value)
        {
            IsMuted = value;
            if (ambientSource != null && ambientSource.isPlaying)
            {
                ambientSource.volume = AmbientVolume();
            }

            ApplyMusicVolumes();
        }

        public void PlaySfx(SfxId id) => PlaySfx(id, 1f);

        /// <summary>Plays a one-shot with an extra volume scale, e.g. softer for a light landing.</summary>
        public void PlaySfx(SfxId id, float volumeScale)
        {
            if (IsMuted || library == null)
            {
                return;
            }

            if (!library.TryGetSfx(id, out var clip, out var volume, out var pitchVariance))
            {
                return;
            }

            var voice = NextVoice();
            if (voice == null)
            {
                return;
            }

            voice.pitch = ResolvePitch(pitchVariance);
            voice.PlayOneShot(clip, volume * Mathf.Clamp01(volumeScale));
        }

        public void PlayMusic(MusicId id)
        {
            if (IsMuted)
            {
                return;
            }

            if (library == null || !library.TryGetMusic(id, out var clip, out var volume))
            {
                CurrentMusic = null;
                StopMusicSources();
                return;
            }

            if (CurrentMusic == id && musicTo != null && musicTo.clip == clip && musicTo.isPlaying)
            {
                return;
            }

            CurrentMusic = id;
            musicFrom = musicTo;
            outgoingTargetVolume = musicFrom != null && musicFrom.isPlaying ? musicTargetVolume : 0f;
            musicTargetVolume = volume;
            musicTo = OtherSlot(musicTo);
            musicTo.loop = true;
            musicTo.clip = clip;
            musicTo.time = 0f;
            musicTo.volume = 0f;
            musicTo.Play();
            incomingVolume = 0f;
            outgoingVolume = musicFrom != null && musicFrom.isPlaying ? 1f : 0f;
            MusicState = MusicPlaybackState.FadingIn;
        }

        public void PlayAmbient(SfxId id, float volume = 1f)
        {
            if (IsMuted || ambientSource == null || library == null)
            {
                return;
            }

            if (!library.TryGetSfx(id, out var clip, out _, out _))
            {
                return;
            }

            ambientSource.clip = clip;
            ambientSource.loop = true;
            ambientSource.volume = Mathf.Clamp01(volume) * AmbientVolume();
            ambientSource.Play();
        }

        private void Update()
        {
            TrackGameManager();
            UpdateMusicCrossfade(Time.unscaledDeltaTime);
        }

        private void BuildSources()
        {
            musicSlotA = CreateSource("MusicSlotA", musicGroup);
            musicSlotB = CreateSource("MusicSlotB", musicGroup);
            musicTo = musicSlotA;
            musicFrom = null;
            ambientSource = CreateSource("Ambient", ambientGroup);

            var voices = Mathf.Clamp(sfxVoiceCount, 1, 16);
            sfxVoices = new AudioSource[voices];
            for (var index = 0; index < voices; index++)
            {
                sfxVoices[index] = CreateSource($"SfxVoice{index}", sfxGroup);
            }
        }

        private AudioSource CreateSource(string sourceName, AudioMixerGroup group)
        {
            var host = new GameObject(sourceName);
            host.transform.SetParent(transform, false);
            var source = host.AddComponent<AudioSource>();
            source.playOnAwake = false;
            source.loop = false;
            source.spatialBlend = 0f;
            if (mixer != null)
            {
                source.outputAudioMixerGroup = group;
            }

            return source;
        }

        private AudioSource OtherSlot(AudioSource source)
        {
            return ReferenceEquals(source, musicSlotB) ? musicSlotA : musicSlotB;
        }

        private AudioSource NextVoice()
        {
            if (sfxVoices == null || sfxVoices.Length == 0)
            {
                return null;
            }

            for (var offset = 0; offset < sfxVoices.Length; offset++)
            {
                sfxCursor = (sfxCursor + 1) % sfxVoices.Length;
                var voice = sfxVoices[sfxCursor];
                if (voice != null && !voice.isPlaying)
                {
                    return voice;
                }
            }

            return sfxVoices[sfxCursor];
        }

        private void UpdateMusicCrossfade(float deltaTime)
        {
            if (musicTo == null)
            {
                return;
            }

            var step = deltaTime / Mathf.Max(0.01f, Constants.MUSIC_CROSSFADE_TIME);
            if (MusicState == MusicPlaybackState.FadingIn)
            {
                incomingVolume = Mathf.MoveTowards(incomingVolume, 1f, step);
                outgoingVolume = Mathf.MoveTowards(outgoingVolume, 0f, step);
                ApplyMusicVolumes();
                if (incomingVolume >= 1f)
                {
                    ReleaseSlot(musicFrom);
                    MusicState = MusicPlaybackState.Playing;
                }

                return;
            }

            musicTo.volume = musicTargetVolume * pausedMultiplier * (IsMuted ? 0f : 1f);
        }

        private void ApplyMusicVolumes()
        {
            var muteMultiplier = IsMuted ? 0f : 1f;
            if (musicTo != null)
            {
                musicTo.volume = incomingVolume * musicTargetVolume * pausedMultiplier * muteMultiplier;
            }

            if (musicFrom != null)
            {
                musicFrom.volume = outgoingVolume * outgoingTargetVolume * pausedMultiplier * muteMultiplier;
            }
        }

        private static void ReleaseSlot(AudioSource source)
        {
            if (source == null)
            {
                return;
            }

            source.Stop();
            source.clip = null;
        }

        private void StopMusicSources()
        {
            incomingVolume = 0f;
            outgoingVolume = 0f;
            outgoingTargetVolume = 0f;
            ReleaseSlot(musicSlotA);
            ReleaseSlot(musicSlotB);
            musicTo = musicSlotA;
            musicFrom = null;
            MusicState = MusicPlaybackState.Stopped;
        }

        // Ambient rides the mute state only. The per-clip volume from the library is
        // already applied by PlayAmbient, so this is a gate, not a second volume.
        private float AmbientVolume() => IsMuted ? 0f : 1f;

        private float ResolvePitch(float pitchVariance)
        {
            if (!randomizePitch || pitchVariance <= 0f)
            {
                return 1f;
            }

            return 1f + Random.Range(-pitchVariance, pitchVariance);
        }

        private void TrackGameManager()
        {
            if (gameManager != null)
            {
                return;
            }

            gameManager = GameManager.Instance;
            if (gameManager == null)
            {
                return;
            }

            gameManager.OnStateChanged -= HandleGameStateChanged;
            gameManager.OnStateChanged += HandleGameStateChanged;
        }

        private void HandleGameStateChanged(GameState state)
        {
            pausedMultiplier = state == GameState.Paused ? pausedMusicVolume : 1f;
        }

        private void OnDestroy()
        {
            if (gameManager != null)
            {
                gameManager.OnStateChanged -= HandleGameStateChanged;
            }

            if (Instance == this)
            {
                Instance = null;
            }
        }
    }
}
