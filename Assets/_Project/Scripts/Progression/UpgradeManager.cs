using System;
using System.Collections.Generic;
using CatCourier.Core;
using CatCourier.Monetization;
using UnityEngine;

namespace CatCourier.Progression
{
    /// <summary>
    /// Hub-facing upgrade catalog. Levels, costs, and values come from <see cref="UpgradeConfig"/>
    /// assets. The six upgrades defined in docs/spec.md section 12 have built-in spec defaults so the
    /// system is fully usable and testable before authored assets exist; an authored asset always wins.
    /// </summary>
    public sealed class UpgradeManager : MonoBehaviour
    {
        [SerializeField] private UpgradeConfig[] configs = Array.Empty<UpgradeConfig>();
        [SerializeField] private CatBreedManager breeds;

        private readonly List<UpgradeConfig> catalog = new();
        private readonly List<UpgradeConfig> generated = new();
        private readonly Dictionary<string, UpgradeConfig> byId = new(StringComparer.Ordinal);
        private readonly List<string> warnings = new();
        private bool breedSubscribed;
        private bool entitlementSubscribed;

        /// <summary>Effective upgrade list: authored assets first, spec defaults for anything missing.</summary>
        public IReadOnlyList<UpgradeConfig> Upgrades => catalog;

        /// <summary>Problems found in the authored configuration, e.g. missing ids or incomplete data.</summary>
        public IReadOnlyList<string> Warnings => warnings;

        /// <summary>Stats rebuilt from save data, the selected cat, and current entitlements.</summary>
        public PlayerStats Stats { get; private set; }

        public event Action OnUpgradesChanged;

        private void Awake()
        {
            RebuildCatalog();
            Stats = BuildPlayerStats();
            OnUpgradesChanged?.Invoke();
        }

        public void SetConfigs(IEnumerable<UpgradeConfig> newConfigs)
        {
            configs = newConfigs == null ? Array.Empty<UpgradeConfig>() : new List<UpgradeConfig>(newConfigs).ToArray();
            RebuildCatalog();
            Refresh();
        }

        public UpgradeConfig Get(string id)
        {
            return !string.IsNullOrEmpty(id) && byId.TryGetValue(id, out var config) ? config : null;
        }

        public int GetLevel(string id)
        {
            var config = Get(id);
            if (config == null)
            {
                return 0;
            }

            var level = 0;
            var levels = SaveSystem.Instance?.Data?.upgradeLevels;
            if (levels != null)
            {
                foreach (var entry in levels)
                {
                    if (entry != null && string.Equals(entry.id, id, StringComparison.Ordinal))
                    {
                        level = entry.level;
                        break;
                    }
                }
            }

            return Mathf.Clamp(level, 0, config.maxLevel);
        }

        /// <summary>Cost to buy the next level, or -1 when the id is unknown or already maxed.</summary>
        public int GetNextCost(string id)
        {
            var config = Get(id);
            if (config == null)
            {
                return -1;
            }

            var level = GetLevel(id);
            if (level >= config.maxLevel)
            {
                return -1;
            }

            var costs = config.costs;
            if (costs == null || level < 0 || level >= costs.Length)
            {
                return -1;
            }

            var cost = costs[level];
            return cost < 0 ? -1 : cost;
        }

        public bool CanAfford(string id)
        {
            var cost = GetNextCost(id);
            return cost >= 0 && (SaveSystem.Instance?.Data?.totalCoins ?? 0) >= cost;
        }

        public bool IsMaxLevel(string id)
        {
            var config = Get(id);
            return config != null && GetLevel(id) >= config.maxLevel;
        }

        /// <summary>Stat value at the current level. Index 0 is the level-0 base value.</summary>
        public float GetValue(string id) => GetValueAtLevel(id, GetLevel(id));

        public float GetValueAtLevel(string id, int level)
        {
            var config = Get(id);
            if (config?.values == null || config.values.Length == 0)
            {
                return 0f;
            }

            return config.values[Mathf.Clamp(level, 0, config.values.Length - 1)];
        }

