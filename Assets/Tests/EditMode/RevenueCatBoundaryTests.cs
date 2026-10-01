using CatCourier.Core;
using CatCourier.Monetization;
using NUnit.Framework;

namespace CatCourier.Tests
{
    public sealed class RevenueCatBoundaryTests
    {
        [Test]
        public void FakeBackend_ReturnsConfiguredPackages()
        {
            var backend = new FakePurchasesBackend();

            // ponytail: assertions live in the callback, so an async backend would
            // skip them and the test would pass green. invoked guards that.
            var invoked = false;
            backend.GetPackage(RevenueCatIds.OfferingDefault, RevenueCatIds.PackageMonthly, (found, package) =>
            {
                invoked = true;
                Assert.That(found, Is.True);
                Assert.That(package.PackageId, Is.EqualTo(RevenueCatIds.PackageMonthly));
                Assert.That(package.PriceString, Is.Not.Empty);
                Assert.That(package.HasFreeTrial, Is.True);
            });

            Assert.That(invoked, Is.True, "GetPackage never invoked its completion callback");
        }

        [Test]
        public void FakeBackend_MapsMonthlyPurchaseToPremiumEntitlement()
        {
            var backend = new FakePurchasesBackend();
            var received = false;
            backend.EntitlementsChanged += entitlements =>
            {
                received = entitlements.Contains(Constants.ENTITLEMENT_PREMIUM);
            };

            backend.Purchase(RevenueCatIds.OfferingDefault, RevenueCatIds.PackageMonthly, outcome =>
                Assert.That(outcome, Is.EqualTo(PurchaseOutcome.Success)));

            Assert.That(received, Is.True);
        }

        [Test]
        public void FakeBackend_RejectsPackageFromWrongOffering()
        {
            var backend = new FakePurchasesBackend();
            backend.Purchase(RevenueCatIds.OfferingBreeds, RevenueCatIds.PackageMonthly, outcome =>
                Assert.That(outcome, Is.EqualTo(PurchaseOutcome.Error)));
        }

        [Test]
        public void FakeBackend_PreservesEntitlementsAcrossRefresh()
        {
            var backend = new FakePurchasesBackend();
            backend.Purchase(RevenueCatIds.OfferingDefault, RevenueCatIds.PackageMonthly, _ => { });

            var refreshedEntitlements = false;
            backend.EntitlementsChanged += entitlements =>
                refreshedEntitlements = entitlements.Contains(Constants.ENTITLEMENT_PREMIUM);

            backend.RefreshCustomerInfo(_ => { });
            Assert.That(refreshedEntitlements, Is.True);
        }

        [Test]
        public void FakeBackend_UnknownPackageReturnsError()
        {
            var backend = new FakePurchasesBackend();
            backend.Purchase(RevenueCatIds.OfferingDefault, "unknown_package", outcome =>
                Assert.That(outcome, Is.EqualTo(PurchaseOutcome.Error)));
        }

        [Test]
        public void FakeBackend_CancellationRemainsDistinctFromError()
        {
            var backend = new FakePurchasesBackend();
            backend.CancelNextPurchase();
            backend.Purchase(RevenueCatIds.OfferingDefault, RevenueCatIds.PackageMonthly, outcome =>
                Assert.That(outcome, Is.EqualTo(PurchaseOutcome.Cancelled)));
        }
    }
}
