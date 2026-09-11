using System.Data.Common;
using System.Text.Json;
using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Maf;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.AgentFramework.Persistence;
using CanDoItAll.AgentFramework.Tooling;
using CanDoItAll.AgentFramework.WorkflowExecutors.Standard.ProjectStructure;
using CanDoItAll.AgentFramework.Workflows.Abstractions;
using CanDoItAll.Infrastructure.ControlPlane;
using CanDoItAll.Modules.AgentFramework;
using CanDoItAll.Modules.Projects;
using CanDoItAll.Modules.Workbench;
using CanDoItAll.Tests.Support;
using Microsoft.Agents.AI.Workflows;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace CanDoItAll.Tests.Integration.ProjectStructure;

public sealed partial class WorkflowStructureDeliveryPersistenceTests {
    [Theory]
    [InlineData(WorkflowProjectStructureOperation.ReadTree)]
    [InlineData(WorkflowProjectStructureOperation.ReadNode)]
    public async Task Workflow_read_fixed_target_cannot_bypass_original_scope_or_inherit_a_later_grant(WorkflowProjectStructureOperation operation) {
        await using var app = await TestApplication.CreateAsync(new() { ConfigureServices = RemoveAutomaticDelivery });
        await using var fixture = await CreateFixtureAsync(app);
        var other = await CreateReadProjectAsync(fixture.Services, "Private project");
        var source = await CreateReadSourceAsync(fixture, [fixture.ProjectId]);
        var settings = ReadSettings(operation, other.ProjectId);
        var read = await SaveReadInvocationAsync(fixture, source.Authority, settings, prepareFixedTargets: false);
        var gateway = fixture.Services.GetRequiredService<IProjectStructureRuntimeGateway>();
        Assert.Equal(other.ProjectId, (await gateway.ReadStructureAsync(other.ProjectId, new())).ProjectId);
        var input = JsonSerializer.Serialize(new { project = new { id = fixture.ProjectId } });
        var denied = await Assert.ThrowsAsync<WorkflowExecutorInvocationException>(() => InvokeReadAsync(gateway, read, input));
        Assert.Contains("exact read grants", denied.ToString(), StringComparison.Ordinal);
        await SetReadGrantsAsync(fixture.Services, source, [fixture.ProjectId, other.ProjectId]);
        await Assert.ThrowsAsync<WorkflowExecutorInvocationException>(() => InvokeReadAsync(gateway, read, input));
        await Assert.ThrowsAsync<ProjectStructureAgentException>(() => fixture.Services.GetRequiredService<ProjectStructureWorkflowAuthorityService>()
            .PrepareLaunchAsync(source.Authority, read.Definition));
        Assert.Equal(source.Authority.ProjectScope!.Projects, (await fixture.Services.GetRequiredService<IWorkflowRunStore>()
            .GetRunAsync(read.Run.RunId))!.Origin!.StructureAuthority!.ProjectScope!.Projects);
    }

    [Theory]
    [InlineData(WorkflowProjectStructureOperation.ReadTree)]
    [InlineData(WorkflowProjectStructureOperation.ReadNode)]
    public async Task Workflow_read_allows_original_cross_project_grant_and_rechecks_revocation_on_same_adapter(WorkflowProjectStructureOperation operation) {
        await using var app = await TestApplication.CreateAsync(new() { ConfigureServices = RemoveAutomaticDelivery });
        await using var fixture = await CreateFixtureAsync(app);
        var other = await CreateReadProjectAsync(fixture.Services, "Allowed cross-project");
        var source = await CreateReadSourceAsync(fixture, [fixture.ProjectId, other.ProjectId]);
        var read = await SaveReadInvocationAsync(fixture, source.Authority, ReadSettings(operation, other.ProjectId));
        var gateway = fixture.Services.GetRequiredService<IProjectStructureRuntimeGateway>();
        Assert.Contains("Allowed cross-project", (await InvokeReadAsync(gateway, read)).PayloadJson, StringComparison.Ordinal);
        await SetReadGrantsAsync(fixture.Services, source, [fixture.ProjectId]);
        await Assert.ThrowsAsync<WorkflowExecutorInvocationException>(() => InvokeReadAsync(gateway, read));
        await SetReadGrantsAsync(fixture.Services, source, [fixture.ProjectId, other.ProjectId]);
        Assert.Contains("Allowed cross-project", (await InvokeReadAsync(gateway, read)).PayloadJson, StringComparison.Ordinal);
        Assert.False(read.Run.Origin!.StructureAuthority!.CanCreateTasks);
        Assert.False(read.Run.Origin.StructureAuthority.CanCreateAssets);
    }

