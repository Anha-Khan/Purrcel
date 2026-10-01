using UnityEngine;

namespace CatCourier.UI
{
    public static class HubLayout
    {
        /// <summary>
        /// The screen rect with the notch cut out of it. Every menu area is inset by
        /// this, otherwise a notched phone in landscape puts the buttons under the
        /// camera cutout.
        /// </summary>
        public static Rect SafeRect
        {
            get
            {
                var safe = Screen.safeArea;
                // A zero or full-screen rect means no safe area was reported.
                if (safe.width <= 0f || safe.height <= 0f)
                {
                    return new Rect(0f, 0f, Screen.width, Screen.height);
                }

                return new Rect(safe.x, safe.y, safe.width, safe.height);
            }
        }

        public static Rect ContentRect
        {
            get
            {
                var safe = SafeRect;
                var scale = UiTheme.Scale;
                var top = Screen.height - safe.yMax + 176f * scale;
                return new Rect(
                    safe.x + 24f * scale + 18f * scale,
                    top + 18f * scale,
                    Mathf.Max(100f, safe.width * 0.63f - 48f * scale - 36f * scale),
                    Mathf.Max(100f, safe.height - 176f * scale - 88f * scale - 36f * scale));
            }
        }

        /// <summary>
        /// A rect positioned from the safe area's left edge and top, where xOffset is
        /// the historical horizontal offset (which may be negative) and top is the
        /// distance down from the safe top.
        /// </summary>
        public static Rect HubRect(float xOffset, float top, float width, float height)
        {
            var safe = SafeRect;
            return new Rect(
                safe.x + xOffset + safe.width * 0.63f,
                Screen.height - safe.yMax + top,
                Mathf.Min(width, safe.width),
                Mathf.Min(height, safe.height));
        }

        /// <summary>
        /// Per-tab scroll offsets, so switching tabs does not inherit the last one's
        /// position. Declared here because every sibling tab presenter scrolls.
        /// </summary>
        private static readonly System.Collections.Generic.Dictionary<HubTab, Vector2> Scrolls =
            new System.Collections.Generic.Dictionary<HubTab, Vector2>();

        /// <summary>
        /// Opens the shared content rect for a sibling tab presenter, wrapped in a scroll
        /// view. The upgrades and cats lists are taller than the card on a phone, so
        /// without this the tail of the list is simply unreachable.
        /// </summary>
        public static void BeginContent(HubTab tab)
        {
            GUILayout.BeginArea(ContentRect);
            if (!Scrolls.TryGetValue(tab, out var position))
            {
                position = Vector2.zero;
            }

            position = GUILayout.BeginScrollView(position, false, true);
            Scrolls[tab] = position;
        }

        /// <summary>Closes the scroll view opened by <see cref="BeginContent"/>, then the area.</summary>
        public static void EndContent()
        {
            GUILayout.EndScrollView();
            GUILayout.EndArea();
        }
    }
}
