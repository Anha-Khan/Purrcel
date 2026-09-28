using UnityEngine;

namespace CatCourier.Core
{
    public sealed class PlayerStats
    {
        public float BaseRunSpeed { get; private set; } = Constants.BASE_RUN_SPEED;
        public float CatSpeedBonus { get; private set; }
        public float SpeedMultiplier { get; private set; } = 1f;
        public float CurrentRunSpeed { get; private set; } = Constants.BASE_RUN_SPEED;
        public float BaseJumpForce { get; private set; } = Constants.JUMP_FORCE;
        public float CatJumpBonus { get; private set; }
        public float JumpForceMultiplier { get; private set; } = 1f;
        public float JumpForce => Mathf.Max(0f, (BaseJumpForce + CatJumpBonus) * JumpForceMultiplier);
        public float DoubleJumpForce { get; private set; } = Constants.DOUBLE_JUMP_FORCE;
        public bool DoubleJumpUnlocked { get; private set; }
        public float CoinMagnetRadius { get; private set; }
        public float UpgradeCoinMultiplier { get; private set; } = 1f;
        public float PremiumCoinMultiplier { get; private set; } = 1f;
        public float BreedCoinMultiplier { get; private set; } = 1f;
        public int PackageSlots { get; private set; } = 1;

        private float runTime;

        /// <summary>
        /// Every field is a value type, so a shallow copy is exact and cannot drift when a stat is added.
        /// </summary>
        public PlayerStats Clone() => (PlayerStats)MemberwiseClone();

        public void SetRunTime(float value)
        {
            runTime = Mathf.Max(0f, value);
            var modifiedBase = Mathf.Max(0f, BaseRunSpeed + CatSpeedBonus) * Mathf.Max(0f, SpeedMultiplier);
            CurrentRunSpeed = Mathf.Min(modifiedBase + runTime * Constants.SPEED_INCREMENT, Constants.MAX_RUN_SPEED);
        }

        public void SetUpgrades(float baseSpeed, float jumpForce, bool doubleJumpUnlocked, float doubleJumpForce,
            float magnetRadius, float coinMultiplier, int packageSlots)
        {
            BaseRunSpeed = baseSpeed;
            BaseJumpForce = jumpForce;
            DoubleJumpUnlocked = doubleJumpUnlocked;
            DoubleJumpForce = doubleJumpForce;
            CoinMagnetRadius = magnetRadius;
            UpgradeCoinMultiplier = coinMultiplier;
            PackageSlots = packageSlots;
            SetRunTime(runTime);
        }

        public void SetCatBonuses(float speedBonus, float jumpBonus)
        {
            CatSpeedBonus = speedBonus;
            CatJumpBonus = jumpBonus;
            SetRunTime(runTime);
        }

        public void SetExternalMultipliers(float speedMultiplier, float jumpForceMultiplier)
        {
            SpeedMultiplier = speedMultiplier;
            JumpForceMultiplier = jumpForceMultiplier;
            SetRunTime(runTime);
        }

        public void SetPremiumCoinMultiplier(float multiplier) => PremiumCoinMultiplier = multiplier;
        public void SetBreedCoinMultiplier(float multiplier) => BreedCoinMultiplier = multiplier;
    }
}
