using CanDoItAll.AgentFramework.Maf;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.AgentFramework.Providers;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using System.Net;
using System.Text;

namespace CanDoItAll.Tests.Unit.AgentFramework;

public sealed class MafAgentRuntimeProviderHealthTests
{
    [Fact]
    public void BuildProviderTestInputMessages_treats_omitted_messages_as_empty()
    {
        var request = new ProviderTestChatRequest(
            Model: "gpt-5.4-mini",
            SystemPrompt: string.Empty,
            Messages: null!,
            Prompt: "Reply with OK only.");

        var messages = ProviderRuntimeDiagnostics.BuildProviderTestInputMessages(request);

        Assert.Empty(messages);
    }

    [Fact]
    public void BuildProviderTestInputMessages_orders_trims_and_filters_messages()
    {
        var now = DateTimeOffset.UtcNow;
        var request = new ProviderTestChatRequest(
            Model: "gpt-5.4-mini",
            SystemPrompt: string.Empty,
            Messages:
            [
                new ProviderTestChatMessage(ChatMessageRole.User, " second ", now.AddMinutes(2)),
                new ProviderTestChatMessage(ChatMessageRole.Assistant, "  ", now.AddMinutes(1)),
                new ProviderTestChatMessage(ChatMessageRole.System, " first ", now)
            ],
            Prompt: string.Empty);

        var messages = ProviderRuntimeDiagnostics.BuildProviderTestInputMessages(request);

        Assert.Collection(
            messages,
            message =>
            {
                Assert.Equal(ChatRole.System, message.Role);
                Assert.Equal("first", message.Text);
            },
            message =>
            {
                Assert.Equal(ChatRole.User, message.Role);
                Assert.Equal("second", message.Text);
            });
    }

    [Fact]
    public async Task MafRuntimeProviderDiagnostics_UseProviderRuntimeGateway()
    {
        var gateway = new CapturingMafProviderRuntimeGateway();
        var services = MafRuntimeTestServices.CreateProviderRuntimeServiceCollection()
            .AddSingleton<IMafProviderRuntimeGateway>(gateway)
            .BuildServiceProvider();
        var runtime = new MafAgentRuntime(Path.GetTempPath(), services);
        var provider = CreateProvider(ProviderKind.OpenAi, "gpt-test");

        var health = await runtime.DiagnosticsPort.TestHealthAsync(provider);
        var chat = await runtime.DiagnosticsPort.RunProbeAsync(
            provider,
            new ProviderTestChatRequest(
                Model: string.Empty,
                SystemPrompt: "system",
                Messages: [],
                Prompt: "ping"));
        var maintenance = await runtime.ModelAdministrationPort.CreateOrUpdateModelAsync(
            provider,
            new ProviderModelMaintenanceEditorRequest("base", "target", "system", 4096));

        Assert.True(health.Success);
        Assert.Equal("gpt-test", chat.Model);
        Assert.Equal("target", maintenance.ModelName);
        Assert.Equal(1, gateway.HealthCalls);
        Assert.Equal(1, gateway.ChatCalls);
        Assert.Equal(1, gateway.MaintenanceCalls);
    }

    [Fact]
    public async Task MafProviderRuntimeGateway_CorrelatesConcurrentChatDispatch()
    {
        var descriptorSource = new ProviderProfileRuntimeDescriptorStore();
        var driver = new ConcurrentChatDriver(expectedConcurrentRequests: 8);
        var factory = new AgentProviderDriverRegistryBuilder()
            .AddDriver(driver)
            .Build();
        await using var runtimePool = new ProviderRuntimePool(
            descriptorSource,
            new ProviderRuntimeHandleFactory(factory));
        var gateway = new MafProviderRuntimeGateway(descriptorSource, runtimePool);
        var provider = CreateProvider(ProviderKind.OpenAi, "gpt-test");

        var tasks = Enumerable.Range(0, 8)
            .Select(index => gateway.RunProviderTestChatAsync(
                provider,
                new ProviderTestChatRequest(
                    Model: "gpt-test",
                    SystemPrompt: string.Empty,
                    Messages: [],
                    Prompt: $"request-{index}"),
                "gpt-test"))
            .ToArray();

        var results = await Task.WhenAll(tasks);

        Assert.Equal(8, results.Select(result => result.ResponseText).Distinct(StringComparer.Ordinal).Count());
        Assert.True(driver.MaxObservedInFlight > 1);
    }

