using CatCourier.Audio;
using CatCourier.Core;
using CatCourier.Monetization;
using UnityEngine;

namespace CatCourier.UI
{
    /// <summary>
    /// Shows the final run result, the continue decision, and the ad states for both.
    /// It only calls <see cref="GameManager"/> and <see cref="AdManager"/> contract methods.
    /// </summary>
    public sealed class DeathScreenPresenter : MonoBehaviour
    {
        [SerializeField] private bool showAdContinue = true;

        private float continueSecondsRemaining;
        private bool continueDecisionActive;
        private string adMessage = string.Empty;
        private bool rewardGranted;
        private bool shareCopied;

        private GameManager game;

        private void OnEnable()
        {
            if (game == null)
            {
                game = GameManager.Instance;
            }

            if (game != null)
            {
                game.OnContinueOffered -= HandleContinueOffered;
                game.OnContinueOffered += HandleContinueOffered;
                game.OnStateChanged -= HandleStateChanged;
                game.OnStateChanged += HandleStateChanged;
            }
        }

        private void OnDisable()
        {
            if (game != null)
            {
                game.OnContinueOffered -= HandleContinueOffered;
                game.OnStateChanged -= HandleStateChanged;
            }
        }

        private void OnGUI()
        {
            game ??= GameManager.Instance;
            if (game == null || game.State != GameState.Dead)
            {
                return;
            }

            var result = game.LastRun;
            var delivered = result.PackagesDelivered > 0;
            var area = new Rect(Screen.width * 0.5f - 260f, 60f, 520f, Screen.height - 120f);
            GUILayout.BeginArea(area, GUI.skin.box);
            GUILayout.Label(delivered ? "DELIVERED" : "WIPED OUT");
            GUILayout.Label($"Score: {result.Score}");
            GUILayout.Label($"Distance: {result.DistanceMeters:0} m");
            GUILayout.Label($"Packages delivered: {result.PackagesDelivered}");
            GUILayout.Label($"Run coins: {result.CoinsCollected}");
            GUILayout.Label($"Bank: {SaveSystem.Instance?.TotalCoins ?? 0}");
            GUILayout.Label($"District: {result.DistrictReached}");

            DrawContinueBlock();
            DrawAdBlock();
            DrawResultActions();
            GUILayout.EndArea();
        }

        private void DrawContinueBlock()
        {
            if (!continueDecisionActive)
            {
                return;
            }

            GUILayout.Space(8f);
            GUILayout.Label($"Continue? {Mathf.Max(0f, continueSecondsRemaining):0.0}s   (remaining: {game.ContinuesLeft})");
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Keep Running", GUILayout.Height(36f)))
            {
                AudioManager.Instance?.PlaySfx(SfxId.UiTap);
                if (game.UseContinue())
                {
                    continueDecisionActive = false;
                }
            }

            if (GUILayout.Button("No Thanks", GUILayout.Height(36f)))
            {
                AudioManager.Instance?.PlaySfx(SfxId.UiTap);
                continueDecisionActive = false;
                game.DeclineContinue();
            }

            GUILayout.EndHorizontal();
        }

        private void DrawAdBlock()
        {
            var ads = AdManager.Instance;
            if (!showAdContinue || ads == null || continueDecisionActive)
            {
                return;
            }

            GUILayout.Space(6f);
            GUILayout.Label(rewardGranted ? "Continue reward already used this run." : "Watch an ad for an extra continue.");
            if (!rewardGranted && !ads.IsBusy && GUILayout.Button("Watch Ad", GUILayout.Height(32f)))
            {
                AudioManager.Instance?.PlaySfx(SfxId.UiTap);
                ads.ShowContinueRewarded(HandleRewardResult);
            }

            if (ads.IsBusy)
            {
                GUILayout.Label("Ad loading...");
            }

            if (!string.IsNullOrEmpty(adMessage))
            {
                GUILayout.Label(adMessage);
            }
        }

        private void DrawResultActions()
        {
            GUILayout.Space(10f);
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Copy Score", GUILayout.Height(32f)))
            {
                AudioManager.Instance?.PlaySfx(SfxId.UiTap);
                GUIUtility.systemCopyBuffer = BuildShareText();
                shareCopied = true;
            }

            if (GUILayout.Button("Back to Hub", GUILayout.Height(32f)))
            {
                AudioManager.Instance?.PlaySfx(SfxId.UiTap);
                var ads = AdManager.Instance;
                if (ads == null)
                {
                    game.ReturnToHub();
                }
                else
                {
                    ads.ShowDeathInterstitial(game.ReturnToHub);
                }
            }

            GUILayout.EndHorizontal();
            if (shareCopied)
            {
                GUILayout.Label("Score copied to clipboard.");
            }
        }

        private string BuildShareText()
        {
            var result = game.LastRun;
            var header = result.PackagesDelivered > 0 ? "Delivered" : "Wiped out";
            return $"Cat Courier - {header}: {result.Score} pts, {result.DistanceMeters:0} m, " +
                   $"{result.PackagesDelivered} package(s) in {result.DistrictReached}.";
        }

        private void HandleRewardResult(bool granted)
        {
            rewardGranted = granted;
            adMessage = granted
                ? "Extra continue granted."
                : AdManager.Instance != null ? AdManager.Instance.StatusMessage : "Reward unavailable.";
            if (granted)
            {
                continueDecisionActive = game != null && game.ContinuesLeft > 0;
            }
        }

        private void HandleContinueOffered(float secondsRemaining)
        {
            continueSecondsRemaining = secondsRemaining;
            continueDecisionActive = secondsRemaining > 0f;
        }

        private void HandleStateChanged(GameState state)
        {
            if (state != GameState.Dead)
            {
                continueDecisionActive = false;
                rewardGranted = false;
                adMessage = string.Empty;
            }
        }
    }
}
