using System;
using System.IO;
using CatCourier.Core;
using NUnit.Framework;
using UnityEngine;

namespace CatCourier.Tests
{
    public sealed class SaveSystemTests
    {
        private GameObject host;
        private string directory;
        private SaveSystem saveSystem;

        [SetUp]
        public void SetUp()
        {
            directory = Path.Combine(Path.GetTempPath(), "CatCourierSaveTests", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(directory);
            host = new GameObject("SaveSystemTests");
            host.SetActive(false);
            saveSystem = host.AddComponent<SaveSystem>();
            // Inactive object prevents Awake from touching the real persistent save path.
            saveSystem.ConfigurePaths(directory);
        }

        [TearDown]
        public void TearDown()
        {
            UnityEngine.Object.DestroyImmediate(host);
            if (Directory.Exists(directory))
            {
                Directory.Delete(directory, true);
            }
        }

        [Test]
        public void Load_MissingFile_ReturnsDefaults()
        {
            Assert.That(saveSystem.Load(), Is.True);
            Assert.That(saveSystem.Data, Is.Not.Null);
            Assert.That(saveSystem.Data.saveVersion, Is.EqualTo(Constants.SAVE_VERSION));
            Assert.That(saveSystem.Data.totalCoins, Is.Zero);
            Assert.That(saveSystem.Data.upgradeLevels, Is.Empty);
            Assert.That(saveSystem.Data.selectedCatBreedId, Is.Empty);
            Assert.That(saveSystem.Data.unlockedCatBreedIds, Is.Empty);
            Assert.That(saveSystem.Data.unlockedDistrictIds, Is.Empty);
            Assert.That(saveSystem.Data.runHistory, Is.Empty);
            Assert.That(saveSystem.Data.seenStoryBeatIds, Is.Empty);
            Assert.That(saveSystem.Data.totalRunsCompleted, Is.Zero);
            Assert.That(saveSystem.Data.hasSeenPaywall, Is.False);
            Assert.That(saveSystem.Data.hasUsedFreeTrialEver, Is.False);
        }

        [Test]
        public void SaveLoad_RoundTripsEverySupportedField()
        {
            saveSystem.Load();
            saveSystem.Data.totalCoins = 1234;
            saveSystem.Data.selectedCatBreedId = "tuxedo";
            saveSystem.Data.unlockedCatBreedIds.Add("tuxedo");
            saveSystem.Data.unlockedDistrictIds.Add("downtown");
            saveSystem.Data.upgradeLevels.Add(new UpgradeLevel { id = "sprint_speed", level = 3 });
            saveSystem.Data.seenStoryBeatIds.Add("oldtown_1");
            saveSystem.Data.totalRunsCompleted = 4;
            saveSystem.Data.hasSeenPaywall = true;
            saveSystem.Data.hasUsedFreeTrialEver = true;
            saveSystem.Data.runHistory.Add(new RunRecord
            {
                score = 9000,
                distanceMeters = 500f,
                packagesDelivered = 2,
                districtReached = "Downtown",
                catBreedId = "tuxedo",
                dateISO = "2026-09-25T00:00:00Z"
            });

            Assert.That(saveSystem.Save(), Is.True);
            saveSystem.ResetToDefaults();
            Assert.That(saveSystem.Load(), Is.True);

            Assert.That(saveSystem.Data.totalCoins, Is.EqualTo(1234));
            Assert.That(saveSystem.Data.selectedCatBreedId, Is.EqualTo("tuxedo"));
            Assert.That(saveSystem.Data.unlockedCatBreedIds, Does.Contain("tuxedo"));
            Assert.That(saveSystem.Data.unlockedDistrictIds, Does.Contain("downtown"));
            Assert.That(saveSystem.Data.upgradeLevels, Has.Count.EqualTo(1));
            Assert.That(saveSystem.Data.upgradeLevels[0].level, Is.EqualTo(3));
            Assert.That(saveSystem.Data.seenStoryBeatIds, Does.Contain("oldtown_1"));
            Assert.That(saveSystem.Data.totalRunsCompleted, Is.EqualTo(4));
            Assert.That(saveSystem.Data.hasSeenPaywall, Is.True);
            Assert.That(saveSystem.Data.hasUsedFreeTrialEver, Is.True);
            Assert.That(saveSystem.Data.runHistory, Has.Count.EqualTo(1));
            Assert.That(saveSystem.Data.runHistory[0].score, Is.EqualTo(9000));
            Assert.That(saveSystem.Data.runHistory[0].distanceMeters, Is.EqualTo(500f));
            Assert.That(saveSystem.Data.runHistory[0].packagesDelivered, Is.EqualTo(2));
            Assert.That(saveSystem.Data.runHistory[0].districtReached, Is.EqualTo("Downtown"));
            Assert.That(saveSystem.Data.runHistory[0].catBreedId, Is.EqualTo("tuxedo"));
            Assert.That(saveSystem.Data.runHistory[0].dateISO, Is.EqualTo("2026-09-25T00:00:00Z"));
            Assert.That(saveSystem.Data.lastSaved, Is.Not.Empty);
        }

        [Test]
        public void Load_UnsupportedVersionQuarantinesSaveAndReturnsDefaults()
        {
            var invalidPath = Path.Combine(directory, "save.json");
            File.WriteAllText(invalidPath, "{\"saveVersion\":999,\"upgradeLevels\":[],\"unlockedCatBreedIds\":[],\"unlockedDistrictIds\":[],\"runHistory\":[],\"seenStoryBeatIds\":[]}");

            Assert.That(saveSystem.Load(), Is.False);
            Assert.That(File.Exists(invalidPath), Is.False);
            Assert.That(Directory.GetFiles(directory, "save.json.invalid-*"), Has.Length.EqualTo(1));
            Assert.That(saveSystem.Data.saveVersion, Is.EqualTo(Constants.SAVE_VERSION));
        }

        [Test]
        public void Load_WrongSchemaMarkerQuarantinesSave()
        {
            saveSystem.Load();
            var json = JsonUtility.ToJson(saveSystem.Data).Replace("cat-courier-save-v1", "wrong-marker");
            File.WriteAllText(Path.Combine(directory, "save.json"), json);

            Assert.That(saveSystem.Load(), Is.False);
            Assert.That(saveSystem.Data.saveVersion, Is.EqualTo(Constants.SAVE_VERSION));
            Assert.That(Directory.GetFiles(directory, "save.json.invalid-*"), Has.Length.EqualTo(1));
        }

        [Test]
        public void Load_CorruptJson_ReturnsDefaults()
        {
            File.WriteAllText(Path.Combine(directory, "save.json"), "{ definitely-not-json");

            Assert.That(saveSystem.Load(), Is.False);
            Assert.That(saveSystem.Data, Is.Not.Null);
            Assert.That(saveSystem.Data.saveVersion, Is.EqualTo(Constants.SAVE_VERSION));
            Assert.That(Directory.GetFiles(directory, "save.json.invalid-*"), Has.Length.EqualTo(1));
        }

        [Test]
        public void Load_KeepsOnlyBestTenSortedDescending()
        {
            saveSystem.Load();
            for (var i = 0; i < 12; i++)
            {
                saveSystem.Data.runHistory.Add(new RunRecord
                {
                    score = i * 100,
                    districtReached = DistrictId.OldTown.ToString(),
                    catBreedId = "tuxedo",
                    dateISO = "2026-09-25T00:00:00Z"
                });
            }

            Assert.That(saveSystem.Save(), Is.True);
            Assert.That(saveSystem.Data.runHistory, Has.Count.EqualTo(Constants.MAX_RUN_HISTORY));
            Assert.That(saveSystem.Load(), Is.True);
            Assert.That(saveSystem.Data.runHistory, Has.Count.EqualTo(Constants.MAX_RUN_HISTORY));
            Assert.That(saveSystem.Data.runHistory[0].score, Is.EqualTo(1100));
            Assert.That(saveSystem.Data.runHistory[9].score, Is.EqualTo(200));
        }

        [Test]
        public void Load_RecoversBackupWhenCurrentSaveHasValidJsonButMissingFields()
        {
            saveSystem.Load();
            saveSystem.Data.totalCoins = 88;
            Assert.That(saveSystem.Save(), Is.True);
            File.Copy(Path.Combine(directory, "save.json"), Path.Combine(directory, "save.json.bak"), true);
            File.WriteAllText(Path.Combine(directory, "save.json"), "{\"saveVersion\":1}");

            Assert.That(saveSystem.Load(), Is.True);
            Assert.That(saveSystem.Data.totalCoins, Is.EqualTo(88));
            Assert.That(File.Exists(Path.Combine(directory, "save.json.bak")), Is.False);
        }

        [Test]
        public void RecordCompletedRun_UpdatesEconomyHistoryAndPersists()
        {
            saveSystem.Load();
            saveSystem.Data.selectedCatBreedId = "tuxedo";
            var result = new RunResult(2500, 300f, 2, 17, DistrictId.Downtown);

            Assert.That(saveSystem.RecordCompletedRun(result), Is.True);
            saveSystem.ResetToDefaults();
            Assert.That(saveSystem.Load(), Is.True);
            Assert.That(saveSystem.Data.totalCoins, Is.EqualTo(17));
            Assert.That(saveSystem.Data.totalRunsCompleted, Is.EqualTo(1));
            Assert.That(saveSystem.Data.runHistory, Has.Count.EqualTo(1));
            Assert.That(saveSystem.Data.runHistory[0].score, Is.EqualTo(2500));
            Assert.That(saveSystem.Data.runHistory[0].districtReached, Is.EqualTo("Downtown"));
            Assert.That(saveSystem.Data.runHistory[0].catBreedId, Is.EqualTo("tuxedo"));
        }

        [Test]
        public void Load_RecoversInterruptedWriteFromBackup()
        {
            saveSystem.Load();
            saveSystem.Data.totalCoins = 77;
            Assert.That(saveSystem.Save(), Is.True);
            File.Copy(Path.Combine(directory, "save.json"), Path.Combine(directory, "save.json.bak"), true);
            File.WriteAllText(Path.Combine(directory, "save.json"), "{ broken");

            Assert.That(saveSystem.Load(), Is.True);
            Assert.That(saveSystem.Data.totalCoins, Is.EqualTo(77));
            Assert.That(File.Exists(Path.Combine(directory, "save.json.bak")), Is.False);
        }

        [Test]
        public void Save_OverwriteKeepsNewDataAndNoTemporaryFiles()
        {
            saveSystem.Load();
            saveSystem.Data.totalCoins = 10;
            Assert.That(saveSystem.Save(), Is.True);
            saveSystem.Data.totalCoins = 20;
            Assert.That(saveSystem.Save(), Is.True);
            saveSystem.ResetToDefaults();
            Assert.That(saveSystem.Load(), Is.True);

            Assert.That(saveSystem.Data.totalCoins, Is.EqualTo(20));
            Assert.That(File.Exists(Path.Combine(directory, "save.json.tmp")), Is.False);
            Assert.That(File.Exists(Path.Combine(directory, "save.json.bak")), Is.False);
        }

        [Test]
        public void Save_RemovesTemporaryFileAfterSuccess()
        {
            saveSystem.Load();
            Assert.That(saveSystem.Save(), Is.True);
            Assert.That(File.Exists(Path.Combine(directory, "save.json")), Is.True);
            Assert.That(File.Exists(Path.Combine(directory, "save.json.tmp")), Is.False);
        }
    }
}
