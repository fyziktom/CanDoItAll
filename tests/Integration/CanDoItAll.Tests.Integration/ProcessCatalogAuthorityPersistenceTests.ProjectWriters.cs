using CanDoItAll.SharedKernel;
using System.Data.Common;
using System.Text.Json;
using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.AgentFramework.Tooling;
using CanDoItAll.Infrastructure.Persistence;
using CanDoItAll.Modules.Projects;
using CanDoItAll.Modules.Workbench;
using CanDoItAll.Processes.Application;
using CanDoItAll.Processes.Persistence;
using CanDoItAll.Processes.Runtime;
using CanDoItAll.Tests.Support;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;

namespace CanDoItAll.Tests.Integration.Processes;

public sealed partial class ProcessCatalogAuthorityPersistenceTests {
    public enum ProcessProjectWrite { Update, Root, Child, Hierarchy }

    [Theory]
    [InlineData(ProcessProjectWrite.Update)]
    [InlineData(ProcessProjectWrite.Root)]
    [InlineData(ProcessProjectWrite.Child)]
    [InlineData(ProcessProjectWrite.Hierarchy)]
    public async Task Process_Project_writer_rechecks_the_original_claim_after_real_owner_flush(ProcessProjectWrite kind) {
        var clock = new NativeClock();
        var probe = new ProcessProjectFlushProbe();
        await using var app = await TestApplication.CreateAsync(ProcessProjectsHarness(clock, probe));
        await using var scope = app.Services.CreateAsyncScope();
        var services = scope.ServiceProvider;
        var fixture = await Fixture.CreateAsync(services);
        var authority = await CaptureProjectOperationsAsync(fixture);
        var run = await CreateClaimedExecutionAsync(services, fixture, clock, authority: authority);
        var admission = (await services.GetRequiredService<ProjectProcessExecutionMutationService>().ObserveAsync(run.Id))!;
        var write = await PrepareProcessProjectWriteAsync(services, admission, kind);
        var before = await ProjectWriterStateAsync(services);
        probe.AfterFlush = () => clock.Now = clock.Now.AddHours(2);
        await Assert.ThrowsAsync<ProcessExecutionAuthorityMismatchException>(write);
        Assert.Equal(1, probe.ArmedFlushes);
        Assert.Equal(before, await ProjectWriterStateAsync(services));
        await fixture.Writer.UpdateCatalogAsync(catalog => catalog).WaitAsync(TimeSpan.FromSeconds(10));
    }

    [Theory]
    [InlineData(ProcessProjectWrite.Update)]
    [InlineData(ProcessProjectWrite.Root)]
    [InlineData(ProcessProjectWrite.Child)]
    [InlineData(ProcessProjectWrite.Hierarchy)]
    public async Task Process_Project_writer_rejects_current_source_revocation_before_any_owner_change(ProcessProjectWrite kind) {
        var clock = new NativeClock();
        await using var app = await TestApplication.CreateAsync(ProcessProjectsHarness(clock));
        await using var scope = app.Services.CreateAsyncScope();
        var services = scope.ServiceProvider;
        var fixture = await Fixture.CreateAsync(services);
        var run = await CreateClaimedExecutionAsync(services, fixture, clock, authority: await CaptureProjectOperationsAsync(fixture));
        var admission = (await services.GetRequiredService<ProjectProcessExecutionMutationService>().ObserveAsync(run.Id))!;
        var write = await PrepareProcessProjectWriteAsync(services, admission, kind);
        var before = await ProjectWriterStateAsync(services);
        await fixture.RevokeAsync(Revocation.Write);
        await Assert.ThrowsAsync<ProcessLaunchAuthorityRejectedException>(write);
        Assert.Equal(before, await ProjectWriterStateAsync(services));
    }

