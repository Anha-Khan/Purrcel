using System;
using System.Globalization;
using System.IO;
using UnityEngine;

namespace CatCourier.Core
{
    public sealed class SaveSystem : MonoBehaviour
    {
        private const string SaveFileName = "save.json";
        private const string TempFileName = "save.json.tmp";
        private const string SchemaMarker = "cat-courier-save-v1";

        private string savePath;
        private string tempPath;
        private string backupPath;

        public static SaveSystem Instance { get; private set; }
        public PlayerSaveData Data { get; private set; }

        public event Action OnDataLoaded;
        public event Action OnDataSaved;

        /// <summary>
        /// Test seam. When set before a SaveSystem is created, it replaces Application.persistentDataPath
        /// so automated tests never read or write the real save file. Leave null in production.
        /// </summary>
        public static string DirectoryOverrideForTests { get; set; }

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
            PersistIfRoot();
            ConfigurePaths(DirectoryOverrideForTests ?? Application.persistentDataPath);
            Load();
        }

        private void PersistIfRoot()
        {
            // Managers are parented under PersistentSystems, which is already DontDestroyOnLoad.
            if (transform.parent == null)
            {
                DontDestroyOnLoad(gameObject);
            }
        }

        public void ConfigurePaths(string directoryPath)
        {
            savePath = Path.Combine(directoryPath, SaveFileName);
            tempPath = Path.Combine(directoryPath, TempFileName);
            backupPath = savePath + ".bak";
        }

        public bool Load()
        {
            if (string.IsNullOrWhiteSpace(savePath) || string.IsNullOrWhiteSpace(backupPath))
            {
                throw new InvalidOperationException("Save paths must be configured before loading.");
            }

            try
            {
                RecoverInterruptedWrite();
                if (!File.Exists(savePath))
                {
                    Data = CreateDefaultData();
                    RepairAndSort(Data);
                    return true;
                }

                if (!TryReadSupportedSave(savePath, out var loaded))
                {
                    Debug.LogWarning("Save file is unsupported or incomplete; using defaults.");
                    QuarantineInvalidSave();
                    Data = CreateDefaultData();
                    RepairAndSort(Data);
                    return false;
                }

                Data = loaded;
                RepairAndSort(Data);
                return true;
            }
            catch (Exception exception)
            {
                Debug.LogError($"Save load failed; using defaults. {exception.GetType().Name}");
                Data = CreateDefaultData();
                RepairAndSort(Data);
                return false;
            }
        }

        public bool Save()
        {
            if (string.IsNullOrWhiteSpace(savePath) || string.IsNullOrWhiteSpace(tempPath) || string.IsNullOrWhiteSpace(backupPath))
            {
                throw new InvalidOperationException("Save paths must be configured before saving.");
            }

            try
            {
                Data ??= CreateDefaultData();
                Data.lastSaved = DateTime.UtcNow.ToString("O");
                RepairAndSort(Data);
                Directory.CreateDirectory(Path.GetDirectoryName(savePath)!);
                var json = JsonUtility.ToJson(Data, true);
                File.WriteAllText(tempPath, json);

                if (!TryReadSupportedSave(tempPath, out _))
                {
                    throw new InvalidDataException("Temporary save validation failed.");
                }

                ReplaceFile(tempPath, savePath);
                if (!TryReadSupportedSave(savePath, out _))
                {
                    throw new InvalidDataException("Final save validation failed.");
                }

                try
                {
                    OnDataSaved?.Invoke();
                }
                catch (Exception exception)
                {
                    Debug.LogError($"Save event failed after durable write. {exception.GetType().Name}");
                }

                return true;
            }
            catch (Exception exception)
            {
                Debug.LogError($"Save write failed. {exception.GetType().Name}");
                return false;
            }
        }

        public void ResetToDefaults()
        {
            Data = CreateDefaultData();
            RepairAndSort(Data);
        }

        public int TotalCoins => Data?.totalCoins ?? 0;

