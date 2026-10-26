using System.Collections.Generic;
using UnityEngine;

namespace Wataame
{
    /// <summary>テーブルや背景、モデルが読めなかったときの代わりに使う簡単なメッシュ。</summary>
    public static class MeshBuilder
    {
        /// <summary>(半径, 高さ) のプロファイルを Y 軸まわりに回転させたメッシュ。下から上へ並べる。</summary>
        public static Mesh Lathe(string name, IList<Vector2> profile, int segments = 48)
        {
            var vertices = new List<Vector3>();
            var triangles = new List<int>();
            int ring = segments + 1;
            for (int p = 0; p < profile.Count; p++)
            {
                for (int s = 0; s <= segments; s++)
                {
                    float a = 2f * Mathf.PI * s / segments;
                    vertices.Add(new Vector3(Mathf.Cos(a) * profile[p].x, profile[p].y, Mathf.Sin(a) * profile[p].x));
                }
            }
            for (int p = 0; p < profile.Count - 1; p++)
            {
                for (int s = 0; s < segments; s++)
                {
                    int i0 = p * ring + s, i1 = i0 + 1, i2 = i0 + ring, i3 = i2 + 1;
                    triangles.Add(i0); triangles.Add(i2); triangles.Add(i1);
                    triangles.Add(i1); triangles.Add(i2); triangles.Add(i3);
                }
            }
            var mesh = new Mesh { name = name };
            mesh.SetVertices(vertices);
            mesh.SetTriangles(triangles, 0);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }

        /// <summary>内側を向いた球 (背景用)。</summary>
        public static Mesh InvertedSphere(string name, float radius, int segments = 32)
        {
            var profile = new List<Vector2>();
            for (int i = segments / 2; i >= 0; i--)
            {
                float a = Mathf.PI * i / (segments / 2) - Mathf.PI / 2f;
                profile.Add(new Vector2(Mathf.Cos(a) * radius, Mathf.Sin(a) * radius));
            }
            return Lathe(name, profile, segments);
        }

        /// <summary>XZ 平面の正方形 (影など)。UV は 0..1。</summary>
        public static Mesh Quad(string name, float size)
        {
            float h = size * 0.5f;
            var mesh = new Mesh { name = name };
            mesh.SetVertices(new List<Vector3> { new Vector3(-h, 0, -h), new Vector3(-h, 0, h), new Vector3(h, 0, h), new Vector3(h, 0, -h) });
            mesh.SetUVs(0, new List<Vector2> { new Vector2(0, 0), new Vector2(0, 1), new Vector2(1, 1), new Vector2(1, 0) });
            mesh.SetColors(new List<Color> { Color.white, Color.white, Color.white, Color.white });
            mesh.SetTriangles(new[] { 0, 1, 2, 0, 2, 3 }, 0);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }
    }
}
