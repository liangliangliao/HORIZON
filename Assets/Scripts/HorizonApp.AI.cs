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
        private InputField AIField(string name, string caption, string initial, Action<string> changed, float y, bool secret = false)
        {
            View.Label(overlay, name + " caption", caption, 24, Palette.Muted, TextAnchor.MiddleLeft,
                0.075f, y + 0.06f, 0.925f, y + 0.097f);
            RectTransform rect = View.Rect(overlay, name, 0.075f, y, 0.925f, y + 0.059f);
            Image image = rect.gameObject.AddComponent<Image>(); image.color = Palette.Panel;
            Text label = View.Label(rect, name + " text", "", 25, Palette.Text, TextAnchor.MiddleLeft, 0.025f, 0.06f, 0.975f, 0.94f);
            label.supportRichText = false;
            InputField input = rect.gameObject.AddComponent<InputField>(); input.targetGraphic = image; input.textComponent = label;
            input.characterLimit = secret ? 2048 : 500; input.lineType = InputField.LineType.SingleLine;
            if (secret) input.contentType = InputField.ContentType.Password;
            input.text = initial ?? ""; input.onValueChanged.AddListener(value => changed(value)); return input;
        }
        private void RenderAISettings()
        {
            SettingsPanel("在线 AI · 未来自己", "选择内容服务商，或继续使用离线内容。");
            string[] names = { "离线", "DeepSeek", "Azure OpenAI" };
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
                    AIField("AI Azure endpoint", "Endpoint · https://资源名.openai.azure.com", aiDraft.azureEndpoint, x => aiDraft.azureEndpoint = x, 0.552f);
                    AIField("AI Azure deployment", "Deployment · Azure 中创建的部署名称", aiDraft.azureDeployment, x => aiDraft.azureDeployment = x, 0.449f);
                    AIField("AI Azure api version", "API Version", aiDraft.azureApiVersion, x => aiDraft.azureApiVersion = x, 0.346f);
                    AIField("AI provider key", "API Key · 仅本次会话", aiSecrets.Get(aiDraft.provider, aiDraft.connection),
                        x => aiSecrets.Set(AIProvider.AzureOpenAI, AIConnection.Direct, x), 0.243f, true);
                }
                Text notice = View.Label(overlay, "AI connection status", "只有点击生成才发送目标和最多 6 条近期因果。\n密钥不进入存档和备份。连接测试使用虚拟内容。", 21,
                    Palette.Muted, TextAnchor.MiddleLeft, 0.075f, 0.166f, 0.925f, 0.236f);
                Button test = View.Button(overlay, "Test AI connection", "测试连接", () => { },
                    0.075f, 0.104f, 0.925f, 0.16f, Palette.Panel, Palette.Gold, 27);
                test.onClick.AddListener(() => TestAIConnection(notice, test));
            }
            View.Button(overlay, "Save AI settings", "保存配置", () => { aiDraft.Repair(); archive.ai = aiDraft.Copy(); Save(); LeaveAISettings(); },
                0.075f, 0.035f, 0.49f, 0.095f, Palette.Mint, Palette.Ink, 27);
            View.Button(overlay, "Back from AI settings", "返回", LeaveAISettings, 0.51f, 0.035f, 0.925f, 0.095f, Palette.Panel, Palette.Text, 27);
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
                await CreateContentAdapter(aiDraft, false).Personalize(new NarrativeContext("连接测试", "", Array.Empty<string>()), request.Token);
                if (notice != null) { notice.text = "连接成功 · 已收到完整内容。"; notice.color = Palette.Mint; }
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
            View.Label(overlay, "AI active provider", settings.provider == AIProvider.Local ? "本地内容 · 离线可用" :
                (settings.provider == AIProvider.DeepSeek ? "DeepSeek" : "Azure OpenAI") + " · 点击后发送目标与最多 6 条近期因果", 24, Palette.Muted,
                TextAnchor.MiddleLeft, 0.075f, 0.45f, 0.925f, 0.525f);
            Text status = View.Label(overlay, "AI generation status", "", 24, Palette.Muted, TextAnchor.MiddleLeft, 0.075f, 0.385f, 0.925f, 0.445f);
            Button generate = View.Button(overlay, "Generate personal content", "与未来自己对话", () => { },
                0.075f, 0.285f, 0.925f, 0.37f, Palette.Mint, Palette.Ink, 29);
            generate.onClick.AddListener(() => GeneratePersonalContent(status, generate));
            View.Button(overlay, "Configure online AI", "配置 DeepSeek / Azure", () => { aiSettingsReturnToFuture = true; ShowAISettings(); },
                0.075f, 0.175f, 0.925f, 0.25f, Palette.Panel, Palette.Text, 27);
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
