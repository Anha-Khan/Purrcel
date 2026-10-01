using System;
using System.Collections.Generic;
using UnityEngine;
using CatCourier.Core;

namespace CatCourier.Generation
{
    public interface IChunkPoolResettable
    {
        void OnChunkActivated();
        void OnChunkDeactivated();
    }

    public sealed class ChunkManager : MonoBehaviour
    {
        [Header("Generation")]
        [SerializeField] private ChunkCatalog catalog;
        [SerializeField] private ProceduralGenerator generator;
        [SerializeField] private Transform player;
        [SerializeField] private Transform chunkContainer;
        [SerializeField] private Transform fallbackContent;
        [SerializeField] private Camera gameplayCamera;
        [SerializeField] private int seed = 1;
        [SerializeField] private DistrictId initialDistrict = DistrictId.OldTown;
        [SerializeField] private int doubleJumpLevel;
        [SerializeField] private bool unlockHarbour;
        [SerializeField] private bool unlockSuburbs;
        [SerializeField] private bool startOnAwake = true;

        private readonly List<ChunkMarker> activeChunks = new();
        private readonly List<GameObject> pool = new();
        private readonly HashSet<DistrictId> explicitUnlockedDistricts = new();
        private readonly Dictionary<ChunkMarker, ProceduralChunkSelection> chunkSelections = new();
        private readonly Dictionary<GameObject, GameObject> pooledPrefabSources = new();
        private readonly Dictionary<ChunkMarker, GameObject> activePrefabSources = new();
        private bool initialized;
        private bool running;
        private bool paused;

        public DistrictId CurrentDistrict { get; private set; } = DistrictId.OldTown;
        public int ActiveChunkCount => activeChunks.Count;
        public int ActiveAheadCount { get; private set; }
        public bool IsRunning => running;
        public bool IsPaused => paused;
        public ProceduralGenerator Generator => generator;
        public ChunkCatalog Catalog => catalog;
        public event Action<DistrictId> OnDistrictChanged;
        public event Action<DistrictId, int, StoryBeat> OnCheckpointReached;

        private void Awake()
        {
            ResolveReferences();
            SubscribePauseState();
        }

        private void Start()
        {
            if (startOnAwake)
            {
                StartGeneration();
            }
        }

        private void Update()
        {
            if (!running || paused)
            {
                return;
            }

            UpdateDifficulty();
            RefreshUnlockedDistricts();
            RecycleBehindCamera();
            MaintainChunks();
        }

        public void Configure(
            ChunkCatalog chunkCatalog,
            ProceduralGenerator proceduralGenerator,
            Transform playerTransform,
            Camera gameplayCamera,
            Transform fallbackContent = null,
            bool autoStart = false)
        {
            catalog = chunkCatalog;
            generator = proceduralGenerator;
            player = playerTransform;
            this.gameplayCamera = gameplayCamera;
            this.fallbackContent = fallbackContent;
            startOnAwake = autoStart;
            initialized = false;
            ResolveReferences();
        }

        public bool StartGeneration()
        {
            ResolveReferences();
            if (generator == null || catalog == null)
            {
                Debug.LogWarning("ChunkManager needs a ProceduralGenerator and ChunkCatalog before starting generation.", this);
                return false;
            }

            if (fallbackContent != null)
            {
                fallbackContent.gameObject.SetActive(!HasCatalogContent());
            }

            if (!initialized)
            {
                generator.Initialize(catalog, seed, initialDistrict, GetEligibleDistricts(), doubleJumpLevel);
                generator.OnDistrictChanged += HandleDistrictChanged;
                initialized = true;
            }

            running = true;
            paused = false;
            RefreshUnlockedDistricts();
            CurrentDistrict = generator.CurrentDistrict;
            EnsureChunkContainer();
            MaintainChunks();
            return true;
        }

        public void StopGeneration()
        {
            running = false;
            paused = false;
        }

