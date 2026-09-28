using UnityEngine;

namespace CatCourier.Core
{
    public sealed class FollowCamera : MonoBehaviour
    {
        [SerializeField] private Transform target;
        [SerializeField] private float verticalOffset = 2.5f;

        public void Configure(Transform followTarget)
        {
            target = followTarget;
        }

        private void LateUpdate()
        {
            if (target == null)
            {
                return;
            }

            var position = transform.position;
            position.x = target.position.x;
            position.y = target.position.y + verticalOffset;
            transform.position = position;
        }
    }
}
