using System;
using System.IO;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Wataame.Editor
{
    /// <summary>
    /// シーン生成とビルド。GitHub Actions (game-ci/unity-builder) からは
    /// buildMethod: Wataame.Editor.BuildScript.Build で呼ばれる。
    /// </summary>
    public static class BuildScript
    {
        public const string ScenePath = "Assets/Scenes/Main.unity";

        [MenuItem("Wataame/Setup Main Scene")]
        public static void SetupMainScene()
        {
            if (!File.Exists(ScenePath))
            {
                var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
                new GameObject("WataameGame").AddComponent<WataameGame>();
                Directory.CreateDirectory(Path.GetDirectoryName(ScenePath));
                EditorSceneManager.SaveScene(scene, ScenePath);
                Debug.Log($"[Wataame] Created {ScenePath}");
            }
            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };
            ConfigurePlayer();
            AssetDatabase.SaveAssets();
        }

        [MenuItem("Wataame/Build/Windows")]
        public static void BuildWindowsMenu() => BuildFor(BuildTarget.StandaloneWindows64, "Builds/Windows/Wataame.exe");

        [MenuItem("Wataame/Build/WebGL")]
        public static void BuildWebGLMenu() => BuildFor(BuildTarget.WebGL, "Builds/WebGL");

        /// <summary>CI 用。-buildTarget / -customBuildTarget と -customBuildPath を読む。</summary>
        public static void Build()
        {
            string targetName = GetArg("-customBuildTarget") ?? GetArg("-buildTarget") ?? EditorUserBuildSettings.activeBuildTarget.ToString();
            if (!Enum.TryParse(targetName, true, out BuildTarget target))
                Fail($"Unknown build target '{targetName}'");

            string path = GetArg("-customBuildPath") ?? DefaultPath(target);
            string version = GetArg("-buildVersion");
            if (!string.IsNullOrEmpty(version) && version != "none") PlayerSettings.bundleVersion = version;

            BuildFor(target, path);
        }

        static void BuildFor(BuildTarget target, string path)
        {
            SetupMainScene();
            var options = new BuildPlayerOptions
            {
                scenes = new[] { ScenePath },
                locationPathName = path,
                target = target,
                targetGroup = BuildPipeline.GetBuildTargetGroup(target),
                options = BuildOptions.None,
            };
            BuildReport report = BuildPipeline.BuildPlayer(options);
            BuildSummary summary = report.summary;
            Debug.Log($"[Wataame] Build {summary.result}: {target} -> {path} ({summary.totalSize / (1024 * 1024)} MB, {summary.totalTime})");
            if (summary.result != BuildResult.Succeeded)
                Fail($"Build failed with {summary.totalErrors} error(s)");
        }

        static void ConfigurePlayer()
        {
            PlayerSettings.companyName = "Wataame";
            PlayerSettings.productName = "わたあめメーカー";
            PlayerSettings.colorSpace = ColorSpace.Gamma;
            PlayerSettings.runInBackground = true;
            PlayerSettings.defaultScreenWidth = 1280;
            PlayerSettings.defaultScreenHeight = 720;
            PlayerSettings.fullScreenMode = FullScreenMode.Windowed;
            PlayerSettings.resizableWindow = true;
            PlayerSettings.WebGL.compressionFormat = WebGLCompressionFormat.Gzip;
            // GitHub Pages は Content-Encoding を付けないので、ブラウザ側で展開できるようにする
            PlayerSettings.WebGL.decompressionFallback = true;
        }

        static string DefaultPath(BuildTarget target)
        {
            return target == BuildTarget.WebGL ? "Builds/WebGL" : $"Builds/{target}/Wataame.exe";
        }

        static string GetArg(string name)
        {
            var args = Environment.GetCommandLineArgs();
            int i = Array.IndexOf(args, name);
            return i >= 0 && i + 1 < args.Length && !args[i + 1].StartsWith("-") ? args[i + 1] : null;
        }

        static void Fail(string message)
        {
            Debug.LogError("[Wataame] " + message);
            if (Application.isBatchMode) EditorApplication.Exit(1);
            throw new Exception(message);
        }
    }
}
