using System;
using System.Collections.Generic;
using CatCourier.Coins;
using CatCourier.Core;
using CatCourier.Generation;
using CatCourier.Obstacles;
using CatCourier.Player;
using UnityEngine;

namespace CatCourier.Art
{
    /// <summary>Streams a playable route while the authored chunk catalog is empty.</summary>
    public sealed class FallbackObstacleSpawner : MonoBehaviour
    {
        /// <summary>
        /// Publishes district and delivery state for a checkpoint. Set by GeneratedBackdrop
        /// because the fallback route has no ChunkMarker of its own.
        /// </summary>
        public ChunkManager chunkManager;

        // PatrolObstacle is a deadly moving drone. It is not spawned here: it needs feel
        // tuning on a real device, and a lethal hazard the auto-player cannot read would
        // make runs unwinnable rather than harder. It remains authored-chunk-only.
        private enum HazardKind { Low, Overhead, Pit, Falling, Stumble }

        private readonly List<GameObject> spawned = new();
        private readonly HazardKind[] bag = new HazardKind[5];
        private Transform fallbackRoot;
        private PlayerController player;
        private Camera gameplayCamera;
        private StaticObstacle staticTemplate;
        private BounceObstacle bounceTemplate;
        private CoinPickup coinTemplate;
        private CoinManager coinManager;
        private GeneratedArtCatalog art;
        private float nextSpawnX;
        private float nextSpecialX;
        private float nextBoostX;
        private float nextCheckpointX;
        private int checkpointIndex;
        private int districtCursor;
        private int sequence;
        private System.Random random;
        private bool configured;
        private int lastSpecialInterval = 65;
        private int lastBoostInterval = 110;
        private Sprite flatSprite;
        private Texture2D flatTexture;

        /// <summary>0..1 difficulty progress for a run distance, matching DifficultyManager levels 0-10.</summary>
        public static float DifficultyProgress(float distanceMeters)
        {
            return Mathf.Clamp01(distanceMeters / (Constants.DIFFICULTY_STEP_DISTANCE * 10f));
        }

        /// <summary>
        /// Near/far hazard spacing for a run distance. Tightens from 9.5-13.5 at the
        /// start of a run to 6.2-9.0 at maximum difficulty, so a long run gets harder.
        /// </summary>
        public static Vector2 SpacingRange(float distanceMeters)
        {
            var t = DifficultyProgress(distanceMeters);
            return new Vector2(Mathf.Lerp(9.5f, 6.2f, t), Mathf.Lerp(13.5f, 9f, t));
        }

        /// <summary>Metres between special coins. Rarer late so a long run still pays more per metre.</summary>
        public static float SpecialCoinInterval(float distanceMeters) =>
            Mathf.Lerp(65f, 40f, DifficultyProgress(distanceMeters));

        /// <summary>Metres between shield boosters.</summary>
        public static float BoostInterval(float distanceMeters) =>
            Mathf.Lerp(110f, 70f, DifficultyProgress(distanceMeters));

