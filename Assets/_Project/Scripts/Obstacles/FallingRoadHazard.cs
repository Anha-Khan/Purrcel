using CatCourier.Core;
using CatCourier.Player;
using UnityEngine;

namespace CatCourier.Obstacles
{
    public sealed class FallingRoadHazard : MonoBehaviour
    {
        private PlayerController player;
        private float landingY;
        private bool falling;

        public void Configure(PlayerController runner, float floorY)
        {
            player = runner;
            landingY = floorY;
            transform.position = new Vector3(transform.position.x, floorY + 3.6f, transform.position.z);
        }

        private void Update()
        {
            if (player == null || player.State == PlayerState.Dead) return;
            var distance = transform.position.x - player.DistanceMeters;
            if (!falling && distance < 5.5f && distance > -1f) falling = true;
            if (!falling) return;
            var position = transform.position;
            position.y = Mathf.MoveTowards(position.y, landingY, Time.deltaTime * 8f);
            transform.position = position;
        }
    }
}
