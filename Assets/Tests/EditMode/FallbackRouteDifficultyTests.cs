using CatCourier.Art;
using CatCourier.Core;
using NUnit.Framework;
using UnityEngine;

namespace CatCourier.Tests
{
    /// <summary>
    /// The empty chunk catalog is the shipping configuration today, so the fallback
    /// spawner is the only thing generating the route. These tests pin the difficulty
    /// curve it is supposed to scale by; before them nothing covered this file.
    /// </summary>
    public sealed class FallbackRouteDifficultyTests
    {
        private const float MaxDistance = Constants.DIFFICULTY_STEP_DISTANCE * 10f;

        /// <summary>
        /// The cat's transform rides at y=0.4 and its hitbox is 1.0 tall standing, so its
        /// head is at 0.9. Sliding uses SLIDE_HITBOX_HEIGHT_ABS and drops it to 0.3.
        /// </summary>
        private const float CatHeadStanding = 0.9f;
        private const float CatHeadSliding = 0.3f;

        [Test]
        public void DifficultyProgress_MatchesDifficultyManagerLevels()
        {
            Assert.That(FallbackObstacleSpawner.DifficultyProgress(0f), Is.Zero);
            Assert.That(FallbackObstacleSpawner.DifficultyProgress(750f), Is.EqualTo(0.5f).Within(0.001f));
            Assert.That(FallbackObstacleSpawner.DifficultyProgress(MaxDistance), Is.EqualTo(1f).Within(0.001f));
            Assert.That(FallbackObstacleSpawner.DifficultyProgress(MaxDistance * 10f), Is.EqualTo(1f).Within(0.001f),
                "Progress must clamp, or an endless run would invert the curve.");
            Assert.That(FallbackObstacleSpawner.DifficultyProgress(-50f), Is.Zero);
        }

        [Test]
        public void HazardSpacing_TightensAsTheRunProgresses()
        {
            var start = FallbackObstacleSpawner.SpacingRange(0f);
            var middle = FallbackObstacleSpawner.SpacingRange(MaxDistance * 0.5f);
            var end = FallbackObstacleSpawner.SpacingRange(MaxDistance);

            Assert.That(start.x, Is.EqualTo(9.5f).Within(0.001f));
            Assert.That(start.y, Is.EqualTo(13.5f).Within(0.001f));
            Assert.That(end.x, Is.EqualTo(6.2f).Within(0.001f));
            Assert.That(end.y, Is.EqualTo(9f).Within(0.001f));

            Assert.That(middle.x, Is.LessThan(start.x), "Near spacing must shrink with difficulty.");
            Assert.That(middle.y, Is.LessThan(start.y), "Far spacing must shrink with difficulty.");
            Assert.That(end.x, Is.LessThan(middle.x));
            Assert.That(end.y, Is.LessThan(middle.y));
        }

        [Test]
        public void HazardSpacing_NeverInvertsAcrossTheWholeCurve()
        {
            for (var meters = 0f; meters <= MaxDistance * 2f; meters += 25f)
            {
                var range = FallbackObstacleSpawner.SpacingRange(meters);
                Assert.That(range.x, Is.GreaterThan(0f), $"Degenerate near spacing at {meters:0} m.");
                Assert.That(range.y, Is.GreaterThan(range.x), $"Inverted spacing range at {meters:0} m.");
            }
        }

        [Test]
        public void HazardSpacing_StaysClearOfTheMinimumObstacleGap()
        {
            // The catalog validator requires 2.5 units between deadly obstacles. A
            // tighter route must not squeeze a hazard pair below what the cat can clear.
            for (var meters = 0f; meters <= MaxDistance; meters += 25f)
            {
                var range = FallbackObstacleSpawner.SpacingRange(meters);
                Assert.That(range.x, Is.GreaterThan(Constants.MIN_OBSTACLE_GAP * 2f),
                    $"Spacing at {meters:0} m leaves no room for a hazard plus its coin arc.");
            }
        }

