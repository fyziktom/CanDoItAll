using System.Reflection;
using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Maf;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.Modules.CognitiveMemory;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;

namespace CanDoItAll.Tests.Unit;

public sealed class AgentContextContributionTests
{
    [Fact]
    public void Contributor_id_rejects_empty_values()
    {
        Assert.Throws<ArgumentException>(() => new AgentContextContributorId(" "));
    }

    [Fact]
    public async Task Maf_provider_converts_successful_contribution_to_chat_messages()
    {
        var contributor = new TestContextContributor(
            "test.context",
            10,
            _ => AgentContextContributionResult.Provided(
            [
                new AgentContextMessage(AgentContextMessageRole.System, "Injected context")
            ],
            new Dictionary<string, string>
            {
                ["source"] = "unit-test"
            }));
        var traceCollector = new AgentContextContributionTraceCollector();
        var provider = CreateProvider(contributor, traceCollector);

        var messages = await provider.ContributeAsync(
        [
            new ChatMessage(ChatRole.User, "Prompt")
        ]);

        var message = Assert.Single(messages);
        Assert.Equal(ChatRole.System, message.Role);
        Assert.Equal("Injected context", message.Text);

        var trace = Assert.Single(traceCollector.Snapshot());
        Assert.Equal(new AgentContextContributorId("test.context"), trace.ContributorId);
        Assert.Equal(AgentContextContributionStatus.Provided, trace.Status);
        Assert.Equal(1, trace.GeneratedMessageCount);
        Assert.Equal("unit-test", trace.TraceMetadata["source"]);
        Assert.True(trace.Elapsed > TimeSpan.Zero);
    }

    [Fact]
    public async Task Maf_provider_records_skipped_contribution_trace()
    {
        var contributor = new TestContextContributor(
            "test.skipped",
            10,
            _ => AgentContextContributionResult.Skipped(new Dictionary<string, string>
            {
                ["reason"] = "not-applicable"
            }));
        var traceCollector = new AgentContextContributionTraceCollector();
        var provider = CreateProvider(contributor, traceCollector);

        var messages = await provider.ContributeAsync([]);

        Assert.Empty(messages);
        var trace = Assert.Single(traceCollector.Snapshot());
        Assert.Equal(new AgentContextContributorId("test.skipped"), trace.ContributorId);
        Assert.Equal(AgentContextContributionStatus.Skipped, trace.Status);
        Assert.Equal(0, trace.GeneratedMessageCount);
        Assert.Equal("not-applicable", trace.TraceMetadata["reason"]);
    }

    [Fact]
    public async Task Maf_provider_surfaces_failed_result_as_typed_exception()
    {
        var contributor = new TestContextContributor(
            "test.failure",
            10,
            _ => AgentContextContributionResult.Failed("Policy denied context."));
        var traceCollector = new AgentContextContributionTraceCollector();
        var provider = CreateProvider(contributor, traceCollector);

        var exception = await Assert.ThrowsAsync<AgentContextContributionException>(async () =>
            await provider.ContributeAsync([]));

        Assert.Equal(new AgentContextContributorId("test.failure"), exception.ContributorId);
        Assert.Contains("Policy denied context", exception.Message, StringComparison.Ordinal);

        var trace = Assert.Single(traceCollector.Snapshot());
        Assert.Equal(new AgentContextContributorId("test.failure"), trace.ContributorId);
        Assert.Equal(AgentContextContributionStatus.Failed, trace.Status);
        Assert.Equal("Policy denied context.", trace.FailureMessage);
    }

    [Fact]
    public async Task Maf_provider_honors_cancellation()
    {
        var contributor = new TestContextContributor(
            "test.cancellation",
            10,
            request =>
            {
                _ = request;
                return AgentContextContributionResult.Provided([]);
        });
        using var cancellation = new CancellationTokenSource();
        await cancellation.CancelAsync();
        contributor.CancellationProbe = token => token.ThrowIfCancellationRequested();
        var traceCollector = new AgentContextContributionTraceCollector();
        var provider = CreateProvider(contributor, traceCollector);

        await Assert.ThrowsAsync<OperationCanceledException>(async () =>
            await provider.ContributeAsync([], cancellation.Token));
        Assert.Empty(traceCollector.Snapshot());
    }

