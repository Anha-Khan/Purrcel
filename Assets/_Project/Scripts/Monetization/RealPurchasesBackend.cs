using System;
using System.Collections.Generic;
using CatCourier.Core;
using UnityEngine;

namespace CatCourier.Monetization
{
    public sealed class RealPurchasesBackend : IPurchasesBackend
    {
        private sealed class CustomerInfoListener : Purchases.UpdatedCustomerInfoListener
        {
            private Action<Purchases.CustomerInfo> callback;

            public void Configure(Action<Purchases.CustomerInfo> value) => callback = value;

            public override void CustomerInfoReceived(Purchases.CustomerInfo customerInfo) => callback?.Invoke(customerInfo);
        }

        public event Action<HashSet<string>> EntitlementsChanged;

        private GameObject host;

        // Only read on device builds; the editor path returns before the SDK is configured.
#pragma warning disable 0414
        private Purchases purchases;
        private CustomerInfoListener listener;
        private bool initialized;
#pragma warning restore 0414
        private readonly Dictionary<(string OfferingId, string PackageId), Purchases.Package> packages = new();
        private readonly Dictionary<string, PaywallOffering> offerings = new();

        public void Initialize(string publicKey, string appUserId, Action<bool> completed)
        {
#if UNITY_EDITOR
            completed(false);
            return;
#else
            if (string.IsNullOrWhiteSpace(publicKey))
            {
                Debug.LogError("RevenueCat public key is missing.");
                completed(false);
                return;
            }

            host = new GameObject("RevenueCatPurchases");
            UnityEngine.Object.DontDestroyOnLoad(host);
            var runner = host.AddComponent<DeferredActionRunner>();
            purchases = host.AddComponent<Purchases>();
            purchases.useRuntimeSetup = true;
            listener = host.AddComponent<CustomerInfoListener>();
            listener.Configure(PublishCustomerInfo);
            purchases.listener = listener;

            var configuration = Purchases.PurchasesConfiguration.Builder.Init(publicKey)
                .SetAppUserId(string.IsNullOrWhiteSpace(appUserId) ? null : appUserId)
                .SetAutomaticDeviceIdentifierCollectionEnabled(false)
                .Build();
#if DEVELOPMENT_BUILD || UNITY_EDITOR
            purchases.SetLogLevel(Purchases.LogLevel.Debug);
#else
            purchases.SetLogLevel(Purchases.LogLevel.Error);
#endif
            runner.RunNextFrames(() =>
            {
                purchases.Configure(configuration);
                runner.RunNextFrames(() => purchases.GetCustomerInfo((customerInfo, error) =>
                {
                    initialized = error == null;
                    PublishCustomerInfo(customerInfo);
                    completed(initialized);
                }));
            });
#endif
        }

        public void GetOffering(string offeringId, Action<bool, PaywallOffering> done)
        {
#if UNITY_EDITOR
            done(false, default);
#else
            if (!initialized)
            {
                done(false, default);
                return;
            }

            if (offerings.TryGetValue(offeringId, out var cached))
            {
                done(true, cached);
                return;
            }

            purchases.GetOfferings((loaded, error) =>
            {
                if (error != null || loaded == null || !loaded.All.TryGetValue(offeringId, out var offering))
                {
                    done(false, default);
                    return;
                }

                var neutralPackages = new List<PaywallPackage>();
                foreach (var package in offering.AvailablePackages)
                {
                    neutralPackages.Add(ToNeutralPackage(package));
                }

                var neutral = new PaywallOffering(offeringId, neutralPackages);
                offerings[offeringId] = neutral;
                done(true, neutral);
            });
#endif
        }

        public void GetPackage(string offeringId, string packageId, Action<bool, PaywallPackage> done)
        {
#if UNITY_EDITOR
            done(false, default);
#else
            if (!initialized)
            {
                done(false, default);
                return;
            }

            var cacheKey = (offeringId, packageId);
            if (packages.TryGetValue(cacheKey, out var cached))
            {
                done(true, ToNeutralPackage(cached));
                return;
            }

            purchases.GetOfferings((offerings, error) =>
            {
                if (error != null || offerings == null || !offerings.All.TryGetValue(offeringId, out var offering))
                {
                    done(false, default);
                    return;
                }

                var match = offering.AvailablePackages.Find(item => item.Identifier == packageId);
                if (match == null)
                {
                    done(false, default);
                    return;
                }

                packages[cacheKey] = match;
                done(true, ToNeutralPackage(match));
            });
#endif
        }

        public void Purchase(string offeringId, string packageId, Action<PurchaseOutcome> done)
        {
#if UNITY_EDITOR
            done(PurchaseOutcome.Error);
#else
            GetPackage(offeringId, packageId, (found, _) =>
            {
                if (!found)
                {
                    done(PurchaseOutcome.Error);
                    return;
                }

                purchases.PurchasePackage(packages[(offeringId, packageId)], result =>
                {
                    if (result.UserCancelled)
                    {
                        done(PurchaseOutcome.Cancelled);
                        return;
                    }

                    if (result.Error != null || result.CustomerInfo == null)
                    {
                        done(PurchaseOutcome.Error);
                        return;
                    }

                    PublishCustomerInfo(result.CustomerInfo);
                    done(PurchaseOutcome.Success);
                });
            });
#endif
        }

        public void Restore(Action<bool> done)
        {
#if UNITY_EDITOR
            done(false);
#else
            if (!initialized)
            {
                done(false);
                return;
            }

            purchases.RestorePurchases((customerInfo, error) =>
            {
                if (error == null && customerInfo != null)
                {
                    PublishCustomerInfo(customerInfo);
                }

                done(error == null);
            });
#endif
        }

        public void RefreshCustomerInfo(Action<bool> done)
        {
#if UNITY_EDITOR
            done(false);
#else
            if (!initialized)
            {
                done(false);
                return;
            }

            purchases.GetCustomerInfo((customerInfo, error) =>
            {
                if (error == null && customerInfo != null)
                {
                    PublishCustomerInfo(customerInfo);
                }

                done(error == null);
            });
#endif
        }

        public void Dispose()
        {
            EntitlementsChanged = null;
            packages.Clear();
            offerings.Clear();
            if (host != null)
            {
                UnityEngine.Object.Destroy(host);
            }

            host = null;
            purchases = null;
            listener = null;
            initialized = false;
        }

        private static PaywallPackage ToNeutralPackage(Purchases.Package package)
        {
            var product = package.StoreProduct;
            var hasFreeTrial = (product.IntroductoryPrice != null && product.IntroductoryPrice.Price == 0f) ||
                               product.DefaultOption?.FreePhase != null ||
                               (product.SubscriptionOptions != null && Array.Exists(product.SubscriptionOptions, option => option.FreePhase != null));
            return new PaywallPackage(package.Identifier, product.PriceString, NormalizePeriod(product.SubscriptionPeriod), hasFreeTrial);
        }

        private static string NormalizePeriod(string period)
        {
            if (string.IsNullOrWhiteSpace(period))
            {
                return "one-time";
            }

            return period switch
            {
                "P1W" => "week",
                "P1M" => "month",
                "P3M" => "3 months",
                "P6M" => "6 months",
                "P1Y" => "year",
                "P2Y" => "2 years",
                _ => period
            };
        }

        private void PublishCustomerInfo(Purchases.CustomerInfo customerInfo)
        {
            if (customerInfo?.Entitlements?.Active == null)
            {
                return;
            }

            EntitlementsChanged?.Invoke(new HashSet<string>(customerInfo.Entitlements.Active.Keys));
        }
    }
}
