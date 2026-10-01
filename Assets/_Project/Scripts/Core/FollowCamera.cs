using UnityEngine;

namespace CatCourier.Core
{
    public sealed class FollowCamera : MonoBehaviour
    {
        [SerializeField] private Transform target;

        private float shakeRemaining;
        private float shakeDuration;
        private float shakeMagnitude;
        private float baseY;
        private bool hasBaseY;

        public bool IsShaking => shakeRemaining > 0f;

        public void Configure(Transform followTarget)
        {
            target = followTarget;
            Subscribe();
        }

        private void OnEnable() => Subscribe();

        private void OnDisable() => Unsubscribe();

        private void OnDestroy() => Unsubscribe();

        private void Subscribe()
        {
            if (target == null) return;
            var player = target.GetComponent<Player.PlayerController>();
            // PlayerController raised this on land and death, but nothing listened, so
            // the SHAKE_LAND_* and SHAKE_DEATH_* constants were inert.
            if (player == null) return;
            player.OnScreenShakeRequested -= RequestShake;
            player.OnScreenShakeRequested += RequestShake;
        }

        private void Unsubscribe()
        {
            if (target == null) return;
            var player = target.GetComponent<Player.PlayerController>();
            if (player != null) player.OnScreenShakeRequested -= RequestShake;
        }

        /// <summary>Starts a decaying positional shake, restarting cleanly if one is running.</summary>
        public void RequestShake(float magnitude, float duration)
        {
            if (duration <= 0f || magnitude <= 0f) return;
            shakeMagnitude = magnitude;
            shakeDuration = duration;
            shakeRemaining = duration;
        }

        private void LateUpdate() => SimulateShakeStep(Time.deltaTime);

        /// <summary>
        /// Exposed so a test can play a shake out deterministically; LateUpdate calls it
        /// with the frame delta.
        /// </summary>
        public void SimulateShakeStep(float deltaTime)
        {
            if (target == null) return;
            if (!hasBaseY)
            {
                baseY = transform.position.y;
                hasBaseY = true;
            }

            // Follow the run horizontally while keeping the authored vertical
            // framing fixed. This keeps the sky, town, and road aligned while
            // the cat jumps and lands.
            var position = transform.position;
            position.x = target.position.x;
            position.y = baseY;

            if (shakeRemaining > 0f)
            {
                shakeRemaining = Mathf.Max(0f, shakeRemaining - Mathf.Max(0f, deltaTime));
                // Rebuilt from baseY every frame, so the offset cannot accumulate.
                var falloff = shakeDuration > 0f ? shakeRemaining / shakeDuration : 0f;
                var strength = shakeMagnitude * falloff;
                position.x += (Mathf.PerlinNoise(Time.time * 24f, 0.37f) - 0.5f) * 2f * strength;
                position.y = baseY + (Mathf.PerlinNoise(0.71f, Time.time * 24f) - 0.5f) * 2f * strength;
            }

            transform.position = position;
        }
    }
}
