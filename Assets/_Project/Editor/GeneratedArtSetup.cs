using System;
using System.IO;
using System.Linq;
using CatCourier.Art;
using CatCourier.Core;
using CatCourier.Generation;
using CatCourier.Obstacles;
using CatCourier.Player;
using CatCourier.Progression;
using CatCourier.Coins;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace CatCourier.Editor
{
    public static class GeneratedArtSetup
    {
        private const string Root = GeneratedArtImporter.Root;
        private const string CatalogPath = "Assets/_Project/Config/GeneratedArtCatalog.asset";
        private const string BreedPath = "Assets/_Project/ScriptableObjects/CatBreedConfigs";
        private static readonly string[] Times = { "day", "sunset", "dusk", "night" };
        private static readonly string[] SkinFiles = {
            "cat_01_ginger_tabby_ink", "cat_02_midnight_black_ink",
            "cat_03_odd_eye_ivory_ink", "cat_04_mosaic_calico_ink",
            "cat_05_bricklane_tuxedo_ink"
        };
        private static readonly string[] BreedIds = { "tabby", "midnight", "ivory", "calico", "tuxedo" };
        private static readonly string[] BreedNames = {
            "Ginger Tabby", "Midnight Black", "Odd-Eyed Ivory", "Mosaic Calico", "Bricklane Tuxedo"
        };

        [MenuItem("Cat Courier/Art/Install Generated Art", priority = 50)]
        public static void Install()
        {
            if (!AssetDatabase.IsValidFolder(Root.TrimEnd('/')))
            {
                Debug.LogError("Generated art folder is missing: " + Root);
                return;
            }
            AssetDatabase.Refresh();
            var art = BuildCatalog();
            var breeds = EnsureBreeds(art);
            WireBoot(breeds, art);
            WireGame(art);
            AssetDatabase.SaveAssets();
            Debug.Log("Generated Cat Courier art installed: five selectable cats, package, obstacles, coin, animated scenery, sky and weather. Open Game scene to review.");
        }

        private static GeneratedArtCatalog BuildCatalog()
        {
            var art = AssetDatabase.LoadAssetAtPath<GeneratedArtCatalog>(CatalogPath);
            if (art == null)
            {
                art = ScriptableObject.CreateInstance<GeneratedArtCatalog>();
                AssetDatabase.CreateAsset(art, CatalogPath);
            }
            art.cats = new CatSkinArt[5];
            for (var i = 0; i < art.cats.Length; i++)
            {
                var prefix = "Characters/PlayableCats/" + SkinFiles[i];
                art.cats[i] = new CatSkinArt {
                    breedId = BreedIds[i],
                    actions = Frames(prefix + "_actions_4x4.png"),
                    reactions = Frames(prefix + "_reactions_3x2.png"),
                    slideWallFinish = Frames(prefix + "_slide_wall_finish_3x2.png")
                };
                Expect(art.cats[i].actions, 16, prefix + " actions");
                Expect(art.cats[i].reactions, 6, prefix + " reactions");
                Expect(art.cats[i].slideWallFinish, 6, prefix + " extra actions");
            }
            art.packages = new[] {
                Package(PackageType.Normal, "package_blue_gingham_bindle_actions_2x2.png"),
                Package(PackageType.Fragile, "package_fragile_bindle_actions_2x2.png"),
                Package(PackageType.Heavy, "package_heavy_bindle_actions_2x2.png"),
                Package(PackageType.Urgent, "package_urgent_bindle_actions_2x2.png")
            };
            art.districts = new[] {
                District(DistrictId.OldTown, "Backgrounds/OldTownSparse", "bg_old_town_block_*_day.png", "Props/Streets/OldTown/street_old_town_intact_a_"),
                District(DistrictId.Downtown, "Backgrounds/ModernTown", "bg_modern_town_block_*_day.png", "Props/Streets/ModernTown/street_modern_town_intact_a_")
            };
            var nature = District(DistrictId.Suburbs, "Backgrounds/Nature", "bg_nature_block_*_day.png", "Props/Streets/Nature/street_nature_intact_a_");
            art.natureBlocksByTime = nature.blocksByTime;
            art.natureBlockCount = nature.blockCount;
            art.natureRoadByTime = nature.roadByTime;
            art.oldNatureTransitionByTime = Times.Select(time => Single("Backgrounds/OldToNature/bg_transition_old_to_nature_clouds_free_" + time + ".png")).ToArray();
            art.natureModernTransitionByTime = Times.Select(time => Single("Backgrounds/NatureToModernTransition/bg_transition_nature_to_modern_clouds_free_" + time + ".png")).ToArray();
            art.oldNatureRoadByTime = Times.Select(time => Single("Props/Streets/Transitions/street_transition_old_town_to_nature_" + time + ".png")).ToArray();
            art.natureModernRoadByTime = Times.Select(time => Single("Props/Streets/Transitions/street_transition_nature_to_modern_" + time + ".png")).ToArray();
            art.oldTownCloseupByTime = Times.Select(time => Single("Backgrounds/CloseUps/OldTown/bg_closeup_old_town_bakery_clocktower_clouds_free_" + time + ".png")).ToArray();
            art.modernTownCloseupByTime = Times.Select(time => Single("Backgrounds/CloseUps/ModernTown/bg_closeup_modern_town_cafe_office_clouds_free_" + time + ".png")).ToArray();
            art.clearSky = Times.Select(time => Single("Backgrounds/SkyAnimation/sky_base_shared_" + time + ".png")).ToArray();
            art.rainySky = Times.Select(time => Single("Backgrounds/SkyAnimation/sky_overcast_rain_" + time + ".png")).ToArray();
            art.cloudDay = Frames("Backgrounds/SkyAnimation/cloud_sheet_shared_day_3sprites.png");
            art.rainFrames = Frames("Backgrounds/SkyAnimation/weather_rain_loop_4frames.png");
            art.windFrames = Frames("Backgrounds/SkyAnimation/weather_wind_gust_loop_4frames.png");
            art.fogFrames = Frames("Backgrounds/SkyAnimation/weather_fog_bank_loop_4frames.png");
            art.birdFrames = Frames("Props/Effects/fx_stun_songbird_wingbeat_2x2.png");
            art.coinFrames = Frames("Props/Coins/coin_pickup_collect_3x2.png");
            art.checkpointFrames = Frames("Props/Markers/marker_delivery_checkpoint_states_2x2.png");
            art.oldTownLaundryFrames = Frames("Props/Ambient/anim_old_town_laundry_sway_2x2.png");
            art.fountainFrames = Frames("Props/Ambient/anim_old_town_piazza_fountain_water_4frames.png");
            art.natureGrassFrames = Frames("Props/Ambient/anim_nature_meadow_grass_sway_4frames.png");
            art.swallowFrames = Frames("Props/Birds/anim_modern_town_white_swallow_flight_6frames.png");
            art.oldTownPaverFrames = Frames("Props/Obstacles/obstacle_old_town_loose_paver_2x2.png");
            art.modernDroneFrames = Frames("Props/Obstacles/obstacle_modern_security_drone_hover_2x2.png");
            art.modernAcFrames = Frames("Props/Obstacles/obstacle_modern_ac_fan_2x2.png");
            art.naturePotFrames = Frames("Props/Obstacles/obstacle_nature_garden_pot_2x2.png");
            art.harbourSeagullFrames = Frames("Props/Obstacles/obstacle_harbour_seagull_flight_3x2.png");
            art.harbourPuddleFrames = Frames("Props/Obstacles/obstacle_harbour_slick_puddle_2x2.png");
            art.hudIcons = Frames("UI/ui_hud_core_3x2.png");
            art.packageIcons = Frames("UI/ui_package_status_2x2.png");
            art.upgradeIcons = Frames("UI/ui_upgrade_badges_3x1.png");
            art.selectionFrames = Frames("UI/ui_selection_frames_3x2.png");
            art.resultsIcons = Frames("UI/ui_results_continue_2x2.png");
            art.premiumBenefits = Frames("UI/ui_premium_benefits_2x2.png");
            art.premiumHero = Single("UI/ui_premium_hero.png");
            EditorUtility.SetDirty(art);
            return art;
        }

        private static PackageArt Package(PackageType type, string file)
        {
            var frames = Frames("Props/Packages/" + file);
            Expect(frames, 4, file);
            return new PackageArt { type = type, frames = frames };
        }

        private static DistrictArt District(DistrictId id, string folder, string search, string roadPrefix)
        {
            var baseNames = Directory.GetFiles(Root + folder, search)
                .Select(Path.GetFileName).OrderBy(name => name, StringComparer.Ordinal).ToArray();
            var blocks = baseNames.SelectMany(name => Times.Select(time =>
                Single(folder + "/" + name.Replace("_day.png", "_" + time + ".png")))).ToArray();
            return new DistrictArt {
                district = id,
                blockCount = baseNames.Length,
                blocksByTime = blocks,
                roadByTime = Times.Select(time => Single(roadPrefix + time + ".png")).ToArray()
            };
        }

        private static Sprite Single(string relative)
        {
            var path = Root + relative;
            var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);
            if (sprite == null) Debug.LogWarning("Missing generated sprite: " + path);
            return sprite;
        }

        private static Sprite[] Frames(string relative)
        {
            var path = Root + relative;
            var frames = AssetDatabase.LoadAllAssetsAtPath(path).OfType<Sprite>()
                .OrderBy(sprite => sprite.name, StringComparer.Ordinal).ToArray();
            if (frames.Length == 0) Debug.LogWarning("Missing generated sprite frames: " + path);
            return frames;
        }

        private static void Expect(Sprite[] frames, int count, string description)
        {
            if (frames.Length != count) Debug.LogError($"{description}: expected {count} sprites, found {frames.Length}. Reimport its PNG before installation.");
        }

        private static CatBreedConfig[] EnsureBreeds(GeneratedArtCatalog art)
        {
            if (!AssetDatabase.IsValidFolder(BreedPath))
            {
                Directory.CreateDirectory(BreedPath);
                AssetDatabase.Refresh();
            }
            var result = new CatBreedConfig[5];
            for (var i = 0; i < 5; i++)
            {
                var path = BreedPath + "/" + BreedIds[i] + ".asset";
                var config = AssetDatabase.LoadAssetAtPath<CatBreedConfig>(path);
                if (config == null)
                {
                    config = ScriptableObject.CreateInstance<CatBreedConfig>();
                    AssetDatabase.CreateAsset(config, path);
                }
                config.id = BreedIds[i];
                config.breedName = BreedNames[i];
                config.description = "Hand-drawn playable cat skin.";
                config.idleSprite = GeneratedArtCatalog.Frame(art.cats[i].actions, 0);
                config.animatorController = AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(
                    "Assets/_Project/Animations/CatPlaceholder.controller");
                config.coinBonusMultiplier = 1f;
                config.isPremium = false;
                config.isIAP = false;
                EditorUtility.SetDirty(config);
                result[i] = config;
            }
            return result;
        }

        private static void WireBoot(CatBreedConfig[] breeds, GeneratedArtCatalog art)
        {
            var scene = EditorSceneManager.OpenScene("Assets/_Project/Scenes/Boot.unity", OpenSceneMode.Single);
            var systems = UnityEngine.Object.FindObjectOfType<PersistentSystems>();
            if (systems != null)
            {
                var serialized = new SerializedObject(systems);
                var property = serialized.FindProperty("generatedCatBreeds");
                property.arraySize = breeds.Length;
                for (var i = 0; i < breeds.Length; i++)
                    property.GetArrayElementAtIndex(i).objectReferenceValue = breeds[i];
                serialized.FindProperty("generatedArtCatalog").objectReferenceValue = art;
                serialized.ApplyModifiedPropertiesWithoutUndo();
                EditorSceneManager.SaveScene(scene);
            }
        }

        private static void WireGame(GeneratedArtCatalog art)
        {
            var scene = EditorSceneManager.OpenScene("Assets/_Project/Scenes/Game.unity", OpenSceneMode.Single);
            var root = GameObject.Find("Day2TestTrack");
            var player = UnityEngine.Object.FindObjectOfType<PlayerController>();
            var camera = Camera.main;
            if (root == null || player == null || camera == null)
            {
                Debug.LogError("Run Cat Courier > Setup > Apply All before installing art; the Game scene is incomplete.");
                return;
            }

            var rig = player.transform.Find("CatRig");
            if (rig == null) { Debug.LogError("CatRig placeholder is missing."); return; }
            var mesh = rig.GetComponent<MeshRenderer>();
            if (mesh != null) mesh.enabled = false;
            var visual = rig.Find("Generated Cat Visual");
            if (visual == null)
            {
                var child = new GameObject("Generated Cat Visual");
                child.transform.SetParent(rig, false);
                visual = child.transform;
            }
            // The placeholder capsule rig sits below the 2D ground line.
            // Align the visible paws with the gameplay ground at y=0.
            visual.localPosition = new Vector3(0f, -0.25f, 0f);
            visual.localScale = Vector3.one;
            var renderer = GetOrAdd<SpriteRenderer>(visual.gameObject);
            renderer.sortingOrder = 50;
            renderer.sprite = GeneratedArtCatalog.Frame(art.cats[0].actions, 0);
            var catArt = GetOrAdd<GeneratedCatArt>(visual.gameObject);
            catArt.Configure(art, player);
            var backdrop = GetOrAdd<GeneratedBackdrop>(root);
            var fallback = root.transform.Find("Day2FallbackContent");
            backdrop.Configure(art, camera, player, fallback != null ? fallback.gameObject : null);
            if (fallback != null)
            {
                foreach (var ground in fallback.GetComponentsInChildren<GroundSurface>(true))
                {
                    var groundMesh = ground.GetComponent<MeshRenderer>();
                    if (groundMesh != null) groundMesh.enabled = false;
                }
                foreach (var coin in fallback.GetComponentsInChildren<CoinPickup>(true))
                    AddLoop(coin.transform, "Generated Coin", art.coinFrames.Take(2).ToArray(), 4f, 0.36f, 52, -2.8f);
                foreach (var obstacle in fallback.GetComponentsInChildren<StaticObstacle>(true))
                {
                    AddLoop(obstacle.transform, "Generated Loose Paver", art.oldTownPaverFrames, 4f, 0.24f, 45, -1.15f);
                    AlignObstacleCollider(obstacle, -1.15f);
                }
                foreach (var obstacle in fallback.GetComponentsInChildren<BounceObstacle>(true))
                {
                    AddLoop(obstacle.transform, "Generated AC Fan", art.modernAcFrames, 5f, 0.25f, 45, -1.15f);
                    AlignObstacleCollider(obstacle, -1.15f);
                }
            }
            CreateAmbient(root.transform, "Old Town Laundry", new Vector3(5f, 3.1f, 0f), art.oldTownLaundryFrames, 5f, 0.2f, -10);
            CreateAmbient(root.transform, "Old Town Fountain", new Vector3(9f, 0.7f, 0f), art.fountainFrames, 6f, 0.18f, -8);
            CreateAmbient(root.transform, "White Swallow", new Vector3(13f, 3.8f, 0f), art.swallowFrames, 9f, 0.08f, -5);
            EditorSceneManager.SaveScene(scene);
        }

        private static void AddLoop(Transform host, string name, Sprite[] frames, float fps, float scale, int order, float localY = 0f)
        {
            var mesh = host.GetComponent<MeshRenderer>();
            if (mesh != null) mesh.enabled = false;
            var child = host.Find(name);
            if (child == null)
            {
                var gameObject = new GameObject(name);
                gameObject.transform.SetParent(host, false);
                child = gameObject.transform;
            }
            child.localPosition = new Vector3(0f, localY, 0f);
            child.localScale = Vector3.one * scale;
            var renderer = GetOrAdd<SpriteRenderer>(child.gameObject);
            renderer.sortingOrder = order;
            renderer.sprite = GeneratedArtCatalog.Frame(frames, 0);
            var loop = GetOrAdd<GeneratedSpriteLoop>(child.gameObject);
            loop.Configure(frames, fps);
        }

        private static void AlignObstacleCollider(ObstacleBase obstacle, float localY)
        {
            // The art is drawn on a child below the obstacle root. Keep its
            // trigger on the road with the visible obstacle so airborne cats
            // are not killed by an invisible collider above the sprite.
            var collider = obstacle.GetComponent<BoxCollider2D>();
            if (collider != null)
                collider.offset = new Vector2(collider.offset.x, localY);
        }

        private static void CreateAmbient(Transform root, string name, Vector3 position, Sprite[] frames, float fps, float scale, int order)
        {
            if (frames.Length == 0) return;
            var child = root.Find(name);
            if (child == null)
            {
                var gameObject = new GameObject(name);
                gameObject.transform.SetParent(root, false);
                child = gameObject.transform;
            }
            child.position = position;
            child.localScale = Vector3.one * scale;
            var renderer = GetOrAdd<SpriteRenderer>(child.gameObject);
            renderer.sortingOrder = order;
            var loop = GetOrAdd<GeneratedSpriteLoop>(child.gameObject);
            loop.Configure(frames, fps);
        }

        private static T GetOrAdd<T>(GameObject host) where T : Component
        {
            var existing = host.GetComponent<T>();
            return existing != null ? existing : host.AddComponent<T>();
        }
    }
}