    [Fact]
    public async Task Workflow_project_list_intersects_original_and_current_grants_without_disclosing_newly_granted_names() {
        await using var app = await TestApplication.CreateAsync(new() { ConfigureServices = RemoveAutomaticDelivery });
        await using var fixture = await CreateFixtureAsync(app);
        var retained = await CreateReadProjectAsync(fixture.Services, "Original retained project");
        var later = await CreateReadProjectAsync(fixture.Services, "Later grant must stay private");
        var source = await CreateReadSourceAsync(fixture, [fixture.ProjectId, retained.ProjectId]);
        var read = await SaveReadInvocationAsync(fixture, source.Authority, ReadSettings(WorkflowProjectStructureOperation.ListProjects, null));
        await SetReadGrantsAsync(fixture.Services, source, [retained.ProjectId, later.ProjectId]);
        var gateway = fixture.Services.GetRequiredService<IProjectStructureRuntimeGateway>();
        var result = await InvokeReadAsync(gateway, read);
        using var json = JsonDocument.Parse(result.PayloadJson);
        var item = Assert.Single(json.RootElement.EnumerateArray());
        Assert.Equal(retained.ProjectId, item.GetProperty("id").GetGuid());
        Assert.DoesNotContain("Later grant", result.PayloadJson, StringComparison.Ordinal);
        Assert.DoesNotContain(fixture.ProjectId.ToString(), result.PayloadJson, StringComparison.OrdinalIgnoreCase);
        Assert.Contains(await gateway.ListProjectsAsync(), project => project.Id == later.ProjectId);
    }

    [Fact]
    public async Task Workflow_with_no_original_read_grant_cannot_gain_one_from_later_catalog_edits() {
        await using var app = await TestApplication.CreateAsync(new() { ConfigureServices = RemoveAutomaticDelivery });
        await using var fixture = await CreateFixtureAsync(app);
        var source = await CreateReadSourceAsync(fixture, []);
        Assert.False(AgentProjectStructureAccessMetadata.Read(source.Agent.ConfigurationJson).CanRead);
        Assert.Empty(source.Authority.ProjectScope!.Projects);
        var list = await SaveReadInvocationAsync(fixture, source.Authority, ReadSettings(WorkflowProjectStructureOperation.ListProjects, null));
        var direct = await SaveReadInvocationAsync(fixture, source.Authority, ReadSettings(WorkflowProjectStructureOperation.ReadTree, fixture.ProjectId),
            prepareFixedTargets: false);
        await SetReadGrantsAsync(fixture.Services, source, [fixture.ProjectId]);
        var gateway = fixture.Services.GetRequiredService<IProjectStructureRuntimeGateway>();
        using var result = JsonDocument.Parse((await InvokeReadAsync(gateway, list)).PayloadJson);
        Assert.Empty(result.RootElement.EnumerateArray());
        await Assert.ThrowsAsync<WorkflowExecutorInvocationException>(() => InvokeReadAsync(gateway, direct));
    }

