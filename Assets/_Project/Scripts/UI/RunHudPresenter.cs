using CatCourier.Coins;
using CatCourier.Core;
using CatCourier.Scoring;
using UnityEngine;

namespace CatCourier.UI
{
    /// <summary>
    /// Run readouts. It reads public manager state every frame and draws nothing else.
    /// </summary>
    public sealed class RunHudPresenter : MonoBehaviour
    {
        [SerializeField] private ScoreManager score;
        [SerializeField] private CoinManager coins;

        private void Awake()
        {
            score ??= FindObjectOfType<ScoreManager>();
            coins ??= FindObjectOfType<CoinManager>();
        }

        private void OnGUI()
        {
            var game = GameManager.Instance;
            if (game == null || (game.State != GameState.Running && game.State != GameState.Paused))
            {
                return;
            }

            var left = new Rect(16f, 62f, 300f, 100f);
            GUILayout.BeginArea(left);
            GUILayout.Label($"Score: {(score != null ? score.Score : 0)}");
            long pastHighScore = 0;
            var history = SaveSystem.Instance?.Data?.runHistory;
            if (history != null)
                foreach (var run in history)
                    if (run != null && run.score > pastHighScore) pastHighScore = run.score;
            GUILayout.Label($"Past high score: {pastHighScore}");
            GUILayout.Label($"Coins collected: {(coins != null ? coins.RunCoins : 0)}");
            GUILayout.EndArea();
        }
    }
}
