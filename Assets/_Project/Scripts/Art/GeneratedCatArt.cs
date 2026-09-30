using CatCourier.Core;
using CatCourier.Packages;
using CatCourier.Player;
using CatCourier.Progression;
using UnityEngine;

namespace CatCourier.Art
{
    [RequireComponent(typeof(SpriteRenderer))]
    public sealed class GeneratedCatArt : MonoBehaviour
    {
        [SerializeField] private GeneratedArtCatalog catalog;
        [SerializeField] private PlayerController player;
        [SerializeField] private PackageManager packages;
        [SerializeField] private SpriteRenderer catRenderer;
        [SerializeField] private float runFramesPerSecond = 10f;
        [SerializeField] private float slideFramesPerSecond = 8f;

        // The first row contains the upright walk, stride and bound poses.
        // Frames 8 and 9 are crouches and made the cat appear to crawl.
        private static readonly int[] RunCycle = { 0, 1, 4, 1, 2, 4, 3, 1 };

        private CatBreedManager breeds;
        private CatSkinArt skin;
        private SpriteRenderer bundle;
        private SpriteRenderer[] birds;
        private string breedId;
        private float deathAt = -100f;
        private float bounceAt = -100f;
        private float finishUntil;
        private float initialScale;
        private Vector3 initialLocalPosition;
        private bool wasDead;
        private GeneratedBackdrop backdrop;

        public void Configure(GeneratedArtCatalog art, PlayerController runner)
        {
            catalog = art;
            player = runner;
            catRenderer = GetComponent<SpriteRenderer>();
            catRenderer.sprite = art != null ? GeneratedArtCatalog.Frame(art.Skin("tabby")?.actions, 0) : null;
        }

        private void Awake()
        {
            if (catRenderer == null) catRenderer = GetComponent<SpriteRenderer>();
            if (player == null) player = GetComponentInParent<PlayerController>();
            if (packages == null) packages = FindObjectOfType<PackageManager>();
            breeds = FindObjectOfType<CatBreedManager>();
            backdrop = FindObjectOfType<GeneratedBackdrop>();
            initialScale = transform.localScale.x;
            if (initialScale <= 0f) initialScale = 1f;
            initialLocalPosition = transform.localPosition;
            CreateBundleAndBirds();
            SelectBreed();
        }

        private void OnEnable()
        {
            if (player != null)
            {
                player.OnDeath += HandleDeath;
                player.OnWallBounce += HandleBounce;
            }
            if (packages != null) packages.OnPackageDelivered += HandleDelivery;
        }

        private void OnDisable()
        {
            if (player != null)
            {
                player.OnDeath -= HandleDeath;
                player.OnWallBounce -= HandleBounce;
            }
            if (packages != null) packages.OnPackageDelivered -= HandleDelivery;
        }

        private void CreateBundleAndBirds()
        {
            var bundleTransform = transform.Find("Carried Bindle");
            if (bundleTransform == null)
            {
                var child = new GameObject("Carried Bindle");
                child.transform.SetParent(transform, false);
                bundleTransform = child.transform;
            }
            bundleTransform.localPosition = new Vector3(-0.52f, 0.16f, 0f);
            bundleTransform.localScale = Vector3.one * 0.13f;
            bundle = GetOrAddRenderer(bundleTransform.gameObject);
            bundle.sortingOrder = catRenderer.sortingOrder + 1;
            bundle.enabled = false;

            birds = new SpriteRenderer[3];
            for (var i = 0; i < birds.Length; i++)
            {
                var birdTransform = transform.Find("Dizzy Bird " + i);
                if (birdTransform == null)
                {
                    var child = new GameObject("Dizzy Bird " + i);
                    child.transform.SetParent(transform, false);
                    birdTransform = child.transform;
                }
                birdTransform.localScale = Vector3.one * 0.04f;
                birds[i] = GetOrAddRenderer(birdTransform.gameObject);
                birds[i].sortingOrder = catRenderer.sortingOrder + 3;
                birds[i].enabled = false;
            }
        }

        private void SelectBreed()
        {
            var selected = breeds != null && breeds.ActiveBreed != null ? breeds.ActiveBreed.id : "tabby";
            if (selected == breedId && skin != null) return;
            breedId = selected;
            skin = catalog != null ? catalog.Skin(selected) : null;
        }

        private void HandleDeath()
        {
            deathAt = Time.unscaledTime;
            wasDead = true;
            if (bundle != null && bundle.enabled)
            {
                var dropped = new GameObject("Dropped Bindle");
                dropped.transform.position = bundle.transform.position;
                dropped.transform.localScale = bundle.transform.lossyScale;
                var renderer = dropped.AddComponent<SpriteRenderer>();
                renderer.sprite = GeneratedArtCatalog.Frame(catalog.Package(packages?.CurrentPackageType ?? PackageType.Normal)?.frames, 3);
                renderer.sortingOrder = catRenderer.sortingOrder + 1;
                Destroy(dropped, 2.5f);
            }
            if (bundle != null) bundle.enabled = false;
        }

