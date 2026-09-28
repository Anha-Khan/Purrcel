using CatCourier.Core;
using UnityEngine;

namespace CatCourier.UI
{
    /// <summary>
    /// Displays the saved best-run list. It reads save data only and records nothing.
    /// </summary>
    public sealed class LeaderboardPresenter : MonoBehaviour
    {
        private void OnGUI()
        {
            if (HubTabs.Active != HubTab.Leaderboard)
            {
                return;
            }

            HubLayout.BeginContent();
            var history = SaveSystem.Instance?.Data?.runHistory;
            if (history == null || history.Count == 0)
            {
                GUILayout.Label("No runs recorded yet.");
                HubLayout.EndContent();
                return;
            }

            GUILayout.Label($"Best {history.Count} runs");
            for (var index = 0; index < history.Count; index++)
            {
                var record = history[index];
                if (record == null)
                {
                    continue;
                }

                GUILayout.Label(
                    $"{index + 1}. {record.score} pts  {record.distanceMeters:0} m  " +
                    $"{record.packagesDelivered} delivered  {record.districtReached}  {record.dateISO}");
            }

            HubLayout.EndContent();
        }
    }
}
