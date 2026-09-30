using UnityEngine;

namespace CatCourier.UI
{
    public static class HubLayout
    {
        public static Rect ContentRect => new(38f, 174f,
            Screen.width * 0.63f - 74f, Mathf.Max(100f, Screen.height - 240f));

        public static void BeginContent() => GUILayout.BeginArea(ContentRect);

        public static void EndContent() => GUILayout.EndArea();
    }
}
