using System;
using System.Collections.Generic;
using CatCourier.Core;

namespace CatCourier.Monetization
{
    public sealed class FakePurchasesBackend : IPurchasesBackend
    {
        public event Action<HashSet<string>> EntitlementsChanged;

        private readonly Dictionary<(string OfferingId, string PackageId), PaywallPackage> packages;
        private readonly HashSet<string> activeEntitlements = new();
        private bool cancelNextPurchase;

        public FakePurchasesBackend()
        {
            packages = new Dictionary<(string OfferingId, string PackageId), PaywallPackage>
            {
                [(RevenueCatIds.OfferingDefault, RevenueCatIds.PackageMonthly)] = new PaywallPackage(RevenueCatIds.PackageMonthly, "$4.99", "month", true),
                [(RevenueCatIds.OfferingDefault, RevenueCatIds.PackageAnnual)] = new PaywallPackage(RevenueCatIds.PackageAnnual, "$39.99", "year", true),
                [(RevenueCatIds.OfferingBreeds, RevenueCatIds.PackageRare)] = new PaywallPackage(RevenueCatIds.PackageRare, "$2.99", "one-time", false),
                [(RevenueCatIds.OfferingBreeds, RevenueCatIds.PackageLegendary)] = new PaywallPackage(RevenueCatIds.PackageLegendary, "$4.99", "one-time", false),
                [(RevenueCatIds.OfferingDistricts, RevenueCatIds.PackageHarbour)] = new PaywallPackage(RevenueCatIds.PackageHarbour, "$1.99", "one-time", false),
                [(RevenueCatIds.OfferingDistricts, RevenueCatIds.PackageSuburbs)] = new PaywallPackage(RevenueCatIds.PackageSuburbs, "$1.99", "one-time", false)
            };
        }

        public void Initialize(string publicKey, string appUserId, Action<bool> completed) => completed(true);

        public void CancelNextPurchase() => cancelNextPurchase = true;

        public void GetOffering(string offeringId, Action<bool, PaywallOffering> done)
        {
            var result = new List<PaywallPackage>();
            foreach (var pair in packages)
            {
                if (pair.Key.OfferingId == offeringId)
                {
                    result.Add(pair.Value);
                }
            }

            done(result.Count > 0, new PaywallOffering(offeringId, result));
        }

        public void GetPackage(string offeringId, string packageId, Action<bool, PaywallPackage> done)
        {
            done(packages.TryGetValue((offeringId, packageId), out var package), package);
        }

        public void Purchase(string offeringId, string packageId, Action<PurchaseOutcome> done)
        {
            if (cancelNextPurchase)
            {
                cancelNextPurchase = false;
                done(PurchaseOutcome.Cancelled);
                return;
            }

            if (!packages.ContainsKey((offeringId, packageId)))
            {
                done(PurchaseOutcome.Error);
                return;
            }

            AddEntitlementForPackage(packageId);
            PublishEntitlements();
            done(PurchaseOutcome.Success);
        }

        public void Restore(Action<bool> done)
        {
            PublishEntitlements();
            done(true);
        }

        public void RefreshCustomerInfo(Action<bool> done)
        {
            PublishEntitlements();
            done(true);
        }

        public void Dispose()
        {
            activeEntitlements.Clear();
            EntitlementsChanged = null;
        }

        private void AddEntitlementForPackage(string packageId)
        {
            // Package to entitlement. The paywall and RevenueCatIds already owned the
            // package to offering half of this; only the entitlement ids are here.
            var entitlement = packageId switch
            {
                RevenueCatIds.PackageMonthly or RevenueCatIds.PackageAnnual => Constants.ENTITLEMENT_PREMIUM,
                RevenueCatIds.PackageRare => Constants.ENTITLEMENT_RARE_PACK,
                RevenueCatIds.PackageLegendary => Constants.ENTITLEMENT_LEGEND_PACK,
                RevenueCatIds.PackageHarbour => Constants.ENTITLEMENT_HARBOUR,
                RevenueCatIds.PackageSuburbs => Constants.ENTITLEMENT_SUBURBS,
                _ => null
            };

            if (entitlement != null)
            {
                activeEntitlements.Add(entitlement);
            }
        }

        private void PublishEntitlements() => EntitlementsChanged?.Invoke(new HashSet<string>(activeEntitlements));
    }
}
