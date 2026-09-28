using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using CatCourier.Audio;
using CatCourier.Core;
using CatCourier.Generation;
using CatCourier.Monetization;
using CatCourier.Player;
using NUnit.Framework;
using UnityEngine;
using Object = UnityEngine.Object;

namespace CatCourier.Tests.PlayMode
{
    /// <summary>
    /// Shared harness for the Day 6 PlayMode suites.
    ///
    /// Three invariants the whole suite relies on:
    ///
    /// 1. Nothing here reads or writes <see cref="Application.persistentDataPath"/>.
    ///    <see cref="SaveSystem.Awake"/> is the only code that hard-codes the real
    ///    path, so every test builds its SaveSystem on an *inactive* GameObject
    ///    (Awake never runs), points it at a per-test temp directory, and claims the
    ///    singleton through the static setter. OneTimeSetUp/OneTimeTearDown snapshot
    ///    the real save directory and fail the run if a single byte changed.
    ///
    /// 2. No test loads a real scene. <c>GameManager.StartRun</c> and
    ///    <c>ReturnToHub</c> call <c>SceneManager.LoadSceneAsync</c>, which in
    ///    Single mode would tear the test scene down mid-test. See
    ///    <see cref="CreateGameManager"/> and <see cref="StubSceneLoader"/> for the
    ///    seam that keeps the state machine fully exercised while the load is
    ///    short-circuited.
    ///
    /// 3. Every global is reset between cases: all static singletons, the paywall
    ///    gate, and <see cref="Time.timeScale"/>.
    /// </summary>
    public abstract class Day6PlayModeTestBase
    {
        /// <summary>Manager singletons that must be blanked between cases.</summary>
        protected static readonly (Type Type, string Property)[] StaticManagers =
        {
            (typeof(SaveSystem), "Instance"),
            (typeof(GameManager), "Instance"),
            (typeof(SceneLoader), "Instance"),
            (typeof(AudioManager), "Instance"),
            (typeof(RevenueCatManager), "Instance"),
            (typeof(EntitlementChecker), "Instance"),
            (typeof(AdManager), "Instance"),
            (typeof(RunCoordinator), "Active")
        };

        /// <summary>
        /// The order PersistentSystems.Awake creates its children in. The names come
        /// from <c>typeof(T).Name</c>, so this doubles as an assertion on the
        /// declaration order of the boot chain.
        /// </summary>
        protected static readonly string[] DeclaredManagerOrder =
        {
            "SaveSystem",
            "GameManager",
            "SceneLoader",
            "AudioManager",
            "RevenueCatManager",
            "EntitlementChecker",
            "CatBreedManager",
            "UpgradeManager",
            "AdManager"
        };

        private static readonly ChunkType[] AllChunkTypes = (ChunkType[])Enum.GetValues(typeof(ChunkType));

        private readonly List<Object> created = new();
        private static Dictionary<string, string> realSaveSnapshot;

        protected string TempDirectory { get; private set; }

        [OneTimeSetUp]
        public void CaptureRealSavePath()
        {
            realSaveSnapshot = ReadRealSaveDirectory();
        }

        [OneTimeTearDown]
        public void AssertRealSavePathUntouched()
        {
            if (realSaveSnapshot == null)
            {
                return;
            }

            var current = ReadRealSaveDirectory();
            var changes = new List<string>();
            foreach (var pair in current)
            {
                if (!realSaveSnapshot.TryGetValue(pair.Key, out var before))
                {
                    changes.Add($"created {pair.Key}");
                }
                else if (!string.Equals(before, pair.Value, StringComparison.Ordinal))
                {
                    changes.Add($"modified {pair.Key}");
                }
            }

            foreach (var key in realSaveSnapshot.Keys)
            {
                if (!current.ContainsKey(key))
                {
                    changes.Add($"removed {key}");
                }
            }

            if (changes.Count > 0)
            {
                Assert.Fail(
                    "PlayMode tests must never touch the real save path " +
                    $"{Application.persistentDataPath}. Offending changes:{Environment.NewLine}  " +
                    string.Join(Environment.NewLine + "  ", changes));
            }
        }

