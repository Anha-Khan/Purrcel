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

    [CreateAssetMenu(fileName = "RevenueCatConfig", menuName = "Cat Courier/RevenueCat Config")]
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

        public string GetPublicKey()
        {
#if UNITY_IOS
            return ApplePublicKey;
#elif UNITY_ANDROID
            return GooglePublicKey;
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

        public bool HasRequiredPublicKey()
        {
#if UNITY_IOS || UNITY_ANDROID
            return !string.IsNullOrWhiteSpace(GetPublicKey());
#else
            return true;
#endif
        }

        private string SelectKey(string developmentKey, string productionKey)
        {
            if (environment == RevenueCatEnvironment.Production)
            {
                return productionKey;
            }

#if DEVELOPMENT_BUILD || UNITY_EDITOR
            // Test Store works with the real RevenueCat SDK on a device, without store products.
            // It must never leak into a non-development build: the SDK rejects Test Store keys there.
            return !string.IsNullOrWhiteSpace(developmentTestStorePublicKey)
                ? developmentTestStorePublicKey
                : developmentKey;
#else
            return developmentKey;
#endif
        }
    }
}
