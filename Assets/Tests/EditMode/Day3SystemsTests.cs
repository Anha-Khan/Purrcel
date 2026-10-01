using System;
using System.Collections.Generic;
using System.Reflection;
using CatCourier.Coins;
using CatCourier.Core;
using CatCourier.Generation;
using CatCourier.Obstacles;
using CatCourier.Packages;
using CatCourier.Player;
using CatCourier.Scoring;
using CatCourier.Weather;
using NUnit.Framework;
using UnityEngine;
using Object = UnityEngine.Object;

namespace CatCourier.Tests
{
    public sealed class Day3SystemsTests
    {
        [Test]
        public void ChunkWeights_MatchDifficultyAnchors()
        {
            Assert.That(ProceduralGenerator.GetChunkWeight(ChunkType.SmallGap, 0), Is.EqualTo(30f));
            Assert.That(ProceduralGenerator.GetChunkWeight(ChunkType.SmallGap, 5), Is.EqualTo(20f));
            Assert.That(ProceduralGenerator.GetChunkWeight(ChunkType.SmallGap, 10), Is.EqualTo(10f));
            Assert.That(ProceduralGenerator.GetChunkWeight(ChunkType.MediumGap, 0), Is.EqualTo(25f));
            Assert.That(ProceduralGenerator.GetChunkWeight(ChunkType.LargeGap, 5), Is.EqualTo(15f));
            Assert.That(ProceduralGenerator.GetChunkWeight(ChunkType.ObstacleDense, 10), Is.EqualTo(20f));
            Assert.That(ProceduralGenerator.GetChunkWeight(ChunkType.ObstacleSparse, 5), Is.EqualTo(10f));
            Assert.That(ProceduralGenerator.GetChunkWeight(ChunkType.HighPlatform, 5), Is.EqualTo(10f));
            Assert.That(ProceduralGenerator.GetChunkWeight(ChunkType.DropDown, 10), Is.EqualTo(10f));
        }

        [Test]
        public void Catalog_AllowsMultipleVariantsPerDistrictAndType()
        {
            var catalog = ScriptableObject.CreateInstance<ChunkCatalog>();
            var first = CreateChunk(ChunkType.SmallGap, "Chunk_SmallGap_OT_01");
            var second = CreateChunk(ChunkType.SmallGap, "Chunk_SmallGap_OT_02");
            try
            {
                catalog.SetEntries(new[]
                {
                    new ChunkCatalog.Entry { district = DistrictId.OldTown, type = ChunkType.SmallGap, prefab = first },
                    new ChunkCatalog.Entry { district = DistrictId.OldTown, type = ChunkType.SmallGap, prefab = second }
                });

                Assert.That(catalog.GetVariantCount(DistrictId.OldTown, ChunkType.SmallGap), Is.EqualTo(2));
            }
            finally
            {
                Object.DestroyImmediate(second);
                Object.DestroyImmediate(first);
                Object.DestroyImmediate(catalog);
            }
        }

        [Test]
        public void Generator_ForcesCheckpointThenSparseAndReturnsStoryBeat()
        {
            var catalog = CreateFullCatalog();
            var generatorObject = new GameObject("ProceduralGeneratorTest");
            try
            {
                var generator = generatorObject.AddComponent<ProceduralGenerator>();
                generator.Initialize(catalog, 7, DistrictId.OldTown, new[] { DistrictId.OldTown }, 0);
                for (var index = 0; index < 15; index++)
                {
                    Assert.That(generator.TrySelectNext(out _), Is.True);
                }

                Assert.That(generator.TrySelectNext(out var checkpoint), Is.True);
                Assert.That(checkpoint.Type, Is.EqualTo(ChunkType.Checkpoint));
                Assert.That(checkpoint.CheckpointIndex, Is.EqualTo(1));
                Assert.That(checkpoint.StoryBeat.HasStory, Is.True);

                Assert.That(generator.TrySelectNext(out var sparse), Is.True);
                Assert.That(sparse.Type, Is.EqualTo(ChunkType.ObstacleSparse));
            }
            finally
            {
                Object.DestroyImmediate(generatorObject);
                DestroyCatalog(catalog);
            }
        }

        [Test]
        public void Generator_ExcludesLargeGapWithoutDoubleJump()
        {
            var catalog = CreateFullCatalog();
            var generatorObject = new GameObject("ProceduralGeneratorGapTest");
            try
            {
                var generator = generatorObject.AddComponent<ProceduralGenerator>();
                generator.Initialize(catalog, 2, DistrictId.OldTown, new[] { DistrictId.OldTown }, 0);
                Assert.That(generator.GetSelectionWeight(ChunkType.LargeGap), Is.Zero);

                generator.SetDoubleJumpLevel(1);
                Assert.That(generator.GetSelectionWeight(ChunkType.LargeGap), Is.GreaterThan(0f));
            }
            finally
            {
                Object.DestroyImmediate(generatorObject);
                DestroyCatalog(catalog);
            }
        }

