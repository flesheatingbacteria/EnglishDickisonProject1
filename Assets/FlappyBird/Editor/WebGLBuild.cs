using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace FlappyBird.EditorTools
{
    // Builds the game for the web (Flappy Bird > Build WebGL), into Builds/WebGL.
    // From a terminal: Unity -batchmode -buildTarget WebGL -executeMethod FlappyBird.EditorTools.WebGLBuild.Build
    public static class WebGLBuild
    {
        public const string Output = "Builds/WebGL";

        [MenuItem("Flappy Bird/Build WebGL")]
        public static void Build()
        {
            // Gzip with a built-in decompressor: static hosts such as GitHub Pages can't send the
            // headers that compressed Unity builds otherwise need.
            PlayerSettings.WebGL.compressionFormat = WebGLCompressionFormat.Gzip;
            PlayerSettings.WebGL.decompressionFallback = true;
            BuildReport report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
            {
                scenes = new[] { "Assets/Scenes/SampleScene.unity" },
                locationPathName = Output,
                target = BuildTarget.WebGL,
                options = BuildOptions.None
            });
            bool ok = report.summary.result == BuildResult.Succeeded;
            Debug.Log($"WebGL build {report.summary.result}: {Output} ({report.summary.totalSize / (1024 * 1024)} MB, {report.summary.totalErrors} errors)");
            if (Application.isBatchMode) EditorApplication.Exit(ok ? 0 : 1);
        }
    }
}
