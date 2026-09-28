using System;
using System.Collections.Generic;
using UnityEngine;
using CatCourier.Core;

namespace CatCourier.Generation
{
    [CreateAssetMenu(fileName = "ChunkCatalog", menuName = "Cat Courier/Chunk Catalog")]
    public sealed class ChunkCatalog : ScriptableObject
    {
        [Serializable]
        public sealed class Entry
        {
            public ChunkType type;
            public DistrictId district;
            public GameObject prefab;
        }

        [SerializeField] private List<Entry> entries = new();
        private readonly Dictionary<(DistrictId District, ChunkType Type), List<GameObject>> lookup = new();
        private readonly List<string> validationMessages = new();

        public IReadOnlyList<Entry> Entries => entries;
        public IReadOnlyList<string> ValidationMessages => validationMessages;
        public bool IsValid { get; private set; }

        public void SetEntries(IEnumerable<Entry> newEntries)
        {
            entries = newEntries == null ? new List<Entry>() : new List<Entry>(newEntries);
            BuildLookup();
        }

        public void BuildLookup()
        {
            entries ??= new List<Entry>();
            lookup.Clear();
            validationMessages.Clear();
            IsValid = true;

            var seenPrefabs = new HashSet<(DistrictId District, ChunkType Type, GameObject Prefab)>();
            var duplicatePrefabs = new HashSet<(DistrictId District, ChunkType Type, GameObject Prefab)>();

            foreach (var entry in entries)
            {
                if (entry == null)
                {
                    AddValidationError("Chunk catalog contains a null entry.");
                    continue;
                }

                var key = (entry.district, entry.type, entry.prefab);
                if (entry.prefab != null && !seenPrefabs.Add(key))
                {
                    duplicatePrefabs.Add(key);
                    AddValidationError($"Chunk catalog contains duplicate prefab for {entry.district}/{entry.type}.");
                }
            }

            foreach (var entry in entries)
            {
                if (entry == null)
                {
                    continue;
                }

                var key = (entry.district, entry.type);
                if (entry.prefab != null && duplicatePrefabs.Contains((entry.district, entry.type, entry.prefab)))
                {
                    continue;
                }

                if (!ValidateEntry(entry))
                {
                    IsValid = false;
                    continue;
                }

                if (!lookup.TryGetValue(key, out var variants))
                {
                    variants = new List<GameObject>();
                    lookup.Add(key, variants);
                }

                variants.Add(entry.prefab);
            }

            if (entries.Count > 0)
            {
                foreach (DistrictId district in Enum.GetValues(typeof(DistrictId)))
                {
                    foreach (ChunkType type in Enum.GetValues(typeof(ChunkType)))
                    {
                        if (!lookup.ContainsKey((district, type)))
                        {
                            IsValid = false;
                            Debug.LogWarning($"Chunk catalog has no valid entry for {district}/{type}.", this);
                        }
                    }
                }
            }

        }

        public bool TryGet(DistrictId district, ChunkType type, out GameObject prefab)
        {
            if (TryGetVariants(district, type, out var variants) && variants.Count > 0)
            {
                prefab = variants[0];
                return true;
            }

            prefab = null;
            return false;
        }

        public bool TryGetVariants(DistrictId district, ChunkType type, out IReadOnlyList<GameObject> variants)
        {
            EnsureLookup();
            if (lookup.TryGetValue((district, type), out var list))
            {
                variants = list;
                return true;
            }

            variants = Array.Empty<GameObject>();
            return false;
        }

        public int GetVariantCount(DistrictId district, ChunkType type)
        {
            return TryGetVariants(district, type, out var variants) ? variants.Count : 0;
        }

        public bool HasVariants(DistrictId district, ChunkType type)
        {
            return GetVariantCount(district, type) > 0;
        }

        private bool ValidateEntry(Entry entry)
        {
            if (entry.prefab == null)
            {
                AddValidationError($"Chunk prefab is missing for {entry.district}/{entry.type}.");
                return false;
            }

            var marker = entry.prefab.GetComponent<ChunkMarker>();
            if (marker == null)
            {
                AddValidationError($"Chunk root has no ChunkMarker for {entry.district}/{entry.type}.");
                return false;
            }

            if (marker.Start == null || marker.End == null)
            {
                AddValidationError($"Chunk Start or End marker is missing for {entry.district}/{entry.type}.");
                return false;
            }

            if (!Mathf.Approximately(marker.Start.localPosition.x, 0f) ||
                !Mathf.Approximately(marker.End.localPosition.x, Constants.CHUNK_WIDTH))
            {
                AddValidationError($"Chunk markers must use X 0 and X {Constants.CHUNK_WIDTH} for {entry.district}/{entry.type}.");
                return false;
            }

            if (entry.type == ChunkType.Checkpoint)
            {
                if (entry.prefab.GetComponentInChildren<CheckpointMarker>(true) == null)
                {
                    AddValidationError($"Checkpoint chunk has no CheckpointMarker for {entry.district}/{entry.type}.");
                    return false;
                }

                var checkpointTrigger = entry.prefab.GetComponentInChildren<Collider2D>(true);
                if (checkpointTrigger == null || !checkpointTrigger.isTrigger)
                {
                    AddValidationError($"Checkpoint chunk has no trigger collider for {entry.district}/{entry.type}.");
                    return false;
                }
            }

            return true;
        }

        private void AddValidationError(string message)
        {
            IsValid = false;
            validationMessages.Add(message);
            Debug.LogWarning(message, this);
        }

        private void EnsureLookup()
        {
            if (lookup.Count == 0)
            {
                BuildLookup();
            }
        }

        private void OnValidate()
        {
            entries ??= new List<Entry>();
            BuildLookup();
        }
    }
}
