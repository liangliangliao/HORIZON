# DeepSeek / Azure OpenAI

v0.4.2 支持离线、DeepSeek、Azure OpenAI 三种内容来源。设置入口为「设置 → 在线 AI」，使用入口为「HORIZON ME → AI · 未来自己」。AI 返回未来自己的一段话、近期模式解释和一个现实小动作。它不能改变资源、概率、Boss 结果、奖励或自动完成现实行动。

## 直接接入

选择服务商，填写配置与自己的 API Key，点击「测试连接」，成功后「保存配置」。测试仅发送虚拟目标；游戏内容只有在主动点击「与未来自己对话」后发送。默认超时 25 秒，失败显示原因并使用本地内容。

| 字段 | DeepSeek | Azure OpenAI |
| --- | --- | --- |
| Endpoint | 默认 `https://api.deepseek.com`，也可使用 `/v1` 基础地址 | Azure 资源基础地址，如 `https://your-resource.openai.azure.com` |
| Model / Deployment | 默认 `deepseek-chat`，可填写帐户支持的模型 | 填写 Azure 中实际创建的 **部署名称** |
| API Version | 无 | 默认 `2024-10-21`，可按部署调整 |
| API Key | DeepSeek 密钥，Bearer 认证 | Azure 资源密钥，`api-key` 认证 |

Azure 使用 `/openai/deployments/{deployment}/chat/completions?api-version={version}`，要求部署支持 Chat Completions 与 JSON 输出。这一接入是 Azure OpenAI 资源接口，尚不包含 Azure AI Foundry 非 OpenAI 模型或 Entra ID 认证。

配置会保存服务商、连接方式、地址、模型/部署和版本。**密钥仅在本次应用会话的内存中使用**，输入框遮蔽，不写入 PlayerPrefs、人生存档、备份、日志、源代码或 APK。重启应用后需重新输入。关闭页面或进入后台会取消正在进行的请求；退出时清除会话凭据。

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
| `AZURE_OPENAI_DEPLOYMENT` / `AZURE_OPENAI_API_VERSION` | 部署名称和 API 版本 |
| `HOST` / `PORT` | 默认 `127.0.0.1:8787`，放在 HTTPS 反向代理之后 |

Unity 配置页切换「服务端代理」，选择 DeepSeek 或 Azure，填写代理 HTTPS 基础地址（或完整 `/v1/personalize` 地址）与会话访问令牌，再测试和保存。模型、资源地址与部署由服务端控制；请求不能传入任意上游地址。

接口为 `POST /v1/personalize`，认证使用 `Authorization: Bearer <访问令牌>`。请求：

```json
{
  "provider": "azure",
  "context": {
    "goal": "准备一次面试",
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

`OnlineAITests` 验证两个服务商的请求契约、密钥与备份隔离、取消、失败回退、规则不可变和 Unity 空 Decision 占位符兼容。网关测试使用内存模拟的上游，没有真实模型调用。Unity 交互测试生成 `45-ai-deepseek.png`、`46-ai-azure.png`、`47-ai-future-self.png`，使用虚拟密钥与模拟响应。

本次环境没有 DeepSeek/Azure 实际凭据，尚未进行付费模型连通测试，也没有部署公网代理。接入代码和连接测试可在配置凭据后使用。

接口参考：[DeepSeek Chat Completions](https://api-docs.deepseek.com/api/create-chat-completion)、[Azure OpenAI REST API](https://learn.microsoft.com/en-us/azure/ai-foundry/openai/reference?view=foundry-classic)。
