using System;
using System.Collections.Generic;
using CatCourier.Core;
using UnityEngine;

namespace CatCourier.Monetization
{
    public enum RevenueCatState
    {
        Uninitialized,
        Initializing,
        Ready,
        Degraded,
        Failed,
        Disposed
    }

    public sealed class RevenueCatManager : MonoBehaviour
    {
        public static RevenueCatManager Instance { get; private set; }
        public bool IsReady { get; private set; }
        public bool IsFakeBackend { get; private set; }
        public RevenueCatState State { get; private set; } = RevenueCatState.Uninitialized;

        public event Action<RevenueCatState> OnStateChanged;
        public event Action<string> OnError;

        private IPurchasesBackend backend;
        private bool initializing;

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

        public void Initialize(RevenueCatConfig config, bool useFakeBackend)
        {
            if (backend != null || initializing || State == RevenueCatState.Ready)
            {
                return;
            }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
            IsFakeBackend = useFakeBackend;
#else
            IsFakeBackend = false;
#endif
            initializing = true;
            SetState(RevenueCatState.Initializing);

#if UNITY_EDITOR || DEVELOPMENT_BUILD
            backend = IsFakeBackend ? (IPurchasesBackend)new FakePurchasesBackend() : new RealPurchasesBackend();
#else
            backend = new RealPurchasesBackend();
#endif
            if (backend == null)
            {
                Fail("RevenueCat backend could not be created.");
                return;
            }

            backend.EntitlementsChanged += PublishEntitlements;
            var key = config != null ? config.GetPublicKey() : string.Empty;
            backend.Initialize(key, null, HandleInitialized);
        }

        public void GetOffering(string offeringId, Action<bool, PaywallOffering> done)
        {
            if (backend == null)
            {
                done(false, default);
                return;
            }

            backend.GetOffering(offeringId, done);
        }

        public void GetPackage(string offeringId, string packageId, Action<bool, PaywallPackage> done)
        {
            if (backend == null)
            {
                done(false, default);
                return;
            }

            backend.GetPackage(offeringId, packageId, done);
        }

        public void Purchase(string offeringId, string packageId, Action<PurchaseOutcome> done)
        {
            if (backend == null)
            {
                done(PurchaseOutcome.Error);
                return;
            }

            backend.Purchase(offeringId, packageId, outcome =>
            {
                // A cancelled purchase is a player choice, not a store fault, so it
                // must not report the SDK as degraded.
                if (outcome == PurchaseOutcome.Error)
                {
                    SetState(RevenueCatState.Degraded);
                }

                done(outcome);
            });
        }

        public void Restore(Action<bool> done)
        {
            if (backend == null)
            {
                done(false);
                return;
            }

            backend.Restore(success =>
            {
                if (!success)
                {
                    SetState(RevenueCatState.Degraded);
                }

                done(success);
            });
        }

        public void RefreshCustomerInfo(Action<bool> done)
        {
            if (backend == null)
            {
                done(false);
                return;
            }

            backend.RefreshCustomerInfo(success =>
            {
                if (!success)
                {
                    SetState(RevenueCatState.Degraded);
                }

                done(success);
            });
        }

        public void ConfigureSdk(string publicKey, string appUserId, Action<bool> completed)
        {
            if (backend == null || initializing)
            {
                completed(false);
                return;
            }

            initializing = true;
            backend.Initialize(publicKey, appUserId, initialized =>
            {
                HandleInitialized(initialized);
                completed(initialized);
            });
        }

        private void HandleInitialized(bool initialized)
        {
            initializing = false;
            IsReady = initialized;
            if (initialized)
            {
                SetState(RevenueCatState.Ready);
                return;
            }

            if (backend != null)
            {
                backend.EntitlementsChanged -= PublishEntitlements;
                backend.Dispose();
                backend = null;
            }

            Fail("RevenueCat initialization failed.");
        }

        private void Fail(string message)
        {
            initializing = false;
            IsReady = false;
            Debug.LogError(message);
            OnError?.Invoke(message);
            SetState(RevenueCatState.Failed);
        }

        private void SetState(RevenueCatState state)
        {
            if (State == state)
            {
                return;
            }

            State = state;
            OnStateChanged?.Invoke(state);
        }

        private void OnDestroy()
        {
            if (backend != null)
            {
                backend.EntitlementsChanged -= PublishEntitlements;
                backend.Dispose();
                backend = null;
            }

            IsReady = false;
            initializing = false;
            SetState(RevenueCatState.Disposed);
            if (Instance == this)
            {
                Instance = null;
            }
        }

        private void PublishEntitlements(HashSet<string> entitlements)
        {
            EntitlementChecker.Instance?.SetEntitlements(entitlements);
        }
    }
}
