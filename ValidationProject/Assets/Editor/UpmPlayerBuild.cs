using System;
using System.IO;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace HighPerfUI.Validation
{
    public static class UpmPlayerBuild
    {
        public static void Build()
        {
            var scenes = AssetDatabase.FindAssets("Inventory t:Scene", new[] { "Assets/Samples" });
            if (scenes.Length != 1) throw new InvalidOperationException("Import exactly one Armory sample scene.");
            string scene = AssetDatabase.GUIDToAssetPath(scenes[0]);
            string output = Path.GetFullPath(Path.Combine(Application.dataPath, "../Builds/Windows/HighPerfUI.exe"));
            Directory.CreateDirectory(Path.GetDirectoryName(output));
            PlayerSettings.productName = "SLG Armory UPM Validation"; PlayerSettings.companyName = "HighPerfUI";
            PlayerSettings.defaultScreenWidth = 1440; PlayerSettings.defaultScreenHeight = 900;
            PlayerSettings.fullScreenMode = FullScreenMode.Windowed; PlayerSettings.resizableWindow = true;
            PlayerSettings.runInBackground = true;
            PlayerSettings.SetScriptingBackend(BuildTargetGroup.Standalone, ScriptingImplementation.Mono2x);
            var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions {
                scenes = new[] { scene }, locationPathName = output,
                target = BuildTarget.StandaloneWindows64, options = BuildOptions.Development
            });
            if (report.summary.result != BuildResult.Succeeded) throw new Exception("Package sample build failed: " + report.summary.result);
            Debug.Log("[HighPerfUI] Installed-package sample built: " + output);
        }
    }
}
