using CatCourier.Core;
using CatCourier.Generation;
using CatCourier.Obstacles;
using CatCourier.Player;
using CatCourier.Weather;
using UnityEngine;

namespace CatCourier.Art
{
    // Visual-only scenery. Ground and gap colliders remain owned by gameplay chunks.
    public sealed class GeneratedBackdrop : MonoBehaviour
    {
        [SerializeField] private GeneratedArtCatalog catalog;
        [SerializeField] private Camera gameplayCamera;
        [SerializeField] private PlayerController player;
        [SerializeField] private ChunkManager chunks;
        [SerializeField] private WeatherManager weather;
        [SerializeField] private GameObject fallbackContent;

        private SpriteRenderer[] sky;
        private SpriteRenderer[] rainSky;
        private SpriteRenderer[,] buildings;
        private SpriteRenderer[,] buildingTransitions;
        private SpriteRenderer[,] road;
        private SpriteRenderer[,] scenicTransitions;
        private SpriteRenderer cloud;
        private SpriteRenderer weatherOverlay;
        private GroundSurface fallbackGround;
        private FallbackObstacleSpawner fallbackObstacleSpawner;
        private Material scenicFeatherMaterial;
        private float rainSkyBlend;
        private float weatherOverlayBlend;
        public float TimePosition { get; private set; }

        public void Configure(GeneratedArtCatalog art, Camera camera, PlayerController runner, GameObject fallback)
        {
            catalog = art;
            gameplayCamera = camera;
            player = runner;
            fallbackContent = fallback;
            ConfigureFallbackGround();
        }

        private void Awake()
        {
            gameplayCamera ??= Camera.main;
            player ??= FindObjectOfType<PlayerController>();
            chunks ??= FindObjectOfType<ChunkManager>();
            weather ??= FindObjectOfType<WeatherManager>();
            ConfigureFallbackGround();
            sky = new[] { Create("Sky From", -100), Create("Sky To", -99) };
            rainSky = new[] { Create("Rain Sky From", -98), Create("Rain Sky To", -97) };
            buildings = new SpriteRenderer[3, 2];
            buildingTransitions = new SpriteRenderer[3, 2];
            road = new SpriteRenderer[3, 2];
            for (var i = 0; i < 3; i++)
            {
                buildings[i, 0] = Create("Town " + i + " From", -40);
                buildings[i, 1] = Create("Town " + i + " To", -39);
                buildingTransitions[i, 0] = Create("Town Transition " + i + " From", -38);
                buildingTransitions[i, 1] = Create("Town Transition " + i + " To", -37);
                road[i, 0] = Create("Road " + i + " From", 20);
                road[i, 1] = Create("Road " + i + " To", 21);
            }
            scenicTransitions = new SpriteRenderer[2, 2];
            var featherShader = Shader.Find("CatCourier/ScenicFeather");
            if (featherShader != null)
                scenicFeatherMaterial = new Material(featherShader);
            for (var i = 0; i < 2; i++)
            {
                scenicTransitions[i, 0] = Create("Scenic Transition " + i + " From", 30);
                scenicTransitions[i, 1] = Create("Scenic Transition " + i + " To", 31);
                if (scenicFeatherMaterial != null)
                {
                    scenicTransitions[i, 0].sharedMaterial = scenicFeatherMaterial;
                    scenicTransitions[i, 1].sharedMaterial = scenicFeatherMaterial;
                }
            }
            cloud = Create("Moving Cloud", -60);
            weatherOverlay = Create("Weather Overlay", 40);
            weatherOverlay.color = new Color(1f, 1f, 1f, 0.33f);
        }

        private void OnDestroy()
        {
            if (scenicFeatherMaterial != null)
                Destroy(scenicFeatherMaterial);
        }

        private SpriteRenderer Create(string name, int order)
        {
            var child = new GameObject(name);
            child.transform.SetParent(transform, false);
            var renderer = child.AddComponent<SpriteRenderer>();
            renderer.sortingOrder = order;
            return renderer;
        }

        private void ConfigureFallbackGround()
        {
            fallbackGround = fallbackContent != null
                ? fallbackContent.GetComponentInChildren<GroundSurface>(true)
                : null;
            fallbackGround?.Follow(player != null ? player.transform : null);

            if (fallbackContent == null || player == null)
                return;

            fallbackObstacleSpawner = GetComponent<FallbackObstacleSpawner>();
            if (fallbackObstacleSpawner == null)
                fallbackObstacleSpawner = gameObject.AddComponent<FallbackObstacleSpawner>();
            fallbackObstacleSpawner.Configure(
                fallbackContent.transform,
                player,
                gameplayCamera,
                fallbackContent.GetComponentInChildren<StaticObstacle>(true),
                fallbackContent.GetComponentInChildren<BounceObstacle>(true),
                fallbackContent.GetComponentInChildren<CatCourier.Coins.CoinPickup>(true), catalog);
        }

