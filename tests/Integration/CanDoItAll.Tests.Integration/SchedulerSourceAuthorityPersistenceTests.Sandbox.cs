using System.Text.Json;
using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.AgentFramework.Tooling;
using CanDoItAll.AgentFramework.Workflows.Abstractions;
using CanDoItAll.Modules.AgentFramework;
using CanDoItAll.Modules.SchedulerPlanner;
using CanDoItAll.Modules.Workbench;
using CanDoItAll.SharedKernel;
using CanDoItAll.Tests.Support;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;

namespace CanDoItAll.Tests.Integration;

public sealed partial class SchedulerSourceAuthorityPersistenceTests {
    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public async Task Managed_Sandbox_schedule_saves_original_empty_project_ceiling_and_admits_exact_fire_after_restart(
        bool schedulerSurface, bool actorHasAllProjects) {
        await using var environment = CanDoItAllTestEnvironment.Create("scheduler-sandbox-restart");
        var profile = environment.CreatePostgreSqlProfile("saved");
        var options = Harness(environment, profile);
        Guid planId;
        string savedAuthority;
        string savedRun;
        string savedStarted;
        await using (var app = await TestApplication.CreateAsync(options)) {
            await using var scope = app.Services.CreateAsyncScope();
            var services = scope.ServiceProvider;
            var scenario = await CreateSandboxScenarioAsync(services, schedulerSurface, actorHasAllProjects);
            using var capture = AgentToolInvocationEffectScope.Begin();
            var raw = await scenario.Create.InvokeAsync(new AIFunctionArguments { ["request"] = SandboxCreateRequest(scenario.Definition) });
            var result = raw is SchedulerWorkflowScheduleCreateResult typed ? typed :
                Assert.IsType<JsonElement>(raw).Deserialize<SchedulerWorkflowScheduleCreateResult>(scenario.Create.JsonSerializerOptions)!;
            Assert.NotNull(result);
            Assert.Equal(scenario.Definition.Id.Value, result.WorkflowId);
            Assert.Equal(scenario.Definition.VersionId.Value, result.WorkflowVersionId);
            Assert.NotNull(capture.CommittedEffect);
            planId = result.PlanId;
            var factory = services.GetRequiredService<IDbContextFactory<SchedulerPlannerDbContext>>();
            await using var database = await factory.CreateDbContextAsync();
            var plan = await database.Set<SchedulerPlan>().AsNoTracking().SingleAsync(row => row.TargetId == result.WorkflowId);
            Assert.Equal(planId, plan.Id);
            savedAuthority = Assert.IsType<string>(plan.StructureAuthorityJson);
            var authority = Assert.IsType<WorkflowStructureAuthority>(SchedulerFireSnapshot.ParseAuthority(savedAuthority));
            AssertSandboxCeiling(authority);
            Assert.Equal(scenario.Governance.AuthorityId, authority.AgentGovernance!.AuthorityId);
            Assert.Equal(scenario.Governance.PolicyFingerprint, authority.PolicyFingerprint);
            Assert.Equal(scenario.Governance.DatabaseProfileGeneration, authority.AgentGovernance.DatabaseProfileGeneration);
            Assert.Equal(scenario.Governance.DatabaseProfileId, authority.DatabaseProfileId);
            var admissions = new SchedulerFireAdmissionStore(factory, services.GetRequiredService<IWorkflowCatalogService>(),
                services.GetRequiredService<IWorkflowRuntimeManager>(), services.GetRequiredService<IClock>());
            var claim = Assert.IsType<SchedulerFireClaim>(await admissions.AcquireAsync(new(planId, Guid.NewGuid(), Guid.NewGuid(),
                DateTimeOffset.UtcNow, null)));
            var prepared = claim.Snapshot.ToContext(claim.PreparedRunId, true);
            var origin = new WorkflowLaunchOrigin.SchedulerPlanRun(prepared.PlanId, prepared.PlanRunId, prepared.SchedulerFireId,
                prepared.FiredAtUtc, prepared.CorrelationId) { PreparedRunId = prepared.PreparedRunId, StructureAuthority = prepared.StructureAuthority };
            var now = DateTimeOffset.UtcNow;
            var run = new WorkflowRunSnapshot(new(claim.PreparedRunId), scenario.Definition.Id, scenario.Definition.VersionId,
                WorkflowRunState.Running, WorkflowRuntimeBackendKind.InProcess, "sandbox-fire", "Prepared", now, now) { Origin = origin };
            savedRun = JsonSerializer.Serialize(run);
            savedStarted = JsonSerializer.Serialize(new WorkflowEventRecord(Guid.NewGuid(), run.RunId, WorkflowEventKind.Started,
                null, "Sandbox schedule", "{}", now));
        }
        await using var restarted = await TestApplication.CreateAsync(options);
        await using var restartedScope = restarted.Services.CreateAsyncScope();
        var restartedServices = restartedScope.ServiceProvider;
        var restoredRun = JsonSerializer.Deserialize<WorkflowRunSnapshot>(savedRun)!;
        var restoredStarted = JsonSerializer.Deserialize<WorkflowEventRecord>(savedStarted)!;
        var runs = restartedServices.GetRequiredService<IWorkflowRunStore>();
        await runs.CreateRunWithStartedEventAsync(restoredRun, restoredStarted);
        var persisted = Assert.IsType<WorkflowRunSnapshot>(await runs.GetRunAsync(restoredRun.RunId));
        Assert.Equal(WorkflowStructureAuthorityFingerprint.Create(restoredRun.Origin!.StructureAuthority!),
            WorkflowStructureAuthorityFingerprint.Create(persisted.Origin!.StructureAuthority!));
        Assert.Equal(restoredStarted.Id, Assert.Single(await runs.ListEventsAsync(restoredRun.RunId)).Id);
        await using var read = await restartedServices.GetRequiredService<IDbContextFactory<SchedulerPlannerDbContext>>().CreateDbContextAsync();
        Assert.Equal(savedAuthority, (await read.Set<SchedulerPlan>().AsNoTracking().SingleAsync(row => row.Id == planId)).StructureAuthorityJson);
        var original = Assert.IsType<WorkflowStructureAuthority>(SchedulerFireSnapshot.ParseAuthority(savedAuthority));
        var sourcePolicy = restartedServices.GetRequiredService<IWorkflowStructureSourceAuthorityPolicy>();
        await using (var disclosure = await sourcePolicy.AcquireAsync(original, WorkflowStructureAuthorityUse.Disclosure)) {
            AssertSandboxCeiling(original);
        }
        var workspace = restartedServices.GetRequiredService<IAgentFrameworkWorkspaceService>();
        var revoked = await workspace.GetAgentEditorAsync(original.AgentGovernance!.AgentId);
        revoked.Permissions = revoked.Permissions with { CanScheduleWork = false };
        await workspace.SaveAgentAsync(revoked);
        Assert.False((await workspace.ListAgentsAsync()).Single(agent => agent.Id == revoked.Id).Permissions.CanScheduleWork);
        await Assert.ThrowsAsync<ProjectStructureAgentException>(() => sourcePolicy
            .AcquireAsync(persisted.Origin.StructureAuthority!, WorkflowStructureAuthorityUse.Admission));
        Assert.Equal(savedAuthority, (await read.Set<SchedulerPlan>().AsNoTracking().SingleAsync(row => row.Id == planId)).StructureAuthorityJson);
        Assert.Equal(restoredRun.RunId, (await runs.GetRunAsync(restoredRun.RunId))!.RunId);
    }

