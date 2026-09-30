using UnityEngine;

namespace CatCourier.Core
{
    public sealed class FollowCamera : MonoBehaviour
    {
        [SerializeField] private Transform target;
        public void Configure(Transform followTarget) => target = followTarget;

        private void LateUpdate()
        {
            if (target == null) return;
            // Follow the run horizontally while keeping the authored vertical
            // framing fixed. This keeps the sky, town, and road aligned while
            // the cat jumps and lands.
            var position = transform.position;
            position.x = target.position.x;
            transform.position = position;
        }
    }
}
