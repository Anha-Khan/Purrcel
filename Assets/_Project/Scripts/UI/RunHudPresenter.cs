using CatCourier.Coins;
using CatCourier.Core;
using CatCourier.Packages;
using CatCourier.Scoring;
using UnityEngine;

namespace CatCourier.UI
{
    /// <summary>
    /// Run readouts. It reads public manager state and draws nothing else.
    ///
    /// Designed for a glance, not for reading. The player is auto-running at 6 m/s and
    /// gets about half a second of attention, so score is the only element given display
    /// weight, the run's supporting numbers sit beneath it at caption size, and the
    /// package block only appears once a parcel is actually carried.
    /// </summary>
    public sealed class RunHudPresenter : MonoBehaviour
    {
        [SerializeField] private ScoreManager score;
        [SerializeField] private CoinManager coins;
        [SerializeField] private PackageManager packages;

        private const float EntranceSeconds = 0.4f;
        private const float PopSeconds = 0.22f;

        /// <summary>
        /// Peak alpha of the innermost scrim band behind the score cluster.
        ///
        /// Not a guess: the six bands composite, so the effective alpha at the centre is
        /// 1 - (1-p)(1-2p/6)...(1-p) ~= 0.95 here, which is what carries cream over a
        /// fully white road. 0.62 was the first value I picked and it only reaches AA
        /// because the bands accumulate. UiContrastTests composites the real curve and
        /// asserts the result, so lowering this below the floor fails the build.
        /// </summary>
        public const float ScrimAlpha = 0.62f;

        /// <summary>
        /// How far the scrim spreads beyond the cluster, in unscaled pixels. Sized to the
        /// text block, not the screen: the fade should finish within a digit's width of
        /// the glyphs so it never reads as a panel behind the HUD.
        /// </summary>
        public const float ScrimFeather = 26f;

        // Recomputed only when the save changes. Walking runHistory twice per OnGUI
        // call rescanned the list every frame for a value that changes only when a run
        // is banked.
        private long pastHighScore;
        private int cachedRunCount = -1;

        private float entranceAt = -1f;
        private long shownScore;
        private float popAt = -1f;

        private void Awake()
        {
            score ??= FindObjectOfType<ScoreManager>();
            coins ??= FindObjectOfType<CoinManager>();
            packages ??= FindObjectOfType<PackageManager>();
            shownScore = -1;
        }

        private void Update()
        {
            // Entrance is keyed off state rather than OnEnable because the HUD presenter
            // stays enabled across the hub->run transition in additive scene loads.
            var game = GameManager.Instance;
            if (game != null && game.State == GameState.Running && entranceAt < 0f)
            {
                entranceAt = Time.unscaledTime;
            }
            else if (game != null && game.State != GameState.Running && game.State != GameState.Paused)
            {
                entranceAt = -1f;
            }
        }

        private void OnGUI()
        {
            var game = GameManager.Instance;
            if (game == null || (game.State != GameState.Running && game.State != GameState.Paused))
            {
                return;
            }

            if (entranceAt < 0f)
            {
                entranceAt = Time.unscaledTime;
            }

            var current = score != null ? score.Score : 0;
            if (shownScore < 0)
            {
                shownScore = current;
            }
            else if (current >= shownScore + 50)
            {
                // One pop per 50 points, not per point: a pop every frame reads as noise.
                shownScore = current;
                popAt = Time.unscaledTime;
            }
            else if (current < shownScore)
            {
                shownScore = current;
            }

            var entrance = UiMotion.EaseOutCubic(UiMotion.Progress(entranceAt, EntranceSeconds));
            DrawScoreCluster(entrance);
            DrawPackageBlock(entrance);
        }

