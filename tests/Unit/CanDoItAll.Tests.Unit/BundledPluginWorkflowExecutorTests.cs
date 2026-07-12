using System.Text.Json;
using System.Text.Json.Serialization;
using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.AgentFramework.WorkflowExecutors.Plugins;
using CanDoItAll.Infrastructure.Storage;
using CanDoItAll.Modules.Plugins;
using CanDoItAll.Plugins.Abstractions;
using CanDoItAll.SharedKernel.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;

namespace CanDoItAll.Tests.Unit;

public sealed class BundledPluginWorkflowExecutorTests
{
    private static readonly JsonSerializerOptions JsonOptions = CreateJsonOptions();

    [Fact]
    public void BundledPluginContributionsMatchManifestDefaultsSchemaAndSimulation()
    {
        var services = CreateFakeBackedPluginServices();

        using var provider = services.BuildServiceProvider();
        using var scope = provider.CreateScope();
        var plugins = scope.ServiceProvider.GetServices<ICanDoItAllPlugin>()
            .Select(plugin => plugin.Descriptor)
            .Where(plugin => plugin.Id == GmailPluginConstants.PluginId ||
                             plugin.Id == Office365PluginConstants.PluginId ||
                             plugin.Id == DockerPluginConstants.PluginId)
            .ToArray();
        var contributions = scope.ServiceProvider.GetServices<IWorkflowExecutorContribution>().ToArray();

        Assert.Equal(3, plugins.Length);
        Assert.Equal(9, contributions.Length);
        foreach (var plugin in plugins)
        {
            foreach (var manifest in plugin.WorkflowExecutors)
            {
                var runtime = Assert.Single(contributions, contribution =>
                    contribution.Descriptor.Id == manifest.ExecutorId).Descriptor;

                Assert.Equal(plugin.Id.Value, runtime.Source.PluginId);
                Assert.Equal(manifest.Name, runtime.Name);
                Assert.Equal(manifest.Description, runtime.Description);
                Assert.Equal(manifest.Category, runtime.Category);
                Assert.Equal(manifest.SettingsRendererKey.Value, runtime.SetupRendererKey);
                Assert.Equal(manifest.DefaultSettingsJson, runtime.DefaultSettingsJson);
                Assert.Equal(manifest.SettingsSchema.Version, runtime.ConfigurationSchema.Version);
                Assert.Equal(
                    manifest.SettingsSchema.Fields.Select(ToFieldIdentity),
                    runtime.ConfigurationSchema.Fields.Select(ToFieldIdentity));
                Assert.Equal(manifest.Simulation, runtime.Simulation);
                Assert.Equal(manifest.DefaultPolicy, runtime.DefaultPolicy);
                Assert.Equal(manifest.PermissionPolicy, runtime.PermissionPolicy);
                Assert.Equal(manifest.SideEffects, runtime.SideEffects);
                Assert.Equal(manifest.DeterministicTestMode, runtime.DeterministicTestMode);
            }
        }
    }

    [Fact]
    public void PluginRegistrationsMapWorkflowClientPortsToRealClients()
    {
        var services = new ServiceCollection();
        services.AddHttpClient();
        services.AddCanDoItAllGmailPlugin(registerBundledDescriptor: false, registerWorkflowExecutors: false);
        services.AddCanDoItAllOffice365Plugin(registerBundledDescriptor: false, registerWorkflowExecutors: false);
        services.AddPluginsModule();

        using var provider = services.BuildServiceProvider();
        using var scope = provider.CreateScope();

        Assert.Same(
            scope.ServiceProvider.GetRequiredService<GmailApiClient>(),
            scope.ServiceProvider.GetRequiredService<IGmailWorkflowClient>());
        Assert.Same(
            scope.ServiceProvider.GetRequiredService<Office365GraphClient>(),
            scope.ServiceProvider.GetRequiredService<IOffice365WorkflowClient>());
        Assert.Contains(
            services,
            descriptor => descriptor.ServiceType == typeof(IPluginWorkflowOAuthService) &&
                          descriptor.Lifetime == ServiceLifetime.Scoped);
        Assert.Contains(
            services,
            descriptor => descriptor.ServiceType == typeof(IPluginWorkflowExecutorGrantEvaluator) &&
                          descriptor.Lifetime == ServiceLifetime.Scoped);
    }

