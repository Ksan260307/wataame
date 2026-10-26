using UnityEngine;

namespace Wataame
{
    /// <summary>ざらめ (= わたあめ) の色。</summary>
    public static class CandyPalette
    {
        public struct Entry
        {
            public string Name;
            public Color Color;
            public bool Rainbow;
        }

        public static readonly Entry[] Entries =
        {
            new Entry { Name = "ピンク", Color = new Color(1.00f, 0.62f, 0.78f) },
            new Entry { Name = "みずいろ", Color = new Color(0.56f, 0.80f, 1.00f) },
            new Entry { Name = "しろ", Color = new Color(0.98f, 0.97f, 0.96f) },
            new Entry { Name = "レモン", Color = new Color(1.00f, 0.92f, 0.52f) },
            new Entry { Name = "メロン", Color = new Color(0.64f, 0.92f, 0.62f) },
            new Entry { Name = "ぶどう", Color = new Color(0.78f, 0.64f, 0.98f) },
            new Entry { Name = "にじいろ", Color = new Color(1.00f, 0.80f, 0.80f), Rainbow = true },
        };

        public static int Count => Entries.Length;

        /// <summary>色を評価する。にじいろは時間で色相が変わる。</summary>
        public static Color Evaluate(int index, float time)
        {
            var entry = Entries[Mathf.Clamp(index, 0, Entries.Length - 1)];
            if (!entry.Rainbow) return entry.Color;
            return Color.HSVToRGB(Mathf.Repeat(time * 0.22f, 1f), 0.42f, 1f);
        }
    }
}
