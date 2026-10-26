using UnityEngine;

namespace Wataame
{
    /// <summary>
    /// 棒のまわりのわたあめを表す 3D 密度グリッド (棒のローカル座標)。
    /// 飛んできた糸 (レイ) が表面か棒に引っかかった位置に密度を積み上げる。
    /// 密度が <see cref="Iso"/> を超えた領域がわたあめ。
    /// </summary>
    public sealed class CandyField
    {
        public const float Iso = 1f;
        public const float CatchThreshold = Iso * 0.8f;

        public readonly int NX, NY, NZ;
        public readonly float Cell;
        /// <summary>格子点 (0,0,0) のローカル座標。</summary>
        public readonly Vector3 Origin;
        public readonly float StickRadius;
        public readonly float DensityPerGram;

        public readonly float[] Density;
        readonly Vector3[] colorSum;

        // 正規化済みのガウスカーネル (半径 2 セル)
        const int KernelRadius = 2;
        static readonly float[] Kernel = BuildKernel();

        public CandyField(Vector3Int points, float cell, float bottom, float stickRadius, float densityPerGram)
        {
            NX = points.x;
            NY = points.y;
            NZ = points.z;
            Cell = cell;
            StickRadius = stickRadius;
            DensityPerGram = densityPerGram;
            Origin = new Vector3(-(NX - 1) * 0.5f * cell, bottom, -(NZ - 1) * 0.5f * cell);
            Density = new float[NX * NY * NZ];
            colorSum = new Vector3[Density.Length];
        }

        /// <summary>引っかかったわたあめの合計 (g)。</summary>
        public float TotalGrams { get; private set; }

        /// <summary>Deposit されるたびに増える。メッシュの再生成判定に使う。</summary>
        public int Version { get; private set; }

        public Vector3 Size => new Vector3((NX - 1) * Cell, (NY - 1) * Cell, (NZ - 1) * Cell);

        public int Index(int x, int y, int z) => (z * NY + y) * NX + x;

        public Vector3 PointPosition(int x, int y, int z) => Origin + new Vector3(x, y, z) * Cell;

        public void Clear()
        {
            System.Array.Clear(Density, 0, Density.Length);
            System.Array.Clear(colorSum, 0, colorSum.Length);
            TotalGrams = 0f;
            Version++;
        }

        /// <summary>トリリニア補間した密度。グリッド外は 0。</summary>
        public float Sample(Vector3 local)
        {
            Vector3 g = (local - Origin) / Cell;
            int x0 = Mathf.FloorToInt(g.x), y0 = Mathf.FloorToInt(g.y), z0 = Mathf.FloorToInt(g.z);
            if (x0 < 0 || y0 < 0 || z0 < 0 || x0 >= NX - 1 || y0 >= NY - 1 || z0 >= NZ - 1) return 0f;

            float fx = g.x - x0, fy = g.y - y0, fz = g.z - z0;
            int i000 = Index(x0, y0, z0);
            int sy = NX, sz = NX * NY;
            float c00 = Mathf.Lerp(Density[i000], Density[i000 + 1], fx);
            float c10 = Mathf.Lerp(Density[i000 + sy], Density[i000 + sy + 1], fx);
            float c01 = Mathf.Lerp(Density[i000 + sz], Density[i000 + sz + 1], fx);
            float c11 = Mathf.Lerp(Density[i000 + sy + sz], Density[i000 + sy + sz + 1], fx);
            return Mathf.Lerp(Mathf.Lerp(c00, c10, fy), Mathf.Lerp(c01, c11, fy), fz);
        }

        /// <summary>中心差分による密度の勾配 (わたあめの内側を向く)。</summary>
        public Vector3 Gradient(Vector3 local)
        {
            float h = Cell;
            return new Vector3(
                Sample(local + new Vector3(h, 0, 0)) - Sample(local - new Vector3(h, 0, 0)),
                Sample(local + new Vector3(0, h, 0)) - Sample(local - new Vector3(0, h, 0)),
                Sample(local + new Vector3(0, 0, h)) - Sample(local - new Vector3(0, 0, h)));
        }

        /// <summary>近くの格子点に積もった色の平均。</summary>
        public Color SampleColor(Vector3 local)
        {
            Vector3 g = (local - Origin) / Cell;
            int cx = Mathf.Clamp(Mathf.RoundToInt(g.x), 1, NX - 2);
            int cy = Mathf.Clamp(Mathf.RoundToInt(g.y), 1, NY - 2);
            int cz = Mathf.Clamp(Mathf.RoundToInt(g.z), 1, NZ - 2);
            Vector3 sum = Vector3.zero;
            float weight = 0f;
            for (int z = cz - 1; z <= cz + 1; z++)
            for (int y = cy - 1; y <= cy + 1; y++)
            for (int x = cx - 1; x <= cx + 1; x++)
            {
                int i = Index(x, y, z);
                sum += colorSum[i];
                weight += Density[i];
            }
            if (weight <= 1e-5f) return Color.white;
            sum /= weight;
            return new Color(sum.x, sum.y, sum.z, 1f);
        }