    [Fact]
    public async Task Project_scoped_Workflow_cannot_use_other_organization_grants() {
        await using var app = await TestApplication.CreateAsync(new() { ConfigureServices = RemoveAutomaticDelivery });
        await using var fixture = await CreateFixtureAsync(app);
        var other = await CreateReadProjectAsync(fixture.Services, "Outside original source workspace");
        var source = await CreateReadSourceAsync(fixture, [fixture.ProjectId, other.ProjectId], projectWorkspace: true);
        Assert.Equal(fixture.ProjectId, Assert.Single(source.Authority.ProjectScope!.Projects).ProjectId);
        var read = await SaveReadInvocationAsync(fixture, source.Authority, ReadSettings(WorkflowProjectStructureOperation.ReadTree, other.ProjectId),
            prepareFixedTargets: false);
        await Assert.ThrowsAsync<WorkflowExecutorInvocationException>(() => InvokeReadAsync(fixture.Services.GetRequiredService<IProjectStructureRuntimeGateway>(), read));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Workflow_read_refuses_replaced_project_and_legacy_missing_lifetime_without_reminting(bool legacy) {
        await using var app = await TestApplication.CreateAsync(new() { ConfigureServices = RemoveAutomaticDelivery });
        await using var fixture = await CreateFixtureAsync(app);
        var authority = fixture.Request.WorkflowMutationAdmission!.Authority;
        var read = await SaveReadInvocationAsync(fixture, legacy ? authority with { ProjectScope = null } : authority,
            ReadSettings(WorkflowProjectStructureOperation.ReadTree, fixture.ProjectId), prepareFixedTargets: !legacy);
        if (!legacy) {
            _ = await RecreateProjectAsync(fixture);
        }
        await Assert.ThrowsAsync<WorkflowExecutorInvocationException>(() => InvokeReadAsync(fixture.Services.GetRequiredService<IProjectStructureRuntimeGateway>(), read));
        var saved = (await fixture.Services.GetRequiredService<IWorkflowRunStore>().GetRunAsync(read.Run.RunId))!;
        Assert.Equal(legacy, saved.Origin!.StructureAuthority!.ProjectScope is null);
        if (!legacy) {
            Assert.Equal(fixture.Plan.ProjectLifetime, saved.Origin.StructureAuthority.ProjectScope!.Find(fixture.ProjectId));
        }
    }

    [Fact]
    public async Task Workflow_read_revalidates_lifetime_after_the_actual_delayed_owner_query() {
        var gate = new WorkflowReadQueryGate();
        await using var app = await TestApplication.CreateAsync(new() { ConfigureServices = services => {
            RemoveAutomaticDelivery(services);
            services.RemoveAll<IDbContextFactory<WorkbenchDbContext>>();
            services.AddSingleton<IDbContextFactory<WorkbenchDbContext>>(provider => Factory(provider, gate));
        } });
        await using var fixture = await CreateFixtureAsync(app);
        var read = await SaveReadInvocationAsync(fixture, fixture.Request.WorkflowMutationAdmission!.Authority,
            ReadSettings(WorkflowProjectStructureOperation.ReadTree, fixture.ProjectId));
        gate.Arm();
        var pending = InvokeReadAsync(fixture.Services.GetRequiredService<IProjectStructureRuntimeGateway>(), read);
        try {
            await gate.Entered.Task.WaitAsync(TimeSpan.FromSeconds(15));
            var replacement = await RecreateProjectAsync(fixture);
            Assert.NotEqual(fixture.Plan.ProjectLifetime!.LifetimeId, replacement.LifetimeId);
        } finally {
            gate.Release.TrySetResult();
        }
        await Assert.ThrowsAsync<WorkflowExecutorInvocationException>(() => pending.WaitAsync(TimeSpan.FromSeconds(15)));
        Assert.True(gate.Observed);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Installed_MAF_adapter_preserves_numeric_and_string_read_settings_and_trusted_occurrence(bool numericSettings) {
        await using var app = await TestApplication.CreateAsync(new() { ConfigureServices = RemoveAutomaticDelivery });
        await using var fixture = await CreateFixtureAsync(app);
        var settings = ReadSettings(WorkflowProjectStructureOperation.ReadTree, fixture.ProjectId) with { IncludeInputPayload = true };
        var read = await SaveReadInvocationAsync(fixture, fixture.Request.WorkflowMutationAdmission!.Authority, settings, numericSettings: numericSettings);
        var gateway = new WorkflowReadProbe(fixture.Services.GetRequiredService<IProjectStructureRuntimeGateway>());
        var executor = new ProjectStructureWorkflowExecutor(gateway);
        var catalog = new WorkflowExecutorCatalog([executor]);
        var compiler = new MafWorkflowCompiler(new WorkflowDefinitionValidator(catalog), new WorkflowExecutorInvoker(catalog, [executor]), executorCatalog: catalog);
        var compiled = compiler.Compile(read.Definition, []);
        Assert.True(compiled.Compilation.Succeeded, compiled.Compilation.ErrorMessage);
        using var source = WorkflowExecutorExecutionAuditScope.Push(read.Run.RunId, read.Run.Origin);
        using var progress = WorkflowNodeExecutionProgressScope.Push(new ReadProofProgressObserver(
            fixture.Services.GetRequiredService<IWorkflowRunStore>()));
        using var cancellation = new CancellationTokenSource(TimeSpan.FromSeconds(15));
        await using var execution = await InProcessExecution.RunStreamingAsync(Assert.IsType<Workflow>(compiled.Workflow),
            new WorkflowNodeInput("{\"retainedInput\":true}") { ExecutionOccurrence = WorkflowExecutionOccurrence.Start(read.Run.RunId) },
            cancellationToken: cancellation.Token);
        var events = new List<WorkflowEvent>();
        await foreach (var item in execution.WatchStreamAsync(blockOnPendingRequest: false, cancellation.Token)) {
            events.Add(item);
        }
        Assert.DoesNotContain(events, item => item is WorkflowErrorEvent);
        Assert.NotNull(gateway.ReadContext);
        Assert.Equal(WorkflowExecutionOccurrence.Start(read.Run.RunId).Advance(read.Definition.VersionId, new("start"))
            .Advance(read.Definition.VersionId, read.Node.Id), gateway.ReadContext.Occurrence);
        Assert.Equal(fixture.ProjectId, gateway.Result!.ProjectId);
        Assert.Contains(events.OfType<WorkflowOutputEvent>(), item => item.Is<WorkflowNodeInput>(out var output) &&
            output.PayloadJson.Contains("retainedInput", StringComparison.Ordinal) && output.PayloadJson.Contains("inputPayload", StringComparison.Ordinal));
        Assert.Equal(WorkflowExecutorIds.ProjectStructure, executor.Descriptor.Id);
    }

    [Theory]
    [InlineData(ReadContextMismatch.Run)]
    [InlineData(ReadContextMismatch.Version)]
    [InlineData(ReadContextMismatch.Origin)]
    [InlineData(ReadContextMismatch.Cancelled)]
    public async Task Workflow_owner_refuses_a_caller_selected_run_version_source_or_cancelled_execution(ReadContextMismatch mismatch) {
        await using var app = await TestApplication.CreateAsync(new() { ConfigureServices = RemoveAutomaticDelivery });
        await using var fixture = await CreateFixtureAsync(app);
        var read = await SaveReadInvocationAsync(fixture, fixture.Request.WorkflowMutationAdmission!.Authority,
            ReadSettings(WorkflowProjectStructureOperation.ReadTree, fixture.ProjectId));
        if (mismatch == ReadContextMismatch.Cancelled) {
            await fixture.Services.GetRequiredService<IWorkflowRunStore>().SaveRunAsync(read.Run with { State = WorkflowRunState.Cancelled });
        }
        var origin = mismatch == ReadContextMismatch.Origin
            ? read.Run.Origin! with { StructureAuthority = read.Run.Origin!.StructureAuthority! with {
                Principal = new(WorkflowLaunchActorKind.User, "different-source") } } : read.Run.Origin;
        using var audit = WorkflowExecutorExecutionAuditScope.Push(mismatch == ReadContextMismatch.Run ? WorkflowRunId.New() : read.Run.RunId, origin);
        var context = new WorkflowStructureReadContext(WorkflowExecutionOccurrence.Start(read.Run.RunId),
            mismatch == ReadContextMismatch.Version ? WorkflowVersionId.New() : read.Definition.VersionId, read.Node.Id);
        await Assert.ThrowsAsync<InvalidOperationException>(() => fixture.Services.GetRequiredService<IProjectStructureRuntimeGateway>()
            .ReadWorkflowStructureAsync(fixture.ProjectId, new(), context));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Manual_workflow_read_preserves_UI_and_current_API_authority(bool apiSource) {
        var policy = new MutableApiPolicy();
        await using var app = await TestApplication.CreateAsync(new() { ConfigureServices = services => {
            RemoveAutomaticDelivery(services);
            services.AddSingleton<Microsoft.Extensions.Options.IOptionsMonitor<CanDoItAll.Modules.Workspace.ApiAccess.ApiAccessOptions>>(policy);
        } });
        await using var fixture = await CreateFixtureAsync(app, sourceSurface: apiSource
            ? WorkflowStructureOperatorSurface.Api : WorkflowStructureOperatorSurface.UserInterface);
        var read = await SaveReadInvocationAsync(fixture, fixture.Request.WorkflowMutationAdmission!.Authority,
            ReadSettings(WorkflowProjectStructureOperation.ReadTree, fixture.ProjectId));
        var gateway = fixture.Services.GetRequiredService<IProjectStructureRuntimeGateway>();
        _ = await InvokeReadAsync(gateway, read);
        policy.CurrentValue.Enabled = false;
        if (apiSource) {
            await Assert.ThrowsAsync<WorkflowExecutorInvocationException>(() => InvokeReadAsync(gateway, read));
        } else {
            Assert.Contains(fixture.ProjectId.ToString("D"), (await InvokeReadAsync(gateway, read)).PayloadJson, StringComparison.OrdinalIgnoreCase);
        }
    }

    public enum ReadContextMismatch { Run, Version, Origin, Cancelled }

    private static WorkflowProjectStructureExecutorSettings ReadSettings(WorkflowProjectStructureOperation operation, Guid? projectId)
        => new() { Operation = operation, ProjectId = projectId,
            NodeId = projectId.HasValue ? ProjectWorkbenchGraphConventions.BuildProjectRootNodeKey(projectId.Value) : "" };

    private static async Task<ProjectWriteAdmission> CreateReadProjectAsync(IServiceProvider services, string name) {
        var created = await services.GetRequiredService<ProjectsService>().SaveAsync(new() { Name = name, CurrentPhase = "Planning" });
        Assert.True(created.IsSuccess);
        return Assert.IsType<ProjectWriteAdmission>(await services.GetRequiredService<ProjectWriteAdmissionService>().CaptureAsync(created.Value));
    }

    private static async Task<ReadAgentSource> CreateReadSourceAsync(Fixture fixture, IReadOnlyList<Guid> projectIds, bool projectWorkspace = false) {
        var services = fixture.Services;
        var profile = services.GetRequiredService<ICanonicalRuntimeDatabase>();
        var writer = CreateReadWriter(services);
        var catalog = await writer.LoadCatalogAsync();
        var capability = Assert.Single(catalog.Capabilities, item => item.Kind == CapabilityKind.Tool && item.Key == WorkflowRuntimeCapabilityKeys.RunStart);
        var agent = catalog.Agents.First() with { Id = Guid.NewGuid(), Name = "Workflow read source", IsTemplate = false, TemplateKey = "",
            ConfigurationJson = "{}", Status = AgentLifecycleStatus.Active, Permissions = AgentPermissionsPolicy.Default with { CanUseTools = true },
            Capabilities = [new(capability.Id, capability.Key, capability.Kind, capability.ProofStatus, capability.LastVerifiedAtUtc, capability.ProofNotes)] };
        await writer.UpdateCatalogAsync(current => current with { Agents = [.. current.Agents, agent] });
        var temporary = new ReadAgentSource(agent, null!);
        agent = await SetReadGrantsAsync(services, temporary, projectIds);
        var governance = new AgentExecutionGovernanceSnapshot(new(Guid.NewGuid()), agent.Id, profile.Profile.Profile.Id,
            services.GetRequiredService<IAgentExecutionProfileGenerationSource>().GetGeneration(), projectWorkspace
                ? WorkspaceScopeDescriptor.Project(fixture.ProjectId.ToString("D")) : WorkspaceScopeDescriptor.Organization(profile.Profile.Profile.Id.ToString("N")),
            true, true, "workflow-read-test-v1", "workflow-read-test-policy", [WorkflowToolPolicy.WorkflowsRunStart], [WorkflowRuntimeCapabilityKeys.RunStart]);
        var authority = await services.GetRequiredService<ProjectStructureWorkflowAuthorityService>().CaptureAgentAsync(agent, governance);
        authority = authority with { ProjectScope = new(authority.ProjectScope!.Projects, authority.ProjectScope.AdmissionProjectIds,
            workflowStartCapabilityId: capability.Id) };
        return new(agent, authority);
    }

    private static FileSandboxWorkspaceStore CreateReadWriter(IServiceProvider services) {
        var profile = services.GetRequiredService<ICanonicalRuntimeDatabase>().Profile.Profile;
        return new(profile.Storage.WorkspaceRoot, WorkspaceScopeDescriptor.Organization(profile.Id.ToString("N")),
            new AgentProjectAccessCatalogPolicy(services.GetRequiredService<ProjectWriteAdmissionService>(), profile.Id));
    }

    private static async Task<AgentDefinition> SetReadGrantsAsync(IServiceProvider services, ReadAgentSource source, IReadOnlyList<Guid> ids) {
        var writer = CreateReadWriter(services);
        var admissions = await services.GetRequiredService<ProjectWriteAdmissionService>().CaptureManyAsync(ids);
        var settings = new AgentProjectStructureAccessSettings { AllowedProjectIds = ids.ToList(),
            AllowedProjectLifetimes = admissions.Select(item => new AgentProjectStructureLifetime(item.DatabaseProfileId, item.ProjectId, item.LifetimeId)).ToList() };
        await writer.UpdateCatalogAsync(catalog => catalog with { Agents = catalog.Agents.Select(agent => agent.Id == source.Agent.Id
            ? agent with { ConfigurationJson = AgentProjectStructureAccessMetadata.Write("{}", settings) } : agent).ToArray() });
        var current = Assert.Single((await writer.LoadCatalogAsync()).Agents, agent => agent.Id == source.Agent.Id);
        Assert.Equal(ids.Order(), AgentProjectStructureAccessMetadata.Read(current.ConfigurationJson).AllowedProjectIds.Order());
        return current;
    }

    private static async Task<ReadInvocation> SaveReadInvocationAsync(Fixture fixture, WorkflowStructureAuthority authority,
        WorkflowProjectStructureExecutorSettings settings, bool prepareFixedTargets = true, bool numericSettings = false) {
        var definition = ReadDefinition(fixture.Definition, settings, numericSettings);
        if (prepareFixedTargets) {
            authority = await fixture.Services.GetRequiredService<ProjectStructureWorkflowAuthorityService>().PrepareLaunchAsync(authority, definition);
        }
        var node = Assert.Single(definition.Graph.Nodes, item => item.Kind == WorkflowNodeKind.Executor);
        var now = DateTimeOffset.UtcNow;
        WorkflowLaunchOrigin origin = authority.Channel == WorkflowStructureAuthorityChannel.AgentExecution
            ? new WorkflowLaunchOrigin.AgentRuntimeInvocation(authority.Principal, new("workflow-read-session"), AgentRuntimeToolProviderPurpose.InteractiveChat.ToString(), new(Guid.NewGuid()))
            : new WorkflowLaunchOrigin.Preview(authority.Principal, new("workflow-read-preview"));
        origin = origin with { StructureAuthority = authority };
        var run = new WorkflowRunSnapshot(WorkflowRunId.New(), definition.Id, definition.VersionId, WorkflowRunState.Running,
            WorkflowRuntimeBackendKind.InProcess, "workflow-read", "Running", now, now) { Origin = origin };
        await fixture.Services.GetRequiredService<IWorkflowRunStore>().CreateRunWithStartedEventAsync(run,
            new(Guid.NewGuid(), run.RunId, WorkflowEventKind.Started, null, "Read", "{}", now) {
                DisclosureDeclaration = new(run.RunId, definition.Id, definition.VersionId,
                    WorkflowProviderDisclosureContent.Definition(definition), WorkflowProviderDisclosureContent.Source(origin),
                    WorkflowProviderDisclosureProtocol.Current)
            });
        return new(run, definition, node);
    }

    private static WorkflowDefinition ReadDefinition(WorkflowDefinition source, WorkflowProjectStructureExecutorSettings settings, bool numericSettings) {
        var start = source.Graph.Nodes.Single(node => node.Kind == WorkflowNodeKind.Start);
        var end = source.Graph.Nodes.Single(node => node.Kind == WorkflowNodeKind.End);
        var json = new WorkflowValueShape(WorkflowValueShapeKind.Json, "{}", "JSON");
        var read = new WorkflowNode(new("read"), WorkflowNodeKind.Executor, "Read Structure", [],
            new(null, null, null, null, "", WorkflowValueShape.Text, json) {
                ExecutorId = WorkflowExecutorIds.ProjectStructure,
                ExecutorSettingsJson = numericSettings ? JsonSerializer.Serialize(settings, new JsonSerializerOptions(JsonSerializerDefaults.Web))
                    : WorkflowExecutorJson.Serialize(settings), ExecutionPolicy = WorkflowExecutorExecutionPolicy.Default
            });
        end = end with { Settings = end.Settings with { InputShape = json, ResultShape = json } };
        return source with { Graph = new(start.Id, [start, read, end], [
            new(new("start-read"), start.Id, null, read.Id, null, WorkflowEdgeKind.Direct, ""),
            new(new("read-end"), read.Id, null, end.Id, null, WorkflowEdgeKind.Direct, "")]) };
    }

    private static async Task<WorkflowNodeExecutionResult> InvokeReadAsync(IProjectStructureRuntimeGateway gateway, ReadInvocation read, string input = "{}") {
        var executor = new ProjectStructureWorkflowExecutor(gateway);
        var invoker = new WorkflowExecutorInvoker(new WorkflowExecutorCatalog([executor]), [executor]);
        using var scope = WorkflowExecutorExecutionAuditScope.Push(read.Run.RunId, read.Run.Origin);
        return await invoker.ExecuteAsync(read.Definition, read.Node, new(input), new WorkflowExecutorInvocationContext {
            ExecutionOccurrence = WorkflowExecutionOccurrence.Start(read.Run.RunId).Advance(read.Definition.VersionId, read.Node.Id)
        });
    }

    private sealed record ReadAgentSource(AgentDefinition Agent, WorkflowStructureAuthority Authority);
    private sealed record ReadInvocation(WorkflowRunSnapshot Run, WorkflowDefinition Definition, WorkflowNode Node);

    private sealed class WorkflowReadQueryGate : DbCommandInterceptor {
        private int armed;
        public bool Observed { get; private set; }
        public TaskCompletionSource Entered { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource Release { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public void Arm() => Volatile.Write(ref armed, 1);
        public override async ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(DbCommand command, CommandEventData eventData,
            InterceptionResult<DbDataReader> result, CancellationToken cancellationToken = default) {
            if (command.CommandText.Contains("Workbench_ProjectObjects", StringComparison.Ordinal) && Interlocked.Exchange(ref armed, 0) == 1) {
                Observed = true;
                Entered.TrySetResult();
                await Release.Task.WaitAsync(TimeSpan.FromSeconds(15), cancellationToken);
            }
            return result;
        }
    }

    private sealed class WorkflowReadProbe(IProjectStructureRuntimeGateway inner) : IProjectStructureRuntimeGateway {
        public WorkflowStructureReadContext? ReadContext { get; private set; }
        public ProjectStructureRuntimeReadResponse? Result { get; private set; }
        public async Task<ProjectStructureRuntimeReadResponse> ReadWorkflowStructureAsync(Guid projectId, ProjectStructureRuntimeReadRequest request,
            WorkflowStructureReadContext context, CancellationToken cancellationToken = default) {
            ReadContext = context;
            Result = await inner.ReadWorkflowStructureAsync(projectId, request, context, cancellationToken);
            return Result;
        }
        public Task<IReadOnlyList<ProjectStructureRuntimeProjectSummary>> ListWorkflowProjectsAsync(WorkflowStructureReadContext context,
            CancellationToken cancellationToken = default) => inner.ListWorkflowProjectsAsync(context, cancellationToken);
        public Task<IReadOnlyList<ProjectStructureRuntimeProjectSummary>> ListProjectsAsync(CancellationToken cancellationToken = default)
            => throw new InvalidOperationException("The installed adapter bypassed the admitted owner read.");
        public Task<ProjectStructureRuntimeReadResponse> ReadStructureAsync(Guid projectId, ProjectStructureRuntimeReadRequest request,
            CancellationToken cancellationToken = default) => throw new InvalidOperationException("The installed adapter bypassed the admitted owner read.");
        public Task<ProjectStructureRuntimeNodeSummary> CreateNodeAsync(Guid projectId, ProjectStructureRuntimeNodeCreateRequest request,
            ProjectStructureRuntimeAgentContext agent, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<ProjectStructureRuntimeNodeSummary> CreateAssetAsync(Guid projectId, ProjectStructureRuntimeAssetCreateRequest request,
            ProjectStructureRuntimeAgentContext agent, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    }
}
