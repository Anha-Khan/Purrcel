using System;
using System.Collections.Generic;
using UnityEngine;
using CatCourier.Core;

namespace CatCourier.Generation
{
    public readonly struct ProceduralChunkSelection
    {
        public ProceduralChunkSelection(
            ChunkType type,
            DistrictId district,
            GameObject prefab,
            float startDistanceMeters,
            int checkpointIndex,
            StoryBeat storyBeat)
        {
            Type = type;
            District = district;
            Prefab = prefab;
            StartDistanceMeters = startDistanceMeters;
            CheckpointIndex = checkpointIndex;
            StoryBeat = storyBeat;
        }

        public ChunkType Type { get; }
        public DistrictId District { get; }
        public GameObject Prefab { get; }
        public float StartDistanceMeters { get; }
        public int CheckpointIndex { get; }
        public StoryBeat StoryBeat { get; }
    }

    public sealed class ProceduralGenerator : MonoBehaviour
    {
        private static readonly ChunkType[] WeightedTypes =
        {
            ChunkType.SmallGap,
            ChunkType.MediumGap,
            ChunkType.LargeGap,
            ChunkType.ObstacleDense,
            ChunkType.ObstacleSparse,
            ChunkType.HighPlatform,
            ChunkType.DropDown
        };

        private static readonly DistrictId[] DistrictOrder =
        {
            DistrictId.OldTown,
            DistrictId.Downtown,
            DistrictId.Harbour,
            DistrictId.Suburbs
        };

        private readonly HashSet<DistrictId> unlockedDistricts = new();
        private readonly Dictionary<DistrictId, int> completedCheckpoints = new();
        private readonly HashSet<string> warnedMissingCombinations = new();

        private System.Random random;
        private ChunkCatalog catalog;
        private ChunkType previousType;
        private bool hasPreviousType;
        private float nextDistanceMeters;
        private float selectedDistanceMeters;
        private int doubleJumpLevel;
        private DistrictId pendingDistrict;
        private bool hasPendingDistrict;

        public DistrictId CurrentDistrict { get; private set; } = DistrictId.OldTown;
        public int DifficultyLevel { get; private set; }
        public int DoubleJumpLevel => doubleJumpLevel;
        public float NextCheckpointDistanceMeters => nextDistanceMeters;
        public bool IsInitialized => random != null;
        public DistrictId PendingDistrict => pendingDistrict;
        public event Action<DistrictId> OnDistrictChanged;

        public void Initialize(
            ChunkCatalog chunkCatalog,
            int seed,
            DistrictId initialDistrict,
            IEnumerable<DistrictId> eligibleDistricts,
            int currentDoubleJumpLevel = 0)
        {
            catalog = chunkCatalog;
            SetUnlockedDistricts(eligibleDistricts);
            doubleJumpLevel = Mathf.Max(0, currentDoubleJumpLevel);
            Reset(seed, initialDistrict);
        }

        public void Reset(int seed, DistrictId initialDistrict)
        {
            random = new System.Random(seed);
            previousType = default;
            hasPreviousType = false;
            selectedDistanceMeters = 0f;
            nextDistanceMeters = Constants.CHECKPOINT_INTERVAL;
            completedCheckpoints.Clear();
            warnedMissingCombinations.Clear();
            hasPendingDistrict = false;

            CurrentDistrict = unlockedDistricts.Count > 0 && unlockedDistricts.Contains(initialDistrict)
                ? initialDistrict
                : FirstUnlockedDistrict();
            pendingDistrict = CurrentDistrict;
        }

        public void SetDifficulty(int level)
        {
            DifficultyLevel = Mathf.Clamp(level, 0, 10);
        }

        public void SetDoubleJumpLevel(int level)
        {
            doubleJumpLevel = Mathf.Max(0, level);
        }

        public void SetUnlockedDistricts(IEnumerable<DistrictId> districts)
        {
            unlockedDistricts.Clear();
            if (districts != null)
            {
                foreach (var district in districts)
                {
                    unlockedDistricts.Add(district);
                }
            }

            if (unlockedDistricts.Count == 0)
            {
                unlockedDistricts.Add(DistrictId.OldTown);
                unlockedDistricts.Add(DistrictId.Downtown);
            }

            if (!unlockedDistricts.Contains(CurrentDistrict))
            {
                CurrentDistrict = FirstUnlockedDistrict();
                OnDistrictChanged?.Invoke(CurrentDistrict);
            }
        }

        public void ActivatePendingDistrict()
        {
            if (!hasPendingDistrict)
            {
                return;
            }

            hasPendingDistrict = false;
            SetDistrict(pendingDistrict);
        }