    [Fact]
    public async Task Maf_runtime_attaches_enabled_contributors_in_deterministic_order()
    {
        var services = new ServiceCollection();
        services.AddSingleton<IAgentContextContributor>(new TestContextContributor("late", 20, _ => AgentContextContributionResult.Skipped()));
        services.AddSingleton<IAgentContextContributor>(new TestContextContributor("disabled", 0, _ => AgentContextContributionResult.Skipped(), enabled: false));
        services.AddSingleton<IAgentContextContributor>(new TestContextContributor("early", 10, _ => AgentContextContributionResult.Skipped()));
        var runtime = RuntimeCapabilityComposer.CreateDefault(Path.GetTempPath(), services.BuildServiceProvider());
        var progressMessages = new List<string>();

        var state = await InvokeCreateCapabilityStateAsync(
            runtime,
            CreateAgent(),
            CreateProviderProfile(),
            progressMessages);

        var contextProviders = Assert.IsAssignableFrom<IEnumerable<AIContextProvider>>(
            state.GetType().GetProperty("ContextProviders", BindingFlags.Public | BindingFlags.Instance)?.GetValue(state));
        var contributorIds = contextProviders
            .OfType<MafAgentContextContributionProvider>()
            .Select(provider => provider.ContributorId.Value)
            .ToList();

        Assert.Equal(["early", "late"], contributorIds);
        Assert.Contains(
            progressMessages,
            message => message.Contains("registered agent context contributor", StringComparison.Ordinal));

        var traceCollector = ReadContextContributionTraceCollector(state);
        var firstProvider = contextProviders
            .OfType<MafAgentContextContributionProvider>()
            .First(provider => provider.ContributorId.Value == "early");
        var messages = await firstProvider.ContributeAsync([]);

        Assert.Empty(messages);
        var trace = Assert.Single(traceCollector.Snapshot());
        Assert.Equal(new AgentContextContributorId("early"), trace.ContributorId);
        Assert.Equal(AgentContextContributionStatus.Skipped, trace.Status);
    }

    [Fact]
    public async Task Maf_runtime_uses_context_workspace_scope_override_for_contributors()
    {
        var projectId = Guid.Parse("29fbb9a8-8422-4b8b-89ed-9d515103b801");
        WorkspaceScopeDescriptor? capturedScope = null;
        var services = new ServiceCollection();
        services.AddSingleton<IAgentContextContributor>(new TestContextContributor(
            "scope.capture",
            10,
            request =>
            {
                capturedScope = request.Policy.WorkspaceScope;
                return AgentContextContributionResult.Skipped();
            }));
        var runtime = RuntimeCapabilityComposer.CreateDefault(
            Path.GetTempPath(),
            services.BuildServiceProvider(),
            WorkspaceScopeDescriptor.Organization("unit-org"));
        var state = await InvokeCreateCapabilityStateCoreAsync(
            runtime,
            CreateAgent(),
            CreateProviderProfile(),
            WorkspaceScopeDescriptor.Project(projectId.ToString("D")),
            []);
        var contextProviders = Assert.IsAssignableFrom<IEnumerable<AIContextProvider>>(
            state.GetType().GetProperty("ContextProviders", BindingFlags.Public | BindingFlags.Instance)?.GetValue(state));
        var provider = Assert.Single(contextProviders.OfType<MafAgentContextContributionProvider>());

        await provider.ContributeAsync([new ChatMessage(ChatRole.User, "Summarize workflow input.")]);

        Assert.NotNull(capturedScope);
        Assert.Equal(WorkspaceScopeKind.Project, capturedScope!.Kind);
        Assert.Equal(projectId.ToString("D"), capturedScope.Key);
    }

