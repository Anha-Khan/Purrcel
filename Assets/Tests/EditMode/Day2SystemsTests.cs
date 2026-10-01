using System.Reflection;
using CatCourier.Coins;
using CatCourier.Core;
using CatCourier.Player;
using CatCourier.Scoring;
using NUnit.Framework;
using UnityEngine;

namespace CatCourier.Tests
{
    public sealed class Day2SystemsTests
    {
        // ponytail: timeScale and the GameManager singleton are global. Without this
        // the fixture leaks both, and the suite only passed by accident of test order.
        [SetUp]
        public void ResetGlobalState()
        {
            Time.timeScale = 1f;
            ClearSingleton<GameManager>();
        }

        [TearDown]
        public void RestoreGlobalState()
        {
            Time.timeScale = 1f;
            ClearSingleton<GameManager>();
        }

        [Test]
        public void PlayerStats_UsesTimeFormulaAndCapsSpeed()
        {
            var stats = new PlayerStats();

            stats.SetRunTime(0f);
            Assert.That(stats.CurrentRunSpeed, Is.EqualTo(Constants.BASE_RUN_SPEED).Within(0.001f));

            stats.SetRunTime(10f);
            Assert.That(stats.CurrentRunSpeed, Is.EqualTo(6.5f).Within(0.001f));

            stats.SetRunTime(10000f);
            Assert.That(stats.CurrentRunSpeed, Is.EqualTo(Constants.MAX_RUN_SPEED));
        }

        [Test]
        public void PlayerStats_CatSpeedBonusChangesBaseTermOnly()
        {
            var stats = new PlayerStats();
            stats.SetCatBonuses(1f, 0f);

            stats.SetRunTime(10f);

            Assert.That(stats.CurrentRunSpeed, Is.EqualTo(7.5f).Within(0.001f));
        }

        [Test]
        public void GroundJump_AppliesExactForce()
        {
            var host = CreatePlayer(out var player);
            try
            {
                player.NotifyGround();
                player.RequestJump();
                player.Simulate(0f);

                Assert.That(player.State, Is.EqualTo(PlayerState.Jumping));
                Assert.That(player.VerticalVelocity, Is.EqualTo(Constants.JUMP_FORCE).Within(0.001f));
            }
            finally
            {
                Object.DestroyImmediate(host);
            }
        }

        [Test]
        public void CoyoteJump_WorksInsideWindow()
        {
            var host = CreatePlayer(out var player);
            try
            {
                player.NotifyGround();
                player.NotifyGroundExit();
                player.Simulate(Constants.COYOTE_TIME * 0.9f);
                player.RequestJump();
                player.Simulate(0f);

                Assert.That(player.State, Is.EqualTo(PlayerState.Jumping));
            }
            finally
            {
                Object.DestroyImmediate(host);
            }
        }

        [Test]
        public void JumpBuffer_FiresOnLandingInsideWindow()
        {
            var host = CreatePlayer(out var player);
            try
            {
                player.NotifyGround();
                player.RequestJump();
                player.Simulate(0f);
                player.RequestJump();
                player.Simulate(Constants.JUMP_BUFFER_TIME * 0.9f);
                player.NotifyGround();
                player.Simulate(0f);

                Assert.That(player.State, Is.EqualTo(PlayerState.Jumping));
            }
            finally
            {
                Object.DestroyImmediate(host);
            }
        }

        [Test]
        public void DoubleJump_IsUnavailableLockedAndConsumedOnceUnlocked()
        {
            var host = CreatePlayer(out var lockedPlayer);
            try
            {
                lockedPlayer.NotifyGround();
                lockedPlayer.RequestJump();
                lockedPlayer.Simulate(0f);
                lockedPlayer.RequestJump();
                lockedPlayer.Simulate(0f);

                Assert.That(lockedPlayer.DoubleJumpAvailable, Is.False);
                Assert.That(lockedPlayer.VerticalVelocity, Is.EqualTo(Constants.JUMP_FORCE).Within(0.001f));
            }
            finally
            {
                Object.DestroyImmediate(host);
            }

            host = CreatePlayer(out var unlockedPlayer);
            try
            {
                unlockedPlayer.Stats.SetUpgrades(Constants.BASE_RUN_SPEED, Constants.JUMP_FORCE, true, Constants.DOUBLE_JUMP_FORCE, 0f, 1f, 1);
                unlockedPlayer.NotifyGround();
                unlockedPlayer.RequestJump();
                unlockedPlayer.Simulate(0f);
                unlockedPlayer.RequestJump();
                unlockedPlayer.Simulate(0f);
                unlockedPlayer.RequestJump();
                unlockedPlayer.Simulate(0f);

                Assert.That(unlockedPlayer.DoubleJumpAvailable, Is.False);
                Assert.That(unlockedPlayer.VerticalVelocity, Is.EqualTo(Constants.DOUBLE_JUMP_FORCE).Within(0.001f));

                unlockedPlayer.NotifyGround();
                Assert.That(unlockedPlayer.DoubleJumpAvailable, Is.False);
            }
            finally
            {
                Object.DestroyImmediate(host);
            }
        }

