using CatCourier.Core;
using NUnit.Framework;

namespace CatCourier.Tests
{
    public sealed class ConstantsTests
    {
        [Test]
        public void SpecConstants_MatchApprovedValues()
        {
            Assert.That(Constants.BASE_RUN_SPEED, Is.EqualTo(6.0f));
            Assert.That(Constants.MAX_RUN_SPEED, Is.EqualTo(18.0f));
            Assert.That(Constants.SPEED_INCREMENT, Is.EqualTo(0.05f));
            Assert.That(Constants.JUMP_FORCE, Is.EqualTo(14.0f));
            Assert.That(Constants.DOUBLE_JUMP_FORCE, Is.EqualTo(11.0f));
            Assert.That(Constants.GRAVITY, Is.EqualTo(-32.0f));
            Assert.That(Constants.MAX_FALL_SPEED, Is.EqualTo(-22.0f));
            Assert.That(Constants.COYOTE_TIME, Is.EqualTo(0.1f));
            Assert.That(Constants.JUMP_BUFFER_TIME, Is.EqualTo(0.1f));
            Assert.That(Constants.SLIDE_DURATION, Is.EqualTo(0.7f));
            Assert.That(Constants.SLIDE_HITBOX_HEIGHT_ABS, Is.EqualTo(0.4f));
            Assert.That(Constants.WALL_BOUNCE_HORIZONTAL, Is.EqualTo(-4.0f));
            Assert.That(Constants.WALL_BOUNCE_VERTICAL, Is.EqualTo(8.0f));
            Assert.That(Constants.PLAYER_HITBOX_WIDTH, Is.EqualTo(0.6f));
            Assert.That(Constants.PLAYER_HITBOX_HEIGHT, Is.EqualTo(1.0f));
            Assert.That(Constants.DISTANCE_SCORE_PER_METER, Is.EqualTo(10));
            Assert.That(Constants.PACKAGE_SCORE_VALUE, Is.EqualTo(500));
            Assert.That(Constants.DELIVERY_SCORE_PULSE, Is.EqualTo(1.5f));
            Assert.That(Constants.COIN_BASE_VALUE, Is.EqualTo(1));
            Assert.That(Constants.COIN_RARE_VALUE, Is.EqualTo(5));
            Assert.That(Constants.PACKAGE_DELIVERY_BONUS, Is.EqualTo(15));
            Assert.That(Constants.COMBO_BREAK_DISTANCE, Is.EqualTo(8.0f));
            Assert.That(Constants.DIFFICULTY_STEP_DISTANCE, Is.EqualTo(150.0f));
            Assert.That(Constants.CHUNK_WIDTH, Is.EqualTo(20.0f));
            Assert.That(Constants.ACTIVE_CHUNK_COUNT, Is.EqualTo(6));
            Assert.That(Constants.CHECKPOINT_INTERVAL, Is.EqualTo(300.0f));
            Assert.That(Constants.MIN_OBSTACLE_GAP, Is.EqualTo(2.5f));
            Assert.That(Constants.DRONE_PATROL_WIDTH, Is.EqualTo(3.0f));
            Assert.That(Constants.DRONE_PATROL_SPEED_BASE, Is.EqualTo(2.0f));
            Assert.That(Constants.FRAGILE_DEATH_THRESHOLD, Is.EqualTo(-16.0f));
            Assert.That(Constants.URGENT_BASE_TIME, Is.EqualTo(30.0f));
            Assert.That(Constants.URGENT_TIME_PER_METER, Is.EqualTo(0.18f));
            Assert.That(Constants.URGENT_PREMIUM_BONUS_SEC, Is.EqualTo(15));
            Assert.That(Constants.WIND_DRIFT_FORCE, Is.EqualTo(1.8f));
            Assert.That(Constants.RAIN_LANDING_SLIDE, Is.EqualTo(0.3f));
            Assert.That(Constants.MUSIC_CROSSFADE_TIME, Is.EqualTo(1.5f));
            Assert.That(Constants.SHAKE_LAND_MAGNITUDE, Is.EqualTo(0.05f));
            Assert.That(Constants.SHAKE_LAND_DURATION, Is.EqualTo(0.1f));
            Assert.That(Constants.SHAKE_DEATH_MAGNITUDE, Is.EqualTo(0.3f));
            Assert.That(Constants.SHAKE_DEATH_DURATION, Is.EqualTo(0.4f));
            Assert.That(Constants.ENTITLEMENT_PREMIUM, Is.EqualTo("premium"));
            Assert.That(Constants.ENTITLEMENT_RARE_PACK, Is.EqualTo("rare_breeds_pack"));
            Assert.That(Constants.ENTITLEMENT_LEGEND_PACK, Is.EqualTo("legendary_cats_pack"));
            Assert.That(Constants.ENTITLEMENT_HARBOUR, Is.EqualTo("harbour_district"));
            Assert.That(Constants.ENTITLEMENT_SUBURBS, Is.EqualTo("suburbs_district"));
            Assert.That(Constants.AD_PLACEMENT_DEATH, Is.EqualTo("death_screen_interstitial"));
            Assert.That(Constants.AD_PLACEMENT_CONTINUE, Is.EqualTo("continue_reward"));
            Assert.That(Constants.SAVE_VERSION, Is.EqualTo(1));
            Assert.That(Constants.MAX_RUN_HISTORY, Is.EqualTo(10));
        }
    }
}
