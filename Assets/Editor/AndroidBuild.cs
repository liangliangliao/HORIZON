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
            bool release = string.Equals(Argument("-horizonChannel"), "release", StringComparison.OrdinalIgnoreCase);
            PlayerSettings.companyName = "HORIZON";
            PlayerSettings.productName = release ? "HORIZON" : "HORIZON 试玩";
            PlayerSettings.SetApplicationIdentifier(NamedBuildTarget.Android,
                release ? "com.liangliangliao.horizon" : "com.liangliangliao.horizon.preview");
            PlayerSettings.bundleVersion = Argument("-horizonVersion") ?? "0.4.0";
            if (int.TryParse(Argument("-horizonBuildNumber") ?? Argument("-androidVersionCode"), out int versionCode))
                PlayerSettings.Android.bundleVersionCode = versionCode;
            else PlayerSettings.Android.bundleVersionCode = 400;
            PlayerSettings.Android.minSdkVersion = AndroidSdkVersions.AndroidApiLevel23;
            PlayerSettings.SetManagedStrippingLevel(NamedBuildTarget.Android, ManagedStrippingLevel.Low);
            ConfigureSigning(release);
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

        private static void ConfigureSigning(bool release)
        {
            PlayerSettings.Android.useCustomKeystore = true;
            if (!release)
            {
                // This public QA key intentionally signs only the separate preview
                // package. A production build can never fall back to it.
                PlayerSettings.Android.keystoreName = Path.GetFullPath("tools/signing/horizon-preview.keystore");
                PlayerSettings.Android.keystorePass = "public-horizon-preview";
                PlayerSettings.Android.keyaliasName = "horizon-preview";
                PlayerSettings.Android.keyaliasPass = "public-horizon-preview";
                return;
            }
            string[] keys = { "HORIZON_RELEASE_KEYSTORE", "HORIZON_RELEASE_STORE_PASS",
                "HORIZON_RELEASE_ALIAS", "HORIZON_RELEASE_KEY_PASS" };
            string[] values = keys.Select(Environment.GetEnvironmentVariable).ToArray();
            if (values.Any(string.IsNullOrEmpty) || !File.Exists(values[0]))
                throw new BuildFailedException("Release signing must be configured independently. The public preview key is forbidden for releases.");
            PlayerSettings.Android.keystoreName = Path.GetFullPath(values[0]);
            PlayerSettings.Android.keystorePass = values[1];
            PlayerSettings.Android.keyaliasName = values[2];
            PlayerSettings.Android.keyaliasPass = values[3];
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