        private void HandleBounce() => bounceAt = Time.time;
        private void HandleDelivery() => finishUntil = Time.time + 0.65f;

        private void Update()
        {
            if (catalog == null || player == null || catRenderer == null) return;
            SelectBreed();
            if (skin == null) return;
            if (player.State != PlayerState.Dead && wasDead)
            {
                wasDead = false;
                deathAt = -100f;
            }

            var elapsed = Time.time;
            Sprite sprite;
            if (player.State == PlayerState.Dead)
            {
                var since = Time.unscaledTime - deathAt;
                var index = since < 0.18f ? 3 : since < 1.12f ? 4 : 5;
                sprite = GeneratedArtCatalog.Frame(skin.reactions, index);
            }
            else if (Time.time < finishUntil)
            {
                sprite = GeneratedArtCatalog.Frame(skin.slideWallFinish, 5);
            }
            else if (player.State == PlayerState.Sliding)
            {
                sprite = GeneratedArtCatalog.Frame(skin.slideWallFinish, Mathf.FloorToInt(elapsed * slideFramesPerSecond) % 3);
            }
            else if (player.State == PlayerState.WallBounce)
            {
                sprite = GeneratedArtCatalog.Frame(skin.slideWallFinish, Time.time - bounceAt < 0.13f ? 3 : 4);
            }
            else if (player.State == PlayerState.Jumping)
            {
                var velocity = player.VerticalVelocity;
                sprite = GeneratedArtCatalog.Frame(skin.actions, velocity > 4f ? 6 : velocity > 0f ? 7 : 3);
            }
            else
            {
                var frame = RunCycle[Mathf.FloorToInt(player.RunTime * runFramesPerSecond) % RunCycle.Length];
                sprite = GeneratedArtCatalog.Frame(skin.actions, frame);
            }
            if (sprite != null) catRenderer.sprite = sprite;
            transform.localScale = Vector3.one * initialScale;
            var bob = player.State == PlayerState.Running
                ? Mathf.Abs(Mathf.Sin(player.RunTime * runFramesPerSecond * Mathf.PI)) * 0.035f : 0f;
            transform.localPosition = initialLocalPosition + Vector3.up * bob;
            UpdateBundle();
            UpdateBirds();
            Tint();
        }

        private void UpdateBundle()
        {
            if (bundle == null || packages == null || player.State == PlayerState.Dead) return;
            var frames = catalog.Package(packages.CurrentPackageType ?? PackageType.Normal)?.frames;
            bundle.enabled = packages.HasAssignedPackage && frames != null && frames.Length >= 3;
            if (!bundle.enabled) return;
            var phase = Mathf.Sin(Time.time * 8f);
            bundle.sprite = GeneratedArtCatalog.Frame(frames, phase >= 0f ? 1 : 2);
            bundle.transform.localRotation = Quaternion.Euler(0f, 0f, phase * 6f);
        }

        private void UpdateBirds()
        {
            if (birds == null) return;
            var since = Time.unscaledTime - deathAt;
            var active = player.State == PlayerState.Dead && since >= 0.18f && since < 1.12f;
            for (var i = 0; i < birds.Length; i++)
            {
                birds[i].enabled = active;
                if (!active) continue;
                var angle = 2f * Mathf.PI * (since * 1.1f + i / (float)birds.Length);
                birds[i].transform.localPosition = new Vector3(Mathf.Cos(angle) * 0.39f, 1.02f + Mathf.Sin(angle) * 0.13f, 0f);
                birds[i].sprite = GeneratedArtCatalog.Frame(catalog.birdFrames,
                    (Mathf.FloorToInt(since * 9f) + i) % Mathf.Max(1, catalog.birdFrames.Length));
            }
        }

        private void Tint()
        {
            var phase = backdrop != null ? backdrop.TimePosition : 0f;
            var from = Light(Mathf.FloorToInt(phase));
            var to = Light(Mathf.Min(3, Mathf.FloorToInt(phase) + 1));
            var color = Color.Lerp(from, to, phase - Mathf.Floor(phase));
            catRenderer.color = color;
            if (bundle != null) bundle.color = color;
            if (birds != null) foreach (var bird in birds) bird.color = color;
        }

        private static Color Light(int index)
        {
            switch (index)
            {
                case 1: return new Color(1f, 0.89f, 0.78f);
                case 2: return new Color(0.91f, 0.91f, 1f);
                case 3: return new Color(0.93f, 0.97f, 1f);
                default: return Color.white;
            }
        }

        private static SpriteRenderer GetOrAddRenderer(GameObject host)
        {
            var existing = host.GetComponent<SpriteRenderer>();
            return existing != null ? existing : host.AddComponent<SpriteRenderer>();
        }
    }
}