    [Theory]
    [InlineData(AgentRevocation.Tools)]
    [InlineData(AgentRevocation.Schedule)]
    [InlineData(AgentRevocation.Inactive)]
    public async Task Captured_Sandbox_schedule_rechecks_current_actor_before_owner_save(AgentRevocation revocation) {
        await using var app = await TestApplication.CreateAsync(Harness());
        await using var scope = app.Services.CreateAsyncScope();
        var services = scope.ServiceProvider;
        var scenario = await CreateSandboxScenarioAsync(services, schedulerSurface: false, actorHasAllProjects: false);
        var authority = await services.GetRequiredService<ProjectStructureWorkflowAuthorityService>()
            .CaptureAgentAsync(scenario.Agent, scenario.Governance);
        var editor = await scenario.Workspace.GetAgentEditorAsync(scenario.Agent.Id);
        if (revocation == AgentRevocation.Inactive) {
            editor.Status = AgentLifecycleStatus.Suspended;
        } else {
            editor.Permissions = revocation == AgentRevocation.Tools
                ? editor.Permissions with { CanUseTools = false } : editor.Permissions with { CanScheduleWork = false };
        }
        await scenario.Workspace.SaveAgentAsync(editor);
        var current = (await scenario.Workspace.ListAgentsAsync()).Single(agent => agent.Id == scenario.Agent.Id);
        Assert.False(SchedulerAgentRuntimeAuthorizationPolicy.IsManagedSchedulerActor(current));
        using var capture = AgentToolInvocationEffectScope.Begin();
        await Assert.ThrowsAsync<UnauthorizedAccessException>(async () =>
            await scenario.Create.InvokeAsync(new AIFunctionArguments { ["request"] = SandboxCreateRequest(scenario.Definition) }));
        await Assert.ThrowsAsync<ProjectStructureAgentException>(() => services.GetRequiredService<IWorkflowStructureSourceAuthorityPolicy>()
            .AcquireAsync(authority, WorkflowStructureAuthorityUse.Schedule));
        Assert.Null(capture.CommittedEffect);
        await using var database = await services.GetRequiredService<IDbContextFactory<SchedulerPlannerDbContext>>().CreateDbContextAsync();
        Assert.False(await database.Set<SchedulerPlan>().AnyAsync(row => row.TargetId == scenario.Definition.Id.Value));
    }

