using System;
using CatCourier.Core;
using UnityEngine;

namespace CatCourier.Coins
{
    public sealed class CoinManager : MonoBehaviour
    {
        public int Bank { get; private set; }
        public int RunCoins { get; private set; }
        public float MagnetRadius { get; private set; }
        public float FinalMultiplier => Mathf.Max(0f, PremiumMultiplier * UpgradeMultiplier * BreedMultiplier * DistrictMultiplier);

        /// <summary>Set when the run enters a district whose unlock pack the player owns.</summary>
        public float DistrictMultiplier { get; private set; } = 1f;

        public event Action<int> OnBankChanged;
        public event Action<int, int, Vector3> OnCoinsCollected;

        private float PremiumMultiplier { get; set; } = 1f;
        private float UpgradeMultiplier { get; set; } = 1f;
        private float BreedMultiplier { get; set; } = 1f;

        private void Awake()
        {
            ReloadBank();
        }

        public void Configure(float premiumMultiplier, float upgradeMultiplier, float breedMultiplier, float magnetRadius)
        {
            PremiumMultiplier = Mathf.Max(0f, premiumMultiplier);
            UpgradeMultiplier = Mathf.Max(0f, upgradeMultiplier);
            BreedMultiplier = Mathf.Max(0f, breedMultiplier);
            MagnetRadius = Mathf.Max(0f, magnetRadius);
        }

        public void ReloadBank()
        {
            Bank = SaveSystem.Instance?.Data?.totalCoins ?? 0;
            OnBankChanged?.Invoke(Bank);
        }

        public void ResetRun()
        {
            RunCoins = 0;
            DistrictMultiplier = 1f;
        }

        /// <summary>
        /// Applies the district unlock pack's coin bonus for the district the run just
        /// entered. Passing 1 for a free district or an unowned pack is a no-op, so a
        /// missing entitlement can never change the payout.
        /// </summary>
        public void SetDistrictMultiplier(float multiplier)
        {
            DistrictMultiplier = Mathf.Max(0f, multiplier);
        }

        public int AwardRunCoins(int amount)
        {
            if (amount <= 0)
            {
                return 0;
            }

            var awarded = Math.Min(amount, int.MaxValue);
            Bank = SaturatingAdd(Bank, awarded);
            RunCoins = SaturatingAdd(RunCoins, awarded);
            OnBankChanged?.Invoke(Bank);
            return awarded;
        }

        public int Collect(int rawValue) => Collect(rawValue, transform.position);

        public int Collect(int rawValue, Vector3 worldPosition)
        {
            if (rawValue <= 0)
            {
                return 0;
            }

            var awarded = RoundAward(rawValue * FinalMultiplier);
            Bank = SaturatingAdd(Bank, awarded);
            RunCoins = SaturatingAdd(RunCoins, awarded);
            OnBankChanged?.Invoke(Bank);
            OnCoinsCollected?.Invoke(rawValue, awarded, worldPosition);
            return awarded;
        }

        private static int SaturatingAdd(int current, int value)
        {
            return value > int.MaxValue - current ? int.MaxValue : current + value;
        }

        private static int RoundAward(float value)
        {
            if (!float.IsFinite(value) || value <= 0f)
            {
                return 0;
            }

            if (value >= int.MaxValue)
            {
                return int.MaxValue;
            }

            return (int)Math.Round(value, MidpointRounding.AwayFromZero);
        }
    }
}
