using System;
using CatCourier.Player;
using UnityEngine;

namespace CatCourier.Generation
{
    [DisallowMultipleComponent]
    public sealed class CheckpointMarker : MonoBehaviour
    {
        public event Action<CheckpointMarker> Reached;

        private void OnTriggerEnter2D(Collider2D other)
        {
            if (other.GetComponentInParent<PlayerController>() != null)
            {
                Reached?.Invoke(this);
            }
        }
    }
}
