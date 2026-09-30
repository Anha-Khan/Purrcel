using System;
using System.Collections.Generic;
using CatCourier.Coins;
using CatCourier.Core;
using CatCourier.Obstacles;
using CatCourier.Player;
using UnityEngine;

namespace CatCourier.Art
{
    /// <summary>Streams a playable route while the authored chunk catalog is empty.</summary>
    public sealed class FallbackObstacleSpawner : MonoBehaviour
    {
        private enum HazardKind { Low, Overhead, Pit, Falling }

        private readonly List<GameObject> spawned = new();
        private readonly HazardKind[] bag = new HazardKind[4];
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
        private int sequence;
        private System.Random random;
        private bool configured;
        private Sprite flatSprite;
        private Texture2D flatTexture;

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
            nextSpawnX = Mathf.Max(27f, runner.DistanceMeters + 27f);
            nextSpecialX = 60f;
            nextBoostX = 105f;
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
            while (nextSpecialX <= spawnThrough)
            {
                SpawnCoin(nextSpecialX, 1.35f, true);
                nextSpecialX += 65f;
            }
            while (nextBoostX <= spawnThrough)
            {
                SpawnBoost(nextBoostX);
                nextBoostX += 110f;
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
            switch (kind)
            {
                case HazardKind.Low: SpawnLow(x, false); break;
                case HazardKind.Overhead: SpawnOverhead(x); break;
                case HazardKind.Pit: SpawnPit(x); break;
                case HazardKind.Falling: SpawnLow(x, true); break;
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
            SpawnCoin(x + 3f, 0.45f, false);
            SpawnCoin(x + 4.2f, 0.45f, false);
            nextSpawnX += Mathf.Lerp(9.5f, 13.5f, (float)random.NextDouble());
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
            spawned.Add(clone);
        }

        private void SpawnOverhead(float x)
        {
            var hazard = new GameObject("Slide Under Drone");
            hazard.transform.SetParent(fallbackRoot, false);
            hazard.transform.position = new Vector3(x, 0.78f, 0f);
            hazard.AddComponent<StaticObstacle>();
            var hitbox = hazard.AddComponent<BoxCollider2D>();
            hitbox.isTrigger = true;
            hitbox.size = new Vector2(1.15f, 0.42f);
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