        private void LateUpdate()
        {
            if (catalog == null || gameplayCamera == null || player == null) return;
            var targetTime = weather != null && weather.IsNight ? 3f : Mathf.Clamp(player.DistanceMeters / 1200f, 0f, 3f);
            // Keep weather forced-night transitions gradual, while route distance
            // advances the normal time-of-day palette at a much slower pace.
            TimePosition = Mathf.MoveTowards(TimePosition, targetTime, Time.deltaTime * 0.06f);
            // The four lighting renders were painted separately. Blending their
            // pixels doubles windows and rooflines, so show one coherent frame.
            var from = Mathf.Clamp(Mathf.RoundToInt(TimePosition), 0, 3);
            var to = from;
            const float blend = 0f;
            var cameraX = gameplayCamera.transform.position.x;
            UpdateSky(from, to, blend);
            UpdateBuildings(from, to, blend);
            UpdateRoad(from, to, blend);
            UpdateScenicTransitions(from, to, blend, cameraX);
            UpdateCloud();
            UpdateWeather();
        }

        private void UpdateSky(int from, int to, float blend)
        {
            rainSkyBlend = Mathf.MoveTowards(rainSkyBlend,
                weather != null && weather.Current == WeatherType.Rain && catalog.rainySky.Length == 4 ? 1f : 0f,
                Time.deltaTime * 0.4f);
            var center = gameplayCamera.transform.position;
            for (var i = 0; i < 2; i++)
            {
                var paletteAlpha = i == 0 ? 1f : blend;
                SetSkyLayer(sky[i], catalog.clearSky, i == 0 ? from : to,
                    paletteAlpha * (1f - rainSkyBlend), center);
                SetSkyLayer(rainSky[i], catalog.rainySky, i == 0 ? from : to,
                    paletteAlpha * rainSkyBlend, center);
            }
        }

        private void SetSkyLayer(SpriteRenderer renderer, Sprite[] frames, int frame, float alpha, Vector3 center)
        {
            renderer.enabled = alpha > 0.001f && frames != null && frames.Length > frame;
            if (!renderer.enabled) return;
            renderer.sprite = GeneratedArtCatalog.Frame(frames, frame);
            renderer.color = new Color(1f, 1f, 1f, alpha);
            renderer.transform.position = new Vector3(center.x, center.y, 0f);
            FitToView(renderer, 1.08f);
            CenterBoundsOn(renderer, center);
        }

        private void UpdateBuildings(int from, int to, float blend)
        {
            var cameraX = gameplayCamera.transform.position.x;
            const float width = 18f;
            var centerIndex = Mathf.FloorToInt(cameraX / width);
            for (var i = 0; i < 3; i++)
            {
                var tileIndex = centerIndex + i - 1;
                for (var palette = 0; palette < 2; palette++)
                {
                    var renderer = buildings[i, palette];
                    renderer.sprite = RouteSprite(tileIndex, palette == 0 ? from : to, false);
                    var paletteAlpha = palette == 0 ? 1f : blend;
                    var transitionBlend = RouteTransitionBlend(tileIndex, cameraX);
                    renderer.color = new Color(1f, 1f, 1f, paletteAlpha * (1f - transitionBlend));
                    renderer.transform.position = new Vector3((tileIndex + 0.5f) * width, -0.3f, 0f);
                    FitWidth(renderer, width);

                    var transition = buildingTransitions[i, palette];
                    transition.sprite = RouteTransitionSprite(tileIndex, palette == 0 ? from : to);
                    transition.enabled = transition.sprite != null && transitionBlend > 0.001f;
                    transition.color = new Color(1f, 1f, 1f, paletteAlpha * transitionBlend);
                    transition.transform.position = renderer.transform.position;
                    FitWidth(transition, width);
                }
            }
        }

        private float RouteTransitionBlend(int tile, float cameraX)
        {
            if (chunks != null && chunks.CurrentDistrict == DistrictId.Downtown)
                return 0f;

            const float width = 18f;
            var start = tile switch
            {
                1 => width,
                3 => width * 3f,
                _ => 0f
            };
            if (tile != 1 && tile != 3)
                return 0f;

            return Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(start, start + width, cameraX));
        }