        [Test]
        public void Slide_RequiresGround_ChangesHitbox_AndJumpCancels()
        {
            var host = CreatePlayer(out var player);
            try
            {
                player.NotifyGround();
                player.RequestJump();
                player.Simulate(0f);
                player.RequestSlide();
                Assert.That(player.State, Is.EqualTo(PlayerState.Jumping));

                player.NotifyGround();
                player.RequestSlide();
                Assert.That(player.State, Is.EqualTo(PlayerState.Sliding));
                Assert.That(player.Hitbox.size.y, Is.EqualTo(Constants.SLIDE_HITBOX_HEIGHT_ABS).Within(0.001f));

                player.RequestJump();
                player.Simulate(0f);
                Assert.That(player.State, Is.EqualTo(PlayerState.Jumping));
                Assert.That(player.Hitbox.size.y, Is.EqualTo(Constants.PLAYER_HITBOX_HEIGHT).Within(0.001f));
            }
            finally
            {
                Object.DestroyImmediate(host);
            }
        }

        [Test]
        public void Slide_ExpiresAtDocumentedDuration()
        {
            var host = CreatePlayer(out var player);
            try
            {
                player.NotifyGround();
                player.RequestSlide();
                player.Simulate(Constants.SLIDE_DURATION);

                Assert.That(player.State, Is.EqualTo(PlayerState.Running));
                Assert.That(player.Hitbox.size.y, Is.EqualTo(Constants.PLAYER_HITBOX_HEIGHT).Within(0.001f));
            }
            finally
            {
                Object.DestroyImmediate(host);
            }
        }

        [Test]
        public void WallBounce_AppliesForcesAndTransitionsToJumping()
        {
            var host = CreatePlayer(out var player);
            try
            {
                player.NotifyGround();
                player.NotifyWallBounce();

                Assert.That(player.CurrentHorizontalVelocity, Is.EqualTo(Constants.WALL_BOUNCE_HORIZONTAL));
                Assert.That(player.VerticalVelocity, Is.EqualTo(Constants.WALL_BOUNCE_VERTICAL));
                Assert.That(player.State, Is.EqualTo(PlayerState.WallBounce));

                player.Simulate(Constants.WALL_BOUNCE_DURATION);
                Assert.That(player.State, Is.EqualTo(PlayerState.Jumping));
            }
            finally
            {
                Object.DestroyImmediate(host);
            }
        }

        [Test]
        public void WallBounce_ResetsDoubleJumpAvailabilityWhenUnlocked()
        {
            var host = CreatePlayer(out var player);
            try
            {
                player.Stats.SetUpgrades(Constants.BASE_RUN_SPEED, Constants.JUMP_FORCE, true, Constants.DOUBLE_JUMP_FORCE, 0f, 1f, 1);
                player.NotifyGround();
                player.RequestJump();
                player.Simulate(0f);
                player.RequestJump();
                player.Simulate(0f);
                Assert.That(player.DoubleJumpAvailable, Is.False);

                player.NotifyWallBounce();
                Assert.That(player.DoubleJumpAvailable, Is.True);
            }
            finally
            {
                Object.DestroyImmediate(host);
            }
        }

        [Test]
        public void Death_BlocksInput_AndFallTriggersDeath()
        {
            var host = CreatePlayer(out var player);
            try
            {
                player.TriggerDeath();
                player.RequestJump();
                player.Simulate(0f);
                Assert.That(player.State, Is.EqualTo(PlayerState.Dead));
            }
            finally
            {
                Object.DestroyImmediate(host);
            }

            host = CreatePlayer(out var fallingPlayer);
            try
            {
                fallingPlayer.ConfigureWorldDeathY(-10f);
                fallingPlayer.transform.position = new Vector3(0f, -11f, 0f);
                fallingPlayer.Simulate(0f);
                Assert.That(fallingPlayer.State, Is.EqualTo(PlayerState.Dead));
            }
            finally
            {
                Object.DestroyImmediate(host);
            }
        }

        [Test]
        public void PausedGame_RejectsBufferedJumpAndSlide()
        {
            var managerHost = new GameObject("PausedGameManagerDay2Test");
            var playerHost = CreatePlayer(out var player);
            try
            {
                var manager = managerHost.AddComponent<GameManager>();
                Assert.That(InvokePrivate(manager, "ClaimSingleton"), Is.True);
                InvokePrivate(manager, "SetState", GameState.Running);
                player.NotifyGround();

                manager.PauseRun();
                player.RequestJump();
                player.RequestSlide();
                player.Simulate(0f);

                Assert.That(player.State, Is.EqualTo(PlayerState.Running));
                Assert.That(Time.timeScale, Is.Zero);
            }
            finally
            {
                Object.DestroyImmediate(playerHost);
                Object.DestroyImmediate(managerHost);
            }
        }