        public void Configure(Transform root, PlayerController runner, Camera camera,
            StaticObstacle staticObstacle, BounceObstacle bounceObstacle,
            CoinPickup firstCoin,
            GeneratedArtCatalog catalog = null)
        {
            fallbackRoot = root;
            player = runner;
            gameplayCamera = camera;
            staticTemplate = staticObstacle;
            bounceTemplate = bounceObstacle;
            coinTemplate = firstCoin;
            coinManager = runner != null ? runner.GetComponent<CoinManager>() : null;
            art = catalog;
            random = new System.Random(unchecked(Environment.TickCount ^ runner.GetInstanceID()));
            sequence = 0;
            // Seeded from the clock, so log it: a bug report needs a replayable route.
            Debug.Log($"[FallbackObstacleSpawner] seed={random.Next()} startX={runner.DistanceMeters:0.00}");
            nextSpawnX = Mathf.Max(27f, runner.DistanceMeters + 27f);
            nextSpecialX = 60f;
            nextBoostX = 105f;
            nextCheckpointX = runner.DistanceMeters + Constants.CHECKPOINT_INTERVAL;
            configured = fallbackRoot != null && player != null && gameplayCamera != null &&
                         staticTemplate != null && coinTemplate != null;
            if (!configured) return;
            var visual = coinTemplate.GetComponentInChildren<SpriteRenderer>(true);
            if (visual != null)
            {
                // The painted cat is offset below its physics trigger to sit on
                // the road. Keep coin triggers where the cat can collect them,
                // while drawing their rings in the same visible lane.
                visual.transform.localPosition = new Vector3(0f, -2.8f, 0f);
                visual.transform.localScale = Vector3.one * 0.36f;
                if (art != null && art.coinFrames != null && art.coinFrames.Length >= 2)
                    visual.GetComponent<GeneratedSpriteLoop>()?.Configure(
                        new[] { art.coinFrames[0], art.coinFrames[1] }, 4f);
            }
            for (var x = 2.4f; x <= 7.2f; x += 1.2f)
                SpawnCoin(x, 0.45f, false);
            SpawnCoin(11.1f, 1.4f, false);
            SpawnCoin(12f, 1.65f, false);
            SpawnCoin(12.9f, 1.4f, false);
            ShuffleBag();
        }

        private void Update()
        {
            if (!configured || fallbackRoot == null || !fallbackRoot.gameObject.activeInHierarchy ||
                player == null || gameplayCamera == null)
                return;

            var visibleAhead = gameplayCamera.orthographicSize * gameplayCamera.aspect + 18f;
            var spawnThrough = player.DistanceMeters + visibleAhead;
            var safety = 0;
            while (nextSpawnX <= spawnThrough && safety++ < 8)
                SpawnNext();
            while (nextCheckpointX <= spawnThrough)
            {
                SpawnCheckpoint(nextCheckpointX);
                nextCheckpointX += Constants.CHECKPOINT_INTERVAL;
            }
            // Re-anchor the reward cursors when the interval shrinks, so a tightening
            // curve never stacks two rewards on the same metre.
            var specialInterval = Mathf.RoundToInt(SpecialCoinInterval(player.DistanceMeters));
            var boostInterval = Mathf.RoundToInt(BoostInterval(player.DistanceMeters));
            if (specialInterval != lastSpecialInterval)
            {
                nextSpecialX += specialInterval - lastSpecialInterval;
                lastSpecialInterval = specialInterval;
            }
            if (boostInterval != lastBoostInterval)
            {
                nextBoostX += boostInterval - lastBoostInterval;
                lastBoostInterval = boostInterval;
            }
            while (nextSpecialX <= spawnThrough)
            {
                SpawnCoin(nextSpecialX, 1.35f, true);
                nextSpecialX += specialInterval;
            }
            while (nextBoostX <= spawnThrough)
            {
                SpawnBoost(nextBoostX);
                nextBoostX += boostInterval;
            }

            var recycleBefore = player.DistanceMeters - visibleAhead - 18f;
            for (var i = spawned.Count - 1; i >= 0; i--)
            {
                if (spawned[i] == null)
                {
                    spawned.RemoveAt(i);
                    continue;
                }
                if (spawned[i].transform.position.x < recycleBefore)
                {
                    Destroy(spawned[i]);
                    spawned.RemoveAt(i);
                }
            }
        }

