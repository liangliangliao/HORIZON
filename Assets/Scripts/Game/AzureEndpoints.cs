using System;
using System.Linq;
using UnityEngine;

namespace Horizon.Game
{
    public sealed class AzureEndpoint
    {
        public readonly string Root;
        public readonly AzureAccessMode[] Modes;
        public readonly bool Serverless;
        public readonly string Deployment;
        internal AzureEndpoint(string root, AzureAccessMode[] modes, bool serverless, string deployment = "")
        { Root = root; Modes = modes; Serverless = serverless; Deployment = deployment; }
        public string ChatUrl(AzureAccessMode mode, string deployment, string version)
        {
            if (mode == AzureAccessMode.Responses) return Root + "/openai/v1/responses";
            if (mode == AzureAccessMode.OpenAIV1) return Root + "/openai/v1/chat/completions";
            string apiVersion = AzureEndpoints.Version(mode, version);
            string path = mode == AzureAccessMode.AzureOpenAI ? "/openai/deployments/" + Uri.EscapeDataString(deployment) + "/chat/completions" :
                (Serverless ? "" : "/models") + "/chat/completions";
            return Root + path + "?api-version=" + Uri.EscapeDataString(apiVersion);
        }
    }

    // All fallback addresses retain the configured origin. No key-bearing cross-host redirects.
    public static class AzureEndpoints
    {
        public const int MaximumDeployments = 6;
        [Serializable] private sealed class Model { public string id; }
        [Serializable] private sealed class Models { public Model[] data; }
        [Serializable] private sealed class Error { public string code; }
        [Serializable] private sealed class ErrorBody { public Error error; }