        /// <summary>Score is the anchor. Everything else is subordinate to it.</summary>
        private void DrawScoreCluster(float entrance)
        {
            var safe = HubLayout.SafeRect;
            var scale = UiTheme.Scale;
            var pad = 18f * scale;

            var pop = popAt >= 0f ? UiMotion.Progress(popAt, PopSeconds) : 1f;
            var popEase = pop < 1f ? UiMotion.EaseOutBack(pop) : 1f;
            var scoreScale = Mathf.Lerp(1.16f, 1f, popEase);

            var cluster = new Rect(safe.x + pad, Screen.height - safe.yMax + pad,
                340f * scale, 132f * scale);

            // Slides in from the left and settles, rather than appearing mid-frame.
            var body = UiTheme.Offset(cluster, (1f - entrance) * -46f * scale, 0f);

            // A soft scrim behind the whole cluster, not a per-glyph drop shadow. The
            // previous version offset a second copy of the score 2px, which is a hard
            // shadow: it read as a duplicate digit over busy art, and its alpha was a
            // guess against an animated background. The scrim grows outward from the
            // text bounds so the score sits on the opaque centre and the edge fades.
            UiTheme.SoftScrim(body, ScrimFeather * scale, ScrimAlpha, entrance);

            UiTheme.Label(new Rect(body.x, body.y, body.width, 20f * scale), "SCORE",
                UiTheme.Caption, UiTheme.Muted);

            // Grow the box about its bottom-left so the pop pushes upward out of the
            // label above it instead of overlapping it.
            var displayRect = new Rect(body.x, body.y + 16f * scale, body.width, 62f * scale);
            displayRect.height *= scoreScale;
            displayRect.y = body.y + 16f * scale + 62f * scale - displayRect.height;

            UiTheme.Label(displayRect, Format(currentScore()), UiTheme.Display, UiTheme.Cream);

            var support = new Rect(body.x, body.y + 82f * scale, body.width, 24f * scale);
            var runCoins = coins != null ? coins.RunCoins : 0;
            UiTheme.Label(support, $"BEST {PastHighScore()}    COINS {runCoins}",
                UiTheme.Caption, UiTheme.Muted);
        }

        private void DrawPackageBlock(float entrance)
        {
            if (packages == null || !packages.HasAssignedPackage)
            {
                return;
            }

            var safe = HubLayout.SafeRect;
            var scale = UiTheme.Scale;
            var type = packages.CurrentPackageType;
            var urgent = type == PackageType.Urgent && packages.UrgentTimeLimit > 0f;

            var rows = urgent ? 3 : 2;
            var block = new Rect(safe.x + 18f * scale, Screen.height - safe.yMax + 172f * scale,
                300f * scale, rows * 30f * scale + 20f * scale);
            var body = UiTheme.Offset(block, 0f, (1f - entrance) * -26f * scale);
            var content = UiTheme.Panel(body, entrance, type == PackageType.Fragile ? UiTheme.Danger : UiTheme.Orange);

            var row = 30f * scale;
            var label = type == PackageType.Fragile ? "FRAGILE" : type == PackageType.Urgent ? "URGENT" : "CARRYING";
            var face = type == PackageType.Fragile ? UiTheme.Danger : UiTheme.Orange;
            UiTheme.Label(new Rect(content.x, content.y, content.width, row), $"{label}  {type}",
                UiTheme.Caption, face);

            if (urgent)
            {
                var remaining = Mathf.Max(0f, packages.UrgentTimeRemaining);
                var fraction = packages.UrgentTimeLimit > 0f ? remaining / packages.UrgentTimeLimit : 0f;

                // Colour and a pulse carry the urgency so the number is not the only signal.
                var critical = remaining <= 2f;
                var timerFace = critical ? UiTheme.Danger : UiTheme.Gold;
                var pulse = critical ? 0.55f + 0.45f * UiMotion.Pulse(9f) : 1f;
                var timer = new Rect(content.x, content.y + row, content.width, 30f * scale);
                UiTheme.Label(timer, $"DELIVER IN {remaining:0.0}s", UiTheme.Body,
                    UiTheme.Fade(timerFace, pulse));

                var track = new Rect(content.x, content.y + row * 2f, content.width, 8f * scale);
                UiTheme.Bar(track, fraction, timerFace);
            }
            else if (type == PackageType.Fragile)
            {
                UiTheme.Label(new Rect(content.x, content.y + row, content.width, row),
                    "Do not land hard", UiTheme.Caption, UiTheme.Danger);
            }
            else
            {
                UiTheme.Label(new Rect(content.x, content.y + row, content.width, row),
                    "Deliver at the next checkpoint", UiTheme.Caption, UiTheme.Muted);
            }

        }

        private long currentScore() => score != null ? score.Score : 0;

        private static string Format(long value) => value.ToString("N0");

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