        public bool TrySelectNext(out ProceduralChunkSelection selection)
        {
            selection = default;
            if (!IsInitialized || catalog == null)
            {
                return false;
            }

            var type = ChunkType.Checkpoint;
            var selectionDistrict = hasPreviousType && previousType == ChunkType.Checkpoint && hasPendingDistrict
                ? pendingDistrict
                : CurrentDistrict;

            if (hasPreviousType && previousType == ChunkType.Checkpoint)
            {
                type = ChunkType.ObstacleSparse;
            }
            else if (selectedDistanceMeters < nextDistanceMeters)
            {
                type = SelectWeightedType();
            }

            if (type == ChunkType.Checkpoint)
            {
                if (TrySelectCheckpointDistrict(out var checkpointDistrict))
                {
                    selectionDistrict = checkpointDistrict;
                    pendingDistrict = checkpointDistrict;
                    hasPendingDistrict = true;
                }
                else
                {
                    hasPendingDistrict = false;
                    WarnMissingCombination(CurrentDistrict, ChunkType.Checkpoint);
                }
            }

            if (!TrySelectVariant(selectionDistrict, type, out var prefab))
            {
                WarnMissingCombination(selectionDistrict, type);
                selectionDistrict = CurrentDistrict;
                if (!TrySelectWeightedType(selectionDistrict, out type) || !TrySelectVariant(selectionDistrict, type, out prefab))
                {
                    WarnMissingCombination(selectionDistrict, type);
                    return false;
                }
            }

            var checkpointIndex = 0;
            StoryBeat storyBeat = default;
            if (type == ChunkType.Checkpoint)
            {
                nextDistanceMeters += Constants.CHECKPOINT_INTERVAL;
                checkpointIndex = completedCheckpoints.TryGetValue(selectionDistrict, out var count)
                    ? count + 1
                    : 1;
                completedCheckpoints[selectionDistrict] = checkpointIndex;
                storyBeat = GetStoryBeatHook(selectionDistrict, checkpointIndex);
            }

            selection = new ProceduralChunkSelection(
                type,
                selectionDistrict,
                prefab,
                selectedDistanceMeters,
                checkpointIndex,
                storyBeat);

            selectedDistanceMeters += Constants.CHUNK_WIDTH;
            previousType = type;
            hasPreviousType = true;
            return true;
        }

        public static float GetChunkWeight(ChunkType type, int difficultyLevel)
        {
            var level = Mathf.Clamp(difficultyLevel, 0, 10);
            if (type == ChunkType.Checkpoint)
            {
                return 0f;
            }

            switch (type)
            {
                case ChunkType.SmallGap:
                    return Interpolate(level, 30f, 20f, 10f);
                case ChunkType.MediumGap:
                    return Interpolate(level, 25f, 25f, 20f);
                case ChunkType.LargeGap:
                    return Interpolate(level, 10f, 15f, 20f);
                case ChunkType.ObstacleDense:
                    return Interpolate(level, 10f, 15f, 20f);
                case ChunkType.ObstacleSparse:
                    return Interpolate(level, 15f, 10f, 10f);
                case ChunkType.HighPlatform:
                    return Interpolate(level, 5f, 10f, 10f);
                case ChunkType.DropDown:
                    return Interpolate(level, 5f, 5f, 10f);
                default:
                    return 0f;
            }
        }

        public float GetSelectionWeight(ChunkType type)
        {
            var weight = GetChunkWeight(type, DifficultyLevel);
            if (type == ChunkType.LargeGap && (doubleJumpLevel < 1 || (hasPreviousType && previousType == ChunkType.LargeGap)))
            {
                return 0f;
            }

            return weight;
        }

        public static StoryBeat GetStoryBeatHook(DistrictId district, int checkpointIndex)
        {
            string id;
            string text;

            switch (district)
            {
                case DistrictId.OldTown when checkpointIndex == 1:
                    id = "oldtown_checkpoint_1";
                    text = "The cat's name is Pip. She's been running deliveries since before the pigeons took over the square.";
                    break;
                case DistrictId.OldTown when checkpointIndex == 3:
                    id = "oldtown_checkpoint_3";
                    text = "Old Marko at the café always leaves a window open. Pip never stops to rest — but she glances.";
                    break;
                case DistrictId.Downtown when checkpointIndex == 1:
                    id = "downtown_checkpoint_1";
                    text = "The glass towers are cold. The drones don't like cats. Good thing the feeling is mutual.";
                    break;
                case DistrictId.Downtown when checkpointIndex == 3:
                    id = "downtown_checkpoint_3";
                    text = "Pip finds a package addressed to no one. She delivers it anyway.";
                    break;
                case DistrictId.Harbour when checkpointIndex == 1:
                    id = "harbour_checkpoint_1";
                    text = "Salt air. The seagulls remember when this was their territory. They haven't forgiven.";
                    break;
                case DistrictId.Suburbs when checkpointIndex == 1:
                    id = "suburbs_checkpoint_1";
                    text = "A dog. A very loud, very slow dog. Pip has met worse.";
                    break;
                default:
                    return default;
            }

            return new StoryBeat(id, text);
        }

