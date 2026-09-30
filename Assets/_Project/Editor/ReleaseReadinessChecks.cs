using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using CatCourier.Audio;
using CatCourier.Core;
using CatCourier.Generation;
using CatCourier.Monetization;
using CatCourier.Progression;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using UnityEngine.InputSystem;

namespace CatCourier.Editor
{
    /// <summary>Day 6 checks. Every method only reads state and appends findings.</summary>
    public static partial class ReleaseReadinessValidator
    {
        private const string ScenesCheck = "Scenes";
        private const string BuildSettingsCheck = "Build settings";
        private const string CatalogCheck = "Chunk catalog";
        private const string PaidDistrictsCheck = "Paid districts";
        private const string PaidBreedsCheck = "Paid breeds";
        private const string VisibilityCheck = "Paid content visibility";
        private const string AudioCheck = "Audio";
        private const string InputCheck = "Input";
        private const string AnimatorCheck = "Animator";
        private const string TagLayerCheck = "Tags and layers";
        private const string SecretCheck = "Secrets and ignores";
        private const string ReadmeCheck = "README";

        public static readonly DistrictId[] PaidDistricts = { DistrictId.Harbour, DistrictId.Suburbs };

        public static readonly DistrictId[] FreeDistricts = { DistrictId.OldTown, DistrictId.Downtown };

        public static readonly string[] RequiredAnimatorParameters =
            { "Speed", "IsGrounded", "IsSliding", "Jump", "Die", "WallBounce" };

        public static readonly string[] RequiredAnimatorStates = { "CatIdle", "CatRun", "CatJump", "CatSlide", "CatDie" };

        public static readonly string[] RequiredTags =
            { "Player", "Ground", "Obstacle", "WallBounce", "Coin", "CoinRare", "Checkpoint" };

        public static readonly string[] RequiredLayers = { "Player", "Ground", "Obstacle", "Pickup", "UI" };

        /// <summary>RevenueCat breed packs an authored IAP breed may reference (GameContracts.RevenueCatIds).</summary>
        public static readonly string[] KnownBreedPacks = { RevenueCatIds.PackageRare, RevenueCatIds.PackageLegendary };

        private static void CheckScenes(ReleaseReadinessReport report)
        {
            foreach (var name in new[] { SceneNames.Boot, SceneNames.Hub, SceneNames.Game })
            {
                var path = $"{ScenesPath}/{name}.unity";
                if (AssetDatabase.LoadAssetAtPath<SceneAsset>(path) == null)
                {
                    report.Add(ScenesCheck, ReleaseSeverity.Blocker,
                        $"Required scene '{name}' is missing.",
                        $"Expected at {path}. Run Cat Courier > Setup > Create Missing Scenes.");
                }
                else
                {
                    report.Add(ScenesCheck, ReleaseSeverity.Pass, $"Scene '{name}' exists.");
                }
            }
        }

        private static void CheckBuildSettings(ReleaseReadinessReport report)
        {
            var scenes = EditorBuildSettings.scenes ?? Array.Empty<EditorBuildSettingsScene>();
            foreach (var name in new[] { SceneNames.Boot, SceneNames.Hub, SceneNames.Game })
            {
                var expected = $"{ScenesPath}/{name}.unity";
                var match = scenes.FirstOrDefault(scene => scene.path == expected);
                if (match.path == null)
                {
                    report.Add(BuildSettingsCheck, ReleaseSeverity.Blocker,
                        $"Scene '{name}' is not registered in Build Settings.",
                        $"Expected path {expected} in ProjectSettings/EditorBuildSettings.asset.");
                }
                else if (!match.enabled)
                {
                    report.Add(BuildSettingsCheck, ReleaseSeverity.Blocker,
                        $"Scene '{name}' is registered but disabled in Build Settings.");
                }
                else
                {
                    report.Add(BuildSettingsCheck, ReleaseSeverity.Pass, $"Scene '{name}' is enabled in Build Settings.");
                }
            }

            if (scenes.Length == 0)
            {
                report.Add(BuildSettingsCheck, ReleaseSeverity.Blocker, "Build Settings contains no scenes at all.");
                return;
            }

            var bootPath = $"{ScenesPath}/{SceneNames.Boot}.unity";
            if (scenes[0].path != bootPath)
            {
                report.Add(BuildSettingsCheck, ReleaseSeverity.Blocker,
                    $"Build Settings index 0 is '{scenes[0].path}' but '{bootPath}' must be first.",
                    "Boot creates the persistent systems and must be the startup scene.");
            }
            else
            {
                report.Add(BuildSettingsCheck, ReleaseSeverity.Pass, "Boot is the first scene in Build Settings.");
            }

            var extras = scenes
                .Where(scene => scene.enabled && scene.path != null &&
                                !new[] { bootPath, $"{ScenesPath}/{SceneNames.Hub}.unity", $"{ScenesPath}/{SceneNames.Game}.unity" }
                                    .Contains(scene.path))
                .Select(scene => scene.path)
                .ToList();
            if (extras.Count > 0)
            {
                report.Add(BuildSettingsCheck, ReleaseSeverity.Warn,
                    "Build Settings contains extra enabled scenes.",
                    string.Join(", ", extras));
            }
        }

