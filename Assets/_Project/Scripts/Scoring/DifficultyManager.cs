using System;
using CatCourier.Core;
using CatCourier.Generation;
using UnityEngine;

namespace CatCourier.Scoring
{
    public sealed class DifficultyManager : MonoBehaviour
    {
        public int Level { get; private set; }
        public float ObstacleDensity { get; private set; } = 1f;
        public float DronePatrolSpeed { get; private set; } = Constants.DRONE_PATROL_SPEED_BASE;
        public float CoinClusterFrequency { get; private set; } = 1f;
        public float ScoreMultiplier { get; private set; } = 1f;

        public event Action<int> OnDifficultyChanged;

        private bool initialized;

        public void SetDistance(float meters)
        {
            var next = ChunkManager.DifficultyForDistance(meters);
            if (initialized && next == Level)
            {
                return;
            }

            initialized = true;

            Level = next;
            ObstacleDensity = Interpolate(1f, 1.3f, 1.6f, Level);
            DronePatrolSpeed = Interpolate(2f, 2.8f, 4f, Level);
            CoinClusterFrequency = Interpolate(1f, 0.85f, 0.7f, Level);
            ScoreMultiplier = Interpolate(1f, 1.5f, 2f, Level);
            OnDifficultyChanged?.Invoke(Level);
        }

        public static float Interpolate(float d0, float d5, float d10, int level)
        {
            if (level <= 5)
            {
                return Mathf.Lerp(d0, d5, Mathf.Clamp01(level / 5f));
            }

            return Mathf.Lerp(d5, d10, Mathf.Clamp01((level - 5) / 5f));
        }
    }
}