    public enum SandboxExpansion { AllProjects, ProjectLifetime, TaskOutput, AssetOutput }

    [Fact]
    public async Task Cached_Sandbox_schedule_tool_refuses_removed_current_create_capability() {
        await using var app = await TestApplication.CreateAsync(Harness());
        await using var scope = app.Services.CreateAsyncScope();
        var scenario = await CreateSandboxScenarioAsync(scope.ServiceProvider, schedulerSurface: false, actorHasAllProjects: false);
        var assignment = Assert.Single(scenario.Agent.Capabilities,
            item => item.CapabilityKey == SchedulerAgentIdentity.WorkflowScheduleCreateCapabilityKey);
        var editor = await scenario.Workspace.GetAgentEditorAsync(scenario.Agent.Id);
        Assert.True(editor.SelectedCapabilityIds.Remove(assignment.CapabilityId));
        await scenario.Workspace.SaveAgentAsync(editor);
        var current = (await scenario.Workspace.ListAgentsAsync()).Single(agent => agent.Id == scenario.Agent.Id);
        Assert.DoesNotContain(current.Capabilities, item => item.CapabilityId == assignment.CapabilityId);
        using var capture = AgentToolInvocationEffectScope.Begin();
        await Assert.ThrowsAsync<UnauthorizedAccessException>(async () =>
            await scenario.Create.InvokeAsync(new AIFunctionArguments { ["request"] = SandboxCreateRequest(scenario.Definition) }));
        Assert.Null(capture.CommittedEffect);
        await using var database = await scope.ServiceProvider.GetRequiredService<IDbContextFactory<SchedulerPlannerDbContext>>().CreateDbContextAsync();
        Assert.False(await database.Set<SchedulerPlan>().AnyAsync(row => row.TargetId == scenario.Definition.Id.Value));
    }

    public enum SandboxSourceMismatch { Profile, Generation, NamedSandbox }

