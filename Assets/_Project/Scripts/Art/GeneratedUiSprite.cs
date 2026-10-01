using UnityEngine;

namespace CatCourier.Art
{
    public static class GeneratedUiSprite
    {
        public static void Draw(Sprite sprite, float width, float height)
        {
            Draw(GUILayoutUtility.GetRect(width, height, GUILayout.Width(width), GUILayout.Height(height)), sprite);
        }

        /// <summary>
        /// Draws at an explicit rect. Required wherever the caller is positioning by hand
        /// rather than through GUILayout: mixing the two coordinate systems in one pass
        /// puts content in the wrong place.
        /// </summary>
        public static void Draw(Rect rect, Sprite sprite)
        {
            if (sprite == null || sprite.texture == null) return;
            var texture = sprite.texture;
            var pixels = sprite.textureRect;
            var uv = new Rect(pixels.x / texture.width, pixels.y / texture.height,
                pixels.width / texture.width, pixels.height / texture.height);
            GUI.DrawTextureWithTexCoords(rect, texture, uv, true);
        }
    }
}
