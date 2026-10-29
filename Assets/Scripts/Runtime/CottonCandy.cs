using UnityEngine;
using UnityEngine.Rendering;

namespace Wataame
{
    /// <summary>
    /// 棒についたわたあめ。飛んできた糸を受け止めて密度グリッドに積み、
    /// Surface Nets でメッシュ化して、毛皮 (シェル) 描画でふわふわに見せる。
    /// </summary>
    public sealed class CottonCandy : MonoBehaviour
    {
        // スマホ・タブレットでは描画とメッシュ更新を軽くする
        static readonly bool LowPower = Application.isMobilePlatform;
        static readonly int ShellCount = LowPower ? 8 : 14;
        static readonly float RemeshInterval = LowPower ? 0.1f : 0.06f;

        public CandyField Field { get; private set; }
        public float[] GramsByColor { get; } = new float[CandyPalette.Count];

        /// <summary>棒の軸からわたあめの表面までのおおよその半径 (m)。糸のねらいに使う。</summary>
        public float ApproxRadius { get; private set; } = GameConfig.StickRadius;

        /// <summary>わたあめの中心 (ワールド座標)。</summary>
        public Vector3 WorldCenter => transform.TransformPoint(localCenter);

        readonly SurfaceNets mesher = new SurfaceNets();
        Mesh mesh;
        Material material;
        MaterialPropertyBlock[] shellProps;
        int meshedVersion = -1;
        float lastRemesh;
        Vector3 localCenter = new Vector3(0f, 0.12f, 0f);

        void Awake()
        {
            Field = new CandyField(GameConfig.CandyGridPoints, GameConfig.CandyCellSize, GameConfig.CandyGridBottom,
                GameConfig.StickRadius, GameConfig.CandyDensityPerGram);

            mesh = new Mesh { name = "CottonCandy", indexFormat = IndexFormat.UInt32 };
            mesh.MarkDynamic();
            material = WataameMaterials.CottonCandy();

            var go = new GameObject("CandyMesh");
            go.transform.SetParent(transform, false);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var mr = go.AddComponent<MeshRenderer>();
            mr.sharedMaterial = material;
            mr.shadowCastingMode = ShadowCastingMode.Off;
            mr.receiveShadows = false;

            shellProps = new MaterialPropertyBlock[ShellCount];
            for (int i = 0; i < ShellCount; i++)
            {
                shellProps[i] = new MaterialPropertyBlock();
                shellProps[i].SetFloat("_Shell", (i + 1f) / ShellCount);
            }
        }

        /// <summary>
        /// ワールド座標の糸 (レイ) を受け止める。表面か棒に当たればそこに積もらせて true を返す。
        /// </summary>
        public bool TryCatch(Vector3 worldOrigin, Vector3 worldDirection, float maxDistance, float grams, Color color, int colorIndex, out Vector3 worldHit)
        {
            worldHit = default;
            Vector3 origin = transform.InverseTransformPoint(worldOrigin);
            Vector3 direction = transform.InverseTransformDirection(worldDirection);
            if (!Field.Trace(origin, direction, maxDistance, out Vector3 hit)) return false;

            // ほんの少し位置をばらつかせて、表面をふわっとさせる
            hit += Random.insideUnitSphere * (Field.Cell * 0.6f);
            Field.Deposit(hit, grams, color);
            if (colorIndex >= 0) GramsByColor[colorIndex] += grams;
            worldHit = transform.TransformPoint(hit);
            return true;
        }

        public void ResetCandy()
        {
            Field.Clear();
            System.Array.Clear(GramsByColor, 0, GramsByColor.Length);
            ApproxRadius = GameConfig.StickRadius;
            localCenter = new Vector3(0f, 0.12f, 0f);
            Remesh();
        }

        public CandyStats Evaluate() => CandyEvaluator.Evaluate(Field, GramsByColor);

        void LateUpdate()
        {
            if (Field.Version != meshedVersion && Time.unscaledTime - lastRemesh >= RemeshInterval)
                Remesh();

            if (mesh.vertexCount == 0) return;
            var rp = new RenderParams(material)
            {
                layer = gameObject.layer,
                shadowCastingMode = ShadowCastingMode.Off,
                receiveShadows = false,
                worldBounds = new Bounds(transform.TransformPoint(mesh.bounds.center), mesh.bounds.size + Vector3.one * 0.1f),
            };
            Matrix4x4 matrix = transform.localToWorldMatrix;
            for (int i = 0; i < ShellCount; i++)
            {
                rp.matProps = shellProps[i];
                Graphics.RenderMesh(rp, mesh, 0, matrix);
            }
        }

        void Remesh()
        {
            lastRemesh = Time.unscaledTime;
            meshedVersion = Field.Version;
            mesher.Build(Field);

            mesh.Clear();
            mesh.SetVertices(mesher.Vertices);
            mesh.SetNormals(mesher.Normals);
            mesh.SetColors(mesher.Colors);
            mesh.SetTriangles(mesher.Triangles, 0, true);

            // ねらい用の半径と中心を更新
            ApproxRadius = Field.MeasureRadius();
            if (mesher.Vertices.Count > 0)
            {
                Vector3 sum = Vector3.zero;
                foreach (var v in mesher.Vertices) sum += v;
                localCenter = sum / mesher.Vertices.Count;
            }
        }

        void OnDestroy()
        {
            if (mesh != null) Destroy(mesh);
        }
    }
}
