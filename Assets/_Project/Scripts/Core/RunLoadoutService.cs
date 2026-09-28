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

        public static RunLoadout Build()
        {
            var upgradeManager = UnityEngine.Object.FindObjectOfType<UpgradeManager>();
            var breedManager = UnityEngine.Object.FindObjectOfType<CatBreedManager>();
            var sourceStats = upgradeManager != null ? upgradeManager.Stats : new PlayerStats();
            var stats = sourceStats != null ? sourceStats.Clone() : new PlayerStats();
            breedManager?.ApplyTo(stats);
            var premium = EntitlementChecker.Instance?.IsPremium == true;
            stats.SetPremiumCoinMultiplier(premium ? 2f : 1f);
            stats.SetRunTime(0f);
            var packageSlots = Math.Max(1, stats.PackageSlots);
            return new RunLoadout(stats, packageSlots, premium);
        }

        public static DistrictId[] GetEligibleDistricts(float runDistance, bool premium, IEnumerable<string> unlockedIds)
        {
            var result = new List<DistrictId> { DistrictId.OldTown, DistrictId.Downtown };
            var entitlements = Monetization.EntitlementChecker.Instance;
            if (premium || runDistance >= 600f || Contains(unlockedIds, DistrictId.Harbour) || entitlements?.HasDistrict(DistrictId.Harbour) == true)
            {
                result.Add(DistrictId.Harbour);
            }

            if (premium || runDistance >= 1200f || Contains(unlockedIds, DistrictId.Suburbs) || entitlements?.HasDistrict(DistrictId.Suburbs) == true)
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