        [Test]
        public void DeathDelay_CannotBePausedByUiButton()
        {
            var managerHost = new GameObject("DeathPauseManagerDay2Test");
            var playerHost = CreatePlayer(out var player);
            try
            {
                var manager = managerHost.AddComponent<GameManager>();
                Assert.That(InvokePrivate(manager, "ClaimSingleton"), Is.True);
                InvokePrivate(manager, "SetState", GameState.Running);
                player.TriggerDeath();

                player.GetComponent<InputHandler>().PressPause();

                Assert.That(Time.timeScale, Is.EqualTo(1f));
                Assert.That(manager.State, Is.EqualTo(GameState.Running));
            }
            finally
            {
                Object.DestroyImmediate(playerHost);
                Object.DestroyImmediate(managerHost);
            }
        }

        [Test]
        public void CoinManager_AwardsRawValuesOnce_AndResetsRunTotal()
        {
            var host = new GameObject("CoinManagerDay2Test");
            try
            {
                var manager = host.AddComponent<CoinManager>();
                manager.Configure(1f, 1f, 1f, 0f);
                var events = 0;
                manager.OnCoinsCollected += (_, _, _) => events++;

                Assert.That(manager.Collect(Constants.COIN_BASE_VALUE), Is.EqualTo(1));
                Assert.That(manager.Collect(Constants.COIN_RARE_VALUE), Is.EqualTo(5));
                Assert.That(manager.RunCoins, Is.EqualTo(6));
                Assert.That(events, Is.EqualTo(2));

                manager.ResetRun();
                Assert.That(manager.RunCoins, Is.Zero);
                Assert.That(manager.Bank, Is.EqualTo(6));
            }
            finally
            {
                Object.DestroyImmediate(host);
            }
        }

        [Test]
        public void CoinPickup_PreventsDuplicateCollection_AndChecksMagnetRadius()
        {
            var managerHost = new GameObject("CoinManagerMagnetTest");
            var coinHost = new GameObject("CoinPickupTest");
            try
            {
                var manager = managerHost.AddComponent<CoinManager>();
                manager.Configure(1f, 1f, 1f, 1.5f);
                var pickup = coinHost.AddComponent<CoinPickup>();
                pickup.Configure(manager, Constants.COIN_BASE_VALUE);

                Assert.That(pickup.TryCollectFromMagnet(new Vector3(1.5f, 0f, 0f)), Is.True);
                Assert.That(pickup.Collect(), Is.False);
                Assert.That(manager.RunCoins, Is.EqualTo(1));
            }
            finally
            {
                Object.DestroyImmediate(coinHost);
                Object.DestroyImmediate(managerHost);
            }

            managerHost = new GameObject("CoinManagerOutsideMagnetTest");
            coinHost = new GameObject("CoinPickupOutsideMagnetTest");
            try
            {
                var manager = managerHost.AddComponent<CoinManager>();
                manager.Configure(1f, 1f, 1f, 1.5f);
                var pickup = coinHost.AddComponent<CoinPickup>();
                pickup.Configure(manager, Constants.COIN_BASE_VALUE);

                Assert.That(pickup.TryCollectFromMagnet(new Vector3(1.51f, 0f, 0f)), Is.False);
                Assert.That(pickup.Collected, Is.False);
            }
            finally
            {
                Object.DestroyImmediate(coinHost);
                Object.DestroyImmediate(managerHost);
            }
        }

        [Test]
        public void ScoreManager_TracksDistancePackagesAndMilestoneEvents()
        {
            var host = new GameObject("ScoreManagerDay2Test");
            try
            {
                var score = host.AddComponent<ScoreManager>();
                var milestone = 0;
                score.OnComboMilestone += _ => milestone++;
                score.SetDistance(10f);
                score.AddPackage();
                for (var i = 0; i < 10; i++)
                {
                    score.CollectCoin(Constants.COIN_BASE_VALUE, i);
                }

                Assert.That(score.Score, Is.EqualTo(615L));
                Assert.That(score.ComboCount, Is.EqualTo(10));
                Assert.That(score.ComboMultiplier, Is.EqualTo(1.5f));
                Assert.That(milestone, Is.EqualTo(1));
            }
            finally
            {
                Object.DestroyImmediate(host);
            }
        }

