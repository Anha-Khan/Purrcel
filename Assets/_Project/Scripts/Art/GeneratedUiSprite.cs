using UnityEngine;

namespace CatCourier.Art
{
    public static class GeneratedUiSprite
    {
        public static void Draw(Sprite sprite, float width, float height)
        {
            var rect = GUILayoutUtility.GetRect(width, height, GUILayout.Width(width), GUILayout.Height(height));
            if (sprite == null || sprite.texture == null) return;
            var texture = sprite.texture;
            var pixels = sprite.textureRect;
            var uv = new Rect(pixels.x / texture.width, pixels.y / texture.height,
                pixels.width / texture.width, pixels.height / texture.height);
            GUI.DrawTextureWithTexCoords(rect, texture, uv, true);
        }
    }
}