        [Test]
        public void RewardIntervals_GetRarerAsTheRunProgresses()
        {
            Assert.That(FallbackObstacleSpawner.SpecialCoinInterval(0f), Is.EqualTo(65f).Within(0.01f));
            Assert.That(FallbackObstacleSpawner.SpecialCoinInterval(MaxDistance), Is.EqualTo(40f).Within(0.01f));
            Assert.That(FallbackObstacleSpawner.BoostInterval(0f), Is.EqualTo(110f).Within(0.01f));
            Assert.That(FallbackObstacleSpawner.BoostInterval(MaxDistance), Is.EqualTo(70f).Within(0.01f));

            Assert.That(FallbackObstacleSpawner.SpecialCoinInterval(MaxDistance),
                Is.LessThan(FallbackObstacleSpawner.SpecialCoinInterval(0f)));
            Assert.That(FallbackObstacleSpawner.BoostInterval(MaxDistance),
                Is.LessThan(FallbackObstacleSpawner.BoostInterval(0f)));
        }

        [Test]
        public void RewardIntervals_RemainMonotonic()
        {
            var previousSpecial = float.MaxValue;
            var previousBoost = float.MaxValue;
            for (var meters = 0f; meters <= MaxDistance * 2f; meters += 25f)
            {
                var special = FallbackObstacleSpawner.SpecialCoinInterval(meters);
                var boost = FallbackObstacleSpawner.BoostInterval(meters);
                Assert.That(special, Is.LessThanOrEqualTo(previousSpecial), $"Special coins oscillated at {meters:0} m.");
                Assert.That(boost, Is.LessThanOrEqualTo(previousBoost), $"Boosters oscillated at {meters:0} m.");
                previousSpecial = special;
                previousBoost = boost;
            }
        }

        [Test]
        public void CheckpointClearSpan_FitsInsideTheHazardSpacing()
        {
            // SpawnNext forces clear road for +/- CheckpointClearRadius around a checkpoint.
            // If that span were wider than the gap between two checkpoints, hazards would be
            // suppressed for the whole run and the route would be empty.
            Assert.That(FallbackObstacleSpawner.CheckpointClearRadius * 2f,
                Is.LessThan(Constants.CHECKPOINT_INTERVAL),
                "A checkpoint's clear span must be narrower than the distance to the next one.");
            Assert.That(FallbackObstacleSpawner.CheckpointClearRadius,
                Is.GreaterThan(0f));
        }

        /// <summary>
        /// The overhead drone shipped as a box around its painted body, low enough that a
        /// running cat overlapped it and high enough that the same jump cleared it. It
        /// behaved exactly like cargo, so the route had one verb instead of two. These
        /// pin the geometry that makes it duck-only.
        /// </summary>
        [Test]
        public void OverheadHazard_CanBeDuckedUnderButNotStoodInto()
        {
            Assert.That(FallbackObstacleSpawner.OverheadHazardBottom,
                Is.GreaterThan(CatHeadSliding),
                "A sliding cat must clear the underside, or the hazard is unavoidable.");
            Assert.That(FallbackObstacleSpawner.OverheadHazardBottom,
                Is.LessThan(CatHeadStanding),
                "A running cat must collide with the underside, or ducking is pointless.");
        }

        [Test]
        public void OverheadHazard_CannotBeJumpedAtAnyUpgradeLevel()
        {
            // Single jump rises v^2/(2g); the double jump adds its own on top. A collider
            // ending below either apex can simply be jumped, which was the original bug.
            var single = Constants.JUMP_FORCE * Constants.JUMP_FORCE / (2f * -Constants.GRAVITY);
            var second = Constants.DOUBLE_JUMP_FORCE * Constants.DOUBLE_JUMP_FORCE /
                (2f * -Constants.GRAVITY);
            var catApex = 0.4f + single + second + 0.5f;

            Assert.That(FallbackObstacleSpawner.OverheadHazardTop,
                Is.GreaterThan(catApex),
                $"The collider top ({FallbackObstacleSpawner.OverheadHazardTop}) must clear the " +
                $"cat's double-jump apex ({catApex:0.00}), or the drone is just another jump.");
            Assert.That(single, Is.GreaterThan(0f), "Jump maths guard: gravity sign flipped.");
        }
    }
}
