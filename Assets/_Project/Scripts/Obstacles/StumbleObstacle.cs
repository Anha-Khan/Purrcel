using CatCourier.Player;
using UnityEngine;

namespace CatCourier.Obstacles
{
    public sealed class StumbleObstacle : ObstacleBase
    {
        [SerializeField, Min(0f)] private float slowdownDuration = 0.5f;
        [SerializeField, Range(0f, 1f)] private float slowdownMultiplier = 0.8f;

        private PlayerController affectedPlayer;
        private float remainingSlowdownTime;

        public float SlowdownDuration => Mathf.Max(0f, slowdownDuration);
        public float SlowdownMultiplier => Mathf.Clamp01(slowdownMultiplier);
        public float RemainingSlowdownTime => remainingSlowdownTime;
        public bool IsSlowingDown => affectedPlayer != null;

        protected override void ConfigureDefaults()
        {
            type = ObstacleType.Stumble;
            isDeadly = false;
            isDestroyable = false;
            speedMultiplierOnHit = SlowdownMultiplier;
        }

        protected override void ResetRuntimeState()
        {
            RestoreSlowdown();
            remainingSlowdownTime = 0f;
        }

        protected override void OnDisable()
        {
            RestoreSlowdown();
            base.OnDisable();
        }

        public void ApplyStumble(PlayerController player)
        {
            InitializeIfNeeded();
            if (player == null || SlowdownDuration <= 0f || !IsActive)
            {
                return;
            }

            if (affectedPlayer == player)
            {
                remainingSlowdownTime = SlowdownDuration;
                return;
            }

            RestoreSlowdown();
            affectedPlayer = player;
            player.ApplyStumbleSlowdown(SlowdownMultiplier);
            remainingSlowdownTime = SlowdownDuration;
        }

        public void RestoreSlowdown()
        {
            if (affectedPlayer != null)
            {
                // Ask the player to recompose instead of writing the captured
                // multipliers back. Writing them back overwrote the weather x package
                // product, so any weather change during a stumble silently erased it.
                affectedPlayer.ClearStumbleSlowdown();
            }

            affectedPlayer = null;
            remainingSlowdownTime = 0f;
        }

        public void Simulate(float deltaTime)
        {
            if (!IsActive || !IsSlowingDown)
            {
                return;
            }

            remainingSlowdownTime -= Mathf.Max(0f, deltaTime);
            if (remainingSlowdownTime <= 0f)
            {
                RestoreSlowdown();
            }
        }

        private void FixedUpdate()
        {
            Simulate(Time.fixedDeltaTime);
        }

        private void OnTriggerEnter2D(Collider2D other)
        {
            if (IsActive)
            {
                ApplyStumble(FindPlayer(other));
            }
        }
    }
}