        private void SpawnNext()
        {
            if (sequence > 0 && sequence % bag.Length == 0) ShuffleBag();
            var kind = bag[sequence % bag.Length];
            var x = nextSpawnX;

            // A checkpoint needs clear road around it so the delivery reads as a
            // breather and the player is never asked to jump into one. Coins only,
            // no hazard, and the bag cursor still advances so the skip does not make
            // one hazard type appear twice in a row.
            if (Mathf.Abs(x - nextCheckpointX) < CheckpointClearRadius)
            {
                sequence++;
                nextSpawnX += Mathf.Max(SpacingRange(player.DistanceMeters).x, 4.5f);
                SpawnCoin(x, 0.45f, false);
                SpawnCoin(x + 2.2f, 0.45f, false);
                return;
            }

            var spacing = Mathf.Lerp(SpacingRange(player.DistanceMeters).x,
                SpacingRange(player.DistanceMeters).y, (float)random.NextDouble());
            switch (kind)
            {
                case HazardKind.Low: SpawnLow(x, false); break;
                case HazardKind.Overhead: SpawnOverhead(x); break;
                case HazardKind.Pit: SpawnPit(x); break;
                case HazardKind.Falling: SpawnLow(x, true); break;
                case HazardKind.Stumble: SpawnStumble(x); break;
            }
            if (kind == HazardKind.Overhead)
            {
                SpawnCoin(x - 0.75f, 0.3f, false);
                SpawnCoin(x + 0.8f, 0.3f, false);
            }
            else
            {
                SpawnCoin(x - 0.9f, 1.35f, false);
                SpawnCoin(x, 1.6f, false);
                SpawnCoin(x + 0.9f, 1.35f, false);
            }
            // Leave a ground-level trail between hazards so every part of the
            // run has collectible coins, including when the hazard coins are missed.
            for (var offset = 3f; offset < spacing - 2f; offset += 1.2f)
            {
                if (offset < 5f || random.NextDouble() < 0.8)
                    SpawnCoin(x + offset, 0.45f, false);
            }
            nextSpawnX += spacing;
            sequence++;
        }

        private void ShuffleBag()
        {
            for (var i = 0; i < bag.Length; i++) bag[i] = (HazardKind)i;
            for (var i = bag.Length - 1; i > 0; i--)
            {
                var j = random.Next(i + 1);
                (bag[i], bag[j]) = (bag[j], bag[i]);
            }
        }

        private void SpawnLow(float x, bool falling)
        {
            var source = !falling && bounceTemplate != null && random.Next(100) < 15
                ? (ObstacleBase)bounceTemplate : staticTemplate;
            var landing = source.transform.position;
            landing.x = x;
            var clone = Instantiate(source.gameObject, landing, source.transform.rotation, fallbackRoot);
            clone.name = falling ? "Falling Road Hazard" : "Low Road Hazard";
            var visual = clone.GetComponentInChildren<GeneratedSpriteLoop>(true);
            if (visual != null && art != null)
            {
                var frames = falling ? art.modernAcFrames :
                    random.Next(2) == 0 ? art.oldTownPaverFrames : art.naturePotFrames;
                if (frames != null && frames.Length > 0) visual.Configure(frames, 5f);
            }
            if (falling) clone.AddComponent<FallingRoadHazard>().Configure(player, landing.y);
            // A template may have been disabled during an earlier encounter.
            clone.SetActive(true);
            spawned.Add(clone);
        }

        /// <summary>
        /// Underside of an overhead hazard's collider, in world Y.
        ///
        /// The cat stands 1.0 tall with its transform at y=0.4, so its head reaches 0.9 and
        /// a slide drops it to 0.3. This sits between the two: a running cat must duck, a
        /// sliding cat passes.
        /// </summary>
        public const float OverheadHazardBottom = 0.75f;

        /// <summary>
        /// Top of an overhead hazard's collider. A single jump peaks at 0.4 + 14^2/64 =
        /// 3.46, and a double jump at about 5.35, so a collider ending here cannot be
        /// jumped over at any upgrade level. Without this the drone behaved exactly like
        /// cargo: the only way past it was a jump.
        /// </summary>
        public const float OverheadHazardTop = 7f;

        private void SpawnOverhead(float x)
        {
            var hazard = new GameObject("Slide Under Drone");
            hazard.transform.SetParent(fallbackRoot, false);
            hazard.transform.position = new Vector3(x, OverheadHazardBottom, 0f);
            var obstacle = hazard.AddComponent<StaticObstacle>();
            obstacle.requiresDuckUnder = true;

            // The collider is a curtain hanging from the drone up into the sky, not a box
            // around its painted body. The art stays at the hazard's own height; only the
            // trigger is tall, so the drone reads as something you duck under and cannot
            // leap over.
            var hitbox = hazard.AddComponent<BoxCollider2D>();
            hitbox.isTrigger = true;
            var height = OverheadHazardTop - OverheadHazardBottom;
            hitbox.size = new Vector2(1.15f, height);
            hitbox.offset = new Vector2(0f, height * 0.5f);

            AddLoop(hazard.transform, "Drone Art", art?.modernDroneFrames, 0.2f, 46);
            spawned.Add(hazard);
        }

