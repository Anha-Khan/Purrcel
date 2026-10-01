using System;
using UnityEngine;
using CatCourier.Core;

namespace CatCourier.Weather
{
    public sealed class WeatherManager : MonoBehaviour
    {
        public const float RainSpeedMultiplier = 0.92f;
        public const float NightExposureAdjustment = -1.2f;
        public const float NightDronePatrolSpeedMultiplier = 1.5f;

        public WeatherType Current { get; private set; } = WeatherType.Clear;
        public WeatherType NextRunWeather { get; private set; } = WeatherType.Clear;
        public int WindDirection { get; private set; } = -1;
        public float SpeedMultiplier => Current == WeatherType.Rain ? RainSpeedMultiplier : 1f;
        public float LandingSlideDistance => Current == WeatherType.Rain ? Constants.RAIN_LANDING_SLIDE : 0f;
        public float AirborneDriftAcceleration => Current == WeatherType.Wind
            ? Constants.WIND_DRIFT_FORCE * WindDirection
            : 0f;
        public float VisibilityExposureAdjustment => Current == WeatherType.Night ? NightExposureAdjustment : 0f;
        public float DronePatrolSpeedMultiplier => Current == WeatherType.Night
            ? NightDronePatrolSpeedMultiplier
            : 1f;
        public bool IsNight => Current == WeatherType.Night;

        /// <summary>
        /// Fired when the run's weather activates. The four granular events that used to
        /// sit beside this were invoked but never subscribed to; the player reads the
        /// properties directly, so they were removed rather than left as dead API.
        /// </summary>
        public event Action<WeatherType> OnWeatherChanged;

        [SerializeField] private int randomSeed;

        private System.Random random;
        private bool hasNextRunWeather;

        private void Awake()
        {
            SetSeed(randomSeed);
        }

        public void SetSeed(int seed)
        {
            random = new System.Random(seed);
            hasNextRunWeather = false;
            SelectNextWeather();
        }

        public void SelectNextWeather()
        {
            if (random == null)
            {
                random = new System.Random(0);
            }

            NextRunWeather = SelectWeightedWeather(random);
            hasNextRunWeather = true;
        }

        public WeatherType ActivateNextWeather()
        {
            if (!hasNextRunWeather)
            {
                SelectNextWeather();
            }

            Current = NextRunWeather;
            WindDirection = SelectWindDirection(random);
            OnWeatherChanged?.Invoke(Current);
            SelectNextWeather();
            return Current;
        }

        public WeatherType StartRun() => ActivateNextWeather();

        public void ResetRun()
        {
            Current = WeatherType.Clear;
            WindDirection = -1;
            OnWeatherChanged?.Invoke(Current);
        }

        public static WeatherType SelectWeightedWeather(System.Random source)
        {
            if (source == null)
            {
                throw new ArgumentNullException(nameof(source));
            }

            var roll = source.Next(100);
            if (roll < 40)
            {
                return WeatherType.Clear;
            }

            if (roll < 65)
            {
                return WeatherType.Rain;
            }

            if (roll < 90)
            {
                return WeatherType.Night;
            }

            return WeatherType.Wind;
        }

        private static int SelectWindDirection(System.Random source)
        {
            return source.NextDouble() < 0.7d ? -1 : 1;
        }
    }
}
