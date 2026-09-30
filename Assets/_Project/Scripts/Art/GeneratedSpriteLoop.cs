using UnityEngine;

namespace CatCourier.Art
{
    [RequireComponent(typeof(SpriteRenderer))]
    public sealed class GeneratedSpriteLoop : MonoBehaviour
    {
        [SerializeField] private Sprite[] frames;
        [SerializeField] private float framesPerSecond = 6f;
        [SerializeField] private float swayDegrees;
        private SpriteRenderer target;
        private Quaternion baseRotation;

        public void Configure(Sprite[] sequence, float fps, float sway = 0f)
        {
            frames = sequence;
            framesPerSecond = Mathf.Max(1f, fps);
            swayDegrees = sway;
            target = GetComponent<SpriteRenderer>();
            target.sprite = frames != null && frames.Length > 0 ? frames[0] : null;
        }

        private void Awake()
        {
            target = GetComponent<SpriteRenderer>();
            baseRotation = transform.localRotation;
        }

        private void Update()
        {
            if (frames == null || frames.Length == 0) return;
            target.sprite = frames[Mathf.FloorToInt(Time.time * framesPerSecond) % frames.Length];
            if (swayDegrees != 0f)
                transform.localRotation = baseRotation * Quaternion.Euler(0f, 0f,
                    Mathf.Sin(Time.time * 2f) * swayDegrees);
        }
    }
}
