using CatCourier.Player;
using UnityEngine;

namespace CatCourier.Coins
{
    public sealed class BoostPickup : MonoBehaviour
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
    }
}
