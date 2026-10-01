namespace CatCourier.Core
{
    public static class DistrictUnlockService
    {
        public static bool UnlockReachedDistricts(float distance)
        {
            var save = SaveSystem.Instance;
            var data = save?.Data;
            if (data == null)
            {
                return false;
            }

            var changed = false;
            if (distance >= RunLoadoutService.HarbourReachDistance && !data.unlockedDistrictIds.Contains(DistrictId.Harbour.ToString()))
            {
                changed |= save.UnlockDistrict(DistrictId.Harbour.ToString());
            }

            if (distance >= RunLoadoutService.SuburbsReachDistance && !data.unlockedDistrictIds.Contains(DistrictId.Suburbs.ToString()))
            {
                changed |= save.UnlockDistrict(DistrictId.Suburbs.ToString());
            }

            return changed;
        }

        public static DistrictId[] GetEligible(float runDistance, bool premium)
        {
            return RunLoadoutService.GetEligibleDistricts(runDistance, premium, SaveSystem.Instance?.Data?.unlockedDistrictIds);
        }
    }
}