        private Sprite RouteTransitionSprite(int tile, int time)
        {
            if (tile == 1)
                return GeneratedArtCatalog.Frame(catalog.natureBlocksByTime,
                    (time + tile % Mathf.Max(1, catalog.natureBlockCount) * 4));
            if (tile == 3)
                return Block(catalog.District(DistrictId.Downtown), tile, time);
            return null;
        }

        private void UpdateRoad(int from, int to, float blend)
        {
            // Generated chunks own their road and gap visuals once authored chunk
            // prefabs exist. The fallback road art is faded beneath full-scene
            // transition panoramas to avoid visible tile seams.
            var visible = fallbackContent == null || fallbackContent.activeInHierarchy;
            var cameraX = gameplayCamera.transform.position.x;
            const float width = 18f;
            var centerIndex = Mathf.FloorToInt(cameraX / width);
            for (var i = 0; i < 3; i++)
            {
                var tileIndex = centerIndex + i - 1;
                for (var palette = 0; palette < 2; palette++)
                {
                    var renderer = road[i, palette];
                    renderer.enabled = visible;
                    if (!renderer.enabled) continue;
                    renderer.sprite = RouteSprite(tileIndex, palette == 0 ? from : to, true);
                    renderer.color = new Color(1f, 1f, 1f, palette == 0 ? 1f : blend);
                    // Move the sidewalk up to meet the town ground line, closing
                    // the exposed flat gameplay strip between the two images.
                    renderer.transform.position = new Vector3((tileIndex + 0.5f) * width, -2.2f, 0f);
                    FitWidth(renderer, width);
                }
            }
        }

        private void UpdateScenicTransitions(int from, int to, float paletteBlend, float cameraX)
        {
            for (var transitionIndex = 0; transitionIndex < 2; transitionIndex++)
            {
                var frames = transitionIndex == 0
                    ? catalog.oldNatureTransitionByTime
                    : catalog.natureModernTransitionByTime;
                // Each panorama occupies a fixed section of the world. Camera
                // motion reveals it from right to left instead of fading an
                // entire scene over another scene in screen space.
                // A short overlap lets the next panel fade in while this one
                // remains opaque, so the district tiles cannot ghost through.
                var sectionWidth = transitionIndex == 0 ? 47f : 45f;
                var startX = transitionIndex == 0 ? 15f : 60f;
                var renderer = scenicTransitions[transitionIndex, 0];
                scenicTransitions[transitionIndex, 1].enabled = false;
                renderer.sprite = GeneratedArtCatalog.Frame(frames, from);
                if (renderer.sprite == null)
                {
                    renderer.enabled = false;
                    continue;
                }
                renderer.transform.localScale = new Vector3(
                    sectionWidth / renderer.sprite.bounds.size.x,
                    gameplayCamera.orthographicSize * 2f / renderer.sprite.bounds.size.y,
                    1f);
                var center = new Vector3(startX + sectionWidth * 0.5f,
                    gameplayCamera.transform.position.y, 0f);
                renderer.transform.position = center;
                CenterBoundsOn(renderer, center);
                var viewHalfWidth = gameplayCamera.orthographicSize * gameplayCamera.aspect;
                renderer.enabled = cameraX + viewHalfWidth > startX &&
                    cameraX - viewHalfWidth < startX + sectionWidth;
                if (!renderer.enabled) continue;

                renderer.color = Color.white;
            }
        }

        private Sprite RouteSprite(int tile, int time, bool isRoad)
        {
            var district = chunks != null ? chunks.CurrentDistrict : DistrictId.OldTown;
            if (district == DistrictId.Downtown)
            {
                var modern = catalog.District(DistrictId.Downtown);
                return isRoad ? GeneratedArtCatalog.Frame(modern?.roadByTime, time)
                    : Block(modern, tile, time);
            }
            var oldTown = catalog.District(DistrictId.OldTown);
            var modernTown = catalog.District(DistrictId.Downtown);
            if (tile <= 0) return isRoad ? GeneratedArtCatalog.Frame(oldTown?.roadByTime, time) : Block(oldTown, tile, time);
            // The transition background images are complete panoramas (sky,
            // buildings and ground). Drawing one as a narrow building tile
            // exposes its sky as a colored vertical slab at the tile edge.
            // Keep the transitions on the road layer and use transparent
            // district blocks for the buildings so the global sky stays seamless.
            if (tile == 1) return isRoad ? GeneratedArtCatalog.Frame(catalog.oldNatureRoadByTime, time)
                : Block(oldTown, tile, time);
            if (tile == 2) return isRoad ? GeneratedArtCatalog.Frame(catalog.natureRoadByTime, time)
                : GeneratedArtCatalog.Frame(catalog.natureBlocksByTime, (time + Mathf.Abs(tile) % Mathf.Max(1, catalog.natureBlockCount) * 4));
            if (tile == 3) return isRoad ? GeneratedArtCatalog.Frame(catalog.natureModernRoadByTime, time)
                : GeneratedArtCatalog.Frame(catalog.natureBlocksByTime,
                    (time + Mathf.Abs(tile) % Mathf.Max(1, catalog.natureBlockCount) * 4));
            return isRoad ? GeneratedArtCatalog.Frame(modernTown?.roadByTime, time) : Block(modernTown, tile, time);
        }

