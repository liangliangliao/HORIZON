using System;
using System.Collections.Generic;
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
        [Test]
        public async Task PastedResponsesEndpointOverridesStaleFoundrySelectorAndDiscoversDeployment()
        {
            var settings = new AISettings { provider = AIProvider.AzureOpenAI,
                azureEndpoint = "https://modleapikey-resource.services.ai.azure.com/openai/v1/responses", azureAccessMode = AzureAccessMode.FoundryModels };
            var transport = new Transport();
            transport.Responses.Enqueue(new AIResponse(200, "{\"data\":[{\"id\":\"coach\"}]}"));
            transport.Responses.Enqueue(new AIResponse(200, "{\"status\":\"completed\",\"output\":[{\"type\":\"reasoning\"},{\"type\":\"message\",\"status\":\"completed\",\"content\":[{\"type\":\"output_text\",\"text\":" + Quote(Content) + "}]}]}"));
            PersonalContent result = await new OnlineContentAdapter(settings, Secret, transport).Personalize(Context, CancellationToken.None);
            Assert.AreEqual(2, transport.Calls.Count);
            Assert.That(transport.Last.Url, Does.EndWith("/openai/v1/responses"));
            Assert.That(transport.Last.Body, Does.Contain("max_output_tokens").And.Contain("\"input\"").And.Contain("\"store\":false").And.Not.Contain("max_tokens").And.Not.Contain(Secret));
            Assert.AreEqual("coach", AzureEndpoints.ReadModels(new AIResponse(200, "{\"data\":[{\"id\":\"coach\"}]}"))[0]);
            Assert.That(result.status, Does.Contain("Responses")); Assert.IsTrue(result.generatedByAI);
        }

        [TestCase("incomplete")]
        [TestCase("failed")]
        [TestCase("queued")]
        public void ResponsesMustBeCompletedBeforeAnyContentIsAccepted(string status)
        { Assert.Throws<AIException>(() => AIProtocol.Read(new AIResponse(200, "{\"status\":\"" + status + "\",\"output\":[]}"), AIProvider.AzureOpenAI, AIConnection.Direct)); }

        [Test]
        public void FullAzureDeploymentUrlCanSupplyItsOwnDeployment()
        {
            var settings = new AISettings { provider = AIProvider.AzureOpenAI,
                azureEndpoint = "https://resource.openai.azure.com/openai/deployments/interview%20coach/chat/completions" };
            Assert.AreEqual("interview coach", AzureEndpoints.Deployments(settings).Single());
            Assert.That(AIProtocol.Build(settings, Secret, Context).Url, Does.Contain("/deployments/interview%20coach/chat/completions"));
        }

        private const string Secret = "test-key-never-persist-this";
        private const string Content = "{\"futureSelfLine\":\"你已经走过一步。\",\"quest\":\"打开一份材料并读两分钟\",\"patternExplanation\":\"近期证据还不足，需要继续观察。\"}";
        private static NarrativeContext Context { get { return new NarrativeContext("面试", "近期犹豫", Enumerable.Range(1, 10).Select(x => "D" + x)); } }
        private static AIResponse Chat(string content = Content, string finish = "stop")
        { return new AIResponse(200, "{\"choices\":[{\"finish_reason\":\"" + finish + "\",\"message\":{\"content\":" + Quote(content) + "}}]}"); }
        private static string Quote(string value) { return "\"" + value.Replace("\\", "\\\\").Replace("\"", "\\\"").Replace("\n", "\\n") + "\""; }
        private sealed class Transport : IAITransport
        {
            public AIRequest Last;
            public readonly List<AIRequest> Calls = new List<AIRequest>();
            public readonly Queue<AIResponse> Responses = new Queue<AIResponse>();
            public Action AfterSend;
            public AIResponse Response = Chat();
            public bool cancel, fail;
            public Task<AIResponse> Send(AIRequest request, CancellationToken cancellation)
            {
                Last = request; Calls.Add(request); cancellation.ThrowIfCancellationRequested();
                if (cancel) throw new OperationCanceledException(); if (fail) throw new Exception(Secret);
                AIResponse response = Responses.Count > 0 ? Responses.Dequeue() : Response;
                AfterSend?.Invoke(); return Task.FromResult(response);
            }
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
        [TestCase("https://resource.services.ai.azure.com/api/projects/interview/", "https://resource.services.ai.azure.com", AzureAccessMode.OpenAIV1)]
        [TestCase("https://resource.services.ai.azure.com", "https://resource.services.ai.azure.com", AzureAccessMode.OpenAIV1)]
        [TestCase("https://resource.openai.azure.com/openai/v1/", "https://resource.openai.azure.com", AzureAccessMode.OpenAIV1)]
        [TestCase("https://resource.openai.azure.com/openai/v1/chat/completions", "https://resource.openai.azure.com", AzureAccessMode.OpenAIV1)]
        [TestCase("https://resource.services.ai.azure.com/models/", "https://resource.services.ai.azure.com", AzureAccessMode.FoundryModels)]
        [TestCase("https://resource.openai.azure.com/", "https://resource.openai.azure.com", AzureAccessMode.AzureOpenAI)]
        public void AzureResourceAndProjectEndpointsNormalizeWithoutChangingOrigin(string raw, string root, AzureAccessMode preferred)
        {
            var settings = new AISettings { provider = AIProvider.AzureOpenAI, azureEndpoint = raw, azureDeployment = "coach" };
            AzureEndpoint endpoint = AzureEndpoints.Resolve(settings);
            Assert.AreEqual(root, endpoint.Root); Assert.AreEqual(preferred, endpoint.Modes[0]);
            Assert.That(AIProtocol.Build(settings, Secret, Context).Url, Does.StartWith(root + "/"));
        }
        [TestCase("http://resource.services.ai.azure.com/api/projects/demo")]
        [TestCase("https://user:key@resource.services.ai.azure.com/api/projects/demo")]
        [TestCase("https://resource.services.ai.azure.com/api/projects/demo?api-key=hidden")]
        [TestCase("https://resource.services.ai.azure.com/api/projects/demo#hidden")]
        [TestCase("https://resource.services.ai.azure.com/api/projects/demo/unknown")]
        [TestCase("https://resource.services.ai.azure.com/unknown")]
        public void AzureRejectsAmbiguousOrCredentialBearingPathsBeforeSending(string raw)
        { Assert.Throws<AIException>(() => AzureEndpoints.Resolve(new AISettings { azureEndpoint = raw })); }
        [Test]
        public void AzureDefaultsAndPayloadsMatchEachApiWhileV1OmitsDatedVersion()
        {
            var settings = new AISettings { provider = AIProvider.AzureOpenAI, azureEndpoint = "https://resource.services.ai.azure.com", azureDeployment = "coach" };
            AIRequest classic = AIProtocol.BuildAzure(settings, Secret, Context, "coach", AzureAccessMode.AzureOpenAI);
            Assert.That(classic.Url, Does.EndWith("api-version=2024-10-21"));
            Assert.That(classic.Body, Does.Not.Contain("\"model\""));
            AIRequest foundry = AIProtocol.BuildAzure(settings, Secret, Context, "coach", AzureAccessMode.FoundryModels);
            Assert.That(foundry.Url, Does.EndWith("/models/chat/completions?api-version=2024-05-01-preview"));
            Assert.That(foundry.Body, Does.Contain("\"model\":\"coach\"").And.Contain("max_tokens").And.Not.Contain("max_completion_tokens"));
            settings.azureApiVersion = "2024-10-21";
            AIRequest v1 = AIProtocol.BuildAzure(settings, Secret, Context, "coach", AzureAccessMode.OpenAIV1);
            Assert.That(v1.Url, Does.EndWith("/openai/v1/chat/completions").And.Not.Contain("api-version"));
            Assert.That(v1.Body, Does.Contain("\"model\":\"coach\"").And.Contain("max_completion_tokens"));
        }
        [Test]
        public void AzureServerlessInferenceKeepsItsRootPathAndSdkCompatibleKeyHeaders()
        {
            var settings = new AISettings { provider = AIProvider.AzureOpenAI, azureEndpoint = "https://coach.models.ai.azure.com", azureDeployment = "coach" };
            AIRequest request = AIProtocol.Build(settings, Secret, Context);
            Assert.AreEqual("https://coach.models.ai.azure.com/chat/completions?api-version=2024-05-01-preview", request.Url);
            Assert.AreEqual(Secret, request.Headers["api-key"]); Assert.AreEqual("Bearer " + Secret, request.Headers["Authorization"]);
        }
        [Test]
        public async Task Azure404TriesAnotherPathOnTheSameOriginWithoutTouchingGameRules()
        {
            var settings = new AISettings { provider = AIProvider.AzureOpenAI, azureEndpoint = "https://resource.openai.azure.com", azureDeployment = "coach", azureResourceName = "练习资源" };
            var transport = new Transport(); transport.Responses.Enqueue(new AIResponse(404, Secret)); transport.Responses.Enqueue(Chat());
            var life = GameSession.StartMasterLife(2, 15, RunMode.Quick); string before = JsonUtility.ToJson(life.Snapshot());
            PersonalContent result = await new OnlineContentAdapter(settings, Secret, transport).Personalize(NarrativeContext.From(life), CancellationToken.None);
            Assert.AreEqual(2, transport.Calls.Count);
            Assert.That(transport.Calls[0].Url, Does.Contain("/openai/deployments/coach/"));
            Assert.AreEqual("https://resource.openai.azure.com/openai/v1/chat/completions", transport.Calls[1].Url);
            Assert.AreEqual("练习资源 / coach", result.source); Assert.That(result.status, Does.Contain("v1"));
            Assert.AreEqual(before, JsonUtility.ToJson(life.Snapshot()));
            Assert.That(transport.Calls.All(x => x.Headers["api-key"] == Secret), Is.True);
        }
        [Test]
        public async Task FoundryProject404MovesFromV1ToModelsWithAnApiSpecificDefault()
        {
            var settings = new AISettings { provider = AIProvider.AzureOpenAI, azureEndpoint = "https://resource.services.ai.azure.com/api/projects/demo", azureDeployment = "coach" };
            var transport = new Transport(); transport.Responses.Enqueue(new AIResponse(404, "")); transport.Responses.Enqueue(Chat());
            await new OnlineContentAdapter(settings, Secret, transport).Personalize(Context, CancellationToken.None);
            Assert.AreEqual("https://resource.services.ai.azure.com/openai/v1/chat/completions", transport.Calls[0].Url);
            Assert.AreEqual("https://resource.services.ai.azure.com/models/chat/completions?api-version=2024-05-01-preview", transport.Calls[1].Url);
            Assert.That(transport.Calls.All(x => !x.Url.Contains("api/projects")), Is.True);
        }
        [Test]
        public async Task CommaSeparatedDeploymentsPrioritizeTheSelectedModelAndSkipMissingDeployments()
        {
            var settings = new AISettings { provider = AIProvider.AzureOpenAI, azureEndpoint = "https://resource.services.ai.azure.com", azureDeployment = "good, missing，good", azureSelectedDeployment = "missing" };
            var transport = new Transport(); for (int i = 0; i < 3; i++) transport.Responses.Enqueue(new AIResponse(404, "")); transport.Responses.Enqueue(Chat());
            PersonalContent result = await new OnlineContentAdapter(settings, Secret, transport).Personalize(Context, CancellationToken.None);
            Assert.AreEqual(4, transport.Calls.Count);
            Assert.That(transport.Calls[0].Body, Does.Contain("\"model\":\"missing\""));
            Assert.That(transport.Calls[3].Body, Does.Contain("\"model\":\"good\""));
            Assert.AreEqual("resource / good", result.source);
        }
        [TestCase(401, AIError.Credentials)]
        [TestCase(403, AIError.Credentials)]
        [TestCase(429, AIError.RateLimit)]
        [TestCase(500, AIError.Unavailable)]
        [TestCase(503, AIError.Unavailable)]
        [TestCase(504, AIError.Timeout)]
        [TestCase(400, AIError.Unavailable)]
        public void AzureDoesNotRetryAuthenticationLimitsTimeoutsOrOtherFailures(int status, AIError code)
        {
            var transport = new Transport { Response = new AIResponse(status, Secret) };
            var settings = new AISettings { provider = AIProvider.AzureOpenAI, azureEndpoint = "https://resource.services.ai.azure.com", azureDeployment = "one,two" };
            AIException error = Assert.ThrowsAsync<AIException>(async () => await new OnlineContentAdapter(settings, Secret, transport).Personalize(Context, CancellationToken.None));
            Assert.AreEqual(code, error.Code); Assert.AreEqual(1, transport.Calls.Count); Assert.That(error.Message, Does.Not.Contain(Secret));
        }
        [Test]
        public async Task OnlyAnExplicitMissingDeploymentErrorCanRetryA400()
        {
            var settings = new AISettings { provider = AIProvider.AzureOpenAI, azureEndpoint = "https://resource.openai.azure.com", azureDeployment = "coach" };
            var transport = new Transport(); transport.Responses.Enqueue(new AIResponse(400, "{\"error\":{\"code\":\"DeploymentNotFound\"}}")); transport.Responses.Enqueue(Chat());
            await new OnlineContentAdapter(settings, Secret, transport).Personalize(Context, CancellationToken.None);
            Assert.AreEqual(2, transport.Calls.Count);
        }
        [Test]
        public async Task EmptyDeploymentReadsV1ModelsWithGetThenUsesReturnedId()
        {
            var settings = new AISettings { provider = AIProvider.AzureOpenAI, azureEndpoint = "https://resource.services.ai.azure.com/api/projects/demo" };
            var transport = new Transport(); transport.Responses.Enqueue(new AIResponse(200, "{\"data\":[{\"id\":\"returned-coach\"}]}")); transport.Responses.Enqueue(Chat());
            await new OnlineContentAdapter(settings, Secret, transport).Personalize(Context, CancellationToken.None);
            Assert.AreEqual("GET", transport.Calls[0].Method); Assert.IsEmpty(transport.Calls[0].Body);
            Assert.AreEqual("https://resource.services.ai.azure.com/openai/v1/models", transport.Calls[0].Url);
            Assert.That(transport.Calls[1].Body, Does.Contain("\"model\":\"returned-coach\"")); Assert.AreEqual("POST", transport.Calls[1].Method);
        }
        [Test]
        public void EmptyDeploymentNeverGuessesNamesWhenModelListingIsUnavailable()
        {
            var settings = new AISettings { provider = AIProvider.AzureOpenAI, azureEndpoint = "https://resource.openai.azure.com" };
            var transport = new Transport { Response = new AIResponse(404, Secret) };
            AIException error = Assert.ThrowsAsync<AIException>(async () => await new OnlineContentAdapter(settings, Secret, transport).Personalize(Context, CancellationToken.None));
            Assert.AreEqual(AIError.Deployment, error.Code); Assert.AreEqual(1, transport.Calls.Count);
            Assert.That(error.Message, Does.Contain("部署名").And.Not.Contain(Secret));
        }
        [Test]
        public void AzureDeploymentListsAndDiscoveredModelsHaveFiniteBounds()
        {
            Assert.Throws<AIException>(() => AzureEndpoints.Deployments(new AISettings { azureDeployment = "a,b,c,d,e,f,g" }));
            Assert.Throws<AIException>(() => AzureEndpoints.Deployments(new AISettings { azureDeployment = "a\nb" }));
            Assert.Throws<AIException>(() => AzureEndpoints.ReadModels(new AIResponse(200, "{}")));
            string data = "{\"data\":[" + string.Join(",", Enumerable.Range(0, 20).Select(x => "{\"id\":\"coach" + x + "\"}")) + "]}";
            Assert.AreEqual(6, AzureEndpoints.ReadModels(new AIResponse(200, data)).Length);
        }
        [Test]
        public void CancellationBetweenAzureFallbackAttemptsStopsImmediately()
        {
            using (var cancel = new CancellationTokenSource())
            {
                var settings = new AISettings { provider = AIProvider.AzureOpenAI, azureEndpoint = "https://resource.openai.azure.com", azureDeployment = "coach" };
                var transport = new Transport { Response = new AIResponse(404, ""), AfterSend = () => cancel.Cancel() };
                var adapter = new ResilientContentAdapter(new OnlineContentAdapter(settings, Secret, transport));
                Assert.CatchAsync<OperationCanceledException>(async () => await adapter.Personalize(Context, cancel.Token)); Assert.AreEqual(1, transport.Calls.Count);
            }
        }
        [Test]
        public void OlderAzureSettingsKeepWorkingAndNewProfileFieldsSurviveBackupWithoutKeys()
        {
            var legacy = JsonUtility.FromJson<AISettings>("{\"provider\":2,\"azureEndpoint\":\"https://example.openai.azure.com\",\"azureDeployment\":\"coach\",\"azureApiVersion\":\"2024-10-21\"}");
            legacy.Repair(); Assert.AreEqual(AzureAccessMode.Auto, legacy.azureAccessMode);
            Assert.That(AIProtocol.Build(legacy, Secret, Context).Url, Does.Contain("/openai/deployments/coach/"));
            legacy.azureResourceName = "练习资源"; legacy.azureDeployment = "one,two"; legacy.azureSelectedDeployment = "two";
            AISettings restored = JsonUtility.FromJson<AISettings>(JsonUtility.ToJson(legacy)); restored.Repair();
            Assert.AreEqual("练习资源", restored.azureResourceName); Assert.AreEqual("two", AzureEndpoints.Deployments(restored)[0]);
            Assert.That(ArchiveStore.Encode(new ArchiveData { ai = restored }), Does.Not.Contain(Secret));
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
            Assert.CatchAsync<OperationCanceledException>(async () => await adapter.Personalize(Context, cancel.Token)); cancel.Dispose();
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
