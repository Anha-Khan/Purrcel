using System.Collections;
using CatCourier.Core;
using CatCourier.Player;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace CatCourier.Tests.PlayMode
{
    /// <summary>
    /// A catalog with nothing in it is a supported shipping configuration (content
    /// can be stripped per platform), not a start-up failure. Generation has to stay
    /// alive, expose the authored fallback, and leave the player playable.
    /// </summary>
    public sealed class MissingCatalogFallbackTests : Day6PlayModeTestBase
    {
        [UnityTest]
        public IEnumerator MissingCatalog_KeepsFallbackVisibleAndTheRunAlive()
        {
            var catalog = CreateEmptyCatalog();
            var camera = CreateCamera();
            var player = CreatePlayer("Day6FallbackPlayer");
            var fallback = Track(new GameObject("Day6FallbackContent"));
            fallback.SetActive(false);
            var manager = CreateChunkManager(catalog, player.transform, camera, fallback.transform, out var generator);

            Assert.That(
                manager.StartGeneration(),
                Is.True,
                "An empty catalog must not be reported as a start-up failure.");
            Assert.That(fallback.activeSelf, Is.True, "With no catalog content the authored fallback must be shown.");
            Assert.That(manager.IsRunning, Is.True);
            Assert.That(manager.IsPaused, Is.False);
            Assert.That(generator.IsInitialized, Is.True, "The generator must still initialize so the run can start later.");
            Assert.That(manager.ActiveChunkCount, Is.Zero, "There is nothing valid to instantiate.");

            for (var step = 0; step < 60; step++)
            {
                yield return null;
                Assert.That(manager.IsRunning, Is.True, $"Frame {step}: generation must survive a failed selection.");
                Assert.That(manager.ActiveChunkCount, Is.Zero, $"Frame {step}: no invalid chunk may be spawned.");
            }

            Assert.That(fallback.activeSelf, Is.True, "The fallback must survive a full second of failed selection.");
            Assert.That(
                player.State,
                Is.EqualTo(PlayerState.Running),
                "The player must stay playable with no chunks available.");
            Assert.That(manager.Generator.CurrentDistrict, Is.EqualTo(DistrictId.OldTown));
        }

        [UnityTest]
        public IEnumerator MissingCatalog_ResumeAndRestartStillDoNotThrow()
        {
            var catalog = CreateEmptyCatalog();
            var camera = CreateCamera();
            var player = CreatePlayer("Day6FallbackRestartPlayer");
            var manager = CreateChunkManager(catalog, player.transform, camera, null, out _);

            Assert.That(manager.StartGeneration(), Is.True);
            manager.PauseGeneration();
            Assert.That(manager.IsPaused, Is.True);
            manager.ResumeGeneration();
            Assert.That(manager.IsPaused, Is.False);

            for (var step = 0; step < 10; step++)
            {
                yield return null;
            }

            manager.StopGeneration();
            Assert.That(manager.IsRunning, Is.False, "StopGeneration must halt streaming.");
            Assert.That(manager.ActiveChunkCount, Is.Zero);

            Assert.That(manager.StartGeneration(), Is.True, "A stopped run must be restartable with an empty catalog.");
            Assert.That(manager.IsRunning, Is.True);

            for (var step = 0; step < 10; step++)
            {
                yield return null;
            }

            Assert.That(manager.IsRunning, Is.True);
        }

        [UnityTest]
        public IEnumerator PopulatedCatalog_HidesTheFallbackAndStreamsTheActiveWindow()
        {
            var sources = CreateChunkPrefabSources();
            var catalog = CreateSinglePrefabCatalog(sources, DistrictId.OldTown);
            var camera = CreateCamera();
            var player = CreatePlayer("Day6CatalogPlayer");
            var fallback = Track(new GameObject("Day6FallbackContent"));
            fallback.SetActive(false);
            var manager = CreateChunkManager(catalog, player.transform, camera, fallback.transform, out _);

            Assert.That(manager.StartGeneration(), Is.True);
            Assert.That(fallback.activeSelf, Is.False, "A populated catalog must hide the fallback.");
            Assert.That(
                manager.ActiveChunkCount,
                Is.EqualTo(Constants.ACTIVE_CHUNK_COUNT),
                "Generation must fill the active chunk window.");

            for (var step = 0; step < 20; step++)
            {
                camera.transform.position = new Vector3(8f * (step + 1), 0f, 0f);
                yield return null;
            }

            Assert.That(
                manager.ActiveChunkCount,
                Is.EqualTo(Constants.ACTIVE_CHUNK_COUNT),
                "Recycling must refill the window without dropping below it.");
            Assert.That(fallback.activeSelf, Is.False);
        }
    }
}
