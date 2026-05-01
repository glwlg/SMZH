using UnityEngine;

namespace XTD.Presentation
{
    public static class RuntimeSpriteFactory
    {
        private static Sprite unitSprite;
        private static Sprite projectileSprite;
        private static Sprite effectSprite;

        public static Sprite UnitSprite => unitSprite != null ? unitSprite : unitSprite = CreateSquareSprite(24, Color.white);
        public static Sprite ProjectileSprite => projectileSprite != null ? projectileSprite : projectileSprite = CreateCircleSprite(16, Color.white);
        public static Sprite EffectSprite => effectSprite != null ? effectSprite : effectSprite = CreateCircleSprite(32, Color.white);

        private static Sprite CreateSquareSprite(int size, Color color)
        {
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Point
            };

            var pixels = new Color[size * size];
            for (var i = 0; i < pixels.Length; i++)
            {
                pixels[i] = color;
            }

            texture.SetPixels(pixels);
            texture.Apply();
            return Sprite.Create(texture, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), size);
        }

        private static Sprite CreateCircleSprite(int size, Color color)
        {
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Bilinear
            };

            var center = (size - 1) * 0.5f;
            var radius = size * 0.45f;
            for (var y = 0; y < size; y++)
            {
                for (var x = 0; x < size; x++)
                {
                    var distance = Vector2.Distance(new Vector2(x, y), new Vector2(center, center));
                    var alpha = Mathf.Clamp01(1f - (distance - radius + 2f) / 2f);
                    texture.SetPixel(x, y, new Color(color.r, color.g, color.b, alpha));
                }
            }

            texture.Apply();
            return Sprite.Create(texture, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), size);
        }
    }
}