        public bool TryPurchaseUpgrade(string id, int expectedLevel, int cost)
        {
            if (Data == null || string.IsNullOrWhiteSpace(id) || cost < 0)
            {
                return false;
            }

            var before = JsonUtility.ToJson(Data);
            UpgradeLevel level = null;
            foreach (var candidate in Data.upgradeLevels)
            {
                if (candidate != null && candidate.id == id)
                {
                    level = candidate;
                    break;
                }
            }

            var currentLevel = level?.level ?? 0;
            if (currentLevel != expectedLevel || Data.totalCoins < cost)
            {
                return false;
            }

            if (level == null)
            {
                level = new UpgradeLevel { id = id, level = 0 };
                Data.upgradeLevels.Add(level);
            }

            Data.totalCoins -= cost;
            level.level++;
            if (Save())
            {
                return true;
            }

            Rollback(before);
            return false;
        }

        public bool SetSelectedCat(string id)
        {
            if (Data == null || string.IsNullOrWhiteSpace(id))
            {
                return false;
            }

            var before = JsonUtility.ToJson(Data);
            Data.selectedCatBreedId = id;
            if (Save())
            {
                return true;
            }

            Rollback(before);
            return false;
        }

        public bool UnlockDistrict(string id)
        {
            return AddUnique(Data?.unlockedDistrictIds, id);
        }

        public bool RecordCompletedRun(RunResult result)
        {
            if (Data == null)
            {
                Debug.LogError("Cannot record run before save data is loaded.");
                return false;
            }

            var before = JsonUtility.ToJson(Data);
            var catBreedId = Data.selectedCatBreedId ?? string.Empty;
            Data.totalCoins += result.CoinsCollected;
            Data.totalRunsCompleted++;
            Data.runHistory.Add(new RunRecord
            {
                score = result.Score,
                distanceMeters = result.DistanceMeters,
                packagesDelivered = result.PackagesDelivered,
                districtReached = result.DistrictReached.ToString(),
                catBreedId = catBreedId,
                dateISO = DateTime.UtcNow.ToString("O")
            });
            if (Save())
            {
                return true;
            }

            Data = JsonUtility.FromJson<PlayerSaveData>(before);
            RepairAndSort(Data);
            return false;
        }

        private bool AddUnique(System.Collections.Generic.List<string> values, string id)
        {
            if (Data == null || values == null || string.IsNullOrWhiteSpace(id) || values.Contains(id))
            {
                return false;
            }

            var before = JsonUtility.ToJson(Data);
            values.Add(id);
            if (Save())
            {
                return true;
            }

            Rollback(before);
            return false;
        }

        private void Rollback(string before)
        {
            Data = JsonUtility.FromJson<PlayerSaveData>(before);
            RepairAndSort(Data);
        }

        private static PlayerSaveData CreateDefaultData()
        {
            return new PlayerSaveData
            {
                saveVersion = Constants.SAVE_VERSION,
                schemaMarker = SchemaMarker,
                lastSaved = string.Empty,
                selectedCatBreedId = string.Empty,
                totalCoins = 0,
                totalRunsCompleted = 0,
                hasSeenPaywall = false,
                hasUsedFreeTrialEver = false
            };
        }

        private static void RepairAndSort(PlayerSaveData data)
        {
            data.Repair();
            data.runHistory.Sort((left, right) => right.score.CompareTo(left.score));
            if (data.runHistory.Count > Constants.MAX_RUN_HISTORY)
            {
                data.runHistory.RemoveRange(Constants.MAX_RUN_HISTORY, data.runHistory.Count - Constants.MAX_RUN_HISTORY);
            }
        }

        private void ReplaceFile(string sourcePath, string destinationPath)
        {
            if (!File.Exists(destinationPath))
            {
                File.Move(sourcePath, destinationPath);
                return;
            }

            try
            {
                File.Replace(sourcePath, destinationPath, backupPath, true);
            }
            catch (PlatformNotSupportedException)
            {
                ReplaceUsingBackup(sourcePath, destinationPath, backupPath);
            }
            catch (IOException)
            {
                ReplaceUsingBackup(sourcePath, destinationPath, backupPath);
            }

            if (TryReadSupportedSave(destinationPath, out _))
            {
                DeleteBackup();

                return;
            }

            if (File.Exists(backupPath))
            {
                File.Copy(backupPath, destinationPath, true);
                DeleteBackup();
            }

            throw new IOException("Save replacement could not be verified.");
        }

