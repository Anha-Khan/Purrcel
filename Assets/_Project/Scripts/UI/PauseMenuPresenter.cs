using CatCourier.Audio;
using CatCourier.Core;
using CatCourier.Scoring;
using UnityEngine;

namespace CatCourier.UI
{
    /// <summary>
    /// Pause menu over a paused run. It calls <see cref="GameManager"/> contract methods only.
    /// </summary>
    public sealed class PauseMenuPresenter : MonoBehaviour
    {
        [SerializeField] private ScoreManager score;

        private const float EntranceSeconds = 0.28f;
        private float shownAt = -1f;

        private void Awake()
        {
            score ??= FindObjectOfType<ScoreManager>();
        }

        private void OnGUI()
        {
            var game = GameManager.Instance;
            if (game == null || game.State != GameState.Paused)
            {
                shownAt = -1f;
                return;
            }

            if (shownAt < 0f)
            {
                shownAt = Time.unscaledTime;
            }

            var scale = UiTheme.Scale;
            var entrance = UiMotion.EaseOutCubic(UiMotion.Progress(shownAt, EntranceSeconds));

            // The scene is frozen behind this, but it is still bright painted scenery, so
            // the panel still needs to lift itself off it.
            UiTheme.Scrim(0.55f * entrance);

            var width = Mathf.Min(420f * scale, HubLayout.SafeRect.width - 32f * scale);
            var height = Mathf.Min(330f * scale, HubLayout.SafeRect.height - 40f * scale);
            var target = DeathScreenPresenter.CenteredArea(width, height, Screen.height * 0.5f - height * 0.5f);
            var outer = UiTheme.ScaledAboutCentre(target, Mathf.Lerp(0.95f, 1f, entrance));
            var content = UiTheme.Panel(outer, entrance, UiTheme.Teal);

            var y = content.y;
            UiTheme.Label(new Rect(content.x, y, content.width, 38f * scale), "PAUSED",
                UiTheme.Title, UiTheme.Cream);
            y += 46f * scale;

            var rowHeight = 30f * scale;
            UiTheme.StatRow(new Rect(content.x, y, content.width, rowHeight),
                "SCORE", (score != null ? score.Score : 0).ToString("N0"), UiTheme.Cream);
            y += rowHeight;
            UiTheme.StatRow(new Rect(content.x, y, content.width, rowHeight),
                "DISTANCE", $"{(score != null ? score.DistanceMeters : 0f):0} m", UiTheme.Cream);
            y += rowHeight + 12f * scale;

            var audio = AudioManager.Instance;
            var muted = audio != null && audio.IsMuted;

            var buttonHeight = Mathf.Max(UiTheme.Touch, 52f * scale);
            var button = new Rect(content.x, y, content.width, buttonHeight);
            if (UiTheme.FaceButton(button, "RESUME", UiTheme.Orange, UiTheme.Ink))
            {
                audio?.PlaySfx(SfxId.UiTap);
                game.ResumeRun();
            }

            y += buttonHeight + 8f * scale;

            if (UiTheme.FaceButton(new Rect(content.x, y, content.width, buttonHeight),
                    muted ? "UNMUTE" : "MUTE",
                    muted ? UiTheme.Danger : UiTheme.Slate, UiTheme.Cream))
            {
                audio?.PlaySfx(SfxId.UiTap);
                audio?.SetMuted(!muted);
            }

            y += buttonHeight + 8f * scale;

            // Abandoning banks the coins, so it is styled as a quieter, secondary action
            // rather than a peer of Resume.
            if (UiTheme.FaceButton(new Rect(content.x, y, content.width, buttonHeight),
                    "ABANDON RUN", UiTheme.Slate, UiTheme.Muted))
            {
                audio?.PlaySfx(SfxId.UiTap);
                game.AbandonRun();
            }
        }
    }
}