        private static CatalogCoverage CheckChunkCatalog(ReleaseReadinessReport report)
        {
            var coverage = new CatalogCoverage();
            var catalog = AssetDatabase.LoadAssetAtPath<ChunkCatalog>(ChunkCatalogPath);
            if (catalog == null)
            {
                report.Add(CatalogCheck, ReleaseSeverity.Blocker,
                    $"Chunk catalog asset is missing at {ChunkCatalogPath}.",
                    "Run Cat Courier > Setup > Create Day 3 Systems, then author chunk prefabs.");
                return coverage;
            }

            var entries = catalog.Entries ?? Array.Empty<ChunkCatalog.Entry>();
            coverage.EntryCount = entries.Count;
            if (entries.Count == 0)
            {
                report.Add(CatalogCheck, ReleaseSeverity.Blocker,
                    "Chunk catalog has no entries, so endless generation can only use the Day 2 fallback content.",
                    "Authored chunk prefabs for at least Old Town and Downtown are still required.");
                return coverage;
            }

            RebuildCatalogQuietly(catalog);

            foreach (var message in catalog.ValidationMessages)
            {
                report.Add(CatalogCheck, ReleaseSeverity.Blocker, message, "Reported by ChunkCatalog.BuildLookup().");
            }

            foreach (var district in Enum.GetValues(typeof(DistrictId)).Cast<DistrictId>())
            {
                foreach (var type in Enum.GetValues(typeof(ChunkType)).Cast<ChunkType>())
                {
                    coverage.Set(district, type, catalog.GetVariantCount(district, type));
                }
            }

            if (!catalog.IsValid && catalog.ValidationMessages.Count == 0)
            {
                var missing = coverage.MissingPairs(8);
                report.Add(CatalogCheck, ReleaseSeverity.Blocker,
                    "Chunk catalog has incomplete district/chunk coverage.",
                    missing + " Missing entries are skipped safely at runtime, so runs still work with the fallback content.");
            }
            else if (catalog.IsValid)
            {
                report.Add(CatalogCheck, ReleaseSeverity.Pass,
                    $"Chunk catalog is valid across {coverage.EntryCount} entries.");
            }

            foreach (var district in FreeDistricts)
            {
                var state = coverage.Describe(district);
                if (coverage.IsComplete(district))
                {
                    report.Add(CatalogCheck, ReleaseSeverity.Pass, $"District {district} has all chunk types authored.");
                }
                else if (coverage.DistrictVariants(district) > 0)
                {
                    report.Add(CatalogCheck, ReleaseSeverity.Blocker,
                        $"Free district {district} is only partially authored ({state}).",
                        "The Day 6 plan requires two complete shipped districts before submission.");
                }
                else
                {
                    report.Add(CatalogCheck, ReleaseSeverity.Blocker,
                        $"Free district {district} has no authored chunks.",
                        coverage.Describe(district));
                }
            }

            return coverage;
        }

        private static void CheckPaidDistricts(ReleaseReadinessReport report, CatalogCoverage coverage)
        {
            foreach (var district in PaidDistricts)
            {
                var reach = district == DistrictId.Harbour ? HarbourReachDistance : SuburbsReachDistance;
                if (coverage.IsComplete(district))
                {
                    report.Add(PaidDistrictsCheck, ReleaseSeverity.Pass,
                        $"Paid district {district} is fully authored ({coverage.Describe(district)}) and can be sold as shipped content.");
                }
                else if (coverage.DistrictVariants(district) > 0)
                {
                    report.Add(PaidDistrictsCheck, ReleaseSeverity.Blocker,
                        $"Paid district {district} is partially authored ({coverage.Describe(district)}).",
                        "PaywallPresenter only checks for a SmallGap variant, so iap_districts would be advertised " +
                        "for a district that is not finished. Finish every chunk type or remove the partial prefabs.");
                }
                else
                {
                    report.Add(PaidDistrictsCheck, ReleaseSeverity.Blocker,
                        $"Paid district {district} has no authored chunks, so it is not shipped content yet.",
                        $"RunLoadoutService marks {district} reachable at {reach:0} m and DistrictUnlockService unlocks it by " +
                        "distance. The generator falls back to districts that have variants, so runs stay playable, but the " +
                        $"paywall must not sell {district} until its chunks exist.");
                }
            }
        }

