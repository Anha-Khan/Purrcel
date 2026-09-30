using CatCourier.Art;
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
        private Vector2 runScroll;
        private GUIStyle titleStyle;
        private GUIStyle tabStyle;
        private GUIStyle selectedTabStyle;
        private GUIStyle startStyle;
        private Texture2D tabTexture;
        private Texture2D selectedTexture;
        private Texture2D startTexture;

        private void Awake()
        {
            weather ??= FindObjectOfType<WeatherManager>();
            catBreeds ??= FindObjectOfType<CatBreedManager>();
            HubTabs.Active = HubTab.Run;
            if (GetComponent<HubBackdrop>() == null)
                gameObject.AddComponent<HubBackdrop>();
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
            EnsureStyles();
            var width = Screen.width * 0.63f;
            GUI.Label(new Rect(36f, 25f, width - 230f, 54f), "CAT COURIER", titleStyle);
            DrawCoinTopBar();
            DrawTabBar();

            var area = HubLayout.ContentRect;
            GUILayout.BeginArea(area);
            switch (HubTabs.Active)
            {
                case HubTab.Run:
                    runScroll = GUILayout.BeginScrollView(runScroll);
                    DrawRunTab();
                    GUILayout.EndScrollView();
                    break;
                case HubTab.Upgrades:
                case HubTab.Cats:
                case HubTab.Leaderboard:
                    break;
            }

            GUILayout.EndArea();

            DrawFooter();
        }

        private void DrawCoinTopBar()
        {
            var rect = new Rect(Screen.width * 0.63f - 183f, 34f, 150f, 38f);
            GUI.Box(rect, $"●  {SaveSystem.Instance?.TotalCoins ?? 0} coins", selectedTabStyle);
        }

        private void DrawTabBar()
        {
            var width = Screen.width * 0.63f - 64f;
            var tabWidth = (width - 18f) * 0.25f;
            DrawTabButton(new Rect(32f, 108f, tabWidth, 42f), "Run", HubTab.Run);
            DrawTabButton(new Rect(38f + tabWidth, 108f, tabWidth, 42f), "Upgrades", HubTab.Upgrades);
            DrawTabButton(new Rect(44f + tabWidth * 2f, 108f, tabWidth, 42f), "Cats", HubTab.Cats);
            DrawTabButton(new Rect(50f + tabWidth * 3f, 108f, tabWidth, 42f), "Leaders", HubTab.Leaderboard);
        }

        private void DrawTabButton(Rect rect, string label, HubTab tab)
        {
            if (GUI.Button(rect, label, HubTabs.Active == tab ? selectedTabStyle : tabStyle))
            {
                HubTabs.Active = tab;
                AudioManager.Instance?.PlaySfx(SfxId.UiTap);
            }
        }

        private void DrawRunTab()
        {
            GUILayout.Label("YOUR NEXT DELIVERY", titleStyle);
            GUILayout.Space(12f);
            if (GUILayout.Button("START RUN  →", startStyle, GUILayout.Height(68f)))
            {
                AudioManager.Instance?.PlaySfx(SfxId.UiTap);
                GameManager.Instance?.StartRun();
            }

            if (GameManager.Instance != null && GameManager.Instance.IsStartPending)
            {
                GUILayout.Label("Preparing store services...");
            }

            var nextWeather = weather != null ? weather.NextRunWeather.ToString() : nameof(WeatherType.Clear);
            GUILayout.Space(16f);
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
            var rect = new Rect(36f, Screen.height - 50f, Screen.width * 0.63f - 72f, 36f);
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

        private void EnsureStyles()
        {
            if (titleStyle != null) return;
            tabTexture = Solid(new Color(0.12f, 0.18f, 0.21f, 0.96f));
            selectedTexture = Solid(new Color(0.06f, 0.43f, 0.43f, 0.98f));
            startTexture = Solid(new Color(0.91f, 0.54f, 0.20f, 1f));
            titleStyle = new GUIStyle(GUI.skin.label) {
                fontSize = 32, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleLeft
            };
            titleStyle.normal.textColor = new Color(1f, 0.91f, 0.73f);
            tabStyle = new GUIStyle(GUI.skin.button) {
                fontSize = 16, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter
            };
            tabStyle.normal.background = tabTexture;
            tabStyle.normal.textColor = Color.white;
            selectedTabStyle = new GUIStyle(tabStyle);
            selectedTabStyle.normal.background = selectedTexture;
            startStyle = new GUIStyle(tabStyle) { fontSize = 22 };
            startStyle.normal.background = startTexture;
            startStyle.normal.textColor = new Color(0.15f, 0.12f, 0.08f);
            GUI.skin.label.normal.textColor = Color.white;
        }

        private static Texture2D Solid(Color color)
        {
            var texture = new Texture2D(1, 1, TextureFormat.RGBA32, false);
            texture.SetPixel(0, 0, color);
            texture.Apply();
            return texture;
        }

        private void OnDestroy()
        {
            if (tabTexture != null) Destroy(tabTexture);
            if (selectedTexture != null) Destroy(selectedTexture);
            if (startTexture != null) Destroy(startTexture);
        }

        private void HandleEntitlementsChanged()
        {
            premiumMessage = EntitlementChecker.Instance != null && EntitlementChecker.Instance.IsPremium
                ? "Premium unlocked."
                : string.Empty;
        }
    }
}
