using System;
using System.Collections.Generic;
using CatCourier.Monetization;
using CatCourier.Progression;

namespace CatCourier.Core
{
    public sealed class RunLoadout
    {
        public PlayerStats Stats { get; }
        public int PackageSlots { get; }
        public bool IsPremium { get; }

        public RunLoadout(PlayerStats stats, int packageSlots, bool isPremium)
        {
            Stats = stats ?? new PlayerStats();
            PackageSlots = Math.Max(1, packageSlots);
            IsPremium = isPremium;
        }
    }

    public static class RunLoadoutService
    {
        public const string SprintSpeedId = "sprint_speed";
        public const string JumpHeightId = "jump_height";
        public const string DoubleJumpId = "double_jump";
        public const string CoinMagnetId = "coin_magnet";
        public const string CoinMultiplierId = "coin_multiplier";
        public const string PackageSlotsId = "package_slots";

        /// <summary>
        /// Run distance at which a district unlocks for every player. Owned here so the
        /// eligibility check and the persistent unlock cannot drift apart.
        /// </summary>
        public const float HarbourReachDistance = 600f;
        public const float SuburbsReachDistance = 1200f;

        /// <summary>
        /// The coin bonus the harbour_unlock / suburbs_unlock packs grant while running
        /// in that district. The districts already unlock for free on distance, so the
        /// pack has to sell something the reach gate does not.
        /// </summary>
        public const float HarbourPackCoinBonus = 1.25f;
        public const float SuburbsPackCoinBonus = 1.25f;

        public static RunLoadout Build()
        {
            var upgradeManager = UnityEngine.Object.FindObjectOfType<UpgradeManager>();
            var breedManager = UnityEngine.Object.FindObjectOfType<CatBreedManager>();
            var sourceStats = upgradeManager != null ? upgradeManager.Stats : new PlayerStats();
            var stats = sourceStats != null ? sourceStats.Clone() : new PlayerStats();
            breedManager?.ApplyTo(stats);
            // PremiumCoinMultiplier is already baked in by UpgradeManager.BuildPlayerStats,
            // the single owner of the coin formula. Setting it again here was a second
            // copy of the 2x rule that could drift from the first.
            var premium = EntitlementChecker.Instance?.IsPremium == true;
            stats.SetRunTime(0f);
            var packageSlots = Math.Max(1, stats.PackageSlots);
            return new RunLoadout(stats, packageSlots, premium);
        }

        public static DistrictId[] GetEligibleDistricts(float runDistance, bool premium, IEnumerable<string> unlockedIds)
        {
            var result = new List<DistrictId> { DistrictId.OldTown, DistrictId.Downtown };
            var entitlements = Monetization.EntitlementChecker.Instance;
            if (premium || runDistance >= HarbourReachDistance || Contains(unlockedIds, DistrictId.Harbour) || entitlements?.HasDistrict(DistrictId.Harbour) == true)
            {
                result.Add(DistrictId.Harbour);
            }

            if (premium || runDistance >= SuburbsReachDistance || Contains(unlockedIds, DistrictId.Suburbs) || entitlements?.HasDistrict(DistrictId.Suburbs) == true)
            {
                result.Add(DistrictId.Suburbs);
            }

            return result.ToArray();
        }

        private static bool Contains(IEnumerable<string> values, DistrictId district)
        {
            if (values == null)
            {
                return false;
            }

            foreach (var value in values)
            {
                if (string.Equals(value, district.ToString(), StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }

            return false;
        }
    }
}