    [Fact]
    public async Task MafProviderRuntimeGateway_UnsupportedMaintenanceCapabilityFailsExplicitly()
    {
        var descriptorSource = new ProviderProfileRuntimeDescriptorStore();
        var factory = new AgentProviderDriverRegistryBuilder()
            .AddDriver(new ConcurrentChatDriver())
            .Build();
        await using var runtimePool = new ProviderRuntimePool(
            descriptorSource,
            new ProviderRuntimeHandleFactory(factory));
        var gateway = new MafProviderRuntimeGateway(descriptorSource, runtimePool);

        var exception = await Assert.ThrowsAsync<UnsupportedProviderCapabilityException>(
            () => gateway.CreateOrUpdateProviderModelAsync(
                CreateProvider(ProviderKind.OpenAi, "gpt-test"),
                new ProviderModelMaintenanceEditorRequest("base", "target", "system", 4096)));
        Assert.Equal(ProviderKind.OpenAi, exception.ProviderKind);
        Assert.Equal(AgentProviderCapabilityKind.ModelMaintenance, exception.Capability);
    }

    [Fact]
    public async Task MafProviderRuntimeGateway_OllamaMaintenanceUsesProviderCapabilityDriver()
    {
        var handler = new CapturingHandler((request, body) =>
            JsonResponse("""{"status":"success"}"""));
        using var httpClient = new HttpClient(handler);
        var descriptorSource = new ProviderProfileRuntimeDescriptorStore();
        var factory = new AgentProviderDriverRegistryBuilder()
            .AddOllamaProviderDriver(httpClient)
            .Build();
        await using var runtimePool = new ProviderRuntimePool(
            descriptorSource,
            new ProviderRuntimeHandleFactory(factory));
        var gateway = new MafProviderRuntimeGateway(descriptorSource, runtimePool);

        var result = await gateway.CreateOrUpdateProviderModelAsync(
            CreateProvider(ProviderKind.Ollama, "llama3.1"),
            new ProviderModelMaintenanceEditorRequest(
                "llama3.1",
                "custom-model",
                "system prompt",
                4096));

        Assert.Equal("custom-model", result.ModelName);
        Assert.Equal("success", result.StatusMessage);
        Assert.Contains("PARAMETER num_ctx 4096", result.Modelfile, StringComparison.Ordinal);
        var request = Assert.Single(handler.Requests);
        Assert.Equal("/api/create", request.PathAndQuery);
        Assert.Contains("\"model\":\"custom-model\"", request.Body, StringComparison.Ordinal);
        Assert.Contains("\"from\":\"llama3.1\"", request.Body, StringComparison.Ordinal);
    }

    [Fact]
    public void GenericRuntimeContracts_DoNotExposeOllamaMaintenanceNames()
    {
        var root = FindRepositoryRoot();
        var files = new[]
        {
            "src/MAF/Common/CanDoItAll.AgentFramework.Core/Contracts/Contracts.cs",
            "src/MAF/Common/CanDoItAll.AgentFramework.Core/Providers/ProviderServices.cs"
        };

        foreach (var relativePath in files)
        {
            var text = File.ReadAllText(Path.Combine(root, relativePath));
            Assert.DoesNotContain("CreateOrUpdateOllamaModelAsync", text, StringComparison.Ordinal);
            Assert.DoesNotContain("ApplyOllamaModelResult", text, StringComparison.Ordinal);
            Assert.DoesNotContain("OllamaModelfileRequest", text, StringComparison.Ordinal);
            Assert.DoesNotContain("OllamaModelfileResult", text, StringComparison.Ordinal);
        }
    }