        private void RecoverInterruptedWrite()
        {
            if (!File.Exists(backupPath))
            {
                return;
            }

            if (!File.Exists(savePath))
            {
                if (TryReadSupportedSave(backupPath, out _))
                {
                    File.Move(backupPath, savePath);
                }
                return;
            }

            if (TryReadSupportedSave(savePath, out _))
            {
                if (TryReadSupportedSave(backupPath, out _))
                {
                    DeleteBackup();
                }
                return;
            }

            if (TryReadSupportedSave(backupPath, out _))
            {
                File.Copy(backupPath, savePath, true);
                DeleteBackup();
            }
        }

        private static bool TryReadSupportedSave(string path, out PlayerSaveData data)
        {
            try
            {
                data = JsonUtility.FromJson<PlayerSaveData>(File.ReadAllText(path));
                return data != null &&
                       data.saveVersion == Constants.SAVE_VERSION &&
                       data.schemaMarker == SchemaMarker &&
                       !string.IsNullOrWhiteSpace(data.lastSaved) &&
                       DateTime.TryParse(data.lastSaved, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out _) &&
                       data.totalCoins >= 0 &&
                       data.totalRunsCompleted >= 0 &&
                       data.upgradeLevels != null &&
                       data.selectedCatBreedId != null &&
                       data.unlockedCatBreedIds != null &&
                       data.unlockedDistrictIds != null &&
                       data.runHistory != null &&
                       data.seenStoryBeatIds != null &&
                       data.unlockedCatBreedIds.TrueForAll(id => id != null) &&
                       data.unlockedDistrictIds.TrueForAll(id => id != null) &&
                       data.seenStoryBeatIds.TrueForAll(id => id != null) &&
                       data.upgradeLevels.TrueForAll(upgrade => upgrade != null && !string.IsNullOrWhiteSpace(upgrade.id) && upgrade.level >= 0) &&
                       data.runHistory.TrueForAll(run => run != null &&
                           run.score >= 0 &&
                           run.distanceMeters >= 0f &&
                           run.packagesDelivered >= 0 &&
                           run.dateISO != null &&
                           DateTime.TryParse(run.dateISO, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out _) &&
                           run.districtReached != null &&
                           Enum.TryParse<DistrictId>(run.districtReached, out var district) &&
                           Enum.IsDefined(typeof(DistrictId), district) &&
                           run.catBreedId != null);
            }
            catch (Exception)
            {
                data = null;
                return false;
            }
        }

        private void DeleteBackup()
        {
            if (!File.Exists(backupPath))
            {
                return;
            }

            try
            {
                File.Delete(backupPath);
            }
            catch (Exception exception)
            {
                Debug.LogWarning($"Save backup cleanup failed. {exception.GetType().Name}");
            }
        }

        private static void ReplaceUsingBackup(string sourcePath, string destinationPath, string backupPath)
        {
            File.Copy(destinationPath, backupPath, true);
            File.Delete(destinationPath);
            try
            {
                File.Move(sourcePath, destinationPath);
            }
            catch
            {
                File.Copy(backupPath, destinationPath, true);
                throw;
            }
        }

        private void QuarantineInvalidSave()
        {
            if (!File.Exists(savePath))
            {
                return;
            }

            var quarantinePath = $"{savePath}.invalid-{DateTime.UtcNow:yyyyMMddHHmmssfff}";
            File.Move(savePath, quarantinePath);
        }

        private void OnDestroy()
        {
            if (Instance == this)
            {
                Instance = null;
            }
        }

        private void OnEnable()
        {
            if (Data != null)
            {
                OnDataLoaded?.Invoke();
            }
        }
    }
}
