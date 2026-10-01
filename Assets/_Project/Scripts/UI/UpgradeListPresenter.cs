using CatCourier.Art;
using CatCourier.Audio;
using CatCourier.Core;
using CatCourier.Progression;
using UnityEngine;

namespace CatCourier.UI
{
    /// <summary>
    /// Lists upgrades and forwards purchase intent to <see cref="UpgradeManager"/>. It never
    /// changes levels, coins, or stats itself.
    /// </summary>
    public sealed class UpgradeListPresenter : MonoBehaviour
    {
        [SerializeField] private UpgradeManager upgrades;

        private void Awake()
        {
            upgrades ??= FindObjectOfType<UpgradeManager>();
        }

        private void OnGUI()
        {
            if (HubTabs.Active != HubTab.Upgrades)
            {
                return;
            }

            upgrades ??= FindObjectOfType<UpgradeManager>();
            HubLayout.BeginContent(HubTab.Upgrades);
            GUILayout.Label($"COINS  {SaveSystem.Instance?.TotalCoins ?? 0}", HubSkin.Heading,
                GUILayout.Height(34f * UiTheme.Scale));
            GUILayout.Space(6f * UiTheme.Scale);

            var configs = upgrades?.Upgrades;
            if (configs == null || configs.Count == 0)
            {
                GUILayout.Label("No upgrade data is registered yet.", HubSkin.Row);
                HubLayout.EndContent();
                return;
            }

            var scale = UiTheme.Scale;
            var width = HubLayout.ContentRect.width;

            foreach (var config in configs)
            {
                if (config == null || string.IsNullOrEmpty(config.id))
                {
                    continue;
                }

                var level = upgrades.GetLevel(config.id);
                var cost = upgrades.GetNextCost(config.id);
                var canAfford = cost >= 0 && upgrades.CanAfford(config.id);
                var maxed = cost < 0;

                // The card is sized from the text it has to hold, not from a fixed 70px.
                // Descriptions are authored copy that wraps: at 260px a two-line description
                // pushed its second line outside the row and was clipped. Measuring first
                // means a longer description grows the card instead of disappearing.
                var cardWidth = width - 8f * scale;
                var textInset = 70f * scale;
                var sideBlock = 220f * scale;
                var textWidth = Mathf.Max(40f * scale, cardWidth - textInset - sideBlock - 8f * scale);

                var nameHeight = UiTheme.LineHeight(HubSkin.Heading);
                var description = config.description ?? string.Empty;
                var descriptionHeight = UiTheme.MeasureHeight(HubSkin.Small, description, textWidth);
                var cardHeight = 9f * scale + nameHeight + 4f * scale + descriptionHeight + 9f * scale;

                // Card rect comes FROM the layout, so the list scrolls.
                var card = GUILayoutUtility.GetRect(cardWidth, Mathf.Max(70f * scale, cardHeight),
                    GUILayout.Width(cardWidth));
                UiTheme.DrawTexture(card, UiTheme.Solid(UiTheme.Fade(HubSkin.PaperEdge,
                    maxed ? 0.10f : 0.20f)));

                var artIndex = config.id.Contains("jump") ? 0
                    : config.id.Contains("sprint") || config.id.Contains("speed") ? 1 : 2;
                GeneratedUiSprite.Draw(
                    new Rect(card.x + 14f * scale, card.y + 14f * scale, 42f * scale, 42f * scale),
                    GeneratedArtCatalog.Frame(GeneratedArtCatalog.Active?.upgradeIcons, artIndex));

                var textX = card.x + textInset;
                UiTheme.LabelClipped(new Rect(textX, card.y + 9f * scale, textWidth, nameHeight),
                    config.upgradeName, HubSkin.Heading, HubSkin.Ink);
                if (descriptionHeight > 0f)
                {
                    UiTheme.Label(new Rect(textX, card.y + (13f * scale + nameHeight), textWidth,
                            descriptionHeight),
                        description, HubSkin.Small, HubSkin.InkSoft);
                }

                var meter = new Rect(card.xMax - sideBlock, card.y + 16f * scale,
                    108f * scale, 8f * scale);
                UiTheme.Bar(meter, config.maxLevel > 0 ? level / (float)config.maxLevel : 0f,
                    UiTheme.Fade(HubSkin.Leaf, 0.95f), UiTheme.Fade(HubSkin.InkSoft, 0.35f));
                UiTheme.Label(new Rect(meter.x, card.y + 28f * scale, 108f * scale,
                        UiTheme.LineHeight(HubSkin.Small)),
                    maxed ? "MAX" : $"LV {level}/{config.maxLevel}", HubSkin.Small, HubSkin.InkSoft);

                var buy = new Rect(card.xMax - 100f * scale, card.y + 14f * scale,
                    88f * scale, 40f * scale);
                if (maxed)
                {
                    HubSkin.Chip(buy, "MAXED", UiTheme.Muted);
                }
                else if (HubSkin.Pill(buy, $"{cost}",
                        canAfford ? UiTheme.Fade(HubSkin.Honey, 0.92f)
                            : UiTheme.Fade(UiTheme.Muted, 0.3f),
                        HubSkin.Ink, canAfford))
                {
                    if (canAfford)
                    {
                        AudioManager.Instance?.PlaySfx(SfxId.UiTap);
                        if (upgrades.TryPurchase(config.id))
                        {
                            AudioManager.Instance?.PlaySfx(SfxId.Purchase);
                        }
                    }
                    else
                    {
                        AudioManager.Instance?.PlaySfx(SfxId.UiTap);
                    }
                }

                GUILayout.Space(10f * scale);
            }

            HubLayout.EndContent();
        }
    }
}