        private static List<BreedAsset> CheckPaidBreeds(ReleaseReadinessReport report)
        {
            var assets = LoadBreedAssets();
            if (assets.Count == 0)
            {
                report.Add(PaidBreedsCheck, ReleaseSeverity.Warn,
                    "No authored CatBreedConfig assets exist; only the built-in starter defaults (tabby, tuxedo) are available.",
                    "CatBreedManager generates those defaults with null sprites, so the cat is an untextured placeholder. " +
                    "That is a content gap, not a build error.");
                return assets;
            }

            report.Add(PaidBreedsCheck, ReleaseSeverity.Pass, $"Found {assets.Count} authored CatBreedConfig asset(s).");

            var premium = assets.Count(asset => asset.IsPremium);
            var paid = assets.Count(asset => asset.IsIAP);
            report.Add(PaidBreedsCheck, premium + paid > 0 ? ReleaseSeverity.Pass : ReleaseSeverity.Warn,
                $"Authored breeds: {premium} premium-entitled, {paid} extra-pack (IAP).");

            foreach (var asset in assets)
            {
                if (asset.IsIAP && string.IsNullOrWhiteSpace(asset.PackId))
                {
                    report.Add(PaidBreedsCheck, ReleaseSeverity.Blocker,
                        $"Paid breed '{asset.Describe()}' has no iapPackId, so it can never be unlocked.",
                        "CatBreedManager locks it and shows \"Coming soon.\", and PaywallPresenter still loads the " +
                        $"{RevenueCatIds.OfferingBreeds} offering for it. Assign a pack id or remove the asset before release.");
                }
                else if (asset.IsIAP && !KnownBreedPacks.Contains(asset.PackId))
                {
                    report.Add(PaidBreedsCheck, ReleaseSeverity.Warn,
                        $"Paid breed '{asset.Describe()}' references unknown pack id '{asset.PackId}'.",
                        "Known breed packs: " + string.Join(", ", KnownBreedPacks) +
                        $". EntitlementChecker only recognises {Constants.ENTITLEMENT_RARE_PACK} / {Constants.ENTITLEMENT_LEGEND_PACK}.");
                }

                if (asset.IsPremium && asset.IsIAP)
                {
                    report.Add(PaidBreedsCheck, ReleaseSeverity.Warn,
                        $"Breed '{asset.Describe()}' is flagged both premium and IAP; unlock precedence is unclear.");
                }

                if ((asset.IsPremium || asset.IsIAP) && !asset.HasSprite)
                {
                    report.Add(PaidBreedsCheck, ReleaseSeverity.Warn,
                        $"Sellable breed '{asset.Describe()}' has no idleSprite authored yet.");
                }
            }

            return assets;
        }

        /// <summary>
        /// Verifies that unfinished paid districts and breeds stay hidden instead of being advertised,
        /// by replaying the same conditions the runtime presenters use.
        /// </summary>
        private static void CheckPaidContentVisibility(
            ReleaseReadinessReport report,
            CatalogCoverage coverage,
            List<BreedAsset> breeds)
        {
            var advertisedDistricts = PaidDistricts
                .Where(district => coverage.Variants(district, ChunkType.SmallGap) > 0)
                .ToList();
            if (advertisedDistricts.Count == 0)
            {
                report.Add(VisibilityCheck, ReleaseSeverity.Pass,
                    "The iap_districts offering stays hidden: no Harbour/Suburbs SmallGap chunk is registered.",
                    "PaywallPresenter.HasDistrictContent() returns false, so the districts are not sold.");
            }
            else
            {
                report.Add(VisibilityCheck, ReleaseSeverity.Blocker,
                    "The iap_districts offering is advertised for incomplete district(s): " +
                    string.Join(", ", advertisedDistricts.Select(district => district.ToString())) + ".",
                    "HasDistrictContent() only looks for a SmallGap variant, so partial districts would be sold.");
            }

            var unfinishedPaidBreeds = breeds.Where(asset => asset.IsIAP && string.IsNullOrWhiteSpace(asset.PackId)).ToList();
            if (unfinishedPaidBreeds.Count > 0)
            {
                report.Add(VisibilityCheck, ReleaseSeverity.Blocker,
                    "The iap_breeds offering is advertised for unfinished paid breed(s): " +
                    string.Join(", ", unfinishedPaidBreeds.Select(asset => asset.Describe())) + ".",
                    "PaywallPresenter.HasPaidBreedContent() counts any isIAP breed regardless of iapPackId, so an " +
                    "unfinished pack would be sold. CatSelectorPresenter also lists it as \"Coming soon.\"");
            }
            else if (breeds.Any(asset => asset.IsIAP))
            {
                report.Add(VisibilityCheck, ReleaseSeverity.Pass,
                    "Every authored IAP breed has a pack id, so the iap_breeds offering is backed by unlockable content.");
            }
            else
            {
                report.Add(VisibilityCheck, ReleaseSeverity.Pass,
                    "The iap_breeds offering stays hidden: no authored breed is flagged isIAP.",
                    "PaywallPresenter.HasPaidBreedContent() returns false until a real IAP breed asset exists.");
            }

            var unwired = breeds.Where(asset => !asset.IsReferencedInScene).ToList();
            if (breeds.Count == 0)
            {
                report.Add(VisibilityCheck, ReleaseSeverity.Warn,
                    "No breed assets are wired into any scene, so the cat list shows only the built-in starter defaults.");
            }
            else if (unwired.Count == 0)
            {
                report.Add(VisibilityCheck, ReleaseSeverity.Pass, "Every authored breed asset is referenced by a scene.");
            }
            else
            {
                report.Add(VisibilityCheck, ReleaseSeverity.Warn,
                    "Authored breed assets are not referenced by any scene and therefore stay hidden: " +
                    string.Join(", ", unwired.Select(asset => asset.Describe())) + ".",
                    "Assign them to a CatBreedManager component, or delete them, so the list matches what is shipped.");
            }

            foreach (var district in PaidDistricts)
            {
                if (coverage.IsComplete(district))
                {
                    continue;
                }

                var partiallyAuthored = coverage.DistrictVariants(district) > 0;
                report.Add(VisibilityCheck, ReleaseSeverity.Warn,
                    partiallyAuthored
                        ? $"Unlock copy for {district} is reachable while the district is only partially authored."
                        : $"Unlock copy for {district} is reachable without any authored content.",
                    "RunLoadoutService.GetEligibleDistricts adds it by distance and premium, and " +
                    "DistrictUnlockService writes it into unlockedDistrictIds. ProceduralGenerator only switches to " +
                    "districts that have variants and the Hub labels the district as not shipped, so play stays " +
                    "safe and the copy stays honest, but " +
                    (partiallyAuthored
                        ? "the district must not be sold as finished."
                        : $"the {district} entry remains in save data before its content exists."));
            }
        }

