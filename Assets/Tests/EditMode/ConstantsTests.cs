using System;
using System.Collections.Generic;
using System.Reflection;
using CatCourier.Core;
using NUnit.Framework;

namespace CatCourier.Tests
{
    /// <summary>
    /// A specification lock on the tuning values. Each constant is its own case, so a
    /// deliberate rebalance reports exactly which value moved instead of failing on the
    /// first mismatch and hiding the rest.
    ///
    /// This asserts no behaviour: it only catches an accidental edit. Delete or update
    /// a row when the design intentionally changes that value.
    /// </summary>
    public sealed class ConstantsTests
    {
        [TestCase("BASE_RUN_SPEED", 6.0f)]
        [TestCase("MAX_RUN_SPEED", 18.0f)]
        [TestCase("SPEED_INCREMENT", 0.05f)]
        [TestCase("JUMP_FORCE", 14.0f)]
        [TestCase("DOUBLE_JUMP_FORCE", 11.0f)]
        [TestCase("GRAVITY", -32.0f)]
        [TestCase("MAX_FALL_SPEED", -22.0f)]
        [TestCase("COYOTE_TIME", 0.1f)]
        [TestCase("JUMP_BUFFER_TIME", 0.1f)]
        [TestCase("SLIDE_DURATION", 0.7f)]
        [TestCase("SLIDE_HITBOX_HEIGHT_ABS", 0.4f)]
        [TestCase("WALL_BOUNCE_HORIZONTAL", -4.0f)]
        [TestCase("WALL_BOUNCE_VERTICAL", 8.0f)]
        [TestCase("WALL_BOUNCE_DURATION", 0.15f)]
        [TestCase("PLAYER_HITBOX_WIDTH", 0.6f)]
        [TestCase("PLAYER_HITBOX_HEIGHT", 1.0f)]
        [TestCase("DISTANCE_SCORE_PER_METER", 10f)]
        [TestCase("PACKAGE_SCORE_VALUE", 500f)]
        [TestCase("DELIVERY_SCORE_PULSE", 1.5f)]
        [TestCase("COIN_BASE_VALUE", 1f)]
        [TestCase("COIN_RARE_VALUE", 5f)]
        [TestCase("PACKAGE_DELIVERY_BONUS", 15f)]
        [TestCase("COMBO_BREAK_DISTANCE", 8.0f)]
        [TestCase("DIFFICULTY_STEP_DISTANCE", 150.0f)]
        [TestCase("CHUNK_WIDTH", 20.0f)]
        [TestCase("ACTIVE_CHUNK_COUNT", 6f)]
        [TestCase("CHECKPOINT_INTERVAL", 300.0f)]
        [TestCase("MIN_OBSTACLE_GAP", 2.5f)]
        [TestCase("DRONE_PATROL_WIDTH", 3.0f)]
        [TestCase("DRONE_PATROL_SPEED_BASE", 2.0f)]
        [TestCase("FRAGILE_DEATH_THRESHOLD", -16.0f)]
        [TestCase("URGENT_BASE_TIME", 30.0f)]
        [TestCase("URGENT_TIME_PER_METER", 0.18f)]
        [TestCase("URGENT_PREMIUM_BONUS_SEC", 15f)]
        [TestCase("WIND_DRIFT_FORCE", 1.8f)]
        [TestCase("RAIN_LANDING_SLIDE", 0.3f)]
        [TestCase("MUSIC_CROSSFADE_TIME", 1.5f)]
        [TestCase("SHAKE_LAND_MAGNITUDE", 0.05f)]
        [TestCase("SHAKE_LAND_DURATION", 0.1f)]
        [TestCase("SHAKE_DEATH_MAGNITUDE", 0.3f)]
        [TestCase("SHAKE_DEATH_DURATION", 0.4f)]
        [TestCase("SAVE_VERSION", 1f)]
        [TestCase("MAX_RUN_HISTORY", 10f)]
        public void SpecFloatConstants_MatchApprovedValues(string name, float expected)
        {
            Assert.That(FloatConstant(name), Is.EqualTo(expected),
                $"Constants.{name} changed. Update this row if the rebalance is intentional.");
        }

