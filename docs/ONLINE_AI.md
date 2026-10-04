# DeepSeek / Azure / Microsoft Foundry

v0.4.2 支持离线、DeepSeek、Azure OpenAI 三种内容来源。设置入口为「设置 → 在线 AI」，使用入口包括未来自己、近期模式、思维怪物、知识熔炉和想象设置。六种用途分别生成未来对话、Personal Quest、Pattern 解释、预演情境、知识转行动或 NPC 对话。它不能改变资源、概率、Boss 结果、奖励或自动完成现实行动。

## 直接接入

选择服务商，填写配置与自己的 API Key，点击「测试连接」，成功后「保存配置」。测试仅发送虚拟目标；游戏内容只有在主动点击「与未来自己对话」后发送。默认超时 25 秒，失败显示原因并使用本地内容。

| 字段 | DeepSeek | Azure OpenAI |
| --- | --- | --- |
| 资源 / 项目名称 | 无 | 可填写资源标签；模型列表显示为 `资源名 / 部署名`，留空使用终结点中的资源名 |
| Endpoint | 默认 `https://api.deepseek.com`，也可使用 `/v1` 基础地址 | Azure 资源根地址、Foundry 项目地址（含 `/api/projects/项目名`）、`/openai/v1`、`/models`、完整 `/openai/v1/responses` 或完整部署对话地址 |
| Model / Deployment | 默认 `deepseek-chat`，可填写帐户支持的模型 | Azure 中实际创建的 **部署名称**；可留空或用英文 / 中文逗号分隔最多 6 个名称 |
| 接入方式 | 无 | 自动识别、Azure OpenAI 部署接口、Foundry Models 推理接口、OpenAI v1、Responses；选择决定尝试顺序，但完整操作地址优先于旧的选择 |
| API Version | 无 | 可留空；按实际接口使用默认值，v1 无需日期版本 |
| API Key | DeepSeek 密钥，Bearer 认证 | Azure 资源密钥，`api-key` 认证 |

Azure 编辑页对应参考图中的资源名称、终结点、会话密钥、接入方式、可选版本和部署列表。接受例如 `https://your-resource.services.ai.azure.com/api/projects/your-project`；调用前将其归一化到 **同一域名和端口** 的资源根地址，API Key 不会被发送到其他资源或重定向目标。

| 接口 | 调用地址 | 留空时的默认版本 | 模型字段 |
| --- | --- | --- | --- |
| Azure OpenAI 部署接口 | `/openai/deployments/{deployment}/chat/completions` | `2024-10-21` | 部署名在 URL 中 |
| Azure OpenAI v1 | `/openai/v1/chat/completions` | 不需要日期版本 | `model` 为部署名 |
| Azure Responses | `/openai/v1/responses` | 不需要日期版本 | `model` 为部署名 |
| Foundry Models | `/models/chat/completions` | `2024-05-01-preview` | `model` 为部署名 |

自动识别优先：Azure OpenAI 根地址使用部署接口，Foundry 项目 / `services.ai.azure.com` 使用 v1，`/models` 使用 Foundry Models。接入方式只调整尝试顺序；遇到 404 / 405 路径缺失，或明确的 `DeploymentNotFound` / `ModelNotFound` 错误，才尝试另一接口或已填写的下一部署。认证失败、限流、其他请求错误、网络错误、超时和无效内容均不触发额外尝试。整个操作共用一次超时，退出页面会取消所有后续请求。

部署名留空时，先通过同源 `GET /openai/v1/models` 读取服务返回的模型标识，最多尝试六个；不会猜测部署名，也不会使用旧 `/openai/models` 的基础模型目录。资源未开放列表、返回空列表或所列模型不支持对话时，需要填写实际部署名。填写多个部署后，可在未来自己对话页点击模型，按 `资源名 / 部署名` 选择当前部署；只有路径或部署不存在时才切换候选。

Chat Completions 路径要求模型支持对话与 JSON 输出；Responses 路径使用 `instructions`、`input` 和 `text.format`，仅接受完成状态的一段 `output_text`，拒绝未完成、拒绝和多段歧义响应。完整部署对话地址会提取实际部署名；完整 Responses 地址即使曾选择 Foundry Models 也优先调用 Responses。Anthropic Messages、Agents 和 Entra ID 认证不在当前适配范围内。

配置会保存服务商、连接方式、资源标签、接入偏好、地址、部署列表、当前部署和版本。**密钥仅在本次应用会话的内存中使用**，输入框遮蔽，不写入 PlayerPrefs、人生存档、备份、日志、源代码或 APK。重启应用后需重新输入。关闭页面或进入后台会取消正在进行的请求；退出时清除会话凭据。

