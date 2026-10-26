using UnityEngine;

namespace Wataame
{
    public struct CandyStats
    {
        public float Grams;
        /// <summary>横幅 (直径) と高さ (cm)。</summary>
        public float WidthCm, HeightCm;
        /// <summary>0..1。1 で完全な球。</summary>
        public float Roundness;
        /// <summary>高さ / 横幅。</summary>
        public float Elongation;
        /// <summary>重心が棒からどれだけずれているか (0..1)。</summary>
        public float Offset;
        public Vector3 LocalCenter;
        public int Stars;
        public string Name;
        public string Comment;
    }

    /// <summary>できあがったわたあめの形を評価して、名前とコメントをつける。</summary>
    public static class CandyEvaluator
    {
        public static CandyStats Evaluate(CandyField field, float[] gramsByColor)
        {
            var stats = new CandyStats { Grams = field.TotalGrams };

            // 内側のセルの重心と分散を求める
            double n = 0, sx = 0, sy = 0, sz = 0, sxx = 0, syy = 0, szz = 0, sxz = 0;
            Vector3 min = Vector3.positiveInfinity, max = Vector3.negativeInfinity;
            for (int z = 0; z < field.NZ; z++)
            for (int y = 0; y < field.NY; y++)
            for (int x = 0; x < field.NX; x++)
            {
                if (field.Density[field.Index(x, y, z)] <= CandyField.Iso) continue;
                Vector3 p = field.PointPosition(x, y, z);
                n++;
                sx += p.x; sy += p.y; sz += p.z;
                sxx += p.x * p.x; syy += p.y * p.y; szz += p.z * p.z; sxz += p.x * p.z;
                min = Vector3.Min(min, p);
                max = Vector3.Max(max, p);
            }

            if (n < 8)
            {
                stats.Name = "ちいさな わたあめ";
                stats.Comment = "もっと ざらめを まきとってみよう";
                stats.Stars = 1;
                return stats;
            }

            double mx = sx / n, my = sy / n, mz = sz / n;
            double vx = sxx / n - mx * mx, vy = syy / n - my * my, vz = szz / n - mz * mz, cxz = sxz / n - mx * mz;
            // XZ 平面の共分散行列の固有値 (棒が傾いていても横方向の扁平さを測る)
            double tr = vx + vz, det = vx * vz - cxz * cxz;
            double disc = System.Math.Sqrt(System.Math.Max(0, tr * tr / 4 - det));
            double ha = System.Math.Sqrt(System.Math.Max(1e-12, tr / 2 + disc));
            double hb = System.Math.Sqrt(System.Math.Max(1e-12, tr / 2 - disc));
            double v = System.Math.Sqrt(System.Math.Max(1e-12, vy));

            double largest = System.Math.Max(ha, System.Math.Max(hb, v));
            double smallest = System.Math.Min(ha, System.Math.Min(hb, v));

            stats.LocalCenter = new Vector3((float)mx, (float)my, (float)mz);
            stats.Roundness = Mathf.Clamp01((float)(smallest / largest));
            stats.Elongation = (float)(v / ((ha + hb) * 0.5));
            Vector3 size = max - min + Vector3.one * field.Cell;
            stats.WidthCm = Mathf.Max(size.x, size.z) * 100f;
            stats.HeightCm = size.y * 100f;
            float radius = Mathf.Max(size.x, size.z) * 0.5f;
            stats.Offset = Mathf.Clamp01(new Vector2((float)mx, (float)mz).magnitude / Mathf.Max(radius, 1e-4f));

            string shape;
            if (stats.Elongation > 1.5f)
            {
                shape = "タワー";
                stats.Comment = "たてながの ふしぎな わたあめ！";
            }
            else if (stats.Elongation < 0.6f)
            {
                shape = "UFO";
                stats.Comment = "ひらべったくて とんでいきそう！";
            }
            else if (stats.Offset > 0.35f)
            {
                shape = "ひつじ";
                stats.Comment = "かたよった ユニークな かたち！";
            }
            else if (stats.Roundness > 0.82f)
            {
                shape = "くも";
                stats.Comment = "まんまる！ おみせの わたあめ みたい";
            }
            else
            {
                shape = "ふわふわ";
                stats.Comment = "ふわふわで おいしそう！";
            }

            stats.Name = ColorName(gramsByColor) + "の" + shape;
            float score = 1f + Mathf.Clamp01(stats.Grams / 15f) * 2.2f + stats.Roundness * 1.4f + (shape == "くも" ? 0.4f : 0f);
            stats.Stars = Mathf.Clamp(Mathf.RoundToInt(score), 1, 5);
            return stats;
        }

        static string ColorName(float[] gramsByColor)
        {
            float total = 0f, best = 0f;
            int bestIndex = 0, used = 0;
            for (int i = 0; i < gramsByColor.Length; i++) total += gramsByColor[i];
            if (total <= 0f) return CandyPalette.Entries[2].Name;
            for (int i = 0; i < gramsByColor.Length; i++)
            {
                if (gramsByColor[i] / total > 0.2f) used++;
                if (gramsByColor[i] > best)
                {
                    best = gramsByColor[i];
                    bestIndex = i;
                }
            }
            return used >= 2 ? "ミックス" : CandyPalette.Entries[bestIndex].Name;
        }
    }
}