        [SetUp]
        public void SetUpPlayModeHarness()
        {
            ResetStatics();
            TempDirectory = Path.Combine(
                Path.GetTempPath(),
                "CatCourierDay6PlayMode",
                Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(TempDirectory);
        }

        [TearDown]
        public void TearDownPlayModeHarness()
        {
            for (var index = created.Count - 1; index >= 0; index--)
            {
                if (created[index] != null)
                {
                    Object.DestroyImmediate(created[index]);
                }
            }

            created.Clear();
            ResetStatics();

            if (!string.IsNullOrEmpty(TempDirectory) && Directory.Exists(TempDirectory))
            {
                Directory.Delete(TempDirectory, true);
            }
        }

        // ---------------------------------------------------------------- statics

        /// <summary>Blanks every static singleton, the paywall gate, and the time scale.</summary>
        protected static void ResetStatics()
        {
            foreach (var (type, property) in StaticManagers)
            {
                SetStaticInstance(type, property, null);
            }

            // PersistentSystems.Awake publishes the authored catalog here, so a boot
            // test would otherwise leave the real project catalog behind for later cases.
            CatCourier.UI.PaywallPresenter.CatalogReference = null;
            PaywallGate.ResetForTests();
            Time.timeScale = 1f;
        }

        /// <summary>The one permitted reflection seam: writing a static singleton backing field.</summary>
        protected static void SetStaticInstance(Type type, string propertyName, object value)
        {
            var property = type.GetProperty(propertyName, BindingFlags.Static | BindingFlags.Public);
            Assert.That(property, Is.Not.Null, $"{type.Name}.{propertyName} was not found.");
            var setter = property.GetSetMethod(true);
            Assert.That(setter, Is.Not.Null, $"{type.Name}.{propertyName} has no setter.");
            setter.Invoke(null, new[] { value });
        }

        /// <summary>Identity of every manager singleton, for before/after comparisons.</summary>
        protected static List<string> SnapshotManagerSingletons()
        {
            var snapshot = new List<string>(StaticManagers.Length);
            foreach (var (type, property) in StaticManagers)
            {
                var value = type.GetProperty(property, BindingFlags.Static | BindingFlags.Public)?.GetValue(null, null);
                snapshot.Add(value is not Component component
                    ? $"{type.Name}:none"
                    : $"{type.Name}:{component.gameObject.name}#{component.GetInstanceID()}");
            }

            return snapshot;
        }

    // --------------------------------------------------------------- scene seam

        /// <summary>
        /// The one private field the suite has to write. <c>SceneLoader.Load</c>
        /// starts a coroutine that calls <c>SceneManager.LoadSceneAsync</c>, which
        /// in Single mode would replace the test scene. Flagging the loader as
        /// mid-load makes <c>Load()</c> take its "already loading" branch and raise
        /// the failure event synchronously instead. There is no public API for
        /// this, and no way to keep the GameManager state machine honest without
        /// it, so it is deliberately isolated here.
        ///
        /// Paired with <see cref="CreateGameManager"/>, which creates the loader
        /// before the manager so nothing is subscribed to that failure event: the
        /// manager keeps the state it just set and no real scene is ever touched.
        /// </summary>
        protected static void StubSceneLoader(SceneLoader loader)
        {
            Assert.That(loader, Is.Not.Null);
            SetPrivateField(loader, "isLoading", true);
        }

        // ---------------------------------------------------------------- builders

        /// <summary>Tracks a scene object so the harness destroys it in teardown.</summary>
        protected GameObject Track(GameObject gameObject)
        {
            created.Add(gameObject);
            return gameObject;
        }

        /// <summary>Tracks a non-GameObject asset (catalogs) for teardown.</summary>
        protected TAsset TrackAsset<TAsset>(TAsset asset) where TAsset : Object
        {
            created.Add(asset);
            return asset;
        }

        /// <summary>
        /// A SaveSystem that is safe by construction: the host stays inactive so
        /// <c>Awake</c> (the only code that touches the real path) never runs, and
        /// the paths are aimed at <see cref="TempDirectory"/>.
        /// </summary>
        protected SaveSystem CreateTempSaveSystem()
        {
            var host = Track(new GameObject("Day6SaveSystem"));
            host.SetActive(false);
            var save = host.AddComponent<SaveSystem>();
            save.ConfigurePaths(TempDirectory);
            save.Load();
            SetStaticInstance(typeof(SaveSystem), "Instance", save);
            return save;
        }

        /// <summary>
        /// A GameManager plus a SceneLoader that can never reach the SceneManager.
        ///
        /// The creation order is load-bearing. <c>SceneLoader.Awake</c> subscribes
        /// to <c>GameManager.HandleSceneLoadFailed</c> only when
        /// <c>GameManager.Instance</c> already exists, so building the loader first
        /// leaves that event with no subscribers. StartRun and ReturnToHub then run
        /// their real state transitions and their real Load call, the stubbed Load
        /// raises a failure event nobody hears, and the requested state survives.
        /// </summary>
        protected GameManager CreateGameManager(out SceneLoader loader)
        {
            loader = Track(new GameObject("Day6SceneLoader")).AddComponent<SceneLoader>();
            StubSceneLoader(loader);
            return Track(new GameObject("Day6GameManager")).AddComponent<GameManager>();
        }

        protected RunCoordinator CreateRunCoordinator()
        {
            return Track(new GameObject("Day6RunCoordinator")).AddComponent<RunCoordinator>();
        }

        /// <summary>
        /// A kinematic, trigger-collider player. It never falls: with
        /// <see cref="PlayerState.Running"/> and <c>grounded == false</c> the
        /// gravity branch in <c>PlayerController.Simulate</c> is skipped, so distance
        /// is a clean, monotonic signal of "is gameplay ticking".
        /// </summary>
        protected PlayerController CreatePlayer(string name)
        {
            var host = Track(new GameObject(name));
            host.tag = "Player";
            var body = host.AddComponent<Rigidbody2D>();
            body.bodyType = RigidbodyType2D.Kinematic;
            body.gravityScale = 0f;
            var hitbox = host.AddComponent<BoxCollider2D>();
            hitbox.size = new Vector2(Constants.PLAYER_HITBOX_WIDTH, Constants.PLAYER_HITBOX_HEIGHT);
            return host.AddComponent<PlayerController>();
        }

        protected Camera CreateCamera(string name = "Day6Camera")
        {
            var host = Track(new GameObject(name));
            host.tag = "MainCamera";
            var camera = host.AddComponent<Camera>();
            camera.orthographic = true;
            camera.orthographicSize = 5f;
            return camera;
        }

        /// <summary>
        /// A ChunkManager with <c>startOnAwake</c> off so the test decides when
        /// generation starts. Create the GameManager before calling this if the test
        /// cares about pause propagation: <c>ChunkManager.Awake</c> only subscribes
        /// when <c>GameManager.Instance</c> already exists.
        /// </summary>
        protected ChunkManager CreateChunkManager(
            ChunkCatalog catalog,
            Transform player,
            Camera camera,
            Transform fallback,
            out ProceduralGenerator generator)
        {
            var host = Track(new GameObject("Day6ChunkManager"));
            generator = host.AddComponent<ProceduralGenerator>();
            var manager = host.AddComponent<ChunkManager>();
            manager.Configure(catalog, generator, player, camera, fallback, false);
            return manager;
        }

        /// <summary>Inactive root that holds in-memory chunk prefabs out of the scene.</summary>
        protected Transform CreateChunkPrefabSources()
        {
            var sources = Track(new GameObject("Day6ChunkSources"));
            sources.SetActive(false);
            return sources.transform;
        }

        /// <summary>Every chunk type for one district, with the requested variant count.</summary>
        protected ChunkCatalog CreateCatalog(
            Transform sources,
            DistrictId district,
            int variantsPerType,
            bool withCheckpoint)
        {
            var entries = new List<ChunkCatalog.Entry>();
            foreach (var type in AllChunkTypes)
            {
                for (var variant = 0; variant < variantsPerType; variant++)
                {
                    entries.Add(new ChunkCatalog.Entry
                    {
                        district = district,
                        type = type,
                        prefab = CreateChunkPrefab(sources, type, $"Chunk_{type}_{district}_{variant:00}", withCheckpoint)
                    });
                }
            }

            var catalog = TrackAsset(ScriptableObject.CreateInstance<ChunkCatalog>());
            catalog.SetEntries(entries);
            return catalog;
        }

        /// <summary>
        /// One shared prefab registered under every chunk type. Because
        /// <c>ChunkManager</c> pools per source prefab, a recycle is always
        /// immediately reusable, which makes "no new instantiation" an exact
        /// assertion instead of a statistical one.
        /// </summary>
        protected ChunkCatalog CreateSinglePrefabCatalog(Transform sources, DistrictId district)
        {
            var prefab = CreateChunkPrefab(sources, ChunkType.SmallGap, $"Chunk_Shared_{district}", true);
            var entries = new List<ChunkCatalog.Entry>();
            foreach (var type in AllChunkTypes)
            {
                entries.Add(new ChunkCatalog.Entry { district = district, type = type, prefab = prefab });
            }

            var catalog = TrackAsset(ScriptableObject.CreateInstance<ChunkCatalog>());
            catalog.SetEntries(entries);
            return catalog;
        }

        /// <summary>An empty catalog: the "content was never authored" configuration.</summary>
        protected ChunkCatalog CreateEmptyCatalog()
        {
            return TrackAsset(ScriptableObject.CreateInstance<ChunkCatalog>());
        }

        // ------------------------------------------------------------- assertions

        /// <summary>Active chunk identity + position, for "nothing moved" comparisons.</summary>
        protected static List<string> SnapshotActiveChunks(ChunkManager manager)
        {
            var container = manager.transform.Find("ChunkContainer");
            if (container == null)
            {
                return new List<string>();
            }

            return container.GetComponentsInChildren<ChunkMarker>(true)
                .Where(marker => marker.gameObject.activeSelf)
                .Select(marker => $"{marker.GetInstanceID()}@{marker.transform.position.x:0.###}")
                .OrderBy(entry => entry, StringComparer.Ordinal)
                .ToList();
        }

        /// <summary>Instance IDs of every chunk clone that exists, pooled ones included.</summary>
        protected static List<int> SnapshotAllChunkInstanceIds(ChunkManager manager)
        {
            var container = manager.transform.Find("ChunkContainer");
            if (container == null)
            {
                return new List<int>();
            }

            return container.GetComponentsInChildren<ChunkMarker>(true)
                .Select(marker => marker.GetInstanceID())
                .OrderBy(id => id)
                .ToList();
        }

        // -------------------------------------------------------------- reflection

        /// <summary>
        /// Second reflection seam, used only where a public API does not exist:
        /// ChunkMarker has no public way to receive its Start/End markers, and
        /// StubSceneLoads above. Mirrors the pattern already used by the Day 3
        /// EditMode suite.
        /// </summary>
        protected static void SetPrivateField(object target, string fieldName, object value)
        {
            var field = target.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(field, Is.Not.Null, $"{target.GetType().Name}.{fieldName} was not found.");
            field.SetValue(target, value);
        }

        private GameObject CreateChunkPrefab(
            Transform sources,
            ChunkType markerType,
            string name,
            bool withCheckpoint)
        {
            var root = new GameObject(name);
            root.transform.SetParent(sources, false);

            var start = new GameObject("Start").transform;
            start.SetParent(root.transform, false);
            start.localPosition = Vector3.zero;

            var end = new GameObject("End").transform;
            end.SetParent(root.transform, false);
            end.localPosition = new Vector3(Constants.CHUNK_WIDTH, 0f, 0f);

            var marker = root.AddComponent<ChunkMarker>();
            SetPrivateField(marker, "start", start);
            SetPrivateField(marker, "end", end);
            SetPrivateField(marker, "type", markerType);

            if (withCheckpoint)
            {
                root.AddComponent<CheckpointMarker>();
                var trigger = root.AddComponent<BoxCollider2D>();
                trigger.isTrigger = true;
            }

            return root;
        }

        private static Dictionary<string, string> ReadRealSaveDirectory()
        {
            var snapshot = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            var root = Application.persistentDataPath;
            if (string.IsNullOrEmpty(root) || !Directory.Exists(root))
            {
                return snapshot;
            }

            foreach (var path in Directory.GetFiles(root))
            {
                snapshot[Path.GetFileName(path)] = Convert.ToBase64String(File.ReadAllBytes(path));
            }

            return snapshot;
        }
    }
}
