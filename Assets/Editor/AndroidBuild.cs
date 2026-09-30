using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace Horizon.Editor
{
    public static class AndroidBuild
    {
        public static void Build()
        {
            string output = Argument("-customBuildPath") ?? "build/Android/HORIZON.apk";
            var scenes = EditorBuildSettings.scenes.Where(scene => scene.enabled).Select(scene => scene.path).ToArray();
            if (scenes.Length == 0) throw new BuildFailedException("No enabled scenes for the Android build.");
            Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(output)));

            PlayerSettings.SetScriptingBackend(NamedBuildTarget.Android, ScriptingImplementation.IL2CPP);
            PlayerSettings.Android.targetArchitectures = AndroidArchitecture.ARMv7 | AndroidArchitecture.ARM64;
            PlayerSettings.defaultInterfaceOrientation = UIOrientation.Portrait;
            PlayerSettings.productName = "HORIZON";
            PlayerSettings.bundleVersion = Argument("-buildVersion") ?? "0.3.0";
            if (int.TryParse(Argument("-androidVersionCode"), out int versionCode))
                PlayerSettings.Android.bundleVersionCode = versionCode;
            EditorUserBuildSettings.buildAppBundle = false;
            EditorUserBuildSettings.exportAsGoogleAndroidProject = false;

            BuildReport report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
            {
                scenes = scenes,
                locationPathName = output,
                target = BuildTarget.Android,
                options = BuildOptions.None
            });
            if (report.summary.result != BuildResult.Succeeded)
                throw new BuildFailedException("Android build failed: " + report.summary.result);
            Debug.Log("HORIZON APK: ARMv7 + ARM64, " + report.summary.totalSize + " bytes, " + output);
        }

        private static string Argument(string key)
        {
            string[] arguments = Environment.GetCommandLineArgs();
            for (int i = 0; i < arguments.Length - 1; i++)
                if (string.Equals(arguments[i], key, StringComparison.OrdinalIgnoreCase)) return arguments[i + 1];
            return null;
        }
    }
}
