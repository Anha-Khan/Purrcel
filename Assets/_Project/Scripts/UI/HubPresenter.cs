using CatCourier.Audio;
using CatCourier.Core;
using CatCourier.Monetization;
using CatCourier.Progression;
using CatCourier.Weather;
using UnityEngine;

namespace CatCourier.UI
{
    public enum HubTab
    {
        Run,
        Upgrades,
        Cats,
        Leaderboard
    }

    public static class HubTabs
    {
        public static HubTab Active { get; set; } = HubTab.Run;
    }

    /// <summary>
    /// Reads Hub state and calls contract methods. It owns no economy, progression, or run state.
    /// </summary>
    public sealed class HubPresenter : MonoBehaviour
    {
        [SerializeField] private WeatherManager weather;
        [SerializeField] private CatBreedManager catBreeds;

        private string restoreMessage = string.Empty;
        private string premiumMessage = string.Empty;

        private void Awake()
        {
            weather ??= FindObjectOfType<WeatherManager>();
            catBreeds ??= FindObjectOfType<CatBreedManager>();
            HubTabs.Active = HubTab.Run;
        }

        private void OnEnable()
        {
            if (catBreeds != null)
            {
                catBreeds.OnSelectionChanged += HandleCatSelectionChanged;
            }

            var checker = EntitlementChecker.Instance;
            if (checker != null)
            {
                checker.OnEntitlementsChanged += HandleEntitlementsChanged;
            }
        }

        private void OnDisable()
        {
            if (catBreeds != null)
            {
                catBreeds.OnSelectionChanged -= HandleCatSelectionChanged;
            }

            var checker = EntitlementChecker.Instance;
            if (checker != null)
            {
                checker.OnEntitlementsChanged -= HandleEntitlementsChanged;
            }
        }

        private void OnGUI()
        {
            DrawCoinTopBar();
            DrawTabBar();

            var area = new Rect(24f, 84f, Screen.width - 48f, Screen.height - 120f);
            GUILayout.BeginArea(area);
            switch (HubTabs.Active)
            {
                case HubTab.Run:
                    DrawRunTab();
                    break;
                case HubTab.Upgrades:
                case HubTab.Cats:
                case HubTab.Leaderboard:
                    break;
            }

            GUILayout.EndArea();

            DrawFooter();
        }

        private static void DrawCoinTopBar()
        {
            var rect = new Rect(16f, 12f, 220f, 56f);
            GUILayout.BeginArea(rect, GUI.skin.box);
            GUILayout.Label($"Coins: {SaveSystem.Instance?.TotalCoins ?? 0}");
            GUILayout.EndArea();
        }

        private static void DrawTabBar()
        {
            var rect = new Rect(Screen.width - 460f, 12f, 444f, 40f);
            GUILayout.BeginArea(rect);
            GUILayout.BeginHorizontal();
            DrawTabButton("Run", HubTab.Run);
            DrawTabButton("Upgrades", HubTab.Upgrades);
            DrawTabButton("Cats", HubTab.Cats);
            DrawTabButton("Leaderboard", HubTab.Leaderboard);
            GUILayout.EndHorizontal();
            GUILayout.EndArea();
        }

        private static void DrawTabButton(string label, HubTab tab)
        {
            var active = HubTabs.Active == tab;
            var previous = GUI.backgroundColor;
            if (active)
            {
                GUI.backgroundColor = Color.cyan;
            }

            if (GUILayout.Button(label, GUILayout.Height(32f)))
            {
                HubTabs.Active = tab;
                AudioManager.Instance?.PlaySfx(SfxId.UiTap);
            }

            GUI.backgroundColor = previous;
        }

        private void DrawRunTab()
        {
            if (GUILayout.Button("START RUN", GUILayout.Height(56f)))
            {
                AudioManager.Instance?.PlaySfx(SfxId.UiTap);
                GameManager.Instance?.StartRun();
            }

            if (GameManager.Instance != null && GameManager.Instance.IsStartPending)
            {
                GUILayout.Label("Preparing store services...");
            }

            var nextWeather = weather != null ? weather.NextRunWeather.ToString() : nameof(WeatherType.Clear);
            GUILayout.Label($"Next weather: {nextWeather}");
            DrawDistrictStatus();
            DrawSelectedCat();
            DrawPremiumEntry();
            DrawRestoreResult();
        }

