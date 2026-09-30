using CatCourier.Core;
using CatCourier.Player;
using UnityEngine;

namespace CatCourier.Obstacles
{
    public sealed class RollingObstacle : ObstacleBase
    {
        [SerializeField] private Transform playerTarget;
        [SerializeField, Min(0f)] private float rollingSpeed = Constants.BASE_RUN_SPEED;

        public float RollingSpeed => Mathf.Max(0f, rollingSpeed);
        public Transform Target => playerTarget;

        protected override void ConfigureDefaults()
        {
            type = ObstacleType.Rolling;
            isDeadly = true;
            isDestroyable = false;
            speedMultiplierOnHit = 1f;
        }

        public void SetTarget(Transform target)
        {
            playerTarget = target;
        }

        public void SetPlayerTarget(PlayerController player)
        {
            playerTarget = player != null ? player.transform : null;
        }

        public void SetRollingSpeed(float value)
        {
            rollingSpeed = Mathf.Max(0f, value);
        }

        public void Simulate(float deltaTime)
        {
            InitializeIfNeeded();
            if (!IsActive || RollingSpeed <= 0f)
            {
                return;
            }

            ResolveTarget();
            if (playerTarget == null)
            {
                return;
            }

            var current = transform.position;
            var nextX = Mathf.MoveTowards(current.x, playerTarget.position.x, RollingSpeed * Mathf.Max(0f, deltaTime));
            current.x = nextX;
            transform.position = current;
        }

        public void ResetRolling()
        {
            ResetForChunk();
        }

        protected override void OnEnable()
        {
            base.OnEnable();
            ResolveTarget();
        }

        private void Start()
        {
            ResolveTarget();
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
                    player.NotifyObstacle(!isDeadly, GetComponent<Collider2D>());
                }
            }
        }

        private void ResolveTarget()
        {
            if (playerTarget == null)
            {
                var player = FindObjectOfType<PlayerController>();
                if (player != null)
                {
                    playerTarget = player.transform;
                }
            }
        }
    }
}
