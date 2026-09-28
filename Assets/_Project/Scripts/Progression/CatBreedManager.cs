using System;
using System.Collections.Generic;
using CatCourier.Core;
using CatCourier.Monetization;
using UnityEngine;

namespace CatCourier.Progression
{
    /// <summary>
    /// Cat breed catalog, entitlement-aware unlock state, and selection persistence.
    ///
    /// Paid breeds are only sellable once an authored <see cref="CatBreedConfig"/> asset registers
    /// them, so unfinished paid content is never listed. The two starter breeds from
    /// docs/spec.md section 13 have built-in defaults so the game always has a selectable cat.
    /// </summary>
    public sealed class CatBreedManager : MonoBehaviour
    {
        public const string TabbyId = "tabby";
        public const string TuxedoId = "tuxedo";

        public const string UnknownBreedReason = "Cat breed unavailable.";
        public const string PremiumRequiredReason = "Requires Cat Courier Premium.";
        public const string ExtraPackRequiredReason = "Requires an extra pack.";
        public const string ComingSoonReason = "Coming soon.";

        [SerializeField] private CatBreedConfig[] configs = Array.Empty<CatBreedConfig>();

        private readonly List<CatBreedConfig> catalog = new();
        private readonly List<CatBreedConfig> generated = new();
        private readonly Dictionary<string, CatBreedConfig> byId = new(StringComparer.Ordinal);
        private readonly List<string> warnings = new();

        private EntitlementChecker entitlements;
        private bool subscribed;
        private bool catalogDirty = true;

        /// <summary>Effective breed list: authored assets first, starter defaults for anything missing.</summary>
        public IReadOnlyList<CatBreedConfig> Breeds => catalog;

        public IReadOnlyList<string> Warnings => warnings;

        /// <summary>Saved selection. May be locked, e.g. right after a premium lapse.</summary>
        public string SelectedId { get; private set; } = string.Empty;

        public CatBreedConfig Selected { get; private set; }

        /// <summary>Breed that currently contributes bonuses: the selection if usable, otherwise a starter.</summary>
        public CatBreedConfig ActiveBreed { get; private set; }

        public bool IsSelectedUsable => Selected != null && IsUnlocked(SelectedId);

        public float SpeedBonus => ActiveBreed != null ? ActiveBreed.speedBonus : 0f;
        public float JumpBonus => ActiveBreed != null ? ActiveBreed.jumpBonus : 0f;
        public float CoinBonusMultiplier =>
            ActiveBreed != null && ActiveBreed.coinBonusMultiplier > 0f ? ActiveBreed.coinBonusMultiplier : 1f;

        public event Action OnSelectionChanged;
        public event Action OnUnlocksChanged;

        private void OnEnable()
        {
            Refresh();
        }

        private void Start()
        {
            EnsureEntitlementSubscription();
        }

        private void OnDisable()
        {
            if (subscribed && entitlements != null)
            {
                entitlements.OnEntitlementsChanged -= HandleEntitlementsChanged;
            }

            subscribed = false;
            entitlements = null;
        }

        public void SetConfigs(IEnumerable<CatBreedConfig> newConfigs)
        {
            configs = newConfigs == null ? Array.Empty<CatBreedConfig>() : new List<CatBreedConfig>(newConfigs).ToArray();
            RebuildCatalog();
            Refresh();
        }

        public CatBreedConfig Get(string id)
        {
            return !string.IsNullOrEmpty(id) && byId.TryGetValue(id, out var config) ? config : null;
        }

        public bool IsStarter(string id)
        {
            var config = Get(id);
            return config != null && !config.isPremium && !config.isIAP;
        }

        public bool IsUnlocked(string id)
        {
            var config = Get(id);
            if (config == null)
            {
                return false;
            }

            if (!config.isPremium && !config.isIAP)
            {
                return true;
            }

            if (entitlements == null && !TryResolveEntitlements())
            {
                return false;
            }

            if (config.isPremium && !entitlements.IsPremium)
            {
                return false;
            }

            if (config.isIAP &&
                (string.IsNullOrWhiteSpace(config.iapPackId) || !entitlements.HasBreedPack(config.iapPackId)))
            {
                return false;
            }

            return true;
        }

        /// <summary>Empty when unlocked or selectable. Otherwise the reason to show in the UI.</summary>
        public string LockReason(string id)
        {
            var config = Get(id);
            if (config == null)
            {
                return UnknownBreedReason;
            }

            if (IsUnlocked(id))
            {
                return string.Empty;
            }

            if (config.isIAP && string.IsNullOrWhiteSpace(config.iapPackId))
            {
                return ComingSoonReason;
            }

            if (config.isPremium)
            {
                return PremiumRequiredReason;
            }

            return config.isIAP ? PackReason(config.iapPackId) : ComingSoonReason;
        }

        public bool TrySelect(string id)
        {
            var config = Get(id);
            if (config == null || !IsUnlocked(id))
            {
                return false;
            }

            if (string.Equals(SelectedId, id, StringComparison.Ordinal))
            {
                return true;
            }

            var save = SaveSystem.Instance;
            if (save?.Data != null && !save.SetSelectedCat(id))
            {
                return false;
            }

            ApplySelection(config);
            OnSelectionChanged?.Invoke();
            return true;
        }

