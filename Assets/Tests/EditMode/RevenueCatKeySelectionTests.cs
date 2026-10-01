using System.Reflection;
using CatCourier.Monetization;
using NUnit.Framework;
using UnityEngine;

namespace CatCourier.Tests
{
    /// <summary>
    /// Key selection for the Android demo build.
    ///
    /// These lock down which credential the SDK is handed, and in particular the rule that
    /// a Test Store key is only ever selected in a development build — the SDK rejects it
    /// in production, so a leak is a broken build, not a cosmetic one.
    ///
    /// A correction worth recording: I first "fixed" a bug here that did not affect the
    /// demo APK. BuildAndroidNextGenDemo routes through BuildAndroid, which passes
    /// BuildOptions.Development, so DEVELOPMENT_BUILD was already defined and SelectKey
    /// was already reaching the Test Store branch. My change preferred the Test Store key
    /// on environment alone, which would have sent a sandbox credential to a RELEASE
    /// build. The tests below are what should have existed before either edit.
    /// </summary>
    public sealed class RevenueCatKeySelectionTests
    {
        private const string TestStoreKey = "test_sandbox_placeholder";
        private const string DevGoogleKey = "AIza_dev_google_placeholder";
        private const string ProdGoogleKey = "AIza_prod_google_placeholder";

        private static RevenueCatConfig Config(
            RevenueCatEnvironment environment,
            string testStore = null,
            string devGoogle = null,
            string prodGoogle = null,
            RevenueCatBackendSelection backend = RevenueCatBackendSelection.Real)
        {
            var config = ScriptableObject.CreateInstance<RevenueCatConfig>();
            Set(config, "environment", environment);
            // backend matters: IsAndroidDemoReady requires Real, and the field default is
            // Fake. Leaving it unset made a correctly-configured config report not-ready.
            Set(config, "backend", backend);
            Set(config, "developmentTestStorePublicKey", testStore);
            Set(config, "developmentGooglePublicKey", devGoogle);
            Set(config, "productionGooglePublicKey", prodGoogle);
            return config;
        }

        private static void Set(object target, string field, object value)
        {
            target.GetType()
                .GetField(field, BindingFlags.Instance | BindingFlags.NonPublic)
                .SetValue(target, value);
        }

        [Test]
        public void Sandbox_UsesTheTestStoreKeyWhenThatIsTheOnlyKey()
        {
            // The demo APK's real configuration: Test Store key pasted, Google keys blank.
            // These run in Edit Mode, where UNITY_EDITOR satisfies the same condition the
            // demo APK satisfies via DEVELOPMENT_BUILD.
            var config = Config(RevenueCatEnvironment.DevelopmentSandbox, testStore: TestStoreKey);

            Assert.That(config.ResolveAndroidKey(), Is.EqualTo(TestStoreKey),
                "A Test Store key is a separate sandbox credential, not a Google key. The demo " +
                "build must authenticate with it or the SDK is handed an empty string.");
        }

        [Test]
        public void Sandbox_PrefersTestStoreOverDevGoogle()
        {
            var config = Config(RevenueCatEnvironment.DevelopmentSandbox,
                testStore: TestStoreKey, devGoogle: DevGoogleKey);

            Assert.That(config.ResolveAndroidKey(), Is.EqualTo(TestStoreKey));
        }

        [Test]
        public void Sandbox_FallsBackToDevGoogleWhenNoTestStoreKey()
        {
            var config = Config(RevenueCatEnvironment.DevelopmentSandbox, devGoogle: DevGoogleKey);

            Assert.That(config.ResolveAndroidKey(), Is.EqualTo(DevGoogleKey),
                "Without a Test Store key a plain development build must still resolve its Google key.");
        }

        [Test]
        public void Production_NeverUsesTheTestStoreKey()
        {
            // The Test Store key is a sandbox credential. Leaking it into a production build
            // would authenticate real users against a sandbox project.
            var config = Config(RevenueCatEnvironment.Production,
                testStore: TestStoreKey, devGoogle: DevGoogleKey, prodGoogle: ProdGoogleKey);

            Assert.That(config.ResolveAndroidKey(), Is.EqualTo(ProdGoogleKey),
                "A Test Store key must never be used when the environment is Production.");
        }

        [Test]
        public void Production_UsesTheProductionGoogleKey()
        {
            var config = Config(RevenueCatEnvironment.Production, prodGoogle: ProdGoogleKey);

            Assert.That(config.ResolveAndroidKey(), Is.EqualTo(ProdGoogleKey));
        }

        [Test]
        public void IsAndroidDemoReady_AgreesWithTheKeyThatWillActuallyBeUsed()
        {
            // The failure mode was a green readiness check over a build with no usable key.
            // Readiness and resolution must not disagree.
            var ready = Config(RevenueCatEnvironment.DevelopmentSandbox, testStore: TestStoreKey);
            Assert.That(ready.IsAndroidDemoReady, Is.True);
            Assert.That(ready.ResolveAndroidKey(), Is.Not.Empty,
                "IsAndroidDemoReady says ready but no key would be sent to the SDK.");

            var notReady = Config(RevenueCatEnvironment.DevelopmentSandbox);
            Assert.That(notReady.IsAndroidDemoReady, Is.False);
            Assert.That(notReady.ResolveAndroidKey(), Is.Empty,
                "IsAndroidDemoReady says not ready yet a key would be sent to the SDK.");
        }

        [Test]
        public void Sandbox_WithNoKeysAtAll_ResolvesEmptyNotNull()
        {
            var config = Config(RevenueCatEnvironment.DevelopmentSandbox);

            // Is.Empty, not Is.Null: an unconfigured key used to come back null, which is a
            // latent NullReferenceException for any caller doing key.Length or similar.
            Assert.That(config.ResolveAndroidKey(), Is.Not.Null);
            Assert.That(config.ResolveAndroidKey(), Is.EqualTo(string.Empty));
        }

        [Test]
        public void GooglePublicKey_IsNeverNullEvenWhenUnconfigured()
        {
            var config = Config(RevenueCatEnvironment.DevelopmentSandbox);

            Assert.That(config.GooglePublicKey, Is.Not.Null);
            Assert.That(config.ApplePublicKey, Is.Not.Null);
        }
    }
}