    private static ProviderProfile CreateProvider(
        ProviderKind kind,
        string defaultModel)
    {
        return new ProviderProfile(
            Guid.NewGuid(),
            $"{kind} provider",
            kind,
            kind == ProviderKind.Ollama ? "http://localhost:11434" : "https://example.test",
            "TEST_API_KEY",
            defaultModel,
            ProviderTransportKind.ChatCompletions,
            IsEnabled: true,
            SupportsStreaming: false,
            SupportsTools: false,
            PreferFrameworkManagedChatHistory: true,
            SupportsBackgroundResponses: false,
            ConfigurationJson: "{}",
            Notes: string.Empty,
            HealthStatus: "Not checked",
            LastCheckedAtUtc: null,
            SuggestedModels: [defaultModel]);
    }

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(Directory.GetCurrentDirectory());
        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "CanDoItAll.slnx")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        throw new DirectoryNotFoundException("Could not find repository root.");
    }

    private sealed class CapturingMafProviderRuntimeGateway : IMafProviderRuntimeGateway
    {
        public int HealthCalls { get; private set; }

        public int ChatCalls { get; private set; }

        public int MaintenanceCalls { get; private set; }

        public Task<ProviderHealthResult> TestProviderAsync(
            ProviderProfile provider,
            CancellationToken cancellationToken = default)
        {
            HealthCalls++;
            return Task.FromResult(new ProviderHealthResult(true, "ok", [provider.DefaultModel]));
        }

        public Task<ProviderTestChatResult> RunProviderTestChatAsync(
            ProviderProfile provider,
            ProviderTestChatRequest request,
            string model,
            CancellationToken cancellationToken = default)
        {
            ChatCalls++;
            return Task.FromResult(new ProviderTestChatResult(model, "chat", 1, 2));
        }

        public Task<ProviderTestChatResult> RunProviderImageChatAsync(
            ProviderProfile provider,
            ProviderTestChatRequest request,
            string model,
            IReadOnlyList<ProviderChatAttachment> attachments,
            string modelParameterConfigurationJson = "",
            CancellationToken cancellationToken = default)
        {
            ChatCalls++;
            return Task.FromResult(new ProviderTestChatResult(model, "image chat", 1, 2));
        }

        public Task<ProviderModelMaintenanceEditorResult> CreateOrUpdateProviderModelAsync(
            ProviderProfile provider,
            ProviderModelMaintenanceEditorRequest request,
            CancellationToken cancellationToken = default)
        {
            MaintenanceCalls++;
            return Task.FromResult(new ProviderModelMaintenanceEditorResult(
                request.TargetModel,
                request.BaseModel,
                request.SystemPrompt,
                request.ContextLength,
                "definition",
                "ok"));
        }
    }

    private sealed class CapturingHandler(Func<HttpRequestMessage, string, HttpResponseMessage> respond) : HttpMessageHandler
    {
        public List<CapturedRequest> Requests { get; } = [];

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            var body = request.Content is null
                ? string.Empty
                : await request.Content.ReadAsStringAsync(cancellationToken);
            Requests.Add(new CapturedRequest(
                request.Method,
                request.RequestUri?.PathAndQuery ?? string.Empty,
                body));
            return respond(request, body);
        }
    }

    private sealed record CapturedRequest(
        HttpMethod Method,
        string PathAndQuery,
        string Body);

    private static HttpResponseMessage JsonResponse(
        string json,
        HttpStatusCode statusCode = HttpStatusCode.OK)
    {
        return new HttpResponseMessage(statusCode)
        {
            Content = new StringContent(json, Encoding.UTF8, "application/json")
        };
    }

    private sealed class ConcurrentChatDriver(int expectedConcurrentRequests = 1) :
        IProviderHealthDriver,
        IProviderChatCompletionDriver
    {
        private static readonly IReadOnlySet<AgentProviderCapabilityKind> SupportedCapabilities = new HashSet<AgentProviderCapabilityKind>
        {
            AgentProviderCapabilityKind.Health,
            AgentProviderCapabilityKind.ChatCompletion
        };

        private int inFlight;
        private int startedRequests;
        private int maxObservedInFlight;
        private readonly TaskCompletionSource releaseConcurrentRequests = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public ProviderKind ProviderKind => ProviderKind.OpenAi;

        public IReadOnlySet<AgentProviderCapabilityKind> Capabilities => SupportedCapabilities;

        public int MaxObservedInFlight => Volatile.Read(ref maxObservedInFlight);

        public ProviderDispatchLimits GetDispatchLimits(ProviderDispatchQuery query)
        {
            return ProviderDispatchLimits.Batched(
                maxBatchSize: expectedConcurrentRequests,
                maxInFlightBatches: 1,
                maxQueueDepth: expectedConcurrentRequests,
                maxQueueDelay: TimeSpan.Zero,
                requestTimeout: TimeSpan.FromSeconds(30));
        }

        public Task<ProviderHealthResult> TestHealthAsync(
            ProviderProfile provider,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult(new ProviderHealthResult(true, "healthy", [provider.DefaultModel]));
        }

        public async Task<ProviderChatCompletionResult> CompleteChatAsync(
            ProviderChatCompletionRequest request,
            CancellationToken cancellationToken = default)
        {
            var current = Interlocked.Increment(ref inFlight);
            TrackMaxObservedInFlight(current);
            try
            {
                if (Interlocked.Increment(ref startedRequests) >= expectedConcurrentRequests)
                {
                    releaseConcurrentRequests.TrySetResult();
                }

                await releaseConcurrentRequests.Task.WaitAsync(TimeSpan.FromSeconds(5), cancellationToken);
                return new ProviderChatCompletionResult(
                    request.Model,
                    request.Prompt,
                    1,
                    1);
            }
            finally
            {
                Interlocked.Decrement(ref inFlight);
            }
        }

        private void TrackMaxObservedInFlight(int current)
        {
            while (true)
            {
                var observed = Volatile.Read(ref maxObservedInFlight);
                if (current <= observed)
                {
                    return;
                }

                if (Interlocked.CompareExchange(ref maxObservedInFlight, current, observed) == observed)
                {
                    return;
                }
            }
        }
    }
}