        public static AzureEndpoint Resolve(AISettings settings)
        {
            string raw = (settings.azureEndpoint ?? "").Trim();
            if (raw.Length > 500 || raw.Any(char.IsControl) || !Uri.TryCreate(raw, UriKind.Absolute, out Uri uri) ||
                uri.Scheme != Uri.UriSchemeHttps || !string.IsNullOrEmpty(uri.UserInfo) || !string.IsNullOrEmpty(uri.Query) ||
                !string.IsNullOrEmpty(uri.Fragment) || !Enum.IsDefined(typeof(AzureAccessMode), settings.azureAccessMode))
                throw new AIException(AIError.Configuration);
            string path = uri.AbsolutePath.TrimEnd('/');
            string[] parts = path.Split(new[] { '/' }, StringSplitOptions.RemoveEmptyEntries);
            bool project = parts.Length == 3 && parts[0] == "api" && parts[1] == "projects" && parts[2].Length > 0;
            bool responses = path == "/openai/v1/responses";
            bool v1 = path == "/openai/v1" || path == "/openai/v1/chat/completions" || responses;
            bool models = path == "/models" || path == "/models/chat/completions";
            bool deploymentPath = parts.Length == 5 && parts[0] == "openai" && parts[1] == "deployments" &&
                parts[2].Length > 0 && parts[3] == "chat" && parts[4] == "completions";
            if (path != "" && path != "/openai" && !project && !v1 && !models && !deploymentPath) throw new AIException(AIError.Configuration);
            bool serverless = uri.Host.EndsWith(".models.ai.azure.com", StringComparison.OrdinalIgnoreCase);
            AzureAccessMode preferred = settings.azureAccessMode;
            // A pasted operation URL is stronger evidence than a stale selector.
            if (responses) preferred = AzureAccessMode.Responses;
            else if (deploymentPath) preferred = AzureAccessMode.AzureOpenAI;
            if (preferred == AzureAccessMode.Auto)
                preferred = serverless || models ? AzureAccessMode.FoundryModels :
                    project || v1 || uri.Host.EndsWith(".services.ai.azure.com", StringComparison.OrdinalIgnoreCase) ? AzureAccessMode.OpenAIV1 : AzureAccessMode.AzureOpenAI;
            AzureAccessMode[] modes = preferred == AzureAccessMode.Responses ?
                new[] { AzureAccessMode.Responses, AzureAccessMode.OpenAIV1, AzureAccessMode.FoundryModels, AzureAccessMode.AzureOpenAI } : preferred == AzureAccessMode.AzureOpenAI ?
                new[] { AzureAccessMode.AzureOpenAI, AzureAccessMode.OpenAIV1, AzureAccessMode.FoundryModels } :
                preferred == AzureAccessMode.FoundryModels ? new[] { AzureAccessMode.FoundryModels, AzureAccessMode.OpenAIV1, AzureAccessMode.AzureOpenAI } :
                new[] { AzureAccessMode.OpenAIV1, AzureAccessMode.FoundryModels, AzureAccessMode.AzureOpenAI };
            return new AzureEndpoint(uri.GetLeftPart(UriPartial.Authority), modes, serverless,
                deploymentPath ? Uri.UnescapeDataString(parts[2]) : "");
        }
        public static string Version(AzureAccessMode mode, string raw)
        {
            if (mode != AzureAccessMode.AzureOpenAI && mode != AzureAccessMode.FoundryModels) throw new AIException(AIError.Configuration);
            if (string.IsNullOrWhiteSpace(raw)) return mode == AzureAccessMode.AzureOpenAI ? "2024-10-21" : "2024-05-01-preview";
            if (raw.Length > 50 || raw.Any(char.IsControl)) throw new AIException(AIError.Configuration);
            return raw.Trim();
        }
        public static string[] Deployments(AISettings settings)
        {
            string raw = settings.azureDeployment ?? "";
            if (raw.Length > 600 || raw.Any(char.IsControl)) throw new AIException(AIError.Configuration);
            string[] values = raw.Split(new[] { ',', '，' }, StringSplitOptions.RemoveEmptyEntries).Select(x => x.Trim()).Where(x => x.Length > 0).Distinct().ToArray();
            if (values.Length == 0 && !string.IsNullOrWhiteSpace(settings.azureEndpoint))
            { string inferred = Resolve(settings).Deployment; if (!string.IsNullOrEmpty(inferred)) values = new[] { inferred }; }
            if (values.Length > MaximumDeployments || values.Any(x => x.Length > 100)) throw new AIException(AIError.Configuration);
            return Prioritize(values, settings.azureSelectedDeployment);
        }
        public static string[] Prioritize(string[] values, string selected)
        { return values.OrderBy(x => x == selected ? 0 : 1).ToArray(); }
        public static string[] ReadModels(AIResponse response)
        {
            if (response.Status == 404 || response.Status == 405) throw new AIException(AIError.Deployment);
            AIProtocol.CheckStatus(response);
            try
            {
                Models models = JsonUtility.FromJson<Models>(response.Body);
                string[] names = (models?.data ?? Array.Empty<Model>()).Where(x => !string.IsNullOrWhiteSpace(x?.id)).Select(x => x.id.Trim()).Distinct().ToArray();
                if (names.Any(x => x.Length > 100 || x.Any(char.IsControl))) throw new AIException(AIError.InvalidContent);
                if (names.Length == 0) throw new AIException(AIError.Deployment);
                return names.Take(MaximumDeployments).ToArray();
            }
            catch (AIException) { throw; }
            catch (Exception) { throw new AIException(AIError.InvalidContent); }
        }
        public static bool PathOrDeploymentMissing(AIResponse response)
        {
            if (response.Status == 404 || response.Status == 405) return true;
            if (response.Status != 400 || response.Body.Length > 65536) return false;
            try
            {
                string code = JsonUtility.FromJson<ErrorBody>(response.Body)?.error?.code ?? "";
                return code.Equals("DeploymentNotFound", StringComparison.OrdinalIgnoreCase) ||
                    code.Equals("ModelNotFound", StringComparison.OrdinalIgnoreCase) || code.Equals("model_not_found", StringComparison.OrdinalIgnoreCase);
            }
            catch (Exception) { return false; }
        }
        public static string ModeLabel(AzureAccessMode mode)
        {
            switch (mode)
            {
                case AzureAccessMode.AzureOpenAI: return "Azure OpenAI · 部署接口";
                case AzureAccessMode.FoundryModels: return "Foundry Models · 推理接口";
                case AzureAccessMode.OpenAIV1: return "Azure OpenAI · v1 接口";
                case AzureAccessMode.Responses: return "Azure / Foundry · Responses 接口";
                default: return "自动识别（按终结点判断）";
            }
        }
        public static string ModelLabel(AISettings settings, string deployment)
        {
            string resource = AIText.Bound(settings.azureResourceName, 100);
            if (resource.Length == 0) resource = Uri.TryCreate(settings.azureEndpoint, UriKind.Absolute, out Uri uri) ? uri.Host.Split('.')[0] : "Azure";
            return (resource + " / " + deployment).Replace("<", "‹").Replace(">", "›");
        }
    }
}
