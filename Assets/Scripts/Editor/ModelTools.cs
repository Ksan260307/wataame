using System;
using System.Diagnostics;
using System.IO;
using UnityEditor;
using Debug = UnityEngine.Debug;

namespace Wataame.Editor
{
    /// <summary>Blender で作ったモデルのインポート設定と、エディタからの再生成。</summary>
    public sealed class ModelTools : AssetPostprocessor
    {
        const string ModelFolder = "Assets/Resources/Models/";

        void OnPreprocessModel()
        {
            if (!assetPath.StartsWith(ModelFolder)) return;
            var importer = (ModelImporter)assetImporter;
            importer.globalScale = 1f;
            importer.useFileScale = true;
            importer.materialImportMode = ModelImporterMaterialImportMode.None; // マテリアルはゲーム側で付ける
            importer.importAnimation = false;
            importer.animationType = ModelImporterAnimationType.None;
            importer.importCameras = false;
            importer.importLights = false;
            importer.importBlendShapes = false;
            importer.importNormals = ModelImporterNormals.Import;
            importer.isReadable = false;
        }

        [MenuItem("Wataame/Regenerate Models (Blender)")]
        public static void Regenerate()
        {
            string blender = FindBlender();
            if (blender == null)
            {
                Debug.LogError("[Wataame] Blender が見つかりません。環境変数 BLENDER_PATH に blender の実行ファイルを指定してください。");
                return;
            }

            var psi = new ProcessStartInfo(blender, "-b --factory-startup --python Blender/generate_models.py -- --out Assets/Resources/Models --blend Blender/wataame_models.blend")
            {
                WorkingDirectory = Directory.GetCurrentDirectory(),
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true,
            };
            using (var process = Process.Start(psi))
            {
                string output = process.StandardOutput.ReadToEnd() + process.StandardError.ReadToEnd();
                process.WaitForExit();
                if (process.ExitCode != 0)
                    Debug.LogError($"[Wataame] Blender failed ({process.ExitCode}):\n{output}");
                else
                    Debug.Log($"[Wataame] Models regenerated.\n{output}");
            }
            AssetDatabase.Refresh();
        }

        static string FindBlender()
        {
            string env = Environment.GetEnvironmentVariable("BLENDER_PATH");
            if (!string.IsNullOrEmpty(env) && File.Exists(env)) return env;

            string[] roots =
            {
                @"C:\Program Files\Blender Foundation",
                "/Applications/Blender.app/Contents/MacOS",
                "/usr/bin",
                "/snap/bin",
            };
            foreach (var root in roots)
            {
                if (!Directory.Exists(root)) continue;
                foreach (var file in Directory.GetFiles(root, "blender*", SearchOption.AllDirectories))
                {
                    string name = Path.GetFileName(file).ToLowerInvariant();
                    if (name == "blender.exe" || name == "blender") return file;
                }
            }
            return null;
        }
    }
}
