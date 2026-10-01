using CatCourier.Generation;
using CatCourier.Player;
using UnityEngine;

namespace CatCourier.Coins
{
    public sealed class BoostPickup : MonoBehaviour, IChunkPoolResettable
    {
        [SerializeField] private float duration = 4f;
        private bool collected;

        private void OnTriggerEnter2D(Collider2D other)
        {
            if (collected) return;
            var player = other.GetComponentInParent<PlayerController>();
            if (player == null) return;
            collected = true;
            player.GrantBoost(duration);
            gameObject.SetActive(false);
        }

        /// <summary>
        /// Without this a recycled chunk returned a boost that had already been
        /// consumed, so the pickup was permanently dead after its first use.
        /// </summary>
        public void OnChunkActivated()
        {
            collected = false;
            gameObject.SetActive(true);
        }

        public void OnChunkDeactivated()
        {
        }
    }
}
