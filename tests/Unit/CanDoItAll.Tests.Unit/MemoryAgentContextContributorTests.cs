using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.Memory.Abstractions;
using CanDoItAll.Memory.Application;
using CanDoItAll.Modules.AgentFramework;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace CanDoItAll.Tests.Unit;

public sealed class MemoryAgentContextContributorTests
{
    [Fact]
    public async Task Context_contributor_routes_to_shared_handler_and_shapes_system_message()
    {
        var handler = new RecordingMemoryOperationHandler
        {
            QueryResult = RecordingMemoryOperationHandler.CompletedQuery(
                "Architecture context",
                "Use the generic memory provider boundary.")
        };
        var contributor = new MemoryAgentContextContributor(handler, TimeProvider.System);
        var agent = CreateAgent(new AgentMemoryAccessSettings
        {
            InvocationMode = AgentMemoryInvocationMode.Automatic,
            PreferredProviderInstanceId = MemoryProviderInstanceId.Parse("memory.context"),
            ProviderBindings = [Binding("memory.context")],
            AllowedCapabilityIds = [MemoryCapabilityIds.ContextQuerySync]
        });

        var result = await contributor.ContributeAsync(CreateRequest(
            agent,
            [new AgentContextRequestMessage(AgentContextMessageRole.User, "How should MAF use memory?")]));

        Assert.Equal(AgentContextContributionStatus.Provided, result.Status);
        var message = Assert.Single(result.Messages);
        Assert.Equal(AgentContextMessageRole.System, message.Role);
        Assert.Contains("Memory reference data", message.Text, StringComparison.Ordinal);
        Assert.Contains("Use the generic memory provider boundary.", message.Text, StringComparison.Ordinal);
        Assert.Equal("memory.context", result.TraceMetadata[MemoryAgentContextContributionTraceKeys.ProviderInstanceId]);
        Assert.NotNull(handler.LastQuery);
        Assert.Equal(MemoryOperationCallerKind.ContextContributor, handler.LastQuery.Caller.Kind);
        Assert.Equal(MemoryAgentContextContributor.ContributorIdValue, handler.LastQuery.Caller.Route);
        Assert.Equal(agent.Id.ToString("D"), handler.LastQuery.Caller.Requester.AgentId);
        Assert.Equal("memory.context", handler.LastQuery.SelectionPolicy.ExplicitProviderId?.Value);
    }

    [Fact]
    public async Task Context_contributor_skips_when_disabled_without_dispatch()
    {
        var handler = new RecordingMemoryOperationHandler();
        var contributor = new MemoryAgentContextContributor(handler, TimeProvider.System);

        var result = await contributor.ContributeAsync(CreateRequest(
            CreateAgent(new AgentMemoryAccessSettings()),
            [new AgentContextRequestMessage(AgentContextMessageRole.User, "query")]));

        Assert.Equal(AgentContextContributionStatus.Skipped, result.Status);
        Assert.Equal(
            MemoryAgentContextContributionTraceReasons.Disabled,
            result.TraceMetadata[MemoryAgentContextContributionTraceKeys.Reason]);
        Assert.Empty(handler.QueryRequests);
    }

    [Fact]
    public async Task No_provider_optional_skips_and_required_fails_without_hidden_fallback()
    {
        var handler = new RecordingMemoryOperationHandler
        {
            QueryResult = RecordingMemoryOperationHandler.NoProviderQuery()
        };
        var contributor = new MemoryAgentContextContributor(handler, TimeProvider.System);
        var optionalAgent = CreateAgent(new AgentMemoryAccessSettings
        {
            InvocationMode = AgentMemoryInvocationMode.Automatic
        });
        var requiredAgent = CreateAgent(new AgentMemoryAccessSettings
        {
            InvocationMode = AgentMemoryInvocationMode.Automatic,
            RequireContextContributions = true
        });

        var optional = await contributor.ContributeAsync(CreateRequest(
            optionalAgent,
            [new AgentContextRequestMessage(AgentContextMessageRole.User, "query")]));
        var required = await contributor.ContributeAsync(CreateRequest(
            requiredAgent,
            [new AgentContextRequestMessage(AgentContextMessageRole.User, "query")]));

        Assert.Equal(AgentContextContributionStatus.Skipped, optional.Status);
        Assert.Equal(AgentContextContributionStatus.Failed, required.Status);
        Assert.Equal(
            MemoryAgentContextContributionTraceReasons.NoProviderConfigured,
            optional.TraceMetadata[MemoryAgentContextContributionTraceKeys.Reason]);
        Assert.All(
            handler.QueryRequests,
            request => Assert.Equal(MemoryProviderFallbackBehavior.DenyImplicitFallback, request.SelectionPolicy.FallbackBehavior));
    }

