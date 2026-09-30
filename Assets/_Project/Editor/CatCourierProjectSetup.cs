using System.IO;
using System.Linq;
using CatCourier.Core;
using CatCourier.Generation;
using CatCourier.Monetization;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace CatCourier.Editor
{
    public static class CatCourierProjectSetup
    {
        private const string ScenesPath = "Assets/_Project/Scenes";
        private const string RenderPipelinePath = "Assets/_Project/Settings/UniversalRenderPipelineAsset.asset";
        private const string RevenueCatConfigPath = "Assets/_Project/Config/RevenueCatConfig.asset";
        private const string PlaceholderAnimatorPath = "Assets/_Project/Animations/CatPlaceholder.controller";
        private const string ChunkCatalogPath = "Assets/_Project/Config/ChunkCatalog.asset";
        private const string AudioLibraryPath = "Assets/_Project/Config/AudioLibrary.asset";

        [InitializeOnLoadMethod]
        private static void ConfigureEditorPlayModeStartScene()
        {
            // Unity's PlayMode test runner needs its own empty scene. Forcing
            // Boot here loads Hub during tests and stalls the command line run.
            if (Application.isBatchMode ||
                System.Array.Exists(System.Environment.GetCommandLineArgs(),
                    argument => argument == "-runTests"))
            {
                EditorSceneManager.playModeStartScene = null;
                return;
            }

            // Build Settings only choose the first scene for a player build.
            // In the Editor, Play normally starts from the currently open scene,
            // which skips Boot when someone is reviewing Game directly.
            EditorApplication.delayCall += () =>
            {
                var boot = AssetDatabase.LoadAssetAtPath<SceneAsset>($"{ScenesPath}/{SceneNames.Boot}.unity");
                if (boot != null)
                    EditorSceneManager.playModeStartScene = boot;
            };
        }

        [MenuItem("Cat Courier/Setup/Create Missing Scenes", priority = 1)]
        public static void EnsureScenes()
        {
            Directory.CreateDirectory(ScenesPath);
            CreateSceneIfMissing(SceneNames.Boot, true);
            CreateSceneIfMissing(SceneNames.Hub, false);
            CreateSceneIfMissing(SceneNames.Game, false);

            EditorBuildSettings.scenes = new[]
            {
                new EditorBuildSettingsScene($"{ScenesPath}/{SceneNames.Boot}.unity", true),
                new EditorBuildSettingsScene($"{ScenesPath}/{SceneNames.Hub}.unity", true),
                new EditorBuildSettingsScene($"{ScenesPath}/{SceneNames.Game}.unity", true)
            };
        }

        [MenuItem("Cat Courier/Setup/Apply All", priority = -1)]
        public static void ApplyAll()
        {
            ApplyPlayerSettings();
            ApplyRenderPipeline();
            EnsureScenes();
            EnsureDay2TestScene();
            EnsureHubPlayButton();
            EnsureRevenueCatConfig();
            EnsureDay3Systems();
            EnsureDay6ContentReferences();
            RemoveMissingScripts();
            VerifyDay2Setup();
            GeneratedArtSetup.Install();
        }

        [MenuItem("Cat Courier/Setup/Apply Player Settings", priority = 0)]
        public static void ApplyPlayerSettings()
        {
            PlayerSettings.companyName = "Cat Courier";
            EditorSettings.serializationMode = SerializationMode.ForceText;
            PlayerSettings.productName = "Purrcel";
            PlayerSettings.bundleVersion = "0.1.0";
            PlayerSettings.SetApplicationIdentifier(BuildTargetGroup.Android, "com.catcourier.game");
            PlayerSettings.SetApplicationIdentifier(BuildTargetGroup.iOS, "com.catcourier.game");
            PlayerSettings.defaultInterfaceOrientation = UIOrientation.LandscapeLeft;
            PlayerSettings.allowedAutorotateToPortrait = false;
            PlayerSettings.allowedAutorotateToPortraitUpsideDown = false;
            PlayerSettings.allowedAutorotateToLandscapeLeft = true;
            PlayerSettings.allowedAutorotateToLandscapeRight = true;
            PlayerSettings.runInBackground = false;
            PlayerSettings.Android.forceInternetPermission = true;
            PlayerSettings.colorSpace = ColorSpace.Linear;
            PlayerSettings.SetScriptingBackend(BuildTargetGroup.Android, ScriptingImplementation.IL2CPP);
            PlayerSettings.SetScriptingBackend(BuildTargetGroup.iOS, ScriptingImplementation.IL2CPP);
            PlayerSettings.SetApiCompatibilityLevel(BuildTargetGroup.Android, ApiCompatibilityLevel.NET_Standard);
            PlayerSettings.SetApiCompatibilityLevel(BuildTargetGroup.iOS, ApiCompatibilityLevel.NET_Standard);
            PlayerSettings.Android.minSdkVersion = AndroidSdkVersions.AndroidApiLevel24;
            PlayerSettings.Android.targetSdkVersion = AndroidSdkVersions.AndroidApiLevel34;
            PlayerSettings.Android.targetArchitectures = AndroidArchitecture.ARMv7 | AndroidArchitecture.ARM64;
            PlayerSettings.iOS.targetOSVersionString = "14.0";
            PlayerSettings.SetApplicationIdentifier(BuildTargetGroup.Standalone, "com.catcourier.game");
            AddScriptingDefine(BuildTargetGroup.Android, "CAT_COURIER_REVENUECAT");
            AddScriptingDefine(BuildTargetGroup.iOS, "CAT_COURIER_REVENUECAT");
            QualitySettings.vSyncCount = 0;
            Physics2D.gravity = new Vector2(0f, Constants.GRAVITY);
            AssetDatabase.SaveAssets();
        }

        private static void AddScriptingDefine(BuildTargetGroup group, string define)
        {
            var current = PlayerSettings.GetScriptingDefineSymbolsForGroup(group) ?? string.Empty;
            var defines = new System.Collections.Generic.List<string>(current.Split(new[] {';'}, System.StringSplitOptions.RemoveEmptyEntries));
            if (!defines.Contains(define))
            {
                defines.Add(define);
            }

            PlayerSettings.SetScriptingDefineSymbolsForGroup(group, string.Join(";", defines));
        }

        [MenuItem("Cat Courier/Setup/Apply URP Asset", priority = 2)]
        public static void ApplyRenderPipeline()
        {
            Directory.CreateDirectory(Path.GetDirectoryName(RenderPipelinePath)!);
            var pipeline = AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>(RenderPipelinePath);
            if (pipeline == null || RendererData(pipeline) == null)
            {
                AssetDatabase.DeleteAsset(RenderPipelinePath);
                pipeline = UniversalRenderPipelineAsset.Create();
                AssetDatabase.CreateAsset(pipeline, RenderPipelinePath);
                var rendererData = RendererData(pipeline);
                if (rendererData != null)
                {
                    AssetDatabase.AddObjectToAsset(rendererData, pipeline);
                }
            }

            GraphicsSettings.defaultRenderPipeline = pipeline;
            QualitySettings.renderPipeline = pipeline;
            EditorUtility.SetDirty(pipeline);
            AssetDatabase.SaveAssets();
        }

        [MenuItem("Cat Courier/Setup/Create Day 2 Test Scene", priority = 4)]
        public static void EnsureDay2TestScene()
        {
            var path = $"{ScenesPath}/{SceneNames.Game}.unity";
            var scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
            var existingRoot = GameObject.Find("Day2TestTrack");
            if (existingRoot != null)
            {
                AlignDay2Scene(existingRoot);
                EditorSceneManager.SaveScene(scene);
                return;
            }

            var root = new GameObject("Day2TestTrack");
            var followCamera = CreateCamera(root.transform);
            CreateGround(root.transform);
            var player = CreatePlayer(root.transform);
            followCamera.Configure(player.transform);
            ConfigureWorldDeathBoundary(followCamera, player);
            CreateObstacle(root.transform, "StaticObstacle", new Vector3(12f, 0.75f, 0f), "Obstacle");
            CreateObstacle(root.transform, "WallBounceObstacle", new Vector3(18f, 0.75f, 0f), "WallBounce");
            CreateCoin(root.transform, new Vector3(2f, 0.75f, 0f), Constants.COIN_BASE_VALUE);
            CreateCoin(root.transform, new Vector3(3f, 0.75f, 0f), Constants.COIN_BASE_VALUE);
            CreateCoin(root.transform, new Vector3(9f, 0.75f, 0f), Constants.COIN_RARE_VALUE);
            EditorSceneManager.SaveScene(scene);
        }

        private static void AlignDay2Scene(GameObject root)
        {
            var player = root.GetComponentInChildren<Player.PlayerController>();
            var followCamera = root.GetComponentInChildren<FollowCamera>();
            var animator = player != null ? player.GetComponentInChildren<Animator>() : null;
            if (player == null || followCamera == null || animator == null)
            {
                throw new BuildFailedException("Day 2 scene is missing the player, animator, or follow camera.");
            }

            player.gameObject.tag = "Player";
            player.gameObject.layer = LayerMask.NameToLayer("Player");
            animator.gameObject.layer = LayerMask.NameToLayer("Player");

            var ground = root.GetComponentInChildren<Obstacles.GroundSurface>();
            if (ground != null)
            {
                ground.gameObject.tag = "Ground";
                ground.gameObject.layer = LayerMask.NameToLayer("Ground");
            }

            foreach (var obstacle in root.GetComponentsInChildren<Obstacles.StaticObstacle>(true))
            {
                obstacle.gameObject.tag = "Obstacle";
                obstacle.gameObject.layer = LayerMask.NameToLayer("Obstacle");
            }

            foreach (var obstacle in root.GetComponentsInChildren<Obstacles.BounceObstacle>(true))
            {
                obstacle.gameObject.tag = "WallBounce";
                obstacle.gameObject.layer = LayerMask.NameToLayer("Obstacle");
            }

            foreach (var pickup in root.GetComponentsInChildren<Coins.CoinPickup>(true))
            {
                pickup.gameObject.tag = pickup.Value == Constants.COIN_RARE_VALUE ? "CoinRare" : "Coin";
                pickup.gameObject.layer = LayerMask.NameToLayer("Pickup");
            }

            player.transform.position = new Vector3(0f, 0.4f, 0f);
            animator.runtimeAnimatorController = EnsurePlaceholderAnimator();
            followCamera.Configure(player.transform);
            ConfigureWorldDeathBoundary(followCamera, player);
            foreach (var pickup in root.GetComponentsInChildren<Coins.CoinPickup>(true))
            {
                pickup.transform.position = new Vector3(pickup.transform.position.x, 0.75f, 0f);
                EditorUtility.SetDirty(pickup);
            }

            EditorUtility.SetDirty(player);
            EditorUtility.SetDirty(animator);
            EditorUtility.SetDirty(followCamera);
        }

        private static void ConfigureWorldDeathBoundary(FollowCamera followCamera, Player.PlayerController player)
        {
            var camera = followCamera.GetComponent<Camera>();
            if (camera == null || !camera.orthographic)
            {
                throw new BuildFailedException("Day 2 follow camera must be orthographic.");
            }

            player.ConfigureWorldDeathY(camera.ViewportToWorldPoint(Vector3.zero).y);
        }

        private static FollowCamera CreateCamera(Transform parent)
        {
            var cameraObject = new GameObject("Main Camera");
            cameraObject.tag = "MainCamera";
            cameraObject.transform.SetParent(parent);
            cameraObject.transform.position = new Vector3(0f, 3.25f, -10f);
            var camera = cameraObject.AddComponent<Camera>();
            camera.orthographic = true;
            camera.orthographicSize = 5f;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.12f, 0.16f, 0.21f);
            return cameraObject.AddComponent<FollowCamera>();
        }

        private static void CreateGround(Transform parent)
        {
            var ground = GameObject.CreatePrimitive(PrimitiveType.Cube);
            ground.name = "Ground";
            ground.tag = "Ground";
            ground.layer = LayerMask.NameToLayer("Ground");
            ground.transform.SetParent(parent);
            ground.transform.position = new Vector3(10f, -0.25f, 0f);
            ground.transform.localScale = new Vector3(40f, 0.5f, 1f);
            UnityEngine.Object.DestroyImmediate(ground.GetComponent<BoxCollider>());
            var trigger = ground.AddComponent<BoxCollider2D>();
            trigger.isTrigger = true;
            ground.AddComponent<Obstacles.GroundSurface>();
        }

        private static Player.PlayerController CreatePlayer(Transform parent)
        {
            var player = new GameObject("Player");
            player.gameObject.tag = "Player";
            player.gameObject.layer = LayerMask.NameToLayer("Player");
            player.transform.SetParent(parent);
            player.transform.position = new Vector3(0f, 0.4f, 0f);
            var visual = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            visual.name = "CatRig";
            visual.layer = LayerMask.NameToLayer("Player");
            visual.transform.SetParent(player.transform);
            visual.transform.localScale = new Vector3(0.6f, 1f, 0.3f);
            UnityEngine.Object.DestroyImmediate(visual.GetComponent<Collider>());
            var animator = visual.AddComponent<Animator>();
            animator.runtimeAnimatorController = EnsurePlaceholderAnimator();
            var body = player.AddComponent<Rigidbody2D>();
            body.bodyType = RigidbodyType2D.Kinematic;
            var collider = player.AddComponent<BoxCollider2D>();
            collider.size = new Vector2(Constants.PLAYER_HITBOX_WIDTH, Constants.PLAYER_HITBOX_HEIGHT);
            collider.isTrigger = true;
            var input = player.AddComponent<Player.InputHandler>();
            var score = player.AddComponent<Scoring.ScoreManager>();
            var difficulty = player.AddComponent<Scoring.DifficultyManager>();
            var coins = player.AddComponent<Coins.CoinManager>();
            var controller = player.AddComponent<Player.PlayerController>();
            var actions = AssetDatabase.LoadAssetAtPath<UnityEngine.InputSystem.InputActionAsset>("Assets/_Project/Input/CatCourierControls.inputactions");
            input.Configure(actions);
            controller.Initialize();
            return controller;
        }

        private static void CreateObstacle(Transform parent, string name, Vector3 position, string tag)
        {
            var obstacle = GameObject.CreatePrimitive(PrimitiveType.Cube);
            obstacle.name = name;
            obstacle.tag = tag;
            obstacle.layer = LayerMask.NameToLayer("Obstacle");
            obstacle.transform.SetParent(parent);
            obstacle.transform.position = position;
            obstacle.transform.localScale = new Vector3(0.8f, 1f, 1f);
            UnityEngine.Object.DestroyImmediate(obstacle.GetComponent<BoxCollider>());
            var trigger = obstacle.AddComponent<BoxCollider2D>();
            trigger.isTrigger = true;
            if (tag == "WallBounce")
            {
                obstacle.AddComponent<Obstacles.BounceObstacle>();
            }
            else
            {
                obstacle.AddComponent<Obstacles.StaticObstacle>();
            }
        }

        private static void CreateCoin(Transform parent, Vector3 position, int value)
        {
            var coin = GameObject.CreatePrimitive(PrimitiveType.Quad);
            coin.name = value == Constants.COIN_RARE_VALUE ? "RareCoin" : "Coin";
            coin.tag = value == Constants.COIN_RARE_VALUE ? "CoinRare" : "Coin";
            coin.layer = LayerMask.NameToLayer("Pickup");
            coin.transform.SetParent(parent);
            coin.transform.position = position;
            coin.transform.localScale = Vector3.one * 0.35f;
            UnityEngine.Object.DestroyImmediate(coin.GetComponent<MeshCollider>());
            var trigger = coin.AddComponent<CircleCollider2D>();
            trigger.isTrigger = true;
            trigger.radius = 0.5f;
            var pickup = coin.AddComponent<Coins.CoinPickup>();
            pickup.Configure(parent.GetComponentInChildren<Coins.CoinManager>(), value, parent.GetComponentInChildren<Player.PlayerController>());
        }

        [MenuItem("Cat Courier/Setup/Create Hub Start Button", priority = 5)]
        public static void EnsureHubPlayButton()
        {
            var path = $"{ScenesPath}/{SceneNames.Hub}.unity";
            var scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
            var root = GameObject.Find("HubRoot") ?? new GameObject("HubRoot");
            GetOrAdd<Weather.WeatherManager>(root);
            GetOrAdd<UI.HubPresenter>(root);
            GetOrAdd<UI.UpgradeListPresenter>(root);
            GetOrAdd<UI.CatSelectorPresenter>(root);
            GetOrAdd<UI.LeaderboardPresenter>(root);
            GetOrAdd<UI.PaywallPresenter>(root);
            GetOrAdd<Audio.RunAudioRelay>(root);
            var legacyStart = GameObject.Find("HubStartButton");
            if (legacyStart != null)
            {
                UnityEngine.Object.DestroyImmediate(legacyStart);
            }

            EditorSceneManager.SaveScene(scene);
        }

        private static RuntimeAnimatorController EnsurePlaceholderAnimator()
        {
            Directory.CreateDirectory(Path.GetDirectoryName(PlaceholderAnimatorPath)!);
            var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(PlaceholderAnimatorPath);
            if (controller == null)
            {
                controller = AnimatorController.CreateAnimatorControllerAtPath(PlaceholderAnimatorPath);
            }

            var parameterNames = new[] { "Speed", "IsGrounded", "IsSliding", "Jump", "Die", "WallBounce" };
            for (var index = controller.parameters.Length - 1; index >= 0; index--)
            {
                if (System.Array.IndexOf(parameterNames, controller.parameters[index].name) < 0)
                {
                    controller.RemoveParameter(index);
                }
            }

            AddAnimatorParameter(controller, "Speed", AnimatorControllerParameterType.Float);
            AddAnimatorParameter(controller, "IsGrounded", AnimatorControllerParameterType.Bool);
            AddAnimatorParameter(controller, "IsSliding", AnimatorControllerParameterType.Bool);
            AddAnimatorParameter(controller, "Jump", AnimatorControllerParameterType.Trigger);
            AddAnimatorParameter(controller, "Die", AnimatorControllerParameterType.Trigger);
            AddAnimatorParameter(controller, "WallBounce", AnimatorControllerParameterType.Trigger);

            var stateMachine = controller.layers[0].stateMachine;
            var stateNames = new[] { "CatIdle", "CatRun", "CatJump", "CatSlide", "CatDie" };
            var existingStates = stateMachine.states;
            for (var index = existingStates.Length - 1; index >= 0; index--)
            {
                if (System.Array.IndexOf(stateNames, existingStates[index].state.name) < 0)
                {
                    stateMachine.RemoveState(existingStates[index].state);
                }
            }

            var idle = EnsureAnimatorState(stateMachine, "CatIdle");
            var run = EnsureAnimatorState(stateMachine, "CatRun");
            var jump = EnsureAnimatorState(stateMachine, "CatJump");
            var slide = EnsureAnimatorState(stateMachine, "CatSlide");
            var die = EnsureAnimatorState(stateMachine, "CatDie");
            stateMachine.defaultState = idle;
            stateMachine.anyStateTransitions = System.Array.Empty<AnimatorStateTransition>();
            foreach (var state in stateMachine.states)
            {
                state.state.transitions = System.Array.Empty<AnimatorStateTransition>();
            }

            var runTransition = AddAnyStateTransition(stateMachine, run);
            runTransition.AddCondition(AnimatorConditionMode.If, 0f, "IsGrounded");
            runTransition.AddCondition(AnimatorConditionMode.IfNot, 0f, "IsSliding");
            var slideTransition = AddAnyStateTransition(stateMachine, slide);
            slideTransition.AddCondition(AnimatorConditionMode.If, 0f, "IsSliding");
            var jumpTransition = AddAnyStateTransition(stateMachine, jump);
            jumpTransition.AddCondition(AnimatorConditionMode.If, 0f, "Jump");
            var wallBounceTransition = AddAnyStateTransition(stateMachine, jump);
            wallBounceTransition.AddCondition(AnimatorConditionMode.If, 0f, "WallBounce");
            var dieTransition = AddAnyStateTransition(stateMachine, die);
            dieTransition.AddCondition(AnimatorConditionMode.If, 0f, "Die");

            EditorUtility.SetDirty(controller);
            AssetDatabase.SaveAssets();
            return controller;
        }

        private static AnimatorState EnsureAnimatorState(AnimatorStateMachine stateMachine, string name)
        {
            foreach (var child in stateMachine.states)
            {
                if (child.state.name == name)
                {
                    return child.state;
                }
            }

            return stateMachine.AddState(name);
        }

        private static AnimatorStateTransition AddAnyStateTransition(AnimatorStateMachine stateMachine, AnimatorState destination)
        {
            var transition = stateMachine.AddAnyStateTransition(destination);
            transition.hasExitTime = false;
            transition.duration = 0f;
            transition.canTransitionToSelf = false;
            return transition;
        }

        private static void AddAnimatorParameter(AnimatorController controller, string name, AnimatorControllerParameterType type)
        {
            for (var index = 0; index < controller.parameters.Length; index++)
            {
                var parameter = controller.parameters[index];
                if (parameter.name != name)
                {
                    continue;
                }

                if (parameter.type == type)
                {
                    return;
                }

                controller.RemoveParameter(index);
                break;
            }

            controller.AddParameter(name, type);
        }

        [MenuItem("Cat Courier/Setup/Create Day 3 Systems", priority = 7)]
        public static void EnsureDay3Systems()
        {
            Directory.CreateDirectory(Path.GetDirectoryName(ChunkCatalogPath)!);
            var catalog = AssetDatabase.LoadAssetAtPath<ChunkCatalog>(ChunkCatalogPath);
            if (catalog == null)
            {
                catalog = ScriptableObject.CreateInstance<ChunkCatalog>();
                AssetDatabase.CreateAsset(catalog, ChunkCatalogPath);
                AssetDatabase.SaveAssets();
            }

            var scene = EditorSceneManager.OpenScene($"{ScenesPath}/{SceneNames.Game}.unity", OpenSceneMode.Single);
            var root = GameObject.Find("Day2TestTrack") ?? new GameObject("Day2TestTrack");
            var player = root.GetComponentInChildren<Player.PlayerController>();
            if (player == null)
            {
                throw new BuildFailedException("Day 3 systems require the Day 2 player.");
            }

            var camera = Camera.main;
            var fallback = root.transform.Find("Day2FallbackContent");
            if (fallback == null)
            {
                var created = new GameObject("Day2FallbackContent");
                created.transform.SetParent(root.transform, false);
                fallback = created.transform;
            }

            var fallbackChildren = new System.Collections.Generic.List<Transform>();
            foreach (Transform child in root.transform)
            {
                if (child == fallback)
                {
                    continue;
                }

                if (child.GetComponent<Obstacles.GroundSurface>() != null ||
                    child.GetComponent<Obstacles.StaticObstacle>() != null ||
                    child.GetComponent<Obstacles.BounceObstacle>() != null ||
                    child.GetComponent<Coins.CoinPickup>() != null)
                {
                    fallbackChildren.Add(child);
                }
            }

            foreach (var child in fallbackChildren)
            {
                child.SetParent(fallback, false);
            }

            var weather = GetOrAdd<Weather.WeatherManager>(root);
            var packages = GetOrAdd<Packages.PackageManager>(root);
            var generator = GetOrAdd<Generation.ProceduralGenerator>(root);
            var chunks = GetOrAdd<Generation.ChunkManager>(root);
            var coordinator = GetOrAdd<RunCoordinator>(root);
            var hud = GetOrAdd<UI.RunHudPresenter>(root);
            GetOrAdd<UI.PauseMenuPresenter>(root);
            GetOrAdd<UI.DeathScreenPresenter>(root);
            GetOrAdd<UI.StoryCardPresenter>(root);
            GetOrAdd<Audio.RunAudioRelay>(root);
            var container = root.transform.Find("ChunkContainer");
            if (container == null)
            {
                var created = new GameObject("ChunkContainer");
                created.transform.SetParent(root.transform, false);
            }

            SetObjectReference(chunks, "catalog", catalog);
            SetObjectReference(chunks, "generator", generator);
            SetObjectReference(chunks, "player", player != null ? player.transform : null);
            SetObjectReference(chunks, "gameplayCamera", camera);
            SetObjectReference(chunks, "fallbackContent", fallback);
            SetBool(chunks, "startOnAwake", false);
            SetObjectReference(coordinator, "player", player);
            SetObjectReference(coordinator, "chunkManager", chunks);
            SetObjectReference(coordinator, "generator", generator);
            SetObjectReference(coordinator, "catalog", catalog);
            SetObjectReference(coordinator, "weather", weather);
            SetObjectReference(coordinator, "packages", packages);
            SetObjectReference(coordinator, "score", player != null ? player.GetComponent<Scoring.ScoreManager>() : null);
            SetObjectReference(coordinator, "coins", player != null ? player.GetComponent<Coins.CoinManager>() : null);
            SetObjectReference(hud, "weather", weather);
            SetObjectReference(hud, "packages", packages);
            SetObjectReference(hud, "coordinator", coordinator);
            EditorSceneManager.SaveScene(scene);
        }

        private static T GetOrAdd<T>(GameObject host) where T : Component
        {
            var component = host.GetComponent<T>();
            return component != null ? component : host.AddComponent<T>();
        }

        private static void SetObjectReference(Component component, string propertyName, UnityEngine.Object value)
        {
            var serialized = new SerializedObject(component);
            var property = serialized.FindProperty(propertyName);
            if (property != null)
            {
                property.objectReferenceValue = value;
                serialized.ApplyModifiedPropertiesWithoutUndo();
            }
        }

        private static void SetBool(Component component, string propertyName, bool value)
        {
            var serialized = new SerializedObject(component);
            var property = serialized.FindProperty(propertyName);
            if (property != null)
            {
                property.boolValue = value;
                serialized.ApplyModifiedPropertiesWithoutUndo();
            }
        }

        [MenuItem("Cat Courier/Setup/Verify Day 2", priority = 6)]
        public static void VerifyDay2Setup()
        {
            var gameScene = EditorSceneManager.OpenScene($"{ScenesPath}/{SceneNames.Game}.unity", OpenSceneMode.Single);
            var root = GameObject.Find("Day2TestTrack");
            if (root == null)
            {
                throw new BuildFailedException("Day 2 scene root is missing.");
            }

            var player = root.GetComponentInChildren<Player.PlayerController>();
            var ground = root.GetComponentInChildren<Obstacles.GroundSurface>();
            var animator = player != null ? player.GetComponentInChildren<Animator>() : null;
            if (player == null || ground == null || animator == null || player.Hitbox == null)
            {
                throw new BuildFailedException("Day 2 player, ground, collider, or animator is missing.");
            }

            if (player.gameObject.tag != "Player" || player.gameObject.layer != LayerMask.NameToLayer("Player") ||
                ground.gameObject.tag != "Ground" || ground.gameObject.layer != LayerMask.NameToLayer("Ground"))
            {
                throw new BuildFailedException("Day 2 player or ground tag/layer assignment is invalid.");
            }

            var playerBottom = player.transform.position.y - player.Hitbox.bounds.extents.y;
            var groundTop = ground.GetComponent<Collider2D>().bounds.max.y;
            if (playerBottom > groundTop)
            {
                throw new BuildFailedException("Day 2 player does not overlap the ground trigger at spawn.");
            }

            if (root.GetComponentInChildren<Obstacles.StaticObstacle>() == null ||
                root.GetComponentInChildren<Obstacles.BounceObstacle>() == null)
            {
                throw new BuildFailedException("Day 2 obstacle behaviors are missing.");
            }

            var pickups = root.GetComponentsInChildren<Coins.CoinPickup>(true);
            if (pickups.Length < 3 || pickups.Any(pickup => pickup.gameObject.layer != LayerMask.NameToLayer("Pickup")))
            {
                throw new BuildFailedException("Day 2 coin pickups or Pickup layer assignment are missing.");
            }

            var controller = animator.runtimeAnimatorController as AnimatorController;
            var parameters = controller == null
                ? System.Array.Empty<AnimatorControllerParameter>()
                : controller.parameters;
            var parameterNames = new System.Collections.Generic.HashSet<string>(parameters.Select(parameter => parameter.name));
            var requiredParameters = new[] { "Speed", "IsGrounded", "IsSliding", "Jump", "Die", "WallBounce" };
            if (controller == null || requiredParameters.Any(name => !parameterNames.Contains(name)) || parameterNames.Count != requiredParameters.Length)
            {
                throw new BuildFailedException("Animator parameters do not match the Day 2 contract.");
            }

            var stateNames = new System.Collections.Generic.HashSet<string>(controller.layers[0].stateMachine.states.Select(child => child.state.name));
            var requiredStates = new[] { "CatIdle", "CatRun", "CatJump", "CatSlide", "CatDie" };
            if (requiredStates.Any(name => !stateNames.Contains(name)) || stateNames.Count != requiredStates.Length)
            {
                throw new BuildFailedException("Animator states do not match the Day 2 contract.");
            }

            var actions = AssetDatabase.LoadAssetAtPath<UnityEngine.InputSystem.InputActionAsset>("Assets/_Project/Input/CatCourierControls.inputactions");
            if (actions == null || actions.FindActionMap("Gameplay", true) == null)
            {
                throw new BuildFailedException("Gameplay InputActionAsset is missing.");
            }

            Debug.Log("Day 2 setup verification passed.");
        }

        /// <summary>
        /// Wires the authored content assets the runtime expects. Creates an empty AudioLibrary so the
        /// field is always assignable, and assigns the chunk catalog so paid-content gating can see it.
        /// </summary>
        [MenuItem("Cat Courier/Setup/Wire Day 6 Content References", priority = 8)]
        public static void EnsureDay6ContentReferences()
        {
            Directory.CreateDirectory(Path.GetDirectoryName(AudioLibraryPath)!);
            var library = AssetDatabase.LoadAssetAtPath<Audio.AudioLibrary>(AudioLibraryPath);
            if (library == null)
            {
                library = ScriptableObject.CreateInstance<Audio.AudioLibrary>();
                AssetDatabase.CreateAsset(library, AudioLibraryPath);
                AssetDatabase.SaveAssets();
            }

            var catalog = AssetDatabase.LoadAssetAtPath<ChunkCatalog>(ChunkCatalogPath);
            var mixer = AssetDatabase.FindAssets("t:AudioMixer", new[] { "Assets/_Project" })
                .Select(AssetDatabase.GUIDToAssetPath)
                .Select(AssetDatabase.LoadAssetAtPath<UnityEngine.Audio.AudioMixer>)
                .FirstOrDefault(mixer => mixer != null);

            var bootPath = $"{ScenesPath}/{SceneNames.Boot}.unity";
            var scene = EditorSceneManager.OpenScene(bootPath, OpenSceneMode.Single);
            var systems = UnityEngine.Object.FindObjectOfType<PersistentSystems>();
            if (systems != null)
            {
                var serialized = new SerializedObject(systems);
                AssignIfPresent(serialized, "chunkCatalog", catalog);
                AssignIfPresent(serialized, "audioLibrary", library);
                AssignIfPresent(serialized, "audioMixer", mixer);
                serialized.ApplyModifiedPropertiesWithoutUndo();
                EditorSceneManager.SaveScene(scene);
            }

            AssetDatabase.SaveAssets();
        }

        private static void AssignIfPresent(SerializedObject serialized, string propertyName, UnityEngine.Object value)
        {
            var property = serialized.FindProperty(propertyName);
            if (property != null)
            {
                property.objectReferenceValue = value;
            }
        }

        [MenuItem("Cat Courier/Setup/Create Local RevenueCat Config", priority = 3)]
        public static void EnsureRevenueCatConfig()
        {
            Directory.CreateDirectory(Path.GetDirectoryName(RevenueCatConfigPath)!);
            var config = AssetDatabase.LoadAssetAtPath<RevenueCatConfig>(RevenueCatConfigPath);
            if (config == null)
            {
                config = ScriptableObject.CreateInstance<RevenueCatConfig>();
                AssetDatabase.CreateAsset(config, RevenueCatConfigPath);
            }

            var bootPath = $"{ScenesPath}/{SceneNames.Boot}.unity";
            var scene = EditorSceneManager.OpenScene(bootPath, OpenSceneMode.Single);
            var systems = UnityEngine.Object.FindObjectOfType<PersistentSystems>();
            if (systems != null)
            {
                var serialized = new SerializedObject(systems);
                serialized.FindProperty("revenueCatConfig").objectReferenceValue = config;
                serialized.ApplyModifiedPropertiesWithoutUndo();
                EditorSceneManager.SaveScene(scene);
            }

            AssetDatabase.SaveAssets();
        }

        [MenuItem("Cat Courier/Setup/Use Real RevenueCat for Next Gen Demo", priority = 12)]
        public static void PrepareNextGenDemo()
        {
            EnsureRevenueCatConfig();
            var config = AssetDatabase.LoadAssetAtPath<RevenueCatConfig>(RevenueCatConfigPath);
            var serialized = new SerializedObject(config);
            serialized.FindProperty("environment").enumValueIndex = (int)RevenueCatEnvironment.DevelopmentSandbox;
            serialized.FindProperty("backend").enumValueIndex = (int)RevenueCatBackendSelection.Real;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(config);
            AssetDatabase.SaveAssets();
            Selection.activeObject = config;
            Debug.Log("Real RevenueCat selected for the Android development demo. Enter the Test Store public key in the selected local config asset, then use Cat Courier > Build > Android Next Gen Demo APK.");
        }

        [MenuItem("Cat Courier/Build/Android Next Gen Demo APK")]
        public static void BuildAndroidNextGenDemo()
        {
            var config = AssetDatabase.LoadAssetAtPath<RevenueCatConfig>(RevenueCatConfigPath);
            if (config == null || !config.IsAndroidDemoReady)
            {
                throw new BuildFailedException("Next Gen demo requires sandbox mode, the real RevenueCat backend, and a Test Store or Google sandbox public key. Run Cat Courier > Setup > Use Real RevenueCat for Next Gen Demo, then enter the key in the selected local config asset.");
            }

            BuildAndroid();
        }

        /// <summary>
        /// Strips MonoBehaviours whose script was deleted, so removing a component class never leaves
        /// a missing script behind in a saved scene or prefab.
        /// </summary>
        private static void RemoveMissingScripts()
        {
            var guids = AssetDatabase.FindAssets("t:Scene", new[] { ScenesPath });
            guids = guids.Concat(AssetDatabase.FindAssets("t:Prefab", new[] { "Assets/_Project" })).ToArray();
            var cleaned = 0;
            foreach (var guid in guids)
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                if (AssetDatabase.IsValidFolder(path))
                {
                    continue;
                }

                if (path.EndsWith(".unity", System.StringComparison.OrdinalIgnoreCase))
                {
                    foreach (var go in AssetDatabase.LoadAllAssetsAtPath(path))
                    {
                        if (go is GameObject sceneGo)
                        {
                            cleaned += GameObjectUtility.RemoveMonoBehavioursWithMissingScript(sceneGo);
                        }
                    }

                    continue;
                }

                var prefabRoot = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (prefabRoot == null)
                {
                    continue;
                }

                cleaned += GameObjectUtility.RemoveMonoBehavioursWithMissingScript(prefabRoot);
            }

            if (cleaned > 0)
            {
                AssetDatabase.SaveAssets();
                Debug.Log($"Removed {cleaned} missing script reference(s).");
            }
        }

        [MenuItem("Cat Courier/Build/Android Development APK")]
        public static void BuildAndroid()
        {
            ApplyAll();
            Directory.CreateDirectory("Assets/Plugins/Android");
            var sdkRoot = System.Environment.GetEnvironmentVariable("CAT_COURIER_ANDROID_SDK");
            if (!string.IsNullOrWhiteSpace(sdkRoot))
            {
                ConfigureAndroidSdk(sdkRoot);
            }

            var outputPath = Path.GetFullPath(Path.Combine(Application.dataPath, "../Builds/Android/CatCourier.apk"));
            Directory.CreateDirectory(Path.GetDirectoryName(outputPath)!);
            EditorUserBuildSettings.buildAppBundle = false;
            var report = BuildPipeline.BuildPlayer(EditorBuildSettings.scenes, outputPath, BuildTarget.Android, BuildOptions.Development);
            if (report.summary.result != BuildResult.Succeeded)
            {
                throw new BuildFailedException($"Android build failed: {report.summary.result}");
            }
        }

        private static void ConfigureAndroidSdk(string sdkRoot)
        {
            var settingsType = System.Type.GetType("UnityEditor.Android.AndroidExternalToolsSettings, UnityEditor.Android.Extensions") ??
                               typeof(UnityEditor.Editor).Assembly.GetType("UnityEditor.Android.AndroidExternalToolsSettings");
            var property = settingsType?.GetProperty("sdkRootPath", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
            if (property == null)
            {
                throw new BuildFailedException("Unity Android SDK settings API is unavailable.");
            }

            property.SetValue(null, sdkRoot, null);
        }

        private static UniversalRendererData RendererData(UniversalRenderPipelineAsset pipeline)
        {
            var property = new SerializedObject(pipeline).FindProperty("m_RendererDataList");
            return property.arraySize == 0 ? null : property.GetArrayElementAtIndex(0).objectReferenceValue as UniversalRendererData;
        }

        private static void CreateSceneIfMissing(string sceneName, bool addPersistentSystems)
        {
            var path = $"{ScenesPath}/{sceneName}.unity";
            if (File.Exists(path))
            {
                return;
            }

            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            if (addPersistentSystems)
            {
                var root = new GameObject("PersistentSystems");
                root.AddComponent<PersistentSystems>();
            }

            EditorSceneManager.SaveScene(scene, path);
            AssetDatabase.Refresh();
        }
    }
}
