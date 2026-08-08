using System.Reflection;
using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Llm.Abstractions;
using CanDoItAll.AgentFramework.Maf;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.AgentFramework.Workflows.Runtime;

namespace CanDoItAll.Tests.Unit;

/// <summary>
/// Proves the ordinary workflow LLM path never transits the full agent runtime: source guards over the
/// rewritten invoker and the two new lightweight LLM projects, a structural guarantee that the port request
/// contract cannot carry workspace/authority/process concepts, and functional tests that payload content
/// (including project id GUIDs) never grants authority while usage/schema-validation behavior is preserved.
/// </summary>
public sealed class WorkflowLlmLightweightPathTests
{
    private static readonly string[] ForbiddenFullAgentTokens =
    [
        "IAgentRuntime",
        "new AgentDefinition(",
        "new ChatSessionRecord(",
        "TryResolveProjectId(",
        "TryResolveProjectScope(",
        "RuntimeCapabilityComposer",
        "AgentRuntimeTransientContext"
    ];

    private static readonly string[] ForbiddenLlmAbstractionTokens =
    [
        "Microsoft.Agents.AI",
        "Microsoft.Extensions.AI",
        "OpenAI.",
        "Azure.AI.OpenAI",
        "AgentDefinition",
        "ChatSessionRecord",
        "IAgentRuntime",
        "IAgentExecutionRuntime",
        "WorkspaceScopeDescriptor",
        "AgentRuntimeTransientContext",
        "ProcessStepOutcome",
        "CanDoItAll.Modules.",
        "new HttpClient(",
        "Environment.GetEnvironmentVariable("
    ];

    [Fact]
    public void MafWorkflowLlmComponentInvokerSourceHasNoFullAgentRuntimeTokensAndUsesTheLightweightPort()
    {
        var source = File.ReadAllText(Path.Combine(
            FindRepositoryRoot(),
            "src", "MAF", "Workflows", "CanDoItAll.AgentFramework.Workflows.Runtime", "WorkflowLlmComponentInvoker.cs"));

        foreach (var token in ForbiddenFullAgentTokens)
        {
            Assert.DoesNotContain(token, source, StringComparison.Ordinal);
        }

        Assert.Contains("ILlmInvocationPort", source, StringComparison.Ordinal);
    }

    [Fact]
    public void LlmAbstractionsAndProviderRuntimeSourcesContainNoForbiddenDependencies()
    {
        var root = FindRepositoryRoot();
        var scanRoots = new[]
        {
            Path.Combine(root, "src", "MAF", "Common", "CanDoItAll.AgentFramework.Llm.Abstractions"),
            Path.Combine(root, "src", "MAF", "Common", "CanDoItAll.AgentFramework.Llm.ProviderRuntime")
        };

        foreach (var scanRoot in scanRoots)
        {
            Assert.True(Directory.Exists(scanRoot), $"Expected project directory: {scanRoot}");
            foreach (var path in Directory.EnumerateFiles(scanRoot, "*.cs", SearchOption.AllDirectories))
            {
                var text = File.ReadAllText(path);
                foreach (var token in ForbiddenLlmAbstractionTokens)
                {
                    Assert.False(
                        text.Contains(token, StringComparison.Ordinal),
                        $"{path} leaks forbidden lightweight LLM dependency: {token}");
                }
            }
        }
    }

