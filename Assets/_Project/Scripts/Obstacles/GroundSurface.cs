using UnityEngine;

namespace CatCourier.Obstacles
{
    public sealed class GroundSurface : MonoBehaviour
    {
        private Transform followTarget;

        public void Follow(Transform target)
        {
            followTarget = target;
        }

        private void LateUpdate()
        {
            if (followTarget == null)
            {
                return;
            }

            var position = transform.position;
            position.x = followTarget.position.x;
            transform.position = position;
        }
    }
}