        public void PauseGeneration()
        {
            paused = true;
        }

        public void ResumeGeneration()
        {
            if (running)
            {
                paused = false;
            }
        }

        public bool ResetGeneration(int newSeed)
        {
            seed = newSeed;
            if (generator == null || catalog == null)
            {
                return false;
            }

            ReturnAllActiveChunks();
            generator.Initialize(catalog, seed, initialDistrict, GetEligibleDistricts(), doubleJumpLevel);
            RefreshUnlockedDistricts();
            CurrentDistrict = generator.CurrentDistrict;
            running = true;
            paused = false;
            EnsureChunkContainer();
            MaintainChunks();
            return true;
        }

        public void SetDifficulty(int difficultyLevel)
        {
            if (generator != null)
            {
                generator.SetDifficulty(difficultyLevel);
            }
        }

        public void SetDoubleJumpLevel(int level)
        {
            doubleJumpLevel = Mathf.Max(0, level);
            if (generator != null)
            {
                generator.SetDoubleJumpLevel(doubleJumpLevel);
            }
        }

        public void SetUnlockedDistricts(IEnumerable<DistrictId> districts)
        {
            explicitUnlockedDistricts.Clear();
            if (districts != null)
            {
                foreach (var district in districts)
                {
                    explicitUnlockedDistricts.Add(district);
                }
            }

            if (generator != null)
            {
                generator.SetUnlockedDistricts(GetEligibleDistricts());
                CurrentDistrict = generator.CurrentDistrict;
            }
        }

