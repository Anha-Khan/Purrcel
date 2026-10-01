using System;
using System.Collections.Generic;
using UnityEngine;
using CatCourier.Core;

namespace CatCourier.Packages
{
    public enum PackageState
    {
        Assigned,
        Delivered,
        Lost
    }

    public sealed class PackageSlot
    {
        internal PackageSlot(int index, PackageType type, PackageState state, float urgentTimeLimit)
        {
            Index = index;
            Type = type;
            State = state;
            UrgentTimeLimit = urgentTimeLimit;
            UrgentTimeRemaining = urgentTimeLimit;
        }

        public int Index { get; }
        public PackageType Type { get; }
        public PackageState State { get; internal set; }
        public float UrgentTimeLimit { get; }
        public float UrgentTimeRemaining { get; internal set; }
        public bool IsAssigned => State == PackageState.Assigned;
    }

    public readonly struct CheckpointDeliveryResult
    {
        public CheckpointDeliveryResult(int packagesDelivered, int coinsAwarded, float scoreMultiplier, float scoreDuration)
        {
            PackagesDelivered = packagesDelivered;
            CoinsAwarded = coinsAwarded;
            ScoreMultiplier = scoreMultiplier;
            ScoreDuration = scoreDuration;
        }

        public int PackagesDelivered { get; }
        public int CoinsAwarded { get; }
        public float ScoreMultiplier { get; }
        public float ScoreDuration { get; }
    }

    public sealed class PackageManager : MonoBehaviour
    {
        public const int DefaultSlotCount = 1;
        public const float FragileJumpMultiplier = 0.9f;
        public const float HeavyJumpMultiplier = 0.75f;
        public const float UrgentSpeedMultiplier = 1.05f;
        public const float DeliveryScoreDuration = 2f;

        public IReadOnlyList<PackageSlot> Slots => slots;
        public bool HasAssignedPackage
        {
            get
            {
                for (var i = 0; i < slots.Count; i++)
                {
                    if (slots[i].IsAssigned)
                    {
                        return true;
                    }
                }

                return false;
            }
        }
        public PackageType? CurrentPackageType => slots.Count > 0 ? (PackageType?)slots[0].Type : null;
        public PackageState? State
        {
            get
            {
                for (var i = 0; i < slots.Count; i++)
                {
                    if (slots[i].IsAssigned)
                    {
                        return PackageState.Assigned;
                    }
                }

                return slots.Count > 0 ? (PackageState?)slots[0].State : null;
            }
        }
        public int PackagesDelivered { get; private set; }
        public int PackagesLost { get; private set; }
        public int DeliveryCoinsAwarded { get; private set; }
        public bool IsFragile { get; private set; }
        public float SpeedMultiplier { get; private set; } = 1f;
        public float JumpMultiplier { get; private set; } = 1f;
        public float UrgentTimeRemaining { get; private set; }
        public float UrgentTimeLimit { get; private set; }

        public event Action<PackageType> OnPackageAssigned;
        public event Action OnPackageDelivered;
        public event Action OnPackageLost;
        public event Action OnRunReset;
        public event Action<float, float> OnDeliveryScorePulseRequested;

        [SerializeField] private int randomSeed;

        private readonly List<PackageSlot> slots = new();
        private System.Random random;
        private int targetSlotCount = DefaultSlotCount;

        private void Awake()
        {
            SetSeed(randomSeed);
        }

        public void SetSeed(int seed)
        {
            random = new System.Random(seed);
        }

        public void AssignPackage(bool premium, float distanceToNextCheckpoint)
        {
            AssignPackages(premium, distanceToNextCheckpoint, DefaultSlotCount);
        }

