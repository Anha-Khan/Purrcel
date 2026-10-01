using System;
using CatCourier.Core;
using UnityEngine;

namespace CatCourier.Scoring
{
    public sealed class ScoreManager : MonoBehaviour
    {
        public long Score { get; private set; }
        public float DistanceMeters { get; private set; }
        public int ComboCount { get; private set; }
        public float ComboMultiplier { get; private set; } = 1f;
        public int PackagesDelivered { get; private set; }
        public float DeliveryScoreMultiplier { get; private set; } = 1f;

        /// <summary>
        /// Fires on every score recalculation. The HUD polls Score each frame instead,
        /// so the previous OnDistanceChanged and OnComboChanged were invoked but never
        /// subscribed to and have been removed.
        /// </summary>
        public event Action<long> OnScoreChanged;
        public event Action<int> OnComboMilestone;

        private DifficultyManager difficulty;
        private int comboCoins;
        private int lastMilestone;
        private float lastCoinX;
        private bool hasCoinPosition;
        private float deliveryPulseRemaining;

        public void Initialize(DifficultyManager manager)
        {
            difficulty = manager;
            Recalculate();
        }

        public void ResetRun()
        {
            Score = 0;
            DistanceMeters = 0f;
            ComboCount = 0;
            ComboMultiplier = 1f;
            PackagesDelivered = 0;
            comboCoins = 0;
            lastMilestone = 0;
            lastCoinX = 0f;
            hasCoinPosition = false;
            DeliveryScoreMultiplier = 1f;
            deliveryPulseRemaining = 0f;
            difficulty?.SetDistance(0f);
            Recalculate();
        }

        public void SetDistance(float meters)
        {
            DistanceMeters = Mathf.Max(0f, meters);
            difficulty?.SetDistance(DistanceMeters);
            Recalculate();
        }

        public void AddPackage()
        {
            PackagesDelivered++;
            Recalculate();
        }

        public void SetDeliveryPulse(float multiplier, float duration)
        {
            DeliveryScoreMultiplier = Mathf.Max(1f, multiplier);
            deliveryPulseRemaining = Mathf.Max(0f, duration);
            Recalculate();
        }

        public void Simulate(float deltaTime)
        {
            if (deliveryPulseRemaining <= 0f)
            {
                return;
            }

            deliveryPulseRemaining = Mathf.Max(0f, deliveryPulseRemaining - Mathf.Max(0f, deltaTime));
            if (deliveryPulseRemaining <= 0f)
            {
                DeliveryScoreMultiplier = 1f;
            }

            Recalculate();
        }

        public void CollectCoin(int rawValue) => CollectCoin(rawValue, 0f, false);

        public void CollectCoin(int rawValue, float worldX)
        {
            CollectCoin(rawValue, worldX, true);
        }

        private void CollectCoin(int rawValue, float worldX, bool trackPosition)
        {
            if (rawValue <= 0)
            {
                return;
            }

            if (trackPosition && hasCoinPosition && Mathf.Abs(worldX - lastCoinX) > Constants.COMBO_BREAK_DISTANCE)
            {
                BreakCombo();
            }

            ComboCount++;
            comboCoins += rawValue;
            lastCoinX = worldX;
            hasCoinPosition = trackPosition;
            ComboMultiplier = GetComboMultiplier(ComboCount);

            if (ComboCount / 10 > lastMilestone)
            {
                lastMilestone = ComboCount / 10;
                OnComboMilestone?.Invoke(ComboCount);
            }

            Recalculate();
        }

        public void BreakCombo()
        {
            if (ComboCount == 0)
            {
                return;
            }

            ComboCount = 0;
            comboCoins = 0;
            ComboMultiplier = 1f;
            lastMilestone = 0;
            lastCoinX = 0f;
            hasCoinPosition = false;
            Recalculate();
        }

        public void RegisterObstacleContact() => BreakCombo();
        public void RegisterDeath() => BreakCombo();

        public static float GetComboMultiplier(int count)
        {
            if (count >= 30) return 3f;
            if (count >= 20) return 2f;
            if (count >= 10) return 1.5f;
            return 1f;
        }

        private void Recalculate()
        {
            var distanceScore = DistanceMeters * Constants.DISTANCE_SCORE_PER_METER;
            var packageScore = (double)PackagesDelivered * Constants.PACKAGE_SCORE_VALUE;
            var comboBonus = comboCoins * ComboMultiplier;
            var difficultyMultiplier = difficulty?.ScoreMultiplier ?? 1f;
            Score = (long)Math.Round((distanceScore + packageScore + comboBonus) * difficultyMultiplier * DeliveryScoreMultiplier, MidpointRounding.AwayFromZero);
            OnScoreChanged?.Invoke(Score);
        }
    }
}