        [Test]
        public void ScoreManager_ComboBreaksOnGapAndObstacle()
        {
            var host = new GameObject("ScoreManagerComboTest");
            try
            {
                var score = host.AddComponent<ScoreManager>();
                for (var i = 0; i < 10; i++)
                {
                    score.CollectCoin(1, i);
                }

                score.CollectCoin(1, 20f);
                Assert.That(score.ComboCount, Is.EqualTo(1));
                Assert.That(score.ComboMultiplier, Is.EqualTo(1f));

                for (var i = 0; i < 5; i++)
                {
                    score.CollectCoin(1, 21f + i);
                }

                score.RegisterObstacleContact();
                Assert.That(score.ComboCount, Is.Zero);
            }
            finally
            {
                Object.DestroyImmediate(host);
            }
        }

        [Test]
        public void ScoreManager_ComboBandsMatchContract()
        {
            Assert.That(ScoreManager.GetComboMultiplier(0), Is.EqualTo(1f));
            Assert.That(ScoreManager.GetComboMultiplier(9), Is.EqualTo(1f));
            Assert.That(ScoreManager.GetComboMultiplier(10), Is.EqualTo(1.5f));
            Assert.That(ScoreManager.GetComboMultiplier(19), Is.EqualTo(1.5f));
            Assert.That(ScoreManager.GetComboMultiplier(20), Is.EqualTo(2f));
            Assert.That(ScoreManager.GetComboMultiplier(29), Is.EqualTo(2f));
            Assert.That(ScoreManager.GetComboMultiplier(30), Is.EqualTo(3f));
        }

        [Test]
        public void Difficulty_ReachesDocumentedLevelsAndAnchors()
        {
            var host = new GameObject("DifficultyManagerDay2Test");
            try
            {
                var difficulty = host.AddComponent<DifficultyManager>();
                difficulty.SetDistance(0f);
                Assert.That(difficulty.Level, Is.Zero);

                difficulty.SetDistance(150f);
                Assert.That(difficulty.Level, Is.EqualTo(1));

                difficulty.SetDistance(300f);
                Assert.That(difficulty.Level, Is.EqualTo(2));
                Assert.That(difficulty.ObstacleDensity, Is.EqualTo(1.12f).Within(0.001f));
                Assert.That(difficulty.DronePatrolSpeed, Is.EqualTo(2.32f).Within(0.001f));
                Assert.That(difficulty.CoinClusterFrequency, Is.EqualTo(0.94f).Within(0.001f));
                Assert.That(difficulty.ScoreMultiplier, Is.EqualTo(1.2f).Within(0.001f));

                difficulty.SetDistance(750f);
                Assert.That(difficulty.Level, Is.EqualTo(5));
                Assert.That(difficulty.ObstacleDensity, Is.EqualTo(1.3f).Within(0.001f));
                Assert.That(difficulty.DronePatrolSpeed, Is.EqualTo(2.8f).Within(0.001f));
                Assert.That(difficulty.CoinClusterFrequency, Is.EqualTo(0.85f).Within(0.001f));
                Assert.That(difficulty.ScoreMultiplier, Is.EqualTo(1.5f).Within(0.001f));

                difficulty.SetDistance(1500f);
                Assert.That(difficulty.Level, Is.EqualTo(10));
                Assert.That(difficulty.ObstacleDensity, Is.EqualTo(1.6f).Within(0.001f));
                Assert.That(difficulty.DronePatrolSpeed, Is.EqualTo(4f).Within(0.001f));
                Assert.That(difficulty.CoinClusterFrequency, Is.EqualTo(0.7f).Within(0.001f));
                Assert.That(difficulty.ScoreMultiplier, Is.EqualTo(2f).Within(0.001f));
            }
            finally
            {
                Object.DestroyImmediate(host);
            }
        }

        private static void ClearSingleton<T>() where T : class
        {
            var property = typeof(T).GetProperty("Instance", BindingFlags.Static | BindingFlags.Public);
            property?.GetSetMethod(true)?.Invoke(null, new object[] { null });
        }

        private static object InvokePrivate(object target, string methodName, params object[] arguments)
        {
            var method = target.GetType().GetMethod(methodName, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(method, Is.Not.Null);
            return method.Invoke(target, arguments);
        }

        private static GameObject CreatePlayer(out PlayerController player)
        {
            var host = new GameObject("PlayerDay2Test");
            host.AddComponent<Rigidbody2D>();
            var collider = host.AddComponent<BoxCollider2D>();
            collider.size = new Vector2(Constants.PLAYER_HITBOX_WIDTH, Constants.PLAYER_HITBOX_HEIGHT);
            host.AddComponent<DifficultyManager>();
            host.AddComponent<ScoreManager>();
            host.AddComponent<CoinManager>();
            host.AddComponent<InputHandler>();
            player = host.AddComponent<PlayerController>();
            player.Initialize();
            return host;
        }
    }
}
