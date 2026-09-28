using CatCourier.Core;
using CatCourier.Scoring;
using UnityEngine;

namespace CatCourier.Obstacles
{
    public sealed class PatrolObstacle : ObstacleBase
    {
        [SerializeField] private DifficultyManager difficultyManager;
        [SerializeField, Min(0f)] private float patrolWidth = Constants.DRONE_PATROL_WIDTH;
        [SerializeField, Min(0f)] private float patrolSpeed = Constants.DRONE_PATROL_SPEED_BASE;
        [SerializeField, Min(1f)] private float nightConeSpeedMultiplier = 1.5f;

        private float patrolOffset;
        private int patrolDirection = 1;
        private bool nightConeActive;

        public float PatrolWidth => Mathf.Max(0f, patrolWidth);
        public float PatrolSpeed { get; private set; } = Constants.DRONE_PATROL_SPEED_BASE;
        public bool NightConeActive => nightConeActive;
        public float NightConeSpeedMultiplier => Mathf.Max(1f, nightConeSpeedMultiplier);

        protected override void ConfigureDefaults()
        {
            type = ObstacleType.Patrol;
            isDeadly = true;
            isDestroyable = false;
            speedMultiplierOnHit = 1f;
        }

        protected override void OnEnable()
        {
            base.OnEnable();
            ResolveDifficultyManager();
            SubscribeToDifficulty();
            RecalculatePatrolSpeed();
        }

        protected override void OnDisable()
        {
            UnsubscribeFromDifficulty();
            base.OnDisable();
        }

        protected override void ResetRuntimeState()
        {
            transform.localPosition = InitialLocalPosition;
            patrolOffset = 0f;
            patrolDirection = 1;
            nightConeActive = false;
            RecalculatePatrolSpeed();
        }

        public void SetPatrolWidth(float value)
        {
            patrolWidth = Mathf.Max(0f, value);
            ResetForChunk();
        }

        public void SetPatrolSpeed(float value)
        {
            patrolSpeed = Mathf.Max(0f, value);
            RecalculatePatrolSpeed();
        }

        public void SetDifficultyManager(DifficultyManager manager)
        {
            if (difficultyManager == manager)
            {
                return;
            }

            UnsubscribeFromDifficulty();
            difficultyManager = manager;
            if (difficultyManager != null && isActiveAndEnabled)
            {
                difficultyManager.OnDifficultyChanged += HandleDifficultyChanged;
            }

            RecalculatePatrolSpeed();
        }

        public void SetNightConeActive(bool active)
        {
            nightConeActive = active;
            RecalculatePatrolSpeed();
        }

        public void Simulate(float deltaTime)
        {
            InitializeIfNeeded();
            if (!IsActive || PatrolWidth <= 0f || PatrolSpeed <= 0f)
            {
                return;
            }

            var halfWidth = PatrolWidth * 0.5f;
            patrolOffset += patrolDirection * PatrolSpeed * Mathf.Max(0f, deltaTime);
            if (patrolOffset >= halfWidth)
            {
                patrolOffset = halfWidth;
                patrolDirection = -1;
            }
            else if (patrolOffset <= -halfWidth)
            {
                patrolOffset = -halfWidth;
                patrolDirection = 1;
            }

            var position = InitialLocalPosition;
            position.x += patrolOffset;
            transform.localPosition = position;
        }

        public void ResetPatrol()
        {
            ResetForChunk();
        }

        private void Start()
        {
            ResolveDifficultyManager();
            SubscribeToDifficulty();
            RecalculatePatrolSpeed();
        }

        private void FixedUpdate()
        {
            Simulate(Time.fixedDeltaTime);
        }

        private void OnTriggerEnter2D(Collider2D other)
        {
            if (IsActive)
            {
                var player = FindPlayer(other);
                if (player != null)
                {
                    player.NotifyObstacle(!isDeadly);
                }
            }
        }

        private void ResolveDifficultyManager()
        {
            if (difficultyManager == null)
            {
                difficultyManager = FindObjectOfType<DifficultyManager>();
            }
        }

        private void SubscribeToDifficulty()
        {
            if (difficultyManager != null)
            {
                difficultyManager.OnDifficultyChanged -= HandleDifficultyChanged;
                difficultyManager.OnDifficultyChanged += HandleDifficultyChanged;
            }
        }

        private void UnsubscribeFromDifficulty()
        {
            if (difficultyManager != null)
            {
                difficultyManager.OnDifficultyChanged -= HandleDifficultyChanged;
            }
        }

        private void HandleDifficultyChanged(int level)
        {
            RecalculatePatrolSpeed();
        }

        private void RecalculatePatrolSpeed()
        {
            var baseSpeed = difficultyManager != null ? difficultyManager.DronePatrolSpeed : patrolSpeed;
            PatrolSpeed = Mathf.Max(0f, baseSpeed) * (nightConeActive ? NightConeSpeedMultiplier : 1f);
        }
    }
}
