using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using CatCourier.Audio;
using CatCourier.Core;
using CatCourier.Monetization;
using CatCourier.Player;
using CatCourier.Progression;
using NUnit.Framework;
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
                Assert.DoesNotThrow(() => audio.PlaySfx(SfxId.Jump));
                Assert.DoesNotThrow(() => audio.PlayMusic(MusicId.Hub));
                Assert.DoesNotThrow(() => audio.PlayAmbient(SfxId.Death));
            }
            finally
            {
                Object.DestroyImmediate(host);
            }
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

        [Test]
        public void ContinueQuota_FreeOnePremiumThree()
        {
            var host = new GameObject("ContinueQuotaDay5Test");
            try
            {
                var game = host.AddComponent<GameManager>();
                game.ResetContinuesForRun(false);
                Assert.That(game.ContinuesLeft, Is.EqualTo(1));
                game.ResetContinuesForRun(true);
                Assert.That(game.ContinuesLeft, Is.EqualTo(3));
            }
            finally
            {
                Object.DestroyImmediate(host);
            }
        }

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
    }
}