        /// <summary>
        /// Validates, deducts, increments, persists, rebuilds stats, then emits. Any failure before the
        /// emit leaves coins and level untouched.
        /// </summary>
        public bool TryPurchase(string id)
        {
            var config = Get(id);
            var save = SaveSystem.Instance;
            if (config == null || save?.Data == null)
            {
                return false;
            }

            var level = GetLevel(id);
            if (level >= config.maxLevel)
            {
                return false;
            }

            var cost = GetNextCost(id);
            if (cost < 0 || save.Data.totalCoins < cost)
            {
                return false;
            }

            if (!save.TryPurchaseUpgrade(id, level, cost))
            {
                return false;
            }

            Refresh();
            return true;
        }

        /// <summary>Single rebuild path: save levels, selected cat bonuses, entitlement multipliers.</summary>
        public PlayerStats BuildPlayerStats()
        {
            var active = ActiveBreed();
            var speedBonus = active != null ? active.speedBonus : 0f;
            var jumpBonus = active != null ? active.jumpBonus : 0f;
            var coinMultiplier = active != null && active.coinBonusMultiplier > 0f ? active.coinBonusMultiplier : 1f;
            var doubleJump = GetValue(RunLoadoutService.DoubleJumpId);

            var stats = new PlayerStats();
            stats.SetUpgrades(
                GetValue(RunLoadoutService.SprintSpeedId),
                GetValue(RunLoadoutService.JumpHeightId),
                doubleJump > 0f,
                doubleJump > 0f ? doubleJump : Constants.DOUBLE_JUMP_FORCE,
                GetValue(RunLoadoutService.CoinMagnetId),
                GetValue(RunLoadoutService.CoinMultiplierId),
                Mathf.Max(1, Mathf.RoundToInt(GetValue(RunLoadoutService.PackageSlotsId))));
            stats.SetCatBonuses(speedBonus, jumpBonus);
            // The premium multiplier used to be applied here and again in
            // RunLoadoutService.Build, so the two copies could disagree. BuildPlayerStats
            // is the single owner; RunLoadoutService clones these stats.
            stats.SetPremiumCoinMultiplier(EntitlementChecker.Instance != null && EntitlementChecker.Instance.IsPremium ? 2f : 1f);
            stats.SetBreedCoinMultiplier(coinMultiplier);
            return stats;
        }

        /// <summary>Re-reads save data and entitlements, rebuilds <see cref="Stats"/>, then emits.</summary>
        public void Refresh()
        {
            EnsureBreedSubscription();
            EnsureEntitlementSubscription();
            Stats = BuildPlayerStats();
            OnUpgradesChanged?.Invoke();
        }

        private void RebuildCatalog()
        {
            DestroyGenerated();
            catalog.Clear();
            byId.Clear();
            warnings.Clear();

            if (configs != null)
            {
                foreach (var config in configs)
                {
                    if (config == null)
                    {
                        warnings.Add("Upgrade catalog contains a null entry.");
                        continue;
                    }

                    if (string.IsNullOrWhiteSpace(config.id))
                    {
                        warnings.Add($"Upgrade '{config.name}' has no id and was skipped.");
                        continue;
                    }

                    if (config.maxLevel <= 0 ||
                        config.costs == null || config.costs.Length < config.maxLevel ||
                        config.values == null || config.values.Length < config.maxLevel + 1)
                    {
                        warnings.Add($"Upgrade '{config.id}' has incomplete cost or value data and was skipped.");
                        continue;
                    }

                    if (byId.ContainsKey(config.id))
                    {
                        warnings.Add($"Duplicate upgrade id '{config.id}' was skipped.");
                        continue;
                    }

                    byId.Add(config.id, config);
                    catalog.Add(config);
                }
            }

            AppendMissingDefaults();

            foreach (var warning in warnings)
            {
                Debug.LogWarning($"UpgradeManager: {warning}", this);
            }
        }

        private void AppendMissingDefaults()
        {
            foreach (var spec in SpecUpgrades)
            {
                if (byId.ContainsKey(spec.Id))
                {
                    continue;
                }

                var config = ScriptableObject.CreateInstance<UpgradeConfig>();
                config.name = spec.Id;
                config.hideFlags = HideFlags.HideAndDontSave;
                config.id = spec.Id;
                config.upgradeName = spec.Name;
                config.description = spec.Description;
                config.costs = (int[])spec.Costs.Clone();
                config.values = (float[])spec.Values.Clone();
                config.maxLevel = spec.Costs.Length;
                generated.Add(config);
                byId.Add(spec.Id, config);
                catalog.Add(config);
            }
        }