        public void AssignPackages(
            bool premium,
            float distanceToNextCheckpoint,
            int slotCount = DefaultSlotCount,
            System.Random source = null)
        {
            slotCount = Math.Max(1, slotCount);
            targetSlotCount = slotCount;
            if (float.IsNaN(distanceToNextCheckpoint) || float.IsInfinity(distanceToNextCheckpoint))
            {
                distanceToNextCheckpoint = 0f;
            }

            distanceToNextCheckpoint = Mathf.Max(0f, distanceToNextCheckpoint);
            slots.Clear();
            RecalculateModifiers();

            for (var i = 0; i < slotCount; i++)
            {
                var type = SelectWeightedPackage(source ?? random ?? (random = new System.Random(0)));
                var urgentTimeLimit = type == PackageType.Urgent
                    ? Constants.URGENT_BASE_TIME
                      + Constants.URGENT_TIME_PER_METER * distanceToNextCheckpoint
                      + (premium ? Constants.URGENT_PREMIUM_BONUS_SEC : 0)
                    : 0f;
                slots.Add(new PackageSlot(i, type, PackageState.Assigned, urgentTimeLimit));
            }

            RecalculateModifiers();
            PublishUrgentTimer();
        }

        public void AssignSpecificPackage(PackageType type, bool premium, float distanceToNextCheckpoint, int slotCount = DefaultSlotCount)
        {
            ResetRun();
            var safeDistance = float.IsNaN(distanceToNextCheckpoint) || float.IsInfinity(distanceToNextCheckpoint)
                ? 0f
                : Mathf.Max(0f, distanceToNextCheckpoint);
            var count = Math.Max(1, slotCount);
            targetSlotCount = count;
            for (var index = 0; index < count; index++)
            {
                var urgentLimit = type == PackageType.Urgent
                    ? Constants.URGENT_BASE_TIME + Constants.URGENT_TIME_PER_METER * safeDistance + (premium ? Constants.URGENT_PREMIUM_BONUS_SEC : 0f)
                    : 0f;
                slots.Add(new PackageSlot(index, type, PackageState.Assigned, urgentLimit));
                OnPackageAssigned?.Invoke(type);
            }

            RecalculateModifiers();
            PublishUrgentTimer();
        }

        public void SetTargetSlotCount(int slotCount)
        {
            targetSlotCount = Math.Max(1, slotCount);
        }

        public int ReplenishForNextLeg(bool premium, float distanceToNextCheckpoint)
        {
            slots.RemoveAll(slot => !slot.IsAssigned);
            return RefillEmptySlots(premium, distanceToNextCheckpoint);
        }

        public int RefillEmptySlots(bool premium, float distanceToNextCheckpoint)
        {
            var safeDistance = float.IsNaN(distanceToNextCheckpoint) || float.IsInfinity(distanceToNextCheckpoint)
                ? 0f
                : Mathf.Max(0f, distanceToNextCheckpoint);
            var assigned = 0;
            while (slots.Count < targetSlotCount)
            {
                var type = SelectWeightedPackage(random ?? (random = new System.Random(0)));
                var urgentLimit = type == PackageType.Urgent
                    ? Constants.URGENT_BASE_TIME + Constants.URGENT_TIME_PER_METER * safeDistance + (premium ? Constants.URGENT_PREMIUM_BONUS_SEC : 0f)
                    : 0f;
                slots.Add(new PackageSlot(slots.Count, type, PackageState.Assigned, urgentLimit));
                OnPackageAssigned?.Invoke(type);
                assigned++;
            }

            RecalculateModifiers();
            PublishUrgentTimer();
            return assigned;
        }

        public void AssignPackages(int slotCount, bool premium, float distanceToNextCheckpoint)
        {
            AssignPackages(premium, distanceToNextCheckpoint, slotCount);
        }

        public void StartRun(bool premium, float distanceToNextCheckpoint)
        {
            ResetRun();
            AssignPackage(premium, distanceToNextCheckpoint);
        }

        public void StartRun(bool premium, float distanceToNextCheckpoint, int slotCount)
        {
            ResetRun();
            AssignPackages(premium, distanceToNextCheckpoint, slotCount);
        }

