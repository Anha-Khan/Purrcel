using CatCourier.Coins;
using CatCourier.Core;
using CatCourier.Packages;
using CatCourier.Scoring;
using CatCourier.Weather;
using UnityEngine;

namespace CatCourier.UI
{
    /// <summary>
    /// Run readouts. It reads public manager state every frame and draws nothing else.
    /// </summary>
    public sealed class RunHudPresenter : MonoBehaviour
    {
        [SerializeField] private WeatherManager weather;
        [SerializeField] private PackageManager packages;
        [SerializeField] private RunCoordinator coordinator;
        [SerializeField] private ScoreManager score;
        [SerializeField] private CoinManager coins;

        private void Awake()
        {
            weather ??= FindObjectOfType<WeatherManager>();
            packages ??= FindObjectOfType<PackageManager>();
            coordinator ??= FindObjectOfType<RunCoordinator>();
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

            var left = new Rect(16f, 68f, 260f, 160f);
            GUILayout.BeginArea(left);
            GUILayout.Label($"Score: {(score != null ? score.Score : 0)}");
            GUILayout.Label($"Distance: {(score != null ? score.DistanceMeters : 0f):0} m");
            GUILayout.Label($"Coins: {(coins != null ? coins.RunCoins : 0)}");
            if (score != null && score.ComboCount > 0)
            {
                GUILayout.Label($"Combo x{score.ComboMultiplier:0.0} ({score.ComboCount})");
            }

            if (packages != null)
            {
                var type = packages.CurrentPackageType;
                if (type.HasValue)
                {
                    var state = packages.State;
                    GUILayout.Label($"Package: {type.Value} ({state})");
                }
            }

            if (weather != null)
            {
                GUILayout.Label($"Weather: {weather.Current}");
            }

            if (coordinator != null)
            {
                GUILayout.Label($"District: {coordinator.CurrentDistrict}");
            }

            GUILayout.EndArea();
            DrawUrgentTimer();
        }

        private void DrawUrgentTimer()
        {
            if (packages == null || packages.UrgentTimeLimit <= 0f)
            {
                return;
            }

            var remaining = Mathf.Clamp01(packages.UrgentTimeRemaining / packages.UrgentTimeLimit);
            var bar = new Rect(16f, 232f, 220f, 16f);
            GUI.Box(bar, GUIContent.none);
            var fill = new Rect(bar.x, bar.y, bar.width * remaining, bar.height);
            var previous = GUI.color;
            GUI.color = Color.red;
            GUI.DrawTexture(fill, Texture2D.whiteTexture);
            GUI.color = previous;
            GUI.Label(bar, $"Urgent: {packages.UrgentTimeRemaining:0.0}s");
        }
    }
}