        private static void CheckAudio(ReleaseReadinessReport report, CatalogCoverage coverage)
        {
            var clipGuids = AssetDatabase.FindAssets("t:AudioClip");
            if (clipGuids.Length == 0)
            {
                report.Add(AudioCheck, ReleaseSeverity.Blocker,
                    "The project contains no AudioClip assets, so the shipped build is silent.",
                    "AudioManager and AudioLibrary are silent-safe by design; this is missing content, not broken code.");
            }
            else
            {
                report.Add(AudioCheck, ReleaseSeverity.Pass, $"Found {clipGuids.Length} AudioClip asset(s).");
            }

            if (AssetDatabase.FindAssets("t:AudioMixer").Length == 0)
            {
                report.Add(AudioCheck, ReleaseSeverity.Blocker,
                    "No AudioMixer asset exists, so AudioManager mixer groups (music/sfx/ambient) are unassigned.",
                    "Sources fall back to the Default group; no bus routing, ducking, or volume control ships.");
            }
            else
            {
                report.Add(AudioCheck, ReleaseSeverity.Pass, "An AudioMixer asset exists.");
            }

            var libraryPath = FirstAssetPath("t:AudioLibrary");
            if (libraryPath == null)
            {
                report.Add(AudioCheck, ReleaseSeverity.Blocker,
                    "No AudioLibrary asset exists, so AudioManager plays no SFX and no music.",
                    "Expected under Assets/_Project/ScriptableObjects/Audio. Do not claim audio in the README until it exists.");
                return;
            }

            var library = AssetDatabase.LoadAssetAtPath<AudioLibrary>(libraryPath);
            if (library == null)
            {
                report.Add(AudioCheck, ReleaseSeverity.Blocker, $"AudioLibrary at {libraryPath} could not be loaded.");
                return;
            }

            var sfx = ReadLibrarySfx(library);
            var music = ReadLibraryMusic(library);
            report.Add(AudioCheck, ReleaseSeverity.Pass,
                $"AudioLibrary '{libraryPath}' wires {sfx.WiredCount}/{SfxIdCount()} SFX and {music.WiredCount}/{MusicIdCount()} music ids.");

            if (sfx.WiredCount == 0)
            {
                report.Add(AudioCheck, ReleaseSeverity.Blocker, "AudioLibrary wires no SFX clips.");
            }
            else if (sfx.Missing.Count > 0)
            {
                report.Add(AudioCheck, ReleaseSeverity.Warn, "AudioLibrary has no clip for SFX ids: " + string.Join(", ", sfx.Missing) + ".");
            }

            // MusicId names deliberately match DistrictId names, so a shipped district implies a shipped track.
            var requiredMusic = new List<string> { nameof(MusicId.Hub), nameof(MusicId.OldTown), nameof(MusicId.Downtown) };
            foreach (var district in PaidDistricts)
            {
                if (coverage.IsComplete(district))
                {
                    requiredMusic.Add(district.ToString());
                }
            }

            var missingMusic = music.Missing.Where(requiredMusic.Contains).ToList();
            if (music.WiredCount == 0)
            {
                report.Add(AudioCheck, ReleaseSeverity.Blocker, "AudioLibrary wires no music clips.");
            }
            else if (missingMusic.Count > 0)
            {
                var missingPaid = missingMusic.Where(id => PaidDistricts.Any(district => district.ToString() == id)).ToList();
                report.Add(AudioCheck, missingPaid.Count > 0 ? ReleaseSeverity.Blocker : ReleaseSeverity.Warn,
                    "AudioLibrary has no clip for shipped music ids: " + string.Join(", ", missingMusic) + ".",
                    missingPaid.Count > 0
                        ? "A fully authored paid district would ship without its music track: " +
                          string.Join(", ", missingPaid) + "."
                        : null);
            }
        }

        private static void CheckInput(ReleaseReadinessReport report)
        {
            var asset = AssetDatabase.LoadAssetAtPath<InputActionAsset>(InputActionsPath);
            if (asset == null)
            {
                report.Add(InputCheck, ReleaseSeverity.Blocker, $"Input action asset is missing at {InputActionsPath}.");
                return;
            }

            var map = asset.FindActionMap("Gameplay", false);
            if (map == null)
            {
                report.Add(InputCheck, ReleaseSeverity.Blocker,
                    "InputActionAsset has no 'Gameplay' action map.",
                    "InputHandler and CatCourierProjectSetup both require it.");
                return;
            }

            var missing = new[] { "Jump", "Slide", "Pause" }
                .Where(action => map.FindAction(action, false) == null)
                .ToList();
            if (missing.Count > 0)
            {
                report.Add(InputCheck, ReleaseSeverity.Blocker,
                    "Gameplay map is missing actions: " + string.Join(", ", missing) + ".");
                return;
            }

            report.Add(InputCheck, ReleaseSeverity.Pass, "InputActionAsset 'Gameplay' map wires Jump, Slide and Pause.");
        }

