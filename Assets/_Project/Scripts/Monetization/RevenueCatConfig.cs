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
        [SerializeField] private string productionApplePublicKey;
        [SerializeField] private string productionGooglePublicKey;

        public RevenueCatEnvironment Environment => environment;
        public RevenueCatBackendSelection Backend => backend;
        public string ApplePublicKey => SelectKey(developmentApplePublicKey, productionApplePublicKey);
        public string GooglePublicKey => SelectKey(developmentGooglePublicKey, productionGooglePublicKey);
        public bool IsProduction => environment == RevenueCatEnvironment.Production;

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
#if UNITY_EDITOR || DEVELOPMENT_BUILD
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
            return environment == RevenueCatEnvironment.Production ? productionKey : developmentKey;
        }
    }
}