    [Theory]
    [InlineData(SandboxSourceMismatch.Profile)]
    [InlineData(SandboxSourceMismatch.Generation)]
    [InlineData(SandboxSourceMismatch.NamedSandbox)]
    public async Task Empty_Sandbox_ceiling_still_requires_original_profile_generation_and_default_scope(SandboxSourceMismatch mismatch) {
        await using var app = await TestApplication.CreateAsync(Harness());
        await using var scope = app.Services.CreateAsyncScope();
        var scenario = await CreateSandboxScenarioAsync(scope.ServiceProvider, schedulerSurface: false, actorHasAllProjects: false);
        var owner = scope.ServiceProvider.GetRequiredService<ProjectStructureWorkflowAuthorityService>();
        var original = await owner.CaptureAgentAsync(scenario.Agent, scenario.Governance);
        var saved = scenario.Governance;
        var governance = new AgentExecutionGovernanceSnapshot(saved.AuthorityId, saved.AgentId,
            mismatch == SandboxSourceMismatch.Profile ? Guid.NewGuid() : saved.DatabaseProfileId,
            mismatch == SandboxSourceMismatch.Generation ? new(saved.DatabaseProfileGeneration.Value + 1) : saved.DatabaseProfileGeneration,
            mismatch == SandboxSourceMismatch.NamedSandbox ? new(WorkspaceScopeKind.Sandbox, "other") : saved.WorkspaceScope,
            saved.ReadAllowed, saved.MutationAllowed, saved.PolicyVersion, saved.PolicyFingerprint,
            saved.AllowedOperations.ToArray(), saved.AllowedCapabilityKeys.ToArray(), saved.WritableExternalTargetAliases.ToArray(),
            saved.ReadOnlyExternalTargetAliases.ToArray(), saved.AllowedManagedArtifactReadRefs.ToArray());
        var changed = original with { AgentGovernance = governance, DatabaseProfileId = governance.DatabaseProfileId };
        foreach (var use in new[] { WorkflowStructureAuthorityUse.Schedule, WorkflowStructureAuthorityUse.Admission, WorkflowStructureAuthorityUse.Disclosure }) {
            await Assert.ThrowsAsync<ProjectStructureAgentException>(() => owner.AcquireAsync(changed, use));
        }
    }

    [Theory]
    [InlineData(SandboxExpansion.AllProjects)]
    [InlineData(SandboxExpansion.ProjectLifetime)]
    [InlineData(SandboxExpansion.TaskOutput)]
    [InlineData(SandboxExpansion.AssetOutput)]
    public async Task Sandbox_cannot_inherit_project_grants_even_when_disclosure_skips_admission_targets(SandboxExpansion expansion) {
        await using var app = await TestApplication.CreateAsync(Harness());
        await using var scope = app.Services.CreateAsyncScope();
        var services = scope.ServiceProvider;
        var scenario = await CreateSandboxScenarioAsync(services, schedulerSurface: false, actorHasAllProjects: true);
        var owner = services.GetRequiredService<ProjectStructureWorkflowAuthorityService>();
        var original = await owner.CaptureAgentAsync(scenario.Agent, scenario.Governance);
        AssertSandboxCeiling(original);
        var target = new WorkflowProjectLifetime(original.DatabaseProfileId, Guid.NewGuid(), Guid.NewGuid());
        var forged = expansion switch {
            SandboxExpansion.AllProjects => original with { AllProjects = true },
            SandboxExpansion.ProjectLifetime => original with { ProjectIds = [target.ProjectId], ProjectScope = new([target]) },
            SandboxExpansion.TaskOutput => original with { CanCreateTasks = true },
            SandboxExpansion.AssetOutput => original with { CanCreateAssets = true },
            _ => throw new ArgumentOutOfRangeException(nameof(expansion))
        };
        foreach (var use in new[] { WorkflowStructureAuthorityUse.Schedule, WorkflowStructureAuthorityUse.Admission, WorkflowStructureAuthorityUse.Disclosure }) {
            await Assert.ThrowsAsync<ProjectStructureAgentException>(() => owner.AcquireAsync(forged, use));
        }
        var now = DateTimeOffset.UtcNow;
        var run = new WorkflowRunSnapshot(WorkflowRunId.New(), scenario.Definition.Id, scenario.Definition.VersionId,
            WorkflowRunState.Running, WorkflowRuntimeBackendKind.InProcess, "forged-disclosure", "Read", now, now) {
            Origin = new WorkflowLaunchOrigin.Api(forged.Principal, new(Guid.NewGuid())) { StructureAuthority = forged }
        };
        await Assert.ThrowsAsync<ProjectStructureAgentException>(() => owner.AcquireReadAsync(run, CancellationToken.None));
        await Assert.ThrowsAsync<ProjectStructureAgentException>(() => owner.AcquireAsync(original, WorkflowStructureAuthorityUse.Admission, target));
        await Assert.ThrowsAsync<ProjectStructureAgentException>(() => owner.CaptureOutputTargetAsync(original, target.ProjectId, CancellationToken.None));
    }