        private static void CheckAnimatorContract(ReleaseReadinessReport report)
        {
            var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(AnimatorControllerPath);
            if (controller == null)
            {
                report.Add(AnimatorCheck, ReleaseSeverity.Blocker,
                    $"Animator controller is missing at {AnimatorControllerPath}.");
                return;
            }

            var parameterNames = new HashSet<string>(controller.parameters.Select(parameter => parameter.name));
            var missingParameters = RequiredAnimatorParameters.Where(name => !parameterNames.Contains(name)).ToList();
            if (missingParameters.Count > 0)
            {
                report.Add(AnimatorCheck, ReleaseSeverity.Blocker,
                    "Animator controller is missing required parameters: " + string.Join(", ", missingParameters) + ".");
            }
            else
            {
                var extraParameters = parameterNames.Where(name => !RequiredAnimatorParameters.Contains(name)).ToList();
                if (extraParameters.Count > 0)
                {
                    report.Add(AnimatorCheck, ReleaseSeverity.Warn,
                        "Animator controller has parameters outside the Day 2 contract: " + string.Join(", ", extraParameters) + ".");
                }
                else
                {
                    report.Add(AnimatorCheck, ReleaseSeverity.Pass, "Animator parameters match the Day 2 contract.");
                }
            }

            if (controller.layers.Length == 0)
            {
                report.Add(AnimatorCheck, ReleaseSeverity.Blocker, "Animator controller has no layers.");
                return;
            }

            var stateNames = new HashSet<string>(
                controller.layers[0].stateMachine.states.Select(child => child.state.name));
            var missingStates = RequiredAnimatorStates.Where(name => !stateNames.Contains(name)).ToList();
            if (missingStates.Count > 0)
            {
                report.Add(AnimatorCheck, ReleaseSeverity.Blocker,
                    "Animator base layer is missing required states: " + string.Join(", ", missingStates) + ".");
            }
            else
            {
                var extraStates = stateNames.Where(name => !RequiredAnimatorStates.Contains(name)).ToList();
                if (extraStates.Count > 0)
                {
                    report.Add(AnimatorCheck, ReleaseSeverity.Warn,
                        "Animator base layer has states outside the Day 2 contract: " + string.Join(", ", extraStates) + ".");
                }
                else
                {
                    report.Add(AnimatorCheck, ReleaseSeverity.Pass, "Animator base layer states match the Day 2 contract.");
                }
            }
        }

        private static void CheckTagsAndLayers(ReleaseReadinessReport report)
        {
            var tagManagerAssets = AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TagManager.asset");
            if (tagManagerAssets == null || tagManagerAssets.Length == 0)
            {
                report.Add(TagLayerCheck, ReleaseSeverity.Blocker, "ProjectSettings/TagManager.asset could not be read.");
                return;
            }

            var serialized = new SerializedObject(tagManagerAssets[0]);
            var tags = ReadStringArray(serialized.FindProperty("tags"));
            var layers = ReadStringArray(serialized.FindProperty("layers"));

            var missingTags = RequiredTags.Where(tag => !tags.Contains(tag)).ToList();
            if (missingTags.Count > 0)
            {
                report.Add(TagLayerCheck, ReleaseSeverity.Blocker,
                    "Project is missing required tags: " + string.Join(", ", missingTags) + ".");
            }
            else
            {
                report.Add(TagLayerCheck, ReleaseSeverity.Pass, "All required gameplay tags are defined.");
            }

            var missingLayers = RequiredLayers.Where(layer => !layers.Contains(layer)).ToList();
            if (missingLayers.Count > 0)
            {
                report.Add(TagLayerCheck, ReleaseSeverity.Blocker,
                    "Project is missing required layers: " + string.Join(", ", missingLayers) + ".");
            }
            else
            {
                report.Add(TagLayerCheck, ReleaseSeverity.Pass, "All required gameplay layers are defined.");
            }

            var unnamed = RequiredLayers.Where(layer => LayerMask.NameToLayer(layer) < 0).ToList();
            if (unnamed.Count > 0)
            {
                report.Add(TagLayerCheck, ReleaseSeverity.Blocker,
                    "Layers resolve to index -1 and cannot be assigned: " + string.Join(", ", unnamed) + ".");
            }
        }

