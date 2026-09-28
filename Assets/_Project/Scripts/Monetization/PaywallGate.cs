using System;
using CatCourier.Core;

namespace CatCourier.Monetization
{
    public static class PaywallGate
    {
        public static event Action<PaywallSource> OnRequested;

        public static void Request(PaywallSource source)
        {
            if (source == PaywallSource.AfterRun3)
            {
                TryRequestAfterRun3();
                return;
            }

            OnRequested?.Invoke(source);
        }

        public static void NotifyRunCompleted()
        {
            TryRequestAfterRun3();
        }

        public static void ResetForTests()
        {
            OnRequested = null;
        }

        private static void TryRequestAfterRun3()
        {
            var save = SaveSystem.Instance;
            var data = save?.Data;
            if (data == null || data.totalRunsCompleted < 3 || data.hasSeenPaywall)
            {
                return;
            }

            data.hasSeenPaywall = true;
            if (save.Save())
            {
                OnRequested?.Invoke(PaywallSource.AfterRun3);
                return;
            }

            data.hasSeenPaywall = false;
        }
    }
}
