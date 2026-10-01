namespace CatCourier.Core
{
    public static class Constants
    {
        public const float BASE_RUN_SPEED = 6.0f;
        public const float MAX_RUN_SPEED = 18.0f;
        public const float SPEED_INCREMENT = 0.05f;

        public const float JUMP_FORCE = 14.0f;
        public const float DOUBLE_JUMP_FORCE = 11.0f;
        public const float GRAVITY = -32.0f;
        public const float MAX_FALL_SPEED = -22.0f;
        public const float COYOTE_TIME = 0.1f;
        public const float JUMP_BUFFER_TIME = 0.1f;

        public const float SLIDE_DURATION = 0.7f;
        public const float SLIDE_HITBOX_HEIGHT_ABS = 0.4f;

        public const float WALL_BOUNCE_HORIZONTAL = -4.0f;
        public const float WALL_BOUNCE_VERTICAL = 8.0f;
        public const float WALL_BOUNCE_DURATION = 0.15f;

        public const float PLAYER_HITBOX_WIDTH = 0.6f;
        public const float PLAYER_HITBOX_HEIGHT = 1.0f;

        public const int DISTANCE_SCORE_PER_METER = 10;
        public const int PACKAGE_SCORE_VALUE = 500;
        public const float DELIVERY_SCORE_PULSE = 1.5f;

        public const int COIN_BASE_VALUE = 1;
        public const int COIN_RARE_VALUE = 5;
        public const int PACKAGE_DELIVERY_BONUS = 15;

        public const float COMBO_BREAK_DISTANCE = 8.0f;

        public const float DIFFICULTY_STEP_DISTANCE = 150.0f;

        public const float CHUNK_WIDTH = 20.0f;
        public const int ACTIVE_CHUNK_COUNT = 6;
        public const float CHECKPOINT_INTERVAL = 300.0f;

        public const float MIN_OBSTACLE_GAP = 2.5f;
        public const float DRONE_PATROL_WIDTH = 3.0f;
        public const float DRONE_PATROL_SPEED_BASE = 2.0f;

        public const float FRAGILE_DEATH_THRESHOLD = -16.0f;
        public const float URGENT_BASE_TIME = 30.0f;
        public const float URGENT_TIME_PER_METER = 0.18f;
        public const int URGENT_PREMIUM_BONUS_SEC = 15;

        public const float WIND_DRIFT_FORCE = 1.8f;
        public const float RAIN_LANDING_SLIDE = 0.3f;

        public const float MUSIC_CROSSFADE_TIME = 1.5f;

        public const float SHAKE_LAND_MAGNITUDE = 0.05f;
        public const float SHAKE_LAND_DURATION = 0.1f;
        public const float SHAKE_DEATH_MAGNITUDE = 0.3f;
        public const float SHAKE_DEATH_DURATION = 0.4f;

        public const string ENTITLEMENT_PREMIUM = "purrcel_pro";
        public const string ENTITLEMENT_RARE_PACK = "rare_breeds_pack";
        public const string ENTITLEMENT_LEGEND_PACK = "legendary_cats_pack";
        public const string ENTITLEMENT_HARBOUR = "harbour_district";
        public const string ENTITLEMENT_SUBURBS = "suburbs_district";
        public const string AD_PLACEMENT_DEATH = "death_screen_interstitial";
        public const string AD_PLACEMENT_CONTINUE = "continue_reward";

        public const int SAVE_VERSION = 1;
        public const int MAX_RUN_HISTORY = 10;
    }
}