    [Fact]
    public async Task Sandbox_rejects_fixed_Structure_target_and_Structure_only_uses_without_recapturing_a_project() {
        await using var app = await TestApplication.CreateAsync(Harness());
        await using var scope = app.Services.CreateAsyncScope();
        var scenario = await CreateSandboxScenarioAsync(scope.ServiceProvider, schedulerSurface: false, actorHasAllProjects: true);
        var owner = scope.ServiceProvider.GetRequiredService<ProjectStructureWorkflowAuthorityService>();
        var authority = await owner.CaptureAgentAsync(scenario.Agent, scenario.Governance);
        var executor = new WorkflowNode(new("effect"), WorkflowNodeKind.Executor, "Create task", [],
            new(null, null, null, null, string.Empty, WorkflowValueShape.Text, WorkflowValueShape.Text) {
                ExecutorId = WorkflowExecutorIds.ProjectStructure,
                ExecutorSettingsJson = JsonSerializer.Serialize(new WorkflowProjectStructureExecutorSettings {
                    Operation = WorkflowProjectStructureOperation.CreateTaskNodes, ProjectId = Guid.NewGuid()
                }, new JsonSerializerOptions(JsonSerializerDefaults.Web))
            });
        var definition = scenario.Definition with { Graph = scenario.Definition.Graph with { Nodes = [.. scenario.Definition.Graph.Nodes, executor] } };
        await Assert.ThrowsAsync<ProjectStructureAgentException>(() => owner.PrepareLaunchAsync(authority, definition));
        foreach (var use in new[] { WorkflowStructureAuthorityUse.TaskOutput, WorkflowStructureAuthorityUse.AssetOutput,
            WorkflowStructureAuthorityUse.StructureAdmission, WorkflowStructureAuthorityUse.StatusProjection }) {
            await Assert.ThrowsAsync<ProjectStructureAgentException>(() => owner.AcquireAsync(authority, use));
        }
        AssertSandboxCeiling(authority);
    }

