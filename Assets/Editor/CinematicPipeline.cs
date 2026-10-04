using System.IO;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Horizon.Editor
{
    // Generate serialized pipeline assets in the editor, never on a player's
    // device. CI and local editor use exactly the same deterministic settings.
    [InitializeOnLoad]
    public sealed class CinematicPipeline : IPreprocessBuildWithReport
    {
        public int callbackOrder { get { return -100; } }
        static CinematicPipeline() { EditorApplication.delayCall += Ensure; }
        public void OnPreprocessBuild(BuildReport report) { Ensure(); }
        [MenuItem("HORIZON/Configure cinematic URP")]
        public static void Ensure()
        {
            // TMP's shaders/settings ship as the package's essential resources.
            // Import them once so runtime-created Chinese captions survive stripping.
            if (Resources.Load<TMPro.TMP_Settings>("TMP Settings") == null)
            {
                var package = UnityEditor.PackageManager.PackageInfo.FindForAssembly(typeof(TMPro.TMP_Text).Assembly);
                string essentials = package == null ? null : Path.Combine(package.resolvedPath, "Package Resources", "TMP Essential Resources.unitypackage");
                if (essentials != null && File.Exists(essentials)) AssetDatabase.ImportPackage(essentials, false);
            }
            const string directory = "Assets/Settings";
            const string rendererPath = directory + "/HorizonRenderer.asset";
            const string pipelinePath = directory + "/HorizonURP.asset";
            if (!AssetDatabase.IsValidFolder(directory)) AssetDatabase.CreateFolder("Assets", "Settings");
            var renderer = AssetDatabase.LoadAssetAtPath<UniversalRendererData>(rendererPath);
            if (renderer == null)
            {
                renderer = ScriptableObject.CreateInstance<UniversalRendererData>();
                renderer.name = "HORIZON mobile forward renderer";
                renderer.renderingMode = RenderingMode.Forward;
                AssetDatabase.CreateAsset(renderer, rendererPath);
            }
            if (renderer.postProcessData == null)
            {
                renderer.postProcessData = AssetDatabase.LoadAssetAtPath<PostProcessData>(
                    "Packages/com.unity.render-pipelines.universal/Runtime/Data/PostProcessData.asset");
                EditorUtility.SetDirty(renderer);
            }
            var pipeline = AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>(pipelinePath);
            if (pipeline == null)
            {
                pipeline = UniversalRenderPipelineAsset.Create(renderer); pipeline.name = "HORIZON cinematic URP";
                pipeline.supportsHDR = true; pipeline.msaaSampleCount = 2; pipeline.renderScale = 1;
                pipeline.supportsCameraDepthTexture = false; pipeline.supportsCameraOpaqueTexture = false;
                pipeline.mainLightRenderingMode = LightRenderingMode.PerPixel;
                pipeline.additionalLightsRenderingMode = LightRenderingMode.PerPixel;
                pipeline.additionalLightsPerObjectLimit = 2; pipeline.shadowDistance = 22;
                AssetDatabase.CreateAsset(pipeline, pipelinePath);
            }
            GraphicsSettings.defaultRenderPipeline = pipeline;
            QualitySettings.renderPipeline = pipeline;
            EditorUtility.SetDirty(pipeline); AssetDatabase.SaveAssets();
        }
    }
}
