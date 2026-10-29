using UnityEngine;

namespace Wataame
{
    /// <summary>
    /// 棒の位置がわかるようにする目印。
    /// 先端の真下のマーカーとガイド線、ベストゾーンのリング、ヘッドに近すぎるときの警告、
    /// つかめるとき / つかんでいるときの棒の光。
    /// </summary>
    public sealed class StickFeedback : MonoBehaviour
    {
        static readonly Color BestColor = new Color(0.35f, 0.85f, 0.55f);
        static readonly Color GoodColor = new Color(1.00f, 0.75f, 0.35f);
        static readonly Color OutColor = new Color(0.55f, 0.50f, 0.58f);
        static readonly Color WarnColor = new Color(1.00f, 0.30f, 0.35f);

        StickController stick;
        CottonCandyMachine machine;
        Transform marker;
        Material markerMat, ringMat, warnMat, lineMat, stickMat;
        LineRenderer line;
        GameObject ring, warn;
        float flash;

        public void Build(StickController stickController, CottonCandyMachine candyMachine)
        {
            stick = stickController;
            machine = candyMachine;

            marker = CreateQuad("StickMarker", MeshBuilder.Quad("Marker", 0.05f), out markerMat, 0f).transform;

            var ringGo = CreateQuad("BestZone", MeshBuilder.Annulus("BestZone", StickController.BestRadiusMin, StickController.BestRadiusMax), out ringMat, 1f);
            ringGo.transform.position = new Vector3(0f, StickController.BestTipY, 0f);
            ring = ringGo;

            var warnGo = CreateQuad("HeadWarning", MeshBuilder.Annulus("HeadWarning", GameConfig.HeadRadius + 0.005f, GameConfig.HeadRadius + 0.05f), out warnMat, 1f);
            warnGo.transform.position = GameConfig.HeadCenter + Vector3.up * 0.04f;
            warn = warnGo;

            var lineGo = new GameObject("StickGuide");
            lineGo.transform.SetParent(transform, false);
            line = lineGo.AddComponent<LineRenderer>();
            lineMat = new Material(WataameMaterials.Particle("Guide")) { name = "GuideInstance" };
            lineMat.SetFloat("_Band", 1f);
            line.sharedMaterial = lineMat;
            line.positionCount = 2;
            line.startWidth = line.endWidth = 0.004f;
            line.useWorldSpace = true;
            line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            line.receiveShadows = false;

            // 棒の光 (つかめる / つかんでいる)
            var renderers = stick.GetComponentsInChildren<MeshRenderer>(true);
            foreach (var r in renderers)
            {
                if (r.name == "CandyMesh") continue;
                if (stickMat == null) stickMat = new Material(r.sharedMaterial) { name = "StickInstance" };
                r.sharedMaterial = stickMat;
            }

            stick.Bumped += _ => flash = 1f;
        }

        GameObject CreateQuad(string name, Mesh mesh, out Material material, float band)
        {
            var go = new GameObject(name);
            go.transform.SetParent(transform, false);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            material = new Material(WataameMaterials.Particle(name)) { name = name + "Instance" };
            material.SetFloat("_Band", band);
            var mr = go.AddComponent<MeshRenderer>();
            mr.sharedMaterial = material;
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            mr.receiveShadows = false;
            return go;
        }

        void LateUpdate()
        {
            bool show = !stick.Showcase;
            marker.gameObject.SetActive(show);
            line.enabled = show;

            var zone = stick.CurrentZone;
            Color zoneColor = zone == StickController.Zone.Best ? BestColor
                : zone == StickController.Zone.Good ? GoodColor
                : zone == StickController.Zone.TooClose ? WarnColor
                : zone == StickController.Zone.Outside ? OutColor : GoodColor;
            flash = Mathf.MoveTowards(flash, 0f, Time.deltaTime * 4f);
            Color c = Color.Lerp(zoneColor, WarnColor, flash);

            // 先端の真下のマーカーとガイド線
            Vector3 tip = stick.transform.position;
            float r = new Vector2(tip.x, tip.z).magnitude;
            float groundY = (r < GameConfig.BowlRimRadius ? GameConfig.BowlSurfaceY(r) : 0f) + 0.003f;
            marker.position = new Vector3(tip.x, groundY, tip.z);
            float pulse = 1f + 0.12f * Mathf.Sin(Time.time * 6f);
            marker.localScale = Vector3.one * (zone == StickController.Zone.Best ? pulse : 1f);
            markerMat.SetColor("_Tint", new Color(c.r, c.g, c.b, 0.8f));
            line.SetPosition(0, marker.position);
            line.SetPosition(1, tip);
            lineMat.SetColor("_Tint", new Color(c.r, c.g, c.b, 0.6f));

            // ベストゾーン: 機械が動いているか、棒がボウルに近いときだけ見せる
            bool showRing = show && (machine.IsOn || zone != StickController.Zone.Outside);
            ring.SetActive(showRing);
            if (showRing)
            {
                float a = zone == StickController.Zone.Best ? 0.30f + 0.12f * Mathf.Sin(Time.time * 5f) : 0.16f;
                ringMat.SetColor("_Tint", new Color(BestColor.r, BestColor.g, BestColor.b, a));
            }

            bool danger = show && (zone == StickController.Zone.TooClose || stick.LastContact.Head);
            warn.SetActive(danger);
            if (danger) warnMat.SetColor("_Tint", new Color(WarnColor.r, WarnColor.g, WarnColor.b, 0.35f + 0.2f * Mathf.Sin(Time.time * 10f)));

            if (stickMat != null)
            {
                float glow = stick.Grabbing ? 0.22f : stick.Hovering ? 0.12f : 0f;
                stickMat.SetColor("_Emission", new Color(1f, 0.75f, 0.85f) * glow);
            }
        }
    }
}
