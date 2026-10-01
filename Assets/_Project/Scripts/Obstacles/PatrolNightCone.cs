using CatCourier.Core;
using CatCourier.Player;
using CatCourier.Weather;
using UnityEngine;

namespace CatCourier.Obstacles
{
    [DisallowMultipleComponent]
    public sealed class PatrolNightCone : MonoBehaviour
    {
        [SerializeField] private PatrolObstacle owner;

        // Resolved once. This used to call FindObjectOfType on every trigger enter.
        private WeatherManager weather;

        private void Awake()
        {
            owner ??= GetComponentInParent<PatrolObstacle>();
            weather = FindObjectOfType<WeatherManager>();
        }

        private void OnTriggerEnter2D(Collider2D other)
        {
            if (other.GetComponentInParent<PlayerController>() == null || owner == null)
            {
                return;
            }

            if (weather != null && weather.Current == WeatherType.Night)
            {
                owner.SetNightConeActive(true);
            }
        }

        private void OnTriggerExit2D(Collider2D other)
        {
            if (other.GetComponentInParent<PlayerController>() != null && owner != null)
            {
                owner.SetNightConeActive(false);
            }
        }

        private void OnDisable()
        {
            owner?.SetNightConeActive(false);
        }
    }
}
