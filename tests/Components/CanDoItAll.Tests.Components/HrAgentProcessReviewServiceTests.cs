using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.Infrastructure.ControlPlane;
using CanDoItAll.Infrastructure.Storage;
using CanDoItAll.Modules.AgentFramework;
using CanDoItAll.SharedKernel;
using CanDoItAll.Tests.Support;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;

using CanDoItAll.AgentFramework.Runtime.Abstractions;
namespace CanDoItAll.Tests.Components.CrmHr;

public sealed class HrAgentProcessReviewServiceTests
{
    private const string ReviewQuestion = "Was the persisted result complete?";
    private const string ReviewResponse = "Evidence was sufficient and the result was complete.";

    [Fact]
    public async Task Manager_review_is_tool_free_bounded_to_process_lineage_and_never_creates_a_chat_session()
    {
        await using var environment = CanDoItAllTestEnvironment.Create("hr-agent-manager-review");
        var profile = environment.CreateInMemoryProfile("primary");
        var configuration = TestApplicationBootstrap.BuildConfiguration(profile);
        var runtime = new CapturingManagerReviewRuntime();
        var services = new ServiceCollection();
        TestApplicationBootstrap.ConfigureDefaultServices(
            services,
            configuration,
            environment.CreateHostEnvironment("CanDoItAll.HrAgentProcessReviewServiceTests"));
        await using var serviceProvider = services.BuildServiceProvider(new ServiceProviderOptions
        {
            ValidateOnBuild = true,
            ValidateScopes = true
        });
        await using var scope = serviceProvider.CreateAsyncScope();
        var workspaceStore = scope.ServiceProvider.GetRequiredService<ISandboxWorkspaceStore>();
        var profileId = scope.ServiceProvider
            .GetRequiredService<IDatabaseProfileRuntimeAccessor>()
            .ResolveCurrentProfile()
            .Profile
            .Id;
        // SB18: the workspace service consumes the narrow runtime ports; the test-only adapter
        // adapts the capturing fake runtime for all four ports.
        var portFacade = new FakeAgentRuntimePortAdapter(runtime);
        var workspace = new AgentFrameworkWorkspaceService(
            workspaceStore,
            new UnusedAgentPackageService(),
            portFacade,
            portFacade,
            portFacade,
            portFacade,
            scope.ServiceProvider.GetRequiredService<ICapabilityProofService>(),
            logger: NullLogger<AgentFrameworkWorkspaceService>.Instance,
            activityCoordinator: scope.ServiceProvider
                .GetRequiredService<IAgentExecutionActivityCoordinator>(),
            activityWorkspaceIdentity:
                new AgentExecutionActivityWorkspaceIdentity(
                    profileId,
                    WorkspaceScopeDescriptor.Organization(
                        profileId.ToString("N")),
                    scope.ServiceProvider
                        .GetRequiredService<IAgentExecutionProfileGenerationSource>()
                        .GetGeneration()),
            executionPreparationCache: scope.ServiceProvider
                .GetRequiredService<IAgentExecutionPreparationCache>(),
            executionProfileGenerationSource: scope.ServiceProvider
                .GetRequiredService<IAgentExecutionProfileGenerationSource>(),
            workspaceProcessLeaseCleaner: scope.ServiceProvider
                .GetRequiredService<IWorkspaceExecutionRunProcessLeaseCleaner>(),
            externalTargetPathRegistryFactory: scope.ServiceProvider
                .GetRequiredService<IExternalTargetPathRegistryFactory>());
        var provider = Assert.Single(
            await workspace.ListProvidersAsync(),
            item => item.IsEnabled &&
                    item.Purpose == ProviderProfilePurpose.Chat &&
                    string.Equals(item.Name, "OpenAI default", StringComparison.Ordinal));
        var administration = new HrAgentAdministrationService(
            workspace,
            scope.ServiceProvider.GetRequiredService<IExternalTargetPathRegistry>(),
            NullLogger<HrAgentAdministrationService>.Instance);
        var target = await CreateAgentAsync(
            administration,
            workspace,
            provider,
            "Review target",
            canObserveOtherAgents: false);
        var manager = await CreateAgentAsync(
            administration,
            workspace,
            provider,
            "Review manager",
            canObserveOtherAgents: true);
        var managerWithoutProvider = await CreateAgentAsync(
            administration,
            workspace,
            provider: null,
            name: "Unconfigured manager",
            canObserveOtherAgents: true);
        var processRunId = Guid.NewGuid();
        var executionStore = scope.ServiceProvider.GetRequiredService<ISandboxWorkspaceExecutionStore>();
        await executionStore.UpdateExecutionAsync(state => state with
        {
            ExecutionRuns = state.ExecutionRuns.Concat(
            [
                CreateProcessRun(target.Id, processRunId, "target-step"),
                CreateProcessRun(manager.Id, processRunId, "manager-step"),
                CreateProcessRun(managerWithoutProvider.Id, processRunId, "unconfigured-manager-step")
            ]).ToArray()
        });
        var service = new HrAgentProcessReviewService(
            executionStore,
            scope.ServiceProvider.GetRequiredService<ISandboxWorkspaceExecutionRunStore>(),
            workspace,
            NullLogger<HrAgentProcessReviewService>.Instance);

        var history = await service.GetHistoryAsync(
            new HrAgentProcessHistoryInput(target.Id),
            CancellationToken.None);
        var process = Assert.Single(history.ProcessRuns);
        var eligibleManager = Assert.Single(
            process.Participants,
            participant => participant.AgentId == manager.Id);
        var unconfiguredManager = Assert.Single(
            process.Participants,
            participant => participant.AgentId == managerWithoutProvider.Id);

        Assert.True(eligibleManager.EligibleReviewManager);
        Assert.Equal(HrAgentTextTrust.UntrustedAgentCatalogData, eligibleManager.TextTrust);
        Assert.False(unconfiguredManager.EligibleReviewManager);
        Assert.Equal(HrAgentTextTrust.UntrustedAgentCatalogData, history.TextTrust);
        var result = await service.RequestManagerReviewAsync(
            HrAgentIdentity.AgentId,
            new HrAgentManagerReviewRequestInput(
                processRunId,
                target.Id,
                manager.Id,
                ReviewQuestion),
            CancellationToken.None);

        Assert.Null(result.ChatSessionId);
        Assert.Equal(ReviewResponse, result.ManagerResponse);
        Assert.Empty(await workspace.ListChatSessionsAsync(manager.Id));
        Assert.NotNull(runtime.LastExecutionOptions?.ContextIntent);
        Assert.False(runtime.LastExecutionOptions.ContextIntent.RuntimeToolProvidersEnabled);
        Assert.False(runtime.LastExecutionOptions.ContextIntent.WorkspaceToolsEnabled);
        Assert.False(runtime.LastExecutionOptions.ContextIntent.ToolCapabilitiesEnabled);
        Assert.Empty(runtime.LastMemory);

        var detail = await workspace.GetExecutionRunDetailAsync(result.ExecutionRunId);
        Assert.Equal(HrAgentExecutionLineage.ManagerReviewSourceKind, detail.Run.SourceKind);
        Assert.Equal(processRunId.ToString("D"), detail.Run.ProcessRunId);
        Assert.Equal(string.Empty, detail.Run.ProcessStepId);
        Assert.Equal(ExecutionState.Completed, detail.Run.State);
        Assert.Equal(RunOutcome.Succeeded, detail.Run.Outcome);
        Assert.Empty(detail.Run.PendingApprovals);
        Assert.DoesNotContain(ReviewQuestion, detail.Run.InputSummary, StringComparison.Ordinal);
        Assert.DoesNotContain(ReviewResponse, detail.Run.ResultSummary, StringComparison.Ordinal);
        Assert.Null(detail.Run.SerializedSessionStateJson);
        Assert.All(detail.ExecutionLog, entry =>
        {
            Assert.DoesNotContain(ReviewQuestion, entry.Message, StringComparison.Ordinal);
            Assert.DoesNotContain(ReviewResponse, entry.Message, StringComparison.Ordinal);
        });
        var managerHistory = await service.GetHistoryAsync(
            new HrAgentProcessHistoryInput(manager.Id),
            CancellationToken.None);
        Assert.Equal(1, Assert.Single(managerHistory.ProcessRuns).AttemptCount);

        await Assert.ThrowsAsync<InvalidOperationException>(() => service.RequestManagerReviewAsync(
            HrAgentIdentity.AgentId,
            new HrAgentManagerReviewRequestInput(
                processRunId,
                Guid.NewGuid(),
                manager.Id,
                "Review a nonparticipant."),
            CancellationToken.None));
        await Assert.ThrowsAsync<InvalidOperationException>(() => service.RequestManagerReviewAsync(
            HrAgentIdentity.AgentId,
            new HrAgentManagerReviewRequestInput(
                processRunId,
                target.Id,
                managerWithoutProvider.Id,
                "Review without a provider."),
            CancellationToken.None));

        runtime.PendingApprovals =
        [
            new PendingToolApprovalRecord(
                "unexpected-approval",
                "unexpected-call",
                "unexpected_tool",
                "function",
                "manager-pending-details-secret",
                "{\"question\":\"manager-pending-arguments-secret\"}")
        ];
        await Assert.ThrowsAsync<InvalidOperationException>(() => service.RequestManagerReviewAsync(
            HrAgentIdentity.AgentId,
            new HrAgentManagerReviewRequestInput(
                processRunId,
                target.Id,
                manager.Id,
                "Attempt a tool call."),
            CancellationToken.None));
        var pendingDetail = await workspace.GetExecutionRunDetailAsync(runtime.LastExecutionRunId!.Value);
        var protectedPendingApproval = Assert.Single(pendingDetail.Run.PendingApprovals);
        Assert.Equal(
            HrAgentExecutionRetention.ManagerReviewApprovalDetails,
            protectedPendingApproval.Details);
        Assert.Equal(
            HrAgentExecutionRetention.ManagerReviewApprovalArgumentsJson,
            protectedPendingApproval.ArgumentsJson);
        Assert.DoesNotContain("manager-pending-details-secret", protectedPendingApproval.Details, StringComparison.Ordinal);
        Assert.DoesNotContain("manager-pending-arguments-secret", protectedPendingApproval.ArgumentsJson, StringComparison.Ordinal);
        Assert.DoesNotContain("Attempt a tool call.", pendingDetail.Run.InputSummary, StringComparison.Ordinal);

        runtime.PendingApprovals = [];
        runtime.ExceptionToThrow = new InvalidOperationException("Synthetic manager runtime failure.");
        await Assert.ThrowsAnyAsync<Exception>(() => service.RequestManagerReviewAsync(
            HrAgentIdentity.AgentId,
            new HrAgentManagerReviewRequestInput(
                processRunId,
                target.Id,
                manager.Id,
                "Exercise failed execution handling."),
            CancellationToken.None));
        var failedDetail = await workspace.GetExecutionRunDetailAsync(runtime.LastExecutionRunId!.Value);
        Assert.Equal(ExecutionState.Failed, failedDetail.Run.State);
        Assert.Equal(RunOutcome.Failed, failedDetail.Run.Outcome);
        Assert.DoesNotContain("Exercise failed execution handling.", failedDetail.Run.InputSummary, StringComparison.Ordinal);
        Assert.Empty(await workspace.ListChatSessionsAsync(manager.Id));
    }

