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
            HubLayout.BeginContent();
            GUILayout.Label($"Coins: {SaveSystem.Instance?.TotalCoins ?? 0}");

            var configs = upgrades?.Upgrades;
            if (configs == null || configs.Count == 0)
            {
                GUILayout.Label("No upgrade data is registered yet.");
                HubLayout.EndContent();
                return;
            }

            foreach (var config in configs)
            {
                if (config == null || string.IsNullOrEmpty(config.id))
                {
                    continue;
                }

                GUILayout.BeginHorizontal();
                var artIndex = config.id.Contains("jump") ? 0 : config.id.Contains("sprint") || config.id.Contains("speed") ? 1 : 2;
                GeneratedUiSprite.Draw(GeneratedArtCatalog.Frame(GeneratedArtCatalog.Active?.upgradeIcons, artIndex), 40f, 40f);
                GUILayout.Label($"{config.upgradeName}  Lv {upgrades.GetLevel(config.id)}/{config.maxLevel}", GUILayout.Width(220f));
                var cost = upgrades.GetNextCost(config.id);
                if (cost < 0)
                {
                    GUILayout.Label("MAX");
                }
                else
                {
                    var canAfford = upgrades.CanAfford(config.id);
                    var previous = GUI.backgroundColor;
                    if (!canAfford)
                    {
                        GUI.backgroundColor = Color.grey;
                    }

                    if (GUILayout.Button($"Buy {cost}", GUILayout.Width(110f)) && canAfford)
                    {
                        AudioManager.Instance?.PlaySfx(SfxId.UiTap);
                        if (upgrades.TryPurchase(config.id))
                        {
                            AudioManager.Instance?.PlaySfx(SfxId.Purchase);
                        }
                    }

                    GUI.backgroundColor = previous;
                }

                GUILayout.EndHorizontal();

                if (!string.IsNullOrEmpty(config.description))
                {
                    GUILayout.Label($"    {config.description}");
                }
            }

            HubLayout.EndContent();
        }
    }
}