        private bool HasCatalogContent()
        {
            foreach (var district in Enum.GetValues(typeof(DistrictId)))
            {
                foreach (ChunkType type in Enum.GetValues(typeof(ChunkType)))
                {
                    if (catalog.HasVariants((DistrictId)district, (ChunkType)type))
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        private void MaintainChunks()
        {
            if (generator == null || !generator.IsInitialized || player == null || gameplayCamera == null)
            {
                return;
            }

            var attempts = 0;
            while (activeChunks.Count < Constants.ACTIVE_CHUNK_COUNT && attempts < Constants.ACTIVE_CHUNK_COUNT)
            {
                attempts++;
                if (!generator.TrySelectNext(out var selection))
                {
                    return;
                }

                // A failed spawn is logged inside Spawn. Keep filling the window so one
                // bad prefab costs a chunk rather than the whole route.
                Spawn(selection);
            }
        }

        private bool Spawn(ProceduralChunkSelection selection)
        {
            if (selection.Prefab == null || activeChunks.Count >= Constants.ACTIVE_CHUNK_COUNT)
            {
                return false;
            }

            var chunk = TakePooled(selection.Prefab);
            if (chunk == null)
            {
                chunk = Instantiate(selection.Prefab, chunkContainer);
            }
            chunk.transform.SetParent(chunkContainer, false);
            chunk.SetActive(true);

            var marker = chunk.GetComponent<ChunkMarker>();
            if (marker == null)
            {
                // Returning false here aborted MaintainChunks for the whole frame, so
                // one malformed prefab silently stalled the entire route. Pool the bad
                // instance and report, but let generation keep filling the window.
                chunk.SetActive(false);
                pooledPrefabSources[chunk] = selection.Prefab;
                pool.Add(chunk);
                Debug.LogError(
                    $"Chunk prefab '{selection.Prefab.name}' has no ChunkMarker. It was skipped; " +
                    "fix the prefab or remove it from the catalog.");
                return false;
            }

            var position = activeChunks.Count == 0
                ? new Vector3(0f, 0f, 0f)
                : activeChunks[activeChunks.Count - 1].End.position;
            chunk.transform.position = position;
            SetChunkState(chunk, true);
            activeChunks.Add(marker);
            activePrefabSources[marker] = selection.Prefab;
            chunkSelections[marker] = selection;
            var checkpoint = chunk.GetComponentInChildren<CheckpointMarker>(true);
            if (checkpoint != null)
            {
                checkpoint.Reached -= HandleCheckpointReached;
                checkpoint.Reached += HandleCheckpointReached;
            }

            ActiveAheadCount = CountAhead();

            return true;
        }

        private void RecycleBehindCamera()
        {
            if (activeChunks.Count == 0 || gameplayCamera == null)
            {
                return;
            }

            var cameraLeft = gameplayCamera.transform.position.x - gameplayCamera.orthographicSize * gameplayCamera.aspect;
            var recycleAt = cameraLeft - Constants.CHUNK_WIDTH;
            ChunkMarker rearmost = null;

            foreach (var marker in activeChunks)
            {
                if (marker == null)
                {
                    continue;
                }

                if (marker.Start.position.x < recycleAt && (rearmost == null || marker.Start.position.x < rearmost.Start.position.x))
                {
                    rearmost = marker;
                }
            }

            if (rearmost == null)
            {
                return;
            }

            activeChunks.Remove(rearmost);
            var chunk = rearmost.gameObject;
            var checkpoint = chunk.GetComponentInChildren<CheckpointMarker>(true);
            if (checkpoint != null)
            {
                checkpoint.Reached -= HandleCheckpointReached;
            }

            chunkSelections.Remove(rearmost);
            activePrefabSources.TryGetValue(rearmost, out var sourcePrefab);
            activePrefabSources.Remove(rearmost);
            SetChunkState(chunk, false);
            chunk.SetActive(false);
            chunk.transform.SetParent(chunkContainer, false);
            pooledPrefabSources[chunk] = sourcePrefab;
            pool.Add(chunk);
            ActiveAheadCount = CountAhead();
        }

        private int CountAhead()
        {
            if (player == null)
            {
                return 0;
            }

            var count = 0;
            foreach (var marker in activeChunks)
            {
                if (marker != null && marker.End.position.x > player.position.x)
                {
                    count++;
                }
            }

            return count;
        }

        /// <summary>
        /// Difficulty from run distance. Shared with <see cref="DifficultyManager"/>, which
        /// derives the same level for the score multiplier and drone speed, so the two
        /// cannot disagree.
        /// </summary>
        public static int DifficultyForDistance(float meters)
        {
            return Mathf.Clamp(
                Mathf.FloorToInt(Mathf.Max(0f, meters) / Constants.DIFFICULTY_STEP_DISTANCE), 0, 10);
        }

        private void UpdateDifficulty()
        {
            if (player == null)
            {
                return;
            }

            SetDifficulty(DifficultyForDistance(player.position.x));
        }

        private void ReturnAllActiveChunks()
        {
            foreach (var marker in activeChunks)
            {
                if (marker == null)
                {
                    continue;
                }

                var chunk = marker.gameObject;
                var checkpoint = chunk.GetComponentInChildren<CheckpointMarker>(true);
                if (checkpoint != null)
                {
                    checkpoint.Reached -= HandleCheckpointReached;
                }

                chunkSelections.Remove(marker);
                activePrefabSources.TryGetValue(marker, out var sourcePrefab);
                activePrefabSources.Remove(marker);
                SetChunkState(chunk, false);
                chunk.SetActive(false);
                chunk.transform.SetParent(chunkContainer, false);
                pooledPrefabSources[chunk] = sourcePrefab;
                pool.Add(chunk);
            }

            activeChunks.Clear();
            ActiveAheadCount = 0;
        }

        private void ResolveReferences()
        {
            if (generator == null)
            {
                generator = GetComponent<ProceduralGenerator>();
            }

            if (player == null)
            {
                var playerObject = GameObject.FindGameObjectWithTag("Player");
                if (playerObject != null)
                {
                    player = playerObject.transform;
                }
            }

            if (gameplayCamera == null)
            {
                gameplayCamera = Camera.main;
            }

            EnsureChunkContainer();
        }

        private void EnsureChunkContainer()
        {
            if (chunkContainer != null)
            {
                return;
            }

            chunkContainer = transform.Find("ChunkContainer");
            if (chunkContainer != null)
            {
                return;
            }

            var container = new GameObject("ChunkContainer");
            container.transform.SetParent(transform, false);
            chunkContainer = container.transform;
        }

        private void RefreshUnlockedDistricts()
        {
            if (player != null)
            {
                var distance = Mathf.Max(0f, player.position.x);
                unlockHarbour |= distance >= 600f;
                unlockSuburbs |= distance >= 1200f;
            }

            generator.SetUnlockedDistricts(GetEligibleDistricts());
            CurrentDistrict = generator.CurrentDistrict;
        }

        private IEnumerable<DistrictId> GetEligibleDistricts()
        {
            if (explicitUnlockedDistricts.Count > 0)
            {
                foreach (DistrictId district in Enum.GetValues(typeof(DistrictId)))
                {
                    if (explicitUnlockedDistricts.Contains(district))
                    {
                        yield return district;
                    }
                }
                yield break;
            }

            yield return DistrictId.OldTown;
            yield return DistrictId.Downtown;
            if (unlockHarbour)
            {
                yield return DistrictId.Harbour;
            }
            if (unlockSuburbs)
            {
                yield return DistrictId.Suburbs;
            }
        }

        private void HandleDistrictChanged(DistrictId district)
        {
            CurrentDistrict = district;
            OnDistrictChanged?.Invoke(district);
        }

        private void HandleCheckpointReached(CheckpointMarker checkpoint)
        {
            if (checkpoint == null)
            {
                return;
            }

            var marker = checkpoint.GetComponentInParent<ChunkMarker>();
            if (marker == null || !chunkSelections.TryGetValue(marker, out var selection))
            {
                return;
            }

            generator.ActivatePendingDistrict();
            CurrentDistrict = generator.CurrentDistrict;
            OnCheckpointReached?.Invoke(selection.District, selection.CheckpointIndex, selection.StoryBeat);
        }

        private GameObject TakePooled(GameObject prefab)
        {
            if (prefab == null)
            {
                return null;
            }

            for (var index = 0; index < pool.Count; index++)
            {
                var candidate = pool[index];
                if (candidate != null && pooledPrefabSources.TryGetValue(candidate, out var source) && source == prefab)
                {
                    pool.RemoveAt(index);
                    pooledPrefabSources.Remove(candidate);
                    return candidate;
                }
            }

            return null;
        }

        private void SetChunkState(GameObject chunk, bool active)
        {
            var behaviours = chunk.GetComponentsInChildren<MonoBehaviour>(true);
            var resetByInterface = false;
            foreach (var behaviour in behaviours)
            {
                if (!(behaviour is IChunkPoolResettable resettable))
                {
                    continue;
                }

                resetByInterface = true;
                if (active)
                {
                    resettable.OnChunkActivated();
                }
                else
                {
                    resettable.OnChunkDeactivated();
                }
            }

            if (!resetByInterface)
            {
                chunk.SendMessage(active ? "OnChunkActivated" : "OnChunkDeactivated", SendMessageOptions.DontRequireReceiver);
            }
        }

        private void SubscribePauseState()
        {
            if (GameManager.Instance != null)
            {
                GameManager.Instance.OnStateChanged += HandleGameStateChanged;
            }
        }

        private void HandleGameStateChanged(GameState state)
        {
            if (state == GameState.Paused || state == GameState.Dead)
            {
                PauseGeneration();
            }
            else if (state == GameState.Running)
            {
                ResumeGeneration();
            }
        }

        private void OnDestroy()
        {
            if (GameManager.Instance != null)
            {
                GameManager.Instance.OnStateChanged -= HandleGameStateChanged;
            }

            if (generator != null)
            {
                generator.OnDistrictChanged -= HandleDistrictChanged;
            }
        }
    }
}