    private static async Task<AgentDefinition> CreateAgentAsync(
        HrAgentAdministrationService administration,
        IAgentFrameworkWorkspaceService workspace,
        ProviderProfile? provider,
        string name,
        bool canObserveOtherAgents)
    {
        var created = await administration.CreateAsync(
            HrAgentIdentity.AgentId,
            new HrAgentCreateInput(
                name,
                "Process participant",
                "Participates in a manager-review test.",
                "Review supplied evidence and return a concise assessment.",
                provider?.Id,
                provider?.DefaultModel ?? string.Empty,
                Permissions: new HrAgentPermissionsInput(
                    CanObserveOtherAgents: canObserveOtherAgents)),
            CancellationToken.None);
        var draft = Assert.Single(
            await workspace.ListAgentsAsync(includeTemplates: true),
            agent => agent.Id == created.AgentId);
        await administration.UpdateAsync(
            HrAgentIdentity.AgentId,
            new HrAgentSettingsUpdateInput(
                draft.Id,
                draft.UpdatedAtUtc,
                Status: AgentLifecycleStatus.Active),
            CancellationToken.None);
        return Assert.Single(
            await workspace.ListAgentsAsync(includeTemplates: true),
            agent => agent.Id == draft.Id);
    }

    private static ExecutionRunRecord CreateProcessRun(
        Guid agentId,
        Guid processRunId,
        string processStepId)
    {
        var now = DateTimeOffset.UtcNow;
        return new ExecutionRunRecord(
            Guid.NewGuid(),
            agentId,
            null,
            "Process evidence",
            HrAgentExecutionLineage.ProcessStepSourceKind,
            processStepId,
            processRunId.ToString("D"),
            string.Empty,
            "process-runtime",
            "system",
            "{}",
            string.Empty,
            string.Empty,
            "OpenAI default",
            "gpt-5-mini",
            ExecutionState.Completed,
            RunOutcome.Succeeded,
            now,
            now,
            now,
            now,
            string.Empty,
            null,
            [],
            ProcessRunId: processRunId.ToString("D"),
            ProcessStepId: processStepId);
    }