    [Fact]
    public async Task Maf_runtime_rejects_duplicate_contributor_ids()
    {
        var services = new ServiceCollection();
        services.AddSingleton<IAgentContextContributor>(new TestContextContributor("duplicate", 10, _ => AgentContextContributionResult.Skipped()));
        services.AddSingleton<IAgentContextContributor>(new TestContextContributor("duplicate", 20, _ => AgentContextContributionResult.Skipped()));
        var runtime = RuntimeCapabilityComposer.CreateDefault(Path.GetTempPath(), services.BuildServiceProvider());

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(async () =>
            await InvokeCreateCapabilityStateAsync(
                runtime,
                CreateAgent(),
                CreateProviderProfile(),
                []));

        Assert.Contains("must be unique", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Cognitive_memory_contributor_accepts_explicit_project_marker_for_chat_scope()
    {
        var projectId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
        var orchestrator = new RecordingRecallOrchestrator(projectId);
        var contributor = new CognitiveMemoryAgentContextContributor(orchestrator, CreateSettingsService());

        var result = await contributor.ContributeAsync(new AgentContextContributionRequest(
            CreateAgent(),
            CreateProviderProfile(),
            [
                new AgentContextRequestMessage(
                    AgentContextMessageRole.User,
                    $"CognitiveMemoryProjectId: {projectId:D}\nWhich ClinicFlow instruction should be remembered?")
            ],
            new AgentContextContributionPolicy(
                AgentContextExecutionMode.InteractiveChat,
                SuppressApprovalRequirements: false,
                WorkspaceScopeDescriptor.Organization("unit"))));

        Assert.Equal(AgentContextContributionStatus.Provided, result.Status);
        Assert.Equal(projectId, orchestrator.LastRequest?.ProjectId);
        Assert.Equal("Which ClinicFlow instruction should be remembered?", orchestrator.LastRequest?.Query);
        Assert.Contains("Cognitive Memory context pack", Assert.Single(result.Messages).Text, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Cognitive_memory_contributor_strips_chat_prompt_controls_before_recall()
    {
        var projectId = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb");
        var orchestrator = new RecordingRecallOrchestrator(projectId);
        var contributor = new CognitiveMemoryAgentContextContributor(orchestrator, CreateSettingsService());

        var result = await contributor.ContributeAsync(new AgentContextContributionRequest(
            CreateAgent(),
            CreateProviderProfile(),
            [
                new AgentContextRequestMessage(
                    AgentContextMessageRole.User,
                    $"""
                    CognitiveMemoryProjectId: {projectId:D}

                    Answer using CanDoItAll Cognitive Memory context only.
                    If no memory context is available, answer with exactly: NO_MEMORY_CONTEXT.
                    Return concise JSON with keys: answer, sourceFilename, confidence.

                    Project key: clinicflow-saas
                    Question: Which ClinicFlow instruction should be remembered for future product positioning, and what phrase must not be overgeneralized?
                    """)
            ],
            new AgentContextContributionPolicy(
                AgentContextExecutionMode.InteractiveChat,
                SuppressApprovalRequirements: false,
                WorkspaceScopeDescriptor.Organization("unit"))));

        var context = Assert.Single(result.Messages).Text;
        Assert.Equal(AgentContextContributionStatus.Provided, result.Status);
        Assert.Equal(
            "Which ClinicFlow instruction should be remembered for future product positioning, and what phrase must not be overgeneralized?",
            orchestrator.LastRequest?.Query);
        Assert.Contains("clinicflow-saas-s04.md#section-02-email-2-instruction", context, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Cognitive_memory_contributor_skips_before_project_scope_when_runtime_usage_is_disabled()
    {
        var projectId = Guid.Parse("bcbcbcbc-bcbc-bcbc-bcbc-bcbcbcbcbcbc");
        var orchestrator = new RecordingRecallOrchestrator(projectId);
        var contributor = new CognitiveMemoryAgentContextContributor(orchestrator, CreateSettingsService(isEnabled: false));

        var result = await contributor.ContributeAsync(new AgentContextContributionRequest(
            CreateAgent(),
            CreateProviderProfile(),
            [new AgentContextRequestMessage(AgentContextMessageRole.User, "What should the process remember?")],
            new AgentContextContributionPolicy(
                AgentContextExecutionMode.GovernedProcessAutomation,
                SuppressApprovalRequirements: false,
                WorkspaceScopeDescriptor.Organization("unit"))));

        Assert.Equal(AgentContextContributionStatus.Skipped, result.Status);
        Assert.Equal(CognitiveMemoryRuntimeUsage.DisabledReason, result.TraceMetadata["reason"]);
        Assert.Empty(result.Messages);
        Assert.Null(orchestrator.LastRequest);
    }

    [Fact]
    public async Task Cognitive_memory_contributor_skips_remote_provider_when_local_only()
    {
        var projectId = Guid.Parse("cccccccc-cccc-cccc-cccc-cccccccccccc");
        var orchestrator = new RecordingRecallOrchestrator(projectId);
        var contributor = new CognitiveMemoryAgentContextContributor(
            orchestrator,
            CreateSettingsService(CognitiveMemoryModelAccessMode.LocalProvidersOnly));

        var result = await contributor.ContributeAsync(new AgentContextContributionRequest(
            CreateAgent(),
            CreateProviderProfile(ProviderKind.OpenAi, "https://api.openai.com/v1"),
            [
                new AgentContextRequestMessage(
                    AgentContextMessageRole.User,
                    $"CognitiveMemoryProjectId: {projectId:D}\nWhat does the project remember?")
            ],
            new AgentContextContributionPolicy(
                AgentContextExecutionMode.InteractiveChat,
                SuppressApprovalRequirements: false,
                WorkspaceScopeDescriptor.Organization("unit"))));

        Assert.Equal(AgentContextContributionStatus.Skipped, result.Status);
        Assert.Equal("provider-is-not-local", result.TraceMetadata["reason"]);
        Assert.Equal(CognitiveMemoryModelAccessMode.LocalProvidersOnly.ToString(), result.TraceMetadata["modelAccessMode"]);
        Assert.Null(orchestrator.LastRequest);
    }

    [Fact]
    public async Task Cognitive_memory_contributor_does_not_treat_remote_ollama_as_local()
    {
        var projectId = Guid.Parse("dddddddd-dddd-dddd-dddd-dddddddddddd");
        var orchestrator = new RecordingRecallOrchestrator(projectId);
        var contributor = new CognitiveMemoryAgentContextContributor(
            orchestrator,
            CreateSettingsService(CognitiveMemoryModelAccessMode.LocalProvidersOnly));

        var result = await contributor.ContributeAsync(new AgentContextContributionRequest(
            CreateAgent(),
            CreateProviderProfile(ProviderKind.Ollama, "http://192.168.10.132:11434"),
            [
                new AgentContextRequestMessage(
                    AgentContextMessageRole.User,
                    $"CognitiveMemoryProjectId: {projectId:D}\nWhat does the project remember?")
            ],
            new AgentContextContributionPolicy(
                AgentContextExecutionMode.InteractiveChat,
                SuppressApprovalRequirements: false,
                WorkspaceScopeDescriptor.Organization("unit"))));

        Assert.Equal(AgentContextContributionStatus.Skipped, result.Status);
        Assert.Equal("provider-is-not-local", result.TraceMetadata["reason"]);
        Assert.Equal("False", result.TraceMetadata["providerIsLocal"]);
        Assert.Null(orchestrator.LastRequest);
    }

    [Fact]
    public async Task Cognitive_memory_contributor_allows_loopback_provider_when_local_only()
    {
        var projectId = Guid.Parse("eeeeeeee-eeee-eeee-eeee-eeeeeeeeeeee");
        var orchestrator = new RecordingRecallOrchestrator(projectId);
        var contributor = new CognitiveMemoryAgentContextContributor(
            orchestrator,
            CreateSettingsService(CognitiveMemoryModelAccessMode.LocalProvidersOnly));

        var result = await contributor.ContributeAsync(new AgentContextContributionRequest(
            CreateAgent(),
            CreateProviderProfile(ProviderKind.OpenAi, "localhost:11434"),
            [
                new AgentContextRequestMessage(
                    AgentContextMessageRole.User,
                    $"CognitiveMemoryProjectId: {projectId:D}\nWhat does the project remember?")
            ],
            new AgentContextContributionPolicy(
                AgentContextExecutionMode.InteractiveChat,
                SuppressApprovalRequirements: false,
                WorkspaceScopeDescriptor.Organization("unit"))));

        Assert.Equal(AgentContextContributionStatus.Provided, result.Status);
        Assert.Equal(projectId, orchestrator.LastRequest?.ProjectId);
        Assert.Equal("True", result.TraceMetadata["providerIsLocal"]);
    }

    [Fact]
    public async Task Cognitive_memory_contributor_fails_process_automation_when_project_scope_is_missing()
    {
        var projectId = Guid.Parse("abababab-abab-abab-abab-abababababab");
        var orchestrator = new RecordingRecallOrchestrator(projectId);
        var contributor = new CognitiveMemoryAgentContextContributor(orchestrator, CreateSettingsService());

        var result = await contributor.ContributeAsync(new AgentContextContributionRequest(
            CreateAgent(),
            CreateProviderProfile(),
            [new AgentContextRequestMessage(AgentContextMessageRole.User, "What should the process remember?")],
            new AgentContextContributionPolicy(
                AgentContextExecutionMode.GovernedProcessAutomation,
                SuppressApprovalRequirements: false,
                WorkspaceScopeDescriptor.Organization("unit"))));

        Assert.Equal(AgentContextContributionStatus.Failed, result.Status);
        Assert.Equal("project-scope-not-provided", result.TraceMetadata["reason"]);
        Assert.Contains("project scope", result.FailureMessage, StringComparison.OrdinalIgnoreCase);
        Assert.Null(orchestrator.LastRequest);
    }

    [Fact]
    public async Task Cognitive_memory_contributor_fails_process_automation_when_required_memory_is_unavailable()
    {
        var projectId = Guid.Parse("cdcdcdcd-cdcd-cdcd-cdcd-cdcdcdcdcdcd");
        var contributor = new CognitiveMemoryAgentContextContributor(
            new ThrowingRecallOrchestrator(projectId),
            CreateSettingsService());

        var result = await contributor.ContributeAsync(new AgentContextContributionRequest(
            CreateAgent(),
            CreateProviderProfile(),
            [new AgentContextRequestMessage(AgentContextMessageRole.User, "What should the process remember?")],
            new AgentContextContributionPolicy(
                AgentContextExecutionMode.AutoApprovedNonInteractive,
                SuppressApprovalRequirements: false,
                WorkspaceScopeDescriptor.Project(projectId.ToString("D")))));

        Assert.Equal(AgentContextContributionStatus.Failed, result.Status);
        Assert.Equal("cognitive-memory-unavailable", result.TraceMetadata["reason"]);
        Assert.Contains("required memory outage", result.FailureMessage, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Cognitive_memory_contributor_skips_empty_context_pack_for_process_automation()
    {
        var projectId = Guid.Parse("edededed-eded-eded-eded-edededededed");
        var orchestrator = new EmptyRecallOrchestrator(projectId);
        var contributor = new CognitiveMemoryAgentContextContributor(orchestrator, CreateSettingsService());

        var result = await contributor.ContributeAsync(new AgentContextContributionRequest(
            CreateAgent(),
            CreateProviderProfile(),
            [new AgentContextRequestMessage(AgentContextMessageRole.User, "Summarize the workflow payload.")],
            new AgentContextContributionPolicy(
                AgentContextExecutionMode.AutoApprovedNonInteractive,
                SuppressApprovalRequirements: false,
                WorkspaceScopeDescriptor.Project(projectId.ToString("D")))));

        Assert.Equal(AgentContextContributionStatus.Skipped, result.Status);
        Assert.Equal("empty-context-pack", result.TraceMetadata["reason"]);
        Assert.NotNull(orchestrator.LastRequest);
        Assert.Equal(projectId, orchestrator.LastRequest!.ProjectId);
    }

    private static MafAgentContextContributionProvider CreateProvider(
        IAgentContextContributor contributor,
        IAgentContextContributionTraceSink? traceSink = null)
        => new(
            contributor,
            CreateAgent(),
            CreateProviderProfile(),
            new AgentContextContributionPolicy(
                AgentContextExecutionMode.InteractiveChat,
                SuppressApprovalRequirements: false,
                WorkspaceScopeDescriptor.Sandbox),
            traceSink);

    private static AgentDefinition CreateAgent()
        => new(
            Id: Guid.NewGuid(),
            Name: "Context Agent",
            RoleTitle: "Tester",
            Summary: "Tests context contribution.",
            Instructions: "Use supplied context.",
            Status: AgentLifecycleStatus.Active,
            ProviderProfileId: Guid.NewGuid(),
            Model: string.Empty,
            Workload: AgentWorkloadKind.Programming,
            ChatHistoryMode: AgentChatHistoryMode.FrameworkManaged,
            Temperature: 0,
            RequirePerServiceCallChatHistoryPersistence: false,
            EnableBackgroundResponses: false,
            ConfigurationJson: "{}",
            IsTemplate: false,
            TemplateKey: string.Empty,
            Permissions: AgentPermissionsPolicy.Default with
            {
                CanUseTools = false,
                CanAskOtherAgents = false,
                RequiresApprovalForExternalCalls = false
            },
            Capabilities: [],
            Tags: [],
            CreatedAtUtc: DateTimeOffset.UtcNow,
            UpdatedAtUtc: DateTimeOffset.UtcNow);

    private static ProviderProfile CreateProviderProfile(
        ProviderKind kind = ProviderKind.OpenAi,
        string baseUrl = "https://api.openai.com/v1",
        Guid? providerId = null)
        => new(
            providerId ?? Guid.NewGuid(),
            "Unit Provider",
            kind,
            baseUrl,
            "OPENAI_API_KEY",
            "gpt-4.1",
            ProviderTransportKind.ChatCompletions,
            true,
            true,
            true,
            false,
            true,
            "{}",
            string.Empty,
            "Not checked",
            null,
            []);

    private static ICognitiveMemoryAutomationSettingsService CreateSettingsService(
        CognitiveMemoryModelAccessMode modelAccessMode = CognitiveMemoryModelAccessMode.AnyEnabledProvider,
        Guid? defaultProviderProfileId = null,
        IReadOnlyList<Guid>? allowedProviderProfileIds = null,
        bool isEnabled = true)
        => new TestAutomationSettingsService(new CognitiveMemoryAutomationSettings(
            isEnabled,
            CognitiveMemoryAutomationScheduleMode.ManualOnly,
            "02:00",
            30,
            [],
            AutoIngestProjectStructure: true,
            AutoIngestProcessRuntime: true,
            AutoConsolidateAfterIngestion: true,
            modelAccessMode,
            defaultProviderProfileId,
            DefaultAgentId: null,
            allowedProviderProfileIds ?? [],
            UpdatedByActorId: "unit-test",
            UpdatedAtUtc: DateTimeOffset.UnixEpoch));

    private static async Task<object> InvokeCreateCapabilityStateAsync(
        RuntimeCapabilityComposer composer,
        AgentDefinition agent,
        ProviderProfile provider,
        List<string> progressMessages)
    {
        return await composer.CreateCapabilityStateAsync(
            agent,
            provider,
            Array.Empty<CapabilityCatalogItem>(),
            Array.Empty<AgentMemoryRecord>(),
            (_, _, message) =>
            {
                progressMessages.Add(message);
                return Task.CompletedTask;
            },
            CancellationToken.None,
            suppressApprovalRequirements: false);
    }

    private static async Task<object> InvokeCreateCapabilityStateCoreAsync(
        RuntimeCapabilityComposer composer,
        AgentDefinition agent,
        ProviderProfile provider,
        WorkspaceScopeDescriptor contextWorkspaceScope,
        List<string> progressMessages)
    {
        return await composer.CreateCapabilityStateCoreAsync(
            agent,
            provider,
            provider.DefaultModel,
            Array.Empty<CapabilityCatalogItem>(),
            Array.Empty<AgentMemoryRecord>(),
            (_, _, message) =>
            {
                progressMessages.Add(message);
                return Task.CompletedTask;
            },
            CancellationToken.None,
            suppressApprovalRequirements: false,
            contextWorkspaceScope,
            AgentRuntimeContextIntent.Empty);
    }

    private static AgentContextContributionTraceCollector ReadContextContributionTraceCollector(object state)
        => Assert.IsType<AgentContextContributionTraceCollector>(
            state.GetType().GetProperty("ContextContributionTraceCollector", BindingFlags.Public | BindingFlags.Instance)?.GetValue(state));

    private sealed class TestContextContributor(
        string id,
        int order,
        Func<AgentContextContributionRequest, AgentContextContributionResult> resultFactory,
        bool enabled = true) : IAgentContextContributor
    {
        public Action<CancellationToken>? CancellationProbe { get; set; }

        public AgentContextContributorDescriptor Descriptor { get; } = new(
            new AgentContextContributorId(id),
            id,
            order,
            enabled);

        public ValueTask<AgentContextContributionResult> ContributeAsync(
            AgentContextContributionRequest request,
            CancellationToken cancellationToken = default)
        {
            CancellationProbe?.Invoke(cancellationToken);
            return ValueTask.FromResult(resultFactory(request));
        }
    }

    private sealed class RecordingRecallOrchestrator(Guid expectedProjectId) : ICognitiveMemoryRecallOrchestrator
    {
        public CognitiveMemoryRecallRequest? LastRequest { get; private set; }

        public ValueTask<CognitiveMemoryRecallResult> RecallAsync(
            CognitiveMemoryRecallRequest request,
            CancellationToken cancellationToken = default)
        {
            Assert.Equal(expectedProjectId, request.ProjectId);
            LastRequest = request;
            var recordId = CognitiveMemoryRecordId.New();
            var sourceRef = new CognitiveMemoryRecallSourceRef(
                recordId,
                CognitiveMemorySourceItemId.New(),
                CognitiveMemoryEvidenceAnchorId.New(),
                "ExternalFile",
                "clinicflow-saas-s04.md#section-02-email-2-instruction",
                "Replace clinical prioritization wording with administrative waitlist ranking.",
                CognitiveMemoryAccessLevel.Project,
                CognitiveMemoryRedactionState.Safe,
                IncludedInContext: true,
                CognitiveMemoryRecallExclusionReasonKind.None);
            var section = new CognitiveMemoryRecallContextSection(
                new CognitiveMemorySectionId("unit-section"),
                CognitiveMemoryRecallContextSectionKind.SelectedMemory,
                "ClinicFlow instruction",
                "Use administrative waitlist ranking, not clinical prioritization automation.",
                [recordId],
                [],
                [sourceRef]);
            var contextPack = new CognitiveMemoryRecallContextPack(
                CognitiveMemoryRecallContextPackId.New(),
                expectedProjectId,
                WorkspaceFrameId: null,
                "Recall context for unit test",
                "Selected 1 source-backed memory candidate.",
                [section],
                [sourceRef],
                [],
                new Dictionary<string, string>());
            return ValueTask.FromResult(new CognitiveMemoryRecallResult(
                Guid.NewGuid(),
                contextPack,
                [],
                [],
                []));
        }
    }

    private sealed class ThrowingRecallOrchestrator(Guid expectedProjectId) : ICognitiveMemoryRecallOrchestrator
    {
        public ValueTask<CognitiveMemoryRecallResult> RecallAsync(
            CognitiveMemoryRecallRequest request,
            CancellationToken cancellationToken = default)
        {
            Assert.Equal(expectedProjectId, request.ProjectId);
            throw new InvalidOperationException("required memory outage");
        }
    }

    private sealed class EmptyRecallOrchestrator(Guid expectedProjectId) : ICognitiveMemoryRecallOrchestrator
    {
        public CognitiveMemoryRecallRequest? LastRequest { get; private set; }

        public ValueTask<CognitiveMemoryRecallResult> RecallAsync(
            CognitiveMemoryRecallRequest request,
            CancellationToken cancellationToken = default)
        {
            Assert.Equal(expectedProjectId, request.ProjectId);
            LastRequest = request;
            var contextPack = new CognitiveMemoryRecallContextPack(
                CognitiveMemoryRecallContextPackId.New(),
                expectedProjectId,
                WorkspaceFrameId: null,
                "Recall context for empty project",
                "No matching memory was available.",
                [],
                [],
                [],
                new Dictionary<string, string>());
            return ValueTask.FromResult(new CognitiveMemoryRecallResult(
                Guid.NewGuid(),
                contextPack,
                [],
                [],
                []));
        }
    }

    private sealed class TestAutomationSettingsService(CognitiveMemoryAutomationSettings settings) : ICognitiveMemoryAutomationSettingsService
    {
        public ValueTask<CognitiveMemoryAutomationSettings> GetAsync(CancellationToken cancellationToken = default)
            => ValueTask.FromResult(settings);

        public ValueTask<CognitiveMemoryAutomationSettings> SaveAsync(
            CognitiveMemoryAutomationSettingsUpdate update,
            CancellationToken cancellationToken = default)
            => throw new NotSupportedException("Unit tests only read cognitive memory automation settings.");
    }
}
