using UnityEngine;

namespace CatCourier.Core
{
    public sealed class PersistentSystems : MonoBehaviour
    {
        private const int TargetFrameRate = 60;

        [SerializeField] private bool useFakeMonetizationInEditor = true;

        [SerializeField] private Monetization.RevenueCatConfig revenueCatConfig;

        [Header("Content (authored, optional)")]
        [SerializeField] private Generation.ChunkCatalog chunkCatalog;
        [SerializeField] private Progression.CatBreedConfig[] generatedCatBreeds;
        [SerializeField] private CatCourier.Art.GeneratedArtCatalog generatedArtCatalog;

        [Header("UI (optional)")]
        [Tooltip("Optional override. When empty, the bundled PurrcelUI font is loaded from Resources.")]
        [SerializeField] private Font uiFont;

        [Header("Audio (authored, optional)")]
        [SerializeField] private Audio.AudioLibrary audioLibrary;
        [SerializeField] private UnityEngine.Audio.AudioMixer audioMixer;
        [SerializeField] private UnityEngine.Audio.AudioMixerGroup musicMixerGroup;
        [SerializeField] private UnityEngine.Audio.AudioMixerGroup sfxMixerGroup;
        [SerializeField] private UnityEngine.Audio.AudioMixerGroup ambientMixerGroup;

        private void Awake()
        {
            Application.targetFrameRate = TargetFrameRate;
            QualitySettings.vSyncCount = 0;
            DontDestroyOnLoad(gameObject);
            EnsureManager<SaveSystem>(transform);
            EnsureManager<GameManager>(transform);
            EnsureManager<SceneLoader>(transform);
            EnsureManager<Audio.AudioManager>(transform);
            EnsureManager<Monetization.RevenueCatManager>(transform);
            EnsureManager<Monetization.EntitlementChecker>(transform);
            var breedManager = EnsureManager<Progression.CatBreedManager>(transform);
            if (generatedCatBreeds != null && generatedCatBreeds.Length > 0)
                breedManager.SetConfigs(generatedCatBreeds);
            generatedArtCatalog?.Activate();
            EnsureManager<Progression.UpgradeManager>(transform);
            EnsureManager<Monetization.AdManager>(transform);
            // Namespace is UI, resolved from the enclosing CatCourier namespace the same way
            // UI.PaywallPresenter is below. The font lives in Resources so no scene
            // reference is needed and a missing asset degrades to Arial rather than
            // breaking every surface.
            // Plus Jakarta Sans (SIL OFL 1.1) under Assets/_Project/Resources. Bundled rather
            // than wired into a scene so a fresh clone styles correctly with no setup, and
            // loaded by name so a missing file degrades to IMGUI's built-in Arial. The name
            // lives in one place so a rename cannot leave this loader pointing at nothing.
            UI.UiTheme.SetFont(uiFont != null ? uiFont : Resources.Load<Font>(UI.UiTheme.BundledFontName));
            ApplyAudioConfiguration();
            ApplyContentReferences();
        }

        private void ApplyContentReferences()
        {
            if (chunkCatalog == null)
            {
                var loaded = Resources.FindObjectsOfTypeAll<Generation.ChunkCatalog>();
                if (loaded.Length > 0)
                {
                    chunkCatalog = loaded[0];
                }
            }

            UI.PaywallPresenter.CatalogReference = chunkCatalog;
        }

        private void ApplyAudioConfiguration()
        {
            var audio = Audio.AudioManager.Instance;
            if (audio == null)
            {
                return;
            }

            audio.Configure(audioLibrary);
            audio.ConfigureMixer(audioMixer, musicMixerGroup, sfxMixerGroup, ambientMixerGroup);
        }

        private System.Collections.IEnumerator Start()
        {
            var revenueCat = Monetization.RevenueCatManager.Instance;
            var useFakeBackend = revenueCatConfig != null
                ? revenueCatConfig.ShouldUseFakeBackend()
                : useFakeMonetizationInEditor;
            WarnOnMissingRevenueCatConfig(useFakeBackend);
            // Start is a coroutine and an unhandled exception in one is swallowed: the
            // rest of this method never runs, so Load(Hub) below is never reached and the
            // player stares at an empty Boot scene with no error anywhere. That is exactly
            // what the first real-backend device build did. A purchase SDK failing to
            // configure must never stop the game from starting.
            try
            {
                revenueCat?.Initialize(revenueCatConfig, useFakeBackend);
            }
            catch (System.Exception exception)
            {
                Debug.LogError(
                    "Purrcel: RevenueCat failed to initialise, so purchases are unavailable this " +
                    "session. The game will still start. Cause: " + exception);
            }

            SceneLoader.Instance.OnSceneLoadFinished += HandleSceneLoadFinished;
            yield return null;
            SceneLoader.Instance?.Load(SceneNames.Hub);
        }

        /// <summary>
        /// The config asset is gitignored, so a fresh clone has none and a development
        /// build silently runs on fake purchases. That reads as a working store in a
        /// demo, so say so loudly rather than failing quietly.
        /// </summary>
        private void WarnOnMissingRevenueCatConfig(bool useFakeBackend)
        {
            if (revenueCatConfig != null || !useFakeBackend || Application.isEditor)
            {
                return;
            }

            Debug.LogError(
                "Purrcel: no RevenueCat config asset is assigned, so this build uses FAKE purchases. " +
                "Run Purrcel > Setup > Create Local RevenueCat Config, then " +
                "Purrcel > Setup > Use Real RevenueCat for Next Gen Demo, and paste your " +
                "Test Store public key. A release build is unaffected: it always uses the real backend.");
        }

        private static void HandleSceneLoadFinished(string sceneName)
        {
            if (sceneName == SceneNames.Hub)
            {
                var revenueCat = Monetization.RevenueCatManager.Instance;
                if (revenueCat != null && !revenueCat.IsFakeBackend)
                {
                    revenueCat.RefreshCustomerInfo(_ => { });
                }
            }
        }

        private void OnDestroy()
        {
            if (SceneLoader.Instance != null)
            {
                SceneLoader.Instance.OnSceneLoadFinished -= HandleSceneLoadFinished;
            }
        }

        private static T EnsureManager<T>(Transform parent) where T : Component
        {
            var existing = FindObjectOfType<T>();
            if (existing != null)
            {
                return existing;
            }

            var child = new GameObject(typeof(T).Name);
            child.transform.SetParent(parent);
            return child.AddComponent<T>();
        }
    }
}
