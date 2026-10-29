using UnityEngine;

namespace Wataame
{
    /// <summary>UI 用の GUIStyle とテクスチャ (角丸) をコードで作る。</summary>
    public sealed class UiStyles
    {
        public static readonly Color Ink = new Color(0.42f, 0.29f, 0.37f);
        public static readonly Color Pink = new Color(1.00f, 0.50f, 0.66f);
        public static readonly Color Mint = new Color(0.42f, 0.80f, 0.62f);
        public static readonly Color Track = new Color(0.97f, 0.88f, 0.92f);

        public readonly GUIStyle Panel, Sheet, Title, TitleShadow, TitleSmall, TitleSmallShadow, Heading, Label, Value, SmallValue, Status, Small, Big, Stars, Comment, Center, Hint;
        public readonly GUIStyle Button, AltButton, SmallButton, OnButton, OffButton, ActiveButton;
        public readonly GUIStyle Slider, Thumb, MidThumb, BigThumb, Fill, Outline;
        public readonly Texture2D Circle, Ring, Spinner;

        public UiStyles()
        {
            var font = Resources.Load<Font>("Fonts/MPLUSRounded1c-Bold");

            Circle = Rounded(64, 32f, Color.white);
            Ring = RingTexture(64, 29f, 3.5f);
            Spinner = SpinnerTexture(64);

            Panel = new GUIStyle { normal = { background = Rounded(64, 20f, new Color(1f, 1f, 1f, 0.92f)) }, border = new RectOffset(22, 22, 22, 22) };
            Sheet = new GUIStyle { normal = { background = Rounded(64, 24f, new Color(1f, 0.97f, 0.98f, 1f)) }, border = new RectOffset(26, 26, 26, 26) };
            Fill = new GUIStyle { normal = { background = Rounded(32, 8f, Color.white) }, border = new RectOffset(9, 9, 9, 9) };
            Outline = new GUIStyle { normal = { background = OutlineTexture(64, 22f, 4f) }, border = new RectOffset(24, 24, 24, 24) };

            Title = Text(font, 38, Pink, TextAnchor.UpperLeft);
            Heading = Text(font, 19, Ink, TextAnchor.MiddleLeft);
            Label = Text(font, 16, Ink, TextAnchor.MiddleLeft);
            Value = Text(font, 16, Ink, TextAnchor.MiddleRight);
            Small = Text(font, 14, new Color(Ink.r, Ink.g, Ink.b, 0.85f), TextAnchor.MiddleLeft);
            Big = Text(font, 28, Pink, TextAnchor.MiddleLeft);
            Comment = Text(font, 16, Ink, TextAnchor.UpperLeft);
            Comment.wordWrap = true;
            TitleShadow = Text(font, 38, Color.white, TextAnchor.UpperLeft);
            TitleSmall = Text(font, 26, Pink, TextAnchor.MiddleLeft);
            TitleSmallShadow = Text(font, 26, Color.white, TextAnchor.MiddleLeft);
            SmallValue = Text(font, 14, Ink, TextAnchor.MiddleRight);
            Status = Text(font, 16, Pink, TextAnchor.MiddleRight);
            Stars = Text(font, 26, new Color(1f, 0.72f, 0.25f), TextAnchor.MiddleLeft);
            Center = Text(font, 15, Ink, TextAnchor.MiddleCenter);
            Hint = Text(font, 14, Color.white, TextAnchor.MiddleCenter);
            Hint.normal.background = Rounded(32, 12f, new Color(0.42f, 0.29f, 0.37f, 0.85f));
            Hint.border = new RectOffset(13, 13, 13, 13);
            Hint.padding = new RectOffset(10, 10, 4, 4);

            Button = MakeButton(font, Pink, Color.white);
            AltButton = MakeButton(font, new Color(0.95f, 0.91f, 1.00f), Ink);
            OnButton = MakeButton(font, Mint, Color.white);
            OffButton = MakeButton(font, new Color(1.00f, 0.58f, 0.52f), Color.white);
            OnButton.fontSize = OffButton.fontSize = 22;
            SmallButton = MakeButton(font, new Color(0.95f, 0.91f, 1.00f), Ink);
            SmallButton.fontSize = 14;
            ActiveButton = MakeButton(font, new Color(1.00f, 0.72f, 0.82f), Color.white);
            ActiveButton.fontSize = 17;
            AltButton.fontSize = 17;

            // トラックは Fill で自前で描くので、スライダー本体は透明
            Slider = new GUIStyle();
            Thumb = new GUIStyle
            {
                normal = { background = Rounded(64, 32f, Pink) },
                hover = { background = Rounded(64, 32f, Color.Lerp(Pink, Color.white, 0.2f)) },
                active = { background = Rounded(64, 32f, Color.Lerp(Pink, Color.black, 0.1f)) },
                fixedWidth = 26f,
                fixedHeight = 26f,
            };
            // 縦の高さスライダー用 (指でつまみやすい大きさ)
            BigThumb = new GUIStyle(Thumb) { fixedWidth = 44f, fixedHeight = 44f };
            MidThumb = new GUIStyle(Thumb) { fixedWidth = 34f, fixedHeight = 34f };
        }

