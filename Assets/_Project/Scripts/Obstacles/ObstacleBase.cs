using CatCourier.Player;
using UnityEngine;

namespace CatCourier.Obstacles
{
    public enum ObstacleType
    {
        Static,
        Stumble,
        Bounce,
        Patrol,
        Rolling
    }

    public abstract class ObstacleBase : MonoBehaviour
    {
        public ObstacleType type;
        public bool isDeadly;
        public bool isDestroyable;
        public float speedMultiplierOnHit = 1f;

        private bool initialized;
        private bool chunkActive = true;
        private Vector3 initialLocalPosition;

        public bool IsActive => chunkActive && isActiveAndEnabled;
        public bool IsChunkActive => chunkActive;
        protected Vector3 InitialLocalPosition => initialLocalPosition;

        protected virtual void Awake()
        {
            InitializeIfNeeded();
        }

        protected virtual void OnEnable()
        {
            InitializeIfNeeded();
            chunkActive = true;
            ResetForChunk();
        }

        protected virtual void OnDisable()
        {
            chunkActive = false;
        }

        public virtual void ResetForChunk()
        {
            InitializeIfNeeded();
            transform.localPosition = initialLocalPosition;
            ResetRuntimeState();
        }

        public void SetChunkActive(bool active)
        {
            chunkActive = active;
            if (active)
            {
                if (!gameObject.activeSelf)
                {
                    gameObject.SetActive(true);
                }
                else
                {
                    ResetForChunk();
                }
            }
            else
            {
                gameObject.SetActive(false);
            }
        }

        protected void InitializeIfNeeded()
        {
            if (initialized)
            {
                return;
            }

            initialLocalPosition = transform.localPosition;
            ConfigureDefaults();
            initialized = true;
        }

        protected virtual void ConfigureDefaults()
        {
        }

        protected virtual void ResetRuntimeState()
        {
        }

        protected static PlayerController FindPlayer(Collider2D other)
        {
            return other != null ? other.GetComponentInParent<PlayerController>() : null;
        }
    }
}
