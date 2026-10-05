using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEditor.Rendering;
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
            const string typographySettings = "Assets/TextMesh Pro/Resources/TMP Settings.asset";
            if (AssetDatabase.LoadAssetAtPath<TMPro.TMP_Settings>(typographySettings) == null)
            {
                var package = UnityEditor.PackageManager.PackageInfo.GetAllRegisteredPackages()
                    .FirstOrDefault(p => p.name == "com.unity.ugui");
                string essentials = package == null ? null : Path.Combine(package.resolvedPath, "Package Resources", "TMP Essential Resources.unitypackage");
                if (essentials == null || !File.Exists(essentials))
                    throw new BuildFailedException("TextMeshPro essential resources are unavailable.");
                UnpackTypography(essentials);
                AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
                if (AssetDatabase.LoadAssetAtPath<TMPro.TMP_Settings>(typographySettings) == null)
                    throw new BuildFailedException("TextMeshPro settings were not imported before building: " +
                        Path.Combine(Application.dataPath, "TextMesh Pro/Resources/TMP Settings.asset"));
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
            ConfigureMobileRenderPath(directory);
            EditorUtility.SetDirty(pipeline); AssetDatabase.SaveAssets();
        }

        private static void ConfigureMobileRenderPath(string directory)
        {
            // RenderGraphSettings is build configuration, not a runtime quality
            // setting. Unity 6 throws in a player even when assigning the same
            // value. Serialize the GLES-compatible path before shader stripping.
            var global = EditorGraphicsSettings.GetRenderPipelineGlobalSettingsAsset<UniversalRenderPipeline>();
            if (global == null)
            {
                string path = directory + "/HorizonURPGlobalSettings.asset";
                global = AssetDatabase.LoadAssetAtPath<RenderPipelineGlobalSettings>(path);
                if (global == null)
                {
                    // URP 17's concrete settings type is internal. The public
                    // creation API accepts its Type and populates all resources.
                    Type type = typeof(UniversalRenderPipelineAsset).Assembly.GetType(
                        "UnityEngine.Rendering.Universal.UniversalRenderPipelineGlobalSettings", true);
                    global = RenderPipelineGlobalSettingsUtils.Create(type, path);
                }
            }
            if (global == null) throw new BuildFailedException("URP global settings could not be created.");
            EditorGraphicsSettings.SetRenderPipelineGlobalSettingsAsset<UniversalRenderPipeline>(global);
            if (!EditorGraphicsSettings.TryGetRenderPipelineSettingsForPipeline<RenderGraphSettings, UniversalRenderPipeline>(out var graph))
                throw new BuildFailedException("URP Render Graph settings were not populated before building.");
            graph.enableRenderCompatibilityMode = true;
            EditorUtility.SetDirty(global);
        }

        private sealed class PackageAsset
        {
            public string Path;
            public byte[] Asset, Meta;
        }

        // ImportPackage queues editor work even in batch mode. Read the official
        // package's GUID/asset, asset.meta and pathname entries directly, keeping
        // its original GUIDs so all TMP font, material and shader references work.
        private static void UnpackTypography(string packagePath)
        {
            var assets = new Dictionary<string, PackageAsset>();
            var entryNames = new List<string>();
            using (var file = File.OpenRead(packagePath))
            using (var archive = new GZipStream(file, CompressionMode.Decompress))
            {
                var header = new byte[512];
                while (ReadBlock(archive, header, true))
                {
                    if (header.All(b => b == 0)) break;
                    string name = Encoding.UTF8.GetString(header, 0, 100).TrimEnd('\0');
                    if (entryNames.Count < 4) entryNames.Add(name);
                    string sizeText = Encoding.ASCII.GetString(header, 124, 12).Trim('\0', ' ');
                    long size = string.IsNullOrEmpty(sizeText) ? 0 : Convert.ToInt64(sizeText, 8);
                    if (size < 0 || size > 64 * 1024 * 1024)
                        throw new BuildFailedException("Invalid TMP package entry size.");
                    var data = new byte[(int)size]; ReadBlock(archive, data, false);
                    var padding = new byte[(int)((512 - size % 512) % 512)]; ReadBlock(archive, padding, false);
                    string[] parts = name.Replace('\\', '/').Split(new[] { '/' }, StringSplitOptions.RemoveEmptyEntries);
                    if (parts.Length < 2) continue;
                    string guid = parts[parts.Length - 2], leaf = parts[parts.Length - 1];
                    if (!Guid.TryParseExact(guid, "N", out _)) continue;
                    if (!assets.TryGetValue(guid, out PackageAsset asset))
                        assets.Add(guid, asset = new PackageAsset());
                    if (leaf == "pathname") asset.Path = Encoding.UTF8.GetString(data).TrimEnd('\0', '\r', '\n');
                    else if (leaf == "asset") asset.Asset = data;
                    else if (leaf == "asset.meta") asset.Meta = data;
                }
            }
            string project = System.IO.Path.GetDirectoryName(Application.dataPath);
            string root = System.IO.Path.GetFullPath(System.IO.Path.Combine(Application.dataPath, "TextMesh Pro"));
            int count = 0;
            foreach (PackageAsset asset in assets.Values)
            {
                if (string.IsNullOrEmpty(asset.Path)) continue;
                string target = System.IO.Path.GetFullPath(System.IO.Path.Combine(project, asset.Path));
                if (target != root && !target.StartsWith(root + System.IO.Path.DirectorySeparatorChar, StringComparison.Ordinal))
                    throw new BuildFailedException("TMP package contains an unexpected asset path.");
                Directory.CreateDirectory(System.IO.Path.GetDirectoryName(target));
                if (asset.Asset != null) File.WriteAllBytes(target, asset.Asset);
                else Directory.CreateDirectory(target);
                if (asset.Meta != null) File.WriteAllBytes(target + ".meta", asset.Meta);
                count++;
            }
            if (count == 0) throw new BuildFailedException("The TMP package has no asset records: " + string.Join(", ", entryNames));
            Debug.Log("HORIZON typography resources: " + count + "; project assets: " + Application.dataPath +
                "; settings file exists: " + File.Exists(System.IO.Path.Combine(root, "Resources/TMP Settings.asset")));
        }

        private static bool ReadBlock(Stream stream, byte[] data, bool allowEnd)
        {
            int count = 0;
            while (count < data.Length)
            {
                int read = stream.Read(data, count, data.Length - count);
                if (read == 0)
                {
                    if (allowEnd && count == 0) return false;
                    throw new BuildFailedException("The TMP essential package is truncated.");
                }
                count += read;
            }
            return true;
        }
    }
}