        public void ApplyTo(PlayerStats stats)
        {
            if (stats == null)
            {
                return;
            }

            var active = ActiveBreed;
            stats.SetCatBonuses(active != null ? active.speedBonus : 0f, active != null ? active.jumpBonus : 0f);
            stats.SetBreedCoinMultiplier(
                active != null && active.coinBonusMultiplier > 0f ? active.coinBonusMultiplier : 1f);
        }

        /// <summary>Re-reads save data and entitlements, then re-emits state. Persists only the initial starter default.</summary>
        public void Refresh()
        {
            EnsureEntitlementSubscription();
            if (catalogDirty)
            {
                RebuildCatalog();
            }

            var savedId = SaveSystem.Instance?.Data?.selectedCatBreedId;
            var restored = Get(savedId) ?? DefaultBreed();
            if (string.IsNullOrWhiteSpace(savedId) && restored != null && SaveSystem.Instance?.Data != null)
            {
                SaveSystem.Instance.SetSelectedCat(restored.id);
            }

            var changed = !string.Equals(SelectedId, restored != null ? restored.id : string.Empty, StringComparison.Ordinal);
            Selected = restored;
            SelectedId = restored != null ? restored.id : string.Empty;

            ActiveBreed = IsSelectedUsable ? Selected : DefaultBreed();
            OnUnlocksChanged?.Invoke();
            if (changed)
            {
                OnSelectionChanged?.Invoke();
            }
        }

        private void RebuildCatalog()
        {
            DestroyGenerated();
            catalog.Clear();
            byId.Clear();
            warnings.Clear();
            catalogDirty = false;

            if (configs != null)
            {
                foreach (var config in configs)
                {
                    if (config == null)
                    {
                        warnings.Add("Cat breed catalog contains a null entry.");
                        continue;
                    }

                    if (string.IsNullOrWhiteSpace(config.id))
                    {
                        warnings.Add($"Cat breed '{config.name}' has no id and was skipped.");
                        continue;
                    }

                    if (byId.ContainsKey(config.id))
                    {
                        warnings.Add($"Duplicate cat breed id '{config.id}' was skipped.");
                        continue;
                    }

                    byId.Add(config.id, config);
                    catalog.Add(config);
                }
            }

            AppendMissingStarters();

            foreach (var warning in warnings)
            {
                Debug.LogWarning($"CatBreedManager: {warning}", this);
            }
        }

        private void AppendMissingStarters()
        {
            foreach (var starter in SpecStarters)
            {
                if (byId.ContainsKey(starter.Id))
                {
                    continue;
                }

                var config = ScriptableObject.CreateInstance<CatBreedConfig>();
                config.name = starter.Id;
                config.hideFlags = HideFlags.HideAndDontSave;
                config.id = starter.Id;
                config.breedName = starter.Name;
                config.description = starter.Description;
                config.speedBonus = starter.SpeedBonus;
                config.jumpBonus = starter.JumpBonus;
                config.coinBonusMultiplier = starter.CoinMultiplier;
                generated.Add(config);
                byId.Add(starter.Id, config);
                catalog.Add(config);
            }
        }

        private CatBreedConfig DefaultBreed()
        {
            var tabby = Get(TabbyId);
            if (tabby != null && IsUnlocked(TabbyId))
            {
                return tabby;
            }

            foreach (var config in catalog)
            {
                if (IsStarter(config.id))
                {
                    return config;
                }
            }

            return null;
        }

        private void ApplySelection(CatBreedConfig config)
        {
            Selected = config;
            SelectedId = config != null ? config.id : string.Empty;
            ActiveBreed = config;
        }

        private void HandleEntitlementsChanged()
        {
            Refresh();
        }

        private void EnsureEntitlementSubscription()
        {
            if (entitlements == null && !TryResolveEntitlements())
            {
                return;
            }

            if (subscribed || entitlements == null)
            {
                return;
            }

            entitlements.OnEntitlementsChanged += HandleEntitlementsChanged;
            subscribed = true;
        }

        private bool TryResolveEntitlements()
        {
            entitlements = EntitlementChecker.Instance;
            return entitlements != null;
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
            DestroyGenerated();
        }

        private readonly struct SpecStarter
        {
            public SpecStarter(string id, string name, string description, float speedBonus, float jumpBonus, float coinMultiplier)
            {
                Id = id;
                Name = name;
                Description = description;
                SpeedBonus = speedBonus;
                JumpBonus = jumpBonus;
                CoinMultiplier = coinMultiplier;
            }

            public string Id { get; }
            public string Name { get; }
            public string Description { get; }
            public float SpeedBonus { get; }
            public float JumpBonus { get; }
            public float CoinMultiplier { get; }
        }

        // docs/spec.md section 13 starter table. No art is fabricated; sprites stay null until authored.
        private static readonly SpecStarter[] SpecStarters =
        {
            new SpecStarter(TabbyId, "Tabby", "Default cat.", 0f, 0f, 1f),
            new SpecStarter(TuxedoId, "Tuxedo", "Slightly faster.", 0.3f, 0f, 1f)
        };

        private static string PackReason(string packId)
        {
            if (string.IsNullOrWhiteSpace(packId))
            {
                return ComingSoonReason;
            }

            if (string.Equals(packId, Constants.ENTITLEMENT_RARE_PACK, StringComparison.Ordinal))
            {
                return "Requires the Rare Breeds pack.";
            }

            return string.Equals(packId, Constants.ENTITLEMENT_LEGEND_PACK, StringComparison.Ordinal)
                ? "Requires the Legendary Cats pack."
                : ExtraPackRequiredReason;
        }
    }
}
