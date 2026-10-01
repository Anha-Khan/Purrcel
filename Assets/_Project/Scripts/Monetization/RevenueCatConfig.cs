using UnityEngine;

namespace CatCourier.Monetization
{
    public enum RevenueCatEnvironment
    {
        DevelopmentSandbox,
        Production
    }

    public enum RevenueCatBackendSelection
    {
        Real,
        Fake
    }

    [CreateAssetMenu(fileName = "RevenueCatConfig", menuName = "Purrcel/RevenueCat Config")]
    public sealed class RevenueCatConfig : ScriptableObject
    {
        [Header("Local development only. Keep production key selection explicit.")]
        [SerializeField] private RevenueCatEnvironment environment = RevenueCatEnvironment.DevelopmentSandbox;
        [SerializeField] private RevenueCatBackendSelection backend = RevenueCatBackendSelection.Fake;
        [SerializeField] private string developmentApplePublicKey;
        [SerializeField] private string developmentGooglePublicKey;
        [Tooltip("RevenueCat Test Store key for internal Android/iOS development builds. Never use it in a release build.")]
        [SerializeField] private string developmentTestStorePublicKey;
        [SerializeField] private string productionApplePublicKey;
        [SerializeField] private string productionGooglePublicKey;

        public RevenueCatEnvironment Environment => environment;
        public RevenueCatBackendSelection Backend => backend;
        public string ApplePublicKey => SelectKey(developmentApplePublicKey, productionApplePublicKey);
        public string GooglePublicKey => SelectKey(developmentGooglePublicKey, productionGooglePublicKey);
        public bool IsProduction => environment == RevenueCatEnvironment.Production;
        public bool IsAndroidDemoReady => environment == RevenueCatEnvironment.DevelopmentSandbox &&
                                          backend == RevenueCatBackendSelection.Real &&
                                          (!string.IsNullOrWhiteSpace(developmentTestStorePublicKey) ||
                                           !string.IsNullOrWhiteSpace(developmentGooglePublicKey));

        /// <summary>
        /// Picks the Android public key for the current configuration.
        ///
        /// A RevenueCat Test Store key is a SEPARATE sandbox credential from the Play Store
        /// key, and the SDK REJECTS it in a non-development build. So it may only be
        /// selected when DEVELOPMENT_BUILD is defined — which the Next Gen demo APK is,
        /// because BuildAndroidNextGenDemo routes through BuildAndroid and passes
        /// BuildOptions.Development.
        ///
        /// The guard is load-bearing. An earlier version of this preferred the Test Store key
        /// on environment alone, which sent a sandbox credential to production builds.
        /// UNITY_EDITOR is in the condition so the selection stays testable in Edit Mode.
        /// </summary>
        public string ResolveAndroidKey()
        {
#if DEVELOPMENT_BUILD || UNITY_EDITOR
            if (!IsProduction && !string.IsNullOrWhiteSpace(developmentTestStorePublicKey))
            {
                return developmentTestStorePublicKey;
            }
#endif
            return GooglePublicKey;
        }

        public string GetPublicKey()
        {
#if UNITY_IOS
            return ApplePublicKey;
#elif UNITY_ANDROID
            return ResolveAndroidKey();
#else
            return string.Empty;
#endif
        }

        public bool ShouldUseFakeBackend()
        {
#if UNITY_EDITOR
            // RevenueCat's Unity SDK cannot run in the Editor; keep Play mode usable
            // even while the device demo is configured for the real backend.
            return true;
#elif DEVELOPMENT_BUILD
            return backend == RevenueCatBackendSelection.Fake;
#else
            return false;
#endif
        }

        private string SelectKey(string developmentKey, string productionKey)
        {
            if (environment == RevenueCatEnvironment.Production)
            {
                return productionKey ?? string.Empty;
            }

#if DEVELOPMENT_BUILD || UNITY_EDITOR
            // Test Store works with the real RevenueCat SDK on a device, without store products.
            // It must never leak into a non-development build: the SDK rejects Test Store keys there.
            return !string.IsNullOrWhiteSpace(developmentTestStorePublicKey)
                ? developmentTestStorePublicKey
                : developmentKey ?? string.Empty;
#else
            return developmentKey ?? string.Empty;
#endif
        }
    }
}
