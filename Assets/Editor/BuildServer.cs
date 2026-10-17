using System.IO;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

public static class BuildServer
{
    public static void Build()
    {
        string outputPath = "Builds/Server/ATITProject.exe";

        var options = new BuildPlayerOptions
        {
            scenes = new[] { "Assets/Scenes/SampleScene.unity" },
            locationPathName = outputPath,
            target = BuildTarget.StandaloneWindows64,
            subtarget = (int)StandaloneBuildSubtarget.Server,
            options = BuildOptions.None
        };

        BuildReport report = BuildPipeline.BuildPlayer(options);

        if (report.summary.result == BuildResult.Succeeded)
            Debug.Log($"Server build succeeded: {outputPath}");
        else
            Debug.LogError($"Server build failed: {report.summary.totalErrors} errors");
    }
}

public class CertPostprocessor : IPostprocessBuildWithReport
{
    public int callbackOrder => 0;

    public void OnPostprocessBuild(BuildReport report)
    {
        string buildDir  = Path.GetDirectoryName(report.summary.outputPath) ?? "";
        string destCerts = Path.Combine(buildDir, "Assets", "Certs");
        Directory.CreateDirectory(destCerts);

        CopyCert("ca.crt",     destCerts, required: true);
        CopyCert("device.crt", destCerts, required: false);
        CopyCert("device.key", destCerts, required: false);
    }

    private static void CopyCert(string filename, string destDir, bool required)
    {
        string src = Path.Combine("Assets", "Certs", filename);
        if (!File.Exists(src))
        {
            if (required)
                Debug.LogError($"[CertPostprocessor] Missing required cert: {src}");
            else
                Debug.LogWarning($"[CertPostprocessor] {src} not found — copy manually before running the server");
            return;
        }
        File.Copy(src, Path.Combine(destDir, filename), overwrite: true);
        Debug.Log($"[CertPostprocessor] Copied {filename} → build output");
    }
}