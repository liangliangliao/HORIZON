using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Horizon.Game;
using NUnit.Framework;
using UnityEngine;

namespace Horizon.Tests
{
    public sealed class OnlineAITests
    {
        private const string Secret = "test-key-never-persist-this";
        private const string Content = "{\"futureSelfLine\":\"你已经走过一步。\",\"quest\":\"打开一份材料并读两分钟\",\"patternExplanation\":\"近期证据还不足，需要继续观察。\"}";
        private static NarrativeContext Context { get { return new NarrativeContext("面试", "近期犹豫", Enumerable.Range(1, 10).Select(x => "D" + x)); } }
        private static AIResponse Chat(string content = Content, string finish = "stop")
        { return new AIResponse(200, "{\"choices\":[{\"finish_reason\":\"" + finish + "\",\"message\":{\"content\":" + Quote(content) + "}}]}"); }
        private static string Quote(string value) { return "\"" + value.Replace("\\", "\\\\").Replace("\"", "\\\"").Replace("\n", "\\n") + "\""; }
        private sealed class Transport : IAITransport
        {
            public AIRequest Last;
            public AIResponse Response = Chat();
            public bool cancel, fail;
            public Task<AIResponse> Send(AIRequest request, CancellationToken cancellation)
            { Last = request; cancellation.ThrowIfCancellationRequested(); if (cancel) throw new OperationCanceledException(); if (fail) throw new Exception(Secret); return Task.FromResult(Response); }
        }
        [Test]
        public void DeepSeekUsesBearerAndPreservesVersionedBaseWithSixBoundedEvidenceItems()
        {
            var settings = new AISettings { provider = AIProvider.DeepSeek, deepSeekEndpoint = "https://api.deepseek.com/v1/" };
            AIRequest request = AIProtocol.Build(settings, Secret, Context);
            Assert.AreEqual("https://api.deepseek.com/v1/chat/completions", request.Url);
            Assert.AreEqual("Bearer " + Secret, request.Headers["Authorization"]);
            Assert.That(request.Body, Does.Contain("deepseek-chat").And.Contain("json_object"));
            Assert.That(request.Body, Does.Not.Contain(Secret).And.Not.Contain("D4\\\""));
            Assert.AreEqual(6, Context.Evidence.Length); Assert.AreEqual("D5", Context.Evidence[0]);
            Assert.AreEqual(300, new NarrativeContext(new string('x', 1000), "", null).Goal.Length);
        }
        [Test]
        public void AzureUsesResourceDeploymentVersionAndApiKeyWithoutDeepSeekModelField()
        {
            var settings = new AISettings { provider = AIProvider.AzureOpenAI, azureEndpoint = "https://example.openai.azure.com/",
                azureDeployment = "interview coach", azureApiVersion = "2024-10-21" };
            AIRequest request = AIProtocol.Build(settings, Secret, Context);
            Assert.AreEqual("https://example.openai.azure.com/openai/deployments/interview%20coach/chat/completions?api-version=2024-10-21", request.Url);
            Assert.AreEqual(Secret, request.Headers["api-key"]); Assert.IsFalse(request.Headers.ContainsKey("Authorization"));
            Assert.That(request.Body, Does.Contain("max_completion_tokens").And.Not.Contain("\"model\"").And.Not.Contain("temperature"));
        }
        [Test]
        public void GatewaySendsProviderAndContextWithoutUpstreamCredentialsOrEndpoints()
        {
            var settings = new AISettings { provider = AIProvider.AzureOpenAI, connection = AIConnection.Gateway, gatewayEndpoint = "https://horizon.example" };
            AIRequest request = AIProtocol.Build(settings, Secret, Context);
            Assert.AreEqual("https://horizon.example/v1/personalize", request.Url);
            Assert.That(request.Body, Does.Contain("\"provider\":\"azure\"").And.Not.Contain(Secret).And.Not.Contain("azureEndpoint"));
            Assert.IsTrue(AIProtocol.Read(new AIResponse(200, Content), settings.provider, settings.connection).generatedByAI);
        }
        [TestCase("http://api.deepseek.com")]
        [TestCase("https://user:password@api.deepseek.com")]
        [TestCase("https://api.deepseek.com?api-key=hidden")]
        [TestCase("https://api.deepseek.com/#hidden")]
        public void UnsafeCredentialDestinationsAreRejectedBeforeTransport(string endpoint)
        {
            Assert.AreEqual(AIError.Configuration, Assert.Throws<AIException>(() => AIProtocol.Build(
                new AISettings { provider = AIProvider.DeepSeek, deepSeekEndpoint = endpoint }, Secret, Context)).Code);
        }
        [TestCase(401, AIError.Credentials)]
        [TestCase(403, AIError.Credentials)]
        [TestCase(429, AIError.RateLimit)]
        [TestCase(504, AIError.Timeout)]
        [TestCase(503, AIError.Unavailable)]
        [TestCase(0, AIError.Network)]
        public void ProviderErrorsAreCategorizedWithoutRepeatingUpstreamBodies(int status, AIError expected)
        {
            AIException error = Assert.Throws<AIException>(() => AIProtocol.Read(new AIResponse(status, Secret), AIProvider.DeepSeek, AIConnection.Direct));
            Assert.AreEqual(expected, error.Code); Assert.That(error.Message, Does.Not.Contain(Secret));
        }
        [Test]
        public void OnlyCompleteContentIsAcceptedAndFencedJsonIsSupported()
        {
            PersonalContent content = AIProtocol.Read(Chat("```json\n" + Content + "\n```"), AIProvider.DeepSeek, AIConnection.Direct);
            Assert.IsTrue(content.generatedByAI); Assert.AreEqual("DeepSeek", content.source);
            Assert.Throws<AIException>(() => AIProtocol.Read(Chat("{}"), AIProvider.DeepSeek, AIConnection.Direct));
            Assert.Throws<AIException>(() => AIProtocol.Read(Chat(finish: "length"), AIProvider.DeepSeek, AIConnection.Direct));
            Assert.Throws<AIException>(() => AIProtocol.Read(Chat(Content.Replace("打开一份材料并读两分钟", new string('x', 141))), AIProvider.DeepSeek, AIConnection.Direct));
        }
        [Test]
        public async Task ProviderContentCannotModifyAnyGameRuleAndSettingsAreFrozenPerRequest()
        {
            var life = GameSession.StartMasterLife(2, 15, RunMode.Quick);
            string before = JsonUtility.ToJson(life.Snapshot());
            var transport = new Transport { Response = Chat(Content.Replace("}", ",\"energy\":999,\"victory\":true}")) };
            var settings = new AISettings { provider = AIProvider.DeepSeek };
            var adapter = new OnlineContentAdapter(settings, Secret, transport);
            settings.deepSeekModel = "changed-after-creation";
            PersonalContent result = await adapter.Personalize(NarrativeContext.From(life), CancellationToken.None);
            Assert.IsTrue(result.generatedByAI); Assert.AreEqual(before, JsonUtility.ToJson(life.Snapshot()));
            Assert.That(transport.Last.Body, Does.Not.Contain("changed-after-creation"));
        }
        [Test]
        public async Task MissingKeyNetworkAndTimeoutReturnExplicitLocalFallback()
        {
            foreach (var transport in new[] { new Transport(), new Transport { fail = true }, new Transport { cancel = true } })
            {
                string key = transport.fail || transport.cancel ? Secret : "";
                var adapter = new ResilientContentAdapter(new OnlineContentAdapter(new AISettings { provider = AIProvider.DeepSeek }, key, transport));
                PersonalContent result = await adapter.Personalize(Context, CancellationToken.None);
                Assert.IsFalse(result.generatedByAI); Assert.That(result.status, Does.Contain("本地内容").And.Not.Contain(Secret));
            }
        }
        [Test]
        public void PlayerCancellationIsNotConvertedToAnotherResult()
        {
            var cancel = new CancellationTokenSource(); cancel.Cancel();
            var adapter = new ResilientContentAdapter(new OnlineContentAdapter(new AISettings { provider = AIProvider.DeepSeek }, Secret, new Transport()));
            Assert.ThrowsAsync<OperationCanceledException>(async () => await adapter.Personalize(Context, cancel.Token)); cancel.Dispose();
        }
        [Test]
        public void ProviderKeysStayInMemoryAndNeverEnterArchiveBackups()
        {
            var secrets = new AISessionSecrets(); secrets.Set(AIProvider.DeepSeek, AIConnection.Direct, Secret);
            secrets.Set(AIProvider.AzureOpenAI, AIConnection.Direct, "azure-only");
            secrets.Set(AIProvider.DeepSeek, AIConnection.Gateway, "gateway-only");
            var archive = new ArchiveData { ai = new AISettings { provider = AIProvider.DeepSeek } };
            Assert.That(ArchiveStore.Encode(archive), Does.Not.Contain(Secret).And.Not.Contain("azure-only").And.Not.Contain("gateway-only"));
            Assert.AreEqual(Secret, secrets.Get(AIProvider.DeepSeek, AIConnection.Direct));
            Assert.AreEqual("gateway-only", secrets.Get(AIProvider.AzureOpenAI, AIConnection.Gateway));
            secrets.Clear(); Assert.AreEqual("", secrets.Get(AIProvider.DeepSeek, AIConnection.Direct));
        }
        [Test]
        public void UnityEmptyDecisionPlaceholderIsNormalizedButCorruptRecordedDecisionIsRejected()
        {
            var life = GameSession.StartMasterLife(2, 15, RunMode.Quick);
            RunSnapshot snapshot = life.Snapshot(); snapshot.master.decision = new DecisionRecord();
            Assert.IsFalse(GameSession.Restore(snapshot).InExecutionMode);
            snapshot.master.decision = new DecisionRecord { day = 1 };
            Assert.Throws<ArgumentException>(() => GameSession.Restore(snapshot));
        }
    }
}
