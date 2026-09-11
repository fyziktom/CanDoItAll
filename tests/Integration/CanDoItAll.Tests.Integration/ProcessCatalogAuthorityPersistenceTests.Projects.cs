using System.Data.Common;
using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.AgentFramework.Tooling;
using CanDoItAll.AgentFramework.Persistence;
using CanDoItAll.Modules.AgentFramework;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Hosting;
using CanDoItAll.Infrastructure.Configuration;
using CanDoItAll.Infrastructure.ControlPlane;
using CanDoItAll.Infrastructure.Persistence;
using CanDoItAll.Modules.Projects;
using CanDoItAll.Modules.Workbench;
using CanDoItAll.Modules.Workbench.ProjectStructure;
using CanDoItAll.Tests.Support;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;

namespace CanDoItAll.Tests.Integration.Processes;

public sealed partial class ProcessCatalogAuthorityPersistenceTests {
    [Theory]
    [InlineData(Revocation.Tools)]
    [InlineData(Revocation.Read)]
    [InlineData(Revocation.Write)]
    [InlineData(Revocation.ExactLifetime)]
    public async Task Ordinary_Project_owner_save_rechecks_original_and_current_authority(Revocation revocation) {
        await using var app = await TestApplication.CreateAsync(ProjectsHarness());
        await using var scope = app.Services.CreateAsyncScope();
        var services = scope.ServiceProvider;
        var fixture = await Fixture.CreateAsync(services);
        var owner = OrdinaryOwner(fixture);
        var projects = services.GetRequiredService<ProjectsService>();
        var editor = await projects.GetAsync(fixture.Project.ProjectId);
        var original = editor.Name;
        editor.Name = "Denied late overwrite";
        var authority = services.GetRequiredService<ProjectAgentSourceMutationAuthority>().Bind(owner.AgentMutationAdmission!, [owner.ExpectedProjectAdmission!]);
        await fixture.RevokeAsync(revocation);
        var failure = await Assert.ThrowsAsync<ProjectStructureAgentException>(() => projects.SaveAsync(editor, authorization: authority));
        Assert.Equal("ProjectMutationAuthorityDenied", failure.ErrorCode);
        Assert.Equal(original, (await projects.GetAsync(fixture.Project.ProjectId)).Name);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Ordinary_Project_creation_checks_current_source_at_reservation_and_at_create(bool afterReservation) {
        await using var app = await TestApplication.CreateAsync(ProjectsHarness());
        await using var scope = app.Services.CreateAsyncScope();
        var services = scope.ServiceProvider;
        var fixture = await Fixture.CreateAsync(services);
        var owner = await CreationOwnerAsync(fixture, services);
        var authority = services.GetRequiredService<ProjectAgentSourceMutationAuthority>().Bind(owner.AgentMutationAdmission!, [owner.ExpectedProjectAdmission!]);
        var admissions = services.GetRequiredService<ProjectWriteAdmissionService>();
        var id = Guid.NewGuid();
        ProjectCreationReservation? reservation = null;
        if (afterReservation) {
            reservation = await admissions.ReserveCreationAsync(id, fixture.Agent.Id, Guid.NewGuid(), fixture.Project.ProjectId, authorization: authority);
            await services.GetRequiredService<ProjectStructureAgentAuthorizationService>().GrantCreatedProjectAccessAsync(fixture.Agent.Id, reservation, CancellationToken.None);
        }
        await fixture.RevokeAsync(Revocation.Tools);
        var failure = await Assert.ThrowsAsync<ProjectStructureAgentException>(async () => {
            if (reservation is null) {
                await admissions.ReserveCreationAsync(id, fixture.Agent.Id, Guid.NewGuid(), fixture.Project.ProjectId, authorization: authority);
            } else {
                await services.GetRequiredService<ProjectsService>().CreateWithReceiptAsync(reservation, new() { Name = "Denied child" }, authorization: authority);
            }
        });
        Assert.Equal("ProjectMutationAuthorityDenied", failure.ErrorCode);
        await using var read = await services.GetRequiredService<IDbContextFactory<ProjectsDbContext>>().CreateDbContextAsync();
        Assert.False(await read.Set<Project>().AnyAsync(project => project.Id == id));
        var saved = await read.Set<ProjectCreationReservationRecord>().SingleOrDefaultAsync(row => row.ProjectId == id);
        if (afterReservation) {
            Assert.Equal(ProjectCreationReservationState.Reserved, saved!.State);
        } else {
            Assert.Null(saved);
        }
    }

    public enum HierarchyChange { Add, Remove, Reconnect }

    [Theory]
    [InlineData(HierarchyChange.Add)]
    [InlineData(HierarchyChange.Remove)]
    [InlineData(HierarchyChange.Reconnect)]
    public async Task Ordinary_Project_hierarchy_actual_owner_rechecks_source_before_writes(HierarchyChange change) {
        await using var app = await TestApplication.CreateAsync(ProjectsHarness());
        await using var scope = app.Services.CreateAsyncScope();
        var services = scope.ServiceProvider;
        var fixture = await Fixture.CreateAsync(services);
        var projects = services.GetRequiredService<ProjectsService>();
        var parent = new ProjectWriteAdmission(fixture.Project.DatabaseProfileId, fixture.Project.ProjectId, fixture.Project.LifetimeId);
        var child = (await projects.CreateWithReceiptAsync(Guid.NewGuid(), new() { Name = "Hierarchy child" }, parent.ProjectId)).Value!.Project;
        var other = (await projects.CreateWithReceiptAsync(Guid.NewGuid(), new() { Name = "Other parent" })).Value!.Project;
        var owner = await CreationOwnerAsync(fixture, services, allowAll: true);
        ProjectWriteAdmission[] affected = change switch {
            HierarchyChange.Add => new[] { other, child },
            HierarchyChange.Remove => [parent, child],
            _ => [child, parent, other]
        };
        var authority = services.GetRequiredService<ProjectAgentSourceMutationAuthority>().Bind(owner.AgentMutationAdmission!, affected);
        await fixture.RevokeAsync(Revocation.Write);
        await Assert.ThrowsAsync<ProjectStructureAgentException>(async () => {
            _ = change switch {
                HierarchyChange.Add => await projects.AddSubprojectAsync(other.ProjectId, child.ProjectId, authorization: authority),
                HierarchyChange.Remove => await projects.RemoveSubprojectAsync(parent.ProjectId, child.ProjectId, authorization: authority),
                _ => await projects.ReconnectSubprojectAsync(child.ProjectId, parent.ProjectId, other.ProjectId, authorization: authority)
            };
        });
        await using var read = await services.GetRequiredService<IDbContextFactory<ProjectsDbContext>>().CreateDbContextAsync();
        var link = Assert.Single(await read.Set<ProjectHierarchyLink>().Where(row => row.ChildProjectId == child.ProjectId).ToArrayAsync());
        Assert.Equal(parent.ProjectId, link.ParentProjectId);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Registered_Project_tool_keeps_original_source_until_actual_Projects_transaction(bool creation) {
        var probe = new OrdinaryCatalogProbe();
        await using var app = await TestApplication.CreateAsync(ProjectsHarness(sourceProbe: probe));
        await using var scope = app.Services.CreateAsyncScope();
        var services = scope.ServiceProvider;
        var fixture = await Fixture.CreateAsync(services);
        await CreationOwnerAsync(fixture, services);
        var catalog = await fixture.Writer.LoadCatalogAsync();
        var agent = catalog.Agents.Single(row => row.Id == fixture.Agent.Id);
        var name = creation ? ProjectStructureToolPolicy.ProjectStructureProjectCreate : ProjectStructureToolPolicy.ProjectStructureProjectUpdate;
        var context = new AgentRuntimeToolProviderContext(agent, catalog.Providers.First(), [], false,
            AgentRuntimeToolProviderPurpose.InteractiveChat, "ordinary-project-producer", AgentRuntimeContextIntent.Empty with {
                SourceKind = ProjectStructureAgentChatContextBuilder.SourceKind,
                SourceId = fixture.Project.ProjectId.ToString("D"),
                WorkspaceScope = WorkspaceScopeDescriptor.Organization(fixture.Project.DatabaseProfileId.ToString("N")),
                Purpose = AgentRuntimeContextPurpose.InteractiveChat
            }, new Dictionary<string, string>()) {
            Governance = new AgentExecutionGovernanceSnapshot(new(Guid.NewGuid()), agent.Id, fixture.Project.DatabaseProfileId,
                services.GetRequiredService<IAgentExecutionProfileGenerationSource>().GetGeneration(),
                WorkspaceScopeDescriptor.Organization(fixture.Project.DatabaseProfileId.ToString("N")), true, true,
                "ordinary-project", "ordinary-project-fingerprint", allowedOperations: [name])
        };
        var provider = Assert.Single(services.GetServices<IAgentRuntimeToolProvider>().OfType<ProjectStructureAgentRuntimeToolProvider>());
        var tool = Assert.Single((await provider.CreateToolsAsync(context, default)).OfType<AIFunction>(), item => item.Name == name);
        probe.BeforeRead = () => fixture.RevokeAsync(Revocation.Tools);
        var arguments = new AIFunctionArguments { ["request"] = new ProjectStructureProjectSaveRequest("Must not commit", "", "", "") };
        if (!creation) {
            arguments["projectId"] = fixture.Project.ProjectId;
        }
        using var owned = AgentRuntimeToolOwnershipContext.BeginScope(new(provider.Descriptor.ProviderKey, provider.Descriptor.DisplayName, name));
        var failure = await Assert.ThrowsAsync<ProjectStructureAgentException>(async () => { await tool.InvokeAsync(arguments); });
        Assert.Equal("ProjectMutationAuthorityDenied", failure.ErrorCode);
        Assert.Equal(1, probe.Reads);
        await using var read = await services.GetRequiredService<IDbContextFactory<ProjectsDbContext>>().CreateDbContextAsync();
        Assert.False(await read.Set<Project>().AnyAsync(row => row.Name == "Must not commit"));
        Assert.Empty(await read.Set<ProjectCreationReservationRecord>().Where(row => row.RequesterId == agent.Id).ToArrayAsync());
    }

    public enum CreationChange { Name, Phase, Option, Hierarchy, NativeNode, RetainedWorkflowAdmission }

    [Theory]
    [InlineData(CreationChange.Name)]
    [InlineData(CreationChange.Phase)]
    [InlineData(CreationChange.Option)]
    [InlineData(CreationChange.Hierarchy)]
    [InlineData(CreationChange.NativeNode)]
    [InlineData(CreationChange.RetainedWorkflowAdmission)]
    public async Task Creation_compensation_keeps_later_owned_edits_and_native_or_retained_evidence(CreationChange change) {
        await using var app = await TestApplication.CreateAsync(ProjectsHarness());
        await using var scope = app.Services.CreateAsyncScope();
        var services = scope.ServiceProvider;
        var projects = services.GetRequiredService<ProjectsService>();
        var id = Guid.NewGuid();
        var receipt = (await projects.CreateWithReceiptAsync(id, new() { Name = "Original created project" })).Value!;
        Assert.NotNull(receipt);
        var editor = await projects.GetAsync(id);
        switch (change) {
            case CreationChange.Name:
                editor.Name = "A later human name";
                Assert.True((await projects.SaveAsync(editor)).IsSuccess);
                break;
            case CreationChange.Phase:
                editor.Phases.Add(new() { Name = "A later human phase" });
                Assert.True((await projects.SaveAsync(editor)).IsSuccess);
                break;
            case CreationChange.Option:
                editor.Options.Add(new() { Category = ProjectOptionCategory.Storage, OptionName = "A later human choice" });
                Assert.True((await projects.SaveAsync(editor)).IsSuccess);
                break;
            case CreationChange.Hierarchy:
                var parent = (await projects.CreateWithReceiptAsync(Guid.NewGuid(), new() { Name = "Later parent" })).Value!;
                Assert.True((await projects.AddSubprojectAsync(parent.Project.ProjectId, id)).IsSuccess);
                break;
            case CreationChange.NativeNode:
                await SeedOrdinaryGraphAsync(services, id);
                break;
            case CreationChange.RetainedWorkflowAdmission:
                await using (var native = await services.GetRequiredService<IDbContextFactory<WorkbenchDbContext>>().CreateDbContextAsync()) {
                    native.Add(new ProjectWorkflowAdmissionRecord {
                        IntentId = Guid.NewGuid(), ProjectId = id, NativeNodeId = Guid.NewGuid(), NodeId = "deleted:retained",
                        RunId = Guid.NewGuid(), Sequence = 1, AdmissionJson = "{}", StatusJson = "{}", DeliveryFinished = true,
                        NextAttemptAtUtc = DateTimeOffset.MaxValue
                    });
                    await native.SaveChangesAsync();
                }
                break;
        }
        Assert.False(await projects.TryCompensateCreationAsync(receipt));
        var current = await services.GetRequiredService<ProjectWriteAdmissionService>().CaptureAsync(id);
        Assert.Equal(receipt.Project, current);
        if (change == CreationChange.Name) {
            Assert.Equal("A later human name", (await projects.GetAsync(id)).Name);
        }
    }

    [Fact]
    public async Task Creation_compensation_removes_only_the_unchanged_original_child_and_cancels_its_consumed_reservation() {
        await using var app = await TestApplication.CreateAsync(ProjectsHarness());
        await using var scope = app.Services.CreateAsyncScope();
        var services = scope.ServiceProvider;
        var fixture = await Fixture.CreateAsync(services);
        var owner = await CreationOwnerAsync(fixture, services);
        var (reservation, authority) = await ReserveChildAsync(fixture, owner, services);
        var projects = services.GetRequiredService<ProjectsService>();
        var result = await projects.CreateWithReceiptAsync(reservation, new() { Name = "Original child" }, authorization: authority);
        Assert.True(result.IsSuccess);
        var receipt = result.Value!;
        Assert.Equal(new ProjectWriteAdmission(reservation.DatabaseProfileId, reservation.ProjectId, reservation.LifetimeId), receipt.Project);
        Assert.Equal(reservation, receipt.Reservation);
        Assert.Equal(64, receipt.Fingerprint.Length);
        var (unrelated, unrelatedAuthority) = await ReserveChildAsync(fixture, owner, services);
        Assert.True((await projects.CreateWithReceiptAsync(unrelated, new() { Name = "Unrelated consumed child" }, authorization: unrelatedAuthority)).IsSuccess);
        DateTimeOffset? consumedAt;
        await using (var before = await services.GetRequiredService<IDbContextFactory<ProjectsDbContext>>().CreateDbContextAsync()) {
            consumedAt = (await before.Set<ProjectCreationReservationRecord>().SingleAsync(row => row.Id == reservation.Id)).ConsumedAtUtc;
        }
        Assert.NotNull(consumedAt);
        Assert.True(await projects.TryCompensateCreationAsync(receipt));
        await using var read = await services.GetRequiredService<IDbContextFactory<ProjectsDbContext>>().CreateDbContextAsync();
        Assert.False(await read.Set<Project>().AnyAsync(project => project.Id == reservation.ProjectId));
        Assert.True(await read.Set<Project>().AnyAsync(project => project.Id == fixture.Project.ProjectId));
        var cancelled = await read.Set<ProjectCreationReservationRecord>().SingleAsync(row => row.Id == reservation.Id);
        Assert.Equal(ProjectCreationReservationState.Cancelled, cancelled.State);
        Assert.Equal(consumedAt, cancelled.ConsumedAtUtc);
        Assert.NotNull(cancelled.CancelledAtUtc);
        var retained = await read.Set<ProjectCreationReservationRecord>().SingleAsync(row => row.Id == unrelated.Id);
        Assert.Equal(ProjectCreationReservationState.Consumed, retained.State);
        Assert.Null(retained.CancelledAtUtc);
        Assert.True(await read.Set<Project>().AnyAsync(project => project.Id == unrelated.ProjectId && project.LifetimeId == unrelated.LifetimeId));
        Assert.True(await read.Set<ProjectRetirementRecord>().AnyAsync(row => row.ProjectId == reservation.ProjectId && row.LifetimeId == reservation.LifetimeId));
    }

    [Fact]
    public async Task Creation_compensation_cannot_remove_a_recreated_project_with_the_same_public_id() {
        await using var app = await TestApplication.CreateAsync(ProjectsHarness());
        await using var scope = app.Services.CreateAsyncScope();
        var services = scope.ServiceProvider;
        var projects = services.GetRequiredService<ProjectsService>();
        var id = Guid.NewGuid();
        var receipt = (await projects.CreateWithReceiptAsync(id, new() { Name = "Old lifetime" })).Value!;
        await projects.DeleteAsync(id);
        Assert.True((await projects.CreateAsync(id, new() { Name = "Recreated lifetime" })).IsSuccess);
        Assert.False(await projects.TryCompensateCreationAsync(receipt));
        Assert.Equal("Recreated lifetime", (await projects.GetAsync(id)).Name);
        Assert.NotEqual(receipt.Project.LifetimeId, (await services.GetRequiredService<ProjectWriteAdmissionService>().CaptureAsync(id))!.LifetimeId);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Creation_flush_or_commit_ack_failure_retains_exact_exception_and_never_infers_compensation(bool committed) {
        var probe = new ProjectOwnerProbe(committed);
        await using var app = await TestApplication.CreateAsync(ProjectsHarness(probe));
        await using var scope = app.Services.CreateAsyncScope();
        var services = scope.ServiceProvider;
        var fixture = await Fixture.CreateAsync(services);
        var owner = await CreationOwnerAsync(fixture, services);
        var (reservation, authority) = await ReserveChildAsync(fixture, owner, services);
        probe.ArmedProjectId = reservation.ProjectId;
        var failure = await Assert.ThrowsAsync<ArgumentException>(() => services.GetRequiredService<ProjectsService>()
            .CreateWithReceiptAsync(reservation, new() { Name = "Ack boundary child" }, authorization: authority));
        Assert.Same(probe.Failure, failure);
        Assert.True(probe.Fired);
        probe.ArmedProjectId = null;
        await fixture.RevokeAsync(Revocation.Tools);
        await using var read = await services.GetRequiredService<IDbContextFactory<ProjectsDbContext>>().CreateDbContextAsync();
        Assert.Equal(committed, await read.Set<Project>().AnyAsync(project => project.Id == reservation.ProjectId && project.LifetimeId == reservation.LifetimeId));
        var saved = await read.Set<ProjectCreationReservationRecord>().SingleAsync(row => row.Id == reservation.Id);
        Assert.Equal(committed ? ProjectCreationReservationState.Consumed : ProjectCreationReservationState.Reserved, saved.State);
        Assert.Null(saved.CancelledAtUtc);
    }

    [Fact]
    public async Task Agent_creation_producer_surfaces_consumed_reservation_as_partial_completion_and_keeps_original_error() {
        await using var app = await TestApplication.CreateAsync(ProjectsHarness());
        await using var scope = app.Services.CreateAsyncScope();
        var services = scope.ServiceProvider;
        var fixture = await Fixture.CreateAsync(services);
        var owner = await CreationOwnerAsync(fixture, services);
        var authority = services.GetRequiredService<ProjectAgentSourceMutationAuthority>().Bind(owner.AgentMutationAdmission!, [owner.ExpectedProjectAdmission!]);
        var original = new ArgumentException("Creation completed before its producer reply was lost.");
        ProjectCreationReservation? retained = null;
        var observed = await Assert.ThrowsAsync<ProjectStructureAgentException>(() =>
            services.GetRequiredService<ProjectStructureAgentProjectCreationCoordinator>().CreateAsync<Guid>(fixture.Agent,
                async (reservation, cancellationToken) => {
                    retained = reservation;
                    Assert.True((await services.GetRequiredService<ProjectsService>().CreateWithReceiptAsync(reservation,
                        new() { Name = "Producer partial child" }, cancellationToken, authority)).IsSuccess);
                    throw original;
                }, id => id, CancellationToken.None, parentProjectId: fixture.Project.ProjectId, authorization: authority));
        Assert.Same(original, observed.InnerException);
        Assert.Equal("ProjectCreationRequiresObservation", observed.ErrorCode);
        var partial = Assert.IsType<ProjectCreationPartialCompletion>(observed.Details);
        Assert.True(partial.CreationObserved);
        Assert.False(partial.RequestedOperationCompleted);
        Assert.Equal(retained!.ProjectId, partial.ProjectId);
        Assert.Equal(ProjectCreationReservationState.Consumed,
            await services.GetRequiredService<ProjectWriteAdmissionService>().ReadCreationStateAsync(retained));
        Assert.Equal(retained.LifetimeId, (await services.GetRequiredService<ProjectWriteAdmissionService>().CaptureAsync(retained.ProjectId))!.LifetimeId);
    }

    [Fact]
    public async Task Ordinary_Project_actual_commit_holds_catalog_until_commit_then_releases_before_observers() {
        var probe = new ProjectOwnerProbe(null);
        await using var app = await TestApplication.CreateAsync(ProjectsHarness(probe));
        await using var scope = app.Services.CreateAsyncScope();
        var services = scope.ServiceProvider;
        var fixture = await Fixture.CreateAsync(services);
        var owner = await CreationOwnerAsync(fixture, services);
        var (reservation, authority) = await ReserveChildAsync(fixture, owner, services);
        probe.ArmedProjectId = reservation.ProjectId;
        probe.Catalog = fixture.Writer;
        Assert.True((await services.GetRequiredService<ProjectsService>().CreateWithReceiptAsync(reservation,
            new() { Name = "Held source child" }, authorization: authority)).IsSuccess);
        Assert.True(probe.BeforeCommit);
        Assert.True(probe.AfterCommit);
        await fixture.RevokeAsync(Revocation.Tools);
    }

    [Fact]
    public async Task Ordinary_cross_project_transfer_rejects_a_stale_target_tuple_before_any_move() {
        await using var app = await TestApplication.CreateAsync(ProjectsHarness());
        await using var scope = app.Services.CreateAsyncScope();
        var services = scope.ServiceProvider;
        var fixture = await Fixture.CreateAsync(services);
        var projects = services.GetRequiredService<ProjectsService>();
        var targetId = Guid.NewGuid();
        var oldTarget = (await projects.CreateWithReceiptAsync(targetId, new() { Name = "Old target" })).Value!.Project;
        var source = await CreationOwnerAsync(fixture, services, allowAll: true);
        var nodes = await SeedOrdinaryGraphAsync(services, fixture.Project.ProjectId);
        var owner = source with { ExpectedProjectAdmissions = [source.ExpectedProjectAdmission!, oldTarget] };
        await projects.DeleteAsync(targetId);
        Assert.True((await projects.CreateAsync(targetId, new() { Name = "Recreated target" })).IsSuccess);
        await Assert.ThrowsAsync<ProjectWriteAdmissionRejectedException>(() => services.GetRequiredService<ProjectWorkbenchService>()
            .MoveNodesToProjectAsync(fixture.Project.ProjectId, [nodes.First], targetId, false, mutationOwner: owner));
        await AssertNativeCountAsync(services, fixture.Project.ProjectId, 2);
        await AssertNativeCountAsync(services, targetId, 0);
    }

    private static TestHarnessOptions ProjectsHarness(ProjectOwnerProbe? probe = null, OrdinaryCatalogProbe? sourceProbe = null) => new() { ConfigureServices = services => {
        OrdinaryHarness().ConfigureServices!(services);
        if (sourceProbe is not null) {
            services.AddScoped<ProjectAgentSourceMutationAuthority>(provider => {
                var profile = provider.GetRequiredService<ICanonicalRuntimeDatabase>();
                var source = new CanonicalAgentCatalogLeaseSource(profile, provider.GetRequiredService<IOptions<StorageOptions>>(),
                    provider.GetRequiredService<IHostEnvironment>(), provider.GetRequiredService<ProjectWriteAdmissionService>());
                return new(profile, new OrdinaryCatalogStore(source, sourceProbe), provider.GetRequiredService<IAgentExecutionProfileGenerationSource>());
            });
        }
        if (probe is not null) {
            services.AddSingleton<IDbContextFactory<ProjectsDbContext>>(provider => {
                var builder = new DbContextOptionsBuilder<ProjectsDbContext>();
                AppDbContextOptionsConfigurator.Configure(builder, provider.GetRequiredService<ICanonicalRuntimeDatabase>().Profile);
                builder.AddInterceptors(probe, new ProjectFlushProbe(probe));
                return new Factory<ProjectsDbContext>(builder.Options, static options => new(options));
            });
        }
    } };

    private static async Task<ProjectStructureAgentContext> CreationOwnerAsync(Fixture fixture, IServiceProvider services, bool allowAll = false) {
        var access = AgentProjectStructureAccessMetadata.Read(fixture.Agent.ConfigurationJson);
        access.CanCreateProjects = true;
        access.CanCreateSubprojects = true;
        access.AllowAllProjects = allowAll;
        await fixture.Writer.UpdateCatalogAsync(catalog => catalog with { Agents = catalog.Agents.Select(agent => agent.Id != fixture.Agent.Id ? agent :
            agent with { ConfigurationJson = AgentProjectStructureAccessMetadata.Write(agent.ConfigurationJson, access) }).ToArray() });
        return OrdinaryOwner(fixture, captured: access);
    }

    private static async Task<(ProjectCreationReservation, ProjectMutationAuthorization)> ReserveChildAsync(Fixture fixture,
        ProjectStructureAgentContext owner, IServiceProvider services) {
        var authority = services.GetRequiredService<ProjectAgentSourceMutationAuthority>().Bind(owner.AgentMutationAdmission!, [owner.ExpectedProjectAdmission!]);
        var reservation = await services.GetRequiredService<ProjectWriteAdmissionService>().ReserveCreationAsync(Guid.NewGuid(), fixture.Agent.Id,
            Guid.NewGuid(), fixture.Project.ProjectId, authorization: authority);
        await services.GetRequiredService<ProjectStructureAgentAuthorizationService>().GrantCreatedProjectAccessAsync(fixture.Agent.Id, reservation, CancellationToken.None);
        return (reservation, authority);
    }

    private sealed class ProjectOwnerProbe(bool? failAfterCommit) : DbTransactionInterceptor {
        public Guid? ArmedProjectId { get; set; }
        public ArgumentException Failure { get; } = new("Exact Projects owner fault");
        public bool Fired { get; private set; }
        public bool BeforeCommit { get; private set; }
        public bool AfterCommit { get; private set; }
        public FileSandboxWorkspaceStore? Catalog { get; set; }
        public void AfterFlush(DbContext? context) {
            if (failAfterCommit == false && ArmedProjectId is { } id && context is ProjectsDbContext &&
                    context.Set<Project>().Local.Any(project => project.Id == id)) {
                Fired = true;
                throw Failure;
            }
        }
        public override async ValueTask<InterceptionResult> TransactionCommittingAsync(DbTransaction transaction, TransactionEventData eventData,
            InterceptionResult result, CancellationToken cancellationToken = default) {
            if (ArmedProjectId is not null && Catalog is not null) {
                await RequireHeldAsync();
                BeforeCommit = true;
            }
            return result;
        }
        public override async Task TransactionCommittedAsync(DbTransaction transaction, TransactionEndEventData eventData, CancellationToken cancellationToken = default) {
            if (ArmedProjectId is not { } id) {
                return;
            }
            await using var command = eventData.Context!.Database.GetDbConnection().CreateCommand();
            command.CommandText = "SELECT EXISTS (SELECT 1 FROM \"Projects_Projects\" WHERE \"Id\" = @id)";
            var parameter = command.CreateParameter();
            parameter.ParameterName = "id";
            parameter.Value = id;
            command.Parameters.Add(parameter);
            if (await command.ExecuteScalarAsync(cancellationToken) is not true) {
                return;
            }
            ArmedProjectId = null;
            if (Catalog is not null) {
                await RequireHeldAsync();
                AfterCommit = true;
            }
            if (failAfterCommit == true) {
                Fired = true;
                throw Failure;
            }
        }
        private async Task RequireHeldAsync() {
            using var timeout = new CancellationTokenSource(TimeSpan.FromMilliseconds(150));
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => Catalog!.UpdateCatalogAsync(catalog => catalog, timeout.Token));
        }
    }

    private sealed class ProjectFlushProbe(ProjectOwnerProbe owner) : SaveChangesInterceptor {
        public override ValueTask<int> SavedChangesAsync(SaveChangesCompletedEventData eventData, int result, CancellationToken cancellationToken = default) {
            owner.AfterFlush(eventData.Context);
            return ValueTask.FromResult(result);
        }
    }
}
