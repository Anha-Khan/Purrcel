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

        private const float EntranceSeconds = 0.45f;
        private float shownAt = -1f;

        private void OnGUI()
        {
            game ??= GameManager.Instance;
            if (game == null || game.State != GameState.Dead)
            {
                shownAt = -1f;
                return;
            }

            if (shownAt < 0f)
            {
                shownAt = Time.unscaledTime;
            }

            var result = game.LastRun;
            var delivered = result.PackagesDelivered > 0;
            var scale = UiTheme.Scale;
            var entrance = UiMotion.EaseOutCubic(UiMotion.Progress(shownAt, EntranceSeconds));

            // Wash over the frozen scene. Without it the panel sits on a moving road and
            // the text has to win that fight alone.
            UiTheme.Scrim(0.72f * entrance);

            var width = Mathf.Min(560f * scale, HubLayout.SafeRect.width - 32f * scale);
            var height = Mathf.Min(Screen.height - 90f * scale, HubLayout.SafeRect.height - 40f * scale);
            var target = CenteredArea(width, height, 40f * scale);

            // Settles from slightly small rather than fading in place, so the panel reads
            // as arriving instead of appearing.
            var outer = UiTheme.ScaledAboutCentre(target, Mathf.Lerp(0.94f, 1f, entrance));
            var content = UiTheme.Panel(outer, entrance, delivered ? UiTheme.Teal : UiTheme.Danger);

            var y = content.y;
            var rowHeight = 34f * scale;

            UiTheme.Label(new Rect(content.x, y, content.width, 34f * scale),
                delivered ? "DELIVERED" : "WIPED OUT", UiTheme.Title,
                delivered ? UiTheme.Teal : UiTheme.Danger);
            y += 40f * scale;

            // Score is the reason the player opened this screen; everything else is
            // supporting detail at caption size.
            UiTheme.Label(new Rect(content.x, y, content.width, 62f * scale),
                result.Score.ToString("N0"), UiTheme.Display, UiTheme.Cream);
            y += 64f * scale;

            var half = content.width * 0.5f;
            UiTheme.StatRow(new Rect(content.x, y, half - 8f * scale, rowHeight),
                "DISTANCE", $"{result.DistanceMeters:0} m", UiTheme.Cream);
            UiTheme.StatRow(new Rect(content.x + half + 8f * scale, y, half - 8f * scale, rowHeight),
                "DISTRICT", result.DistrictReached.ToString(), UiTheme.Cream);
            y += rowHeight;

            UiTheme.StatRow(new Rect(content.x, y, half - 8f * scale, rowHeight),
                "PARCELS", result.PackagesDelivered.ToString(), UiTheme.Cream);
            UiTheme.StatRow(new Rect(content.x + half + 8f * scale, y, half - 8f * scale, rowHeight),
                "RUN COINS", result.CoinsCollected.ToString(), UiTheme.Gold);
            y += rowHeight;

            // CoinManager.Bank is the live total including this run. Reading the
            // save here showed the pre-run value next to the HUD's correct number.
            UiTheme.StatRow(new Rect(content.x, y, content.width, rowHeight),
                "BANKED", BankedCoins().ToString("N0"), UiTheme.Gold);
            y += rowHeight + 10f * scale;

            y = DrawContinueBlock(content, y, width);
            y = DrawAdBlock(content, y, width);
            DrawResultActions(content, y, width);
        }

        /// <summary>
        /// A fixed-size box centred inside the safe area, so a notch in landscape
        /// cannot push the result panel off screen or under the cutout.
        /// </summary>
        internal static Rect CenteredArea(float width, float height, float topMargin = 0f)
        {
            var safe = HubLayout.SafeRect;
            var x = safe.x + (safe.width - width) * 0.5f;
            var y = Screen.height - safe.yMax + topMargin;
            return new Rect(x, y, Mathf.Min(width, safe.width), Mathf.Min(height, safe.height - topMargin));
        }

        private static int BankedCoins()
        {
            var coins = FindObjectOfType<Coins.CoinManager>();
            return coins != null ? coins.Bank : SaveSystem.Instance?.TotalCoins ?? 0;
        }

        /// <summary>
        /// The continue offer is the one time-pressured decision in the game, so the
        /// remaining time is a depleting bar rather than a number the player has to read
        /// and mentally subtract.
        /// </summary>
        private float DrawContinueBlock(Rect content, float y, float width)
        {
            if (!continueDecisionActive)
            {
                return y;
            }

            var scale = UiTheme.Scale;
            var remaining = Mathf.Max(0f, continueSecondsRemaining);
            var critical = remaining <= 2f;
            var face = critical ? UiTheme.Danger : UiTheme.Gold;

            UiTheme.Label(new Rect(content.x, y, content.width, 26f * scale),
                $"CONTINUE?   {game.ContinuesLeft} LEFT", UiTheme.Caption, UiTheme.Muted);
            y += 26f * scale;

            var track = new Rect(content.x, y, content.width, 8f * scale);
            var pulse = critical ? 0.5f + 0.5f * UiMotion.Pulse(10f) : 1f;
            UiTheme.Bar(track, Mathf.Clamp01(remaining / 5f), UiTheme.Fade(face, pulse));
            y += 18f * scale;

            var buttonWidth = (content.width - 12f * scale) * 0.5f;
            var buttonHeight = Mathf.Max(UiTheme.Touch, 56f * scale);
            var primary = new Rect(content.x, y, buttonWidth, buttonHeight);
            var secondary = new Rect(content.x + buttonWidth + 12f * scale, y, buttonWidth, buttonHeight);

            if (UiTheme.FaceButton(primary, "KEEP RUNNING", UiTheme.Orange, UiTheme.Ink))
            {
                AudioManager.Instance?.PlaySfx(SfxId.UiTap);
                if (game.UseContinue())
                {
                    continueDecisionActive = false;
                }
            }

            if (UiTheme.FaceButton(secondary, "NO THANKS", UiTheme.Slate, UiTheme.Cream))
            {
                AudioManager.Instance?.PlaySfx(SfxId.UiTap);
                continueDecisionActive = false;
                game.DeclineContinue();
            }

            _ = width;
            return y + buttonHeight + 10f * scale;
        }

        private float DrawAdBlock(Rect content, float y, float width)
        {
            var ads = AdManager.Instance;
            if (!showAdContinue || ads == null || continueDecisionActive)
            {
                return y;
            }

            var scale = UiTheme.Scale;

            // No backend means nothing to offer. An empty block reads as a bug, so say
            // it once and leave it out of the layout entirely.
            if (!ads.HasBackend)
            {
                return y;
            }

            if (rewardGranted)
            {
                UiTheme.Label(new Rect(content.x, y, content.width, 28f * scale),
                    "Continue reward already used this run.", UiTheme.Caption, UiTheme.Muted);
                return y + 32f * scale;
            }

            var buttonWidth = Mathf.Max(content.width * 0.42f, UiTheme.Touch * 1.6f);
            var buttonHeight = Mathf.Max(UiTheme.Touch, 52f * scale);
            var watch = new Rect(content.x, y, buttonWidth, buttonHeight);
            var copy = new Rect(content.x + buttonWidth + 12f * scale, y,
                content.width - buttonWidth - 12f * scale, buttonHeight);

            if (!ads.IsBusy && UiTheme.FaceButton(watch, "WATCH AD", UiTheme.Teal, UiTheme.Cream))
            {
                AudioManager.Instance?.PlaySfx(SfxId.UiTap);
                ads.ShowContinueRewarded(HandleRewardResult);
            }

            var message = ads.IsBusy
                ? "Loading..."
                : !string.IsNullOrEmpty(adMessage) ? adMessage : "Extra continue";
            UiTheme.Label(copy, message, UiTheme.Caption, ads.IsBusy ? UiTheme.Muted : UiTheme.Teal);

            _ = width;
            return y + buttonHeight + 10f * scale;
        }

        private void DrawResultActions(Rect content, float y, float width)
        {
            var scale = UiTheme.Scale;
            var buttonHeight = Mathf.Max(UiTheme.Touch, 50f * scale);
            var half = (content.width - 12f * scale) * 0.5f;

            // Returning to the hub is the primary action here: the run is over either
            // way, so it carries the orange rather than sitting as a quiet third option.
            var back = new Rect(content.x, y, half, buttonHeight);
            var copy = new Rect(content.x + half + 12f * scale, y, half, buttonHeight);

            if (UiTheme.FaceButton(back, "BACK TO HUB", UiTheme.Orange, UiTheme.Ink))
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

            if (UiTheme.FaceButton(copy, shareCopied ? "COPIED" : "COPY SCORE",
                    shareCopied ? UiTheme.Teal : UiTheme.Slate, UiTheme.Cream))
            {
                AudioManager.Instance?.PlaySfx(SfxId.UiTap);
                GUIUtility.systemCopyBuffer = BuildShareText();
                shareCopied = true;
            }

            _ = width;
        }

        private string BuildShareText()
        {
            var result = game.LastRun;
            var header = result.PackagesDelivered > 0 ? "Delivered" : "Wiped out";
            return $"Purrcel - {header}: {result.Score} pts, {result.DistanceMeters:0} m, " +
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
