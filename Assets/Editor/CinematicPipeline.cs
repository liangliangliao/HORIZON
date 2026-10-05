using System.IO;
using System.Linq;
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
                var package = UnityEditor.PackageManager.PackageInfo.GetAllRegisteredPackages()
                    .FirstOrDefault(p => p.name == "com.unity.ugui");
                string essentials = package == null ? null : Path.Combine(package.resolvedPath, "Package Resources", "TMP Essential Resources.unitypackage");
                if (essentials == null || !File.Exists(essentials))
                    throw new BuildFailedException("TextMeshPro essential resources are unavailable.");
                AssetDatabase.ImportPackage(essentials, false);
                AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
                if (Resources.Load<TMPro.TMP_Settings>("TMP Settings") == null)
                    throw new BuildFailedException("TextMeshPro settings were not imported before building.");
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
                pipeline.supportsHDR = true; pipeline.renderScale = 1;
                pipeline.supportsCameraDepthTexture = false; pipeline.supportsCameraOpaqueTexture = false;
                // URP creates per-pixel lights by default; its mode setters are internal.
                pipeline.maxAdditionalLightsCount = 2; pipeline.shadowDistance = 22;
                AssetDatabase.CreateAsset(pipeline, pipelinePath);
            }
            // Start safely on GLES before player preferences are applied;
            // desktop preferences can enable multisampling on their own clone.
            pipeline.msaaSampleCount = 1;
            // GLES 3.0 drivers may advertise half-float targets but corrupt the
            // packed R11G11B10 HDR target. Keep separate RGBA half-float channels.
            pipeline.hdrColorBufferPrecision = HDRColorBufferPrecision._64Bits;
            GraphicsSettings.defaultRenderPipeline = pipeline;
            QualitySettings.renderPipeline = pipeline;
            EditorUtility.SetDirty(pipeline); AssetDatabase.SaveAssets();
        }
    }
}
