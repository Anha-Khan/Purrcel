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

        private const float EntranceSeconds = 0.4f;
        private float shownAt = -1f;

        private void OnGUI()
        {
            if (!isOpen)
            {
                shownAt = -1f;
                return;
            }

            if (shownAt < 0f)
            {
                shownAt = Time.unscaledTime;
            }

            var scale = UiTheme.Scale;
            var entrance = UiMotion.EaseOutCubic(UiMotion.Progress(shownAt, EntranceSeconds));

            // The old paywall was an unscaled 420x520 box with a translucent skin, so the
            // hub text showed straight through it. It owns the screen now.
            UiTheme.Scrim(0.74f * entrance);

            var width = Mathf.Min(520f * scale, Screen.width - 40f * scale);
            var height = Mathf.Min(Screen.height - 60f * scale, Screen.height - 40f * scale);
            var target = new Rect(
                (Screen.width - width) * 0.5f,
                Screen.height - height - 24f * scale,
                width, height);
            var outer = UiTheme.ScaledAboutCentre(target, Mathf.Lerp(0.95f, 1f, entrance));
            var content = UiTheme.Panel(outer, entrance, HubSkin.Honey);

            var hero = new Rect(content.x, content.y, content.width, 46f * scale);
            UiTheme.Label(hero, "PURRCEL PRO", UiTheme.Title, HubSkin.Honey);

            var benefits = new Rect(content.x, hero.yMax, content.width, 44f * scale);
            UiTheme.Label(benefits, "No ads   -   3 continues per run   -   2x coins",
                UiTheme.Caption, UiTheme.Cream);

            // Explicit rects: the icon strip sits alongside the benefit line, and
            // GUILayoutUtility here would place it by a second, disagreeing layout pass.
            var iconSize = 40f * scale;
            for (var i = 0; i < 4; i++)
            {
                var frame = GeneratedArtCatalog.Frame(GeneratedArtCatalog.Active?.premiumBenefits, i);
                if (frame == null)
                {
                    continue;
                }

                var slot = new Rect(content.x + content.width - iconSize * (4 - i) - (3 - i) * 6f * scale,
                    hero.yMax + 2f * scale, iconSize, iconSize);
                GeneratedUiSprite.Draw(slot, frame);
            }

            var y = hero.yMax + 52f * scale;

            UiTheme.Label(new Rect(content.x, y, content.width, 24f * scale),
                status.ToUpperInvariant(), UiTheme.Caption, StatusTint());
            y += 28f * scale;

            // The fake-data banner is the single most important line in this dialog: a
            // judge must never mistake an editor price for a real one.
            if (revenueCat != null && revenueCat.IsFakeBackend)
            {
                var warn = new Rect(content.x, y, content.width, 28f * scale);
                UiTheme.DrawTexture(warn, UiTheme.Solid(UiTheme.Fade(UiTheme.Danger, 0.22f)));
                UiTheme.Label(warn, "DEVELOPMENT FAKE DATA - NOT A REAL PRICE",
                    UiTheme.Caption, UiTheme.Danger);
                y += 34f * scale;
            }

            // Pure rect flow, no scroll view. Rows advance y explicitly so they cannot drift out
            // of agreement with the panel, and a plain offering is two rows tall; the
            // tallest real offering is four, which fits the card without scrolling.
            var footerTop = content.yMax - 52f * scale;
            var listBottom = footerTop - 10f * scale;
            var rowHeight = 62f * scale;

            if (packages.Count == 0)
            {
                UiTheme.Label(new Rect(content.x, y + 8f * scale, content.width, 30f * scale),
                    NoPackagesCopy(status), UiTheme.Body, UiTheme.Muted);
            }

            var busy = status == "Purchasing";
            foreach (var package in packages)
            {
                if (y + rowHeight > listBottom)
                {
                    break;
                }

                var row = new Rect(content.x, y, content.width, rowHeight - 8f * scale);
                UiTheme.DrawTexture(row, UiTheme.Solid(UiTheme.Fade(UiTheme.Cream, 0.10f)));

                UiTheme.Label(new Rect(row.x + 14f * scale, row.y + 8f * scale, row.width * 0.58f, 26f * scale),
                    PackageTitle(package), UiTheme.Body, UiTheme.Cream);
                UiTheme.Label(new Rect(row.x + 14f * scale, row.y + 32f * scale, row.width * 0.58f, 22f * scale),
                    PackageSubtitle(package), UiTheme.Caption,
                    package.HasFreeTrial ? UiTheme.Gold : UiTheme.Muted);

                var buy = new Rect(row.xMax - 104f * scale, row.y + 10f * scale, 88f * scale, 38f * scale);
                if (!busy && UiTheme.FaceButton(buy, "BUY", HubSkin.Honey, UiTheme.Ink))
                {
                    Audio.AudioManager.Instance?.PlaySfx(SfxId.UiTap);
                    Buy(package);
                }

                y += rowHeight;
            }

            var closeRect = new Rect(content.x, content.yMax - 52f * scale,
                content.width * 0.5f - 6f * scale, 44f * scale);
            var restoreRect = new Rect(content.x + content.width * 0.5f + 6f * scale,
                content.yMax - 52f * scale, content.width * 0.5f - 6f * scale, 44f * scale);

            if (UiTheme.FaceButton(restoreRect, "RESTORE", UiTheme.Slate, UiTheme.Cream,
                    status != "Restoring"))
            {
                Audio.AudioManager.Instance?.PlaySfx(SfxId.UiTap);
                Restore();
            }

            if (UiTheme.FaceButton(closeRect, "CLOSE", UiTheme.Orange, UiTheme.Ink))
            {
                Audio.AudioManager.Instance?.PlaySfx(SfxId.UiTap);
                Close();
            }
        }

        private Color StatusTint()
        {
            switch (status)
            {
                case "Ready": return UiTheme.Gold;
                case "Purchased":
                case "Restored": return UiTheme.Teal;
                case "Error": return UiTheme.Danger;
                default: return UiTheme.Muted;
            }
        }

        private static string PackageTitle(PaywallPackage package)
        {
            var name = package.PackageId.Replace("$rc_", string.Empty).Replace('_', ' ');
            return name.ToUpperInvariant();
        }

        private static string PackageSubtitle(PaywallPackage package)
        {
            var price = $"{package.PriceString} / {package.PeriodLabel}";
            return package.HasFreeTrial ? $"{price}   -   free trial" : price;
        }

        private static string NoPackagesCopy(string currentStatus)
        {
            if (currentStatus == "Store unavailable. Configure RevenueCat keys and products.")
            {
                return "No store configured. Add a RevenueCat Test Store key, then rebuild.";
            }

            return "No packages available from this store right now.";
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
