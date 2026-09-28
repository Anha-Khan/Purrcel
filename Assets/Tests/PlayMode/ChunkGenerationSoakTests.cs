using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using CatCourier.Core;
using CatCourier.Generation;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace CatCourier.Tests.PlayMode
{
    /// <summary>
    /// Determinism and pool behaviour, both built on in-memory chunk prefabs so the
    /// suite never depends on authored prefab assets or on the project's real
    /// ChunkCatalog.
    ///
    /// The soak registers a single prefab under every chunk type. Because
    /// ChunkManager pools per source prefab, a recycled chunk is always immediately
    /// reusable, so "no new instantiation happened" becomes an exact assertion rather
    /// than a statistical one.
    /// </summary>
    public sealed class ChunkGenerationSoakTests : Day6PlayModeTestBase
    {
        private const int SelectionCount = 40;
        private const int SoakFrames = 90;

        [UnityTest]
        public IEnumerator SameSeed_ReplaysTheIdenticalChunkSequenceAcrossGeneratorsAndResets()
        {
            var sources = CreateChunkPrefabSources();
            var catalog = CreateCatalog(sources, DistrictId.OldTown, variantsPerType: 2, withCheckpoint: true);
            var districts = new[] { DistrictId.OldTown };

            var first = Track(new GameObject("Day6GeneratorFirst")).AddComponent<ProceduralGenerator>();
            var second = Track(new GameObject("Day6GeneratorSecond")).AddComponent<ProceduralGenerator>();
            first.Initialize(catalog, 4242, DistrictId.OldTown, districts, 1);
            second.Initialize(catalog, 4242, DistrictId.OldTown, districts, 1);

            var expected = CollectSequence(first, SelectionCount);
            Assert.That(
                expected,
                Is.EqualTo(CollectSequence(second, SelectionCount)),
                "Two generators on the same seed must replay the same run exactly.");

            first.Reset(4242, DistrictId.OldTown);
            Assert.That(
                CollectSequence(first, SelectionCount),
                Is.EqualTo(expected),
                "Resetting with the same seed must replay the same run again.");

            Assert.That(
                expected.Count(entry => entry.StartsWith("Checkpoint|", StringComparison.Ordinal)),
                Is.GreaterThan(0),
                "A 40-chunk run crosses the 300m checkpoint interval at least once.");
            Assert.That(
                expected.Distinct().Count(),
                Is.GreaterThan(1),
                "Weighted selection must actually vary the run.");

            yield return null;
        }

        [UnityTest]
        public IEnumerator PoolSoak_ReusesInstancesAndNeverGrowsPastTheActiveWindow()
        {
            var sources = CreateChunkPrefabSources();
            var catalog = CreateSinglePrefabCatalog(sources, DistrictId.OldTown);
            var camera = CreateCamera();
            var player = CreatePlayer("Day6SoakPlayer");
            var manager = CreateChunkManager(catalog, player.transform, camera, null, out _);

            Assert.That(manager.StartGeneration(), Is.True);
            Assert.That(
                manager.ActiveChunkCount,
                Is.EqualTo(Constants.ACTIVE_CHUNK_COUNT),
                "The active window must be full before the soak starts.");
            Assert.That(manager.transform.Find("ChunkContainer"), Is.Not.Null, "ChunkManager must own a ChunkContainer.");

            var baseline = SnapshotAllChunkInstanceIds(manager);
            Assert.That(baseline.Count, Is.EqualTo(Constants.ACTIVE_CHUNK_COUNT));

            for (var frame = 0; frame < SoakFrames; frame++)
            {
                // Walk the camera far ahead of the stream so exactly one chunk falls
                // behind per frame and must be recycled rather than re-instantiated.
                camera.transform.position = new Vector3(25f * (frame + 1), 0f, 0f);
                yield return null;

                Assert.That(
                    manager.ActiveChunkCount,
                    Is.LessThanOrEqualTo(Constants.ACTIVE_CHUNK_COUNT),
                    $"Frame {frame}: the active window overflowed.");
                Assert.That(
                    SnapshotAllChunkInstanceIds(manager).Count,
                    Is.LessThanOrEqualTo(Constants.ACTIVE_CHUNK_COUNT),
                    $"Frame {frame}: the pool grew instead of reusing a recycled chunk.");
            }

            Assert.That(
                manager.ActiveChunkCount,
                Is.EqualTo(Constants.ACTIVE_CHUNK_COUNT),
                "The window must still be full after the soak.");
            Assert.That(
                SnapshotAllChunkInstanceIds(manager),
                Is.EqualTo(baseline),
                $"{SoakFrames} recycles must reuse the pooled instances instead of instantiating new chunks.");
        }

        [UnityTest]
        public IEnumerator PoolSoak_StaysDeterministicWhileStreaming()
        {
            var sources = CreateChunkPrefabSources();
            var catalog = CreateSinglePrefabCatalog(sources, DistrictId.OldTown);
            var camera = CreateCamera();
            var player = CreatePlayer("Day6SoakDeterminismPlayer");

            var managerA = CreateChunkManager(catalog, player.transform, camera, null, out _);
            Assert.That(managerA.StartGeneration(), Is.True);
            var firstWindow = SnapshotActiveChunks(managerA);

            for (var frame = 0; frame < 20; frame++)
            {
                camera.transform.position = new Vector3(25f * (frame + 1), 0f, 0f);
                yield return null;
            }

            var streamedWindow = SnapshotActiveChunks(managerA);
            Assert.That(
                streamedWindow,
                Is.Not.EqualTo(firstWindow),
                "Streaming must have advanced the window.");

            // Rewind the same seed and confirm the same layout comes back.
            var managerB = CreateChunkManager(catalog, player.transform, camera, null, out _);
            camera.transform.position = Vector3.zero;
            Assert.That(managerB.StartGeneration(), Is.True);
            Assert.That(
                SnapshotActiveChunks(managerB).Select(entry => entry.Split('@')[1]),
                Is.EqualTo(firstWindow.Select(entry => entry.Split('@')[1])),
                "The same catalog and seed must lay out the opening window identically.");

            for (var frame = 0; frame < 20; frame++)
            {
                camera.transform.position = new Vector3(25f * (frame + 1), 0f, 0f);
                yield return null;
            }

            Assert.That(
                SnapshotActiveChunks(managerB).Select(entry => entry.Split('@')[1]),
                Is.EqualTo(streamedWindow.Select(entry => entry.Split('@')[1])),
                "Streaming must replay identically from the same seed.");
        }

        private static List<string> CollectSequence(ProceduralGenerator generator, int count)
        {
            var sequence = new List<string>(count);
            for (var index = 0; index < count; index++)
            {
                Assert.That(
                    generator.TrySelectNext(out var selection),
                    Is.True,
                    $"Selection {index} failed against a fully populated catalog.");
                sequence.Add(
                    $"{selection.Type}|{selection.District}|{selection.Prefab.name}|" +
                    $"{selection.CheckpointIndex}|{selection.StoryBeat.Id ?? "-"}");
            }

            return sequence;
        }
    }
}
