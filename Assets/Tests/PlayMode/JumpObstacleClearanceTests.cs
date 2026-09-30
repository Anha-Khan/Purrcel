using System.Collections;
using CatCourier.Player;
using CatCourier.Core;
using CatCourier.Obstacles;
using CatCourier.Art;
using CatCourier.Coins;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace CatCourier.Tests.PlayMode
{
    public sealed class JumpObstacleClearanceTests : Day6PlayModeTestBase
    {
        [Test]
        public void RisingJump_ClearsLowRoadHazard()
        {
            var player = CreatePlayer("JumpClearancePlayer");
            var obstacle = CreateObstacle("LowRoadHazard", 0f, 0.5f);

            player.NotifyGround();
            player.RequestJump();
            player.Simulate(0.02f);
            Assert.That(player.State, Is.EqualTo(PlayerState.Jumping));
            Assert.That(player.VerticalVelocity, Is.GreaterThan(0f));

            player.NotifyObstacle(false, obstacle);

            Assert.That(player.State, Is.EqualTo(PlayerState.Jumping),
                "A low road hazard must not end the run during jump takeoff.");
        }

        [Test]
        public void JumpInput_BeforePhysicsContactClearsRoadHazard()
        {
            var player = CreatePlayer("ImmediateJumpPlayer");
            var obstacle = CreateObstacle("LowRoadHazard", 0f, 0.5f);

            player.NotifyGround();
            player.RequestJump();
            Assert.That(player.State, Is.EqualTo(PlayerState.Jumping));
            player.NotifyObstacle(false, obstacle);

            Assert.That(player.State, Is.EqualTo(PlayerState.Jumping));
        }

        [Test]
        public void GroundedContact_WithLowRoadHazardStillCausesGameOver()
        {
            var player = CreatePlayer("GroundedCollisionPlayer");
            var obstacle = CreateObstacle("LowRoadHazard", 0f, 0.5f);

            player.NotifyObstacle(false, obstacle);

            Assert.That(player.State, Is.EqualTo(PlayerState.Dead));
        }

        [Test]
        public void JumpingIntoHighHazardStillCausesGameOver()
        {
            var player = CreatePlayer("HighCollisionPlayer");
            var obstacle = CreateObstacle("HighHazard", 2f, 1f);

            player.NotifyGround();
            player.RequestJump();
            player.Simulate(0.02f);
            player.NotifyObstacle(false, obstacle);

            Assert.That(player.State, Is.EqualTo(PlayerState.Dead));
        }

        [UnityTest]
        public IEnumerator FallbackGroundFollowsRunnerPastItsOriginalEnd()
        {
            var player = CreatePlayer("EndlessGroundPlayer");
            var floor = Track(new GameObject("FallbackGround"));
            var ground = floor.AddComponent<GroundSurface>();
            ground.Follow(player.transform);
            floor.transform.position = new Vector3(10f, -0.25f, 0f);
            player.transform.position = new Vector3(33f, 0.4f, 0f);

            yield return null;

            Assert.That(floor.transform.position.x, Is.EqualTo(33f).Within(0.001f),
                "The fallback trigger must remain under the runner beyond the old x=30 edge.");
            Assert.That(floor.transform.position.y, Is.EqualTo(-0.25f).Within(0.001f),
                "Following the player must preserve the road height.");
        }

        [UnityTest]
        public IEnumerator EmptyChunkCatalog_KeepsSpawningFallbackObstacles()
        {
            var fallback = Track(new GameObject("FallbackContent"));
            var staticTemplate = CreateObstacleTemplate<StaticObstacle>(fallback.transform, "StaticTemplate", 12f);
            var bounceTemplate = CreateObstacleTemplate<BounceObstacle>(fallback.transform, "BounceTemplate", 18f);
            var coinObject = Track(new GameObject("CoinTemplate"));
            coinObject.transform.SetParent(fallback.transform, false);
            coinObject.AddComponent<CircleCollider2D>().isTrigger = true;
            var coinTemplate = coinObject.AddComponent<CoinPickup>();
            var player = CreatePlayer("FallbackSpawnerPlayer");
            player.GetComponent<Rigidbody2D>().position = new Vector2(10f, 0f);
            var camera = CreateCamera();
            var spawnerObject = Track(new GameObject("FallbackObstacleSpawner"));
            var spawner = spawnerObject.AddComponent<FallbackObstacleSpawner>();
            spawner.Configure(fallback.transform, player, camera, staticTemplate, bounceTemplate, coinTemplate);
            player.GetComponent<Rigidbody2D>().position = new Vector2(35f, 0f);
            player.transform.position = new Vector3(35f, 0f, 0f);

            yield return null;

            Assert.That(fallback.GetComponentsInChildren<CoinPickup>().Length, Is.GreaterThan(3),
                "The fallback route needs a visible starting coin trail.");
            var generated = fallback.GetComponentInChildren<PitHazard>();
            var otherGenerated = fallback.GetComponentsInChildren<ObstacleBase>();
            Assert.That(generated != null || otherGenerated.Length > 2, Is.True,
                "The route should add a hazard after the tutorial obstacles.");
        }

        private T CreateObstacleTemplate<T>(Transform parent, string name, float x) where T : ObstacleBase
        {
            var obstacle = Track(new GameObject(name));
            obstacle.transform.SetParent(parent, false);
            obstacle.transform.position = new Vector3(x, 0.75f, 0f);
            var collider = obstacle.AddComponent<BoxCollider2D>();
            collider.isTrigger = true;
            collider.size = Vector2.one;
            return obstacle.AddComponent<T>();
        }

        private BoxCollider2D CreateObstacle(string name, float y, float height)
        {
            var obstacleObject = Track(new GameObject(name));
            obstacleObject.transform.position = new Vector3(0f, y, 0f);
            var collider = obstacleObject.AddComponent<BoxCollider2D>();
            collider.size = new Vector2(1f, height);
            collider.isTrigger = true;
            return collider;
        }
    }
}
