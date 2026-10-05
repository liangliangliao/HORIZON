using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
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
                UnpackTypography(essentials);
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
            using (var file = File.OpenRead(packagePath))
            using (var archive = new GZipStream(file, CompressionMode.Decompress))
            {
                var header = new byte[512];
                while (ReadBlock(archive, header, true))
                {
                    if (header.All(b => b == 0)) break;
                    string name = Encoding.UTF8.GetString(header, 0, 100).TrimEnd('\0');
                    string sizeText = Encoding.ASCII.GetString(header, 124, 12).Trim('\0', ' ');
                    long size = string.IsNullOrEmpty(sizeText) ? 0 : Convert.ToInt64(sizeText, 8);
                    if (size < 0 || size > 64 * 1024 * 1024)
                        throw new BuildFailedException("Invalid TMP package entry size.");
                    var data = new byte[(int)size]; ReadBlock(archive, data, false);
                    var padding = new byte[(int)((512 - size % 512) % 512)]; ReadBlock(archive, padding, false);
                    string[] parts = name.Split('/');
                    if (parts.Length != 2 || parts[0].Length != 32) continue;
                    if (!assets.TryGetValue(parts[0], out PackageAsset asset))
                        assets.Add(parts[0], asset = new PackageAsset());
                    if (parts[1] == "pathname") asset.Path = Encoding.UTF8.GetString(data).TrimEnd('\0', '\r', '\n');
                    else if (parts[1] == "asset") asset.Asset = data;
                    else if (parts[1] == "asset.meta") asset.Meta = data;
                }
            }
            string root = System.IO.Path.GetFullPath("Assets/TextMesh Pro");
            foreach (PackageAsset asset in assets.Values)
            {
                if (string.IsNullOrEmpty(asset.Path)) continue;
                string target = System.IO.Path.GetFullPath(asset.Path);
                if (target != root && !target.StartsWith(root + System.IO.Path.DirectorySeparatorChar, StringComparison.Ordinal))
                    throw new BuildFailedException("TMP package contains an unexpected asset path.");
                Directory.CreateDirectory(System.IO.Path.GetDirectoryName(target));
                if (asset.Asset != null) File.WriteAllBytes(target, asset.Asset);
                else Directory.CreateDirectory(target);
                if (asset.Meta != null) File.WriteAllBytes(target + ".meta", asset.Meta);
            }
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
