// BuildAndroid.cs — Editor menu to build Android APK for Quest 2.
// Menu: SoccerBot → Build Android APK

#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;
using System.IO;

public static class BuildAndroid
{
    [MenuItem("SoccerBot/Build Android APK")]
    public static void BuildAPK()
    {
        var outDir = Path.Combine(Application.dataPath, "../../build");
        Directory.CreateDirectory(outDir);
        var apkPath = Path.Combine(outDir, "SoccerBot.apk");

        if (EditorUserBuildSettings.activeBuildTarget != BuildTarget.Android)
            EditorUserBuildSettings.SwitchActiveBuildTarget(BuildTargetGroup.Android, BuildTarget.Android);

        PlayerSettings.Android.targetArchitectures = AndroidArchitecture.ARM64;
        PlayerSettings.SetScriptingBackend(BuildTargetGroup.Android, ScriptingImplementation.IL2CPP);

        var scenes = new[] { "Assets/Scenes/Main.unity" };
        var report = BuildPipeline.BuildPlayer(scenes, apkPath, BuildTarget.Android, BuildOptions.None);

        if (report.summary.result == BuildResult.Succeeded)
            Debug.Log($"[BuildAndroid] OK -> {apkPath} ({report.summary.totalSize / 1024 / 1024} MB)");
        else
        {
            var message = $"[BuildAndroid] FAILED: {report.summary.totalErrors} error(s), " +
                          $"{report.summary.totalWarnings} warning(s). See Console or Editor.log for details.";
            Debug.LogError(message);

            if (Application.isBatchMode)
                EditorApplication.Exit(1);
            else
                EditorUtility.DisplayDialog("Android build failed", message, "OK");
        }
    }
}
#endif