    [Fact]
    public async Task GmailConcreteExecutorsExecuteThroughFakeWorkflowPorts()
    {
        var grants = FakeGrantEvaluator.AllowAll();
        var oauth = new FakeWorkflowOAuthService();
        var client = new FakeGmailWorkflowClient();
        var download = new GmailDownloadByLabelWorkflowExecutor(grants, oauth, client);
        var markProcessed = new GmailMarkProcessedWorkflowExecutor(grants, oauth, client);

        var downloadResult = await ExecuteAsync(
            download,
            new GmailWorkflowExecutorSettings
            {
                Label = "Review",
                ProcessedLabel = "Reviewed",
                MaxMessages = 1
            },
            "{\"projectId\":\"project-1\"}");
        var markResult = await ExecuteAsync(
            markProcessed,
            new GmailMarkProcessedWorkflowExecutorSettings
            {
                SourceLabel = "Review",
                ProcessedLabel = "Reviewed",
                MessageIdJsonPath = "$.messageId"
            },
            "{\"messageId\":\"gmail-1\"}");

        using var downloadPayload = JsonDocument.Parse(downloadResult.PayloadJson);
        using var markPayload = JsonDocument.Parse(markResult.PayloadJson);
        Assert.Equal("gmail", downloadPayload.RootElement.GetProperty("provider").GetString());
        Assert.Equal("project-1", downloadPayload.RootElement.GetProperty("projectId").GetString());
        Assert.Equal("gmail-1", markPayload.RootElement.GetProperty("messageId").GetString());
        Assert.True(markPayload.RootElement.GetProperty("committed").GetBoolean());
        Assert.Equal(1, client.DownloadCallCount);
        Assert.Equal(1, client.MarkProcessedCallCount);
        Assert.Equal(2, oauth.ResolveCallCount);
        Assert.Equal(2, oauth.TokenCallCount);
    }

    [Fact]
    public async Task Office365ConcreteExecutorsExecuteThroughFakeWorkflowPorts()
    {
        var grants = FakeGrantEvaluator.AllowAll();
        var oauth = new FakeWorkflowOAuthService();
        var client = new FakeOffice365WorkflowClient();
        var byCategory = new Office365DownloadByCategoryWorkflowExecutor(grants, oauth, client);
        var byAddress = new Office365DownloadByAddressWorkflowExecutor(grants, oauth, client);
        var markProcessed = new Office365MarkProcessedWorkflowExecutor(grants, oauth, client);

        var categoryResult = await ExecuteAsync(
            byCategory,
            new Office365WorkflowExecutorSettings
            {
                Category = "Review",
                ProcessedCategory = "Reviewed",
                MaxMessages = 1
            },
            "{}");
        var addressResult = await ExecuteAsync(
            byAddress,
            new Office365MessageAddressWorkflowExecutorSettings
            {
                EmailAddress = "sender@example.test",
                ProcessedCategory = "Reviewed"
            },
            "{}");
        var markResult = await ExecuteAsync(
            markProcessed,
            new Office365MarkProcessedWorkflowExecutorSettings
            {
                SourceCategory = "Review",
                ProcessedCategory = "Reviewed",
                MessageIdJsonPath = "$.messageId"
            },
            "{\"messageId\":\"graph-1\"}");

        using var categoryPayload = JsonDocument.Parse(categoryResult.PayloadJson);
        using var addressPayload = JsonDocument.Parse(addressResult.PayloadJson);
        using var markPayload = JsonDocument.Parse(markResult.PayloadJson);
        Assert.Equal("office365", categoryPayload.RootElement.GetProperty("provider").GetString());
        Assert.Equal("sender@example.test", addressPayload.RootElement.GetProperty("filterValue").GetString());
        Assert.Equal("graph-1", markPayload.RootElement.GetProperty("messageId").GetString());
        Assert.True(markPayload.RootElement.GetProperty("committed").GetBoolean());
        Assert.Equal(1, client.CategoryDownloadCallCount);
        Assert.Equal(1, client.AddressDownloadCallCount);
        Assert.Equal(1, client.MarkProcessedCallCount);
        Assert.Equal(3, oauth.ResolveCallCount);
        Assert.Equal(3, oauth.TokenCallCount);
    }

