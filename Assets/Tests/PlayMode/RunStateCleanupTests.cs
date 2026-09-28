using System.Collections;
using System.IO;
using CatCourier.Core;
using CatCourier.Monetization;
using CatCourier.Player;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace CatCourier.Tests.PlayMode
{
    /// <summary>
    /// Repeated die-and-return cycles must not accumulate state: the time scale
    /// resets, the continue balance re-arms per run, the player comes back clean,
    /// each finalized run lands in the save exactly once, and the after-run-3
    /// paywall fires exactly once across the whole loop.
    /// </summary>
    public sealed class RunStateCleanupTests : Day6PlayModeTestBase
    {
        private const int Cycles = 4;

        [UnityTest]
        public IEnumerator RepeatedDeathAndReturn_LeavesCleanStateAndRecordsEachRunOnce()
        {
            var save = CreateTempSaveSystem();
            var game = CreateGameManager(out _);
            var player = CreatePlayer("Day6CleanupPlayer");
            var coordinator = CreateRunCoordinator();

            var paywallRequests = 0;
            PaywallGate.OnRequested += _ => paywallRequests++;

            // RunCoordinator resolves the player in Start, and GameManager.UseContinue
            // reaches the player through it, so let one frame pass before the loop.
            yield return null;
            Assert.That(RunCoordinator.Active, Is.SameAs(coordinator));

            for (var cycle = 0; cycle < Cycles; cycle++)
            {
                game.StartRun();
                Assert.That(game.State, Is.EqualTo(GameState.Running), $"Cycle {cycle}: a fresh hub must start a run.");
                Assert.That(game.ContinuesLeft, Is.EqualTo(1), $"Cycle {cycle}: the continue balance must re-arm.");
                Assert.That(Time.timeScale, Is.EqualTo(1f), $"Cycle {cycle}: a new run must reset the time scale.");
                Assert.That(game.IsStartPending, Is.False);

                player.ResetRun();
                Assert.That(player.State, Is.EqualTo(PlayerState.Running), $"Cycle {cycle}: the player must come back alive.");
                Assert.That(player.IsInvincible, Is.False, $"Cycle {cycle}: stale invincibility must not survive a new run.");

                game.EndRun(new RunResult(100 + cycle, 10f, 0, 5, DistrictId.OldTown));
                Assert.That(game.State, Is.EqualTo(GameState.Dead));
                Assert.That(game.IsContinueOfferActive, Is.True, $"Cycle {cycle}: a continue must be offered.");

                Assert.That(game.UseContinue(), Is.True, $"Cycle {cycle}: the offer must be spendable.");
                Assert.That(game.State, Is.EqualTo(GameState.Running));
                Assert.That(
                    player.IsInvincible,
                    Is.True,
                    $"Cycle {cycle}: the continue respawn runs through RunCoordinator and must grant invincibility.");
                player.Simulate(2.1f);
                Assert.That(player.IsInvincible, Is.False, $"Cycle {cycle}: the window must expire.");

                // No continues left, so this death finalizes immediately.
                game.EndRun(new RunResult(200 + cycle, 20f, 1, 7, DistrictId.Downtown));
                Assert.That(game.State, Is.EqualTo(GameState.Dead));
                Assert.That(game.IsContinueOfferActive, Is.False, $"Cycle {cycle}: no continues remain.");
                Assert.That(
                    save.Data.totalRunsCompleted,
                    Is.EqualTo(cycle + 1),
                    $"Cycle {cycle}: exactly one run may be recorded per cycle.");

                game.ReturnToHub();
                Assert.That(game.State, Is.EqualTo(GameState.Hub), $"Cycle {cycle}: the hub must be restored.");
                Assert.That(Time.timeScale, Is.EqualTo(1f), $"Cycle {cycle}: the time scale must be restored.");
                Assert.That(game.IsContinueOfferActive, Is.False);
                Assert.That(game.IsStartPending, Is.False);

                game.ReturnToHub();
                Assert.That(game.State, Is.EqualTo(GameState.Hub), $"Cycle {cycle}: returning again must be idempotent.");
                Assert.That(
                    save.Data.totalRunsCompleted,
                    Is.EqualTo(cycle + 1),
                    $"Cycle {cycle}: a second return must not record the run again.");
                Assert.That(save.Data.runHistory.Count, Is.EqualTo(cycle + 1), $"Cycle {cycle}: history must hold one row per cycle.");

                yield return null;
            }

            Assert.That(save.Data.totalRunsCompleted, Is.EqualTo(Cycles));
            Assert.That(save.Data.runHistory.Count, Is.EqualTo(Cycles));
            Assert.That(
                paywallRequests,
                Is.EqualTo(1),
                "The after-run-3 paywall must be requested once, not once per completed run.");
            Assert.That(save.Data.hasSeenPaywall, Is.True);

            var newest = save.Data.runHistory[0];
            Assert.That(newest.score, Is.EqualTo(203L), "History is sorted by score, so the last cycle leads.");
            Assert.That(newest.districtReached, Is.EqualTo(nameof(DistrictId.Downtown)));
            Assert.That(newest.packagesDelivered, Is.EqualTo(1));

            Assert.That(
                File.Exists(Path.Combine(TempDirectory, "save.json")),
                Is.True,
                "Progress must be written, and only into the per-test temp directory.");
        }

        [UnityTest]
        public IEnumerator RepeatedReturnToHub_WithoutARunIsSafe()
        {
            var save = CreateTempSaveSystem();
            var game = CreateGameManager(out _);

            for (var cycle = 0; cycle < 5; cycle++)
            {
                game.ReturnToHub();
                Assert.That(game.State, Is.EqualTo(GameState.Hub));
                Assert.That(Time.timeScale, Is.EqualTo(1f), $"Cycle {cycle}: the time scale must stay at 1.");
                Assert.That(
                    save.Data.totalRunsCompleted,
                    Is.Zero,
                    $"Cycle {cycle}: returning to the hub with no run must not record anything.");
                yield return null;
            }
        }
    }
}
