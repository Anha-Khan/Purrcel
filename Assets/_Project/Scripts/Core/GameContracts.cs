using System;
using System.Collections.Generic;
using UnityEngine;

namespace CatCourier.Core
{
    public enum GameState { Hub, Running, Dead, Paused }
    public enum PlayerState { Running, Jumping, Sliding, WallBounce, Dead }
    public enum WeatherType { Clear, Rain, Night, Wind }
    public enum PackageType { Normal, Fragile, Heavy, Urgent }
    public enum DistrictId { OldTown, Downtown, Harbour, Suburbs }
    public enum PaywallSource { AfterRun3, LockedFeature, Settings }
    public enum PurchaseOutcome { Success, Cancelled, Error }
    public enum ChunkType { SmallGap, MediumGap, LargeGap, ObstacleDense, ObstacleSparse, HighPlatform, DropDown, Checkpoint }

    public readonly struct RunResult
    {
        public RunResult(long score, float distanceMeters, int packagesDelivered, int coinsCollected, DistrictId districtReached)
        {
            Score = score;
            DistanceMeters = distanceMeters;
            PackagesDelivered = packagesDelivered;
            CoinsCollected = coinsCollected;
            DistrictReached = districtReached;
        }

        public long Score { get; }
        public float DistanceMeters { get; }
        public int PackagesDelivered { get; }
        public int CoinsCollected { get; }
        public DistrictId DistrictReached { get; }
    }

    public readonly struct StoryBeat
    {
        public StoryBeat(string id, string text)
        {
            Id = id;
            Text = text;
        }

        public string Id { get; }
        public string Text { get; }
        public bool HasStory => Id != null;
    }

    public readonly struct PaywallPackage
    {
        public PaywallPackage(string packageId, string priceString, string periodLabel, bool hasFreeTrial)
        {
            PackageId = packageId;
            PriceString = priceString;
            PeriodLabel = periodLabel;
            HasFreeTrial = hasFreeTrial;
        }

        public string PackageId { get; }
        public string PriceString { get; }
        public string PeriodLabel { get; }
        public bool HasFreeTrial { get; }
    }

    public readonly struct PaywallOffering
    {
        public PaywallOffering(string offeringId, IReadOnlyList<PaywallPackage> packages)
        {
            OfferingId = offeringId;
            Packages = packages ?? Array.Empty<PaywallPackage>();
        }

        public string OfferingId { get; }
        public IReadOnlyList<PaywallPackage> Packages { get; }
    }

    public static class RevenueCatIds
    {
        public const string OfferingDefault = "default";
        public const string PackageMonthly = "$rc_monthly";
        public const string PackageAnnual = "$rc_annual";
        public const string OfferingBreeds = "iap_breeds";
        public const string PackageRare = "rare_pack";
        public const string PackageLegendary = "legendary_pack";
        public const string OfferingDistricts = "iap_districts";
        public const string PackageHarbour = "harbour_unlock";
        public const string PackageSuburbs = "suburbs_unlock";
    }

    public enum SfxId
    {
        Jump,
        DoubleJump,
        Land,
        Slide,
        Coin,
        Package,
        Deliver,
        Death,
        Bounce,
        ComboUp,
        UiTap,
        Purchase
    }

    public enum MusicId
    {
        Hub,
        OldTown,
        Downtown,
        Harbour,
        Suburbs
    }

    [Serializable]
    public sealed class UpgradeLevel
    {
        public string id;
        public int level;
    }

    [Serializable]
    public sealed class RunRecord
    {
        public long score;
        public float distanceMeters;
        public int packagesDelivered;
        public string districtReached;
        public string catBreedId;
        public string dateISO;
    }

    [Serializable]
    public sealed class PlayerSaveData
    {
        public int saveVersion = Constants.SAVE_VERSION;
        public string schemaMarker;
        public string lastSaved;
        public int totalCoins;
        public List<UpgradeLevel> upgradeLevels = new();
        public string selectedCatBreedId;
        public List<string> unlockedCatBreedIds = new();
        public List<string> unlockedDistrictIds = new();
        public List<RunRecord> runHistory = new();
        public List<string> seenStoryBeatIds = new();
        public int totalRunsCompleted;
        public bool hasSeenPaywall;
        public bool hasUsedFreeTrialEver;

        public void Repair()
        {
            upgradeLevels ??= new List<UpgradeLevel>();
            unlockedCatBreedIds ??= new List<string>();
            unlockedDistrictIds ??= new List<string>();
            runHistory ??= new List<RunRecord>();
            seenStoryBeatIds ??= new List<string>();

            for (var i = runHistory.Count - 1; i >= 0; i--)
            {
                if (runHistory[i] == null)
                {
                    runHistory.RemoveAt(i);
                }
            }
        }
    }
}
