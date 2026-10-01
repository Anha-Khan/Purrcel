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

        /// <summary>
        /// The Purrcel mark, drawn beside the wordmark. Lives in Resources rather than as a
        /// serialized reference so a fresh clone shows the real identity with no setup step.
        /// </summary>
        private const string MarkResourcePath = "PurrcelMark";

        private static Texture2D markTexture;

        private string restoreMessage = string.Empty;
        private string premiumMessage = string.Empty;
        private float entranceAt = -1f;

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

        private void Update()
        {
            if (entranceAt < 0f)
            {
                entranceAt = Time.unscaledTime;
            }
        }

        private void OnGUI()
        {
            if (entranceAt < 0f)
            {
                entranceAt = Time.unscaledTime;
            }

            var ease = UiMotion.EaseOutCubic(UiMotion.Progress(entranceAt, 0.5f));
            var safe = HubLayout.SafeRect;
            var scale = UiTheme.Scale;

            // Left column only: the painted street is half the appeal and must stay visible.
            var columnWidth = safe.width * 0.63f;
            var top = Screen.height - safe.yMax;

            DrawWordmark(safe, top, columnWidth, scale, ease);
            DrawCoinPurse(safe, columnWidth, scale, ease);
            DrawTabBar(safe, top, columnWidth, scale, ease);

            var card = new Rect(safe.x + 24f * scale, top + 176f * scale,
                columnWidth - 48f * scale, safe.height - 176f * scale - 88f * scale);
            card = UiTheme.ScaledAboutCentre(card, Mathf.Lerp(0.97f, 1f, ease));
            HubSkin.Card(card);

            var content = HubSkin.Inset(card);
            if (HubTabs.Active == HubTab.Run)
            {
                // No scroll view here. DrawRunTab positions every row by hand from the
                // content rect, and a scroll view introduces a second coordinate system
                // that silently disagrees with the absolute ones. The sibling tabs do use
                // GUILayout, so those keep theirs via BeginContent.
                DrawRunTab(content);
            }
            else if (!HasPresenterForActiveTab(HubTabs.Active))
            {
                // The Upgrades/Cats/Leaderboard panels are drawn by sibling presenters that
                // each call BeginContent on the same rect. Warn if one is missing, so a
                // disabled component reads as a bug instead of a silently blank tab.
                UiTheme.Label(content, $"The {HubTabs.Active} panel is not available in this build.",
                    HubSkin.Row, HubSkin.InkSoft);
            }

            DrawFooter(safe, scale);
        }

        /// <summary>
        /// The identity lockup: the Purrcel mark, then the wordmark.
        ///
        /// The mark is a drawn PNG rather than a glyph because it is artwork, not type — it
        /// comes from Assets/_Project/Resources/PurrcelMark.png. The wordmark stays live
        /// IMGUI text in Plus Jakarta Sans Bold, so it scales with the UI the way the rest
        /// of the hub does and needs no texture of its own.
        ///
        /// Both are the same colour (Cream) because the hub's ground here is the painted
        /// street, not parchment: Ink-on-parchment would be invisible over the artwork.
        /// </summary>
        private void DrawWordmark(Rect safe, float top, float columnWidth, float scale, float ease)
        {
            var markSize = 44f * scale;
            var gap = 10f * scale;
            var left = safe.x + 30f * scale;
            var y = top + 20f * scale + (1f - ease) * -18f * scale;

            // The mark is square artwork, so it is centred on the wordmark's cap band
            // rather than sharing its box — a square in a 58px-tall rect would sit low.
            var mark = new Rect(left, y + (58f * scale - markSize) * 0.5f, markSize, markSize);
            UiTheme.DrawTexture(mark, MarkTexture());

            var rect = new Rect(left + markSize + gap, y,
                Mathf.Max(40f * scale, columnWidth * 0.6f - markSize - gap), 58f * scale);
            var lifted = UiTheme.Offset(rect, 0f, 2f * scale);
            UiTheme.Label(lifted, "PURRCEL", HubSkin.Wordmark, UiTheme.Fade(UiTheme.Ink, 0.35f));
            UiTheme.Label(rect, "PURRCEL", HubSkin.Wordmark, UiTheme.Cream);
        }

        /// <summary>
        /// The mark, from Resources so a fresh clone needs no scene wiring. Null is safe:
        /// DrawTexture ignores it and the wordmark simply sits on its own.
        /// </summary>
        private static Texture2D MarkTexture()
        {
            if (markTexture == null)
            {
                markTexture = Resources.Load<Texture2D>(MarkResourcePath);
            }

            return markTexture;
        }

        /// <summary>The purse reads as a coin chip rather than a label.</summary>
        private void DrawCoinPurse(Rect safe, float columnWidth, float scale, float ease)
        {
            var coins = SaveSystem.Instance?.TotalCoins ?? 0;
            var width = 168f * scale;
            var rect = new Rect(
                safe.x + columnWidth - width - 30f * scale,
                Screen.height - safe.yMax + 26f * scale + (1f - ease) * -18f * scale,
                width, 44f * scale);

            var lift = 3f * scale;
            UiTheme.DrawTexture(UiTheme.Offset(rect, 0f, lift),
                UiTheme.Solid(UiTheme.Fade(UiTheme.Ink, 0.3f)));
            UiTheme.DrawTexture(rect, UiTheme.Solid(HubSkin.Honey));
            UiTheme.DrawTexture(new Rect(rect.x, rect.y, Mathf.Max(2f, 5f * scale), rect.height),
                UiTheme.Solid(UiTheme.Brighten(HubSkin.Honey, 0.12f)));
            UiTheme.Label(rect, coins.ToString("N0"), HubSkin.TabActive, UiTheme.Fade(HubSkin.Ink, 1f));

            var dot = new Rect(rect.x + 14f * scale, rect.center.y - 7f * scale, 14f * scale, 14f * scale);
            UiTheme.DrawTexture(dot, UiTheme.Solid(UiTheme.Fade(HubSkin.Ink, 0.75f)));
        }

        private void DrawTabBar(Rect safe, float top, float columnWidth, float scale, float ease)
        {
            var width = columnWidth - 48f * scale;
            var tabWidth = (width - 12f * scale) * 0.25f;
            var y = top + 100f * scale + (1f - ease) * -14f * scale;
            var height = Mathf.Max(UiTheme.Touch * 0.85f, 46f * scale);

            DrawTabButton(new Rect(safe.x + 24f * scale, y, tabWidth, height), "Run", HubTab.Run, scale);
            DrawTabButton(new Rect(safe.x + 24f * scale + tabWidth + 4f * scale, y, tabWidth, height),
                "Upgrades", HubTab.Upgrades, scale);
            DrawTabButton(new Rect(safe.x + 24f * scale + (tabWidth + 4f * scale) * 2f, y, tabWidth, height),
                "Cats", HubTab.Cats, scale);
            DrawTabButton(new Rect(safe.x + 24f * scale + (tabWidth + 4f * scale) * 3f, y, tabWidth, height),
                "Leaders", HubTab.Leaderboard, scale);
        }

        private void DrawTabButton(Rect rect, string label, HubTab tab, float scale)
        {
            if (HubTabs.Active == tab)
            {
                // The selected tab lifts and goes terracotta, so the active section is
                // obvious without relying on a colour shift alone.
                var lifted = UiTheme.Offset(rect, 0f, -3f * scale);
                HubSkin.Pill(lifted, label, HubSkin.Terracotta);
                return;
            }

            if (HubSkin.Pill(rect, label, UiTheme.Fade(UiTheme.Cream, 0.16f), UiTheme.Cream))
            {
                HubTabs.Active = tab;
                AudioManager.Instance?.PlaySfx(SfxId.UiTap);
            }
        }

        /// <summary>
        /// Whether the active tab has a presenter that will actually draw into the shared
        /// content rect. Disabling one of those components used to produce a silently
        /// blank panel; the OnGUI path reports it instead.
        /// </summary>
        public static bool HasPresenterForActiveTab(HubTab tab)
        {
            switch (tab)
            {
                case HubTab.Upgrades:
                    return FindObjectOfType<UpgradeListPresenter>() != null;
                case HubTab.Cats:
                    return FindObjectOfType<CatSelectorPresenter>() != null;
                case HubTab.Leaderboard:
                    return FindObjectOfType<LeaderboardPresenter>() != null;
                default:
                    return true;
            }
        }

        /// <summary>
        /// Asymmetric on purpose: the primary action and the run's own state own the wider
        /// left column, and the account/progression material sits in a narrower right one.
        /// A single full-width stack of equal-weight rows made this card read as a form.
        ///
        /// Every row height and every button width comes from RunTabMetrics, and
        /// RunTabLayoutTests asserts both columns still fit their card. Laying this out
        /// with inline literals is what let the first version overlap itself.
        /// </summary>
        private void DrawRunTab(Rect content)
        {
            var scale = UiTheme.Scale;
            var gap = RunTabMetrics.ButtonGap * 2f * scale;
            var leftWidth = (content.width - gap) * 0.56f;
            var rightWidth = content.width - gap - leftWidth;

            var left = new Rect(content.x, content.y, leftWidth, content.height);
            var right = new Rect(content.x + leftWidth + gap, content.y, rightWidth, content.height);

            DrawRunPrimary(left, scale);
            DrawRunSecondary(right, scale);
        }

        /// <summary>Left column: the thing the player came here to do.</summary>
        private void DrawRunPrimary(Rect column, float scale)
        {

            UiTheme.Label(new Rect(column.x, column.y, column.width, RunTabMetrics.Heading * scale),
                "NEXT DELIVERY", HubSkin.Heading, HubSkin.Terracotta);
            var y = column.y + (RunTabMetrics.Heading + RunTabMetrics.GapAfterHeading) * scale;

            // Terracotta with dark ink: at this width the button needs its own ground.
            var start = new Rect(column.x, y, column.width, RunTabMetrics.StartButton * scale);
            if (HubSkin.Pill(start, "START RUN", HubSkin.Terracotta, UiTheme.Ink))
            {
                AudioManager.Instance?.PlaySfx(SfxId.UiTap);
                GameManager.Instance?.StartRun();
            }

            y += (RunTabMetrics.StartButton + RunTabMetrics.GapAfterStart) * scale;
            if (GameManager.Instance != null && GameManager.Instance.IsStartPending)
            {
                UiTheme.Label(new Rect(column.x, y, column.width, 24f * scale),
                    "Preparing store services...", HubSkin.Small, HubSkin.InkSoft);
                y += 26f * scale;
            }

            // Weather is the one thing that makes a player think about what is coming, so
            // it gets a chip instead of another line of body text.
            var nextWeather = weather != null ? weather.NextRunWeather.ToString() : nameof(WeatherType.Clear);
            HubSkin.Chip(new Rect(column.x, y, column.width, RunTabMetrics.WeatherChip * scale),
                WeatherFace(nextWeather), WeatherTint(nextWeather));
            y += (RunTabMetrics.WeatherChip + RunTabMetrics.GapAfterChip) * scale;

            HubSkin.Rule(new Rect(column.x, y, column.width, RunTabMetrics.Rule * scale));
            y += (RunTabMetrics.Rule + RunTabMetrics.GapAfterRule) * scale;

            DrawDistrictStatus(column, y, scale);
        }

        /// <summary>Right column: who you are and what you can unlock.</summary>
        private void DrawRunSecondary(Rect column, float scale)
        {
            var columnLabelHeight = UiTheme.LineHeight(HubSkin.Small);
            UiTheme.Label(new Rect(column.x, column.y, column.width, columnLabelHeight),
                "YOUR RUN", HubSkin.Small, HubSkin.InkSoft);
            var y = column.y + columnLabelHeight + RunTabMetrics.GapAfterColumnLabel * scale;

            y = DrawSelectedCat(column, y, scale);
            y = DrawPremiumEntry(column, y, scale);
            DrawRestoreResult(column, y, scale);
        }

        private static string WeatherFace(string weather)
        {
            switch (weather)
            {
                case nameof(WeatherType.Rain): return "RAIN  -  slower, coins richer";
                case nameof(WeatherType.Night): return "NIGHT  -  lights low";
                case nameof(WeatherType.Wind): return "WIND  -  gusts";
                default: return "CLEAR  -  dry run";
            }
        }

        private static Color WeatherTint(string weather)
        {
            switch (weather)
            {
                case nameof(WeatherType.Rain): return new Color(0.31f, 0.52f, 0.72f, 1f);
                case nameof(WeatherType.Night): return new Color(0.36f, 0.36f, 0.55f, 1f);
                case nameof(WeatherType.Wind): return new Color(0.45f, 0.68f, 0.55f, 1f);
                default: return HubSkin.Honey;
            }
        }

        private static float DrawDistrictStatus(Rect content, float y, float scale)
        {

            UiTheme.Label(new Rect(content.x, y, content.width, RunTabMetrics.DistrictsLabel * scale), "DISTRICTS",
                HubSkin.Small, HubSkin.InkSoft);
            y += (RunTabMetrics.DistrictsLabel + RunTabMetrics.GapAfterDistrictsLabel) * scale;

            var unlocked = SaveSystem.Instance?.Data?.unlockedDistrictIds;
            var catalog = PaywallPresenter.CatalogReference;
            var dot = 9f * scale;
            foreach (var district in new[]
                     {
                         DistrictId.OldTown, DistrictId.Downtown, DistrictId.Harbour, DistrictId.Suburbs
                     })
            {
                string label;
                Color tint;
                if (district <= DistrictId.Downtown)
                {
                    label = district.ToString();
                    tint = HubSkin.Leaf;
                }
                else
                {
                    var hasContent = catalog != null && catalog.HasVariants(district, ChunkType.SmallGap);
                    if (!hasContent)
                    {
                        label = $"{district} - not shipped yet";
                        tint = UiTheme.Muted;
                    }
                    else if (unlocked != null && unlocked.Contains(district.ToString()))
                    {
                        label = district.ToString();
                        tint = HubSkin.Leaf;
                    }
                    else
                    {
                        label = $"{district} - reach further";
                        tint = UiTheme.Muted;
                    }
                }

                var row = new Rect(content.x, y, content.width, RunTabMetrics.DistrictRow * scale);
                UiTheme.DrawTexture(new Rect(row.x + 2f * scale, row.center.y - dot * 0.5f, dot, dot),
                    UiTheme.Solid(tint));
                // The dot needs its own gutter, so the label starts after it rather than
                // spanning the full row and running under the next row's dot.
                UiTheme.Label(new Rect(row.x + 18f * scale, row.y,
                        row.width - 18f * scale, row.height),
                    label, HubSkin.Row, tint == UiTheme.Muted ? HubSkin.InkSoft : HubSkin.Ink);
                y += RunTabMetrics.DistrictRow * scale;
            }

            HubSkin.Rule(new Rect(content.x, y, content.width, RunTabMetrics.TrailingRule * scale));
            return y + (RunTabMetrics.TrailingRule + RunTabMetrics.GapAfterTrailingRule) * scale;
        }

        private float DrawSelectedCat(Rect content, float y, float scale)
        {
            var selected = catBreeds != null ? catBreeds.SelectedId : SaveSystem.Instance?.Data?.selectedCatBreedId;
            var breed = catBreeds?.ActiveBreed;
            var name = breed != null && !string.IsNullOrEmpty(breed.breedName)
                ? breed.breedName
                : string.IsNullOrEmpty(selected) ? "Tabby" : selected;

            // The name gets the full column width and CHANGE sits beneath it. Sharing the
            // row left the name 115px, and "Ginger Tabby" needs 153px at this size, so the
            // name either wrapped out of its box or truncated.
            var labelHeight = UiTheme.LineHeight(HubSkin.Small);
            var nameHeight = UiTheme.LineHeight(HubSkin.Heading);

            UiTheme.Label(new Rect(content.x, y, content.width, labelHeight), "YOUR CAT",
                HubSkin.Small, HubSkin.InkSoft);
            y += (labelHeight + RunTabMetrics.GapAfterCatLabel * scale);

            UiTheme.LabelClipped(new Rect(content.x, y, content.width, nameHeight),
                name, HubSkin.Heading, HubSkin.Ink);
            y += (nameHeight + RunTabMetrics.GapAfterCatName * scale);

            var button = new Rect(content.x, y, content.width, RunTabMetrics.ChangeButton * scale);
            if (HubSkin.Pill(button, "CHANGE", UiTheme.Fade(UiTheme.Cream, 0.55f), HubSkin.Ink))
            {
                HubTabs.Active = HubTab.Cats;
                AudioManager.Instance?.PlaySfx(SfxId.UiTap);
            }

            return y + (RunTabMetrics.ChangeButton + RunTabMetrics.GapAfterChange) * scale;
        }

        private float DrawPremiumEntry(Rect content, float y, float scale)
        {
            var checker = EntitlementChecker.Instance;
            var isPremium = checker != null && checker.IsPremium;

            // Premium is the thing being sold, so it gets a honey panel rather than
            // sharing the flat body text everything else uses. The panel is tall enough to
            // stack title, subtitle and a full-width button: this column is only ~230px, so
            // a button beside the text left no room for either.
            var panel = new Rect(content.x, y, content.width, RunTabMetrics.PremiumPanel * scale);
            UiTheme.DrawTexture(panel, UiTheme.Solid(isPremium
                ? UiTheme.Fade(HubSkin.Leaf, 0.18f)
                : UiTheme.Fade(HubSkin.Honey, 0.24f)));

            var pad = 12f * scale;
            var textWidth = panel.width - pad * 2f;
            var textY = panel.y + pad;

            UiTheme.Label(new Rect(panel.x + pad, textY, textWidth, 26f * scale),
                isPremium ? "PREMIUM ACTIVE" : "GO PREMIUM",
                HubSkin.Heading, isPremium ? HubSkin.Leaf : HubSkin.Terracotta);

            // Subtitle sits below the title's band, not inside it.
            var subY = textY + 30f * scale;
            UiTheme.Label(new Rect(panel.x + pad, subY, textWidth, 22f * scale),
                "No ads, 3 continues, 2x coins", HubSkin.Small, HubSkin.InkSoft);

            if (!string.IsNullOrEmpty(premiumMessage))
            {
                UiTheme.Label(new Rect(panel.x + pad, subY + 24f * scale, textWidth, 22f * scale),
                    premiumMessage, HubSkin.Small, HubSkin.Leaf);
            }
            else if (!isPremium)
            {
                var buy = new Rect(panel.x + pad, panel.yMax - pad - 36f * scale,
                    textWidth, 36f * scale);
                if (HubSkin.Pill(buy, "BUY", HubSkin.Terracotta))
                {
                    AudioManager.Instance?.PlaySfx(SfxId.UiTap);
                    PaywallGate.Request(PaywallSource.Settings);
                }
            }

            return y + (RunTabMetrics.PremiumPanel + RunTabMetrics.GapAfterPremium) * scale;
        }

        private void DrawRestoreResult(Rect content, float y, float scale)
        {
            var button = new Rect(content.x, y, 120f * scale, 36f * scale);
            if (HubSkin.Pill(button, "RESTORE", UiTheme.Fade(UiTheme.Cream, 0.5f), HubSkin.Ink))
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
                    revenueCat.Restore(success =>
                        restoreMessage = success ? "Purchases restored." : "Nothing to restore.");
                }
            }

            var ads = AdManager.Instance;
            var status = ads != null && !string.IsNullOrEmpty(ads.StatusMessage)
                ? ads.StatusMessage
                : string.Empty;
            if (!string.IsNullOrEmpty(restoreMessage) || !string.IsNullOrEmpty(status))
            {
                UiTheme.Label(new Rect(content.x + 132f * scale, y, content.width - 132f * scale, 36f * scale),
                    string.IsNullOrEmpty(restoreMessage) ? status : restoreMessage,
                    HubSkin.Small, HubSkin.InkSoft);
            }
        }

        private void DrawFooter(Rect safe, float scale)
        {
            var width = safe.width * 0.63f - 48f * scale;
            var height = 40f * scale;
            var y = Screen.height - safe.y - height - 12f * scale;
            var muted = AudioManager.Instance != null && AudioManager.Instance.IsMuted;

            if (HubSkin.Pill(new Rect(safe.x + 24f * scale, y, 100f * scale, height),
                    muted ? "UNMUTE" : "MUTE", UiTheme.Fade(UiTheme.Cream, 0.22f), UiTheme.Cream))
            {
                AudioManager.Instance?.SetMuted(!muted);
            }

            if (HubSkin.Pill(new Rect(safe.x + 132f * scale, y, width - 108f * scale, height),
                    "PAYWALL", UiTheme.Fade(UiTheme.Cream, 0.22f), UiTheme.Cream))
            {
                PaywallGate.Request(PaywallSource.Settings);
            }
        }

        private void HandleCatSelectionChanged()
        {
            AudioManager.Instance?.PlaySfx(SfxId.UiTap);
        }

        /// <summary>
        /// 1x at the reference size, larger on a bigger screen. Now owned by
        /// <see cref="UiTheme"/> so the hub and the in-run surfaces cannot drift apart.
        /// </summary>
        internal static float UiScale() => UiTheme.Scale;

        private void HandleEntitlementsChanged()
        {
            premiumMessage = EntitlementChecker.Instance != null && EntitlementChecker.Instance.IsPremium
                ? "Premium unlocked."
                : string.Empty;
        }
    }
}
