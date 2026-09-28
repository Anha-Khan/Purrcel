namespace CatCourier.Obstacles
{
    public sealed class StaticObstacle : ObstacleBase
    {
        protected override void ConfigureDefaults()
        {
            type = ObstacleType.Static;
            isDeadly = true;
            isDestroyable = false;
            speedMultiplierOnHit = 1f;
        }
    }
}