        static GUIStyle Text(Font font, int size, Color color, TextAnchor anchor)
        {
            return new GUIStyle { font = font, fontSize = size, alignment = anchor, normal = { textColor = color }, richText = true };
        }

        static GUIStyle MakeButton(Font font, Color color, Color text)
        {
            var style = new GUIStyle
            {
                font = font,
                fontSize = 19,
                alignment = TextAnchor.MiddleCenter,
                border = new RectOffset(18, 18, 18, 18),
                normal = { background = Rounded(64, 18f, color), textColor = text },
                hover = { background = Rounded(64, 18f, Color.Lerp(color, Color.white, 0.18f)), textColor = text },
                active = { background = Rounded(64, 18f, Color.Lerp(color, Color.black, 0.12f)), textColor = text },
            };
            return style;
        }

        /// <summary>アンチエイリアスつきの角丸四角形テクスチャ。</summary>
        static Texture2D Rounded(int size, float radius, Color color)
        {
            var tex = NewTexture(size);
            var pixels = new Color[size * size];
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float px = Mathf.Max(Mathf.Abs(x + 0.5f - size * 0.5f) - (size * 0.5f - radius), 0f);
                float py = Mathf.Max(Mathf.Abs(y + 0.5f - size * 0.5f) - (size * 0.5f - radius), 0f);
                float alpha = Mathf.Clamp01(radius - Mathf.Sqrt(px * px + py * py) + 0.5f);
                pixels[y * size + x] = new Color(color.r, color.g, color.b, color.a * alpha);
            }
            tex.SetPixels(pixels);
            tex.Apply();
            return tex;
        }

        static Texture2D RingTexture(int size, float radius, float width)
        {
            var tex = NewTexture(size);
            var pixels = new Color[size * size];
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float d = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(size * 0.5f, size * 0.5f));
                float alpha = Mathf.Clamp01(width * 0.5f - Mathf.Abs(d - radius) + 0.5f);
                pixels[y * size + x] = new Color(1f, 1f, 1f, alpha);
            }
            tex.SetPixels(pixels);
            tex.Apply();
            return tex;
        }

        /// <summary>角丸四角形のふち取りだけ (次にやることの強調用)。</summary>
        static Texture2D OutlineTexture(int size, float radius, float width)
        {
            var tex = NewTexture(size);
            var pixels = new Color[size * size];
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float px = Mathf.Max(Mathf.Abs(x + 0.5f - size * 0.5f) - (size * 0.5f - radius), 0f);
                float py = Mathf.Max(Mathf.Abs(y + 0.5f - size * 0.5f) - (size * 0.5f - radius), 0f);
                float d = radius - Mathf.Sqrt(px * px + py * py); // 外周からの距離
                float alpha = Mathf.Clamp01(d + 0.5f) * Mathf.Clamp01(width - d + 0.5f);
                pixels[y * size + x] = new Color(1f, 1f, 1f, alpha);
            }
            tex.SetPixels(pixels);
            tex.Apply();
            return tex;
        }

        /// <summary>回っていることを表すすき間のあるリング (回転させて使う)。</summary>
        static Texture2D SpinnerTexture(int size)
        {
            var tex = NewTexture(size);
            var pixels = new Color[size * size];
            float radius = size * 0.36f, width = size * 0.11f;
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                var p = new Vector2(x + 0.5f - size * 0.5f, y + 0.5f - size * 0.5f);
                float angle = Mathf.Repeat(Mathf.Atan2(p.y, p.x) * Mathf.Rad2Deg, 360f);
                float ring = Mathf.Clamp01(width * 0.5f - Mathf.Abs(p.magnitude - radius) + 0.5f);
                float arc = Mathf.Clamp01((angle - 20f) / 4f) * Mathf.Clamp01((330f - angle) / 4f);
                pixels[y * size + x] = new Color(1f, 1f, 1f, ring * Mathf.Lerp(0.25f, 1f, arc) * (0.4f + 0.6f * angle / 360f));
            }
            tex.SetPixels(pixels);
            tex.Apply();
            return tex;
        }

        static Texture2D NewTexture(int size)
        {
            return new Texture2D(size, size, TextureFormat.RGBA32, false)
            {
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear,
                hideFlags = HideFlags.HideAndDontSave,
            };
        }
    }
}