    [Fact]
    public async Task Process_creation_and_hierarchy_need_saved_permissions_while_legacy_original_project_save_remains_supported() {
        var clock = new NativeClock();
        await using var app = await TestApplication.CreateAsync(ProcessProjectsHarness(clock));
        await using var scope = app.Services.CreateAsyncScope();
        var services = scope.ServiceProvider;
        var fixture = await Fixture.CreateAsync(services);
        var authority = await CaptureProjectOperationsAsync(fixture);
        var run = await CreateClaimedExecutionAsync(services, fixture, clock, authority: authority with { ProjectMutations = null });
        var service = services.GetRequiredService<ProjectProcessExecutionMutationService>();
        var admission = (await service.ObserveAsync(run.Id))!;
        await (await PrepareProcessProjectWriteAsync(services, admission, ProcessProjectWrite.Update))();
        Assert.Equal("Process owner edit", (await services.GetRequiredService<ProjectsService>().GetAsync(fixture.Project.ProjectId)).Name);
        var before = await ProjectWriterStateAsync(services);
        foreach (var operation in new[] { ProjectProcessProjectOperation.CreateRoot, ProjectProcessProjectOperation.CreateChild,
                     ProjectProcessProjectOperation.MoveToChild }) {
            var child = operation != ProjectProcessProjectOperation.CreateRoot;
            var authorization = service.BindProjectSource(admission, child ? [admission.ProjectAdmission] : [], operation);
            var failure = await Assert.ThrowsAsync<ProcessLaunchAuthorityRejectedException>(() => services.GetRequiredService<ProjectWriteAdmissionService>()
                .ReserveCreationAsync(Guid.NewGuid(), run.AgentId, Guid.NewGuid(), child ? fixture.Project.ProjectId : null, authorization: authorization));
            Assert.Contains("no original project-creation", failure.Message, StringComparison.Ordinal);
        }
        var hierarchy = service.BindProjectSource(admission, [admission.ProjectAdmission], ProjectProcessProjectOperation.ChangeHierarchy);
        await Assert.ThrowsAsync<ProcessLaunchAuthorityRejectedException>(() => hierarchy.SourceAuthority.AcquireAsync(
            new(ProjectMutationPurpose.HierarchyWrite, fixture.Project.ProjectId, [admission.ProjectAdmission])));
        Assert.Equal(before, await ProjectWriterStateAsync(services));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Process_creation_grant_revocation_denies_that_effect_without_removing_authorized_original_project_edits(bool child) {
        var clock = new NativeClock();
        await using var app = await TestApplication.CreateAsync(ProcessProjectsHarness(clock));
        await using var scope = app.Services.CreateAsyncScope();
        var services = scope.ServiceProvider;
        var fixture = await Fixture.CreateAsync(services);
        var run = await CreateClaimedExecutionAsync(services, fixture, clock, authority: await CaptureProjectOperationsAsync(fixture));
        var admission = (await services.GetRequiredService<ProjectProcessExecutionMutationService>().ObserveAsync(run.Id))!;
        var create = await PrepareProcessProjectWriteAsync(services, admission, child ? ProcessProjectWrite.Child : ProcessProjectWrite.Root);
        var before = await ProjectWriterStateAsync(services);
        await fixture.Writer.UpdateCatalogAsync(catalog => catalog with { Agents = catalog.Agents.Select(agent => {
            if (agent.Id != fixture.Agent.Id) {
                return agent;
            }
            var access = AgentProjectStructureAccessMetadata.Read(agent.ConfigurationJson);
            if (child) {
                access.CanCreateSubprojects = false;
            } else {
                access.CanCreateProjects = false;
            }
            return agent with { ConfigurationJson = AgentProjectStructureAccessMetadata.Write(agent.ConfigurationJson, access) };
        }).ToArray() });
        await Assert.ThrowsAsync<ProcessLaunchAuthorityRejectedException>(create);
        Assert.Equal(before, await ProjectWriterStateAsync(services));
        await (await PrepareProcessProjectWriteAsync(services, admission, ProcessProjectWrite.Update))();
        Assert.Equal("Process owner edit", (await services.GetRequiredService<ProjectsService>().GetAsync(fixture.Project.ProjectId)).Name);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Registered_governed_Project_creation_uses_original_source_permissions_not_executor_configuration(bool sourceAllows) {
        var clock = new NativeClock();
        await using var app = await TestApplication.CreateAsync(ProcessProjectsHarness(clock));
        await using var scope = app.Services.CreateAsyncScope();
        var services = scope.ServiceProvider;
        var fixture = await Fixture.CreateAsync(services);
        var authority = sourceAllows ? await CaptureProjectOperationsAsync(fixture) : fixture.SavedAuthority;
        var run = await CreateClaimedExecutionAsync(services, fixture, clock, authority: authority);
        var context = await ProjectProducerContextAsync(services, fixture, run);
        Assert.Null(context.Governance);
        Assert.NotEqual(fixture.Agent.Id, context.Agent.Id);
        Assert.True(AgentProjectStructureAccessMetadata.Read(context.Agent.ConfigurationJson).CanCreateProjects);
        using var audit = WorkspaceExecutionAuditContext.BeginScope(run);
        var provider = Assert.Single(services.GetServices<IAgentRuntimeToolProvider>().OfType<ProjectStructureAgentRuntimeToolProvider>());
        var tools = await provider.CreateToolsAsync(context, default);
        var create = tools.OfType<AIFunction>().SingleOrDefault(tool => tool.Name == ProjectStructureToolPolicy.ProjectStructureProjectCreate);
        if (!sourceAllows) {
            Assert.Null(create);
            var admission = (await services.GetRequiredService<ProjectProcessExecutionMutationService>().ObserveAsync(run.Id))!;
            var authorization = services.GetRequiredService<ProjectProcessExecutionMutationService>()
                .BindProjectSource(admission, [], ProjectProcessProjectOperation.CreateRoot);
            await Assert.ThrowsAsync<ProcessLaunchAuthorityRejectedException>(() => services.GetRequiredService<ProjectWriteAdmissionService>()
                .ReserveCreationAsync(Guid.NewGuid(), run.AgentId, Guid.NewGuid(), authorization: authorization));
            await using var none = await services.GetRequiredService<IDbContextFactory<ProjectsDbContext>>().CreateDbContextAsync();
            Assert.False(await none.Set<ProjectCreationReservationRecord>().AnyAsync(row => row.RequesterId == run.AgentId));
            return;
        }
        Assert.NotNull(create);
        using var ownership = AgentRuntimeToolOwnershipContext.BeginScope(new(provider.Descriptor.ProviderKey, provider.Descriptor.DisplayName, create.Name));
        var created = ReadRegisteredToolResult<ProjectSummary>(create, await create.InvokeAsync(new AIFunctionArguments {
            ["request"] = new ProjectStructureProjectSaveRequest("Governed original source creation", "", "", "")
        }));
        var update = Assert.Single(tools.OfType<AIFunction>(), tool => tool.Name == ProjectStructureToolPolicy.ProjectStructureProjectUpdate);
        var edited = ReadRegisteredToolResult<ProjectSummary>(update, await update.InvokeAsync(new AIFunctionArguments {
            ["projectId"] = created.Id, ["request"] = new ProjectStructureProjectSaveRequest("Same-session retained target", "", "", "")
        }));
        Assert.Equal(created.Id, edited.Id);
        Assert.Equal("Same-session retained target", (await services.GetRequiredService<ProjectsService>().GetAsync(created.Id)).Name);
        await using var read = await services.GetRequiredService<IDbContextFactory<ProjectsDbContext>>().CreateDbContextAsync();
        var reservation = await read.Set<ProjectCreationReservationRecord>().SingleAsync(row => row.ProjectId == created.Id);
        Assert.Equal(run.AgentId, reservation.RequesterId);
        Assert.Equal(ProjectCreationReservationState.Consumed, reservation.State);
        Assert.Equal(reservation.LifetimeId, (await read.Set<Project>().SingleAsync(row => row.Id == created.Id)).LifetimeId);
        Assert.Equal(fixture.Agent.Id, Assert.IsType<ProcessLaunchPrincipal.AgentExecution>(authority.Principal).Ceiling.AgentId);
    }

    [Fact]
    public async Task Governed_move_to_reserved_child_keeps_both_native_targets_and_compensation_bound_to_the_original_claim() {
        var clock = new NativeClock();
        await using var app = await TestApplication.CreateAsync(ProcessProjectsHarness(clock));
        await using var scope = app.Services.CreateAsyncScope();
        var services = scope.ServiceProvider;
        var fixture = await Fixture.CreateAsync(services);
        var run = await CreateClaimedExecutionAsync(services, fixture, clock, authority: await CaptureProjectOperationsAsync(fixture));
        var context = await ProjectProducerContextAsync(services, fixture, run);
        var workbench = services.GetRequiredService<ProjectWorkbenchService>();
        var node = await workbench.CreateObjectAsync(fixture.Project.ProjectId, new(ProjectObjectType.ProjectBlock, "Move this exact node", "", "", null));
        using var audit = WorkspaceExecutionAuditContext.BeginScope(run);
        var provider = Assert.Single(services.GetServices<IAgentRuntimeToolProvider>().OfType<ProjectStructureAgentRuntimeToolProvider>());
        var tool = Assert.Single((await provider.CreateToolsAsync(context, default)).OfType<AIFunction>(),
            candidate => candidate.Name == ProjectStructureToolPolicy.ProjectStructureNodesToNewSubproject);
        using var ownership = AgentRuntimeToolOwnershipContext.BeginScope(new(provider.Descriptor.ProviderKey, provider.Descriptor.DisplayName, tool.Name));
        var result = ReadRegisteredToolResult<ProjectStructureNodesToSubprojectResult>(tool, await tool.InvokeAsync(new AIFunctionArguments {
            ["projectId"] = fixture.Project.ProjectId,
            ["request"] = new ProjectStructureNodesToSubprojectInput("Process reserved child", [node.Id])
        }));
        Assert.Contains(node.Id, result.MovedNodeIds);
        await using var native = await services.GetRequiredService<IDbContextFactory<WorkbenchDbContext>>().CreateDbContextAsync();
        Assert.Equal(result.TargetProjectId, (await native.Set<ProjectObjectRecord>().SingleAsync(row => row.NodeKey == node.Id)).ProjectId);
        await using var projects = await services.GetRequiredService<IDbContextFactory<ProjectsDbContext>>().CreateDbContextAsync();
        Assert.Equal(ProjectCreationReservationState.Consumed, (await projects.Set<ProjectCreationReservationRecord>()
            .SingleAsync(row => row.ProjectId == result.TargetProjectId)).State);
    }

    [Fact]
    public async Task Retained_created_target_cannot_be_rebound_to_a_recreated_ID_or_an_unrelated_existing_project() {
        var clock = new NativeClock();
        await using var app = await TestApplication.CreateAsync(ProcessProjectsHarness(clock));
        await using var scope = app.Services.CreateAsyncScope();
        var services = scope.ServiceProvider;
        var fixture = await Fixture.CreateAsync(services);
        var run = await CreateClaimedExecutionAsync(services, fixture, clock, authority: await CaptureProjectOperationsAsync(fixture));
        var service = services.GetRequiredService<ProjectProcessExecutionMutationService>();
        var original = (await service.ObserveAsync(run.Id))!;
        var (reservation, receipt) = await CreateProcessTargetAsync(services, original, false);
        var captured = original.WithCreatedProject(reservation);
        var projects = services.GetRequiredService<ProjectsService>();
        await projects.DeleteAsync(receipt.Project.ProjectId);
        Assert.True((await projects.CreateAsync(receipt.Project.ProjectId, new() { Name = "Recreated target" })).IsSuccess);
        var editor = await projects.GetAsync(receipt.Project.ProjectId);
        editor.Name = "Must not overwrite recreated target";
        var authorization = service.BindProjectSource(captured, [receipt.Project], ProjectProcessProjectOperation.Update);
        var failure = await Assert.ThrowsAsync<InvalidOperationException>(() => projects.SaveAsync(editor, authorization: authorization));
        Assert.Contains("exact created project reservation", failure.Message, StringComparison.Ordinal);
        Assert.Equal("Recreated target", (await projects.GetAsync(receipt.Project.ProjectId)).Name);
        var unrelated = (await projects.CreateWithReceiptAsync(Guid.NewGuid(), new() { Name = "Unrelated target" })).Value!.Project;
        var denied = service.BindProjectSource(original, [unrelated], ProjectProcessProjectOperation.Update);
        await Assert.ThrowsAsync<ProcessLaunchAuthorityRejectedException>(() => denied.SourceAuthority.AcquireAsync(
            new(ProjectMutationPurpose.ExistingWrite, unrelated.ProjectId, [unrelated])));
    }

    [Fact]
    public async Task Process_creation_compensation_refuses_an_expired_original_claim_and_retains_consumed_evidence() {
        var clock = new NativeClock();
        await using var app = await TestApplication.CreateAsync(ProcessProjectsHarness(clock));
        await using var scope = app.Services.CreateAsyncScope();
        var services = scope.ServiceProvider;
        var fixture = await Fixture.CreateAsync(services);
        var run = await CreateClaimedExecutionAsync(services, fixture, clock, authority: await CaptureProjectOperationsAsync(fixture));
        var original = (await services.GetRequiredService<ProjectProcessExecutionMutationService>().ObserveAsync(run.Id))!;
        var (reservation, receipt) = await CreateProcessTargetAsync(services, original, true);
        clock.Now = clock.Now.AddHours(2);
        await Assert.ThrowsAsync<ProcessExecutionAuthorityMismatchException>(() => services.GetRequiredService<ProjectsService>().TryCompensateCreationAsync(receipt));
        await using var read = await services.GetRequiredService<IDbContextFactory<ProjectsDbContext>>().CreateDbContextAsync();
        Assert.True(await read.Set<Project>().AnyAsync(row => row.Id == receipt.Project.ProjectId && row.LifetimeId == receipt.Project.LifetimeId));
        Assert.Equal(ProjectCreationReservationState.Consumed, (await read.Set<ProjectCreationReservationRecord>().SingleAsync(row => row.Id == reservation.Id)).State);
    }

    [Fact]
    public async Task Process_Project_owner_keeps_source_catalog_and_exact_shared_transaction_until_commit() {
        var clock = new NativeClock();
        var owner = new ProcessProjectFlushProbe();
        var process = new NativeGuardProbe();
        await using var app = await TestApplication.CreateAsync(ProcessProjectsHarness(clock, owner, process));
        await using var scope = app.Services.CreateAsyncScope();
        var services = scope.ServiceProvider;
        var fixture = await Fixture.CreateAsync(services);
        var run = await CreateClaimedExecutionAsync(services, fixture, clock, authority: await CaptureProjectOperationsAsync(fixture));
        var admission = (await services.GetRequiredService<ProjectProcessExecutionMutationService>().ObserveAsync(run.Id))!;
        var write = await PrepareProcessProjectWriteAsync(services, admission, ProcessProjectWrite.Update);
        var held = new CatalogCommitProbe(fixture.Writer);
        owner.Observer = held;
        process.Transactions.Clear();
        await write();
        Assert.True(held.BeforeCommit);
        Assert.True(held.AfterCommit);
        Assert.NotNull(owner.OwnerTransaction);
        Assert.True(process.Transactions.Count >= 2);
        Assert.All(process.Transactions, transaction => Assert.Same(owner.OwnerTransaction, transaction));
        await using var independent = fixture.Context();
        Assert.Null(independent.Database.CurrentTransaction);
        await fixture.Writer.UpdateCatalogAsync(catalog => catalog).WaitAsync(TimeSpan.FromSeconds(10));
    }

    [Fact]
    public async Task Process_Project_waiter_rejects_claim_state_committed_while_waiting_for_the_real_root_lock() {
        var clock = new NativeClock();
        var probe = new NativeGuardProbe();
        await using var app = await TestApplication.CreateAsync(ProcessProjectsHarness(clock, processProbe: probe));
        await using var scope = app.Services.CreateAsyncScope();
        var services = scope.ServiceProvider;
        var fixture = await Fixture.CreateAsync(services);
        var run = await CreateClaimedExecutionAsync(services, fixture, clock, authority: await CaptureProjectOperationsAsync(fixture));
        var admission = (await services.GetRequiredService<ProjectProcessExecutionMutationService>().ObserveAsync(run.Id))!;
        var write = await PrepareProcessProjectWriteAsync(services, admission, ProcessProjectWrite.Update);
        var before = await ProjectWriterStateAsync(services);
        await using var blocker = fixture.Context();
        await using var transaction = await blocker.Database.BeginTransactionAsync();
        await blocker.Database.ExecuteSqlInterpolatedAsync($"SELECT pg_advisory_xact_lock({NativeRootKey(admission.Dispatch.RootRunId.Value)})");
        probe.WaitForRoot = true;
        var pending = write();
        var backend = await probe.RootRequested.Task.WaitAsync(TimeSpan.FromSeconds(10));
        await using (var observer = fixture.Context()) {
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));
            while (!await observer.Database.SqlQuery<bool>($"""
                       SELECT EXISTS (SELECT 1 FROM pg_stat_activity WHERE pid = {backend} AND wait_event_type = 'Lock') AS "Value"
                       """).SingleAsync(timeout.Token)) {
                await Task.Delay(TimeSpan.FromMilliseconds(10), timeout.Token);
            }
        }
        var state = await blocker.RuntimeStates.SingleAsync(row => row.RunId == admission.Dispatch.Evidence.RunId.Value);
        state.Status = ProcessRuntimeStatus.CancelRequested;
        await blocker.SaveChangesAsync();
        await transaction.CommitAsync();
        var failure = await Record.ExceptionAsync(() => pending);
        Assert.NotNull(failure);
        Assert.True(failure is ProcessExecutionAuthorityMismatchException || SerializableMutationScope.IsConflict(failure));
        Assert.Equal(before, await ProjectWriterStateAsync(services));
    }

