using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.Modules.AgentFramework;
using CanDoItAll.Modules.AgentFramework.Hosting;
using CanDoItAll.Modules.CrmHr;
using CanDoItAll.Modules.Processes;
using CanDoItAll.Processes.Abstractions;
using CanDoItAll.Processes.Application;
using CanDoItAll.Processes.Builder;
using CanDoItAll.Processes.Core;
using CanDoItAll.Processes.Drivers.Abstractions;
using CanDoItAll.Processes.Templates;
using CanDoItAll.SharedKernel;
using Microsoft.Extensions.Options;

namespace CanDoItAll.Tests.Unit;

public sealed class ProcessLaunchExecutorResolverTests
{
    private static readonly DateTimeOffset Now = new(2026, 6, 17, 9, 30, 0, TimeSpan.Zero);
    private static readonly ProcessInstancePlanId PlanId = new(new Guid("04991f6e-a37d-45f7-9a65-362c2cb4fef4"));
    private static readonly ProcessStepInstanceId StepId = new(new Guid("f0fcfb5c-4b35-475a-a9a7-bfe3c7bc270d"));
    private static readonly ProcessStrategyBindingSnapshot Binding = new(
        new DriverId("driver.test"),
        new StrategyId("strategy.test"),
        "1.0.0",
        "factory.1.0.0",
        "runtime.1",
        "runtime.1",
        "sha256:binding",
        []);

    [Fact]
    public async Task ResolveAsync_binds_process_local_role_through_shared_role_resource_key()
    {
        var providerId = Guid.Parse("0a7675bb-d7f5-46d0-8c7f-40fdf22df893");
        var agent = CreateAgent(providerId);
        var workspace = new ResolverWorkspaceService([agent], [CreateProvider(providerId)]);
        var workspaceFactory = new ResolverWorkspaceFactory(workspace);
        var resolver = new AgentFrameworkProcessLaunchExecutorResolver(
            workspaceFactory,
            new ProcessMockAgentCatalogService(
                workspaceFactory,
                new NoOpAiTechnicalAgentBridge(),
                Options.Create(new ProcessMockAgentOptions { Enabled = false })),
            new ProviderProfileService());

        var result = await resolver.ResolveAsync(new ProcessLaunchExecutorResolutionRequest(
            CreateDefinition(),
            CreatePlan(),
            LiveRunProfile: null,
            Variables: new Dictionary<string, string>()));

        Assert.DoesNotContain(result.Findings, finding => finding.Severity == ProcessLaunchReadinessSeverity.Error);
        var binding = Assert.Single(result.Bindings);
        Assert.Equal("record-runtime-commands", binding.StepKey);
        Assert.Equal("runtime-command-recorder", binding.RoleKey);
        Assert.Equal(ProcessLaunchExecutorKinds.Agent, binding.ExecutorKind);
        Assert.Equal(agent.Id.ToString("D"), binding.ExecutorId);
        Assert.Contains("delivery-manager", binding.AssignmentReason, StringComparison.OrdinalIgnoreCase);
    }

    private static ProcessTemplateDefinitionDocument CreateDefinition()
    {
        return new ProcessTemplateDefinitionDocument
        {
            Key = "dotnet-runtime-command-writeback",
            RoleUsages =
            [
                new ProcessTemplateDefinitionRoleUsageDocument
                {
                    Key = "runtime-command-recorder",
                    RoleResourceKey = "delivery-manager",
                    DisplayName = "Runtime command recorder",
                    PreferredExecutorKind = ProcessLaunchExecutorKinds.Agent,
                    PreferredProjectAssignmentRole = "Manager",
                    IsRequired = true
                }
            ],
            Steps =
            [
                new ProcessTemplateDefinitionStepDocument
                {
                    Key = "record-runtime-commands",
                    StepKind = "Activity",
                    RoleAssignments =
                    [
                        new ProcessTemplateDefinitionStepRoleAssignmentDocument
                        {
                            RoleKey = "runtime-command-recorder",
                            ResponsibilityKind = "Responsible",
                            IsRequired = true
                        }
                    ]
                }
            ]
        };
    }