        /// <summary>
        /// ローカル座標のレイを進め、わたあめの表面か棒に当たった位置 (の少し手前) を返す。
        /// </summary>
        public bool Trace(Vector3 origin, Vector3 direction, float maxDistance, out Vector3 hit)
        {
            hit = default;
            direction.Normalize();
            if (!IntersectBounds(origin, direction, out float tEnter, out float tExit)) return false;

            tExit = Mathf.Min(tExit, maxDistance);
            float step = Cell * 0.5f;
            // 棒は細いので、棒の近くでは刻みを細かくする
            float stickCatch = StickRadius + Cell * 0.35f;
            for (float t = Mathf.Max(0f, tEnter); t <= tExit; t += step)
            {
                Vector3 p = origin + direction * t;
                bool nearStick = p.y >= 0f && p.x * p.x + p.z * p.z <= stickCatch * stickCatch;
                if (nearStick || Sample(p) >= CatchThreshold)
                {
                    hit = origin + direction * Mathf.Max(0f, t - step);
                    return true;
                }
            }
            return false;
        }

        /// <summary>位置 local に grams 分の糸を色 color で積もらせる。</summary>
        public void Deposit(Vector3 local, float grams, Color color)
        {
            float amount = grams * DensityPerGram;
            Vector3 g = (local - Origin) / Cell;
            int cx = Mathf.RoundToInt(g.x), cy = Mathf.RoundToInt(g.y), cz = Mathf.RoundToInt(g.z);
            var rgb = new Vector3(color.r, color.g, color.b);
            int k = 0;
            for (int dz = -KernelRadius; dz <= KernelRadius; dz++)
            for (int dy = -KernelRadius; dy <= KernelRadius; dy++)
            for (int dx = -KernelRadius; dx <= KernelRadius; dx++, k++)
            {
                int x = cx + dx, y = cy + dy, z = cz + dz;
                // 外周の格子点は常に 0 にしておき、メッシュが必ず閉じるようにする
                if (x < 1 || y < 1 || z < 1 || x > NX - 2 || y > NY - 2 || z > NZ - 2) continue;
                int i = Index(x, y, z);
                float add = amount * Kernel[k];
                Density[i] += add;
                colorSum[i] += rgb * add;
            }
            TotalGrams += grams;
            Version++;
        }

        /// <summary>棒の軸からいちばん遠いわたあめ (密度 &gt; Iso) までの水平距離。</summary>
        public float MeasureRadius()
        {
            float best = StickRadius;
            for (int z = 0; z < NZ; z++)
            for (int y = 0; y < NY; y++)
            for (int x = 0; x < NX; x++)
            {
                if (Density[Index(x, y, z)] <= Iso) continue;
                Vector3 p = PointPosition(x, y, z);
                best = Mathf.Max(best, Mathf.Sqrt(p.x * p.x + p.z * p.z));
            }
            return best;
        }

        bool IntersectBounds(Vector3 origin, Vector3 dir, out float tEnter, out float tExit)
        {
            Vector3 min = Origin, max = Origin + Size;
            tEnter = float.NegativeInfinity;
            tExit = float.PositiveInfinity;
            for (int a = 0; a < 3; a++)
            {
                if (Mathf.Abs(dir[a]) < 1e-8f)
                {
                    if (origin[a] < min[a] || origin[a] > max[a]) return false;
                    continue;
                }
                float t1 = (min[a] - origin[a]) / dir[a];
                float t2 = (max[a] - origin[a]) / dir[a];
                if (t1 > t2) (t1, t2) = (t2, t1);
                tEnter = Mathf.Max(tEnter, t1);
                tExit = Mathf.Min(tExit, t2);
            }
            return tExit >= Mathf.Max(tEnter, 0f);
        }

        static float[] BuildKernel()
        {
            int size = KernelRadius * 2 + 1;
            var kernel = new float[size * size * size];
            float sum = 0f;
            int k = 0;
            for (int z = -KernelRadius; z <= KernelRadius; z++)
            for (int y = -KernelRadius; y <= KernelRadius; y++)
            for (int x = -KernelRadius; x <= KernelRadius; x++, k++)
            {
                float w = Mathf.Exp(-(x * x + y * y + z * z) / 2f);
                kernel[k] = w;
                sum += w;
            }
            for (int i = 0; i < kernel.Length; i++) kernel[i] /= sum;
            return kernel;
        }
    }
}
