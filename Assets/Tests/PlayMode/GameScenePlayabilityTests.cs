using System.Collections;
using System.Linq;
using CatCourier.Art;
using CatCourier.Coins;
using CatCourier.Core;
using CatCourier.Generation;
using CatCourier.Obstacles;
using CatCourier.Packages;
using CatCourier.Player;
using CatCourier.Scoring;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace CatCourier.Tests.PlayMode
{
    // Inherits the harness for its save-path byte snapshot and static resets. This
    // suite loads the real Hub and Game scenes, so it needs that guard even more
    // than the suites that build their own objects.
    public sealed class GameScenePlayabilityTests : Day6PlayModeTestBase
    {
        [UnityTest]
        public IEnumerator HubScene_HasPaintedCamera()
        {
            yield return SceneManager.LoadSceneAsync("Hub", LoadSceneMode.Additive);
            var hub = GameObject.Find("HubRoot");
            Assert.That(hub, Is.Not.Null);
            Assert.That(hub.GetComponentInChildren<Camera>(), Is.Not.Null,
                "The Hub must render behind its menu instead of showing 'No cameras rendering'.");
            Assert.That(hub.transform.Find("Painted Courier Square"), Is.Not.Null);
            yield return SceneManager.UnloadSceneAsync("Hub");
        }

        [UnityTest]
        public IEnumerator OpeningCoins_AreVisibleAndCollectibleBeforeFirstHazard()
        {
            yield return SceneManager.LoadSceneAsync("Game", LoadSceneMode.Additive);
            var player = Object.FindObjectOfType<PlayerController>();
            var root = GameObject.Find("Day2FallbackContent");
            Assert.That(player, Is.Not.Null);
            Assert.That(root, Is.Not.Null);
            var openingCoins = root.GetComponentsInChildren<CoinPickup>()
                .Where(coin => coin.name == "Route Coin" && coin.transform.position.x < 8f).ToArray();
            Assert.That(openingCoins.Length, Is.GreaterThanOrEqualTo(4));
            Assert.That(openingCoins.All(coin => coin.GetComponentInChildren<SpriteRenderer>() != null), Is.True);
            Assert.That(openingCoins.All(coin =>
                coin.GetComponentInChildren<SpriteRenderer>().transform.position.y <
                coin.transform.position.y - 0.7f), Is.True,
                "The collectible rings must appear on the cat's painted road lane.");

            var timeout = 4f;
            while (player.DistanceMeters < 8f && timeout > 0f && player.State != PlayerState.Dead)
            {
                yield return new WaitForFixedUpdate();
                timeout -= Time.fixedDeltaTime;
            }
            var collected = player.GetComponent<CoinManager>().RunCoins;
            yield return SceneManager.UnloadSceneAsync("Game");
            Assert.That(collected, Is.GreaterThan(0),
                "The cat must collect an opening ground-level coin without a jump.");
        }

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
            // One action per frame, and a jump leaves the cat unable to slide. So each frame
            // work out both needs and serve the slide first: an overhead hazard is
            // the one thing a jumping cat cannot answer.
            while (simulatedSeconds < 35f && player.DistanceMeters < 120f &&
                   player.State != PlayerState.Dead)
            {
                var ahead = fallback.GetComponentsInChildren<MonoBehaviour>()
                    .Where(component => component is StaticObstacle || component is PitHazard)
                    .Where(component => component.transform.position.x > player.DistanceMeters)
                    .OrderBy(component => component.transform.position.x).ToArray();
                var overhead = ahead.FirstOrDefault(component => component.name.StartsWith("Slide Under"));
                var ground = ahead.FirstOrDefault(component => !component.name.StartsWith("Slide Under"));
                var overheadGap = overhead != null
                    ? overhead.transform.position.x - player.DistanceMeters
                    : float.MaxValue;
                var groundGap = ground != null
                    ? ground.transform.position.x - player.DistanceMeters
                    : float.MaxValue;

                if (ahead.Length > 0)
                {
                    lastHazard = $"{ahead[0].name} at {ahead[0].transform.position.x:0.0}, " +
                                 $"next {(ahead[0].transform.position.x - player.DistanceMeters):0.0} m";
                }

                if (player.IsGrounded && overheadGap < 2.1f)
                {
                    lastAction = $"slide at {player.DistanceMeters:0.0}";
                    player.RequestSlide();
                }
                else if (player.IsGrounded && groundGap < 2.8f && overheadGap > 6f)
                {
                    lastAction = $"jump at {player.DistanceMeters:0.0}";
                    player.RequestJump();
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

            // Collecting the scene coin deactivates the object used as the
            // spawner template. Later clones still need to be active.
            var coinTemplate = fallback.GetComponentInChildren<CoinPickup>(true);
            Assert.That(coinTemplate, Is.Not.Null);
            coinTemplate.gameObject.SetActive(false);

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
            Assert.That(fallback.GetComponentsInChildren<CoinPickup>()
                .Any(coin => coin.name == "Route Coin" && coin.transform.position.x > 30f), Is.True,
                "Coins spawned after the scene template is collected must be active.");

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

            // StumbleObstacle counts as route content. It shares none of the base classes
            // above, so leaving it out made the stream look short every time the bag
            // dealt a stumble, which is one slot in five.
            var spawned = fallback.GetComponentsInChildren<MonoBehaviour>()
                .Where(component => component is StaticObstacle || component is PitHazard ||
                    component is StumbleObstacle)
                .Where(component => component.name.StartsWith("Low Road") ||
                    component.name.StartsWith("Falling Road") ||
                    component.name.StartsWith("Slide Under") ||
                    component.name.StartsWith("Jump Road") ||
                    component.name.StartsWith("Loose Paving Stone"))
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

            body.position = new Vector2(170f, 0.4f);
            player.transform.position = body.position;
            yield return null;
            yield return null;
            Assert.That(fallback.GetComponentsInChildren<CoinPickup>()
                .Any(coin => coin.name == "Route Coin" && coin.transform.position.x > 170f), Is.True,
                "Ground coin trails must continue far into the run.");
            Assert.That(fallback.GetComponentsInChildren<MonoBehaviour>()
                .Any(component => (component is StaticObstacle || component is PitHazard) &&
                    component.transform.position.x > 170f), Is.True,
                "Hazards must continue far into the run.");

            yield return SceneManager.UnloadSceneAsync("Game");
        }

        /// <summary>
        /// The fallback route emits its own checkpoints because ChunkManager only accepts
        /// markers parented under a ChunkMarker with a catalog selection. Without them the
        /// shipped game never delivered a parcel, so package scoring, the story cards and
        /// district progression were all unreachable at runtime.
        /// </summary>
        [UnityTest]
        public IEnumerator FallbackRoute_SpawnsCheckpointsThatDeliver()
        {
            yield return SceneManager.LoadSceneAsync("Game", LoadSceneMode.Additive);
            var fallback = GameObject.Find("Day2FallbackContent");
            var player = Object.FindObjectOfType<PlayerController>();
            var score = Object.FindObjectOfType<ScoreManager>();
            var packages = Object.FindObjectOfType<PackageManager>();
            Assert.That(fallback, Is.Not.Null);
            Assert.That(player, Is.Not.Null);
            Assert.That(packages, Is.Not.Null);

            // RunCoordinator assigns the opening parcel in Start, which runs a frame after the
            // scene finishes loading, so give it one before asserting.
            for (var step = 0; step < 3; step++)
            {
                yield return null;
            }

            // A run starts with a parcel assigned, so the checkpoint has something to
            // deliver. Assert it first: a delivery test that passes by delivering
            // nothing is worse than no test.
            Assert.That(packages.HasAssignedPackage, Is.True,
                "A fresh run must carry a package, or a checkpoint delivery proves nothing.");

            // The first checkpoint is a full CHECKPOINT_INTERVAL away and the spawner only
            // streams ahead of the camera, so it does not exist yet at the start line.
            // Stand the cat short of that distance and let a frame stream it in.
            var body = player.GetComponent<Rigidbody2D>();
            var firstCheckpointX = Constants.CHECKPOINT_INTERVAL - 20f;
            body.position = new Vector2(firstCheckpointX, 0.4f);
            player.transform.position = body.position;
            for (var step = 0; step < 10; step++)
            {
                yield return new WaitForFixedUpdate();
            }

            var checkpoint = fallback.GetComponentsInChildren<CheckpointMarker>()
                .OrderBy(marker => marker.transform.position.x)
                .FirstOrDefault(marker => marker.transform.position.x > firstCheckpointX);
            Assert.That(checkpoint, Is.Not.Null,
                "The fallback route must stream delivery checkpoints; the authored catalog is empty.");

            var delivered = 0;
            packages.OnPackageDelivered += () => delivered++;
            var scoreBefore = score != null ? score.Score : 0;

            // Walk the cat into the trigger by pinning its position each step. Letting it
            // actually run that stretch made this test flaky: the hazards between here and
            // the checkpoint killed it, which is a real gameplay fact but not what this
            // test is for. Hazard survival has its own test above.
            var markerX = checkpoint.transform.position.x;
            for (var step = 0; step < 40 && delivered == 0; step++)
            {
                var approach = Mathf.Min(markerX, firstCheckpointX + step * 0.5f);
                body.position = new Vector2(approach, 0.4f);
                player.transform.position = body.position;
                yield return new WaitForFixedUpdate();
            }

            Assert.That(delivered, Is.GreaterThan(0),
                "Reaching a fallback checkpoint must deliver the carried package.");
            Assert.That(score != null && score.Score > scoreBefore, Is.True,
                "A delivery must award score.");

            // Force at least one full route refresh past the checkpoint.
            for (var step = 0; step < 30; step++)
            {
                yield return null;
            }

            yield return SceneManager.UnloadSceneAsync("Game");
        }
    }
}
