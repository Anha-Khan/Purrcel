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
                text = string.Empty;
            }
        }

        private void OnGUI()
        {
            if (string.IsNullOrEmpty(text))
            {
                return;
            }

            var area = DeathScreenPresenter.CenteredArea(640f, 110f, 40f);
            GUILayout.BeginArea(area, GUI.skin.box);
            GUILayout.Label(text, GUILayout.ExpandHeight(true));
            if (GUILayout.Button("Skip", GUILayout.Width(90f), GUILayout.Height(24f)))
            {
                text = string.Empty;
            }

            GUILayout.EndArea();
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