        private void SpawnPit(float x)
        {
            var pit = new GameObject("Jump Road Pit");
            pit.transform.SetParent(fallbackRoot, false);
            pit.transform.position = new Vector3(x, -0.05f, 0f);
            pit.AddComponent<PitHazard>();
            var hitbox = pit.AddComponent<BoxCollider2D>();
            hitbox.isTrigger = true;
            hitbox.size = new Vector2(1.5f, 0.7f);
            AddPitArt(pit.transform);
            spawned.Add(pit);
        }

        /// <summary>Metres of clear road either side of a checkpoint.</summary>
        public const float CheckpointClearRadius = 7f;

        /// <summary>
        /// A delivery point. Without these the shipped route never delivered a parcel,
        /// so package scoring, the story cards and district progression were all
        /// unreachable at runtime.
        /// </summary>
        private void SpawnCheckpoint(float x)
        {
            var marker = new GameObject("Route Checkpoint");
            marker.transform.SetParent(fallbackRoot, false);
            marker.transform.position = new Vector3(x, 0f, 0f);
            var trigger = marker.AddComponent<BoxCollider2D>();
            trigger.isTrigger = true;
            trigger.size = new Vector2(0.9f, 3.2f);

            var index = ++checkpointIndex;
            var district = NextCheckpointDistrict();
            var checkpoint = marker.AddComponent<CheckpointMarker>();
            checkpoint.Reached += _ => OnCheckpointReached(district, index);
            AddLoop(marker.transform, "Checkpoint Art", art?.checkpointFrames, 0.5f, 47);
            spawned.Add(marker);
        }

        /// <summary>
        /// Rotates through the districts the player has actually unlocked, so a long run
        /// walks from Old Town toward Downtown rather than staying put.
        /// </summary>
        private DistrictId NextCheckpointDistrict()
        {
            var eligible = DistrictUnlockService.GetEligible(player.DistanceMeters, false);
            if (eligible == null || eligible.Length == 0)
            {
                return DistrictId.OldTown;
            }

            return eligible[Mathf.Min(districtCursor, eligible.Length - 1)];
        }

        private void OnCheckpointReached(DistrictId district, int index)
        {
            // RunCoordinator.HandleCheckpointReached already raises the story beat, so
            // publishing here is the whole job. Raising it a second time from the
            // spawner would show two story cards for one delivery.
            var beat = StoryBeatService.GetFirstTimeBeat(district, index);
            chunkManager?.RaiseExternalCheckpoint(district, index, beat);

            districtCursor = Mathf.Min(districtCursor + 1, Mathf.Max(0, eligibleCount() - 1));
        }

        private int eligibleCount()
        {
            var eligible = DistrictUnlockService.GetEligible(player.DistanceMeters, false);
            return eligible?.Length ?? 0;
        }

        /// <summary>
        /// A non-lethal slowdown. Previously StumbleObstacle existed but was in no scene
        /// and spawned by nothing, so its speed bug was unreachable and the obstacle type
        /// never appeared in a run.
        /// </summary>
        private void SpawnStumble(float x)
        {
            var hazard = new GameObject("Loose Paving Stone");
            hazard.transform.SetParent(fallbackRoot, false);
            hazard.transform.position = new Vector3(x, 0.42f, 0f);
            hazard.AddComponent<StumbleObstacle>();
            var hitbox = hazard.AddComponent<BoxCollider2D>();
            hitbox.isTrigger = true;
            hitbox.size = new Vector2(0.7f, 0.35f);
            AddLoop(hazard.transform, "Stumble Art", art?.oldTownPaverFrames, 0.22f, 45);
            spawned.Add(hazard);
        }