        private static void CheckSecretConfigAndIgnores(ReleaseReadinessReport report)
        {
            var root = ProjectRoot;
            var ignorePath = Path.Combine(root, ".gitignore");
            var ignores = File.Exists(ignorePath) ? ReadText(ignorePath) : string.Empty;
            if (!File.Exists(ignorePath))
            {
                report.Add(SecretCheck, ReleaseSeverity.Blocker,
                    "No .gitignore at the project root, so local secret config could be committed.");
            }
            else
            {
                report.Add(SecretCheck, ReleaseSeverity.Pass, ".gitignore exists at the project root.");
            }

            var secretPatterns = new[] { RevenueCatConfigPath, RevenueCatConfigPath + ".meta" };
            var ignoredSecrets = secretPatterns.Count(pattern => ContainsIgnorePattern(ignores, pattern));
            foreach (var pattern in secretPatterns)
            {
                if (!ContainsIgnorePattern(ignores, pattern))
                {
                    report.Add(SecretCheck, ReleaseSeverity.Blocker,
                        $".gitignore does not ignore '{pattern}'.",
                        "Local RevenueCat keys must never be committed.");
                }
            }

            if (ignoredSecrets == secretPatterns.Length)
            {
                report.Add(SecretCheck, ReleaseSeverity.Pass,
                    ".gitignore covers the local RevenueCatConfig asset and its .meta file.");
            }

            if (!ContainsIgnorePattern(ignores, ".env"))
            {
                report.Add(SecretCheck, ReleaseSeverity.Warn, ".gitignore does not ignore .env files.");
            }

            foreach (var pattern in new[] { "/[Ll]ibrary/", "/[Bb]uilds/", "/[Ll]ogs/", "/[Tt]est[Rr]esults/" })
            {
                if (!ContainsIgnorePattern(ignores, pattern))
                {
                    report.Add(SecretCheck, ReleaseSeverity.Warn, $".gitignore does not ignore generated folder '{pattern}'.");
                }
            }

            if (!Directory.Exists(Path.Combine(root, ".git")))
            {
                report.Add(SecretCheck, ReleaseSeverity.Warn,
                    "The project folder is not a git working tree, so ignore rules are advisory only.",
                    "Day 6 Part D expects .meta files to be tracked; verify that after the repository exists.");
            }

            var config = AssetDatabase.LoadAssetAtPath<RevenueCatConfig>(RevenueCatConfigPath);
            if (config == null)
            {
                report.Add(SecretCheck, ReleaseSeverity.Warn,
                    $"No local {RevenueCatConfigPath} asset.",
                    "Expected on a fresh clone. Create it with Cat Courier > Setup > Create Local RevenueCat Config and never commit it.");
            }
            else
            {
                if (config.Environment == RevenueCatEnvironment.Production && config.Backend == RevenueCatBackendSelection.Fake)
                {
                    report.Add(SecretCheck, ReleaseSeverity.Blocker,
                        "RevenueCatConfig selects Production with the fake backend.",
                        "Release builds must not ship fake purchase data.");
                }
                else if (config.Backend == RevenueCatBackendSelection.Fake)
                {
                    report.Add(SecretCheck, ReleaseSeverity.Warn,
                        "RevenueCatConfig uses the fake backend in sandbox mode.",
                        "Purchases are simulated. Select the real backend for the Next Gen Android demo.");
                }
                else
                {
                    report.Add(SecretCheck, ReleaseSeverity.Pass, "RevenueCatConfig selects the real backend.");
                }

                var keys = ReadRevenueCatKeyState(config);
                if (keys == 0)
                {
                    report.Add(SecretCheck, ReleaseSeverity.Warn,
                        "RevenueCatConfig has no public keys entered yet.",
                        "Enter a Test Store or platform sandbox key locally. Key values are never read into the report.");
                }
                else
                {
                    report.Add(SecretCheck, ReleaseSeverity.Pass,
                        $"RevenueCatConfig has {keys} of 5 public key fields filled in (values never read or logged).");
                }

                if (config.IsAndroidDemoReady)
                    report.Add(SecretCheck, ReleaseSeverity.Pass, "Android Next Gen demo can use the real RevenueCat SDK.");
                else
                    report.Add(SecretCheck, ReleaseSeverity.Warn,
                        "Android Next Gen demo is not configured for a real RevenueCat test purchase.",
                        "Use Cat Courier > Setup > Use Real RevenueCat for Next Gen Demo and enter a Test Store public key locally.");
            }

            if (File.Exists(Path.Combine(root, "LICENSE")))
            {
                report.Add(SecretCheck, ReleaseSeverity.Pass, "LICENSE exists at the project root.");
            }
            else
            {
                report.Add(SecretCheck, ReleaseSeverity.Blocker, "LICENSE is missing at the project root.",
                    "Day 6 Part D requires a real license file.");
            }
        }

        private static void CheckReadme(ReleaseReadinessReport report)
        {
            var readmePath = Path.Combine(ProjectRoot, "README.md");
            if (!File.Exists(readmePath))
            {
                report.Add(ReadmeCheck, ReleaseSeverity.Blocker, "README.md is missing at the project root.");
                return;
            }

            var text = ReadText(readmePath);
            var lower = text.ToLowerInvariant();
            var missing = RequiredReadmeSections
                .Where(section => !section.Keywords.Any(keyword => lower.Contains(keyword)))
                .Select(section => section.Label)
                .ToList();

            if (missing.Count == 0)
            {
                report.Add(ReadmeCheck, ReleaseSeverity.Pass,
                    $"README covers all {RequiredReadmeSections.Length} required Day 6 sections.");
            }
            else
            {
                foreach (var label in missing)
                {
                    report.Add(ReadmeCheck, ReleaseSeverity.Warn, $"README has no '{label}' section.");
                }
            }

            var markers = PlaceholderMarkers.Where(marker => lower.Contains(marker)).ToList();
            if (markers.Count > 0)
            {
                report.Add(ReadmeCheck, ReleaseSeverity.Warn,
                    "README contains placeholder markers: " + string.Join(", ", markers) + ".",
                    "The Day 6 plan forbids placeholder instructions.");
            }
        }