    private sealed class CapturingManagerReviewRuntime : IFakeAgentRuntime
    {
        public AgentRuntimeExecutionOptions? LastExecutionOptions { get; private set; }

        public Guid? LastExecutionRunId { get; private set; }

        public IReadOnlyList<AgentMemoryRecord> LastMemory { get; private set; } = [];

        public IReadOnlyList<PendingToolApprovalRecord> PendingApprovals { get; set; } = [];

        public Exception? ExceptionToThrow { get; set; }

        public Task<AgentRuntimeResponse> RunAsync(
            AgentDefinition agent,
            ProviderProfile provider,
            ChatSessionRecord session,
            IReadOnlyList<CapabilityCatalogItem> capabilities,
            IReadOnlyList<AgentMemoryRecord> memory,
            string prompt,
            string? runtimeSessionKey,
            Func<ExecutionState, string, string, Task> progressCallback,
            CancellationToken cancellationToken = default,
            bool suppressApprovalRequirements = false,
            AgentStructuredOutputContract? structuredOutput = null,
            AgentRuntimeExecutionOptions? executionOptions = null)
        {
            cancellationToken.ThrowIfCancellationRequested();
            LastExecutionRunId = session.LatestExecutionRunId;
            LastExecutionOptions = executionOptions;
            LastMemory = memory;
            if (ExceptionToThrow is not null)
            {
                throw ExceptionToThrow;
            }

            return Task.FromResult(new AgentRuntimeResponse(
                ReviewResponse,
                InputTokens: 12,
                OutputTokens: 8,
                ToolCalls: 0,
                RuntimeSessionKey: string.Empty,
                SerializedSessionStateJson: null,
                PendingApprovals));
        }

