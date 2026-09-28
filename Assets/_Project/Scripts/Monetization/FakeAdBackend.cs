using System;

namespace CatCourier.Monetization
{
    public sealed class FakeAdBackend : IAdBackend
    {
        public void ShowInterstitial(string placementId, Action onClosed) => onClosed?.Invoke();
        public void ShowRewarded(string placementId, Action<bool> onDone) => onDone?.Invoke(true);
    }
}
