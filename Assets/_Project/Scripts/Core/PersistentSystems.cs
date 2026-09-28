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
            EnsureManager<Progression.CatBreedManager>(transform);
            EnsureManager<Progression.UpgradeManager>(transform);
            EnsureManager<Monetization.AdManager>(transform);
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
            revenueCat?.Initialize(revenueCatConfig, useFakeBackend);
            SceneLoader.Instance.OnSceneLoadFinished += HandleSceneLoadFinished;
            yield return null;
            SceneLoader.Instance?.Load(SceneNames.Hub);
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
