using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;
using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.AgentFramework.Tooling;
using CanDoItAll.Components.CanvasLib;
using CanDoItAll.Modules.SchedulerPlanner;
using Microsoft.Extensions.AI;

namespace CanDoItAll.Tests.Unit;

public sealed class SchedulerAgentRuntimeToolProviderTests
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() }
    };

    [Fact]
    public async Task Provider_attaches_only_to_the_exact_managed_scheduler_agent_and_assigned_tools()
    {
        var harness = CreateHarness();

        var tools = await harness.Provider.CreateToolsAsync(harness.Context, CancellationToken.None);
        var metadata = harness.Provider.GetToolMetadata(harness.Context);

        Assert.Equal(
            SchedulerAgentCapabilityKeys.ToolNameToCapabilityKey.Keys.OrderBy(item => item, StringComparer.Ordinal),
            tools.Select(item => item.Name).OrderBy(item => item, StringComparer.Ordinal));
        Assert.Equal(3, metadata.Count);
        Assert.False(metadata.Single(item => item.ToolName == AgentToolInvocationPolicyMetadata.SchedulerWorkflowTargetsSearch).RequiresApprovalByDefault);
        Assert.False(metadata.Single(item => item.ToolName == AgentToolInvocationPolicyMetadata.SchedulerWorkflowSchedulesSearch).RequiresApprovalByDefault);
        Assert.True(metadata.Single(item => item.ToolName == AgentToolInvocationPolicyMetadata.SchedulerWorkflowScheduleCreate).RequiresApprovalByDefault);

        var spoofedContext = harness.Context with
        {
            Agent = harness.Context.Agent with { Id = Guid.NewGuid() }
        };

        Assert.Empty(await harness.Provider.CreateToolsAsync(spoofedContext, CancellationToken.None));
        Assert.Empty(harness.Provider.GetToolMetadata(spoofedContext));
    }

    [Fact]
    public void Scheduler_tool_policy_requires_approval_and_redacts_schedule_content()
    {
        Assert.True(ToolCapabilityRegistry.TryResolve(
            AgentToolInvocationPolicyMetadata.SchedulerWorkflowScheduleCreate,
            out var metadata));
        Assert.True(metadata.IsStateChanging);
        Assert.Equal(ToolCapabilitySideEffectKind.InternalStateMutation, metadata.SideEffectKind);
        Assert.True(AgentToolInvocationPolicyMetadata.RequiresApprovalByDefault(
            AgentToolInvocationPolicyMetadata.SchedulerWorkflowScheduleCreate));

        var workflowId = Guid.Parse("11111111-1111-1111-1111-111111111111");
        const string scheduleName = "Confidential customer schedule";
        const string inputJson = "{\"customer\":\"Project Nightfall\"}";
        var request = new
        {
            workflowId,
            name = scheduleName,
            inputJson
        };
        var redacted = AgentToolInvocationPolicyMetadata.RedactArguments(
            AgentToolInvocationPolicyMetadata.SchedulerWorkflowScheduleCreate,
        [
            new KeyValuePair<string, object?>("request", request)
        ]);
        var signature = AgentToolInvocationPolicyMetadata.BuildSignature(
            AgentToolInvocationPolicyMetadata.SchedulerWorkflowScheduleCreate,
            redacted);
        var audit = AgentToolInvocationPolicyMetadata.ProtectApprovalArgumentsForAudit(
            AgentToolInvocationPolicyMetadata.SchedulerWorkflowScheduleCreate,
            JsonSerializer.Serialize(new { request }));

        Assert.Contains(workflowId.ToString("D"), signature, StringComparison.Ordinal);
        Assert.DoesNotContain(scheduleName, signature, StringComparison.Ordinal);
        Assert.DoesNotContain("Project Nightfall", signature, StringComparison.Ordinal);
        Assert.Contains("scheduler-approval-redacted-v1", audit, StringComparison.Ordinal);
        Assert.Contains(workflowId.ToString("D"), audit, StringComparison.Ordinal);
        Assert.DoesNotContain(scheduleName, audit, StringComparison.Ordinal);
        Assert.DoesNotContain("Project Nightfall", audit, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Tools_search_and_create_workflow_schedules_without_exposing_process_targets()
    {
        var workflowId = Guid.NewGuid();
        var workflowVersionId = Guid.NewGuid();
        var processId = Guid.NewGuid();
        var existingPlanId = Guid.NewGuid();
        var workspace = new SchedulerPlannerWorkspace(
            [
                new SchedulerPlanSummary(
                    existingPlanId,
                    "Morning release",
                    "Existing workflow schedule",
                    SchedulerPlanTargetKind.Workflow,
                    workflowId,
                    workflowVersionId,
                    "Release workflow",
                    "0 0 9 ? * MON-FRI",
                    "Every weekday at 09:00",
                    "UTC",
                    SchedulerPlanMisfirePolicy.FireOnceNow,
                    true,
                    null,
                    null,
                    DateTimeOffset.Parse("2026-07-21T09:00:00Z"),
                    null,
                    string.Empty,
                    DateTimeOffset.Parse("2026-07-20T12:00:00Z")),
                new SchedulerPlanSummary(
                    Guid.NewGuid(),
                    "Process plan",
                    "Deferred process schedule",
                    SchedulerPlanTargetKind.Process,
                    processId,
                    null,
                    "Release process",
                    "0 0 10 ? * MON-FRI",
                    "Every weekday at 10:00",
                    "UTC",
                    SchedulerPlanMisfirePolicy.FireOnceNow,
                    true,
                    null,
                    null,
                    null,
                    null,
                    string.Empty,
                    DateTimeOffset.Parse("2026-07-20T12:00:00Z"))
            ],
            [],
            [
                new SchedulerTargetOption(
                    SchedulerPlanTargetKind.Workflow,
                    workflowId,
                    workflowVersionId,
                    "Release workflow",
                    "Publishes the release.",
                    "Active"),
                new SchedulerTargetOption(
                    SchedulerPlanTargetKind.Process,
                    processId,
                    null,
                    "Release process",
                    "Not yet supported by the managed agent.",
                    "Published")
            ],
            new CanvasCalendarSurface { SurfaceId = "scheduler-agent-tests" });
        var schedulerService = new RecordingSchedulerPlannerService(workspace);
        var harness = CreateHarness(schedulerService);
        var tools = (await harness.Provider.CreateToolsAsync(harness.Context, CancellationToken.None))
            .Cast<AIFunction>()
            .ToDictionary(item => item.Name, StringComparer.Ordinal);

        var targetSearch = await InvokeAsync<SchedulerWorkflowTargetSearchResult>(
            tools[AgentToolInvocationPolicyMetadata.SchedulerWorkflowTargetsSearch],
            new SchedulerWorkflowTargetSearchInput("release"));
        var target = Assert.Single(targetSearch.Items);
        Assert.Equal(workflowId, target.WorkflowId);

        var scheduleSearch = await InvokeAsync<SchedulerWorkflowScheduleSearchResult>(
            tools[AgentToolInvocationPolicyMetadata.SchedulerWorkflowSchedulesSearch],
            new SchedulerWorkflowScheduleSearchInput("release"));
        var schedule = Assert.Single(scheduleSearch.Items);
        Assert.Equal(existingPlanId, schedule.PlanId);

        var created = await InvokeAsync<SchedulerWorkflowScheduleCreateResult>(
            tools[AgentToolInvocationPolicyMetadata.SchedulerWorkflowScheduleCreate],
            new SchedulerWorkflowScheduleCreateInput(
                workflowId,
                "Afternoon release",
                "0 0 15 ? * MON-FRI",
                "UTC",
                workflowVersionId,
                "Approved workflow schedule"));

        Assert.Equal(workflowId, created.WorkflowId);
        Assert.Equal(SchedulerPlanTargetKind.Workflow, schedulerService.SavedEditor?.TargetKind);
        Assert.Equal(workflowVersionId, schedulerService.SavedEditor?.TargetVersionId);
        Assert.Equal("0 0 15 ? * MON-FRI", schedulerService.SavedEditor?.CronExpression);
    }

    private static RuntimeHarness CreateHarness(RecordingSchedulerPlannerService? schedulerService = null)
    {
        var now = DateTimeOffset.Parse("2026-07-20T12:00:00Z");
        var capabilities = SchedulerAgentCapabilityKeys.ToolNameToCapabilityKey.Values
            .Select(key => new CapabilityCatalogItem(
                Guid.NewGuid(),
                CapabilityKind.Tool,
                key,
                key,
                string.Empty,
                string.Empty,
                string.Empty,
                CapabilityProofStatus.Verified,
                string.Empty,
                now,
                IsBuiltIn: true))
            .ToArray();
        var assignments = capabilities
            .Select(capability => new AgentCapabilityAssignment(
                capability.Id,
                capability.Key,
                capability.Kind,
                capability.ProofStatus,
                capability.LastVerifiedAtUtc,
                capability.ProofNotes))
            .ToArray();
        var providerProfileId = Guid.NewGuid();
        var agent = new AgentDefinition(
            SchedulerAgentIdentity.AgentId,
            SchedulerAgentIdentity.DefaultDisplayName,
            "Workflow scheduling assistant",
            "Creates approved workflow schedules.",
            "Use only Scheduler Agent tools.",
            AgentLifecycleStatus.Active,
            providerProfileId,
            "gpt-5.4-mini",
            AgentWorkloadKind.Management,
            AgentChatHistoryMode.FrameworkManaged,
            0.2,
            RequirePerServiceCallChatHistoryPersistence: false,
            EnableBackgroundResponses: false,
            "{}",
            IsTemplate: false,
            SchedulerAgentIdentity.TemplateKey,
            AgentPermissionsPolicy.Default with { CanUseTools = true },
            assignments,
            [],
            now,
            now);
        var providerProfile = new ProviderProfile(
            providerProfileId,
            "OpenAI default",
            ProviderKind.OpenAi,
            "https://api.openai.com/v1",
            "OPENAI_API_KEY",
            agent.Model,
            ProviderTransportKind.Responses,
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
        var workspaceService = DispatchProxy.Create<IAgentFrameworkWorkspaceService, AuthorizationWorkspaceProxy>();
        var workspaceProxy = (AuthorizationWorkspaceProxy)(object)workspaceService;
        workspaceProxy.Agents = [agent];
        workspaceProxy.Capabilities = capabilities;
        schedulerService ??= new RecordingSchedulerPlannerService(new SchedulerPlannerWorkspace(
            [],
            [],
            [],
            new CanvasCalendarSurface { SurfaceId = "scheduler-agent-tests" }));
        var runtimeProvider = new SchedulerAgentRuntimeToolProvider(
            schedulerService,
            new SchedulerAgentRuntimeAuthorizationService(workspaceService));
        var context = new AgentRuntimeToolProviderContext(
            agent,
            providerProfile,
            capabilities,
            SuppressApprovalRequirements: false,
            AgentRuntimeToolProviderPurpose.InteractiveChat,
            RuntimeSessionKey: "scheduler-agent-runtime-test",
            AgentRuntimeContextIntent.Empty,
            Tags: new Dictionary<string, string>());
        return new RuntimeHarness(runtimeProvider, context);
    }

    private static async Task<TResult> InvokeAsync<TResult>(AIFunction function, object request)
    {
        var rawResult = await function.InvokeAsync(new AIFunctionArguments
        {
            ["request"] = request
        });
        return rawResult switch
        {
            TResult result => result,
            JsonElement element => JsonSerializer.Deserialize<TResult>(element.GetRawText(), JsonOptions)
                ?? throw new InvalidOperationException("Scheduler Agent runtime tool returned null JSON."),
            _ => throw new InvalidOperationException(
                $"Unexpected Scheduler Agent runtime tool result type '{rawResult?.GetType().FullName ?? "<null>"}'.")
        };
    }

    private sealed record RuntimeHarness(
        SchedulerAgentRuntimeToolProvider Provider,
        AgentRuntimeToolProviderContext Context);

    private sealed class RecordingSchedulerPlannerService(
        SchedulerPlannerWorkspace workspace) : ISchedulerPlannerService
    {
        public SchedulerPlanEditorModel? SavedEditor { get; private set; }

        public Task<SchedulerPlannerWorkspace> GetWorkspaceAsync(
            SchedulerHistoryQuery? historyQuery = null,
            CancellationToken cancellationToken = default)
            => Task.FromResult(workspace);

        public Task<SchedulerPlanEditorModel> CreateDefaultEditorAsync(CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task<SchedulerPlanEditorModel> GetPlanEditorAsync(
            Guid planId,
            CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task<SchedulerPlanSummary> SavePlanAsync(
            SchedulerPlanEditorModel editor,
            CancellationToken cancellationToken = default)
        {
            SavedEditor = editor;
            var target = workspace.TargetOptions.Single(item =>
                item.Kind == SchedulerPlanTargetKind.Workflow &&
                item.Id == editor.TargetId);
            return Task.FromResult(new SchedulerPlanSummary(
                Guid.NewGuid(),
                editor.Name,
                editor.Description,
                editor.TargetKind,
                editor.TargetId,
                target.VersionId,
                target.Name,
                editor.CronExpression,
                "Every weekday at 15:00",
                editor.TimeZoneId,
                editor.MisfirePolicy,
                editor.IsEnabled,
                editor.StartAtUtc,
                editor.EndAtUtc,
                DateTimeOffset.Parse("2026-07-21T15:00:00Z"),
                null,
                string.Empty,
                DateTimeOffset.Parse("2026-07-20T12:00:00Z")));
        }

        public Task SetPlanEnabledAsync(
            Guid planId,
            bool isEnabled,
            CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task DeletePlanAsync(
            Guid planId,
            CancellationToken cancellationToken = default)
            => throw new NotSupportedException();
    }

    private class AuthorizationWorkspaceProxy : DispatchProxy
    {
        public IReadOnlyList<AgentDefinition> Agents { get; set; } = [];

        public IReadOnlyList<CapabilityCatalogItem> Capabilities { get; set; } = [];

        protected override object? Invoke(MethodInfo? targetMethod, object?[]? args)
        {
            return targetMethod?.Name switch
            {
                nameof(IAgentFrameworkWorkspaceService.ListAgentsAsync) => Task.FromResult(Agents),
                nameof(IAgentFrameworkWorkspaceService.ListCapabilitiesAsync) => Task.FromResult(Capabilities),
                _ => throw new InvalidOperationException(
                    $"Workspace service member '{targetMethod?.Name}' was not expected in Scheduler Agent tests.")
            };
        }
    }
}