        public Task<AgentRuntimeResponse> RespondToPendingApprovalsAsync(
            AgentDefinition agent,
            ProviderProfile provider,
            ChatSessionRecord session,
            IReadOnlyList<CapabilityCatalogItem> capabilities,
            IReadOnlyList<AgentMemoryRecord> memory,
            bool approved,
            string? runtimeSessionKey,
            Func<ExecutionState, string, string, Task> progressCallback,
            CancellationToken cancellationToken = default,
            bool suppressApprovalRequirements = false,
            AgentStructuredOutputContract? structuredOutput = null,
            AgentRuntimeExecutionOptions? executionOptions = null)
            => throw new NotSupportedException();

        public Task<ProviderHealthResult> TestProviderAsync(
            ProviderProfile provider,
            CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task<ProviderTestChatResult> RunProviderTestChatAsync(
            ProviderProfile provider,
            ProviderTestChatRequest request,
            CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task<ProviderModelMaintenanceEditorResult> CreateOrUpdateProviderModelAsync(
            ProviderProfile provider,
            ProviderModelMaintenanceEditorRequest request,
            CancellationToken cancellationToken = default)
            => throw new NotSupportedException();
    }

    private sealed class UnusedAgentPackageService : IAgentPackageService
    {
        public Task<AgentExportResult> ExportAsync(
            SandboxWorkspaceDocument document,
            AgentDefinition agent,
            CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task<AgentImportResult> ImportAsync(
            string packagePath,
            CancellationToken cancellationToken = default)
            => throw new NotSupportedException();
    }
}