        private static readonly (string Label, string[] Keywords)[] RequiredReadmeSections =
        {
            ("product description", new[] { "endless runner", "cat courier is" }),
            ("landscape side-view design", new[] { "landscape" }),
            ("screenshots or gifs", new[] { "screenshot", "screen shot", ".gif", "![", "demo video" }),
            ("gameplay loop", new[] { "gameplay", "core loop", "auto-run" }),
            ("features shipped", new[] { "current status", "features", "shipped" }),
            ("unity version", new[] { "2022.3" }),
            ("exact open project steps", new[] { "open project" }),
            ("required packages", new[] { "manifest.json", "input system", "packages resolve" }),
            ("how to run the editor", new[] { "play mode", "open this folder" }),
            ("how to run edit mode tests", new[] { "test runner", "editmode" }),
            ("how to make an android build", new[] { "android build", "builds/android", "apk" }),
            ("how to make an ios build", new[] { "ios build", "xcode" }),
            ("revenuecat sandbox setup", new[] { "revenuecat" }),
            ("fake backend behavior", new[] { "fake backend", "fake purchases", "fake" }),
            ("save location", new[] { "persistentdatapath", "save location", "save file" }),
            ("team roles", new[] { "team roles", "team:" }),
            ("controls", new[] { "controls" }),
            ("known limitations", new[] { "limitation", "not finished", "intentionally", "still require", "pending", "not supported" }),
            ("license", new[] { "license" }),
            ("repository and demo links", new[] { "github.com", "repository", "demo" })
        };

        private static readonly string[] PlaceholderMarkers =
        {
            "todo", "tbd", "coming soon", "lorem ipsum", "placeholder text", "xxx"
        };

        private static void RebuildCatalogQuietly(ChunkCatalog catalog)
        {
            // BuildLookup logs a warning per missing district/chunk pair. The validator reports the
            // same information in the report, so the console is left free of duplicate noise.
            var logging = Debug.unityLogger.logEnabled;
            Debug.unityLogger.logEnabled = false;
            try
            {
                catalog.BuildLookup();
            }
            finally
            {
                Debug.unityLogger.logEnabled = logging;
            }
        }

        private static List<BreedAsset> LoadBreedAssets()
        {
            var assets = new List<BreedAsset>();
            foreach (var guid in AssetDatabase.FindAssets("t:CatBreedConfig"))
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                var config = AssetDatabase.LoadAssetAtPath<CatBreedConfig>(path);
                if (config == null)
                {
                    continue;
                }

                assets.Add(new BreedAsset
                {
                    AssetPath = path,
                    Guid = guid,
                    Id = config.id,
                    BreedName = config.breedName,
                    IsPremium = config.isPremium,
                    IsIAP = config.isIAP,
                    PackId = config.iapPackId,
                    HasSprite = config.idleSprite != null,
                    IsReferencedInScene = AssetIsReferencedBySceneOrPrefab(guid)
                });
            }

            return assets;
        }

        private static bool AssetIsReferencedBySceneOrPrefab(string guid)
        {
            foreach (var file in SerializedAssetFiles())
            {
                if (ReadText(file).Contains(guid))
                {
                    return true;
                }
            }

            return false;
        }

        private static IEnumerable<string> SerializedAssetFiles()
        {
            var assetsRoot = Application.dataPath;
            if (!Directory.Exists(assetsRoot))
            {
                yield break;
            }

            foreach (var extension in new[] { "*.unity", "*.prefab" })
            {
                foreach (var file in Directory.GetFiles(assetsRoot, extension, SearchOption.AllDirectories))
                {
                    yield return file;
                }
            }
        }

        private static string FirstAssetPath(string filter)
        {
            foreach (var guid in AssetDatabase.FindAssets(filter))
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                if (!string.IsNullOrEmpty(path))
                {
                    return path;
                }
            }

