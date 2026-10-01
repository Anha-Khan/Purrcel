using CatCourier.Coins;
using CatCourier.Core;
using CatCourier.Packages;
using CatCourier.Scoring;
using UnityEngine;

namespace CatCourier.UI
{
    /// <summary>
    /// Run readouts. It reads public manager state and draws nothing else.
    /// </summary>
    public sealed class RunHudPresenter : MonoBehaviour
    {
        [SerializeField] private ScoreManager score;
        [SerializeField] private CoinManager coins;
        [SerializeField] private PackageManager packages;

        // Recomputed only when the save changes. Walking runHistory twice per OnGUI
        // call rescanned the list every frame for a value that changes only when a run
        // is banked.
        private long pastHighScore;
        private int cachedRunCount = -1;

        private void Awake()
        {
            score ??= FindObjectOfType<ScoreManager>();
            coins ??= FindObjectOfType<CoinManager>();
            packages ??= FindObjectOfType<PackageManager>();
        }

        private void OnGUI()
        {
            var game = GameManager.Instance;
            if (game == null || (game.State != GameState.Running && game.State != GameState.Paused))
            {
                return;
            }

            var safe = HubLayout.SafeRect;
            var left = new Rect(safe.x + 16f, Screen.height - safe.yMax + 62f, 300f, 100f);
            GUILayout.BeginArea(left);
            GUILayout.Label($"Score: {(score != null ? score.Score : 0)}");
            GUILayout.Label($"Past high score: {PastHighScore()}");
            GUILayout.Label($"Coins collected: {(coins != null ? coins.RunCoins : 0)}");
            DrawPackageStatus();
            GUILayout.EndArea();
        }

        /// <summary>
        /// The carried parcel and its urgent timer. Nothing displayed these before, so a
        /// player had no way to see the countdown that can lose a package, nor that a
        /// fragile parcel makes a hard landing fatal.
        /// </summary>
        private void DrawPackageStatus()
        {
            if (packages == null || !packages.HasAssignedPackage)
            {
                return;
            }

            var type = packages.CurrentPackageType;
            GUILayout.Label($"Carrying: {type}");
            if (type == PackageType.Urgent && packages.UrgentTimeLimit > 0f)
            {
                var remaining = Mathf.Max(0f, packages.UrgentTimeRemaining);
                GUILayout.Label($"Deliver in {remaining:0.0}s");
            }

            if (type == PackageType.Fragile)
            {
                GUILayout.Label("Fragile: do not land hard");
            }
        }

        private long PastHighScore()
        {
            var history = SaveSystem.Instance?.Data?.runHistory;
            if (history == null)
            {
                return 0;
            }

            if (history.Count == cachedRunCount)
            {
                return pastHighScore;
            }

            cachedRunCount = history.Count;
            long best = 0;
            foreach (var run in history)
            {
                if (run != null && run.score > best)
                {
                    best = run.score;
                }
            }

            pastHighScore = best;
            return pastHighScore;
        }
    }
}
