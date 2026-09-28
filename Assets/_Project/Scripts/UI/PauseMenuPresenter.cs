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

        private void Awake()
        {
            score ??= FindObjectOfType<ScoreManager>();
        }

        private void OnGUI()
        {
            var game = GameManager.Instance;
            if (game == null || game.State != GameState.Paused)
            {
                return;
            }

            var audio = AudioManager.Instance;
            var muted = audio != null && audio.IsMuted;
            var area = new Rect(Screen.width * 0.5f - 200f, Screen.height * 0.5f - 160f, 400f, 320f);
            GUILayout.BeginArea(area, GUI.skin.box);
            GUILayout.Label("Paused");
            GUILayout.Label($"Score so far: {(score != null ? score.Score : 0)}");
            GUILayout.Label($"Distance: {(score != null ? score.DistanceMeters : 0f):0} m");
            GUILayout.Space(8f);
            if (GUILayout.Button("Resume", GUILayout.Height(40f)))
            {
                audio?.PlaySfx(SfxId.UiTap);
                game.ResumeRun();
            }

            if (GUILayout.Button(muted ? "Unmute" : "Mute", GUILayout.Height(40f)))
            {
                audio?.SetMuted(!muted);
            }

            if (GUILayout.Button("Abandon Run", GUILayout.Height(40f)))
            {
                audio?.PlaySfx(SfxId.UiTap);
                game.ReturnToHub();
            }

            GUILayout.EndArea();
        }
    }
}