    private static ProcessInstancePlan CreatePlan()
    {
        return new ProcessInstancePlan(
            new ProcessInstancePlanHeader(PlanId, PlanId, null, null, "processes.instance-plan.v1", Now, 0),
            new ResolvedProcessDefinitionSnapshot(
                ProcessDefinitionId.New(),
                ProcessDefinitionVersionId.New(),
                "sha256:definition",
                "template/1",
                "template/1",
                [],
                [],
                []),
            new DriverStackSnapshot([]),
            new StrategyBindingSet([Binding], [], [], []),
            [new StepInstancePlan(StepId, ProcessStepDefinitionId.New(), "record-runtime-commands", ProcessStepKind.Activity, true, false, Binding)],
            new ArtifactPlan([], []),
            new BranchRouteTable([]),
            [],
            new ManagerPlan("sha256:manager", null, [], []),
            new BudgetPlan([]),
            new MonitoringPlan(true, "sha256:monitoring"),
            new SecurityPlan("sha256:security", []),
            "sha256:plan");
    }

    private static AgentDefinition CreateAgent(Guid providerId)
    {
        return new AgentDefinition(
            Id: Guid.Parse("e650d9a2-ea73-4e73-a0b1-1ac8f3d2a155"),
            Name: "Delivery Manager",
            RoleTitle: "Delivery Manager",
            Summary: "Coordinates delivery readiness and runtime command handoff.",
            Instructions: "Resolve delivery governance tasks.",
            Status: AgentLifecycleStatus.Active,
            ProviderProfileId: providerId,
            Model: string.Empty,
            Workload: AgentWorkloadKind.Management,
            ChatHistoryMode: AgentChatHistoryMode.ProviderDefault,
            Temperature: 0d,
            RequirePerServiceCallChatHistoryPersistence: false,
            EnableBackgroundResponses: false,
            ConfigurationJson: "{}",
            IsTemplate: false,
            TemplateKey: string.Empty,
            Permissions: AgentPermissionsPolicy.Default,
            Capabilities: [],
            Tags: ["delivery-manager"],
            CreatedAtUtc: Now,
            UpdatedAtUtc: Now);
    }

    private static ProviderProfile CreateProvider(Guid providerId)
    {
        return new ProviderProfile(
            Id: providerId,
            Name: "OpenAI default",
            Kind: ProviderKind.OpenAi,
            BaseUrl: "https://api.openai.com/v1",
            ApiKeyEnvironmentVariable: "OPENAI_API_KEY",
            DefaultModel: "gpt-5-mini",
            Transport: ProviderTransportKind.Responses,
            IsEnabled: true,
            SupportsStreaming: true,
            SupportsTools: true,
            PreferFrameworkManagedChatHistory: false,
            SupportsBackgroundResponses: true,
            ConfigurationJson: "{}",
            Notes: "Unit-test provider.",
            HealthStatus: "Not checked",
            LastCheckedAtUtc: null,
            SuggestedModels: ["gpt-5-mini"]);
    }

    private sealed class ResolverWorkspaceFactory(IAgentFrameworkWorkspaceService workspaceService) : ICanDoItAllAgentWorkspaceFactory
    {
        public IAgentFrameworkWorkspaceService GetOrganizationWorkspaceService() => workspaceService;

        public IAgentFrameworkWorkspaceService GetWorkspaceService(WorkspaceScopeDescriptor scope) => workspaceService;

        public WorkspaceScopeDescriptor GetOrganizationScope() => WorkspaceScopeDescriptor.Organization("unit-test");

        public string GetWorkspaceRoot() => Path.GetTempPath();
    }

