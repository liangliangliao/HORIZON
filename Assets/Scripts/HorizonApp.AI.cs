using System;
using System.Linq;
using System.Threading;
using Horizon.Game;
using Horizon.UI;
using UnityEngine;
using UnityEngine.UI;

namespace Horizon
{
    public sealed partial class HorizonApp
    {
        private readonly AISessionSecrets aiSecrets = new AISessionSecrets();
        private IAITransport aiTransport;
        private AISettings aiDraft;
        private CancellationTokenSource aiRequest;
        private bool aiSettingsReturnToFuture;
        private string aiGoal = "开始一个两分钟的小步骤";
        private void CancelAIRequest()
        { if (aiRequest != null) { aiRequest.Cancel(); aiRequest = null; } }
        private IAIAdapter CreateContentAdapter(AISettings settings, bool fallback)
        {
            if (settings.provider == AIProvider.Local) return new LocalContentAdapter();
            if (aiTransport == null) aiTransport = new UnityAITransport();
            IAIAdapter online = new OnlineContentAdapter(settings, aiSecrets.Get(settings.provider, settings.connection), aiTransport);
            return fallback ? new ResilientContentAdapter(online) : online;
        }
        private void ShowAISettings()
        { aiDraft = archive.ai.Copy(); SetPaused(true); RenderAISettings(); }
        private InputField AIField(string name, string caption, string initial, Action<string> changed, float y, bool secret = false, bool compact = false, string placeholder = "")
        {
            View.Label(overlay, name + " caption", caption, compact ? 22 : 24, Palette.Muted, TextAnchor.MiddleLeft,
                0.075f, y + (compact ? 0.042f : 0.06f), 0.925f, y + (compact ? 0.069f : 0.097f));
            RectTransform rect = View.Rect(overlay, name, 0.075f, y, 0.925f, y + (compact ? 0.04f : 0.059f));
            Image image = rect.gameObject.AddComponent<Image>(); image.color = Palette.Panel;
            Text label = View.Label(rect, name + " text", "", 25, Palette.Text, TextAnchor.MiddleLeft, 0.025f, 0.06f, 0.975f, 0.94f);
            label.supportRichText = false;
            InputField input = rect.gameObject.AddComponent<InputField>(); input.targetGraphic = image; input.textComponent = label;
            input.characterLimit = secret ? 2048 : 500; input.lineType = InputField.LineType.SingleLine;
            if (secret) input.contentType = InputField.ContentType.Password;
            if (placeholder.Length > 0)
            {
                Text hint = View.Label(rect, name + " placeholder", placeholder, 22, Palette.Muted, TextAnchor.MiddleLeft, 0.025f, 0.06f, 0.975f, 0.94f);
                hint.supportRichText = false; input.placeholder = hint;
            }
            input.text = initial ?? ""; input.onValueChanged.AddListener(value => changed(value)); return input;
        }
        private void RenderAISettings()
        {
            CancelAIRequest();
            bool azure = aiDraft.provider == AIProvider.AzureOpenAI && aiDraft.connection == AIConnection.Direct;
            SettingsPanel(azure ? "编辑 Azure 资源" : "在线 AI · 未来自己", azure ? "支持 Foundry 项目 / Azure OpenAI 终结点。" : "选择内容服务商，或继续使用离线内容。");
            Text notice = null;
            string[] names = { "离线", "DeepSeek", "Azure / Foundry" };
            for (int i = 0; i < 3; i++)
            {
                AIProvider provider = (AIProvider)i; float x = 0.075f + i * 0.29f;
                View.Button(overlay, "AI provider " + i, names[i], () => { aiDraft.provider = provider; RenderAISettings(); },
                    x, 0.735f, x + 0.27f, 0.79f, aiDraft.provider == provider ? Palette.Mint : Palette.Panel,
                    aiDraft.provider == provider ? Palette.Ink : Palette.Text, 25);
            }
            if (aiDraft.provider == AIProvider.Local)
                View.Label(overlay, "Offline AI explanation", "未来自己的话、现实小动作和近期模式解释\n由本地内容生成，随时可以离线游玩。", 32,
                    Palette.Text, TextAnchor.MiddleCenter, 0.09f, 0.43f, 0.91f, 0.64f);
            else
            {
                View.Button(overlay, "AI connection mode", aiDraft.connection == AIConnection.Direct ? "连接方式：直接接入 · 切换为服务端代理" : "连接方式：服务端代理 · 切换为直接接入", () =>
                { aiDraft.connection = aiDraft.connection == AIConnection.Direct ? AIConnection.Gateway : AIConnection.Direct; RenderAISettings(); },
                    0.075f, 0.667f, 0.925f, 0.716f, Palette.Deep, Palette.Gold, 22);
                if (aiDraft.connection == AIConnection.Gateway)
                {
                    AIField("AI gateway endpoint", "代理 HTTPS 地址", aiDraft.gatewayEndpoint, x => aiDraft.gatewayEndpoint = x, 0.552f);
                    AIField("AI gateway token", "代理访问令牌 · 仅本次会话", aiSecrets.Get(aiDraft.provider, aiDraft.connection),
                        x => aiSecrets.Set(aiDraft.provider, AIConnection.Gateway, x), 0.438f, true);
                    View.Label(overlay, "AI gateway guidance", "DeepSeek / Azure 的密钥、模型与部署\n由你的代理服务配置。客户端只保存代理地址。", 26,
                        Palette.Muted, TextAnchor.MiddleLeft, 0.075f, 0.31f, 0.925f, 0.412f);
                }
                else if (aiDraft.provider == AIProvider.DeepSeek)
                {
                    AIField("AI DeepSeek endpoint", "Endpoint · HTTPS 基础地址", aiDraft.deepSeekEndpoint, x => aiDraft.deepSeekEndpoint = x, 0.552f);
                    AIField("AI DeepSeek model", "Model · 如 deepseek-chat", aiDraft.deepSeekModel, x => aiDraft.deepSeekModel = x, 0.438f);
                    AIField("AI provider key", "API Key · 仅本次会话", aiSecrets.Get(aiDraft.provider, aiDraft.connection),
                        x => aiSecrets.Set(AIProvider.DeepSeek, AIConnection.Direct, x), 0.324f, true);
                }
                else
                {
                    AIField("AI Azure resource name", "资源 / 项目名称", aiDraft.azureResourceName, x => aiDraft.azureResourceName = x, 0.593f,
                        compact: true, placeholder: "模型列表显示为：资源名 / 部署名").characterLimit = 100;
                    AIField("AI Azure endpoint", "Foundry 项目终结点 / Azure OpenAI 终结点", aiDraft.azureEndpoint, x => aiDraft.azureEndpoint = x, 0.515f,
                        compact: true, placeholder: "https://资源名.services.ai.azure.com/api/projects/项目名");
                    AIField("AI provider key", "API 密钥 · 仅本次会话", aiSecrets.Get(aiDraft.provider, aiDraft.connection),
                        x => aiSecrets.Set(AIProvider.AzureOpenAI, AIConnection.Direct, x), 0.437f, true, true);
                    View.Button(overlay, "AI Azure access mode", "接入方式：" + AzureEndpoints.ModeLabel(aiDraft.azureAccessMode) + " · 切换", () =>
                    { aiDraft.azureAccessMode = (AzureAccessMode)(((int)aiDraft.azureAccessMode + 1) % 4); RenderAISettings(); },
                        0.075f, 0.375f, 0.925f, 0.42f, Palette.Deep, Palette.Gold, 23);
                    AIField("AI Azure api version", "api-version（可留空；v1 接口无需版本）", aiDraft.azureApiVersion, x => aiDraft.azureApiVersion = x, 0.295f,
                        compact: true, placeholder: "默认 OpenAI 2024-10-21 · Foundry 2024-05-01-preview").characterLimit = 50;
                    AIField("AI Azure deployment", "部署名（可留空，用逗号分隔，最多 6 个）", aiDraft.azureDeployment,
                        x => { aiDraft.azureDeployment = x; aiDraft.azureSelectedDeployment = ""; }, 0.217f,
                        compact: true, placeholder: "留空读取模型列表；例如 coach, deepseek-chat").characterLimit = 600;
                }
                notice = View.Label(overlay, "AI connection status", azure ? "路径 / 部署不存在时尝试其他接口；版本可留空。\n仅主动生成时发送目标；密钥不进入存档。" :
                    "只有点击生成才发送目标和最多 6 条近期因果。\n密钥不进入存档和备份。连接测试使用虚拟内容。", 21,
                    Palette.Muted, TextAnchor.MiddleLeft, 0.075f, 0.166f, 0.925f, azure ? 0.212f : 0.236f);
                Text status = notice;
                Button test = View.Button(overlay, "Test AI connection", "测试连接", () => { },
                    0.075f, 0.104f, 0.925f, 0.16f, Palette.Panel, Palette.Gold, 27);
                test.onClick.AddListener(() => TestAIConnection(status, test));
            }
            View.Button(overlay, "Save AI settings", "保存配置", () =>
            {
                if (azure)
                {
                    try { AzureEndpoints.Resolve(aiDraft); AzureEndpoints.Deployments(aiDraft); }
                    catch (AIException e) { if (notice != null) { notice.text = e.Message; notice.color = Palette.Coral; } return; }
                }
                aiDraft.Repair(); archive.ai = aiDraft.Copy(); Save(); LeaveAISettings();
            },
                0.075f, 0.035f, 0.49f, 0.095f, Palette.Mint, Palette.Ink, 27);
            View.Button(overlay, "Back from AI settings", "返回", LeaveAISettings, 0.51f, 0.035f, 0.925f, 0.095f, Palette.Panel, Palette.Text, 27);
            View.RefreshText(overlay);
        }
        private void LeaveAISettings()
        {
            CancelAIRequest(); aiDraft = null;
            if (aiSettingsReturnToFuture) { aiSettingsReturnToFuture = false; CloseSettings(); ShowFutureSelfDialogue(); }
            else RenderSettings();
        }
        private async void TestAIConnection(Text notice, Button button)
        {
            CancelAIRequest(); var request = new CancellationTokenSource(); aiRequest = request;
            button.interactable = false; notice.text = "正在连接…";
            try
            {
                PersonalContent result = await CreateContentAdapter(aiDraft, false).Personalize(new NarrativeContext("连接测试", "", Array.Empty<string>()), request.Token);
                request.Token.ThrowIfCancellationRequested();
                if (notice != null) { notice.text = "连接成功 · " + result.source; notice.color = Palette.Mint; }
            }
            catch (OperationCanceledException) { if (notice != null) notice.text = "请求已取消。"; }
            catch (Exception e) { if (notice != null) { notice.text = e is AIException ai ? ai.Message : AIException.Describe(AIError.Network); notice.color = Palette.Coral; } }
            finally { if (button != null) button.interactable = true; if (aiRequest == request) aiRequest = null; request.Dispose(); }
        }
        private NarrativeContext PersonalContext()
        {
            if (session != null) return NarrativeContext.From(session, aiGoal);
            return new NarrativeContext(aiGoal, archive.me.Summary,
                archive.runs.LastOrDefault()?.actions.Select(a => "D" + a.day + " · " + a.cardName) ?? Array.Empty<string>());
        }
        private void ShowFutureSelfDialogue()
        {
            MasterPage("AI future self", "F U T U R E  S E L F", ShowMasterHub);
            View.Label(overlay, "AI content introduction", "未来自己观察你的来路，提出一个问题。\n你仍然亲自选择下一步。", 30, Palette.Text,
                TextAnchor.UpperLeft, 0.075f, 0.7f, 0.925f, 0.84f);
            MasterInput("AI personal goal", aiGoal, x => aiGoal = x, 0.545f, 300).textComponent.supportRichText = false;
            AISettings settings = archive.ai;
            string[] azureDeployments = Array.Empty<string>();
            if (settings.provider == AIProvider.AzureOpenAI && settings.connection == AIConnection.Direct)
            { try { azureDeployments = AzureEndpoints.Deployments(settings); } catch (AIException) { } }
            if (azureDeployments.Length > 0)
                View.Button(overlay, "Choose Azure deployment", "模型：" + AzureEndpoints.ModelLabel(settings,
                    azureDeployments[0]) + " · 选择", ShowAzureDeploymentPicker,
                    0.075f, 0.45f, 0.925f, 0.525f, Palette.Panel, Palette.Gold, 24);
            else View.Label(overlay, "AI active provider", settings.provider == AIProvider.Local ? "本地内容 · 离线可用" :
                (settings.provider == AIProvider.DeepSeek ? "DeepSeek" : "Azure OpenAI") + " · 点击后发送目标与最多 6 条近期因果", 24, Palette.Muted,
                TextAnchor.MiddleLeft, 0.075f, 0.45f, 0.925f, 0.525f);
            Text status = View.Label(overlay, "AI generation status", settings.provider == AIProvider.Local ? "离线生成，不发送数据" : "生成时发送目标与最多 6 条近期因果", 24,
                Palette.Muted, TextAnchor.MiddleLeft, 0.075f, 0.385f, 0.925f, 0.445f);
            Button generate = View.Button(overlay, "Generate personal content", "与未来自己对话", () => { },
                0.075f, 0.285f, 0.925f, 0.37f, Palette.Mint, Palette.Ink, 29);
            generate.onClick.AddListener(() => GeneratePersonalContent(status, generate));
            View.Button(overlay, "Configure online AI", "配置 DeepSeek / Azure", () => { aiSettingsReturnToFuture = true; ShowAISettings(); },
                0.075f, 0.175f, 0.925f, 0.25f, Palette.Panel, Palette.Text, 27);
        }
        private void ShowAzureDeploymentPicker()
        {
            MasterPage("Azure deployment picker", "选择 Azure 对话部署", ShowFutureSelfDialogue);
            string[] deployments;
            try { deployments = AzureEndpoints.Deployments(archive.ai); }
            catch (AIException) { deployments = Array.Empty<string>(); }
            View.Label(overlay, "Azure model guidance", "模型名称以资源 / 部署区分。\n选择只影响之后的 AI 对话。", 28, Palette.Muted, TextAnchor.MiddleLeft, 0.075f, 0.78f, 0.925f, 0.875f);
            for (int i = 0; i < deployments.Length; i++)
            {
                string deployment = deployments[i]; float y = 0.67f - i * 0.085f;
                Button choice = View.Button(overlay, "Azure deployment choice " + i, AzureEndpoints.ModelLabel(archive.ai, deployment), () =>
                { archive.ai.azureSelectedDeployment = deployment; Save(); ShowFutureSelfDialogue(); },
                    0.075f, y, 0.925f, y + 0.07f, Palette.Panel, Palette.Gold, 27);
                choice.GetComponentInChildren<Text>().supportRichText = false;
            }
            if (deployments.Length == 0)
                View.Label(overlay, "Azure no models", "请在配置中填写部署名，\n留空时会尝试资源的模型列表。", 30, Palette.Text, TextAnchor.MiddleCenter, 0.1f, 0.4f, 0.9f, 0.6f);
            View.RefreshText(overlay);
        }
        private async void GeneratePersonalContent(Text status, Button button)
        {
            CancelAIRequest(); var request = new CancellationTokenSource(); aiRequest = request;
            button.interactable = false; status.text = "未来自己正在回望…";
            try
            {
                PersonalContent content = await CreateContentAdapter(archive.ai, true).Personalize(PersonalContext(), request.Token);
                request.Token.ThrowIfCancellationRequested(); ShowPersonalContent(content);
            }
            catch (OperationCanceledException) { if (status != null) status.text = "请求已取消。"; }
            catch (Exception) { if (status != null) status.text = "暂时没有收到内容，请再试一次。"; }
            finally { if (button != null) button.interactable = true; if (aiRequest == request) aiRequest = null; request.Dispose(); }
        }
        private void ShowPersonalContent(PersonalContent content)
        {
            MasterPage("Personal future self content", "未来自己 · " + content.source, ShowFutureSelfDialogue);
            RectTransform viewport = View.Rect(overlay, "Personal content viewport", 0.075f, 0.315f, 0.925f, 0.82f);
            viewport.gameObject.AddComponent<Image>().color = Palette.Panel;
            viewport.gameObject.AddComponent<Mask>().showMaskGraphic = true;
            Text text = View.Label(viewport, "Personal content text", content.futureSelfLine + "\n\n近期模式\n" + content.patternExplanation +
                "\n\n一个现实小动作\n" + content.quest, 30, Palette.Text, TextAnchor.UpperLeft, 0.04f, 1, 0.96f, 1);
            text.supportRichText = false; text.rectTransform.pivot = new Vector2(0.5f, 1);
            text.resizeTextForBestFit = false; text.verticalOverflow = VerticalWrapMode.Overflow;
            text.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            ScrollRect scroll = viewport.gameObject.AddComponent<ScrollRect>(); scroll.viewport = viewport; scroll.content = text.rectTransform;
            scroll.horizontal = false; scroll.vertical = true; scroll.movementType = ScrollRect.MovementType.Clamped;
            View.Label(overlay, "Personal content source", content.status, 23, content.generatedByAI ? Palette.Mint : Palette.Muted,
                TextAnchor.MiddleLeft, 0.075f, 0.24f, 0.925f, 0.305f).supportRichText = false;
            bool available = !archive.reality.quests.Any(q => q.localDate == DateTime.Now.ToString("yyyy-MM-dd"));
            View.Button(overlay, "Accept AI reality suggestion", available ? "选择为今天的现实行动" : "今天已有现实行动 · 查看", () =>
            {
                if (available) { archive.reality.Offer(DateTime.Now, "personal:" + AIText.Bound(aiGoal, 100).ToLowerInvariant(), content.quest, null); Save(); }
                ShowReality();
            }, 0.075f, 0.13f, 0.925f, 0.215f, Palette.Panel, Palette.Gold, 27);
        }
    }
}
