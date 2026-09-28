using System;
using System.Collections.Generic;
using UnityEngine;
using CatCourier.Core;

namespace CatCourier.Monetization
{
    public sealed class EntitlementChecker : MonoBehaviour
    {
        public static EntitlementChecker Instance { get; private set; }
        public bool IsPremium { get; private set; }
        public event Action OnEntitlementsChanged;

        private readonly HashSet<string> activeEntitlements = new();

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
            PersistIfRoot();
        }

        private void PersistIfRoot()
        {
            // Managers are parented under PersistentSystems, which is already DontDestroyOnLoad.
            if (transform.parent == null)
            {
                DontDestroyOnLoad(gameObject);
            }
        }

        public bool HasBreedPack(string packId) => activeEntitlements.Contains(packId);
        public bool HasDistrict(DistrictId district) => activeEntitlements.Contains(DistrictEntitlementId(district));

        public void RefreshAll(Action<bool> completed = null)
        {
            if (RevenueCatManager.Instance == null)
            {
                completed?.Invoke(false);
                return;
            }

            RevenueCatManager.Instance.RefreshCustomerInfo(completed ?? (_ => { }));
        }

        public void SetEntitlements(IEnumerable<string> entitlements)
        {
            activeEntitlements.Clear();
            if (entitlements != null)
            {
                activeEntitlements.UnionWith(entitlements);
            }

            IsPremium = activeEntitlements.Contains(Constants.ENTITLEMENT_PREMIUM);
            OnEntitlementsChanged?.Invoke();
        }

        private void OnDestroy()
        {
            if (Instance == this)
            {
                Instance = null;
            }
        }

        private static string DistrictEntitlementId(DistrictId district)
        {
            return district switch
            {
                DistrictId.Harbour => Constants.ENTITLEMENT_HARBOUR,
                DistrictId.Suburbs => Constants.ENTITLEMENT_SUBURBS,
                _ => string.Empty
            };
        }
    }
}