    private sealed class ResolverWorkspaceService(
        IReadOnlyList<AgentDefinition> agents,
        IReadOnlyList<ProviderProfile> providers) : IAgentFrameworkWorkspaceService
    {
        public event EventHandler<ExecutionLogEntry>? ExecutionUpdated
        {
            add { }
            remove { }
        }

        public Task<IReadOnlyList<AgentDefinition>> ListAgentsAsync(bool includeTemplates = true, CancellationToken cancellationToken = default)
            => Task.FromResult(agents);

        public Task<IReadOnlyList<ProviderProfile>> ListProvidersAsync(CancellationToken cancellationToken = default)
            => Task.FromResult(providers);

        public Task<SandboxDashboardSnapshot> GetDashboardAsync(CancellationToken cancellationToken = default) => throw Unused();

        public Task<AgentEditorModel> GetAgentEditorAsync(Guid? agentId = null, CancellationToken cancellationToken = default) => throw Unused();

        public Task<Guid> SaveAgentAsync(AgentEditorModel model, CancellationToken cancellationToken = default) => throw Unused();

        public Task DeleteAgentAsync(Guid agentId, CancellationToken cancellationToken = default) => throw Unused();

        public Task<IReadOnlyList<AgentTeamDefinition>> ListAgentTeamsAsync(CancellationToken cancellationToken = default) => throw Unused();

        public Task<AgentTeamEditorModel> GetAgentTeamEditorAsync(Guid? teamId = null, CancellationToken cancellationToken = default) => throw Unused();

        public Task<Guid> SaveAgentTeamAsync(AgentTeamEditorModel model, CancellationToken cancellationToken = default) => throw Unused();

        public Task<AgentTeamDefinition> UpdateAgentTeamMembersAsync(Guid teamId, IReadOnlyList<Guid> agentIds, CancellationToken cancellationToken = default) => throw Unused();

        public Task DeleteAgentTeamAsync(Guid teamId, CancellationToken cancellationToken = default) => throw Unused();

        public Task<Guid> CloneAgentAsync(Guid agentId, string cloneName, CancellationToken cancellationToken = default) => throw Unused();

        public Task<Guid> ConvertToTemplateAsync(Guid agentId, string templateKey, CancellationToken cancellationToken = default) => throw Unused();

        public Task<AgentExportResult> ExportAgentAsync(Guid agentId, CancellationToken cancellationToken = default) => throw Unused();

        public Task<Guid> ImportAgentAsync(string packagePath, CancellationToken cancellationToken = default) => throw Unused();

        public Task<ProviderProfileEditorModel> GetProviderEditorAsync(Guid? providerId = null, CancellationToken cancellationToken = default) => throw Unused();

        public Task<Guid> SaveProviderAsync(ProviderProfileEditorModel model, CancellationToken cancellationToken = default) => throw Unused();

        public Task DeleteProviderAsync(Guid providerId, CancellationToken cancellationToken = default) => throw Unused();

        public Task<ProviderHealthResult> TestProviderAsync(Guid providerId, CancellationToken cancellationToken = default) => throw Unused();

        public Task<ProviderTestChatResult> RunProviderTestChatAsync(Guid providerId, ProviderTestChatRequest request, CancellationToken cancellationToken = default) => throw Unused();

        public Task<OllamaModelfileResult> CreateOrUpdateOllamaModelAsync(Guid providerId, OllamaModelfileRequest request, CancellationToken cancellationToken = default) => throw Unused();

        public Task<IReadOnlyList<CapabilityCatalogItem>> ListCapabilitiesAsync(CancellationToken cancellationToken = default) => throw Unused();

        public Task<CapabilityEditorModel> GetCapabilityEditorAsync(Guid? capabilityId = null, CancellationToken cancellationToken = default) => throw Unused();

        public Task<Guid> SaveCapabilityAsync(CapabilityEditorModel model, CancellationToken cancellationToken = default) => throw Unused();

        public Task DeleteCapabilityAsync(Guid capabilityId, CancellationToken cancellationToken = default) => throw Unused();

        public Task VerifyCapabilityAsync(Guid agentId, Guid capabilityId, CancellationToken cancellationToken = default) => throw Unused();

        public Task<IReadOnlyList<ChatSessionRecord>> ListChatSessionsAsync(Guid agentId, CancellationToken cancellationToken = default) => throw Unused();

        public Task<ChatPageBootstrapSnapshot> GetChatPageBootstrapAsync(bool includeTemplates = false, CancellationToken cancellationToken = default) => throw Unused();

        public Task<ChatAgentWorkspaceSnapshot> GetChatAgentWorkspaceAsync(Guid agentId, Guid? preferredSessionId = null, CancellationToken cancellationToken = default) => throw Unused();

        public Task<ChatSessionRecord> GetOrCreateChatSessionAsync(Guid agentId, Guid? chatSessionId = null, CancellationToken cancellationToken = default) => throw Unused();

        public Task<ChatSessionRecord> RenameChatSessionAsync(Guid agentId, Guid chatSessionId, string title, CancellationToken cancellationToken = default) => throw Unused();

        public Task<ExecutionRunResult> ExecuteRunAsync(ExecutionRunRequest request, CancellationToken cancellationToken = default) => throw Unused();

        public Task<ExecutionRunResult> ContinueExecutionRunAsync(Guid executionRunId, bool approved, bool autoApprovePendingToolCalls = false, CancellationToken cancellationToken = default) => throw Unused();

        public Task<AgentChatRunResult> SendMessageAsync(Guid agentId, Guid? chatSessionId, string prompt, CancellationToken cancellationToken = default) => throw Unused();

        public Task<AgentChatRunResult> RespondToPendingApprovalsAsync(Guid agentId, Guid chatSessionId, bool approved, bool autoApprovePendingToolCalls = false, CancellationToken cancellationToken = default) => throw Unused();

        public Task<IReadOnlyList<ExecutionLogEntry>> ListExecutionLogAsync(Guid agentId, Guid? chatSessionId = null, CancellationToken cancellationToken = default) => throw Unused();

        public Task<ChatRuntimeSnapshot> GetChatRuntimeSnapshotAsync(Guid agentId, Guid? chatSessionId = null, CancellationToken cancellationToken = default) => throw Unused();

        public Task<IReadOnlyList<AgentRunMetric>> ListMetricsAsync(Guid agentId, CancellationToken cancellationToken = default) => throw Unused();

        public Task<IReadOnlyList<AgentMemoryRecord>> ListMemoryAsync(Guid agentId, CancellationToken cancellationToken = default) => throw Unused();

        public Task<Guid> SaveMemoryAsync(MemoryEditorModel model, CancellationToken cancellationToken = default) => throw Unused();

        public Task DeleteMemoryAsync(Guid memoryId, CancellationToken cancellationToken = default) => throw Unused();

        public Task<IReadOnlyList<ExecutionRunRecord>> ListExecutionRunsAsync(ExecutionRunQuery query, CancellationToken cancellationToken = default) => throw Unused();

        public Task<ExecutionRunDetail> GetExecutionRunDetailAsync(Guid executionRunId, CancellationToken cancellationToken = default) => throw Unused();

        public Task<IReadOnlyList<ExecutionArtifactRecord>> ListExecutionArtifactsAsync(Guid executionRunId, CancellationToken cancellationToken = default) => throw Unused();

        public Task<IReadOnlyList<ExecutionWorkflowCheckpointRecord>> ListExecutionWorkflowCheckpointsAsync(Guid executionRunId, CancellationToken cancellationToken = default) => throw Unused();

        public Task<IReadOnlyList<ToolExecutionReceiptRecord>> ListToolExecutionReceiptsAsync(Guid executionRunId, CancellationToken cancellationToken = default) => throw Unused();

        private static InvalidOperationException Unused()
            => new("This fake workspace method is not used by the resolver test.");
    }

    private sealed class NoOpAiTechnicalAgentBridge : IAiTechnicalAgentBridge
    {
        public Task SynchronizeDirectoryProjectionAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task<IReadOnlyDictionary<Guid, AiTechnicalAgentDirectorySummary>> GetDirectorySummariesAsync(IReadOnlyList<Guid> partyIds, CancellationToken cancellationToken = default) => throw Unused();

        public Task<AiTechnicalAgentWorkspaceModel> GetWorkspaceAsync(Guid partyId, CancellationToken cancellationToken = default) => throw Unused();

        public Task<IReadOnlyDictionary<Guid, AiAgentStaffingFactModel>> GetStaffingFactsAsync(IReadOnlyList<Guid> partyIds, CancellationToken cancellationToken = default) => throw Unused();

        public Task<Result<AiTechnicalAgentSaveResult>> SaveAsync(AiAgentProfileEditorModel model, CancellationToken cancellationToken = default) => throw Unused();

        private static InvalidOperationException Unused()
            => new("This fake bridge method is not used by the resolver test.");
    }
}
