using System.Collections;
using System.Linq;
using CatCourier.Art;
using CatCourier.Coins;
using CatCourier.Core;
using CatCourier.Obstacles;
using CatCourier.Player;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace CatCourier.Tests.PlayMode
{
    public sealed class GameScenePlayabilityTests
    {
        [UnityTest]
        public IEnumerator RealGameScene_CanRunAndJumpPastStreamedHazards()
        {
            yield return SceneManager.LoadSceneAsync("Game", LoadSceneMode.Additive);
            var player = Object.FindObjectOfType<PlayerController>();
            var fallback = GameObject.Find("Day2FallbackContent");
            Assert.That(player, Is.Not.Null);
            Assert.That(fallback, Is.Not.Null);

            Time.timeScale = 4f;
            var simulatedSeconds = 0f;
            var lastHazard = string.Empty;
            var lastAction = string.Empty;
            while (simulatedSeconds < 35f && player.DistanceMeters < 120f &&
                   player.State != PlayerState.Dead)
            {
                var ahead = fallback.GetComponentsInChildren<MonoBehaviour>()
                    .Where(component => component is StaticObstacle || component is PitHazard)
                    .Where(component => component.transform.position.x > player.DistanceMeters)
                    .OrderBy(component => component.transform.position.x).FirstOrDefault();
                if (ahead != null && player.IsGrounded)
                {
                    var distance = ahead.transform.position.x - player.DistanceMeters;
                    lastHazard = $"{ahead.name} at {ahead.transform.position.x:0.0}, distance {distance:0.0}";
                    if (ahead.name.StartsWith("Slide Under") && distance < 2.1f)
                    {
                        lastAction = $"slide at {player.DistanceMeters:0.0}";
                        player.RequestSlide();
                    }
                    else if (!ahead.name.StartsWith("Slide Under") && distance < 2.8f)
                    {
                        lastAction = $"jump at {player.DistanceMeters:0.0}";
                        player.RequestJump();
                    }
                }

                yield return new WaitForFixedUpdate();
                simulatedSeconds += Time.fixedDeltaTime;
            }

            var finalDistance = player.DistanceMeters;
            var finalState = player.State;
            var deathReason = player.LastDeathReason;
            var collectedCoins = player.GetComponent<CoinManager>().RunCoins;
            Time.timeScale = 1f;
            yield return SceneManager.UnloadSceneAsync("Game");
            Assert.That(finalState, Is.Not.EqualTo(PlayerState.Dead),
                $"The run ended at {finalDistance:0.0} m ({deathReason}). Last hazard: {lastHazard}; last action: {lastAction}.");
            Assert.That(finalDistance, Is.GreaterThanOrEqualTo(120f),
                "A player who jumps before low hazards should be able to keep running.");
            Assert.That(collectedCoins, Is.GreaterThan(0),
                "The opening route must contain coins the running cat can actually collect.");
        }

        [UnityTest]
        public IEnumerator RealGameScene_ScrollsSceneryAndSpawnsRoadHazards()
        {
            yield return SceneManager.LoadSceneAsync("Game", LoadSceneMode.Additive);

            var player = Object.FindObjectOfType<PlayerController>();
            var backdrop = Object.FindObjectOfType<GeneratedBackdrop>();
            var spawner = Object.FindObjectOfType<FallbackObstacleSpawner>();
            var fallback = GameObject.Find("Day2FallbackContent");
            Assert.That(player, Is.Not.Null);
            Assert.That(backdrop, Is.Not.Null);
            Assert.That(spawner, Is.Not.Null);
            Assert.That(fallback, Is.Not.Null);
            Assert.That(fallback.activeInHierarchy, Is.True);

            // The first shuffled bag guarantees one of each hazard; inspect it
            // before the recycler removes objects behind the camera.
            player.enabled = false;
            var body = player.GetComponent<Rigidbody2D>();
            body.position = new Vector2(45f, 0.4f);
            player.transform.position = body.position;
            yield return null;
            yield return null;
            var firstBag = fallback.GetComponentsInChildren<MonoBehaviour>()
                .Where(component => component is StaticObstacle || component is PitHazard)
                .Where(component => component.name.StartsWith("Low Road") ||
                    component.name.StartsWith("Falling Road") ||
                    component.name.StartsWith("Slide Under") ||
                    component.name.StartsWith("Jump Road")).ToArray();
            Assert.That(firstBag.Any(item => item is PitHazard), Is.True);
            Assert.That(firstBag.Any(item => item.name.StartsWith("Slide Under")), Is.True);
            Assert.That(firstBag.Any(item => item.name.StartsWith("Falling Road")), Is.True);

            body.position = new Vector2(85f, 0.4f);
            player.transform.position = body.position;
            yield return null;
            yield return null;

            var first = backdrop.transform.Find("Scenic Transition 0 From");
            var second = backdrop.transform.Find("Scenic Transition 1 From");
            Assert.That(first, Is.Not.Null);
            Assert.That(second, Is.Not.Null);
            Assert.That(first.position.x, Is.EqualTo(38.5f).Within(0.05f),
                "The panorama must stay in world space while the camera moves.");
            Assert.That(second.position.x, Is.EqualTo(82.5f).Within(0.05f));
            Assert.That(second.GetComponent<SpriteRenderer>().enabled, Is.True);

            var spawned = fallback.GetComponentsInChildren<MonoBehaviour>()
                .Where(component => component is StaticObstacle || component is PitHazard)
                .Where(component => component.name.StartsWith("Low Road") ||
                    component.name.StartsWith("Falling Road") ||
                    component.name.StartsWith("Slide Under") ||
                    component.name.StartsWith("Jump Road"))
                .OrderBy(component => component.transform.position.x).ToArray();
            Assert.That(spawned.Length, Is.GreaterThanOrEqualTo(4),
                "The empty chunk catalog must still provide an obstacle stream.");
            Assert.That(spawned.Last().transform.position.x, Is.GreaterThan(85f));
            var firstGap = spawned[1].transform.position.x - spawned[0].transform.position.x;
            var secondGap = spawned[2].transform.position.x - spawned[1].transform.position.x;
            Assert.That(Mathf.Abs(secondGap - firstGap), Is.GreaterThan(0.01f),
                "Obstacle spacing should vary rather than repeat a fixed pair.");
            Assert.That(fallback.GetComponentsInChildren<CoinPickup>().Length, Is.GreaterThan(12),
                "The route needs visible coins beyond the three scene pickups.");
            Assert.That(fallback.GetComponentsInChildren<CoinPickup>()
                .Any(coin => coin.name == "Special Coin"), Is.True);
            Assert.That(fallback.GetComponentInChildren<BoostPickup>(), Is.Not.Null);

            yield return SceneManager.UnloadSceneAsync("Game");
        }
    }
}