        private static Sprite Block(DistrictArt set, int tile, int time)
        {
            if (set == null || set.blockCount == 0) return null;
            return GeneratedArtCatalog.Frame(set.blocksByTime, Mathf.Abs(tile) % set.blockCount * 4 + time);
        }

        private void UpdateCloud()
        {
            if (catalog.cloudDay.Length == 0) { cloud.enabled = false; return; }
            cloud.enabled = true;
            cloud.sprite = catalog.cloudDay[0];
            cloud.transform.localScale = Vector3.one * 0.5f;
            var span = gameplayCamera.orthographicSize * gameplayCamera.aspect * 2f;
            var cloudWidth = cloud.sprite != null
                ? cloud.sprite.bounds.size.x * Mathf.Abs(cloud.transform.lossyScale.x) : 0f;
            var travel = span + cloudWidth;
            var drift = Mathf.Repeat(Time.time * 0.35f, travel) - travel * 0.5f;
            cloud.transform.position = new Vector3(gameplayCamera.transform.position.x + drift,
                gameplayCamera.transform.position.y + gameplayCamera.orthographicSize * 0.53f, 0f);
            cloud.color = Color.Lerp(Color.white, new Color(0.7f, 0.79f, 1f), TimePosition / 3f);
        }

        private void UpdateWeather()
        {
            Sprite[] frames = null;
            if (weather != null)
            {
                switch (weather.Current)
                {
                    case WeatherType.Rain: frames = catalog.rainFrames; break;
                    case WeatherType.Wind: frames = catalog.windFrames; break;
                }
            }
            var hasWeatherFrames = frames != null && frames.Length > 0;
            weatherOverlayBlend = Mathf.MoveTowards(weatherOverlayBlend,
                hasWeatherFrames ? 1f : 0f, Time.deltaTime * 0.4f);
            weatherOverlay.enabled = weatherOverlayBlend > 0.001f;
            if (!weatherOverlay.enabled) return;
            if (hasWeatherFrames)
                weatherOverlay.sprite = frames[Mathf.FloorToInt(Time.time * 7f) % frames.Length];
            weatherOverlay.color = new Color(1f, 1f, 1f, 0.33f * weatherOverlayBlend);
            weatherOverlay.transform.position = new Vector3(gameplayCamera.transform.position.x,
                gameplayCamera.transform.position.y, 0f);
            FitToView(weatherOverlay, 1.1f);
            CenterBoundsOn(weatherOverlay, gameplayCamera.transform.position);
        }

        private static void FitWidth(SpriteRenderer renderer, float width)
        {
            if (renderer.sprite == null) return;
            var scale = width / renderer.sprite.bounds.size.x;
            renderer.transform.localScale = new Vector3(scale, scale, 1f);
        }

        private void FitToView(SpriteRenderer renderer, float margin)
        {
            if (renderer.sprite == null) return;
            var vertical = gameplayCamera.orthographicSize * 2f * margin;
            var horizontal = vertical * gameplayCamera.aspect;
            var scale = Mathf.Max(horizontal / renderer.sprite.bounds.size.x, vertical / renderer.sprite.bounds.size.y);
            renderer.transform.localScale = new Vector3(scale, scale, 1f);
        }

        private static void CenterBoundsOn(SpriteRenderer renderer, Vector3 center)
        {
            // Generated sprites use bottom pivots for ground alignment. Full
            // screen layers must instead center their visible bounds on camera.
            var boundsCenter = renderer.bounds.center;
            renderer.transform.position += new Vector3(center.x - boundsCenter.x,
                center.y - boundsCenter.y, 0f);
        }
    }
}
