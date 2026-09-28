using System.Collections;
using CatCourier.Core;
using CatCourier.Player;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace CatCourier.Tests.PlayMode
{
    /// <summary>
    /// Death, the continue offer, and respawn invincibility, driven through the real
    /// coroutine: the player dies, its 1.2s death coroutine reports into GameManager,
    /// the continue is spent, and RunCoordinator respawns with the invincibility
    /// window that has to absorb one more death before it lapses.
    /// </summary>
    public sealed class DeathContinueRespawnTests : Day6PlayModeTestBase
    {
        [UnityTest]
        public IEnumerator Continue_RespawnsWithInvincibilityAndSpendsExactlyOneContinue()
        {
            var save = CreateTempSaveSystem();
            var game = CreateGameManager(out _);
            var player = CreatePlayer("Day6ContinuePlayer");
            var coordinator = CreateRunCoordinator();

            // Let RunCoordinator.Start install the loadout and the result factory.
            yield return null;

            Assert.That(RunCoordinator.Active, Is.SameAs(coordinator), "RunCoordinator must claim the active slot.");
            Assert.That(
                player.ResultFactory,
                Is.Not.Null,
                "RunCoordinator must install a result factory so death produces a real RunResult.");

            game.StartRun();
            Assert.That(game.State, Is.EqualTo(GameState.Running));
            Assert.That(game.ContinuesLeft, Is.EqualTo(1), "A free player gets one continue per run.");

            player.TriggerDeath();
            Assert.That(
                player.State,
                Is.EqualTo(PlayerState.Dead),
                "An uninvincible death must stick.");
            Assert.That(
                game.State,
                Is.EqualTo(GameState.Running),
                "The run only ends once the death coroutine reports back.");

            var deadline = Time.unscaledTime + 5f;
            while (game.State != GameState.Dead && Time.unscaledTime < deadline)
            {
                yield return null;
            }

            Assert.That(game.State, Is.EqualTo(GameState.Dead), "The death coroutine must end the run.");
            Assert.That(game.IsContinueOfferActive, Is.True, "A player with a continue must be offered one.");
            Assert.That(
                save.Data.totalRunsCompleted,
                Is.Zero,
                "A run with a pending continue must not reach the save yet.");

            Assert.That(game.UseContinue(), Is.True, "The offer must be spendable.");
            Assert.That(game.State, Is.EqualTo(GameState.Running));
            Assert.That(game.ContinuesLeft, Is.Zero, "Spending a continue must decrement the balance.");
            Assert.That(
                player.State,
                Is.EqualTo(PlayerState.Running),
                "RunCoordinator must respawn the player through RespawnFromContinue.");
            Assert.That(
                player.IsInvincible,
                Is.True,
                "A continue respawn must open the invincibility window.");

            player.TriggerDeath();
            Assert.That(
                player.State,
                Is.EqualTo(PlayerState.Running),
                "The invincibility window must absorb a second death.");
            Assert.That(game.UseContinue(), Is.False, "A spent continue cannot be spent twice.");

            var waited = 0f;
            while (player.IsInvincible && waited < 6f)
            {
                waited += Time.deltaTime;
                yield return null;
            }

            Assert.That(player.IsInvincible, Is.False, "The invincibility window must expire on its own.");

            player.TriggerDeath();
            Assert.That(
                player.State,
                Is.EqualTo(PlayerState.Dead),
                "Once invincibility lapses, death must land again.");
        }

        [UnityTest]
        public IEnumerator DeclinedContinue_FinalizesTheRunExactlyOnce()
        {
            var save = CreateTempSaveSystem();
            var game = CreateGameManager(out _);

            game.StartRun();
            game.EndRun(new RunResult(500, 42f, 2, 15, DistrictId.OldTown));

            Assert.That(game.State, Is.EqualTo(GameState.Dead));
            Assert.That(game.IsContinueOfferActive, Is.True, "A free player has a continue to decline.");
            Assert.That(save.Data.totalRunsCompleted, Is.Zero, "Nothing is recorded while the offer is open.");

            game.DeclineContinue();
            Assert.That(game.IsContinueOfferActive, Is.False);
            Assert.That(game.State, Is.EqualTo(GameState.Dead), "Declining keeps the player on the death screen.");
            Assert.That(save.Data.totalRunsCompleted, Is.EqualTo(1), "Declining finalizes the run once.");

            game.DeclineContinue();
            Assert.That(
                save.Data.totalRunsCompleted,
                Is.EqualTo(1),
                "A repeated decline must not double-record the run.");

            yield return null;

            game.ReturnToHub();
            Assert.That(game.State, Is.EqualTo(GameState.Hub));
            Assert.That(
                save.Data.totalRunsCompleted,
                Is.EqualTo(1),
                "Returning to the hub after finalizing must not record again.");
        }

        [UnityTest]
        public IEnumerator ContinueOffer_ExpiresOnItsOwnWhenUnanswered()
        {
            var save = CreateTempSaveSystem();
            var game = CreateGameManager(out _);

            game.StartRun();
            game.EndRun(new RunResult(100, 10f, 0, 3, DistrictId.OldTown));
            Assert.That(game.IsContinueOfferActive, Is.True);

            // The offer window is a private 5s of unscaled time; wait it out and
            // confirm the run finalized without anyone calling DeclineContinue.
            var deadline = Time.unscaledTime + 8f;
            while (game.IsContinueOfferActive && Time.unscaledTime < deadline)
            {
                yield return null;
            }

            Assert.That(game.IsContinueOfferActive, Is.False, "The offer must expire on its own.");
            Assert.That(save.Data.totalRunsCompleted, Is.EqualTo(1), "An expired offer finalizes the run.");
            Assert.That(game.State, Is.EqualTo(GameState.Dead));
        }
    }
}