    [Fact]
    public async Task Capability_policy_denies_before_handler_dispatch()
    {
        var handler = new RecordingMemoryOperationHandler();
        var contributor = new MemoryAgentContextContributor(handler, TimeProvider.System);
        var agent = CreateAgent(new AgentMemoryAccessSettings
        {
            InvocationMode = AgentMemoryInvocationMode.Automatic,
            PreferredProviderInstanceId = MemoryProviderInstanceId.Parse("memory.context"),
            ProviderBindings = [Binding("memory.context")],
            AllowedCapabilityIds = [MemoryCapabilityIds.FeedbackImmediate]
        });

        var result = await contributor.ContributeAsync(CreateRequest(
            agent,
            [new AgentContextRequestMessage(AgentContextMessageRole.User, "query")]));

        Assert.Equal(AgentContextContributionStatus.Skipped, result.Status);
        Assert.Equal(
            MemoryAgentContextContributionTraceReasons.CapabilityDenied,
            result.TraceMetadata[MemoryAgentContextContributionTraceKeys.Reason]);
        Assert.Empty(handler.QueryRequests);
    }

    [Fact]
    public void Imported_configuration_rejects_undefined_assignment_scope()
    {
        const string configuration = """
            {
              "memory": {
                "invocationMode": "Automatic",
                "providerAssignments": [
                  { "scope": "999", "key": "workflow-1", "providerInstanceId": "memory.context" }
                ]
              }
            }
            """;

        var exception = Assert.Throws<AgentMemoryConfigurationException>(
            () => AgentMemoryAccessMetadata.Read(configuration));

        Assert.Contains("valid scope", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData("memory.context", "memory.context")]
    [InlineData("memory.context", "memory.other")]
    public async Task Imported_duplicate_assignment_is_rejected_before_dispatch(
        string firstProvider,
        string secondProvider)
    {
        var configuration = $$"""
            {
              "memory": {
                "invocationMode": "Automatic",
                "providerAssignments": [
                  { "scope": "Workflow", "key": "FLOW-1", "providerInstanceId": "{{firstProvider}}" },
                  { "scope": "Workflow", "key": "flow-1", "providerInstanceId": "{{secondProvider}}" }
                ]
              }
            }
            """;
        var handler = new RecordingMemoryOperationHandler();
        var contributor = new MemoryAgentContextContributor(handler, TimeProvider.System);

        await Assert.ThrowsAsync<AgentMemoryConfigurationException>(async () =>
            await contributor.ContributeAsync(CreateRequest(
                CreateAgent(configuration),
                [new AgentContextRequestMessage(AgentContextMessageRole.User, "query")])));

        Assert.Empty(handler.QueryRequests);
    }

    [Fact]
    public async Task Legacy_async_context_flag_is_ignored_and_current_call_uses_sync_query()
    {
        var handler = new RecordingMemoryOperationHandler
        {
            QueryResult = RecordingMemoryOperationHandler.CompletedQuery("Context", "Sync result")
        };
        var contributor = new MemoryAgentContextContributor(handler, TimeProvider.System);
        var agent = CreateAgent(new AgentMemoryAccessSettings
        {
            InvocationMode = AgentMemoryInvocationMode.Automatic,
            PreferredProviderInstanceId = MemoryProviderInstanceId.Parse("memory.async"),
            ProviderBindings = [Binding("memory.async")],
            AllowAsyncContextContributions = true,
            AllowedCapabilityIds = [MemoryCapabilityIds.ContextQuerySync]
        });

        var result = await contributor.ContributeAsync(CreateRequest(
            agent,
            [new AgentContextRequestMessage(AgentContextMessageRole.User, "query")]));

        Assert.Equal(AgentContextContributionStatus.Provided, result.Status);
        Assert.Equal(
            MemoryCapabilityIds.ContextQuerySync.Value,
            handler.LastQuery?.SelectionPolicy.RequiredCapability.Value);
    }

    [Fact]
    public void AgentFrameworkModule_registers_generic_contributor()
    {
        var services = new ServiceCollection();
        services.AddAgentFrameworkModule(new ConfigurationBuilder().Build());

        Assert.Contains(
            services,
            descriptor => descriptor.ServiceType == typeof(IAgentContextContributor) &&
                          descriptor.ImplementationType == typeof(MemoryAgentContextContributor) &&
                          descriptor.Lifetime == ServiceLifetime.Scoped);
    }

    [Fact]
    public void Generic_maf_memory_paths_do_not_reference_native_cognitive_memory()
    {
        var root = FindRepositoryRoot();
        string[] scannedRoots =
        [
            Path.Combine(root, "src", "MAF"),
            Path.Combine(root, "src", "Modules", "CanDoItAll.Modules.AgentFramework")
        ];
        string[] forbiddenTerms =
        [
            "CanDoItAll.Modules.CognitiveMemory",
            "CanDoItAll.CognitiveMemory",
            "CognitiveMemoryAgentContextContributor",
            "CognitiveMemoryRecallWorkflowExecutor",
            "CognitiveMemoryProbeWorkflowExecutor",
            "CognitiveMemoryLearningProposalWorkflowExecutor",
            "ICognitiveMemory",
            "Qdrant"
        ];

        var offenders = scannedRoots
            .SelectMany(directory => Directory.EnumerateFiles(directory, "*.*", SearchOption.AllDirectories))
            .Where(IsSourceOrProjectFile)
            .Where(path => !IsBuildOutputPath(path))
            .SelectMany(path =>
            {
                var text = File.ReadAllText(path);
                return forbiddenTerms
                    .Where(term => text.Contains(term, StringComparison.Ordinal))
                    .Select(term => $"{Path.GetRelativePath(root, path)} contains {term}");
            })
            .Order(StringComparer.Ordinal)
            .ToArray();

        Assert.Empty(offenders);
    }

    [Fact]
    public void AgentMemoryAccessMetadata_round_trips_context_contribution_policy()
    {
        var configurationJson = AgentMemoryAccessMetadata.Write(
            "{}",
            new AgentMemoryAccessSettings
            {
                InvocationMode = AgentMemoryInvocationMode.Automatic,
                RequireContextContributions = true,
                AllowAsyncContextContributions = true,
                DefaultProviderInstanceId = MemoryProviderInstanceId.Parse("memory.context"),
                ProviderBindings = [Binding("memory.context")],
                AllowedCapabilityIds = [MemoryCapabilityIds.ContextQueryAsync]
            });

        var settings = AgentMemoryAccessMetadata.Read(configurationJson);

        Assert.True(settings.CanUseContextContributions);
        Assert.True(settings.RequireContextContributions);
        Assert.False(settings.AllowAsyncContextContributions);
        Assert.False(settings.CanUseMemoryTools);
        Assert.Equal("memory.context", settings.DefaultProviderInstanceId?.Value);
        Assert.Equal([MemoryCapabilityIds.ContextQueryAsync], settings.AllowedCapabilityIds);
    }

    private static AgentContextContributionRequest CreateRequest(
        AgentDefinition agent,
        IReadOnlyList<AgentContextRequestMessage> messages)
    {
        return new AgentContextContributionRequest(
            agent,
            CreateProviderProfile(),
            messages,
            new AgentContextContributionPolicy(
                AgentContextExecutionMode.InteractiveChat,
                SuppressApprovalRequirements: false,
                WorkspaceScopeDescriptor.Organization("unit")));
    }

    private static AgentMemoryProviderBindingSetting Binding(string providerId) =>
        new(
            AgentMemoryProviderAlias.Parse(providerId),
            MemoryProviderInstanceId.Parse(providerId));

    private static AgentDefinition CreateAgent(AgentMemoryAccessSettings memoryAccess)
        => CreateAgent(AgentMemoryAccessMetadata.Write("{}", memoryAccess));

    private static AgentDefinition CreateAgent(string configurationJson)
    {
        var now = DateTimeOffset.UtcNow;
        return new AgentDefinition(
            Guid.NewGuid(),
            "Memory context agent",
            "Memory context tester",
            "Tests generic memory context contribution.",
            "Use memory deliberately.",
            AgentLifecycleStatus.Active,
            Guid.NewGuid(),
            "gpt-5-mini",
            AgentWorkloadKind.Programming,
            AgentChatHistoryMode.ProviderDefault,
            0.2,
            RequirePerServiceCallChatHistoryPersistence: false,
            EnableBackgroundResponses: false,
            configurationJson,
            IsTemplate: false,
            TemplateKey: string.Empty,
            AgentPermissionsPolicy.Default,
            [],
            [],
            now,
            now);
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

        throw new DirectoryNotFoundException("Could not find repository root.");
    }

    private static bool IsSourceOrProjectFile(string path)
    {
        return path.EndsWith(".cs", StringComparison.OrdinalIgnoreCase) ||
               path.EndsWith(".csproj", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsBuildOutputPath(string path)
    {
        return path.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}", StringComparison.OrdinalIgnoreCase) ||
               path.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.OrdinalIgnoreCase);
    }

    private static ProviderProfile CreateProviderProfile()
        => new(
            Guid.NewGuid(),
            "OpenAI chat",
            ProviderKind.OpenAi,
            "https://api.openai.com",
            "OPENAI_API_KEY",
            "gpt-5-mini",
            ProviderTransportKind.ChatCompletions,
            IsEnabled: true,
            SupportsStreaming: true,
            SupportsTools: true,
            PreferFrameworkManagedChatHistory: true,
            SupportsBackgroundResponses: false,
            ConfigurationJson: string.Empty,
            Notes: string.Empty,
            HealthStatus: string.Empty,
            LastCheckedAtUtc: null,
            SuggestedModels: [],
            ProviderProfilePurpose.Chat);

    private sealed class RecordingMemoryOperationHandler : IMemoryOperationHandler
    {
        private static readonly MemoryProviderProfile ContextProvider = CreateProvider("memory.context");

        public MemoryOperationHandlerResult<MemoryContextPack> QueryResult { get; set; } =
            CompletedQuery("Context", "Result");

        public List<MemoryOperationHandlerRequest<MemoryContextQueryRequest>> QueryRequests { get; } = [];

        public MemoryOperationHandlerRequest<MemoryContextQueryRequest>? LastQuery => QueryRequests.LastOrDefault();

        public static MemoryOperationHandlerResult<MemoryContextPack> CompletedQuery(
            string summary,
            string sectionText)
        {
            var contextPack = new MemoryContextPack(
                MemoryContextPackId.New(),
                summary,
                [
                    new MemoryContextSection(
                        "Context",
                        sectionText,
                        [new MemoryCitation("memory:source", "Memory source")],
                        0.92m)
                ],
                [],
                0.9m,
                MemoryFeedbackHandle.Parse("feedback-" + Guid.NewGuid().ToString("N")));
            return new MemoryOperationHandlerResult<MemoryContextPack>(
                MemoryOperationHandlerStatus.Completed,
                MemoryProviderSelectionResult.Selected(ContextProvider, MemoryProviderSelectionReason.ExplicitProvider, MemoryCapabilityIds.ContextQuerySync),
                OperationRecord: null,
                contextPack,
                AcceptedOperation: null,
                contextPack.FeedbackHandle,
                DriverDispatchAttempted: true,
                Diagnostic: "Context query completed.");
        }

        public static MemoryOperationHandlerResult<MemoryContextPack> AcceptedQuery(MemoryOperationId operationId)
        {
            var accepted = new MemoryOperationAccepted(
                operationId,
                "/memory/operations/" + operationId,
                DateTimeOffset.UtcNow.AddMinutes(15),
                TimeSpan.FromSeconds(2),
                CallbackAvailable: false);
            return new MemoryOperationHandlerResult<MemoryContextPack>(
                MemoryOperationHandlerStatus.Accepted,
                MemoryProviderSelectionResult.Selected(
                    CreateProvider("memory.async"),
                    MemoryProviderSelectionReason.ExplicitProvider,
                    MemoryCapabilityIds.ContextQueryAsync),
                OperationRecord: null,
                Output: null,
                accepted,
                FeedbackHandle: null,
                DriverDispatchAttempted: true,
                Diagnostic: "Context query accepted.");
        }

        public static MemoryOperationHandlerResult<MemoryContextPack> NoProviderQuery()
        {
            return new MemoryOperationHandlerResult<MemoryContextPack>(
                MemoryOperationHandlerStatus.NoProviderConfigured,
                MemoryProviderSelectionResult.Rejected(
                    MemoryProviderSelectionStatus.NoProviderConfigured,
                    MemoryProviderSelectionReason.None,
                    MemoryCapabilityIds.ContextQuerySync,
                    "No memory provider configured.",
                    []),
                OperationRecord: null,
                Output: null,
                AcceptedOperation: null,
                FeedbackHandle: null,
                DriverDispatchAttempted: false,
                Diagnostic: "No memory provider configured.");
        }

        public Task<MemoryOperationHandlerResult<MemoryContextPack>> ExecuteQueryAsync(
            MemoryOperationHandlerRequest<MemoryContextQueryRequest> request,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            QueryRequests.Add(request);
            return Task.FromResult(QueryResult);
        }

        public Task<MemoryOperationHandlerResult<MemorySourceCaptureOperationResult>> CaptureSourceForIngestionAsync(
            MemoryOperationHandlerRequest<MemorySourceCaptureOperationRequest> request,
            CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task<MemoryOperationHandlerResult<MemoryFeedbackRecord>> SubmitFeedbackAsync(
            MemoryOperationHandlerRequest<MemoryFeedbackOperationRequest> request,
            CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task<MemoryOperationHandlerResult<MemoryOperationRecord>> GetStatusAsync(
            MemoryOperationHandlerRequest<MemoryOperationStatusRequest> request,
            CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task<MemoryOperationHandlerResult<MemoryOperationRecord>> CancelAsync(
            MemoryOperationHandlerRequest<MemoryOperationCancellationRequest> request,
            CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task<MemoryOperationHandlerResult<MemoryEventOutboxRecord>> AcknowledgeEventAsync(
            MemoryOperationHandlerRequest<MemoryEventAcknowledgeRequest> request,
            CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        private static MemoryProviderProfile CreateProvider(string providerId)
        {
            return new MemoryProviderProfile(
                MemoryProviderInstanceId.Parse(providerId),
                providerId,
                MemoryProviderDriverKind.Mock,
                IsEnabled: true,
                MemoryProviderHealthState.Healthy,
                MemoryProviderWorkspaceScope.AllWorkspaces,
                SelectionTags: [],
                MemoryProviderProfilePolicy.Default,
                new MemoryProviderManifest(
                    MemoryProviderKind.Parse("mock.memory"),
                    MemoryProtocolVersion.Current,
                    [
                        new MemoryCapabilityDescriptor(MemoryCapabilityIds.ContextQuerySync, "v1", Supported: true),
                        new MemoryCapabilityDescriptor(MemoryCapabilityIds.ContextQueryAsync, "v1", Supported: true)
                    ],
                    MemoryProviderInteractionSupport.SyncQueryOnly,
                    UiSurfaces: [],
                    MemoryProviderLimits.Default,
                    MemoryExtensionData.Empty));
        }
    }
}
