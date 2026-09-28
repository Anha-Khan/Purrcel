using System;
using CatCourier.Coins;
using CatCourier.Generation;
using CatCourier.Packages;
using CatCourier.Player;
using CatCourier.Scoring;
using CatCourier.Weather;
using UnityEngine;

namespace CatCourier.Core
{
    public sealed class RunCoordinator : MonoBehaviour
    {
        public static RunCoordinator Active { get; private set; }
        [SerializeField] private PlayerController player;
        [SerializeField] private ChunkManager chunkManager;
        [SerializeField] private ProceduralGenerator generator;
        [SerializeField] private ChunkCatalog catalog;
        [SerializeField] private WeatherManager weather;
        [SerializeField] private PackageManager packages;
        [SerializeField] private ScoreManager score;
        [SerializeField] private CoinManager coins;

        private RunLoadout loadout;
        private bool started;

        public DistrictId CurrentDistrict => chunkManager?.CurrentDistrict ?? DistrictId.OldTown;
        public RunLoadout Loadout => loadout;
        public event Action<StoryBeat> OnStoryBeat;

        private void Awake()
        {
            if (Active != null && Active != this)
            {
                Destroy(gameObject);
                return;
            }

            Active = this;
            ResolveReferences();
        }

        private void Start()
        {
            StartRun();
        }

        public void StartRun()
        {
            if (started)
            {
                return;
            }

            started = true;
            ResolveReferences();
            loadout = RunLoadoutService.Build();
            weather?.StartRun();
            packages?.StartRun(loadout?.IsPremium == true, Constants.CHECKPOINT_INTERVAL, PackageManager.DefaultSlotCount);
            packages?.SetTargetSlotCount(loadout?.PackageSlots ?? 1);
            player?.ConfigureRunStats(loadout?.Stats);
            player?.ConfigureRunModifiers(weather, packages);
            player?.ConfigureWorldDeathY(GetWorldDeathY());
            player?.ResetRun();
            if (player != null)
            {
                player.ResultFactory = BuildResult;
            }

            ConfigureChunkManager();
            UpdateDistrictUnlocks();
        }

        private void Update()
        {
            if (started)
            {
                score?.Simulate(Time.deltaTime);
                UpdateDistrictUnlocks();
            }
        }

        private void ResolveReferences()
        {
            player ??= FindObjectOfType<PlayerController>();
            chunkManager ??= FindObjectOfType<ChunkManager>();
            generator ??= FindObjectOfType<ProceduralGenerator>();
            weather ??= FindObjectOfType<WeatherManager>();
            packages ??= FindObjectOfType<PackageManager>();
            score ??= player != null ? player.GetComponent<ScoreManager>() : FindObjectOfType<ScoreManager>();
            coins ??= player != null ? player.GetComponent<CoinManager>() : FindObjectOfType<CoinManager>();
        }

        private void ConfigureChunkManager()
        {
            if (chunkManager == null)
            {
                return;
            }

            chunkManager.Configure(
                catalog,
                generator,
                player != null ? player.transform : null,
                Camera.main,
                chunkManager.transform.Find("Day2FallbackContent"),
                false);
            chunkManager.SetDoubleJumpLevel(loadout?.Stats?.DoubleJumpUnlocked == true ? 1 : 0);
            chunkManager.SetUnlockedDistricts(DistrictUnlockService.GetEligible(player != null ? player.DistanceMeters : 0f, loadout?.IsPremium == true));
            chunkManager.OnCheckpointReached -= HandleCheckpointReached;
            chunkManager.OnCheckpointReached += HandleCheckpointReached;
            chunkManager.OnDistrictChanged -= HandleDistrictChanged;
            chunkManager.OnDistrictChanged += HandleDistrictChanged;
            if (packages != null)
            {
                packages.OnDeliveryScorePulseRequested -= HandleDeliveryScorePulse;
                packages.OnDeliveryScorePulseRequested += HandleDeliveryScorePulse;
            }

            chunkManager.StartGeneration();
        }

        private void HandleCheckpointReached(DistrictId district, int checkpointIndex, StoryBeat storyBeat)
        {
            var delivery = packages?.DeliverAtCheckpoint() ?? default;
            if (delivery.PackagesDelivered > 0)
            {
                coins?.AwardRunCoins(delivery.CoinsAwarded);
                for (var index = 0; index < delivery.PackagesDelivered; index++)
                {
                    score?.AddPackage();
                }
            }

            if (packages != null && loadout != null && loadout.PackageSlots > 1)
            {
                packages.ReplenishForNextLeg(loadout.IsPremium, Constants.CHECKPOINT_INTERVAL);
                player?.ConfigureRunModifiers(weather, packages);
            }

            var firstTimeStory = StoryBeatService.GetFirstTimeBeat(district, checkpointIndex);
            if (firstTimeStory.HasStory)
            {
                OnStoryBeat?.Invoke(firstTimeStory);
            }

            UpdateDistrictUnlocks();
        }

        private void HandleDeliveryScorePulse(float multiplier, float duration)
        {
            score?.SetDeliveryPulse(multiplier, duration);
        }

        private void HandleDistrictChanged(DistrictId district)
        {
        }

        private void UpdateDistrictUnlocks()
        {
            var distance = player != null ? player.DistanceMeters : 0f;
            if (DistrictUnlockService.UnlockReachedDistricts(distance) && chunkManager != null)
            {
                chunkManager.SetUnlockedDistricts(DistrictUnlockService.GetEligible(distance, loadout?.IsPremium == true));
            }
        }

        private RunResult BuildResult(long scoreValue, int coinsCollected)
        {
            return new RunResult(
                scoreValue,
                player != null ? player.DistanceMeters : 0f,
                score?.PackagesDelivered ?? 0,
                coinsCollected,
                CurrentDistrict);
        }

        private static float GetWorldDeathY()
        {
            var camera = Camera.main;
            return camera != null ? camera.ViewportToWorldPoint(Vector3.zero).y : -16f;
        }

        public void RespawnFromContinue()
        {
            player?.RespawnFromContinue();
        }

        private void OnDestroy()
        {
            if (Active == this)
            {
                Active = null;
            }

            if (chunkManager != null)
            {
                chunkManager.OnCheckpointReached -= HandleCheckpointReached;
                chunkManager.OnDistrictChanged -= HandleDistrictChanged;
            }

            if (packages != null)
            {
                packages.OnDeliveryScorePulseRequested -= HandleDeliveryScorePulse;
            }
        }
    }
}
