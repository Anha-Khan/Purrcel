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

        /// <summary>A rect inset from <see cref="SafeRect"/> on every edge.</summary>
        public static Rect SafeArea(float margin = 0f)
        {
            var safe = SafeRect;
            margin = Mathf.Max(0f, margin);
            var rect = new Rect(
                safe.x + margin,
                safe.y + margin,
                Mathf.Max(1f, safe.width - margin * 2f),
                Mathf.Max(1f, safe.height - margin * 2f));
            // Unity draws IMGUI areas from the top-left; safeArea.y is bottom-up.
            return new Rect(rect.x, Screen.height - rect.yMax, rect.width, rect.height);
        }

        public static Rect ContentRect
        {
            get
            {
                var safe = SafeRect;
                var left = safe.x + 38f;
                var top = Screen.height - safe.yMax + 174f;
                return new Rect(left, top,
                    Mathf.Max(100f, safe.width * 0.63f - 74f),
                    Mathf.Max(100f, safe.height - 240f));
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

        public static void BeginContent() => GUILayout.BeginArea(ContentRect);

        public static void EndContent() => GUILayout.EndArea();
    }
}
