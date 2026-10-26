using System.Collections.Generic;
using UnityEngine;

namespace Wataame
{
    /// <summary>
    /// Blender で作った FBX (Resources/Models) を読み込み、大きさ・向きをそろえてマテリアルを付ける。
    /// FBX が無いときは簡単な回転体メッシュで代用する。
    /// </summary>
    public static class ModelLoader
    {
        public static Material MaterialFor(string partName)
        {
            switch (partName)
            {
                case "Bowl":
                case "Head":
                case "Spindle":
                    return WataameMaterials.Lit("Metal", new Color(0.86f, 0.88f, 0.92f), 0.85f, 0.85f, 0.15f);
                case "Panel":
                    return WataameMaterials.Lit("Accent", new Color(0.99f, 0.97f, 0.95f), 0.4f);
                case "Switch":
                    return WataameMaterials.Lit("Switch", new Color(1.00f, 0.45f, 0.58f), 0.6f);
                case "Lamp":
                    return WataameMaterials.Lit("Lamp", new Color(1.00f, 0.35f, 0.40f), 0.9f);
                case "Foot":
                case "SwitchPlate":
                    return WataameMaterials.Lit("Dark", new Color(0.42f, 0.36f, 0.42f), 0.3f);
                case "Stick":
                    return WataameMaterials.Lit("Wood", new Color(0.92f, 0.74f, 0.50f), 0.15f, 0f, 0.1f);
                default:
                    return WataameMaterials.Lit("Body", new Color(1.00f, 0.76f, 0.83f), 0.45f, 0f, 0.3f);
            }
        }

        /// <summary>わたあめ機を作る。返り値の Dictionary はパーツ名 → Transform。</summary>
        public static Dictionary<string, Transform> LoadMachine(Transform parent)
        {
            var prefab = Resources.Load<GameObject>("Models/WataameMachine");
            GameObject root = prefab != null ? Object.Instantiate(prefab, parent, false) : BuildFallbackMachine(parent);
            root.name = "WataameMachineModel";
            var parts = Collect(root.transform);

            Fit(root.transform, GameConfig.MachineWidth, horizontal: true);
            // 正面 (スイッチ) がカメラ側 (-Z) を向くようにする
            if (parts.TryGetValue("Switch", out var sw) && sw.position.z > parent.position.z)
                root.transform.Rotate(0f, 180f, 0f, Space.World);

            ApplyMaterials(root);
            return parts;
        }

        /// <summary>棒の見た目を作る。原点 = 先端、+Y 方向に伸びる。</summary>
        public static GameObject LoadStick(Transform parent)
        {
            var prefab = Resources.Load<GameObject>("Models/Stick");
            GameObject root;
            if (prefab != null)
            {
                root = Object.Instantiate(prefab, parent, false);
                Fit(root.transform, GameConfig.StickLength, horizontal: false);
            }
            else
            {
                root = new GameObject("Stick");
                root.transform.SetParent(parent, false);
                AddMesh(root.transform, "Stick", MeshBuilder.Lathe("Stick", new[]
                {
                    new Vector2(0f, 0f), new Vector2(0.0035f, 0.006f), new Vector2(GameConfig.StickRadius, 0.02f),
                    new Vector2(GameConfig.StickRadius, GameConfig.StickLength), new Vector2(0f, GameConfig.StickLength),
                }, 16));
            }
            root.name = "StickModel";
            ApplyMaterials(root);
            return root;
        }

        static Dictionary<string, Transform> Collect(Transform root)
        {
            var parts = new Dictionary<string, Transform>();
            foreach (var t in root.GetComponentsInChildren<Transform>(true))
                parts[t.name] = t;
            return parts;
        }

        static void ApplyMaterials(GameObject root)
        {
            foreach (var r in root.GetComponentsInChildren<MeshRenderer>(true))
            {
                var mat = MaterialFor(r.name);
                var mats = new Material[Mathf.Max(1, r.sharedMaterials.Length)];
                for (int i = 0; i < mats.Length; i++) mats[i] = mat;
                r.sharedMaterials = mats;
                r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                r.receiveShadows = false;
            }
        }

        /// <summary>
        /// 単位設定の違いで大きさが変わっても、期待する寸法に合わせる。
        /// 底面を親の原点 (y = 0) にそろえる。
        /// </summary>
        static void Fit(Transform root, float expected, bool horizontal)
        {
            var renderers = root.GetComponentsInChildren<Renderer>();
            if (renderers.Length == 0) return;
            Bounds b = renderers[0].bounds;
            foreach (var r in renderers) b.Encapsulate(r.bounds);

            float actual = horizontal ? Mathf.Max(b.size.x, b.size.z) : b.size.y;
            if (actual > 1e-5f && Mathf.Abs(actual / expected - 1f) > 0.05f)
            {
                root.localScale *= expected / actual;
                b = renderers[0].bounds;
                foreach (var r in renderers) b.Encapsulate(r.bounds);
            }
            Vector3 parentPos = root.parent != null ? root.parent.position : Vector3.zero;
            root.position += new Vector3(0f, parentPos.y - b.min.y, 0f);
        }

        static void AddMesh(Transform parent, string name, Mesh mesh, Vector3 position = default)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = position;
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            go.AddComponent<MeshRenderer>();
        }

        static GameObject BuildFallbackMachine(Transform parent)
        {
            Debug.LogWarning("[Wataame] Resources/Models/WataameMachine が見つからないので簡易モデルを使います。Blender/generate_models.py を実行してください。");
            var root = new GameObject("WataameMachine");
            root.transform.SetParent(parent, false);
            AddMesh(root.transform, "Base", MeshBuilder.Lathe("Base", new[]
            {
                new Vector2(0f, 0f), new Vector2(0.34f, 0f), new Vector2(0.34f, 0.17f), new Vector2(0f, 0.17f),
            }));
            AddMesh(root.transform, "Bowl", MeshBuilder.Lathe("Bowl", new[]
            {
                new Vector2(0.10f, 0.166f), new Vector2(0.24f, 0.205f), new Vector2(0.30f, 0.32f), new Vector2(0.31f, 0.40f),
                new Vector2(0.30f, 0.40f), new Vector2(0.292f, 0.32f), new Vector2(0.23f, 0.215f), new Vector2(0.10f, 0.178f),
            }));
            AddMesh(root.transform, "Head", MeshBuilder.Lathe("Head", new[]
            {
                new Vector2(0f, -0.035f), new Vector2(0.056f, -0.035f), new Vector2(0.056f, 0.03f), new Vector2(0f, 0.036f),
            }, 32), GameConfig.HeadCenter);
            AddMesh(root.transform, "Switch", MeshBuilder.Lathe("Switch", new[]
            {
                new Vector2(0f, 0f), new Vector2(0.012f, 0f), new Vector2(0.012f, 0.04f), new Vector2(0f, 0.04f),
            }, 12), new Vector3(-0.07f, 0.095f, -0.345f));
            AddMesh(root.transform, "Lamp", MeshBuilder.Lathe("Lamp", new[]
            {
                new Vector2(0f, -0.014f), new Vector2(0.014f, 0f), new Vector2(0f, 0.014f),
            }, 16), new Vector3(0.08f, 0.095f, -0.345f));
            return root;
        }
    }
}
