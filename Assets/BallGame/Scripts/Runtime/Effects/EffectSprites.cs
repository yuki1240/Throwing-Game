using UnityEngine;

namespace BallGame
{
    /// <summary>
    /// 演出で使う形。
    /// </summary>
    public enum EffectShape
    {
        /// <summary>ふちのぼけた円。</summary>
        SoftCircle = 0,

        /// <summary>輪。</summary>
        Ring = 1,

        /// <summary>細い輪。</summary>
        ThinRing = 2,

        /// <summary>小さな点。</summary>
        Dot = 3,

        /// <summary>花びら。</summary>
        Petal = 4,

        /// <summary>光の筋。</summary>
        Bar = 5
    }

    /// <summary>
    /// 演出用の白い形をプログラムで作り、使い回します（色は Image 側で付けます）。
    /// ドメイン再読込で消えても、使うときに作り直します。
    /// </summary>
    public static class EffectSprites
    {
        private const int TextureSize = 128;
        private static Sprite[] sprites;

        /// <summary>
        /// 形の Sprite を返します。
        /// </summary>
        /// <param name="shape">形</param>
        /// <returns>Sprite</returns>
        public static Sprite Get(EffectShape shape)
        {
            int index = (int)shape;
            if (sprites == null)
            {
                sprites = new Sprite[System.Enum.GetValues(typeof(EffectShape)).Length];
            }

            if (sprites[index] == null)
            {
                sprites[index] = CreateSprite(shape);
            }

            return sprites[index];
        }

        private static Sprite CreateSprite(EffectShape shape)
        {
            Texture2D texture = new Texture2D(TextureSize, TextureSize, TextureFormat.RGBA32, false);
            texture.name = "Effect " + shape;
            texture.wrapMode = TextureWrapMode.Clamp;
            texture.hideFlags = HideFlags.DontSave;
            Color32[] pixels = new Color32[TextureSize * TextureSize];
            for (int y = 0; y < TextureSize; y++)
            {
                for (int x = 0; x < TextureSize; x++)
                {
                    float alpha = Mathf.Clamp01(EvaluateAlpha(shape, ToLocal(x), ToLocal(y)));
                    pixels[y * TextureSize + x] = new Color32(255, 255, 255, (byte)Mathf.RoundToInt(alpha * 255f));
                }
            }

            texture.SetPixels32(pixels);
            texture.Apply(false, true);
            Sprite sprite = Sprite.Create(texture, new Rect(0, 0, TextureSize, TextureSize), new Vector2(0.5f, 0.5f), 100f);
            sprite.hideFlags = HideFlags.DontSave;
            return sprite;
        }

        private static float ToLocal(int pixel)
        {
            return (pixel + 0.5f) / TextureSize * 2f - 1f;
        }

        private static float EvaluateAlpha(EffectShape shape, float x, float y)
        {
            float radius = Mathf.Sqrt(x * x + y * y);
            switch (shape)
            {
                case EffectShape.SoftCircle:
                    return Mathf.Pow(Mathf.Clamp01(1f - radius), 1.6f);
                case EffectShape.Ring:
                    return Band(radius, 0.82f, 0.1f);
                case EffectShape.ThinRing:
                    return Band(radius, 0.9f, 0.05f);
                case EffectShape.Dot:
                    return Edge(1f - radius, 0.08f);
                case EffectShape.Petal:
                    return PetalAlpha(x, y);
                default:
                    return BarAlpha(x, y);
            }
        }

        private static float Band(float radius, float center, float halfWidth)
        {
            return Edge(halfWidth - Mathf.Abs(radius - center), 0.04f);
        }

        private static float Edge(float signedDistance, float softness)
        {
            return Mathf.Clamp01(signedDistance / softness);
        }

        private static float PetalAlpha(float x, float y)
        {
            // 縦長で先がとがった形
            float width = 0.55f * Mathf.Sqrt(Mathf.Clamp01(1f - y * y)) * (1f - 0.35f * y);
            return Edge(width - Mathf.Abs(x), 0.06f);
        }

        private static float BarAlpha(float x, float y)
        {
            float vertical = Mathf.Clamp01(1f - Mathf.Abs(y) / 0.18f);
            float horizontal = Mathf.Clamp01(1f - Mathf.Abs(x));
            return vertical * Mathf.Pow(horizontal, 0.7f);
        }
    }
}
