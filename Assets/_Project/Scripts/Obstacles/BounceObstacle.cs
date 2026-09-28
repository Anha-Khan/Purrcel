namespace CatCourier.Obstacles
{
    public sealed class BounceObstacle : ObstacleBase
    {
        protected override void ConfigureDefaults()
        {
            type = ObstacleType.Bounce;
            isDeadly = false;
            isDestroyable = false;
            speedMultiplierOnHit = 1f;
        }
    }
}
