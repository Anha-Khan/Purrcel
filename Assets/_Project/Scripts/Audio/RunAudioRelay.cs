using CatCourier.Coins;
using CatCourier.Core;
using CatCourier.Generation;
using CatCourier.Packages;
using CatCourier.Player;
using CatCourier.Scoring;
using UnityEngine;

namespace CatCourier.Audio
{
    /// <summary>
    /// Forwards public gameplay events to <see cref="AudioManager"/>. It owns no gameplay state
    /// and changes no gameplay value; it only reads events and requests audio.
    /// </summary>
    public sealed class RunAudioRelay : MonoBehaviour
    {
        [SerializeField] private PlayerController player;
        [SerializeField] private CoinManager coins;
        [SerializeField] private ScoreManager score;
        [SerializeField] private PackageManager packages;
        [SerializeField] private ChunkManager chunks;
        [SerializeField] private RunCoordinator coordinator;

        private GameManager gameManager;
        private bool subscribed;

        private void Awake()
        {
            ResolveReferences();
        }

        private void OnEnable()
        {
            ResolveReferences();
            Subscribe();
            SyncMusicWithState();
        }

        private void OnDisable()
        {
            Unsubscribe();
        }

        public void ResolveReferences()
        {
            player ??= FindObjectOfType<PlayerController>();
            coins ??= player != null ? player.GetComponent<CoinManager>() : FindObjectOfType<CoinManager>();
            score ??= player != null ? player.GetComponent<ScoreManager>() : FindObjectOfType<ScoreManager>();
            packages ??= FindObjectOfType<PackageManager>();
            chunks ??= FindObjectOfType<ChunkManager>();
            coordinator ??= FindObjectOfType<RunCoordinator>();
        }

        private void Subscribe()
        {
            if (subscribed)
            {
                return;
            }

            if (player != null)
            {
                player.OnJump += HandleJump;
                player.OnDoubleJump += HandleDoubleJump;
                player.OnLand += HandleLand;
                player.OnSlide += HandleSlide;
                player.OnWallBounce += HandleBounce;
                player.OnDeath += HandleDeath;
            }

            if (coins != null)
            {
                coins.OnCoinsCollected += HandleCoinCollected;
            }

            if (score != null)
            {
                score.OnComboMilestone += HandleComboMilestone;
            }

            if (packages != null)
            {
                packages.OnPackageAssigned += HandlePackageAssigned;
                packages.OnPackageDelivered += HandlePackageDelivered;
            }

            if (chunks != null)
            {
                chunks.OnDistrictChanged += HandleDistrictChanged;
            }

            if (gameManager == null)
            {
                gameManager = GameManager.Instance;
            }

            if (gameManager != null)
            {
                gameManager.OnStateChanged += HandleGameStateChanged;
            }

            subscribed = player != null
                || coins != null
                || score != null
                || packages != null
                || chunks != null
                || gameManager != null;
        }

        private void Unsubscribe()
        {
            if (player != null)
            {
                player.OnJump -= HandleJump;
                player.OnDoubleJump -= HandleDoubleJump;
                player.OnLand -= HandleLand;
                player.OnSlide -= HandleSlide;
                player.OnWallBounce -= HandleBounce;
                player.OnDeath -= HandleDeath;
            }

            if (coins != null)
            {
                coins.OnCoinsCollected -= HandleCoinCollected;
            }

            if (score != null)
            {
                score.OnComboMilestone -= HandleComboMilestone;
            }

            if (packages != null)
            {
                packages.OnPackageAssigned -= HandlePackageAssigned;
                packages.OnPackageDelivered -= HandlePackageDelivered;
            }

            if (chunks != null)
            {
                chunks.OnDistrictChanged -= HandleDistrictChanged;
            }

            if (gameManager != null)
            {
                gameManager.OnStateChanged -= HandleGameStateChanged;
            }

            subscribed = false;
        }

        private void SyncMusicWithState()
        {
            var state = gameManager != null ? gameManager.State : GameState.Hub;
            if (state == GameState.Hub)
            {
                AudioManager.Instance?.PlayMusic(MusicId.Hub);
                return;
            }

            AudioManager.Instance?.PlayMusic(AudioIdMap.MusicForDistrict(CurrentDistrict()));
        }

        private DistrictId CurrentDistrict()
        {
            return coordinator != null ? coordinator.CurrentDistrict : DistrictId.OldTown;
        }

        private void HandleGameStateChanged(GameState state)
        {
            if (state == GameState.Hub)
            {
                AudioManager.Instance?.PlayMusic(MusicId.Hub);
            }
        }

        private void HandleDistrictChanged(DistrictId district)
        {
            if (gameManager != null && gameManager.State != GameState.Running)
            {
                return;
            }

            AudioManager.Instance?.PlayMusic(AudioIdMap.MusicForDistrict(district));
        }

        private void HandleJump() => AudioManager.Instance?.PlaySfx(SfxId.Jump);

        private void HandleDoubleJump() => AudioManager.Instance?.PlaySfx(SfxId.DoubleJump);

        // The event carries an impact magnitude; play softer the softer the landing so
        // a small hop does not sound like a slam. The per-call volume scale handles
        // that, independent of the Sfx mixer bus.
        private void HandleLand(float impact)
        {
            var audio = AudioManager.Instance;
            if (audio == null) return;
            audio.PlaySfx(SfxId.Land, Mathf.Clamp01(Mathf.Abs(impact) / 20f) * 0.5f + 0.5f);
        }

        private void HandleSlide() => AudioManager.Instance?.PlaySfx(SfxId.Slide);

        private void HandleBounce() => AudioManager.Instance?.PlaySfx(SfxId.Bounce);

        private void HandleDeath() => AudioManager.Instance?.PlaySfx(SfxId.Death);

        private void HandleCoinCollected(int rawValue, int awarded, Vector3 worldPosition) => AudioManager.Instance?.PlaySfx(SfxId.Coin);

        private void HandleComboMilestone(int count) => AudioManager.Instance?.PlaySfx(SfxId.ComboUp);

        private void HandlePackageAssigned(PackageType type) => AudioManager.Instance?.PlaySfx(SfxId.Package);

        private void HandlePackageDelivered() => AudioManager.Instance?.PlaySfx(SfxId.Deliver);
    }
}