    [Fact]
    public async Task DockerConcreteExecutorsExecuteOnlyTypedRecipesThroughFakeHostPort()
    {
        var grants = FakeGrantEvaluator.AllowAll();
        var host = new FakePluginHostToolService();
        var settings = new DockerWorkflowExecutorSettings
        {
            Image = "qdrant/qdrant:v1.15.3",
            ContainerName = "workflow-proof",
            PullIfMissing = true,
            Tail = 25,
            Since = "5m",
            MaxOutputCharacters = 4096
        };
        IWorkflowExecutor[] executors =
        [
            new DockerListContainersWorkflowExecutor(grants, host),
            new DockerPullImageWorkflowExecutor(grants, host),
            new DockerStartContainerWorkflowExecutor(grants, host),
            new DockerReadLogsWorkflowExecutor(grants, host)
        ];

        foreach (var executor in executors)
        {
            var result = await ExecuteAsync(
                executor,
                settings,
                "{\"image\":\"alpine:3.20\",\"containerName\":\"fake-container\"}");

            using var payload = JsonDocument.Parse(result.PayloadJson);
            Assert.True(payload.RootElement.GetProperty("succeeded").GetBoolean());
            Assert.Equal("fake-enforced", payload.RootElement.GetProperty("boundaryMode").GetString());
        }

        Assert.Equal(
            [
                PluginHostToolRecipeIds.DockerListContainers,
                PluginHostToolRecipeIds.DockerPullImage,
                PluginHostToolRecipeIds.DockerStartContainer,
                PluginHostToolRecipeIds.DockerReadLogs
            ],
            host.Calls.Select(call => call.RecipeId));
        Assert.Equal("alpine:3.20", host.Calls[1].Arguments["image"]);
        Assert.Equal("fake-container", host.Calls[2].Arguments["containerName"]);
        Assert.Equal("25", host.Calls[3].Arguments["tail"]);
    }