    private static async Task<SandboxScenario> CreateSandboxScenarioAsync(IServiceProvider services, bool schedulerSurface, bool actorHasAllProjects) {
        var workspace = services.GetRequiredService<IAgentFrameworkWorkspaceService>();
        var editor = await workspace.GetAgentEditorAsync(SchedulerAgentIdentity.AgentId);
        editor.ProjectStructureAccess = new() { CanRead = actorHasAllProjects, CanWriteTasks = actorHasAllProjects,
            CanWriteNonTaskStructure = actorHasAllProjects, AllowAllProjects = actorHasAllProjects };
        await workspace.SaveAgentAsync(editor);
        var agent = (await workspace.ListAgentsAsync()).Single(item => item.Id == SchedulerAgentIdentity.AgentId);
        Assert.True(SchedulerAgentRuntimeAuthorizationPolicy.IsManagedSchedulerActor(agent));
        Assert.Equal(actorHasAllProjects, AgentProjectStructureAccessMetadata.Read(agent.ConfigurationJson).AllowAllProjects);
        var capabilities = await workspace.ListCapabilitiesAsync();
        Assert.True(SchedulerAgentRuntimeAuthorizationPolicy.IsToolAuthorized(agent, capabilities, SchedulerToolPolicy.SchedulerWorkflowScheduleCreate));
        var surface = SchedulerAgentChatContextBuilder.Build(SchedulerAgentChatView.Schedules, null, null, null);
        var sourceKind = schedulerSurface ? surface.Source.Kind : new AgentChatContextSourceKind(AgentFrameworkAgentsChatContextBuilder.SourceKind);
        var sourceId = schedulerSurface ? surface.Source.Id : new AgentChatContextSourceId($"agent:{agent.Id:D}");
        var current = await services.GetRequiredService<IAgentExecutionAuthorityResolver>().ResolveAsync(new(agent.Id,
            sourceKind, sourceId, null, services.GetRequiredService<IAgentExecutionProfileGenerationSource>().GetGeneration(), null));
        var governance = AgentExecutionGovernanceSnapshot.FromAuthority(current);
        Assert.True(governance.WorkspaceScope.IsDefaultSandbox);
        Assert.True(governance.MutationAllowed);
        var definition = NewDefinition();
        definition = await services.GetRequiredService<IWorkflowCatalogService>().SaveDefinitionAsync(new(definition.Id, null,
            definition.Name, definition.Description, WorkflowLifecycleStatus.Active, definition.Graph, definition.RuntimePolicy));
        var provider = new SchedulerAgentRuntimeToolProvider(services.GetRequiredService<ISchedulerPlannerService>(),
            services.GetRequiredService<SchedulerAgentRuntimeAuthorizationService>(), services.GetRequiredService<IWorkflowStructureAuthorityFactory>());
        var context = new AgentRuntimeToolProviderContext(agent, (await workspace.ListProvidersAsync()).Single(item => item.Id == agent.ProviderProfileId),
            capabilities, false, AgentRuntimeToolProviderPurpose.InteractiveChat, "sandbox-scheduler", AgentRuntimeContextIntent.Empty,
            new Dictionary<string, string>()) { Governance = governance };
        var create = Assert.Single((await provider.CreateToolsAsync(context, CancellationToken.None)).OfType<AIFunction>(),
            item => item.Name == SchedulerToolPolicy.SchedulerWorkflowScheduleCreate);
        Assert.True(Assert.Single(provider.GetToolMetadata(context), item => item.ToolName == create.Name).RequiresApprovalByDefault);
        return new(workspace, definition, agent, governance, create);
    }

    private static SchedulerWorkflowScheduleCreateInput SandboxCreateRequest(WorkflowDefinition definition)
        => new(definition.Id.Value, $"sandbox-{Guid.NewGuid():N}", "0 0 0 1 1 ?", "UTC", definition.VersionId.Value,
            StartAtUtc: new DateTimeOffset(2099, 1, 1, 0, 0, 0, TimeSpan.Zero));

    private static void AssertSandboxCeiling(WorkflowStructureAuthority authority) {
        Assert.True(authority.AgentGovernance!.WorkspaceScope.IsDefaultSandbox);
        Assert.Equal(Guid.Empty, authority.ProjectId);
        Assert.False(authority.AllProjects);
        Assert.False(authority.CanCreateTasks);
        Assert.False(authority.CanCreateAssets);
        Assert.Empty(authority.ProjectIds);
        Assert.NotNull(authority.ProjectScope);
        Assert.Empty(authority.ProjectScope.Projects);
        Assert.Empty(authority.ProjectScope.AdmissionProjectIds);
    }

    private sealed record SandboxScenario(IAgentFrameworkWorkspaceService Workspace, WorkflowDefinition Definition,
        AgentDefinition Agent, AgentExecutionGovernanceSnapshot Governance, AIFunction Create);
}