        private void SpawnCoin(float x, float y, bool special)
        {
            if (coinTemplate == null) return;
            var clone = Instantiate(coinTemplate.gameObject, new Vector3(x, y, 0f),
                coinTemplate.transform.rotation, fallbackRoot);
            clone.name = special ? "Special Coin" : "Route Coin";
            clone.GetComponent<CoinPickup>().Configure(coinManager,
                special ? Constants.COIN_RARE_VALUE : Constants.COIN_BASE_VALUE, player);
            var visual = clone.GetComponentInChildren<SpriteRenderer>(true);
            if (visual != null)
            {
                visual.transform.localScale = Vector3.one * (special ? 0.48f : 0.36f);
                visual.color = special ? new Color(0.55f, 1f, 0.95f) : Color.white;
            }
            // Unity copies the template's active state. The original scene coin
            // disables itself when collected, but future route coins must appear.
            clone.SetActive(true);
            spawned.Add(clone);
        }

        private void SpawnBoost(float x)
        {
            var boost = new GameObject("Shield Booster");
            boost.transform.SetParent(fallbackRoot, false);
            boost.transform.position = new Vector3(x, 1.1f, 0f);
            boost.AddComponent<BoostPickup>();
            var trigger = boost.AddComponent<CircleCollider2D>();
            trigger.isTrigger = true;
            trigger.radius = 0.36f;
            var visual = AddLoop(boost.transform, "Shield Art", art?.coinFrames, 0.11f, 53);
            if (visual != null) visual.color = new Color(0.45f, 0.9f, 1f);
            spawned.Add(boost);
        }

        private SpriteRenderer AddLoop(Transform parent, string name, Sprite[] frames, float scale, int sortingOrder)
        {
            if (frames == null || frames.Length == 0) return null;
            var visual = new GameObject(name);
            visual.transform.SetParent(parent, false);
            visual.transform.localScale = Vector3.one * scale;
            var renderer = visual.AddComponent<SpriteRenderer>();
            renderer.sortingOrder = sortingOrder;
            visual.AddComponent<GeneratedSpriteLoop>().Configure(frames, 6f);
            return renderer;
        }

        private void AddPitArt(Transform parent)
        {
            if (flatSprite == null)
            {
                const int width = 128;
                const int height = 64;
                flatTexture = new Texture2D(width, height, TextureFormat.RGBA32, false);
                var pixels = new Color[width * height];
                for (var y = 0; y < height; y++)
                for (var x = 0; x < width; x++)
                {
                    var dx = (x + 0.5f - width * 0.5f) / (width * 0.5f);
                    var dy = (y + 0.5f - height * 0.5f) / (height * 0.5f);
                    var radius = Mathf.Sqrt(dx * dx + dy * dy);
                    var rim = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.68f, 0.98f, radius));
                    var color = Color.Lerp(new Color(0.10f, 0.075f, 0.07f),
                        new Color(0.46f, 0.36f, 0.28f), rim);
                    color.a = Mathf.Clamp01((1f - radius) * 9f);
                    pixels[y * width + x] = color;
                }
                flatTexture.SetPixels(pixels);
                flatTexture.Apply();
                flatSprite = Sprite.Create(flatTexture, new Rect(0f, 0f, width, height),
                    new Vector2(0.5f, 0.5f), width);
            }
            var piece = new GameObject("Shaded Road Pit");
            piece.transform.SetParent(parent, false);
            piece.transform.localPosition = new Vector3(0f, -0.4f, 0f);
            piece.transform.localScale = new Vector3(1.7f, 1.2f, 1f);
            var renderer = piece.AddComponent<SpriteRenderer>();
            renderer.sprite = flatSprite;
            renderer.sortingOrder = 44;
        }

        private void OnDestroy()
        {
            if (flatSprite != null) Destroy(flatSprite);
            if (flatTexture != null) Destroy(flatTexture);
        }
    }
}