        [Test]
        public void Generator_SkipsMissingCheckpointAndUsesAvailableChunk()
        {
            var catalog = ScriptableObject.CreateInstance<ChunkCatalog>();
            var sparse = CreateChunk(ChunkType.ObstacleSparse, "Chunk_ObstacleSparse_OT_01");
            try
            {
                catalog.SetEntries(new[]
                {
                    new ChunkCatalog.Entry { district = DistrictId.OldTown, type = ChunkType.ObstacleSparse, prefab = sparse }
                });
                var generatorObject = new GameObject("ProceduralGeneratorMissingCheckpointTest");
                try
                {
                    var generator = generatorObject.AddComponent<ProceduralGenerator>();
                    generator.Initialize(catalog, 4, DistrictId.OldTown, new[] { DistrictId.OldTown }, 1);
                    for (var index = 0; index < 15; index++)
                    {
                        generator.TrySelectNext(out _);
                    }

                    Assert.That(generator.TrySelectNext(out var selection), Is.True);
                    Assert.That(selection.Type, Is.EqualTo(ChunkType.ObstacleSparse));
                }
                finally
                {
                    Object.DestroyImmediate(generatorObject);
                }
            }
            finally
            {
                Object.DestroyImmediate(sparse);
                Object.DestroyImmediate(catalog);
            }
        }

        [Test]
        public void PackageSelection_UsesDocumentedDistribution()
        {
            var counts = new Dictionary<PackageType, int>();
            var random = new System.Random(52);
            for (var index = 0; index < 10000; index++)
            {
                var package = PackageManager.SelectWeightedPackage(random);
                counts.TryGetValue(package, out var count);
                counts[package] = count + 1;
            }

            Assert.That(counts[PackageType.Normal], Is.InRange(3800, 4200));
            Assert.That(counts[PackageType.Fragile], Is.InRange(1800, 2200));
            Assert.That(counts[PackageType.Heavy], Is.InRange(1800, 2200));
            Assert.That(counts[PackageType.Urgent], Is.InRange(1800, 2200));
        }

        [Test]
        public void DistrictEligibility_FollowsDistancePremiumAndSavedUnlocks()
        {
            Assert.That(RunLoadoutService.GetEligibleDistricts(0f, false, Array.Empty<string>()), Is.EquivalentTo(new[] { DistrictId.OldTown, DistrictId.Downtown }));
            Assert.That(RunLoadoutService.GetEligibleDistricts(RunLoadoutService.HarbourReachDistance, false, Array.Empty<string>()), Does.Contain(DistrictId.Harbour));
            Assert.That(RunLoadoutService.GetEligibleDistricts(RunLoadoutService.SuburbsReachDistance, false, Array.Empty<string>()), Does.Contain(DistrictId.Suburbs));
            Assert.That(RunLoadoutService.GetEligibleDistricts(0f, true, Array.Empty<string>()), Does.Contain(DistrictId.Suburbs));
        }

        [Test]
        public void Weather_UsesDocumentedDistribution()
        {
            var counts = new Dictionary<WeatherType, int>();
            var random = new System.Random(44);
            for (var index = 0; index < 10000; index++)
            {
                var weather = WeatherManager.SelectWeightedWeather(random);
                counts.TryGetValue(weather, out var count);
                counts[weather] = count + 1;
            }

            Assert.That(counts[WeatherType.Clear], Is.InRange(3800, 4200));
            Assert.That(counts[WeatherType.Rain], Is.InRange(2300, 2700));
            Assert.That(counts[WeatherType.Night], Is.InRange(2300, 2700));
            Assert.That(counts[WeatherType.Wind], Is.InRange(800, 1200));
        }

        [Test]
        public void Weather_ActivatesRainAndWindContracts()
        {
            var weatherObject = new GameObject("WeatherManagerTest");
            try
            {
                var weather = weatherObject.AddComponent<WeatherManager>();
                weather.SetSeed(10);
                for (var index = 0; index < 100 && weather.NextRunWeather != WeatherType.Rain; index++)
                {
                    weather.SelectNextWeather();
                }

                Assert.That(weather.NextRunWeather, Is.EqualTo(WeatherType.Rain));
                weather.ActivateNextWeather();
                Assert.That(weather.SpeedMultiplier, Is.EqualTo(0.92f));
                Assert.That(weather.LandingSlideDistance, Is.EqualTo(Constants.RAIN_LANDING_SLIDE));

                weather.ActivateNextWeather();
                weather.SetSeed(1);
                for (var index = 0; index < 100; index++)
                {
                    weather.ActivateNextWeather();
                    if (weather.Current == WeatherType.Wind)
                    {
                        break;
                    }
                }

                Assert.That(weather.Current, Is.EqualTo(WeatherType.Wind));
                Assert.That(Mathf.Abs(weather.AirborneDriftAcceleration), Is.EqualTo(Constants.WIND_DRIFT_FORCE));
                Assert.That(weather.WindDirection, Is.EqualTo(-1).Or.EqualTo(1));
            }
            finally
            {
                Object.DestroyImmediate(weatherObject);
            }
        }