        private static void DrawDistrictStatus()
        {
            GUILayout.Label("Districts");
            var unlocked = SaveSystem.Instance?.Data?.unlockedDistrictIds;
            var catalog = PaywallPresenter.CatalogReference;
            foreach (var district in new[] { DistrictId.OldTown, DistrictId.Downtown, DistrictId.Harbour, DistrictId.Suburbs })
            {
                if (district <= DistrictId.Downtown)
                {
                    GUILayout.Label($"  {district}: Unlocked");
                    continue;
                }

                var hasContent = catalog != null && catalog.HasVariants(district, ChunkType.SmallGap);
                if (!hasContent)
                {
                    GUILayout.Label($"  {district}: Not shipped yet");
                    continue;
                }

                var isUnlocked = unlocked != null && unlocked.Contains(district.ToString());
                GUILayout.Label($"  {district}: {(isUnlocked ? "Unlocked" : "Reach further to unlock")}");
            }
        }

        private void DrawSelectedCat()
        {
            var selected = catBreeds != null ? catBreeds.SelectedId : SaveSystem.Instance?.Data?.selectedCatBreedId;
            GUILayout.Label($"Selected cat: {(string.IsNullOrEmpty(selected) ? "Tabby" : selected)}");
            if (GUILayout.Button("Choose Cat", GUILayout.Height(34f)))
            {
                HubTabs.Active = HubTab.Cats;
                AudioManager.Instance?.PlaySfx(SfxId.UiTap);
            }
        }

        private void DrawPremiumEntry()
        {
            var checker = EntitlementChecker.Instance;
            var isPremium = checker != null && checker.IsPremium;
            GUILayout.Label(isPremium
                ? "Premium active: no ads, 3 continues per run, 2x coins."
                : "Free player: ads enabled, 1 continue per run.");
            if (!string.IsNullOrEmpty(premiumMessage))
            {
                GUILayout.Label(premiumMessage);
            }

            if (!isPremium && GUILayout.Button("Go Premium"))
            {
                AudioManager.Instance?.PlaySfx(SfxId.UiTap);
                PaywallGate.Request(PaywallSource.Settings);
            }
        }

        private void DrawRestoreResult()
        {
            GUILayout.Label("Restore purchases");
            if (GUILayout.Button("Restore"))
            {
                AudioManager.Instance?.PlaySfx(SfxId.UiTap);
                restoreMessage = "Restoring...";
                var revenueCat = RevenueCatManager.Instance;
                if (revenueCat == null)
                {
                    restoreMessage = "Purchases unavailable.";
                }
                else
                {
                    revenueCat.Restore(success => restoreMessage = success ? "Purchases restored." : "Nothing to restore.");
                }
            }

            if (!string.IsNullOrEmpty(restoreMessage))
            {
                GUILayout.Label(restoreMessage);
            }

            var ads = AdManager.Instance;
            if (ads != null && !string.IsNullOrEmpty(ads.StatusMessage))
            {
                GUILayout.Label($"Ads: {ads.StatusMessage}");
            }
        }

        private void DrawFooter()
        {
            var rect = new Rect(24f, Screen.height - 44f, 520f, 32f);
            GUILayout.BeginArea(rect);
            GUILayout.BeginHorizontal();
            var muted = AudioManager.Instance != null && AudioManager.Instance.IsMuted;
            if (GUILayout.Button(muted ? "Unmute" : "Mute", GUILayout.Width(110f)))
            {
                AudioManager.Instance?.SetMuted(!muted);
            }

            if (GUILayout.Button("Settings / Paywall", GUILayout.Width(180f)))
            {
                PaywallGate.Request(PaywallSource.Settings);
            }

            GUILayout.EndHorizontal();
            GUILayout.EndArea();
        }

        private void HandleCatSelectionChanged()
        {
            AudioManager.Instance?.PlaySfx(SfxId.UiTap);
        }

        private void HandleEntitlementsChanged()
        {
            premiumMessage = EntitlementChecker.Instance != null && EntitlementChecker.Instance.IsPremium
                ? "Premium unlocked."
                : string.Empty;
        }
    }
}