发送内容仅包含当前目标（最多 300 字）、近期模式（最多 400 字）和最近六条因果摘要（每条最多 180 字），不上传完整人生、现实星座、存档或个人文本统计。AI 建议需要玩家主动接受，仍受每天一个 Reality Quest 的规则限制；建议本身不作为 Reality Convergence 的模拟证据。

## 服务端代理

仓库提供零依赖 Node 服务 `services/ai-gateway`，适合自有私有代理。服务商密钥留在服务端环境变量，Unity 使用代理地址和单独的访问令牌。正式多人产品需要在网关前接入用户认证与每用户限额；当前令牌对应一个私有实例，默认全局每分钟 20 次、最多两个并发请求。

```bash
cd services/ai-gateway
cp .env.example .env
# 在本机安全编辑 .env，填写实际配置。不要把密钥提交到 Git。
node --env-file=.env server.mjs
```

Node 版本要求 20+。配置：

| 环境变量 | 用途 |
| --- | --- |
| `HORIZON_GATEWAY_TOKEN` | 至少 24 字符的私有访问令牌，必须替换示例占位符 |
| `DEEPSEEK_API_KEY` / `DEEPSEEK_MODEL` / `DEEPSEEK_ENDPOINT` | DeepSeek 服务端配置 |
| `AZURE_OPENAI_API_KEY` / `AZURE_OPENAI_ENDPOINT` | Azure 资源密钥与基础地址 |
| `AZURE_OPENAI_DEPLOYMENT` / `AZURE_OPENAI_API_VERSION` | 可选部署列表（逗号分隔，最多 6 个）和可选 API 版本 |
| `AZURE_ACCESS_MODE` | `auto`（默认）、`openai`、`foundry`、`v1` 或 `responses`，影响尝试顺序 |
| `HOST` / `PORT` | 默认 `127.0.0.1:8787`，放在 HTTPS 反向代理之后 |

Unity 配置页切换「服务端代理」，选择 DeepSeek 或 Azure，填写代理 HTTPS 基础地址（或完整 `/v1/personalize` 地址）与会话访问令牌，再测试和保存。模型、资源地址与部署由服务端控制；请求不能传入任意上游地址。

接口为 `POST /v1/personalize`，认证使用 `Authorization: Bearer <访问令牌>`。请求：

```json
{
  "provider": "azure",
  "context": {
    "goal": "准备一次面试",
    "purpose": "Imagination",
    "recentPattern": "近期在开始前反复比较",
    "evidence": ["D2 · 深度学习", "D4 · 请求帮助"]
  }
}
```

成功仅返回 `futureSelfLine`、`quest`、`patternExplanation` 三个字符串。上游错误详情不返回客户端，不记录玩家内容，不允许携带凭据的重定向。网关校验输入和输出尺寸，限制请求频率并取消超时上游。`GET /healthz` 仅返回健康状态。

## 验证

```bash
dotnet run --project tools/RulesHarness.csproj -- --workers=0
npm --prefix services/ai-gateway test
```

`OnlineAITests` 验证两个服务商的请求契约、Foundry 地址归一化、API 默认值、空部署读取、多个部署选择、限定错误重试、密钥与备份隔离、取消、失败回退、规则不可变和 Unity 空 Decision 占位符兼容。代理有 25 项模拟上游测试，包含截图所示 Responses 地址与错误接入选项的兼容案例。Unity 交互测试生成 `45-ai-deepseek.png`、`46-ai-azure.png`、`47-ai-future-self.png`、`48-ai-foundry-resource.png` 和 `49-ai-azure-deployments.png`，使用虚拟密钥与模拟响应。

本次环境没有 DeepSeek/Azure 实际凭据，尚未进行付费模型连通测试，也没有部署公网代理。接入代码和连接测试可在配置凭据后使用。

接口参考：[DeepSeek Chat Completions](https://api-docs.deepseek.com/api/create-chat-completion)、[Azure v1 API 与模型支持](https://learn.microsoft.com/en-us/azure/ai-foundry/openai/api-version-lifecycle?view=foundry-classic)、[Foundry 项目终结点](https://learn.microsoft.com/en-us/azure/ai-foundry/how-to/develop/sdk-overview?view=foundry-classic)、[Foundry Models REST 契约](https://github.com/Azure/azure-rest-api-specs/blob/main/specification/ai/data-plane/ModelInference/preview/2024-05-01-preview/openapi.json)。