        [Test]
        public void ScoreManager_AppliesAndExpiresDeliveryPulse()
        {
            var scoreObject = new GameObject("DeliveryPulseScoreTest");
            try
            {
                var score = scoreObject.AddComponent<ScoreManager>();
                score.SetDistance(10f);
                score.AddPackage();
                var baseScore = score.Score;
                score.SetDeliveryPulse(1.5f, 2f);
                Assert.That(score.Score, Is.EqualTo((long)Math.Round(baseScore * 1.5d)));
                score.Simulate(2f);
                Assert.That(score.DeliveryScoreMultiplier, Is.EqualTo(1f));
                Assert.That(score.Score, Is.EqualTo(baseScore));
            }
            finally
            {
                Object.DestroyImmediate(scoreObject);
            }
        }

        [Test]
        public void Package_ExplicitModifiersAndUrgentTimer()
        {
            var packageObject = new GameObject("PackageManagerTest");
            try
            {
                var packages = packageObject.AddComponent<PackageManager>();
                packages.AssignSpecificPackage(PackageType.Fragile, false, 100f);
                Assert.That(packages.JumpMultiplier, Is.EqualTo(0.9f));
                Assert.That(packages.IsFragile, Is.True);

                packages.AssignSpecificPackage(PackageType.Heavy, false, 100f);
                Assert.That(packages.JumpMultiplier, Is.EqualTo(0.75f));

                packages.AssignSpecificPackage(PackageType.Urgent, false, 100f);
                Assert.That(packages.SpeedMultiplier, Is.EqualTo(1.05f));
                Assert.That(packages.UrgentTimeLimit, Is.EqualTo(48f).Within(0.001f));
                packages.Simulate(48f);
                Assert.That(packages.State, Is.EqualTo(PackageState.Lost));

                packages.AssignSpecificPackage(PackageType.Urgent, true, 100f);
                Assert.That(packages.UrgentTimeLimit, Is.EqualTo(63f).Within(0.001f));
            }
            finally
            {
                Object.DestroyImmediate(packageObject);
            }
        }

        [Test]
        public void CoinPickup_ResetsWhenChunkIsReused()
        {
            var managerObject = new GameObject("CoinReuseManagerTest");
            var coinObject = new GameObject("CoinReuseTest");
            try
            {
                var manager = managerObject.AddComponent<CoinManager>();
                var pickup = coinObject.AddComponent<CoinPickup>();
                pickup.Configure(manager, Constants.COIN_BASE_VALUE);
                Assert.That(pickup.Collect(), Is.True);
                Assert.That(pickup.Collected, Is.True);

                pickup.OnChunkActivated();
                Assert.That(pickup.Collected, Is.False);
                Assert.That(pickup.Collect(), Is.True);
            }
            finally
            {
                Object.DestroyImmediate(coinObject);
                Object.DestroyImmediate(managerObject);
            }
        }

        [Test]
        public void Package_AdditionalSlotsRefillAtCheckpoint()
        {
            var packageObject = new GameObject("PackageRefillTest");
            try
            {
                var packages = packageObject.AddComponent<PackageManager>();
                packages.AssignSpecificPackage(PackageType.Normal, false, 0f, 1);
                packages.SetTargetSlotCount(2);
                Assert.That(packages.RefillEmptySlots(false, 300f), Is.EqualTo(1));
                Assert.That(packages.Slots.Count, Is.EqualTo(2));
            }
            finally
            {
                Object.DestroyImmediate(packageObject);
            }
        }

        [Test]
        public void Package_DeliveryAwardsFifteenCoinsPerPackage()
        {
            var packageObject = new GameObject("PackageDeliveryTest");
            var coinObject = new GameObject("CoinManagerDeliveryTest");
            try
            {
                var packages = packageObject.AddComponent<PackageManager>();
                var coins = coinObject.AddComponent<CoinManager>();
                packages.AssignSpecificPackage(PackageType.Normal, false, 0f);
                var result = packages.DeliverAtCheckpoint();
                coins.AwardRunCoins(result.CoinsAwarded);

                Assert.That(result.PackagesDelivered, Is.EqualTo(1));
                Assert.That(result.CoinsAwarded, Is.EqualTo(Constants.PACKAGE_DELIVERY_BONUS));
                Assert.That(coins.RunCoins, Is.EqualTo(Constants.PACKAGE_DELIVERY_BONUS));
            }
            finally
            {
                Object.DestroyImmediate(coinObject);
                Object.DestroyImmediate(packageObject);
            }
        }

