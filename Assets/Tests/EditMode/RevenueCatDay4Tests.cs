using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using CatCourier.Core;
using CatCourier.Monetization;
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
                    }
                });
                Assert.That(found, Is.True);
            }
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
                Assert.That(events, Is.EqualTo(1));

                checker.SetEntitlements(Array.Empty<string>());
                Assert.That(checker.IsPremium, Is.False);
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
        public void PaywallGate_AllowsExplicitLockedAndSettingsRequests()
        {
            PaywallGate.ResetForTests();
            var requests = new List<PaywallSource>();
            PaywallGate.OnRequested += requests.Add;
            try
            {
                PaywallGate.Request(PaywallSource.LockedFeature);
                PaywallGate.Request(PaywallSource.Settings);
                Assert.That(requests, Is.EqualTo(new[] { PaywallSource.LockedFeature, PaywallSource.Settings }));
            }
            finally
            {
                PaywallGate.ResetForTests();
            }
        }

        private static void SetSaveInstance(SaveSystem value)
        {
            var property = typeof(SaveSystem).GetProperty("Instance", BindingFlags.Static | BindingFlags.Public);
            property?.GetSetMethod(true)?.Invoke(null, new object[] { value });
        }
    }
}
