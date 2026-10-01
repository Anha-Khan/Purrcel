using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using CatCourier.Core;
using CatCourier.Generation;
using CatCourier.Monetization;
using CatCourier.UI;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace CatCourier.Tests
{
    public sealed class RevenueCatDay4Tests
    {
        [Test]
        public void AndroidDemo_RequiresRealBackendAndKey_AndProductionIgnoresTestStore()
        {
            var config = ScriptableObject.CreateInstance<RevenueCatConfig>();
            try
            {
                var serialized = new SerializedObject(config);
                serialized.FindProperty("developmentTestStorePublicKey").stringValue = "test_demo_key";
                serialized.ApplyModifiedPropertiesWithoutUndo();
                Assert.That(config.IsAndroidDemoReady, Is.False);

                serialized.Update();
                serialized.FindProperty("backend").enumValueIndex = (int)RevenueCatBackendSelection.Real;
                serialized.ApplyModifiedPropertiesWithoutUndo();
                Assert.That(config.IsAndroidDemoReady, Is.True);
                Assert.That(config.ApplePublicKey, Is.EqualTo("test_demo_key"));
                Assert.That(config.GooglePublicKey, Is.EqualTo("test_demo_key"));

                serialized.Update();
                serialized.FindProperty("environment").enumValueIndex = (int)RevenueCatEnvironment.Production;
                serialized.ApplyModifiedPropertiesWithoutUndo();
                Assert.That(config.IsAndroidDemoReady, Is.False);
                Assert.That(config.GooglePublicKey, Is.Empty);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(config);
            }
        }

        [Test]
        public void FakeBackend_ReturnsAllOfferingsAndPackages()
        {
            var backend = new FakePurchasesBackend();
            var expected = new[]
            {
                RevenueCatIds.PackageMonthly,
                RevenueCatIds.PackageAnnual,
                RevenueCatIds.PackageRare,
                RevenueCatIds.PackageLegendary,
                RevenueCatIds.PackageHarbour,
                RevenueCatIds.PackageSuburbs
            };

            var returned = new List<string>();
            foreach (var offeringId in new[] { RevenueCatIds.OfferingDefault, RevenueCatIds.OfferingBreeds, RevenueCatIds.OfferingDistricts })
            {
                var found = false;
                backend.GetOffering(offeringId, (success, offering) =>
                {
                    found = success;
                    foreach (var package in offering.Packages)
                    {
                        Assert.That(expected, Does.Contain(package.PackageId));
                        Assert.That(package.PriceString, Is.Not.Empty);
                        returned.Add(package.PackageId);
                    }
                });
                Assert.That(found, Is.True, $"{offeringId} never invoked its completion callback");
            }

            // Containment alone would pass if an offering returned only one package.
            Assert.That(returned, Is.EquivalentTo(expected));
        }

        [Test]
        public void EntitlementChecker_MapsAllEntitlementFamilies()
        {
            var host = new GameObject("EntitlementCheckerDay4Test");
            try
            {
                var checker = host.AddComponent<EntitlementChecker>();
                var events = 0;
                checker.OnEntitlementsChanged += () => events++;
                checker.SetEntitlements(new[]
                {
                    Constants.ENTITLEMENT_PREMIUM,
                    Constants.ENTITLEMENT_RARE_PACK,
                    Constants.ENTITLEMENT_HARBOUR
                });

                Assert.That(checker.IsPremium, Is.True);
                Assert.That(checker.HasBreedPack(Constants.ENTITLEMENT_RARE_PACK), Is.True);
                Assert.That(checker.HasBreedPack(Constants.ENTITLEMENT_LEGEND_PACK), Is.False);
                Assert.That(checker.HasDistrict(DistrictId.Harbour), Is.True);
                Assert.That(checker.HasDistrict(DistrictId.Suburbs), Is.False);
                // Free districts have no entitlement id, but they are never locked.
                Assert.That(checker.HasDistrict(DistrictId.OldTown), Is.True);
                Assert.That(checker.HasDistrict(DistrictId.Downtown), Is.True);
                Assert.That(events, Is.EqualTo(1));

                checker.SetEntitlements(Array.Empty<string>());
                Assert.That(checker.IsPremium, Is.False);
                Assert.That(checker.HasDistrict(DistrictId.Harbour), Is.False);
                Assert.That(checker.HasDistrict(DistrictId.OldTown), Is.True,
                    "Losing entitlements must not lock the always-free districts.");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(host);
            }
        }

        [Test]
        public void RevenueCatManager_UsesFakeBackendAndPublishesReadiness()
        {
            var host = new GameObject("RevenueCatManagerDay4Test");
            try
            {
                var manager = host.AddComponent<RevenueCatManager>();
                manager.Initialize(null, true);
                Assert.That(manager.IsReady, Is.True);
                Assert.That(manager.IsFakeBackend, Is.True);
                Assert.That(manager.State, Is.EqualTo(RevenueCatState.Ready));

                var found = false;
                manager.GetOffering(RevenueCatIds.OfferingDefault, (success, offering) =>
                {
                    found = success && offering.Packages.Count > 0;
                });
                Assert.That(found, Is.True);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(host);
            }
        }

        [Test]
        public void PaywallGate_FiresAfterThirdRunOnlyOnce()
        {
            var directory = Path.Combine(Path.GetTempPath(), "CatCourierPaywallTests", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(directory);
            var host = new GameObject("SaveSystemPaywallTest");
            host.SetActive(false);
            var save = host.AddComponent<SaveSystem>();
            save.ConfigurePaths(directory);
            save.Load();
            SetSaveInstance(save);
            PaywallGate.ResetForTests();
            var requests = new List<PaywallSource>();
            PaywallGate.OnRequested += requests.Add;
            try
            {
                save.Data.totalRunsCompleted = 2;
                PaywallGate.NotifyRunCompleted();
                Assert.That(requests, Is.Empty);

                save.Data.totalRunsCompleted = 3;
                PaywallGate.NotifyRunCompleted();
                Assert.That(requests, Is.EqualTo(new[] { PaywallSource.AfterRun3 }));
                Assert.That(save.Data.hasSeenPaywall, Is.True);

                save.Data.totalRunsCompleted = 4;
                PaywallGate.NotifyRunCompleted();
                Assert.That(requests.Count, Is.EqualTo(1));
            }
            finally
            {
                PaywallGate.ResetForTests();
                SetSaveInstance(null);
                UnityEngine.Object.DestroyImmediate(host);
                if (Directory.Exists(directory))
                {
                    Directory.Delete(directory, true);
                }
            }
        }

        [Test]
        public void PaywallGate_AllowsAnExplicitSettingsRequest()
        {
            PaywallGate.ResetForTests();
            var requests = new List<PaywallSource>();
            PaywallGate.OnRequested += requests.Add;
            try
            {
                PaywallGate.Request(PaywallSource.Settings);
                Assert.That(requests, Is.EqualTo(new[] { PaywallSource.Settings }));
            }
            finally
            {
                PaywallGate.ResetForTests();
            }
        }

        [Test]
        public void RevenueCatIds_IsTheSingleSourceForPackageToOffering()
        {
            // The paywall used to reverse-map package to offering by hand, the fake
            // backend encoded it again, and the tests a third time. Adding a SKU meant
            // editing all three and hoping they agreed.
            foreach (var offeringId in RevenueCatIds.AllOfferings())
            {
                var packages = RevenueCatIds.PackagesIn(offeringId);
                Assert.That(packages, Is.Not.Empty, $"{offeringId} has no packages.");
                foreach (var packageId in packages)
                {
                    Assert.That(RevenueCatIds.OfferingFor(packageId), Is.EqualTo(offeringId),
                        $"{packageId} does not map back to {offeringId}.");
                }
            }

            Assert.That(RevenueCatIds.OfferingFor("not_a_package"), Is.Empty,
                "An unknown package must not fall through to a real offering.");
        }

        [Test]
        public void Paywall_KeepsUnfinishedPaidContentHidden()
        {
            // The "unfinished paid content is never sold" rule is the most valuable
            // guarantee in the monetization layer and it had no coverage at all. With
            // the shipping empty catalog, no district pack may be offered.
            var catalog = ScriptableObject.CreateInstance<ChunkCatalog>();
            var previous = PaywallPresenter.CatalogReference;
            try
            {
                PaywallPresenter.CatalogReference = catalog;
                Assert.That(PaywallPresenter.HasDistrictContent(), Is.False,
                    "An empty catalog must not sell a district pack.");

                catalog.SetEntries(new[]
                {
                    new ChunkCatalog.Entry
                    {
                        district = DistrictId.Harbour,
                        type = ChunkType.SmallGap,
                        prefab = null
                    }
                });

                Assert.That(PaywallPresenter.HasDistrictContent(), Is.False,
                    "A null prefab is not authored content, so the pack must stay hidden.");
            }
            finally
            {
                PaywallPresenter.CatalogReference = previous;
                UnityEngine.Object.DestroyImmediate(catalog);
            }
        }

        [Test]
        public void Paywall_HidesBreedPacksUntilRealContentExists()
        {
            // No CatBreedManager in the scene means no IAP breeds, so no breed offering.
            Assert.That(PaywallPresenter.HasPaidBreedContent(), Is.False,
                "With no breed manager there is no sellable breed content.");
        }

        private static void SetSaveInstance(SaveSystem value)
        {
            var property = typeof(SaveSystem).GetProperty("Instance", BindingFlags.Static | BindingFlags.Public);
            property?.GetSetMethod(true)?.Invoke(null, new object[] { value });
        }
    }
}