    [Fact]
    public void LlmInvocationRequestContractExposesNoWorkspaceAuthorityOrProcessConcept()
    {
        var propertyNames = typeof(LlmInvocationRequest).GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Select(property => property.Name)
            .ToArray();

        // Reflection member order is unspecified for records that mix explicit (validated) and
        // compiler-synthesized positional properties, so this compares the property set, not its order.
        Assert.Equal(
            new[] { "Provider", "Model", "Messages", "Attachments", "ResponseFormat", "Settings", "Timeout", "CorrelationId" }.OrderBy(name => name, StringComparer.Ordinal),
            propertyNames.OrderBy(name => name, StringComparer.Ordinal));
        Assert.DoesNotContain(propertyNames, name =>
            name.Contains("Scope", StringComparison.OrdinalIgnoreCase) ||
            name.Contains("Workspace", StringComparison.OrdinalIgnoreCase) ||
            name.Contains("Authority", StringComparison.OrdinalIgnoreCase) ||
            name.Contains("Process", StringComparison.OrdinalIgnoreCase) ||
            name.Contains("Session", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task InvokerPreservesUsageObservationsOnSuccess()
    {
        var port = new RecordingLlmInvocationPort((request, cancellationToken) =>
            Task.FromResult(new LlmInvocationResult(request.Model, "{\"ok\":true}", new LlmUsage(40, 12, 4))));
        var provider = CreateProviderProfile();
        var invoker = new WorkflowLlmComponentInvoker(port, new SingleProviderSource(provider), new ProviderProfileService());
        var component = CreateComponent();
        var node = CreateNode(component.Id);
        var definition = CreateDefinition(node);

        var result = await invoker.ExecuteAsync(definition, node, component, new WorkflowNodeInput("{}"));

        var observation = Assert.Single(result.UsageObservations);
        Assert.Equal(40, observation.InputTokens);
        Assert.Equal(4, observation.CachedInputTokens);
        Assert.Equal(12, observation.OutputTokens);
        Assert.Equal(52, observation.TotalTokens);
    }

    [Fact]
    public async Task InvokerPreservesZeroedUsageObservationOnPortFailure()
    {
        var port = new RecordingLlmInvocationPort((request, cancellationToken) =>
            throw new InvalidOperationException("simulated port failure"));
        var provider = CreateProviderProfile();
        var invoker = new WorkflowLlmComponentInvoker(port, new SingleProviderSource(provider), new ProviderProfileService());
        var component = CreateComponent();
        var node = CreateNode(component.Id);
        var definition = CreateDefinition(node);

        var exception = await Assert.ThrowsAsync<WorkflowUsageObservationException>(() => invoker.ExecuteAsync(
            definition,
            node,
            component,
            new WorkflowNodeInput("{}")).AsTask());

        var observation = Assert.Single(exception.Observations);
        Assert.Equal(0, observation.InputTokens);
        Assert.Equal(0, observation.OutputTokens);
        Assert.Equal(0, observation.TotalTokens);
    }

    [Fact]
    public async Task InvokerLeavesCancellationUnwrapped()
    {
        var port = new RecordingLlmInvocationPort((request, cancellationToken) =>
            Task.FromCanceled<LlmInvocationResult>(new CancellationToken(canceled: true)));
        var provider = CreateProviderProfile();
        var invoker = new WorkflowLlmComponentInvoker(port, new SingleProviderSource(provider), new ProviderProfileService());
        var component = CreateComponent();
        var node = CreateNode(component.Id);
        var definition = CreateDefinition(node);

        var exception = await Record.ExceptionAsync(() => invoker.ExecuteAsync(
            definition,
            node,
            component,
            new WorkflowNodeInput("{}")).AsTask());

        Assert.IsAssignableFrom<OperationCanceledException>(exception);
    }

    [Fact]
    public async Task InvokerStillEnforcesJsonSchemaValidationAgainstThePortResponse()
    {
        var port = new RecordingLlmInvocationPort((request, cancellationToken) =>
            Task.FromResult(new LlmInvocationResult(request.Model, "{\"unexpected\":true}", new LlmUsage(10, 5))));
        var provider = CreateProviderProfile();
        var invoker = new WorkflowLlmComponentInvoker(port, new SingleProviderSource(provider), new ProviderProfileService());
        var component = CreateComponent(
            """{"type":"object","required":["markdown"],"properties":{"markdown":{"type":"string"}}}""");
        var node = CreateNode(component.Id);
        var definition = CreateDefinition(node);

        var exception = await Assert.ThrowsAsync<WorkflowUsageObservationException>(() => invoker.ExecuteAsync(
            definition,
            node,
            component,
            new WorkflowNodeInput("{}")).AsTask());

        Assert.Contains("response JSON Schema validation", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task InvokerRequestNeverCarriesWorkspaceAuthorityEvenWithProjectIdPayload()
    {
        var projectId = Guid.NewGuid();
        var port = new RecordingLlmInvocationPort((request, cancellationToken) =>
            Task.FromResult(new LlmInvocationResult(request.Model, "{\"ok\":true}", new LlmUsage(1, 1))));
        var provider = CreateProviderProfile();
        var invoker = new WorkflowLlmComponentInvoker(port, new SingleProviderSource(provider), new ProviderProfileService());
        var component = CreateComponent();
        var node = CreateNode(component.Id);
        var definition = CreateDefinition(node);

        await invoker.ExecuteAsync(
            definition,
            node,
            component,
            new WorkflowNodeInput($$"""{"projectId":"{{projectId:D}}","path":"C:\\workspace\\secret"}"""));

        Assert.NotNull(port.LastRequest);
        Assert.Equal(provider.Id, port.LastRequest!.Provider.Id);
        Assert.Equal(provider.DefaultModel, port.LastRequest.Model);
        Assert.All(port.LastRequest.Messages, message => Assert.True(message.Role is LlmMessageRole.System or LlmMessageRole.User));
        Assert.Empty(port.LastRequest.Attachments);
    }

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "CanDoItAll.slnx")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        throw new InvalidOperationException("Unable to locate repository root.");
    }

    private static ProviderProfile CreateProviderProfile()
        => new(
            Guid.NewGuid(),
            "Lightweight path test provider",
            ProviderKind.OpenAi,
            "https://example.invalid/v1",
            "LIGHTWEIGHT_PATH_TEST_API_KEY",
            "gpt-lightweight-path",
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
            SuggestedModels: ["gpt-lightweight-path"]);

    private static LlmCallComponent CreateComponent(string responseFormatJsonSchema = "")
        => new(
            WorkflowComponentId.New(),
            "Lightweight path component",
            ProviderProfileId: null,
            "gpt-lightweight-path",
            WorkflowModality.Text,
            new WorkflowModelSettings(
                Temperature: 0.2,
                MaxOutputTokens: 200,
                RequireJsonOutput: true,
                ResponseFormatJsonSchema: responseFormatJsonSchema),
            "Transform the workflow payload.",
            WorkflowValueShape.Text,
            new WorkflowValueShape(WorkflowValueShapeKind.Json, "{}", "JSON"),
            AgentPermissionsPolicy.Default,
            DateTimeOffset.UtcNow,
            DateTimeOffset.UtcNow);

    private static WorkflowNode CreateNode(WorkflowComponentId componentId)
        => new(
            new WorkflowNodeId("lightweight-path-node"),
            WorkflowNodeKind.LlmCall,
            "Lightweight path node",
            [],
            new WorkflowNodeSettings(
                componentId,
                AgentId: null,
                SubworkflowId: null,
                ExternalRequestKind: null,
                Instructions: "Pinned lightweight path instruction snapshot.",
                InputShape: WorkflowValueShape.Text,
                ResultShape: new WorkflowValueShape(WorkflowValueShapeKind.Json, "{}", "JSON")));

    private static WorkflowDefinition CreateDefinition(WorkflowNode node)
        => new(
            WorkflowId.New(),
            WorkflowVersionId.New(),
            "Lightweight path workflow",
            "Lightweight path workflow for tests.",
            WorkflowLifecycleStatus.Draft,
            new WorkflowGraph(node.Id, [node], []),
            new WorkflowRuntimePolicy(
                WorkflowRuntimeBackendKind.InProcess,
                AllowInProcessPreviewRuns: true,
                RequireDurableProductionRuns: false,
                ExposeAzureFunctionsStatusEndpoint: false,
                ExposeAzureFunctionsMcpTool: false),
            DateTimeOffset.UtcNow,
            DateTimeOffset.UtcNow);

    private sealed class RecordingLlmInvocationPort(
        Func<LlmInvocationRequest, CancellationToken, Task<LlmInvocationResult>> respond) : ILlmInvocationPort
    {
        public LlmInvocationRequest? LastRequest { get; private set; }

        public Task<LlmInvocationResult> InvokeAsync(
            LlmInvocationRequest request,
            CancellationToken cancellationToken = default)
        {
            LastRequest = request;
            return respond(request, cancellationToken);
        }
    }

    private sealed class SingleProviderSource(ProviderProfile provider) : IProviderRuntimeProfileSource
    {
        public Task<IReadOnlyList<ProviderProfile>> ListProvidersAsync(CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<ProviderProfile>>([provider]);

        public Task<ProviderProfile?> GetProviderAsync(Guid providerId, CancellationToken cancellationToken = default)
            => Task.FromResult<ProviderProfile?>(provider.Id == providerId ? provider : null);
    }
}
