using UnityEditor;
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