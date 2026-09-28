using System.Collections;
using CatCourier.Audio;
using CatCourier.Core;
using CatCourier.Monetization;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace CatCourier.Tests.PlayMode
{
    /// <summary>
    /// Boot / PersistentSystems: one initialization pass, declaration-order manager
    /// creation, and no duplication when a second boot object appears.
    ///
    /// These tests deliberately hand the boot chain a pre-existing temp-path
    /// SaveSystem, so boot's own SaveSystem slot self-destructs in Awake without
    /// ever reading Application.persistentDataPath. That keeps the "no test writes
    /// the real save path" rule absolute, and doubles as the single-initialization
    /// assertion.
    /// </summary>
    public sealed class BootInitializationTests : Day6PlayModeTestBase
    {
        [UnityTest]
        public IEnumerator Boot_ClaimsManagersInDeclaredOrderAndReusesExistingSaveSystem()
        {
            var save = CreateTempSaveSystem();
            var sceneBefore = SceneManager.GetActiveScene().name;

            var bootHost = Track(new GameObject("Day6PersistentSystems"));
            bootHost.AddComponent<PersistentSystems>();

            Assert.That(
                bootHost.transform.childCount,
                Is.EqualTo(DeclaredManagerOrder.Length),
                "PersistentSystems.Awake must create exactly one child per manager.");
            for (var slot = 0; slot < DeclaredManagerOrder.Length; slot++)
            {
                Assert.That(
                    bootHost.transform.GetChild(slot).name,
                    Is.EqualTo(DeclaredManagerOrder[slot]),
                    $"Manager slot {slot} must be '{DeclaredManagerOrder[slot]}' in PersistentSystems declaration order.");
            }

            Assert.That(
                SaveSystem.Instance,
                Is.SameAs(save),
                "Boot must reuse the existing SaveSystem instead of standing up a second one.");
            Assert.That(GameManager.Instance, Is.Not.Null, "Boot must claim GameManager.");
            Assert.That(SceneLoader.Instance, Is.Not.Null, "Boot must claim SceneLoader.");
            Assert.That(AudioManager.Instance, Is.Not.Null, "Boot must claim AudioManager.");
            Assert.That(RevenueCatManager.Instance, Is.Not.Null, "Boot must claim RevenueCatManager.");
            Assert.That(EntitlementChecker.Instance, Is.Not.Null, "Boot must claim EntitlementChecker.");
            Assert.That(AdManager.Instance, Is.Not.Null, "Boot must claim AdManager.");

            var createdByBoot = bootHost.transform.Find("AudioManager");
            Assert.That(createdByBoot, Is.Not.Null);
            Assert.That(
                createdByBoot.GetComponent<AudioManager>(),
                Is.SameAs(AudioManager.Instance),
                "Each child must own the singleton it created.");

            // PersistentSystems.Start drives a real Hub scene load, which would
            // destroy this test scene. Stub the loader so only the initialization
            // half of boot runs, then prove no scene actually changed. The manager
            // it booted has no outstanding load, so its failure handler is inert.
            StubSceneLoader(SceneLoader.Instance);
            yield return null;
            yield return null;
            yield return null;

            Assert.That(
                RevenueCatManager.Instance.State,
                Is.EqualTo(RevenueCatState.Ready),
                "Boot must initialize monetization.");
#if UNITY_EDITOR
            Assert.That(
                RevenueCatManager.Instance.IsFakeBackend,
                Is.True,
                "Editor boot must stay on the fake monetization backend.");
#endif
            Assert.That(
                EntitlementChecker.Instance.IsPremium,
                Is.False,
                "A freshly initialized backend grants no entitlement.");
            Assert.That(
                SceneManager.GetActiveScene().name,
                Is.EqualTo(sceneBefore),
                "Boot must not have loaded a real scene during these tests.");
            Assert.That(GameManager.Instance.State, Is.EqualTo(GameState.Hub));
        }

        [UnityTest]
        public IEnumerator SecondPersistentSystems_AddsNoDuplicateManagers()
        {
            var save = CreateTempSaveSystem();

            var firstHost = Track(new GameObject("Day6PersistentSystemsPrimary"));
            firstHost.AddComponent<PersistentSystems>();
            var firstSingletons = SnapshotManagerSingletons();
            var firstChildCount = firstHost.transform.childCount;
            Assert.That(firstChildCount, Is.EqualTo(DeclaredManagerOrder.Length));

            var secondHost = Track(new GameObject("Day6PersistentSystemsDuplicate"));
            secondHost.AddComponent<PersistentSystems>();

            Assert.That(
                SnapshotManagerSingletons(),
                Is.EqualTo(firstSingletons),
                "A second boot object must not replace or duplicate any manager singleton.");
            Assert.That(
                firstHost.transform.childCount,
                Is.EqualTo(firstChildCount),
                "A second boot object must not append children to the first one.");
            Assert.That(
                secondHost.transform.childCount,
                Is.LessThanOrEqualTo(1),
                "A second boot object may only create the self-destroying SaveSystem slot, never a live manager.");
            Assert.That(SaveSystem.Instance, Is.SameAs(save));

            yield return null;

            Assert.That(
                SnapshotManagerSingletons(),
                Is.EqualTo(firstSingletons),
                "Manager ownership must survive the first frame after a duplicate boot object.");
        }

        [UnityTest]
        public IEnumerator Boot_TargetsSixtyFramesAndVsyncOff()
        {
            CreateTempSaveSystem();
            var previousTarget = Application.targetFrameRate;
            var previousVSync = QualitySettings.vSyncCount;
            try
            {
                Track(new GameObject("Day6PersistentSystemsFrameRate")).AddComponent<PersistentSystems>();

                Assert.That(Application.targetFrameRate, Is.EqualTo(60), "Boot must pin the 60 FPS target.");
                Assert.That(QualitySettings.vSyncCount, Is.Zero, "Boot must disable vSync so the frame cap is authoritative.");
                yield return null;
            }
            finally
            {
                Application.targetFrameRate = previousTarget;
                QualitySettings.vSyncCount = previousVSync;
            }
        }
    }
}
