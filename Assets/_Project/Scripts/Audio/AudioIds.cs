using CatCourier.Core;

namespace CatCourier.Audio
{
    public static class AudioIdMap
    {
        public static MusicId MusicForDistrict(DistrictId district)
        {
            switch (district)
            {
                case DistrictId.Downtown:
                    return MusicId.Downtown;
                case DistrictId.Harbour:
                    return MusicId.Harbour;
                case DistrictId.Suburbs:
                    return MusicId.Suburbs;
                default:
                    return MusicId.OldTown;
            }
        }
    }
}