        private ChunkType SelectWeightedType()
        {
            return TrySelectWeightedType(CurrentDistrict, out var type) ? type : ChunkType.Checkpoint;
        }

        private bool TrySelectWeightedType(out ChunkType selected)
        {
            return TrySelectWeightedType(CurrentDistrict, out selected);
        }

        private bool TrySelectWeightedType(DistrictId district, out ChunkType selected)
        {
            selected = default;
            EnsureDistrictHasWeightedChunk(district);

            var totalWeight = 0f;
            for (var i = 0; i < WeightedTypes.Length; i++)
            {
                var type = WeightedTypes[i];
                if (catalog.HasVariants(district, type))
                {
                    totalWeight += GetSelectionWeight(type);
                }
            }

            if (totalWeight <= 0f)
            {
                return false;
            }

            var roll = (float)random.NextDouble() * totalWeight;
            for (var i = 0; i < WeightedTypes.Length; i++)
            {
                var type = WeightedTypes[i];
                if (!catalog.HasVariants(district, type))
                {
                    continue;
                }

                roll -= GetSelectionWeight(type);
                if (roll < 0f || i == WeightedTypes.Length - 1)
                {
                    selected = type;
                    return true;
                }
            }

            selected = ChunkType.DropDown;
            return true;
        }

        private void EnsureDistrictHasWeightedChunk(DistrictId district)
        {
            for (var i = 0; i < WeightedTypes.Length; i++)
            {
                if (catalog.HasVariants(district, WeightedTypes[i]))
                {
                    return;
                }
            }

            if (district != CurrentDistrict)
            {
                return;
            }

            var alternatives = new List<DistrictId>();
            foreach (var candidate in DistrictOrder)
            {
                if (!unlockedDistricts.Contains(candidate) || candidate == CurrentDistrict)
                {
                    continue;
                }

                for (var i = 0; i < WeightedTypes.Length; i++)
                {
                    if (catalog.HasVariants(candidate, WeightedTypes[i]))
                    {
                        alternatives.Add(candidate);
                        break;
                    }
                }
            }

            if (alternatives.Count > 0)
            {
                SetDistrict(alternatives[random.Next(alternatives.Count)]);
            }
        }

        private bool TrySelectCheckpointDistrict(out DistrictId selected)
        {
            var alternatives = new List<DistrictId>();
            foreach (var district in DistrictOrder)
            {
                if (unlockedDistricts.Contains(district) && catalog.HasVariants(district, ChunkType.Checkpoint))
                {
                    alternatives.Add(district);
                }
            }

            if (alternatives.Count == 0)
            {
                selected = CurrentDistrict;
                return false;
            }

            var candidates = new List<DistrictId>();
            foreach (var district in alternatives)
            {
                if (alternatives.Count == 1 || district != CurrentDistrict)
                {
                    candidates.Add(district);
                }
            }

            selected = candidates[random.Next(candidates.Count)];
            return true;
        }

        private bool TrySelectVariant(DistrictId district, ChunkType type, out GameObject prefab)
        {
            if (catalog.TryGetVariants(district, type, out var variants) && variants.Count > 0)
            {
                prefab = variants[random.Next(variants.Count)];
                return true;
            }

            prefab = null;
            return false;
        }

        private void SetDistrict(DistrictId district)
        {
            if (CurrentDistrict == district)
            {
                return;
            }

            CurrentDistrict = district;
            OnDistrictChanged?.Invoke(CurrentDistrict);
        }

        private DistrictId FirstUnlockedDistrict()
        {
            foreach (DistrictId district in Enum.GetValues(typeof(DistrictId)))
            {
                if (unlockedDistricts.Contains(district))
                {
                    return district;
                }
            }

            return DistrictId.OldTown;
        }

        private void WarnMissingCombination(DistrictId district, ChunkType type)
        {
            var key = $"{district}/{type}";
            if (warnedMissingCombinations.Add(key))
            {
                Debug.LogWarning($"Procedural generation skipped missing chunk type {key}.", this);
            }
        }

        private static float Interpolate(int level, float d0, float d5, float d10)
        {
            if (level <= 5)
            {
                return Mathf.LerpUnclamped(d0, d5, level / 5f);
            }

            return Mathf.LerpUnclamped(d5, d10, (level - 5) / 5f);
        }
    }
}