    [Fact]
    public async Task Process_Project_commit_acknowledgement_loss_preserves_exact_error_and_consumed_creation_without_inventing_receipt() {
        var clock = new NativeClock();
        var probe = new ProjectOwnerProbe(true);
        await using var app = await TestApplication.CreateAsync(ProcessProjectsHarness(clock, ownerProbe: probe));
        await using var scope = app.Services.CreateAsyncScope();
        var services = scope.ServiceProvider;
        var fixture = await Fixture.CreateAsync(services);
        var run = await CreateClaimedExecutionAsync(services, fixture, clock, authority: await CaptureProjectOperationsAsync(fixture));
        var admission = (await services.GetRequiredService<ProjectProcessExecutionMutationService>().ObserveAsync(run.Id))!;
        var authorization = services.GetRequiredService<ProjectProcessExecutionMutationService>()
            .BindProjectSource(admission, [], ProjectProcessProjectOperation.CreateRoot);
        var reserved = await services.GetRequiredService<ProjectWriteAdmissionService>().ReserveCreationAsync(Guid.NewGuid(), run.AgentId,
            Guid.NewGuid(), authorization: authorization);
        probe.ArmedProjectId = reserved.ProjectId;
        var original = await Record.ExceptionAsync(() => services.GetRequiredService<ProjectsService>().CreateWithReceiptAsync(reserved,
            new() { Name = "Committed Process target with lost acknowledgement" }, authorization: authorization));
        Assert.Same(probe.Failure, original);
        Assert.True(probe.Fired);
        await using var read = await services.GetRequiredService<IDbContextFactory<ProjectsDbContext>>().CreateDbContextAsync();
        Assert.True(await read.Set<Project>().AnyAsync(row => row.Id == reserved.ProjectId && row.LifetimeId == reserved.LifetimeId));
        Assert.Equal(ProjectCreationReservationState.Consumed, (await read.Set<ProjectCreationReservationRecord>().SingleAsync(row => row.Id == reserved.Id)).State);
        await fixture.Writer.UpdateCatalogAsync(catalog => catalog).WaitAsync(TimeSpan.FromSeconds(10));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Process_Project_creation_rejects_changed_reservation_requester_or_parent_lifetime(bool changeRequester) {
        var clock = new NativeClock();
        await using var app = await TestApplication.CreateAsync(ProcessProjectsHarness(clock));
        await using var scope = app.Services.CreateAsyncScope();
        var services = scope.ServiceProvider;
        var fixture = await Fixture.CreateAsync(services);
        var run = await CreateClaimedExecutionAsync(services, fixture, clock, authority: await CaptureProjectOperationsAsync(fixture));
        var admission = (await services.GetRequiredService<ProjectProcessExecutionMutationService>().ObserveAsync(run.Id))!;
        var authorization = services.GetRequiredService<ProjectProcessExecutionMutationService>()
            .BindProjectSource(admission, [admission.ProjectAdmission], ProjectProcessProjectOperation.CreateChild);
        var reserved = await services.GetRequiredService<ProjectWriteAdmissionService>().ReserveCreationAsync(Guid.NewGuid(), run.AgentId,
            Guid.NewGuid(), fixture.Project.ProjectId, authorization: authorization);
        var changed = changeRequester ? reserved with { RequesterId = Guid.NewGuid() } : reserved with { ParentLifetimeId = Guid.NewGuid() };
        await Assert.ThrowsAsync<ProcessLaunchAuthorityRejectedException>(() => services.GetRequiredService<ProjectsService>().CreateWithReceiptAsync(changed,
            new() { Name = "Wrong Process target" }, authorization: authorization));
        await using var read = await services.GetRequiredService<IDbContextFactory<ProjectsDbContext>>().CreateDbContextAsync();
        Assert.False(await read.Set<Project>().AnyAsync(row => row.Id == reserved.ProjectId));
        Assert.Equal(ProjectCreationReservationState.Reserved, (await read.Set<ProjectCreationReservationRecord>().SingleAsync(row => row.Id == reserved.Id)).State);
    }

    private static T ReadRegisteredToolResult<T>(AIFunction tool, object? result) where T : class =>
        Assert.IsType<T>(Assert.IsType<JsonElement>(result).Deserialize<T>(tool.JsonSerializerOptions));

    private static TestHarnessOptions ProcessProjectsHarness(NativeClock clock, ProcessProjectFlushProbe? probe = null,
        NativeGuardProbe? processProbe = null, ProjectOwnerProbe? ownerProbe = null)
        => new() { ConfigureServices = services => {
            NativeHarness(clock, processProbe: processProbe).ConfigureServices!(services);
            services.AddScoped<ProjectAgentSourceMutationAuthority>();
            services.AddScoped<IProjectCreationCompensationGuard, ProjectCreationCompensationGuard>();
            services.AddScoped<IProjectPartyDeletionStateQuery>(provider => provider.GetRequiredService<CanDoItAll.Modules.CrmHr.ProjectPartyIntegrationService>());
            if (probe is not null) {
                services.AddSingleton<IDbContextFactory<ProjectsDbContext>>(provider =>
                    new Factory<ProjectsDbContext>(NativeOptions<ProjectsDbContext>(provider, probe), static options => new(options)));
            }
            if (ownerProbe is not null) {
                services.AddSingleton<IDbContextFactory<ProjectsDbContext>>(provider =>
                    new Factory<ProjectsDbContext>(NativeOptions<ProjectsDbContext>(provider, ownerProbe), static options => new(options)));
            }
        } };

    private static async Task<ProcessLaunchAuthority> CaptureProjectOperationsAsync(Fixture fixture) {
        var access = AgentProjectStructureAccessMetadata.Read(fixture.Agent.ConfigurationJson);
        access.CanCreateProjects = true;
        access.CanCreateSubprojects = true;
        await fixture.Writer.UpdateCatalogAsync(catalog => catalog with { Agents = catalog.Agents.Select(agent => agent.Id == fixture.Agent.Id
            ? agent with { ConfigurationJson = AgentProjectStructureAccessMetadata.Write(agent.ConfigurationJson, access) } : agent).ToArray() });
        var source = Assert.IsType<ProcessLaunchPrincipal.AgentExecution>(fixture.SavedAuthority.Principal);
        var ceiling = source.Ceiling;
        var governance = new AgentExecutionGovernanceSnapshot(new(ceiling.AuthorityId), ceiling.AgentId, fixture.Project.DatabaseProfileId,
            new(ceiling.DatabaseProfileGeneration), WorkspaceScopeDescriptor.Organization(ceiling.WorkspaceScopeKey), true, true,
            ceiling.PolicyVersion, ceiling.PolicyFingerprint, allowedOperations: ceiling.AllowedOperations);
        var captured = await fixture.Authority.CaptureAgentAsync(fixture.Project, governance, source.Operation);
        Assert.Equal(new ProcessProjectMutationCeiling(true, true, true, true), captured.ProjectMutations);
        return captured;
    }

    private static async Task<Func<Task>> PrepareProcessProjectWriteAsync(IServiceProvider services, ProjectProcessMutationAdmission admission,
        ProcessProjectWrite kind) {
        var projects = services.GetRequiredService<ProjectsService>();
        var policy = services.GetRequiredService<ProjectProcessExecutionMutationService>();
        if (kind == ProcessProjectWrite.Update) {
            var editor = await projects.GetAsync(admission.ProjectAdmission.ProjectId);
            editor.Name = "Process owner edit";
            var authorization = policy.BindProjectSource(admission, [admission.ProjectAdmission], ProjectProcessProjectOperation.Update);
            return async () => { Assert.True((await projects.SaveAsync(editor, authorization: authorization)).IsSuccess); };
        }
        if (kind == ProcessProjectWrite.Hierarchy) {
            var (reservation, receipt) = await CreateProcessTargetAsync(services, admission, false);
            var authority = policy.BindProjectSource(admission.WithCreatedProject(reservation), [admission.ProjectAdmission, receipt.Project],
                ProjectProcessProjectOperation.ChangeHierarchy);
            return async () => { Assert.True((await projects.AddSubprojectAsync(admission.ProjectAdmission.ProjectId, receipt.Project.ProjectId, authorization: authority)).IsSuccess); };
        }
        var child = kind == ProcessProjectWrite.Child;
        var createAuthority = policy.BindProjectSource(admission, child ? [admission.ProjectAdmission] : [],
            child ? ProjectProcessProjectOperation.CreateChild : ProjectProcessProjectOperation.CreateRoot);
        var reserved = await services.GetRequiredService<ProjectWriteAdmissionService>().ReserveCreationAsync(Guid.NewGuid(),
            admission.Dispatch.Evidence.ExecutorAgentId, Guid.NewGuid(), child ? admission.ProjectAdmission.ProjectId : null, authorization: createAuthority);
        return async () => { Assert.True((await projects.CreateWithReceiptAsync(reserved, new() { Name = "Process owner creation" }, authorization: createAuthority)).IsSuccess); };
    }

    private static async Task<(ProjectCreationReservation Reservation, ProjectCreationReceipt Receipt)> CreateProcessTargetAsync(
        IServiceProvider services, ProjectProcessMutationAdmission admission, bool child) {
        var authorization = services.GetRequiredService<ProjectProcessExecutionMutationService>().BindProjectSource(admission,
            child ? [admission.ProjectAdmission] : [], child ? ProjectProcessProjectOperation.CreateChild : ProjectProcessProjectOperation.CreateRoot);
        var reservation = await services.GetRequiredService<ProjectWriteAdmissionService>().ReserveCreationAsync(Guid.NewGuid(),
            admission.Dispatch.Evidence.ExecutorAgentId, Guid.NewGuid(), child ? admission.ProjectAdmission.ProjectId : null, authorization: authorization);
        var result = await services.GetRequiredService<ProjectsService>().CreateWithReceiptAsync(reservation,
            new() { Name = "Retained Process target" }, authorization: authorization);
        Assert.True(result.IsSuccess);
        return (reservation, Assert.IsType<ProjectCreationReceipt>(result.Value));
    }

    private static async Task<string> ProjectWriterStateAsync(IServiceProvider services) {
        await using var context = await services.GetRequiredService<IDbContextFactory<ProjectsDbContext>>().CreateDbContextAsync();
        return JsonSerializer.Serialize(new {
            Projects = await context.Set<Project>().AsNoTracking().OrderBy(row => row.Id).Select(row => new { row.Id, row.LifetimeId, row.Name }).ToArrayAsync(),
            Links = await context.Set<ProjectHierarchyLink>().AsNoTracking().OrderBy(row => row.Id).ToArrayAsync(),
            Reservations = await context.Set<ProjectCreationReservationRecord>().AsNoTracking().OrderBy(row => row.Id).ToArrayAsync()
        });
    }

    private static async Task<AgentRuntimeToolProviderContext> ProjectProducerContextAsync(IServiceProvider services, Fixture fixture, ExecutionRunRecord run) {
        var workspace = services.GetRequiredService<IAgentFrameworkWorkspaceService>();
        var id = await workspace.SaveAgentAsync(new() {
            Id = run.AgentId, Name = "Assigned Process project executor", Status = AgentLifecycleStatus.Active,
            Model = "gpt-5-mini", Instructions = "Exercise the admitted project tools.", Permissions = AgentPermissionsPolicy.Default,
            ProjectStructureAccess = new() { CanRead = true, CanWrite = true, CanCreateProjects = true, CanCreateSubprojects = true,
                AllowedProjectIds = [fixture.Project.ProjectId] }
        });
        Assert.Equal(run.AgentId, id);
        var agent = Assert.Single(await workspace.ListAgentsAsync(), item => item.Id == id);
        var provider = (await fixture.Writer.LoadCatalogAsync()).Providers.First();
        return new(agent, provider, [], false, AgentRuntimeToolProviderPurpose.GovernedProcessAutomation, "process-project-owner",
            AgentRuntimeContextIntent.Empty with { SourceKind = run.SourceKind, SourceId = run.SourceId,
                ProcessRunId = run.ProcessRunId, ProcessStepId = run.ProcessStepId, IsGovernedProcessStep = true,
                AllowsProductMutation = true, Purpose = AgentRuntimeContextPurpose.GovernedProcessAutomation,
                AllowedOperations = [ProcessOperationContractNames.ReadProjectStructure, ProcessOperationContractNames.ExecuteExternalAction] },
            new Dictionary<string, string>());
    }

    private sealed class ProcessProjectFlushProbe : SaveChangesInterceptor, IDbTransactionInterceptor {
        public Action? AfterFlush { get; set; }
        public int ArmedFlushes { get; private set; }
        public DbTransaction? OwnerTransaction { get; private set; }
        public IDbTransactionInterceptor? Observer { get; set; }
        public override ValueTask<int> SavedChangesAsync(SaveChangesCompletedEventData eventData, int result,
            CancellationToken cancellationToken = default) {
            if (eventData.Context is ProjectsDbContext context && result > 0) {
                if (Observer is not null) {
                    Assert.Null(OwnerTransaction);
                    OwnerTransaction = context.Database.CurrentTransaction?.GetDbTransaction()
                        ?? throw new InvalidOperationException("The observed Project save must use an explicit owner transaction.");
                }
                if (AfterFlush is { } action) {
                    AfterFlush = null;
                    ArmedFlushes++;
                    action();
                }
            }
            return ValueTask.FromResult(result);
        }

        public async ValueTask<InterceptionResult> TransactionCommittingAsync(DbTransaction transaction, TransactionEventData eventData,
            InterceptionResult result, CancellationToken cancellationToken = default) {
            return ReferenceEquals(transaction, OwnerTransaction) && Observer is { } observer
                ? await observer.TransactionCommittingAsync(transaction, eventData, result, cancellationToken) : result;
        }

        public Task TransactionCommittedAsync(DbTransaction transaction, TransactionEndEventData eventData, CancellationToken cancellationToken = default)
            => ReferenceEquals(transaction, OwnerTransaction) && Observer is { } observer
                ? observer.TransactionCommittedAsync(transaction, eventData, cancellationToken) : Task.CompletedTask;
    }
}
