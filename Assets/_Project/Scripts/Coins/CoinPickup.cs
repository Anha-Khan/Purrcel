using CatCourier.Core;
using CatCourier.Generation;
using CatCourier.Player;
using UnityEngine;

namespace CatCourier.Coins
{
    public sealed class CoinPickup : MonoBehaviour, IChunkPoolResettable
    {
        [SerializeField] private int value = Constants.COIN_BASE_VALUE;
        [SerializeField] private CoinManager coinManager;
        [SerializeField] private PlayerController player;

        public int Value => value;
        public bool Collected => collected;

        private bool collected;

        private void Awake()
        {
            player ??= FindObjectOfType<PlayerController>();
        }

        private void Update()
        {
            if (collected || player == null || coinManager == null || coinManager.MagnetRadius <= 0f)
            {
                return;
            }

            TryCollectFromMagnet(player.transform.position);
        }

        public void Configure(CoinManager manager, int coinValue, PlayerController targetPlayer = null)
        {
            coinManager = manager;
            value = coinValue;
            player = targetPlayer;
        }

        private void OnTriggerEnter2D(Collider2D other)
        {
            if (other.GetComponentInParent<PlayerController>() == null)
            {
                return;
            }

            Collect();
        }

        public bool TryCollectFromMagnet(Vector3 playerPosition)
        {
            if (collected || coinManager == null || coinManager.MagnetRadius <= 0f)
            {
                return false;
            }

            var distance = Vector3.Distance(transform.position, playerPosition);
            return distance <= coinManager.MagnetRadius && Collect();
        }

        public void OnChunkActivated()
        {
            collected = false;
            if (!gameObject.activeSelf)
            {
                gameObject.SetActive(true);
            }
        }

        public void OnChunkDeactivated()
        {
            collected = false;
        }

        public bool Collect()
        {
            if (collected)
            {
                return false;
            }

            coinManager ??= FindObjectOfType<CoinManager>();
            if (coinManager == null)
            {
                return false;
            }

            collected = true;
            coinManager.Collect(value, transform.position);
            gameObject.SetActive(false);
            return true;
        }
    }
}
