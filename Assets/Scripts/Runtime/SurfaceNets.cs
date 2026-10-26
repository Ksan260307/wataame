using System.Collections.Generic;
using UnityEngine;

namespace Wataame
{
    /// <summary>
    /// Naive Surface Nets で <see cref="CandyField"/> の等値面をメッシュにする。
    /// 表面と交わるセルごとに 1 頂点を置き、符号が変わる格子辺ごとに四角形を張る。
    /// </summary>
    public sealed class SurfaceNets
    {
        public readonly List<Vector3> Vertices = new List<Vector3>();
        public readonly List<Vector3> Normals = new List<Vector3>();
        public readonly List<Color> Colors = new List<Color>();
        public readonly List<int> Triangles = new List<int>();

        int[] cellVertex = new int[0];
        bool[] inside = new bool[0];
        readonly float[] corner = new float[8];

        static readonly Vector3[] CornerOffsets =
        {
            new Vector3(0, 0, 0), new Vector3(1, 0, 0), new Vector3(0, 1, 0), new Vector3(1, 1, 0),
            new Vector3(0, 0, 1), new Vector3(1, 0, 1), new Vector3(0, 1, 1), new Vector3(1, 1, 1),
        };

        public void Build(CandyField field)
        {
            Vertices.Clear();
            Normals.Clear();
            Colors.Clear();
            Triangles.Clear();

            int nx = field.NX, ny = field.NY, nz = field.NZ;
            int cx = nx - 1, cy = ny - 1, cz = nz - 1;
            if (cellVertex.Length != cx * cy * cz) cellVertex = new int[cx * cy * cz];
            if (inside.Length != field.Density.Length) inside = new bool[field.Density.Length];

            var density = field.Density;
            for (int i = 0; i < density.Length; i++) inside[i] = density[i] > CandyField.Iso;

            int sy = nx, sz = nx * ny;

            // 1. 頂点
            for (int z = 0; z < cz; z++)
            for (int y = 0; y < cy; y++)
            for (int x = 0; x < cx; x++)
            {
                int cell = (z * cy + y) * cx + x;
                int p = field.Index(x, y, z);
                int mask = 0;
                for (int c = 0; c < 8; c++)
                {
                    int idx = p + (c & 1) + ((c >> 1) & 1) * sy + ((c >> 2) & 1) * sz;
                    corner[c] = density[idx];
                    if (inside[idx]) mask |= 1 << c;
                }
                if (mask == 0 || mask == 255)
                {
                    cellVertex[cell] = -1;
                    continue;
                }

                Vector3 sum = Vector3.zero;
                int count = 0;
                for (int c = 0; c < 8; c++)
                {
                    for (int bit = 1; bit < 8; bit <<= 1)
                    {
                        if ((c & bit) != 0) continue;
                        int c2 = c | bit;
                        bool a = (mask & (1 << c)) != 0, b = (mask & (1 << c2)) != 0;
                        if (a == b) continue;
                        float t = (CandyField.Iso - corner[c]) / (corner[c2] - corner[c]);
                        sum += Vector3.Lerp(CornerOffsets[c], CornerOffsets[c2], t);
                        count++;
                    }
                }

                Vector3 local = field.PointPosition(x, y, z) + sum / count * field.Cell;
                Vector3 grad = field.Gradient(local);
                cellVertex[cell] = Vertices.Count;
                Vertices.Add(local);
                Normals.Add(grad.sqrMagnitude > 1e-12f ? -grad.normalized : Vector3.up);
                Colors.Add(field.SampleColor(local));
            }

            // 2. 面 (符号が変わる格子辺のまわりの 4 セルをつなぐ)
            for (int z = 1; z < nz - 1; z++)
            for (int y = 1; y < ny - 1; y++)
            for (int x = 1; x < nx - 1; x++)
            {
                int p = field.Index(x, y, z);
                bool here = inside[p];
                // X 方向の辺
                if (here != inside[p + 1])
                    AddQuad(Cell(x, y - 1, z - 1), Cell(x, y, z - 1), Cell(x, y, z), Cell(x, y - 1, z), new Vector3(here ? 1 : -1, 0, 0));
                // Y 方向の辺
                if (here != inside[p + sy])
                    AddQuad(Cell(x - 1, y, z - 1), Cell(x, y, z - 1), Cell(x, y, z), Cell(x - 1, y, z), new Vector3(0, here ? 1 : -1, 0));
                // Z 方向の辺
                if (here != inside[p + sz])
                    AddQuad(Cell(x - 1, y - 1, z), Cell(x, y - 1, z), Cell(x, y, z), Cell(x - 1, y, z), new Vector3(0, 0, here ? 1 : -1));
            }

            int Cell(int x, int y, int z) => cellVertex[(z * cy + y) * cx + x];
        }

        void AddQuad(int a, int b, int c, int d, Vector3 outward)
        {
            if (a < 0 || b < 0 || c < 0 || d < 0) return;
            Vector3 va = Vertices[a], vb = Vertices[b], vc = Vertices[c], vd = Vertices[d];
            // Unity は時計回りが表。Cross(c-a, d-b) が外向きなら a,b,c,d の順で表になる
            bool forward = Vector3.Dot(Vector3.Cross(vc - va, vd - vb), outward) > 0f;
            if (!forward) (b, d) = (d, b);
            Triangles.Add(a); Triangles.Add(b); Triangles.Add(c);
            Triangles.Add(a); Triangles.Add(c); Triangles.Add(d);
        }
    }
}