            return null;
        }

        private static LibraryCoverage ReadLibrarySfx(AudioLibrary library)
        {
            var coverage = new LibraryCoverage();
            var serialized = new SerializedObject(library);
            var array = serialized.FindProperty("sfx");
            if (array == null || !array.isArray)
            {
                return coverage;
            }

            for (var index = 0; index < array.arraySize; index++)
            {
                var element = array.GetArrayElementAtIndex(index);
                var id = element.FindPropertyRelative("id");
                var clip = element.FindPropertyRelative("clip");
                if (id == null || clip == null || clip.objectReferenceValue == null)
                {
                    continue;
                }

                coverage.Mark(Enum.GetName(typeof(SfxId), id.intValue));
            }

            coverage.Finish(Enum.GetNames(typeof(SfxId)));
            return coverage;
        }

        private static LibraryCoverage ReadLibraryMusic(AudioLibrary library)
        {
            var coverage = new LibraryCoverage();
            var serialized = new SerializedObject(library);
            var array = serialized.FindProperty("music");
            if (array == null || !array.isArray)
            {
                return coverage;
            }

            for (var index = 0; index < array.arraySize; index++)
            {
                var element = array.GetArrayElementAtIndex(index);
                var id = element.FindPropertyRelative("id");
                var clip = element.FindPropertyRelative("clip");
                if (id == null || clip == null || clip.objectReferenceValue == null)
                {
                    continue;
                }

                coverage.Mark(Enum.GetName(typeof(MusicId), id.intValue));
            }

            coverage.Finish(Enum.GetNames(typeof(MusicId)));
            return coverage;
        }

        private static int SfxIdCount()
        {
            return Enum.GetValues(typeof(SfxId)).Length;
        }

        private static int MusicIdCount()
        {
            return Enum.GetValues(typeof(MusicId)).Length;
        }

        private static int ReadRevenueCatKeyState(RevenueCatConfig config)
        {
            var serialized = new SerializedObject(config);
            var filled = 0;
            foreach (var field in new[]
                     {
                         "developmentApplePublicKey", "developmentGooglePublicKey", "developmentTestStorePublicKey",
                         "productionApplePublicKey", "productionGooglePublicKey"
                     })
            {
                var property = serialized.FindProperty(field);
                if (property != null && !string.IsNullOrWhiteSpace(property.stringValue))
                {
                    filled++;
                }
            }

            return filled;
        }

        private static List<string> ReadStringArray(SerializedProperty property)
        {
            var values = new List<string>();
            if (property == null || !property.isArray)
            {
                return values;
            }

            for (var index = 0; index < property.arraySize; index++)
            {
                var value = property.GetArrayElementAtIndex(index).stringValue;
                if (!string.IsNullOrEmpty(value))
                {
                    values.Add(value);
                }
            }

            return values;
        }

        private static bool ContainsIgnorePattern(string ignoreText, string pattern)
        {
            if (string.IsNullOrEmpty(ignoreText))
            {
                return false;
            }

            var normalizedPattern = pattern.TrimStart('/');
            foreach (var rawLine in ignoreText.Split('\n'))
            {
                var line = rawLine.Trim();
                if (line.Length == 0 || line.StartsWith("#", StringComparison.Ordinal))
                {
                    continue;
                }

                if (line == pattern || line == normalizedPattern ||
                    string.Equals(line, normalizedPattern, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }

            return false;
        }

        private static string ReadText(string path)
        {
            try
            {
                return File.ReadAllText(path);
            }
            catch (IOException)
            {
                return string.Empty;
            }
            catch (UnauthorizedAccessException)
            {
                return string.Empty;
            }
        }

        /// <summary>Per district/chunk variant counts read from a ChunkCatalog.</summary>
        private sealed class CatalogCoverage
        {
            private readonly Dictionary<DistrictId, Dictionary<ChunkType, int>> counts = new();

            public int EntryCount { get; set; }

            public int Variants(DistrictId district, ChunkType type)
            {
                return counts.TryGetValue(district, out var byType) && byType.TryGetValue(type, out var count) ? count : 0;
            }

            public bool IsComplete(DistrictId district)
            {
                return Enum.GetValues(typeof(ChunkType)).Cast<ChunkType>().All(type => Variants(district, type) > 0);
            }

            public int DistrictVariants(DistrictId district)
            {
                return Enum.GetValues(typeof(ChunkType)).Cast<ChunkType>().Sum(type => Variants(district, type));
            }

            public void Set(DistrictId district, ChunkType type, int count)
            {
                if (!counts.TryGetValue(district, out var byType))
                {
                    byType = new Dictionary<ChunkType, int>();
                    counts[district] = byType;
                }

                byType[type] = count;
            }

            public string MissingPairs(int limit)
            {
                var missing = new List<string>();
                foreach (var district in Enum.GetValues(typeof(DistrictId)).Cast<DistrictId>())
                {
                    foreach (var type in Enum.GetValues(typeof(ChunkType)).Cast<ChunkType>())
                    {
                        if (Variants(district, type) == 0)
                        {
                            missing.Add($"{district}/{type}");
                        }
                    }
                }

                if (missing.Count == 0)
                {
                    return string.Empty;
                }

                var shown = string.Join(", ", missing.Take(limit));
                return missing.Count > limit ? $"{shown} (+{missing.Count - limit} more)" : shown + ".";
            }

            public string Describe(DistrictId district)
            {
                var total = Enum.GetValues(typeof(ChunkType)).Length;
                var covered = Enum.GetValues(typeof(ChunkType)).Cast<ChunkType>()
                    .Count(type => Variants(district, type) > 0);
                return $"{covered}/{total} chunk types, {DistrictVariants(district)} prefab(s)";
            }
        }

        /// <summary>Authored breed asset facts used by the paid-content checks.</summary>
        private sealed class BreedAsset
        {
            public string AssetPath { get; set; }
            public string Guid { get; set; }
            public string Id { get; set; }
            public string BreedName { get; set; }
            public bool IsPremium { get; set; }
            public bool IsIAP { get; set; }
            public string PackId { get; set; }
            public bool HasSprite { get; set; }
            public bool IsReferencedInScene { get; set; }

            public string Describe()
            {
                return string.IsNullOrEmpty(BreedName) ? Id ?? AssetPath : BreedName;
            }
        }

        /// <summary>Which AudioLibrary enum ids have a real clip wired.</summary>
        private sealed class LibraryCoverage
        {
            private readonly HashSet<string> wired = new();

            public int WiredCount => wired.Count;

            public List<string> Missing { get; } = new();

            public void Mark(string id)
            {
                if (!string.IsNullOrEmpty(id))
                {
                    wired.Add(id);
                }
            }

            public void Finish(string[] all)
            {
                Missing.AddRange(all.Where(id => !wired.Contains(id)));
            }
        }
    }
}