        public void Simulate(float deltaTime)
        {
            if (deltaTime <= 0f || !HasAssignedPackage)
            {
                return;
            }

            deltaTime = Mathf.Max(0f, deltaTime);
            for (var i = 0; i < slots.Count; i++)
            {
                var slot = slots[i];
                if (!slot.IsAssigned || slot.Type != PackageType.Urgent)
                {
                    continue;
                }

                slot.UrgentTimeRemaining = Mathf.Max(0f, slot.UrgentTimeRemaining - deltaTime);
                if (slot.UrgentTimeRemaining > 0f)
                {
                    continue;
                }

                slot.State = PackageState.Lost;
                PackagesLost++;
                OnPackageLost?.Invoke();
            }

            RecalculateModifiers();
            PublishUrgentTimer();
        }

        public CheckpointDeliveryResult DeliverAtCheckpoint()
        {
            var deliveredCount = 0;
            for (var i = 0; i < slots.Count; i++)
            {
                var slot = slots[i];
                if (!slot.IsAssigned)
                {
                    continue;
                }

                slot.State = PackageState.Delivered;
                deliveredCount++;
                OnPackageDelivered?.Invoke();
            }

            var coinsAwarded = deliveredCount * Constants.PACKAGE_DELIVERY_BONUS;
            PackagesDelivered += deliveredCount;
            DeliveryCoinsAwarded += coinsAwarded;
            RecalculateModifiers();
            PublishUrgentTimer();

            var result = new CheckpointDeliveryResult(
                deliveredCount,
                coinsAwarded,
                Constants.DELIVERY_SCORE_PULSE,
                DeliveryScoreDuration);
            if (deliveredCount > 0)
            {
                OnDeliveryScorePulseRequested?.Invoke(result.ScoreMultiplier, result.ScoreDuration);
            }

            return result;
        }

        public void ResetRun()
        {
            slots.Clear();
            targetSlotCount = DefaultSlotCount;
            PackagesDelivered = 0;
            PackagesLost = 0;
            DeliveryCoinsAwarded = 0;
            RecalculateModifiers();
            PublishUrgentTimer();
            OnRunReset?.Invoke();
        }

        public static PackageType SelectWeightedPackage(System.Random source)
        {
            if (source == null)
            {
                throw new ArgumentNullException(nameof(source));
            }

            var roll = source.Next(100);
            if (roll < 40)
            {
                return PackageType.Normal;
            }

            if (roll < 60)
            {
                return PackageType.Fragile;
            }

            if (roll < 80)
            {
                return PackageType.Heavy;
            }

            return PackageType.Urgent;
        }

        private void RecalculateModifiers()
        {
            var jumpMultiplier = 1f;
            var speedMultiplier = 1f;
            IsFragile = false;

            for (var i = 0; i < slots.Count; i++)
            {
                var slot = slots[i];
                if (!slot.IsAssigned)
                {
                    continue;
                }

                switch (slot.Type)
                {
                    case PackageType.Fragile:
                        jumpMultiplier = Mathf.Min(jumpMultiplier, FragileJumpMultiplier);
                        IsFragile = true;
                        break;
                    case PackageType.Heavy:
                        jumpMultiplier = Mathf.Min(jumpMultiplier, HeavyJumpMultiplier);
                        break;
                    case PackageType.Urgent:
                        speedMultiplier *= UrgentSpeedMultiplier;
                        break;
                }
            }

            JumpMultiplier = jumpMultiplier;
            SpeedMultiplier = speedMultiplier;
        }

        private void PublishUrgentTimer()
        {
            var remaining = 0f;
            var limit = 0f;
            var hasUrgent = false;

            for (var i = 0; i < slots.Count; i++)
            {
                var slot = slots[i];
                if (!slot.IsAssigned || slot.Type != PackageType.Urgent)
                {
                    continue;
                }

                if (!hasUrgent || slot.UrgentTimeRemaining < remaining)
                {
                    remaining = slot.UrgentTimeRemaining;
                    limit = slot.UrgentTimeLimit;
                }

                hasUrgent = true;
            }

            UrgentTimeRemaining = hasUrgent ? remaining : 0f;
            UrgentTimeLimit = hasUrgent ? limit : 0f;
        }

        private void Update()
        {
            Simulate(Time.deltaTime);
        }
    }
}