    [Fact]
    public async Task PluginExecutorsRejectPermissionInputAndOperationFailuresWithoutExternalEffects()
    {
        var deniedGrants = new FakeGrantEvaluator((_, _) => false);
        var oauth = new FakeWorkflowOAuthService();
        var gmailClient = new FakeGmailWorkflowClient();
        var officeClient = new FakeOffice365WorkflowClient();
        var host = new FakePluginHostToolService();
        var gmail = new GmailMarkProcessedWorkflowExecutor(deniedGrants, oauth, gmailClient);
        var office = new Office365DownloadByAddressWorkflowExecutor(deniedGrants, oauth, officeClient);
        var docker = new DockerPullImageWorkflowExecutor(deniedGrants, host);

        Assert.False(gmail.Descriptor.Availability.IsRunnable);
        Assert.False(office.Descriptor.Availability.IsRunnable);
        Assert.False(docker.Descriptor.Availability.IsRunnable);
        var permissionException = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            ExecuteAsync(docker, new DockerWorkflowExecutorSettings(), "{}").AsTask());
        Assert.Contains("denied", permissionException.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Empty(host.Calls);

        var invalidGmail = new GmailMarkProcessedWorkflowExecutor(FakeGrantEvaluator.AllowAll(), oauth, gmailClient);
        var invalidInputException = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            ExecuteAsync(
                invalidGmail,
                new GmailMarkProcessedWorkflowExecutorSettings
                {
                    MessageIdJsonPath = "$.missing"
                },
                "{}").AsTask());
        Assert.Contains("messageIdJsonPath", invalidInputException.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(0, oauth.ResolveCallCount);
        Assert.Equal(0, gmailClient.MarkProcessedCallCount);

        var invalidOffice = new Office365DownloadByAddressWorkflowExecutor(
            FakeGrantEvaluator.AllowAll(),
            oauth,
            officeClient);
        var invalidOfficeInputException = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            ExecuteAsync(
                invalidOffice,
                new Office365MessageAddressWorkflowExecutorSettings
                {
                    EmailAddress = string.Empty,
                    EmailAddressJsonPath = "$.missing"
                },
                "{}").AsTask());
        Assert.Contains("emailAddressJsonPath", invalidOfficeInputException.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(0, oauth.ResolveCallCount);
        Assert.Equal(0, officeClient.AddressDownloadCallCount);

        var failingHost = new FakePluginHostToolService
        {
            ResultFactory = recipeId => new PluginHostToolExecutionResult(
                recipeId,
                Succeeded: false,
                ExitCode: 9,
                Message: "Masked fake host failure.",
                Stdout: string.Empty,
                Stderr: string.Empty,
                StdoutTruncated: false,
                StderrTruncated: false,
                BoundaryMode: "fake-enforced",
                BoundaryEnforced: true,
                EnvironmentVariableNames: [])
        };
        var failingDocker = new DockerListContainersWorkflowExecutor(FakeGrantEvaluator.AllowAll(), failingHost);
        var operationException = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            ExecuteAsync(failingDocker, new DockerWorkflowExecutorSettings(), "{}").AsTask());
        Assert.Equal("Masked fake host failure.", operationException.Message);

