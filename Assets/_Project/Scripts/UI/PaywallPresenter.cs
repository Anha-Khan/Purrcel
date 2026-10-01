using CatCourier.Art;
using System;
using System.Collections.Generic;
using CatCourier.Core;
using CatCourier.Generation;
using CatCourier.Progression;
using CatCourier.Monetization;
using UnityEngine;

namespace CatCourier.UI
{
    public sealed class PaywallPresenter : MonoBehaviour
    {
        [SerializeField] private RevenueCatManager revenueCat;

        private readonly List<PaywallPackage> packages = new();
        private bool isOpen;
        private string status = "Idle";
        private PaywallSource? source;

        public bool IsOpen => isOpen;
        public IReadOnlyList<PaywallPackage> Packages => packages;
        public event Action OnStateChanged;

        private void Awake()
        {
            revenueCat ??= FindObjectOfType<RevenueCatManager>();
        }

        private void OnEnable()
        {
            revenueCat ??= FindObjectOfType<RevenueCatManager>();
            PaywallGate.OnRequested += Open;
            if (revenueCat != null)
                revenueCat.OnStateChanged += HandleRevenueCatStateChanged;
        }

        private void OnDisable()
        {
            PaywallGate.OnRequested -= Open;
            if (revenueCat != null)
                revenueCat.OnStateChanged -= HandleRevenueCatStateChanged;
        }

        public void Open(PaywallSource requestedSource)
        {
            source = requestedSource;
            isOpen = true;
            packages.Clear();
            status = revenueCat == null ? "Store service unavailable"
                : revenueCat.IsReady ? "Loading"
                : revenueCat.State == RevenueCatState.Failed || revenueCat.State == RevenueCatState.Degraded
                    ? "Store unavailable. Configure RevenueCat keys and products."
                    : "Connecting to store";
            OnStateChanged?.Invoke();
            LoadPackages();
        }

        public void Close()
        {
            isOpen = false;
            source = null;
            status = "Idle";
            OnStateChanged?.Invoke();
        }

        private void LoadPackages()
        {
            if (revenueCat == null || !revenueCat.IsReady)
            {
                return;
            }

            LoadOffering(RevenueCatIds.OfferingDefault);
            if (HasPaidBreedContent())
            {
                LoadOffering(RevenueCatIds.OfferingBreeds);
            }

            if (HasDistrictContent())
            {
                LoadOffering(RevenueCatIds.OfferingDistricts);
            }
        }

        private void HandleRevenueCatStateChanged(RevenueCatState state)
        {
            if (!isOpen)
                return;

            if (state == RevenueCatState.Ready)
            {
                packages.Clear();
                status = "Loading";
                LoadPackages();
            }
            else if (state == RevenueCatState.Failed || state == RevenueCatState.Degraded)
            {
                status = "Store unavailable. Configure RevenueCat keys and products.";
            }
            else
            {
                status = "Connecting to store";
            }
            OnStateChanged?.Invoke();
        }

        /// <summary>
        /// Whether a breed pack may be sold. Public because the "unfinished paid content
        /// is never sold" rule is a submission guarantee worth asserting from a test,
        /// and the test assembly cannot see internals.
        /// </summary>
        public static bool HasPaidBreedContent()
        {
            var breeds = FindObjectOfType<CatBreedManager>();
            if (breeds == null)
            {
                return false;
            }

            foreach (var breed in breeds.Breeds)
            {
                if (breed != null && breed.isIAP && !string.IsNullOrWhiteSpace(breed.iapPackId))
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>Assigned by project setup so paid district packs stay hidden while no chunks ship.</summary>
        public static ChunkCatalog CatalogReference { get; set; }

        /// <summary>
        /// Whether a district pack may be sold. Public for the same reason as
        /// <see cref="HasPaidBreedContent"/>: this is the gate that stops an unfinished
        /// district being sold, and it had no coverage at all.
        /// </summary>
        public static bool HasDistrictContent()
        {
            var catalog = CatalogReference ?? FindObjectOfType<ChunkManager>()?.Catalog;
            return catalog != null &&
                   (catalog.HasVariants(DistrictId.Harbour, ChunkType.SmallGap) || catalog.HasVariants(DistrictId.Suburbs, ChunkType.SmallGap));
        }

        private void LoadOffering(string offeringId)
        {
            revenueCat.GetOffering(offeringId, (found, offering) =>
            {
                if (!isOpen)
                {
                    return;
                }

                if (!found)
                {
                    status = "Error";
                    OnStateChanged?.Invoke();
                    return;
                }

                foreach (var package in offering.Packages)
                {
                    if (!packages.Exists(existing => existing.PackageId == package.PackageId))
                    {
                        packages.Add(package);
                    }
                }

                status = "Ready";
                OnStateChanged?.Invoke();
            });
        }

        private void OnGUI()
        {
            if (!isOpen)
            {
                return;
            }

            var rect = new Rect(40f, 40f, 420f, 520f);
            GUILayout.BeginArea(rect, GUI.skin.box);
            GeneratedUiSprite.Draw(GeneratedArtCatalog.Active?.premiumHero, 110f, 86f);
            GUILayout.Label($"Paywall ({source})");
            GUILayout.Label($"Status: {status}");
            GUILayout.BeginHorizontal();
            for (var i = 0; i < 4; i++)
                GeneratedUiSprite.Draw(GeneratedArtCatalog.Frame(GeneratedArtCatalog.Active?.premiumBenefits, i), 44f, 44f);
            GUILayout.EndHorizontal();
            if (revenueCat != null && revenueCat.IsFakeBackend)
            {
                GUILayout.Label("[DEVELOPMENT FAKE DATA]");
            }

            foreach (var package in packages)
            {
                GUILayout.BeginHorizontal();
                GUILayout.Label($"{package.PackageId}: {package.PriceString} / {package.PeriodLabel}");
                if (package.HasFreeTrial)
                {
                    GUILayout.Label("Free trial");
                }

                if (GUILayout.Button("Buy", GUILayout.Width(60f)))
                {
                    Audio.AudioManager.Instance?.PlaySfx(SfxId.UiTap);
                    Buy(package);
                }

                GUILayout.EndHorizontal();
            }

            if (GUILayout.Button("Restore Purchases"))
            {
                Audio.AudioManager.Instance?.PlaySfx(SfxId.UiTap);
                Restore();
            }

            if (GUILayout.Button("Close"))
            {
                Audio.AudioManager.Instance?.PlaySfx(SfxId.UiTap);
                Close();
            }

            GUILayout.EndArea();
        }

        private void Buy(PaywallPackage package)
        {
            if (revenueCat == null)
            {
                return;
            }

            status = "Purchasing";
            revenueCat.Purchase(OfferingFor(package.PackageId), package.PackageId, outcome =>
            {
                status = outcome switch
                {
                    PurchaseOutcome.Success => "Purchased",
                    PurchaseOutcome.Cancelled => "Cancelled",
                    _ => "Error"
                };
                if (outcome == PurchaseOutcome.Success)
                {
                    Audio.AudioManager.Instance?.PlaySfx(SfxId.Purchase);
                }

                OnStateChanged?.Invoke();
            });
        }

        private void Restore()
        {
            if (revenueCat == null)
            {
                return;
            }

            status = "Restoring";
            revenueCat.Restore(success =>
            {
                status = success ? "Restored" : "Error";
                OnStateChanged?.Invoke();
            });
        }

        private static string OfferingFor(string packageId) => RevenueCatIds.OfferingFor(packageId);
    }
}
