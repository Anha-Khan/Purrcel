using System;
using System.Collections.Generic;
using CatCourier.Core;

namespace CatCourier.Monetization
{
    public interface IPurchasesBackend
    {
        event Action<HashSet<string>> EntitlementsChanged;

        void Initialize(string publicKey, string appUserId, Action<bool> completed);
        void GetOffering(string offeringId, Action<bool, PaywallOffering> done);
        void GetPackage(string offeringId, string packageId, Action<bool, PaywallPackage> done);
        void Purchase(string offeringId, string packageId, Action<PurchaseOutcome> done);
        void Restore(Action<bool> done);
        void RefreshCustomerInfo(Action<bool> done);
        void Dispose();
    }
}
