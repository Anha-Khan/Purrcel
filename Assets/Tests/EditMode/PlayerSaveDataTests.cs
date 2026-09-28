using System;
using System.Collections.Generic;
using CatCourier.Core;
using NUnit.Framework;
using UnityEngine;

namespace CatCourier.Tests
{
    public sealed class PlayerSaveDataTests
    {
        [Test]
        public void Repair_RepairsNullCollectionsAndRemovesNullHistory()
        {
            var data = new PlayerSaveData
            {
                upgradeLevels = null,
                unlockedCatBreedIds = null,
                unlockedDistrictIds = null,
                runHistory = new List<RunRecord> { null, new RunRecord { score = 10 } },
                seenStoryBeatIds = null
            };

            data.Repair();

            Assert.That(data.upgradeLevels, Is.Not.Null);
            Assert.That(data.unlockedCatBreedIds, Is.Not.Null);
            Assert.That(data.unlockedDistrictIds, Is.Not.Null);
            Assert.That(data.seenStoryBeatIds, Is.Not.Null);
            Assert.That(data.runHistory, Has.Count.EqualTo(1));
        }

        [Test]
        public void JsonUtility_RoundTripsUpgradeList()
        {
            var data = PlayerSaveDataFactory.Create();
            data.upgradeLevels.Add(new UpgradeLevel { id = "sprint_speed", level = 2 });
            data.upgradeLevels.Add(new UpgradeLevel { id = "jump_height", level = 1 });

            var json = JsonUtility.ToJson(data);
            var restored = JsonUtility.FromJson<PlayerSaveData>(json);

            Assert.That(restored, Is.Not.Null);
            Assert.That(restored.upgradeLevels, Has.Count.EqualTo(2));
            Assert.That(restored.upgradeLevels[0].id, Is.EqualTo("sprint_speed"));
            Assert.That(restored.upgradeLevels[0].level, Is.EqualTo(2));
        }
    }

    internal static class PlayerSaveDataFactory
    {
        public static PlayerSaveData Create() => new()
        {
            saveVersion = Constants.SAVE_VERSION,
            lastSaved = string.Empty,
            selectedCatBreedId = string.Empty,
            totalCoins = 0,
            totalRunsCompleted = 0,
            hasSeenPaywall = false,
            hasUsedFreeTrialEver = false
        };
    }
}
