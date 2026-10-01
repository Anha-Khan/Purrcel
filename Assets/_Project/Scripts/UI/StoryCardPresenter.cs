using CatCourier.Core;
using UnityEngine;

namespace CatCourier.UI
{
    /// <summary>
    /// Shows first-time checkpoint story beats published by <see cref="RunCoordinator"/>.
    /// It only displays and dismisses; it never pauses, saves, or changes run state.
    /// </summary>
    public sealed class StoryCardPresenter : MonoBehaviour
    {
        [SerializeField, Min(0.5f)] private float displaySeconds = 4f;

        private RunCoordinator coordinator;
        private string text = string.Empty;
        private float hideAt;

        private void Awake()
        {
            coordinator ??= FindObjectOfType<RunCoordinator>();
        }

        private void OnEnable()
        {
            coordinator ??= FindObjectOfType<RunCoordinator>();
            if (coordinator == null)
            {
                return;
            }

            coordinator.OnStoryBeat -= HandleStoryBeat;
            coordinator.OnStoryBeat += HandleStoryBeat;
        }

        private void OnDisable()
        {
            if (coordinator != null)
            {
                coordinator.OnStoryBeat -= HandleStoryBeat;
            }
        }

        private void Update()
        {
            if (!string.IsNullOrEmpty(text) && Time.unscaledTime >= hideAt)
            {
                // OnGUI plays the lift-away over the final fraction of a second, so the
                // clear lands after the card has left rather than cutting it off.
                text = string.Empty;
            }
        }

        private float shownAt = -1f;

        private void OnGUI()
        {
            if (string.IsNullOrEmpty(text))
            {
                shownAt = -1f;
                return;
            }

            if (shownAt < 0f)
            {
                shownAt = Time.unscaledTime;
            }

            var scale = UiTheme.Scale;

            // A story card arrives mid-run. It drops in and lifts away rather than
            // blinking, so it does not read as a glitch over the gameplay.
            var age = Time.unscaledTime - shownAt;
            var life = Mathf.Max(0.001f, hideAt - shownAt);
            var appear = UiMotion.EaseOutBack(UiMotion.Progress(shownAt, 0.32f));
            var exit = age > life - 0.3f ? UiMotion.EaseInOutCubic((age - (life - 0.3f)) / 0.3f) : 0f;

            var width = Mathf.Min(700f * scale, HubLayout.SafeRect.width - 40f * scale);
            var height = 118f * scale;
            var top = Screen.height - HubLayout.SafeRect.yMax + 46f * scale;
            var target = new Rect(
                HubLayout.SafeRect.x + (HubLayout.SafeRect.width - width) * 0.5f,
                top,
                width, height);

            // Drops from above and slides up as it leaves.
            var offset = (1f - appear) * -34f * scale - UiMotion.EaseInOutCubic(exit) * 26f * scale;
            var outer = UiTheme.Offset(target, 0f, offset);

            UiTheme.Label(outer, text, UiTheme.BodyCentered, UiTheme.Cream);

            var skipWidth = Mathf.Max(96f * scale, UiTheme.Touch);
            var skip = new Rect(outer.xMax - skipWidth, outer.yMax + 4f * scale, skipWidth,
                UiTheme.Touch);
            if (UiTheme.FaceButton(skip, "SKIP", UiTheme.Slate, UiTheme.Cream))
            {
                text = string.Empty;
            }
        }

        private void HandleStoryBeat(StoryBeat beat)
        {
            if (!beat.HasStory)
            {
                return;
            }

            text = beat.Text;
            hideAt = Time.unscaledTime + displaySeconds;
        }
    }
}
