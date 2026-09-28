using UnityEngine;

namespace CatCourier.UI
{
    public static class HubLayout
    {
        public static Rect ContentRect => new(24f, 84f, Screen.width - 48f, Screen.height - 120f);

        public static void BeginContent() => GUILayout.BeginArea(ContentRect);

        public static void EndContent() => GUILayout.EndArea();
    }
}