        var realGuardedHost = new DockerHostToolService(
            new StaticWorkspacePathResolver(),
            NullLogger<DockerHostToolService>.Instance);
        var unsafeInputException = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            realGuardedHost.ExecuteAsync(
                DockerPluginConstants.PluginId,
                PluginHostToolRecipeIds.DockerPullImage,
                new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                {
                    ["image"] = "alpine:3.20 --privileged"
                },
                timeoutSeconds: 30,
                maxOutputCharacters: 4096));
        Assert.Contains("invalid", unsafeInputException.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task PluginExecutorCancellationPropagatesBeforeFakeOperation()
    {
        var host = new FakePluginHostToolService();
        var executor = new DockerListContainersWorkflowExecutor(FakeGrantEvaluator.AllowAll(), host);
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            ExecuteAsync(executor, new DockerWorkflowExecutorSettings(), "{}", cancellation.Token).AsTask());

        Assert.Empty(host.Calls);
    }

    private static ServiceCollection CreateFakeBackedPluginServices()
    {
        var services = new ServiceCollection();
        services.AddSingleton<IPluginWorkflowExecutorGrantEvaluator>(FakeGrantEvaluator.AllowAll());
        services.AddSingleton<IPluginWorkflowOAuthService>(new FakeWorkflowOAuthService());
        services.AddSingleton<IGmailWorkflowClient>(new FakeGmailWorkflowClient());
        services.AddSingleton<IOffice365WorkflowClient>(new FakeOffice365WorkflowClient());
        services.AddCanDoItAllGmailPlugin();
        services.AddCanDoItAllOffice365Plugin();
        services.AddCanDoItAllDockerPlugin();
        services.AddSingleton<IPluginHostToolService>(new FakePluginHostToolService());
        return services;
    }

    private static async ValueTask<WorkflowNodeExecutionResult> ExecuteAsync<TSettings>(
        IWorkflowExecutor executor,
        TSettings settings,
        string inputJson,
        CancellationToken cancellationToken = default)
    {
        var settingsJson = JsonSerializer.Serialize(settings, JsonOptions);
        var node = new WorkflowNode(
            new WorkflowNodeId("plugin-node"),
            WorkflowNodeKind.Executor,
            "Plugin node",
            [],
            new WorkflowNodeSettings(
                ComponentId: null,
                AgentId: null,
                SubworkflowId: null,
                ExternalRequestKind: null,
                Instructions: string.Empty,
                InputShape: executor.Descriptor.InputShape,
                ResultShape: executor.Descriptor.ResultShape)
            {
                ExecutorId = executor.Descriptor.Id,
                ExecutorSettingsJson = settingsJson,
                ExecutionPolicy = executor.Descriptor.DefaultPolicy
            });
        var definition = new WorkflowDefinition(
            WorkflowId.New(),
            WorkflowVersionId.New(),
            "Plugin executor test",
            "Direct fake-backed plugin executor test.",
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
        var context = new WorkflowExecutorExecutionContext(
            definition,
            node,
            executor.Descriptor,
            settingsJson,
            executor.Descriptor.DefaultPolicy);
        return await executor.ExecuteAsync(
            context,
            new WorkflowNodeInput(inputJson),
            cancellationToken);
    }

    private static (string Key, ConfigurationFieldType Type, bool Required) ToFieldIdentity(
        ConfigurationFieldDescriptor field)
        => (field.Key, field.FieldType, field.IsRequired);

    private static JsonSerializerOptions CreateJsonOptions()
    {
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        options.Converters.Add(new JsonStringEnumConverter());
        return options;
    }

    private static PluginEmailMessage CreateMessage(string id)
        => new(
            id,
            $"thread-{id}",
            "Workflow test message",
            "sender@example.test",
            "2026-07-12T12:00:00Z",
            "Fake message.",
            "Fake message body.",
            ["Review"],
            $"https://example.test/messages/{id}");

    private sealed class FakeGrantEvaluator(
        Func<PluginCapabilityKind, PluginHostToolRecipeId?, bool> isAllowed) : IPluginWorkflowExecutorGrantEvaluator
    {
        public static FakeGrantEvaluator AllowAll()
            => new((_, _) => true);

        public PluginGrantDecision Evaluate(
            PluginId pluginId,
            PluginCapabilityKind capability,
            PluginHostToolRecipeId? recipeId = null)
            => isAllowed(capability, recipeId)
                ? PluginGrantDecision.Allow(pluginId, capability, recipeId)
                : PluginGrantDecision.Deny(
                    pluginId,
                    capability,
                    PluginGrantDecisionKind.GrantDenied,
                    "Plugin capability was denied by the fake grant boundary.",
                    recipeId);
    }

    private sealed class FakeWorkflowOAuthService : IPluginWorkflowOAuthService
    {
        private static readonly PluginConnectionId ConnectionId = new(
            Guid.Parse("11111111-1111-1111-1111-111111111111"));

        public int ResolveCallCount { get; private set; }

        public int TokenCallCount { get; private set; }

        public ValueTask<PluginConnectionId> ResolveConnectionIdAsync(
            PluginId pluginId,
            PluginConnectionKey connectionKey,
            string configuredConnectionId,
            IReadOnlyList<string> scopes,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            ResolveCallCount++;
            return ValueTask.FromResult(ConnectionId);
        }

        public ValueTask<PluginOAuth2TokenSnapshot> GetAccessTokenAsync(
            PluginId pluginId,
            PluginConnectionId connectionId,
            IReadOnlyList<string> scopes,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            TokenCallCount++;
            return ValueTask.FromResult(new PluginOAuth2TokenSnapshot(
                "fake-token",
                DateTimeOffset.UtcNow.AddHours(1),
                scopes));
        }
    }

    private sealed class FakeGmailWorkflowClient : IGmailWorkflowClient
    {
        public int DownloadCallCount { get; private set; }

        public int MarkProcessedCallCount { get; private set; }

        public Task<PluginEmailMessageBatch> DownloadMessagesByLabelAsync(
            string accessToken,
            string label,
            int maxMessages,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            DownloadCallCount++;
            return Task.FromResult(new PluginEmailMessageBatch(
                "gmail",
                "label",
                label,
                1,
                [CreateMessage("gmail-1")]));
        }

        public Task<GmailMessageLabelMutationResult> MarkMessageProcessedAsync(
            string accessToken,
            string messageId,
            string sourceLabel,
            string processedLabel,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            MarkProcessedCallCount++;
            return Task.FromResult(new GmailMessageLabelMutationResult(
                "gmail",
                messageId,
                sourceLabel,
                processedLabel,
                SourceLabelRemoved: true,
                ProcessedLabelAdded: true));
        }
    }

    private sealed class FakeOffice365WorkflowClient : IOffice365WorkflowClient
    {
        public int CategoryDownloadCallCount { get; private set; }

        public int AddressDownloadCallCount { get; private set; }

        public int MarkProcessedCallCount { get; private set; }

        public Task<PluginEmailMessageBatch> DownloadMessagesByCategoryAsync(
            string accessToken,
            string category,
            int maxMessages,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            CategoryDownloadCallCount++;
            return Task.FromResult(new PluginEmailMessageBatch(
                "office365",
                "category",
                category,
                1,
                [CreateMessage("graph-category-1")]));
        }

        public Task<PluginEmailMessageBatch> DownloadOneUnprocessedMessageByAddressAsync(
            string accessToken,
            Office365MessageAddressFilterSettings settings,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            AddressDownloadCallCount++;
            return Task.FromResult(new PluginEmailMessageBatch(
                "office365",
                "emailAddress",
                settings.EmailAddress,
                1,
                [CreateMessage("graph-address-1")]));
        }

        public Task<Office365MessageCategoryMutationResult> MarkMessageProcessedAsync(
            string accessToken,
            string messageId,
            string? sourceCategory,
            string processedCategory,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            MarkProcessedCallCount++;
            return Task.FromResult(new Office365MessageCategoryMutationResult(
                "office365",
                messageId,
                sourceCategory ?? string.Empty,
                processedCategory,
                SourceCategoryRemoved: true,
                ProcessedCategoryAdded: true,
                ProcessedCategoryCreated: false,
                [processedCategory]));
        }
    }

    private sealed class FakePluginHostToolService : IPluginHostToolService
    {
        public Func<PluginHostToolRecipeId, PluginHostToolExecutionResult> ResultFactory { get; init; } = recipeId =>
            new PluginHostToolExecutionResult(
                recipeId,
                Succeeded: true,
                ExitCode: 0,
                Message: "Fake recipe completed.",
                Stdout: "fake-output",
                Stderr: string.Empty,
                StdoutTruncated: false,
                StderrTruncated: false,
                BoundaryMode: "fake-enforced",
                BoundaryEnforced: true,
                EnvironmentVariableNames: []);

        public List<HostToolCall> Calls { get; } = [];

        public Task<PluginHostToolExecutionResult> ExecuteAsync(
            PluginId pluginId,
            PluginHostToolRecipeId recipeId,
            IReadOnlyDictionary<string, string> arguments,
            int timeoutSeconds,
            int maxOutputCharacters,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Calls.Add(new HostToolCall(pluginId, recipeId, arguments, timeoutSeconds, maxOutputCharacters));
            return Task.FromResult(ResultFactory(recipeId));
        }
    }

    private sealed record HostToolCall(
        PluginId PluginId,
        PluginHostToolRecipeId RecipeId,
        IReadOnlyDictionary<string, string> Arguments,
        int TimeoutSeconds,
        int MaxOutputCharacters);

    private sealed class StaticWorkspacePathResolver : IWorkspacePathResolver
    {
        public string ResolveWorkspaceRoot() => AppContext.BaseDirectory;

        public string ResolveManagedFilesRoot() => AppContext.BaseDirectory;

        public string ResolveExportsRoot() => AppContext.BaseDirectory;

        public string ResolveEvidenceRoot() => AppContext.BaseDirectory;

        public string ResolveManagerArtifactsRoot() => AppContext.BaseDirectory;
    }
}
