using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using CatCourier.Audio;
using CatCourier.Coins;
using CatCourier.Core;
using CatCourier.Monetization;
using CatCourier.Player;
using CatCourier.Progression;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace CatCourier.Tests
{
    public sealed class Day5SystemsTests
    {
        private GameObject saveHost;
        private GameObject entitlementHost;
        private string directory;

        [SetUp]
        public void SetUp()
        {
            directory = Path.Combine(Path.GetTempPath(), "CatCourierDay5Tests", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(directory);
            saveHost = new GameObject("Day5SaveSystem");
            saveHost.SetActive(false);
            var save = saveHost.AddComponent<SaveSystem>();
            save.ConfigurePaths(directory);
            save.Load();
            SetStaticInstance(typeof(SaveSystem), save);
        }

        [TearDown]
        public void TearDown()
        {
            if (entitlementHost != null)
            {
                Object.DestroyImmediate(entitlementHost);
            }

            SetStaticInstance(typeof(SaveSystem), null);
            SetStaticInstance(typeof(EntitlementChecker), null);
            SetStaticInstance(typeof(GameManager), null);
            SetStaticInstance(typeof(AdManager), null);
            Time.timeScale = 1f;
            if (saveHost != null)
            {
                Object.DestroyImmediate(saveHost);
            }

            if (Directory.Exists(directory))
            {
                Directory.Delete(directory, true);
            }
        }

        [Test]
        public void Upgrades_UseExactCostsAndAtomicPurchase()
        {
            var host = new GameObject("UpgradeManagerDay5Test");
            try
            {
                var save = SaveSystem.Instance;
                save.Data.totalCoins = 1000;
                save.Save();
                var upgrades = host.AddComponent<UpgradeManager>();
                upgrades.SetConfigs(Array.Empty<UpgradeConfig>());

                Assert.That(upgrades.GetNextCost(RunLoadoutService.SprintSpeedId), Is.EqualTo(50));
                Assert.That(upgrades.GetNextCost(RunLoadoutService.JumpHeightId), Is.EqualTo(60));
                Assert.That(upgrades.GetNextCost(RunLoadoutService.DoubleJumpId), Is.EqualTo(200));
                Assert.That(upgrades.GetNextCost(RunLoadoutService.CoinMagnetId), Is.EqualTo(80));
                Assert.That(upgrades.GetNextCost(RunLoadoutService.CoinMultiplierId), Is.EqualTo(100));
                Assert.That(upgrades.GetNextCost(RunLoadoutService.PackageSlotsId), Is.EqualTo(150));

                Assert.That(upgrades.TryPurchase(RunLoadoutService.SprintSpeedId), Is.True);
                Assert.That(upgrades.GetLevel(RunLoadoutService.SprintSpeedId), Is.EqualTo(1));
                Assert.That(save.Data.totalCoins, Is.EqualTo(950));
                Assert.That(upgrades.Stats.BaseRunSpeed, Is.EqualTo(6.5f));

                save.Data.totalCoins = 0;
                var level = upgrades.GetLevel(RunLoadoutService.JumpHeightId);
                var coins = save.Data.totalCoins;
                Assert.That(upgrades.TryPurchase(RunLoadoutService.JumpHeightId), Is.False);
                Assert.That(upgrades.GetLevel(RunLoadoutService.JumpHeightId), Is.EqualTo(level));
                Assert.That(save.Data.totalCoins, Is.EqualTo(coins));
            }
            finally
            {
                Object.DestroyImmediate(host);
            }
        }

        [Test]
        public void Upgrades_MagnetAndPackageSlotsAffectStats()
        {
            var host = new GameObject("UpgradeStatsDay5Test");
            try
            {
                var save = SaveSystem.Instance;
                save.Data.totalCoins = 1000;
                save.Save();
                var upgrades = host.AddComponent<UpgradeManager>();
                upgrades.SetConfigs(Array.Empty<UpgradeConfig>());

                Assert.That(upgrades.TryPurchase(RunLoadoutService.CoinMagnetId), Is.True);
                Assert.That(upgrades.TryPurchase(RunLoadoutService.PackageSlotsId), Is.True);
                Assert.That(upgrades.Stats.CoinMagnetRadius, Is.EqualTo(1.5f));
                Assert.That(upgrades.Stats.PackageSlots, Is.EqualTo(2));
            }
            finally
            {
                Object.DestroyImmediate(host);
            }
        }

        [Test]
        public void Upgrades_MaxLevelReturnsNegativeOne()
        {
            var host = new GameObject("UpgradeMaxDay5Test");
            try
            {
                var save = SaveSystem.Instance;
                save.Data.totalCoins = 5000;
                save.Save();
                var upgrades = host.AddComponent<UpgradeManager>();
                upgrades.SetConfigs(Array.Empty<UpgradeConfig>());
                for (var index = 0; index < 3; index++)
                {
                    Assert.That(upgrades.TryPurchase(RunLoadoutService.SprintSpeedId), Is.True);
                }

                Assert.That(upgrades.IsMaxLevel(RunLoadoutService.SprintSpeedId), Is.True);
                Assert.That(upgrades.GetNextCost(RunLoadoutService.SprintSpeedId), Is.EqualTo(-1));
            }
            finally
            {
                Object.DestroyImmediate(host);
            }
        }

        [Test]
        public void CatBreeds_StarterSelectionsPersistAndApplyBonuses()
        {
            var host = new GameObject("CatBreedManagerDay5Test");
            try
            {
                var breeds = host.AddComponent<CatBreedManager>();
                breeds.SetConfigs(Array.Empty<CatBreedConfig>());
                breeds.Refresh();

                Assert.That(breeds.IsUnlocked("tabby"), Is.True);
                Assert.That(breeds.IsUnlocked("tuxedo"), Is.True);
                Assert.That(breeds.TrySelect("tuxedo"), Is.True);
                Assert.That(SaveSystem.Instance.Data.selectedCatBreedId, Is.EqualTo("tuxedo"));

                var stats = new PlayerStats();
                breeds.ApplyTo(stats);
                Assert.That(stats.CatSpeedBonus, Is.EqualTo(0.3f).Within(0.001f));
            }
            finally
            {
                Object.DestroyImmediate(host);
            }
        }

        [Test]
        public void CatBreeds_RejectLockedOrUnknownContent()
        {
            var host = new GameObject("CatBreedLockedDay5Test");
            try
            {
                var breeds = host.AddComponent<CatBreedManager>();
                breeds.SetConfigs(Array.Empty<CatBreedConfig>());
                breeds.Refresh();

                Assert.That(breeds.TrySelect("manx"), Is.False);
                Assert.That(breeds.TrySelect("unknown"), Is.False);
                Assert.That(breeds.LockReason("manx"), Is.Not.Empty);
            }
            finally
            {
                Object.DestroyImmediate(host);
            }
        }

        [Test]
        public void AdManager_PremiumSkipsInterstitial()
        {
            entitlementHost = new GameObject("EntitlementAdDay5Test");
            var entitlement = entitlementHost.AddComponent<EntitlementChecker>();
            SetStaticInstance(typeof(EntitlementChecker), entitlement);
            entitlement.SetEntitlements(new[] { Constants.ENTITLEMENT_PREMIUM });

            var host = new GameObject("AdManagerDay5Test");
            try
            {
                var ads = host.AddComponent<AdManager>();
                ads.SetBackend(new FakeAdBackend());
                var closed = 0;
                ads.ShowDeathInterstitial(() => closed++);

                Assert.That(ads.ShouldShowAds, Is.False);
                Assert.That(ads.State, Is.EqualTo(AdFlowState.NotEligible));
                Assert.That(closed, Is.EqualTo(1));
            }
            finally
            {
                Object.DestroyImmediate(host);
            }
        }

        [Test]
        public void AdManager_RewardedDuplicateCallbackGrantsOnce()
        {
            var host = new GameObject("AdManagerDuplicateDay5Test");
            try
            {
                var ads = host.AddComponent<AdManager>();
                ads.SetBackend(new DuplicateRewardBackend());
                var completions = 0;
                ads.ShowRewarded(Constants.AD_PLACEMENT_CONTINUE, _ => completions++);

                Assert.That(completions, Is.EqualTo(1));
            }
            finally
            {
                Object.DestroyImmediate(host);
            }
        }

        [Test]
        public void AudioManager_MissingLibraryIsSilentAndSafe()
        {
            var host = new GameObject("AudioManagerDay5Test");
            try
            {
                var audio = host.AddComponent<AudioManager>();

                // The null-library path is still the shipping configuration for a player
                // whose audio assets failed to import. It must not throw and must
                // report no music rather than claiming something is playing.
                Assert.DoesNotThrow(() => audio.PlaySfx(SfxId.Jump));
                Assert.DoesNotThrow(() => audio.PlayMusic(MusicId.Hub));
                Assert.DoesNotThrow(() => audio.PlayAmbient(SfxId.Death));
                Assert.That(audio.Library, Is.Null);
                Assert.That(audio.CurrentMusic, Is.Null,
                    "A missing library must not claim a track is playing.");
            }
            finally
            {
                Object.DestroyImmediate(host);
            }
        }

        [Test]
        public void AudioManager_PlaysAndResolvesAConfiguredLibrary()
        {
            // The old test only asserted DoesNotThrow, so it passed even if AudioManager
            // did nothing at all. Drive it with a real library and a real clip.
            var host = new GameObject("AudioManagerLibraryDay5Test");
            var libraryHost = ScriptableObject.CreateInstance<AudioLibrary>();
            var clip = AudioClip.Create("day5-test", 4410, 1, 44100, false);
            try
            {
                SetLibraryEntry(libraryHost, "sfx", (int)SfxId.Jump, clip, 0.8f);
                SetLibraryEntry(libraryHost, "music", (int)MusicId.Hub, clip, 0.5f);

                var audio = host.AddComponent<AudioManager>();
                audio.Configure(libraryHost);
                BuildAudioSources(audio);

                Assert.That(audio.Library, Is.SameAs(libraryHost));
                Assert.That(audio.Library.GetSfx(SfxId.Jump), Is.SameAs(clip),
                    "The library must resolve a configured clip by id.");
                Assert.That(audio.Library.GetMusic(MusicId.Hub), Is.SameAs(clip));

                audio.PlaySfx(SfxId.Jump);
                audio.PlayMusic(MusicId.Hub);
                Assert.That(audio.CurrentMusic, Is.EqualTo(MusicId.Hub),
                    "A configured track must actually become the current music.");

                // Music is a bed, not a one-shot. CreateSource sets loop = false and
                // PlayMusic used to leave it there, so every track stopped after one pass.
                var musicSlot = audio.GetComponentsInChildren<AudioSource>(true)
                    .FirstOrDefault(source => source.clip == clip && source.loop);
                Assert.That(musicSlot, Is.Not.Null,
                    "A music bed must loop; otherwise the track ends and silence follows.");

                // Muting must silence it without losing the library wiring.
                audio.SetMuted(true);
                Assert.That(audio.IsMuted, Is.True);
                Assert.DoesNotThrow(() => audio.PlaySfx(SfxId.Jump));
                audio.SetMuted(false);
                Assert.That(audio.IsMuted, Is.False);
            }
            finally
            {
                Object.DestroyImmediate(host);
                Object.DestroyImmediate(clip);
                Object.DestroyImmediate(libraryHost);
            }
        }

        /// <summary>
        /// AudioManager builds its AudioSources in Awake, which does not run for an
        /// AddComponent in an EditMode test, so the music slots are null.
        /// </summary>
        private static void BuildAudioSources(AudioManager audio)
        {
            var method = typeof(AudioManager).GetMethod("BuildSources",
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(method, Is.Not.Null);
            method.Invoke(audio, null);
        }

        private static void SetLibraryEntry(
            AudioLibrary library, string fieldName, int id, AudioClip clip, float volume)
        {
            // SerializedObject rather than raw reflection: it is the pattern the
            // RevenueCatDay4 suite already uses, and it reaches private serialized
            // fields without a hand-rolled Array.CreateInstance dance.
            var serialized = new SerializedObject(library);
            var array = serialized.FindProperty(fieldName);
            Assert.That(array, Is.Not.Null, $"AudioLibrary.{fieldName} was not found.");
            array.arraySize = 1;
            var element = array.GetArrayElementAtIndex(0);
            element.FindPropertyRelative("id").intValue = id;
            element.FindPropertyRelative("clip").objectReferenceValue = clip;
            element.FindPropertyRelative("volume").floatValue = volume;
            element.FindPropertyRelative("pitchVariance").floatValue = 0f;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        [Test]
        public void PackageLeg_ReplenishesAfterDelivery()
        {
            var host = new GameObject("PackageLegDay5Test");
            try
            {
                var packages = host.AddComponent<CatCourier.Packages.PackageManager>();
                packages.AssignSpecificPackage(CatCourier.Core.PackageType.Normal, false, 0f, 1);
                packages.DeliverAtCheckpoint();
                var added = packages.ReplenishForNextLeg(false, 300f);

                Assert.That(added, Is.EqualTo(1));
                Assert.That(packages.Slots.Count, Is.EqualTo(1));
                Assert.That(packages.State, Is.EqualTo(CatCourier.Packages.PackageState.Assigned));
            }
            finally
            {
                Object.DestroyImmediate(host);
            }
        }

        // GameManagerTests.StartRun_ResetsContinueBalanceAndEnforcesFreeCap already covers
        // the StartRun wiring, so this only pins the cap constants themselves.

        [Test]
        public void ContinueRespawn_GrantsTwoSecondsOfInvincibility()
        {
            var host = new GameObject("ContinuePlayerDay5Test");
            try
            {
                host.AddComponent<Rigidbody2D>();
                host.AddComponent<BoxCollider2D>();
                var player = host.AddComponent<PlayerController>();
                player.Initialize();
                player.RespawnFromContinue();

                Assert.That(player.IsInvincible, Is.True);
                player.TriggerDeath();
                Assert.That(player.State, Is.Not.EqualTo(PlayerState.Dead));

                player.Simulate(2.1f);
                Assert.That(player.IsInvincible, Is.False);
                player.TriggerDeath();
                Assert.That(player.State, Is.EqualTo(PlayerState.Dead));
            }
            finally
            {
                Object.DestroyImmediate(host);
            }
        }

        private static void SetStaticInstance(Type type, object value)
        {
            var property = type.GetProperty("Instance", BindingFlags.Static | BindingFlags.Public);
            property?.GetSetMethod(true)?.Invoke(null, new[] { value });
        }

        private sealed class DuplicateRewardBackend : IAdBackend
        {
            public void ShowInterstitial(string placementId, Action onClosed) => onClosed?.Invoke();

            public void ShowRewarded(string placementId, Action<bool> onDone)
            {
                onDone?.Invoke(true);
                onDone?.Invoke(true);
            }
        }

        private sealed class ThrowingAdBackend : IAdBackend
        {
            public void ShowInterstitial(string placementId, Action onClosed) =>
                throw new InvalidOperationException("no network");

            public void ShowRewarded(string placementId, Action<bool> onDone) =>
                throw new InvalidOperationException("no network");
        }

        private sealed class CountingAdBackend : IAdBackend
        {
            public int Interstitials;

            public void ShowInterstitial(string placementId, Action onClosed)
            {
                Interstitials++;
                onClosed?.Invoke();
            }

            public void ShowRewarded(string placementId, Action<bool> onDone) => onDone?.Invoke(true);
        }

        [Test]
        public void PremiumCoinMultiplier_HasExactlyOneOwner()
        {
            // BuildPlayerStats owns the 2x premium rule. RunLoadoutService used to apply
            // it a second time on the cloned stats, so the two copies could disagree.
            entitlementHost = new GameObject("EntitlementMultiplierDay5Test");
            var entitlement = entitlementHost.AddComponent<EntitlementChecker>();
            SetStaticInstance(typeof(EntitlementChecker), entitlement);
            entitlement.SetEntitlements(new[] { Constants.ENTITLEMENT_PREMIUM });

            var host = new GameObject("MultiplierOwnerDay5Test");
            try
            {
                var upgrades = host.AddComponent<UpgradeManager>();
                upgrades.SetConfigs(Array.Empty<UpgradeConfig>());

                var fromUpgrades = upgrades.Stats;
                Assert.That(fromUpgrades.PremiumCoinMultiplier, Is.EqualTo(2f));

                var loadout = RunLoadoutService.Build();
                Assert.That(loadout.Stats.PremiumCoinMultiplier, Is.EqualTo(2f),
                    "Cloning the loadout must not re-apply or drop the premium multiplier.");
            }
            finally
            {
                Object.DestroyImmediate(host);
            }
        }

        [Test]
        public void SceneLoadFailure_IsReportedToGameManager()
        {
            // SceneLoader only subscribes when GameManager already exists, so a load
            // failing after the manager appears would otherwise go unreported. This
            // pins that the normal order really does deliver the failure.
            var host = new GameObject("SceneLoadFailureDay5Test");
            try
            {
                var manager = host.AddComponent<GameManager>();
                var claim = typeof(GameManager).GetMethod("ClaimSingleton", BindingFlags.Instance | BindingFlags.NonPublic);
                Assert.That(claim.Invoke(manager, null), Is.True);

                var loaderHost = new GameObject("SceneLoaderFailureDay5Test");
                var loader = loaderHost.AddComponent<SceneLoader>();
                SetStaticInstance(typeof(SceneLoader), loader);

                var failed = string.Empty;
                loader.OnSceneLoadFailed += scene => failed = scene;

                // A loader that believes it is mid-load takes the failure branch.
                typeof(SceneLoader).GetField("isLoading", BindingFlags.Instance | BindingFlags.NonPublic)
                    ?.SetValue(loader, true);

                loader.Load("Game");
                Assert.That(failed, Is.EqualTo("Game"));
            }
            finally
            {
                SetStaticInstance(typeof(SceneLoader), null);
                SetStaticInstance(typeof(GameManager), null);
                Object.DestroyImmediate(host);
            }
        }

        [Test]
        public void DistrictReachDistances_AreDefinedInOnePlace()
        {
            Assert.That(RunLoadoutService.HarbourReachDistance, Is.EqualTo(600f));
            Assert.That(RunLoadoutService.SuburbsReachDistance, Is.EqualTo(1200f));

            // Free players reach both on distance alone, which is why the district packs
            // must grant something extra rather than the district itself.
            var free = RunLoadoutService.GetEligibleDistricts(1200f, false, Array.Empty<string>());
            Assert.That(free, Does.Contain(DistrictId.Harbour));
            Assert.That(free, Does.Contain(DistrictId.Suburbs));
            Assert.That(RunLoadoutService.HarbourPackCoinBonus, Is.GreaterThan(1f),
                "A pack whose district already unlocks free needs a bonus to be worth buying.");
            Assert.That(RunLoadoutService.SuburbsPackCoinBonus, Is.GreaterThan(1f));
        }

        [Test]
        public void DistrictPackCoinBonus_OnlyAppliesToAnOwnedPack()
        {
            entitlementHost = new GameObject("EntitlementDistrictBonusDay5Test");
            var entitlement = entitlementHost.AddComponent<EntitlementChecker>();
            SetStaticInstance(typeof(EntitlementChecker), entitlement);

            // Nothing owned: no district may pay a bonus, free or paid.
            entitlement.SetEntitlements(Array.Empty<string>());
            foreach (DistrictId district in Enum.GetValues(typeof(DistrictId)))
            {
                Assert.That(RunLoadoutService.DistrictCoinBonus(district), Is.EqualTo(1f),
                    $"{district} must not pay a bonus the player has not bought.");
            }

            // Harbour owned: only Harbour pays.
            entitlement.SetEntitlements(new[] { Constants.ENTITLEMENT_HARBOUR });
            Assert.That(RunLoadoutService.DistrictCoinBonus(DistrictId.Harbour),
                Is.EqualTo(RunLoadoutService.HarbourPackCoinBonus));
            Assert.That(RunLoadoutService.DistrictCoinBonus(DistrictId.Suburbs), Is.EqualTo(1f));
            Assert.That(RunLoadoutService.DistrictCoinBonus(DistrictId.OldTown), Is.EqualTo(1f));
            Assert.That(RunLoadoutService.DistrictCoinBonus(DistrictId.Downtown), Is.EqualTo(1f));

            // Suburbs owned as well: both paid districts pay, free ones still do not.
            entitlement.SetEntitlements(new[] { Constants.ENTITLEMENT_HARBOUR, Constants.ENTITLEMENT_SUBURBS });
            Assert.That(RunLoadoutService.DistrictCoinBonus(DistrictId.Suburbs),
                Is.EqualTo(RunLoadoutService.SuburbsPackCoinBonus));
            Assert.That(RunLoadoutService.DistrictCoinBonus(DistrictId.OldTown), Is.EqualTo(1f));

            // Premium does not imply the district packs; they are separate SKUs.
            entitlement.SetEntitlements(new[] { Constants.ENTITLEMENT_PREMIUM });
            Assert.That(RunLoadoutService.DistrictCoinBonus(DistrictId.Harbour), Is.EqualTo(1f));
        }

        [Test]
        public void DistrictPackCoinBonus_ReachesTheCoinPayout()
        {
            entitlementHost = new GameObject("EntitlementCoinPayoutDay5Test");
            var entitlement = entitlementHost.AddComponent<EntitlementChecker>();
            SetStaticInstance(typeof(EntitlementChecker), entitlement);
            entitlement.SetEntitlements(new[] { Constants.ENTITLEMENT_HARBOUR });

            var host = new GameObject("DistrictBonusPayoutDay5Test");
            try
            {
                var coinManager = host.AddComponent<CoinManager>();
                coinManager.Configure(1f, 1f, 1f, 0f);
                coinManager.ResetRun();

                Assert.That(coinManager.FinalMultiplier, Is.EqualTo(1f), "No district bonus before entering one.");
                coinManager.SetDistrictMultiplier(RunLoadoutService.DistrictCoinBonus(DistrictId.Harbour));
                Assert.That(coinManager.FinalMultiplier,
                    Is.EqualTo(RunLoadoutService.HarbourPackCoinBonus).Within(0.001f));

                var awarded = coinManager.Collect(Constants.COIN_BASE_VALUE);
                Assert.That(awarded, Is.EqualTo(Mathf.RoundToInt(Constants.COIN_BASE_VALUE * RunLoadoutService.HarbourPackCoinBonus)),
                    "The pack bonus must reach the coins the player actually collects.");

                // Leaving the district removes it again.
                coinManager.SetDistrictMultiplier(RunLoadoutService.DistrictCoinBonus(DistrictId.OldTown));
                Assert.That(coinManager.FinalMultiplier, Is.EqualTo(1f));
            }
            finally
            {
                Object.DestroyImmediate(host);
            }
        }

        [Test]
        public void AdManager_ContinueDoesNotRestoreThePerRunInterstitial()
        {
            var host = new GameObject("AdManagerContinueDay5Test");
            try
            {
                var managerHost = new GameObject("GameManagerAdDay5Test");
                var manager = managerHost.AddComponent<GameManager>();
                var ads = host.AddComponent<AdManager>();
                var backend = new CountingAdBackend();
                ads.SetBackend(backend);

                // AddComponent does not run Awake in an EditMode test, so claim the
                // singleton explicitly the way GameManagerTests does.
                var claim = typeof(GameManager).GetMethod("ClaimSingleton", BindingFlags.Instance | BindingFlags.NonPublic);
                Assert.That(claim, Is.Not.Null);
                Assert.That(claim.Invoke(manager, null), Is.True, "The manager must own the singleton.");
                HookAdManagerToGameManager(ads);

                InvokeState(manager, GameState.Hub);
                InvokeState(manager, GameState.Running);
                Assert.That(ads.InterstitialShownThisRun, Is.False);
                ads.ShowDeathInterstitial(() => { });
                Assert.That(backend.Interstitials, Is.EqualTo(1));

                // A continue moves Dead -> Running. That must not re-arm the cap.
                InvokeState(manager, GameState.Dead);
                InvokeState(manager, GameState.Running);
                ads.ShowDeathInterstitial(() => { });
                Assert.That(backend.Interstitials, Is.EqualTo(1),
                    "A second death after a continue must not show a second interstitial.");
                Assert.That(ads.InterstitialShownThisRun, Is.True);

                // Only returning to the Hub and starting again re-arms it.
                InvokeState(manager, GameState.Hub);
                InvokeState(manager, GameState.Running);
                Assert.That(ads.InterstitialShownThisRun, Is.False);
                ads.ShowDeathInterstitial(() => { });
                Assert.That(backend.Interstitials, Is.EqualTo(2));
            }
            finally
            {
                Object.DestroyImmediate(host);
                var managers = Object.FindObjectsOfType<GameManager>();
                foreach (var manager in managers)
                    Object.DestroyImmediate(manager.gameObject);
            }
        }

        [Test]
        public void AdManager_FailedInterstitialLoadKeepsThePerRunSlot()
        {
            var host = new GameObject("AdManagerFailDay5Test");
            try
            {
                var ads = host.AddComponent<AdManager>();
                ads.SetBackend(new ThrowingAdBackend());

                var closed = 0;
                ads.ShowDeathInterstitial(() => closed++);

                Assert.That(closed, Is.EqualTo(1), "A failed load must still release the flow.");
                Assert.That(ads.InterstitialShownThisRun, Is.False,
                    "A failed load must not burn the run's one permitted interstitial.");

                // A working backend can still use the slot.
                var backend = new CountingAdBackend();
                ads.SetBackend(backend);
                ads.ShowDeathInterstitial(() => { });
                Assert.That(backend.Interstitials, Is.EqualTo(1));
                Assert.That(ads.InterstitialShownThisRun, Is.True);
            }
            finally
            {
                Object.DestroyImmediate(host);
            }
        }

        [Test]
        public void AdManager_WithoutBackendSkipsTheInterstitialEntirely()
        {
            var host = new GameObject("AdManagerNoBackendDay5Test");
            try
            {
                var ads = host.AddComponent<AdManager>();
                ads.SetBackend(null);

                var closed = 0;
                ads.ShowDeathInterstitial(() => closed++);

                Assert.That(closed, Is.EqualTo(1));
                Assert.That(ads.State, Is.EqualTo(AdFlowState.NotEligible));
            }
            finally
            {
                Object.DestroyImmediate(host);
            }
        }

        private static void InvokeState(GameManager manager, GameState state)
        {
            var method = typeof(GameManager).GetMethod("SetState", BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(method, Is.Not.Null);
            method.Invoke(manager, new object[] { state });
        }

        /// <summary>
        /// AdManager subscribes to GameManager.OnStateChanged lazily from Update, which
        /// never runs in an EditMode [Test]. Call it directly so the state machine under
        /// test is actually the one shipping.
        /// </summary>
        private static void HookAdManagerToGameManager(AdManager ads)
        {
            var method = typeof(AdManager).GetMethod("TrackGameManager", BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(method, Is.Not.Null);
            method.Invoke(ads, null);
        }
    }
}