        [Test]
        public void Stumble_AppliesAndExpires()
        {
            var playerObject = new GameObject("StumblePlayerTest");
            var obstacleObject = new GameObject("StumbleObstacleTest");
            try
            {
                playerObject.AddComponent<Rigidbody2D>();
                playerObject.AddComponent<BoxCollider2D>();
                var player = playerObject.AddComponent<PlayerController>();
                player.Initialize();
                var stumble = obstacleObject.AddComponent<StumbleObstacle>();
                player.Stats.SetExternalMultipliers(1f, 1f);

                stumble.ApplyStumble(player);
                Assert.That(player.Stats.SpeedMultiplier, Is.EqualTo(0.8f));
                stumble.Simulate(0.5f);
                Assert.That(player.Stats.SpeedMultiplier, Is.EqualTo(1f));
            }
            finally
            {
                Object.DestroyImmediate(obstacleObject);
                Object.DestroyImmediate(playerObject);
            }
        }

        [Test]
        public void Patrol_UsesDifficultySpeedAndResets()
        {
            var objectWithPatrol = new GameObject("PatrolObstacleTest");
            try
            {
                var difficulty = objectWithPatrol.AddComponent<DifficultyManager>();
                var patrol = objectWithPatrol.AddComponent<PatrolObstacle>();
                patrol.SetDifficultyManager(difficulty);
                difficulty.SetDistance(750f);
                Assert.That(patrol.PatrolSpeed, Is.EqualTo(2.8f).Within(0.001f));

                patrol.Simulate(0.5f);
                var moved = patrol.transform.localPosition.x;
                patrol.ResetPatrol();
                Assert.That(patrol.transform.localPosition.x, Is.Not.EqualTo(moved));
            }
            finally
            {
                Object.DestroyImmediate(objectWithPatrol);
            }
        }

        [Test]
        public void Rolling_ResetRestoresAuthoredPosition()
        {
            var playerObject = new GameObject("RollingPlayerTest");
            var rollingObject = new GameObject("RollingObstacleTest");
            try
            {
                var player = playerObject.AddComponent<PlayerController>();
                var rolling = rollingObject.AddComponent<RollingObstacle>();
                rolling.ResetRolling();
                rolling.SetPlayerTarget(player);
                var authoredX = rolling.transform.position.x;
                rolling.transform.position = new Vector3(authoredX + 5f, 0f, 0f);
                rolling.ResetRolling();

                Assert.That(rolling.transform.position.x, Is.EqualTo(authoredX));
            }
            finally
            {
                Object.DestroyImmediate(rollingObject);
                Object.DestroyImmediate(playerObject);
            }
        }

        private static ChunkCatalog CreateFullCatalog()
        {
            var catalog = ScriptableObject.CreateInstance<ChunkCatalog>();
            var entries = new List<ChunkCatalog.Entry>();
            foreach (ChunkType type in Enum.GetValues(typeof(ChunkType)))
            {
                entries.Add(new ChunkCatalog.Entry
                {
                    district = DistrictId.OldTown,
                    type = type,
                    prefab = CreateChunk(type, $"Chunk_{type}_OT_01")
                });
            }

            catalog.SetEntries(entries);
            return catalog;
        }

        private static void DestroyCatalog(ChunkCatalog catalog)
        {
            foreach (var entry in catalog.Entries)
            {
                if (entry?.prefab != null)
                {
                    Object.DestroyImmediate(entry.prefab);
                }
            }

            Object.DestroyImmediate(catalog);
        }

        private static GameObject CreateChunk(ChunkType type, string name)
        {
            var root = new GameObject(name);
            var start = new GameObject("Start");
            var end = new GameObject("End");
            start.transform.SetParent(root.transform, false);
            end.transform.SetParent(root.transform, false);
            start.transform.localPosition = Vector3.zero;
            end.transform.localPosition = new Vector3(Constants.CHUNK_WIDTH, 0f, 0f);
            var marker = root.AddComponent<ChunkMarker>();
            SetPrivateField(marker, "start", start.transform);
            SetPrivateField(marker, "end", end.transform);
            SetPrivateField(marker, "type", type);
            if (type == ChunkType.Checkpoint)
            {
                root.AddComponent<CheckpointMarker>();
                var trigger = root.AddComponent<BoxCollider2D>();
                trigger.isTrigger = true;
            }

            return root;
        }

        private static void SetPrivateField(object target, string name, object value)
        {
            target.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic)?.SetValue(target, value);
        }
    }
}
