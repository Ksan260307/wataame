using System.Collections.Generic;
using UnityEngine;

namespace Wataame
{
    /// <summary>
    /// マテリアルとライティングの共通設定。シェーダーは自前のライティングで描くので
    /// Built-in / URP どちらのレンダーパイプラインでも同じ見た目になる。
    /// </summary>
    public static class WataameMaterials
    {
        public static readonly Color SkyColor = new Color(0.80f, 0.90f, 1.00f);
        public static readonly Color GroundColor = new Color(1.00f, 0.86f, 0.88f);

        static readonly Dictionary<string, Material> cache = new Dictionary<string, Material>();

        public static void ApplyLighting()
        {
            Shader.SetGlobalVector("_WataameLightDir", new Vector3(0.45f, 0.85f, -0.5f).normalized);
            Shader.SetGlobalColor("_WataameLightColor", new Color(1.0f, 0.97f, 0.92f) * 0.85f);
            Shader.SetGlobalColor("_WataameSkyColor", SkyColor * 0.55f);
            Shader.SetGlobalColor("_WataameGroundColor", GroundColor * 0.35f);
        }

        public static Material Lit(string key, Color color, float gloss = 0.35f, float metal = 0f, float rim = 0.25f)
        {
            if (cache.TryGetValue(key, out var cached)) return cached;
            var mat = new Material(FindShader("Wataame/Lit")) { name = key };
            mat.SetColor("_Color", color);
            mat.SetFloat("_Gloss", gloss);
            mat.SetFloat("_Metal", metal);
            mat.SetFloat("_Rim", rim);
            cache[key] = mat;
            return mat;
        }

        public static Material CottonCandy() => Create("CottonCandy", "Wataame/CottonCandy");
        public static Material Backdrop() => Create("Backdrop", "Wataame/Backdrop");

        public static Material Particle(string key) => Create(key, "Wataame/Particle");

        static Material Create(string key, string shader)
        {
            if (cache.TryGetValue(key, out var cached)) return cached;
            var mat = new Material(FindShader(shader)) { name = key };
            cache[key] = mat;
            return mat;
        }

        static Shader FindShader(string name)
        {
            var shader = Shader.Find(name);
            if (shader == null)
            {
                Debug.LogError($"[Wataame] Shader '{name}' not found. Assets/Resources/Shaders を確認してください。");
                shader = Shader.Find("Hidden/InternalErrorShader");
            }
            return shader;
        }
    }
}
