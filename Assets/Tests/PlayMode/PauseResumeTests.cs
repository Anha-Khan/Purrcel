using System.Collections;
using CatCourier.Core;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace CatCourier.Tests.PlayMode
{
    /// <summary>
    /// Pause must stop the whole gameplay tick, not just the player's input: the
    /// player's distance and the chunk stream both have to hold still while paused
    /// and both have to move again after a resume.
    /// </summary>
    public sealed class PauseResumeTests : Day6PlayModeTestBase
    {
        [UnityTest]
        public IEnumerator Pause_FreezesPlayerAndChunkGeneration()
        {
            CreateTempSaveSystem();

            // GameManager first: ChunkManager.Awake only subscribes to
            // GameManager.OnStateChanged when the singleton already exists.
            var game = CreateGameManager(out _);
            var player = CreatePlayer("Day6PausePlayer");
            var camera = CreateCamera();
            var sources = CreateChunkPrefabSources();
            var catalog = CreateSinglePrefabCatalog(sources, DistrictId.OldTown);
            var manager = CreateChunkManager(catalog, player.transform, camera, null, out _);

            Assert.That(manager.StartGeneration(), Is.True, "Generation must start.");
            Assert.That(manager.ActiveChunkCount, Is.EqualTo(Constants.ACTIVE_CHUNK_COUNT));

            game.StartRun();
            Assert.That(game.State, Is.EqualTo(GameState.Running), "StartRun must reach Running.");
            Assert.That(Time.timeScale, Is.EqualTo(1f));

            for (var step = 0; step < 8; step++)
            {
                yield return new WaitForFixedUpdate();
            }

            var runningDistance = player.DistanceMeters;
            Assert.That(runningDistance, Is.GreaterThan(0f), "The player must actually be moving before pausing.");
            var runningChunks = SnapshotActiveChunks(manager);
            Assert.That(runningChunks.Count, Is.EqualTo(Constants.ACTIVE_CHUNK_COUNT));

            game.PauseRun();
            Assert.That(game.State, Is.EqualTo(GameState.Paused));
            Assert.That(Time.timeScale, Is.Zero, "Pause must zero the time scale.");
            Assert.That(manager.IsPaused, Is.True, "ChunkManager must observe the pause through OnStateChanged.");

            var pausedDistance = player.DistanceMeters;
            var pausedChunks = SnapshotActiveChunks(manager);

            for (var step = 0; step < 40; step++)
            {
                // Drive the camera far past the chunk window: while paused nothing
                // may recycle, so any movement here would be a real defect.
                camera.transform.position = new Vector3(12f * (step + 1), 0f, 0f);
                yield return null;
            }

            Assert.That(
                player.DistanceMeters,
                Is.EqualTo(pausedDistance).Within(0.0001f),
                "Paused gameplay must not advance the player.");
            Assert.That(
                SnapshotActiveChunks(manager),
                Is.EqualTo(pausedChunks),
                "Paused generation must neither recycle nor spawn chunks.");

            game.ResumeRun();
            Assert.That(game.State, Is.EqualTo(GameState.Running));
            Assert.That(Time.timeScale, Is.EqualTo(1f), "Resume must restore the time scale.");
            Assert.That(manager.IsPaused, Is.False, "Resume must clear the generation pause.");

            for (var step = 0; step < 8; step++)
            {
                yield return new WaitForFixedUpdate();
            }

            Assert.That(
                player.DistanceMeters,
                Is.GreaterThan(pausedDistance),
                "Resumed gameplay must move the player again.");
            Assert.That(
                SnapshotActiveChunks(manager),
                Is.Not.EqualTo(pausedChunks),
                "Resumed generation must keep streaming chunks.");
        }

        [UnityTest]
        public IEnumerator Pause_IsIdempotentAndCannotUnpauseItself()
        {
            CreateTempSaveSystem();
            var game = CreateGameManager(out _);

            game.StartRun();
            Assert.That(game.State, Is.EqualTo(GameState.Running));

            game.PauseRun();
            game.PauseRun();
            Assert.That(game.State, Is.EqualTo(GameState.Paused), "A second pause must not move the state on.");
            Assert.That(Time.timeScale, Is.Zero);

            game.ResumeRun();
            game.ResumeRun();
            Assert.That(game.State, Is.EqualTo(GameState.Running), "A second resume must not move the state on.");
            Assert.That(Time.timeScale, Is.EqualTo(1f));

            yield return null;
        }

        [UnityTest]
        public IEnumerator Death_AlsoPausesGenerationSoChunksStopStreaming()
        {
            CreateTempSaveSystem();
            var game = CreateGameManager(out _);
            var player = CreatePlayer("Day6DeathPausePlayer");
            var camera = CreateCamera();
            var sources = CreateChunkPrefabSources();
            var catalog = CreateSinglePrefabCatalog(sources, DistrictId.OldTown);
            var manager = CreateChunkManager(catalog, player.transform, camera, null, out _);

            Assert.That(manager.StartGeneration(), Is.True);
            game.StartRun();
            Assert.That(game.State, Is.EqualTo(GameState.Running), "The run must actually be under way.");
            Assert.That(manager.IsPaused, Is.False);

            // The continue offer window is 5s of unscaled time, so the run stays
            // alive long enough to assert the death pause.
            game.ResetContinuesForRun(false);
            player.TriggerDeath();
            Assert.That(player.State, Is.EqualTo(PlayerState.Dead));

            var deadline = Time.unscaledTime + 5f;
            while (game.State != GameState.Dead && Time.unscaledTime < deadline)
            {
                yield return null;
            }

            Assert.That(game.State, Is.EqualTo(GameState.Dead));
            Assert.That(manager.IsPaused, Is.True, "Entering the Dead state must pause generation.");
            Assert.That(Time.timeScale, Is.EqualTo(1f), "The continue timer runs unscaled, so timeScale must be back to 1.");

            var deadChunks = SnapshotActiveChunks(manager);
            for (var step = 0; step < 20; step++)
            {
                camera.transform.position = new Vector3(20f * (step + 1), 0f, 0f);
                yield return null;
            }

            Assert.That(
                SnapshotActiveChunks(manager),
                Is.EqualTo(deadChunks),
                "Chunks must not stream while the player is dead.");
        }
    }
}