        [TestCase("ENTITLEMENT_PREMIUM", "purrcel_pro")]
        [TestCase("ENTITLEMENT_RARE_PACK", "rare_breeds_pack")]
        [TestCase("ENTITLEMENT_LEGEND_PACK", "legendary_cats_pack")]
        [TestCase("ENTITLEMENT_HARBOUR", "harbour_district")]
        [TestCase("ENTITLEMENT_SUBURBS", "suburbs_district")]
        [TestCase("AD_PLACEMENT_DEATH", "death_screen_interstitial")]
        [TestCase("AD_PLACEMENT_CONTINUE", "continue_reward")]
        public void RevenueCatIdentifiers_MatchTheDashboard(string name, string expected)
        {
            // These strings have to match the RevenueCat project exactly, so they are
            // pinned here rather than left to a typo that only fails on a device.
            Assert.That(StringConstant(name), Is.EqualTo(expected),
                $"Constants.{name} must match the RevenueCat dashboard identifier.");
        }

        [Test]
        public void EveryPublicConstantIsCovered()
        {
            // A new constant with no row here would drift unchecked, so fail until the
            // table above is updated with it.
            var covered = new HashSet<string>
            {
                "BASE_RUN_SPEED", "MAX_RUN_SPEED", "SPEED_INCREMENT", "JUMP_FORCE",
                "DOUBLE_JUMP_FORCE", "GRAVITY", "MAX_FALL_SPEED", "COYOTE_TIME",
                "JUMP_BUFFER_TIME", "SLIDE_DURATION", "SLIDE_HITBOX_HEIGHT_ABS",
                "WALL_BOUNCE_HORIZONTAL", "WALL_BOUNCE_VERTICAL", "WALL_BOUNCE_DURATION",
                "PLAYER_HITBOX_WIDTH",
                "PLAYER_HITBOX_HEIGHT", "DISTANCE_SCORE_PER_METER", "PACKAGE_SCORE_VALUE",
                "DELIVERY_SCORE_PULSE", "COIN_BASE_VALUE", "COIN_RARE_VALUE",
                "PACKAGE_DELIVERY_BONUS", "COMBO_BREAK_DISTANCE", "DIFFICULTY_STEP_DISTANCE",
                "CHUNK_WIDTH", "ACTIVE_CHUNK_COUNT", "CHECKPOINT_INTERVAL", "MIN_OBSTACLE_GAP",
                "DRONE_PATROL_WIDTH", "DRONE_PATROL_SPEED_BASE", "FRAGILE_DEATH_THRESHOLD",
                "URGENT_BASE_TIME", "URGENT_TIME_PER_METER", "URGENT_PREMIUM_BONUS_SEC",
                "WIND_DRIFT_FORCE", "RAIN_LANDING_SLIDE", "MUSIC_CROSSFADE_TIME",
                "SHAKE_LAND_MAGNITUDE", "SHAKE_LAND_DURATION", "SHAKE_DEATH_MAGNITUDE",
                "SHAKE_DEATH_DURATION", "SAVE_VERSION", "MAX_RUN_HISTORY",
                "ENTITLEMENT_PREMIUM", "ENTITLEMENT_RARE_PACK", "ENTITLEMENT_LEGEND_PACK",
                "ENTITLEMENT_HARBOUR", "ENTITLEMENT_SUBURBS", "AD_PLACEMENT_DEATH",
                "AD_PLACEMENT_CONTINUE"
            };

            var uncovered = new List<string>();
            foreach (var field in typeof(Constants).GetFields(
                         BindingFlags.Public | BindingFlags.Static))
            {
                if (!covered.Contains(field.Name))
                {
                    uncovered.Add(field.Name);
                }
            }

            Assert.That(uncovered, Is.Empty,
                $"Constants gained a value with no spec row: {string.Join(", ", uncovered)}");
        }

        private static float FloatConstant(string name)
        {
            var field = typeof(Constants).GetField(name);
            Assert.That(field, Is.Not.Null, $"Constants.{name} no longer exists.");
            // Several tuning values are declared int rather than float, so convert
            // instead of casting.
            return Convert.ToSingle(field.GetRawConstantValue());
        }

        private static string StringConstant(string name)
        {
            var field = typeof(Constants).GetField(name);
            Assert.That(field, Is.Not.Null, $"Constants.{name} no longer exists.");
            return (string)field.GetRawConstantValue();
        }
    }
}