        private CatBreedConfig ActiveBreed()
        {
            EnsureBreedSubscription();
            return breeds != null ? breeds.ActiveBreed : null;
        }

        /// <summary>
        /// Cat bonuses feed the rebuilt stats, so a selection or entitlement change must rebuild them.
        /// Subscribing also covers the case where this Awake runs before the breed manager's OnEnable.
        /// </summary>
        private void EnsureBreedSubscription()
        {
            if (breeds == null)
            {
                breeds = FindObjectOfType<CatBreedManager>();
            }

            if (breedSubscribed || breeds == null)
            {
                return;
            }

            breeds.OnSelectionChanged += HandleBreedsChanged;
            breeds.OnUnlocksChanged += HandleBreedsChanged;
            breedSubscribed = true;
        }

        private void EnsureEntitlementSubscription()
        {
            if (entitlementSubscribed || EntitlementChecker.Instance == null)
            {
                return;
            }

            EntitlementChecker.Instance.OnEntitlementsChanged -= HandleEntitlementsChanged;
            EntitlementChecker.Instance.OnEntitlementsChanged += HandleEntitlementsChanged;
            entitlementSubscribed = true;
        }

        private void HandleEntitlementsChanged()
        {
            Stats = BuildPlayerStats();
            OnUpgradesChanged?.Invoke();
        }

        private void HandleBreedsChanged()
        {
            Stats = BuildPlayerStats();
            OnUpgradesChanged?.Invoke();
        }

        private void UnsubscribeFromBreeds()
        {
            if (breedSubscribed && breeds != null)
            {
                breeds.OnSelectionChanged -= HandleBreedsChanged;
                breeds.OnUnlocksChanged -= HandleBreedsChanged;
            }

            breedSubscribed = false;
        }

        private void DestroyGenerated()
        {
            foreach (var config in generated)
            {
                if (config == null)
                {
                    continue;
                }

                if (Application.isPlaying)
                {
                    Destroy(config);
                }
                else
                {
                    DestroyImmediate(config);
                }
            }

            generated.Clear();
        }

        private void OnDestroy()
        {
            UnsubscribeFromBreeds();
            if (entitlementSubscribed && EntitlementChecker.Instance != null)
            {
                EntitlementChecker.Instance.OnEntitlementsChanged -= HandleEntitlementsChanged;
            }

            DestroyGenerated();
        }

        private readonly struct SpecUpgrade
        {
            public SpecUpgrade(string id, string name, string description, int[] costs, float[] values)
            {
                Id = id;
                Name = name;
                Description = description;
                Costs = costs;
                Values = values;
            }

            public string Id { get; }
            public string Name { get; }
            public string Description { get; }
            public int[] Costs { get; }
            public float[] Values { get; }
        }

        // docs/spec.md section 12. costs[index] buys the level above index; values[index] is the value at that level.
        private static readonly SpecUpgrade[] SpecUpgrades =
        {
            new SpecUpgrade(RunLoadoutService.SprintSpeedId, "Sprint Speed",
                "Raises the base run speed before time-based acceleration.",
                new[] { 50, 120, 250 }, new[] { 6f, 6.5f, 7f, 8f }),
            new SpecUpgrade(RunLoadoutService.JumpHeightId, "Jump Height",
                "Raises the ground jump force.",
                new[] { 60, 150, 300 }, new[] { 14f, 15.5f, 17f, 19f }),
            new SpecUpgrade(RunLoadoutService.DoubleJumpId, "Double Jump",
                "Unlocks the second jump and raises its force.",
                new[] { 200, 400 }, new[] { 0f, 11f, 13f }),
            new SpecUpgrade(RunLoadoutService.CoinMagnetId, "Coin Magnet",
                "Auto-collects coins within a radius.",
                new[] { 80, 200, 400 }, new[] { 0f, 1.5f, 3f, 5f }),
            new SpecUpgrade(RunLoadoutService.CoinMultiplierId, "Coin Multiplier",
                "Multiplies every coin collected in a run.",
                new[] { 100, 250, 500 }, new[] { 1f, 1.25f, 1.5f, 2f }),
            new SpecUpgrade(RunLoadoutService.PackageSlotsId, "Package Carry Limit",
                "Carries more packages at once.",
                new[] { 150, 350 }, new[] { 1f, 2f, 3f })
        };
    }
}
