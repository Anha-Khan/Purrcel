using System;
using System.Globalization;
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

            var scale = UiTheme.Scale;
            var width = HubLayout.ContentRect.width;

            // Card-per-run inside a scroll view. The row rect comes FROM the layout and
            // everything is drawn into it, so the list scrolls. Drawing into absolute
            // rects instead pins the rows to the screen and silently defeats the scroll.
            HubLayout.BeginContent(HubTab.Leaderboard);
            GUILayout.Label("BEST RUNS", HubSkin.Heading, GUILayout.Height(34f * scale));

            var history = SaveSystem.Instance?.Data?.runHistory;
            if (history == null || history.Count == 0)
            {
                GUILayout.Space(6f * scale);
                GUILayout.Label("No runs recorded yet. Finish a delivery to appear here.",
                    HubSkin.Row);
                HubLayout.EndContent();
                return;
            }

            GUILayout.Label($"{history.Count} RUNS ON RECORD", HubSkin.Small);
            GUILayout.Space(10f * scale);

            for (var index = 0; index < history.Count; index++)
            {
                var record = history[index];
                if (record == null)
                {
                    continue;
                }

                // Heights come from the styles, not literals. The score line sits in a 24px
                // box that a 23px Plus Jakarta Sans heading needs 29px for, so every score in
                // the table was showing only its top half.
                var headingHeight = UiTheme.LineHeight(HubSkin.Heading);
                var smallHeight = UiTheme.LineHeight(HubSkin.Small);
                var meta = $"{record.distanceMeters:0} m   -   {record.packagesDelivered} delivered";
                var metaHeight = UiTheme.MeasureHeight(HubSkin.Small, meta, 260f * scale);

                var rowWidth = width - 8f * scale;
                var rowHeight = 10f * scale + headingHeight + 4f * scale + metaHeight + 10f * scale;
                var row = GUILayoutUtility.GetRect(rowWidth, Mathf.Max(68f * scale, rowHeight),
                    GUILayout.Width(rowWidth));
                UiTheme.DrawTexture(row, UiTheme.Solid(index == 0
                    ? UiTheme.Fade(HubSkin.Honey, 0.26f)
                    : UiTheme.Fade(HubSkin.PaperEdge, 0.18f)));

                // The raw dateISO used to be dumped straight into this row, filling the
                // card with a full timestamp and pushing the numbers out of view.
                var metaY = row.y + 10f * scale + headingHeight + 4f * scale;
                UiTheme.LabelClipped(new Rect(row.x + 16f * scale, row.y + 10f * scale,
                        44f * scale, headingHeight),
                    $"#{index + 1}", HubSkin.Heading, HubSkin.Ink);
                UiTheme.LabelClipped(new Rect(row.x + 70f * scale, row.y + 10f * scale,
                        150f * scale, headingHeight),
                    $"{record.score:N0} pts", HubSkin.Heading, HubSkin.Terracotta);
                UiTheme.Label(new Rect(row.x + 70f * scale, metaY, 260f * scale, metaHeight),
                    meta, HubSkin.Small, HubSkin.InkSoft);
                UiTheme.LabelClipped(new Rect(row.xMax - 200f * scale, row.y + 10f * scale,
                        120f * scale, smallHeight),
                    record.districtReached, HubSkin.Small, HubSkin.InkSoft);
                UiTheme.LabelClipped(new Rect(row.xMax - 84f * scale, row.y + 10f * scale,
                        76f * scale, smallHeight),
                    ShortDate(record.dateISO), HubSkin.Small, HubSkin.InkSoft);
                GUILayout.Space(10f * scale);
            }

            HubLayout.EndContent();
        }

        /// <summary>
        /// ISO date to a short day-month. Unparseable input is passed through rather than
        /// blanked, so a bad save still shows something instead of an empty column.
        /// </summary>
        private static string ShortDate(string iso)
        {
            if (string.IsNullOrWhiteSpace(iso))
            {
                return string.Empty;
            }

            return DateTime.TryParse(iso, CultureInfo.InvariantCulture, DateTimeStyles.None,
                out var parsed)
                ? parsed.ToString("d MMM", CultureInfo.InvariantCulture)
                : iso;
        }
    }
}