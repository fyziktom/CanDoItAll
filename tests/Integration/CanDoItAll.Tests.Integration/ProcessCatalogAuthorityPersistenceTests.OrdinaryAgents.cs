using System.Data.Common;
using CanDoItAll.Modules.Workbench.CanvasAdapters;
using CanDoItAll.Modules.Workbench.ProjectStructure;
using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.AgentFramework.Tooling;
using CanDoItAll.Components.Gantt;
using CanDoItAll.Infrastructure.Configuration;
using CanDoItAll.Infrastructure.ControlPlane;
using CanDoItAll.Infrastructure.Persistence;
using CanDoItAll.Modules.AgentFramework;
using CanDoItAll.Modules.Projects;
using CanDoItAll.Modules.Workbench;
using CanDoItAll.SharedKernel;
using CanDoItAll.Tests.Support;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace CanDoItAll.Tests.Integration.Processes;

public sealed partial class ProcessCatalogAuthorityPersistenceTests {
    public enum OrdinaryGraphChange { Move, Recompose, Reparent, Link, Unlink, Copy, Delete, Import, PromptCommand, Approval }
    public enum OrdinaryTaskChange { Title, RowOrder, Pricing, Compensation, Assignee }

    [Theory]
    [InlineData(Revocation.Tools)]
    [InlineData(Revocation.Read)]
    [InlineData(Revocation.Write)]
    [InlineData(Revocation.ExactLifetime)]
    public async Task Ordinary_Agent_native_create_denies_current_revocation_without_writes(Revocation revocation) {
        await using var app = await TestApplication.CreateAsync(OrdinaryHarness());
        await using var scope = app.Services.CreateAsyncScope();
        var services = scope.ServiceProvider;
        var fixture = await Fixture.CreateAsync(services);
        var owner = OrdinaryOwner(fixture);
        await fixture.RevokeAsync(revocation);
        var failure = await Assert.ThrowsAsync<ProjectStructureAgentException>(() => OrdinaryCreateAsync(services, owner));
        Assert.Equal("ProjectMutationAuthorityDenied", failure.ErrorCode);
        await AssertNativeCountAsync(services, fixture.Project.ProjectId, 0);
    }

    [Theory]
    [InlineData(OrdinaryGraphChange.Move)]
    [InlineData(OrdinaryGraphChange.Recompose)]
    [InlineData(OrdinaryGraphChange.Reparent)]
    [InlineData(OrdinaryGraphChange.Link)]
    [InlineData(OrdinaryGraphChange.Unlink)]
    [InlineData(OrdinaryGraphChange.Copy)]
    [InlineData(OrdinaryGraphChange.Delete)]
    [InlineData(OrdinaryGraphChange.Import)]
    [InlineData(OrdinaryGraphChange.PromptCommand)]
    [InlineData(OrdinaryGraphChange.Approval)]
    public async Task Ordinary_graph_services_preserve_original_authority_until_the_native_owner(OrdinaryGraphChange change) {
        await using var app = await TestApplication.CreateAsync(OrdinaryHarness());
        await using var scope = app.Services.CreateAsyncScope();
        var services = scope.ServiceProvider;
        var fixture = await Fixture.CreateAsync(services);
        var nodes = await SeedOrdinaryGraphAsync(services, fixture.Project.ProjectId);
        var owner = OrdinaryOwner(fixture);
        await fixture.RevokeAsync(Revocation.Write);
        var failure = await Assert.ThrowsAsync<ProjectStructureAgentException>(() => OrdinaryGraphChangeAsync(services, owner, nodes, change));
        Assert.Equal("ProjectMutationAuthorityDenied", failure.ErrorCode);
        await using var read = await services.GetRequiredService<IDbContextFactory<WorkbenchDbContext>>().CreateDbContextAsync();
        Assert.Equal(2, await read.Set<ProjectObjectRecord>().CountAsync(row => row.ProjectId == fixture.Project.ProjectId));
        Assert.Equal(1, await read.Set<ProjectObjectLinkRecord>().CountAsync(row => row.ProjectId == fixture.Project.ProjectId));
        Assert.Empty(await read.Set<ProjectCrossModuleMutationRecord>().Where(row => row.ProjectId == fixture.Project.ProjectId).ToArrayAsync());
        Assert.Equal(10, (await read.Set<ProjectObjectRecord>().SingleAsync(row => row.NodeKey == nodes.First)).PositionX);
    }

    [Theory]
    [InlineData(OrdinaryTaskChange.Title)]
    [InlineData(OrdinaryTaskChange.RowOrder)]
    [InlineData(OrdinaryTaskChange.Pricing)]
    [InlineData(OrdinaryTaskChange.Compensation)]
    [InlineData(OrdinaryTaskChange.Assignee)]
    public async Task Ordinary_task_followups_hold_catalog_through_each_real_owner_commit(OrdinaryTaskChange change) {
        var probe = new NativeCommitProbe();
        await using var app = await TestApplication.CreateAsync(OrdinaryHarness(probe));
        await using var scope = app.Services.CreateAsyncScope();
        var services = scope.ServiceProvider;
        var fixture = await Fixture.CreateAsync(services);
        var nodes = await SeedOrdinaryGraphAsync(services, fixture.Project.ProjectId, tasks: true);
        var owner = OrdinaryOwner(fixture, ProjectAgentMutationDomain.Tasks);
        var held = new CatalogCommitProbe(fixture.Writer);
        probe.Observer = change == OrdinaryTaskChange.RowOrder ? new GanttRowCommitProbe(held) : held;
        await OrdinaryTaskChangeAsync(services, owner, nodes, change);
        if (change == OrdinaryTaskChange.RowOrder) {
            Assert.Equal(new[] { nodes.Second, nodes.First },
                (await services.GetRequiredService<ProjectWorkbenchService>().LoadGanttViewStateAsync(fixture.Project.ProjectId)).OrderedTaskNodeIds);
        }
        Assert.True(held.BeforeCommit);
        Assert.True(held.AfterCommit);
        Assert.True(probe.NativeFlushes > 0);
        probe.Observer = null;
        await fixture.RevokeAsync(Revocation.Write).WaitAsync(TimeSpan.FromSeconds(10));
        await Assert.ThrowsAsync<ProjectStructureAgentException>(() => OrdinaryTaskChangeAsync(services, owner, nodes, change));
    }

    [Fact]
    public async Task Ordinary_task_creation_retains_catalog_authority_for_backlog_task_and_row_insertion() {
        var probe = new NativeCommitProbe();
        await using var app = await TestApplication.CreateAsync(OrdinaryHarness(probe));
        await using var scope = app.Services.CreateAsyncScope();
        var services = scope.ServiceProvider;
        var fixture = await Fixture.CreateAsync(services);
        var held = new CatalogCommitProbe(fixture.Writer);
        probe.Observer = held;
        var owner = OrdinaryOwner(fixture, ProjectAgentMutationDomain.Tasks);
        var created = await services.GetRequiredService<ProjectStructureTaskCreationService>().CreateAsync(fixture.Project.ProjectId,
            new("Admitted task", DateTimeOffset.UtcNow, DateTimeOffset.UtcNow.AddHours(1)) { ExpectedProjectAdmission = owner.ExpectedProjectAdmission }, owner);
        Assert.True(held.BeforeCommit);
        Assert.True(held.AfterCommit);
        var surface = await services.GetRequiredService<ProjectWorkbenchService>().GetStructureAsync(fixture.Project.ProjectId);
        Assert.Contains(surface.Nodes, node => node.Id == created.TaskNodeId && node.ObjectSubtype == "task");
        Assert.Contains(created.TaskNodeId, (await services.GetRequiredService<ProjectWorkbenchService>().LoadGanttViewStateAsync(fixture.Project.ProjectId)).OrderedTaskNodeIds);
        probe.Observer = null;
        await fixture.RevokeAsync(Revocation.Write).WaitAsync(TimeSpan.FromSeconds(10));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Ordinary_native_commit_fault_preserves_exact_exception_and_actual_commit_boundary(bool afterCommit) {
        var probe = new NativeCommitProbe();
        await using var app = await TestApplication.CreateAsync(OrdinaryHarness(probe));
        await using var scope = app.Services.CreateAsyncScope();
        var services = scope.ServiceProvider;
        var fixture = await Fixture.CreateAsync(services);
        var failure = new IOException("Ordinary native commit acknowledgement probe.");
        probe.Failure = failure;
        probe.FailAfterCommit = afterCommit;
        var observed = await Record.ExceptionAsync(() => OrdinaryCreateAsync(services, OrdinaryOwner(fixture)));
        Assert.Same(failure, observed);
        Assert.True(probe.NativeFlushes > 0);
        await AssertNativeCountAsync(services, fixture.Project.ProjectId, afterCommit ? 1 : 0);
        await fixture.RevokeAsync(Revocation.Write).WaitAsync(TimeSpan.FromSeconds(10));
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public async Task Ordinary_non_task_ceiling_and_current_task_denial_are_checked_on_actual_native_targets(bool currentDenied, bool linkOnly) {
        await using var app = await TestApplication.CreateAsync(OrdinaryHarness());
        await using var scope = app.Services.CreateAsyncScope();
        var services = scope.ServiceProvider;
        var fixture = await Fixture.CreateAsync(services);
        var nodes = await SeedOrdinaryGraphAsync(services, fixture.Project.ProjectId, tasks: true);
        var limited = AgentProjectStructureAccessMetadata.Read(fixture.Agent.ConfigurationJson);
        limited.CanWrite = false;
        limited.CanWriteTasks = false;
        limited.CanWriteNonTaskStructure = true;
        var owner = OrdinaryOwner(fixture, captured: currentDenied ? null : limited);
        if (currentDenied) {
            await fixture.Writer.UpdateCatalogAsync(catalog => catalog with {
                Agents = catalog.Agents.Select(agent => agent.Id == fixture.Agent.Id
                    ? agent with { ConfigurationJson = AgentProjectStructureAccessMetadata.Write(agent.ConfigurationJson, limited) }
                    : agent).ToArray()
            });
        } else {
            limited.CanWriteTasks = true;
            limited.CanWrite = true;
        }
        var workbench = services.GetRequiredService<ProjectWorkbenchService>();
        var failure = await Assert.ThrowsAsync<ProjectStructureAgentException>(() => linkOnly
            ? workbench.UnlinkObjectsAsync(fixture.Project.ProjectId, nodes.First, nodes.Second, ProjectObjectLinkKind.DependsOn, mutationOwner: owner)
            : workbench.MoveObjectAsync(fixture.Project.ProjectId, nodes.First, 90, 90, mutationOwner: owner));
        Assert.Equal("ProjectTaskWriteDenied", failure.ErrorCode);
        await using var read = await services.GetRequiredService<IDbContextFactory<WorkbenchDbContext>>().CreateDbContextAsync();
        Assert.Equal(10, (await read.Set<ProjectObjectRecord>().SingleAsync(row => row.NodeKey == nodes.First)).PositionX);
        Assert.True(await read.Set<ProjectObjectLinkRecord>().AnyAsync(row => row.ProjectId == fixture.Project.ProjectId));
    }

    [Fact]
    public async Task Ordinary_Agent_missing_native_policy_fails_before_any_owner_write() {
        await using var app = await TestApplication.CreateAsync(OrdinaryHarness(missingPolicy: true));
        await using var scope = app.Services.CreateAsyncScope();
        var services = scope.ServiceProvider;
        var fixture = await Fixture.CreateAsync(services);
        var failure = await Assert.ThrowsAsync<InvalidOperationException>(() => OrdinaryCreateAsync(services, OrdinaryOwner(fixture)));
        Assert.Contains("Ordinary Agent native mutation authority is not installed", failure.Message, StringComparison.Ordinal);
        await AssertNativeCountAsync(services, fixture.Project.ProjectId, 0);
    }

    [Fact]
    public async Task Ordinary_Agent_cannot_acquire_catalog_authority_after_a_native_transaction_has_started() {
        await using var app = await TestApplication.CreateAsync(OrdinaryHarness());
        await using var scope = app.Services.CreateAsyncScope();
        var services = scope.ServiceProvider;
        var fixture = await Fixture.CreateAsync(services);
        var owner = OrdinaryOwner(fixture);
        await using var context = await services.GetRequiredService<IDbContextFactory<WorkbenchDbContext>>().CreateDbContextAsync();
        await using var transaction = await context.Database.BeginTransactionAsync();
        var failure = await Assert.ThrowsAsync<InvalidOperationException>(() => services.GetRequiredService<ProjectStructureMutationScopeFactory>().BeginAsync(
            context, ProjectMutationScopeKeys.ForProject(fixture.Project.ProjectId), default, [owner.ExpectedProjectAdmission!], agentAdmission: owner.AgentMutationAdmission));
        Assert.Contains("before the native transaction", failure.Message, StringComparison.Ordinal);
        await transaction.RollbackAsync();
        await AssertNativeCountAsync(services, fixture.Project.ProjectId, 0);
    }

    [Fact]
    public async Task Ordinary_Agent_generation_change_after_owner_flush_rolls_back_before_commit() {
        var probe = new NativeCommitProbe();
        var generations = new OrdinaryGenerations();
        await using var app = await TestApplication.CreateAsync(OrdinaryHarness(probe, generations: generations));
        await using var scope = app.Services.CreateAsyncScope();
        var services = scope.ServiceProvider;
        var fixture = await Fixture.CreateAsync(services);
        var governance = new AgentExecutionGovernanceSnapshot(new(Guid.NewGuid()), fixture.Agent.Id, fixture.Project.DatabaseProfileId,
            generations.GetGeneration(), WorkspaceScopeDescriptor.Organization(fixture.Project.DatabaseProfileId.ToString("N")),
            true, true, "ordinary-source-test", "ordinary-source-test-fingerprint");
        var owner = OrdinaryOwner(fixture, governance: governance);
        probe.AfterFlush = () => generations.Generation = new DatabaseProfileGeneration(10);
        var failure = await Assert.ThrowsAsync<ProjectStructureAgentException>(() => OrdinaryCreateAsync(services, owner));
        Assert.Equal("ProjectMutationAuthorityDenied", failure.ErrorCode);
        Assert.True(probe.NativeFlushes > 0);
        await AssertNativeCountAsync(services, fixture.Project.ProjectId, 0);
        await fixture.RevokeAsync(Revocation.Write).WaitAsync(TimeSpan.FromSeconds(10));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Ordinary_Gantt_requires_the_displayed_project_lifetime_instead_of_rebinding_current_ID(bool missing) {
        await using var app = await TestApplication.CreateAsync(OrdinaryHarness());
        await using var scope = app.Services.CreateAsyncScope();
        var services = scope.ServiceProvider;
        var fixture = await Fixture.CreateAsync(services);
        var original = OrdinaryOwner(fixture) with { AgentMutationAdmission = null };
        var projectId = fixture.Project.ProjectId;
        if (!missing) {
            await services.GetRequiredService<ProjectsService>().DeleteAsync(projectId);
            Assert.True((await services.GetRequiredService<ProjectsService>().CreateAsync(projectId, new() { Name = "Recreated Gantt project" })).IsSuccess);
            var current = Assert.IsType<ProjectWriteAdmission>(await services.GetRequiredService<ProjectWriteAdmissionService>().CaptureAsync(projectId));
            Assert.NotEqual(original.ExpectedProjectAdmission!.LifetimeId, current.LifetimeId);
        }
        var nodes = await SeedOrdinaryGraphAsync(services, projectId, tasks: true);
        var failure = await Record.ExceptionAsync(() => services.GetRequiredService<ProjectStructureGanttMutationService>().ApplyTitleAsync(projectId,
            new(new GanttTaskId(nodes.First), "First", "Must not be written"), mutationOwner: missing ? null : original));
        Assert.NotNull(failure);
        if (!missing) {
            var rejected = Assert.IsType<ProjectWriteAdmissionRejectedException>(failure);
            Assert.Equal(original.ExpectedProjectAdmission, rejected.Admission);
        } else {
            var rejected = Assert.IsType<InvalidOperationException>(failure);
            Assert.Contains("captured by its caller", rejected.Message, StringComparison.Ordinal);
        }
        await using var read = await services.GetRequiredService<IDbContextFactory<WorkbenchDbContext>>().CreateDbContextAsync();
        Assert.Equal("First", (await read.Set<ProjectObjectRecord>().SingleAsync(row => row.NodeKey == nodes.First)).Title);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Ordinary_registered_tool_keeps_its_captured_actor_until_the_final_native_gate(bool taskTool) {
        var native = new OrdinaryCatalogProbe();
        await using var app = await TestApplication.CreateAsync(OrdinaryHarness(catalogProbe: native));
        await using var scope = app.Services.CreateAsyncScope();
        var services = scope.ServiceProvider;
        var fixture = await Fixture.CreateAsync(services);
        var nodes = taskTool ? default : await SeedOrdinaryGraphAsync(services, fixture.Project.ProjectId);
        var toolName = taskTool ? ProjectStructureToolPolicy.ProjectTaskCreate : ProjectStructureToolPolicy.ProjectStructureNodeMove;
        var context = new AgentRuntimeToolProviderContext(fixture.Agent, (await fixture.Writer.LoadCatalogAsync()).Providers.First(), [],
            false, AgentRuntimeToolProviderPurpose.InteractiveChat, "ordinary-native-producer", AgentRuntimeContextIntent.Empty with {
                SourceKind = ProjectStructureAgentChatContextBuilder.SourceKind,
                SourceId = fixture.Project.ProjectId.ToString("D"),
                WorkspaceScope = WorkspaceScopeDescriptor.Organization(fixture.Project.DatabaseProfileId.ToString("N")),
                Purpose = AgentRuntimeContextPurpose.InteractiveChat
            }, new Dictionary<string, string>()) {
            Governance = new AgentExecutionGovernanceSnapshot(new(Guid.NewGuid()), fixture.Agent.Id, fixture.Project.DatabaseProfileId,
                services.GetRequiredService<IAgentExecutionProfileGenerationSource>().GetGeneration(),
                WorkspaceScopeDescriptor.Organization(fixture.Project.DatabaseProfileId.ToString("N")), true, true,
                "ordinary-producer", "ordinary-producer-fingerprint", allowedOperations: [toolName])
        };
        var provider = Assert.Single(services.GetServices<IAgentRuntimeToolProvider>().OfType<ProjectStructureAgentRuntimeToolProvider>());
        var tool = Assert.Single((await provider.CreateToolsAsync(context, default)).OfType<AIFunction>(), item => item.Name == toolName);
        native.BeforeRead = () => fixture.RevokeAsync(Revocation.Write);
        using var owned = AgentRuntimeToolOwnershipContext.BeginScope(new(provider.Descriptor.ProviderKey, provider.Descriptor.DisplayName, toolName));
        var request = taskTool
            ? (object)new ProjectStructureTaskCreateRequest("Producer task", DateTimeOffset.UtcNow, DateTimeOffset.UtcNow.AddHours(1))
            : new ProjectStructureNodeMoveInput(nodes.First, 95, 95);
        var failure = await Assert.ThrowsAsync<ProjectStructureAgentException>(async () => {
            await tool.InvokeAsync(new AIFunctionArguments { ["projectId"] = fixture.Project.ProjectId, ["request"] = request });
        });
        Assert.Equal("ProjectMutationAuthorityDenied", failure.ErrorCode);
        Assert.Equal(1, native.Reads);
        await AssertNativeCountAsync(services, fixture.Project.ProjectId, taskTool ? 0 : 2);
    }

    private static TestHarnessOptions OrdinaryHarness(NativeCommitProbe? probe = null, bool missingPolicy = false,
        OrdinaryGenerations? generations = null, OrdinaryCatalogProbe? catalogProbe = null)
        => new() { ConfigureServices = services => {
            NativeHarness(new NativeClock(), probe).ConfigureServices!(services);
            services.AddScoped<IProjectCreationCompensationGuard, ProjectCreationCompensationGuard>();
            services.AddScoped<IProjectPartyDeletionStateQuery>(provider => provider.GetRequiredService<CanDoItAll.Modules.CrmHr.ProjectPartyIntegrationService>());
            services.AddScoped<ProjectAgentSourceMutationAuthority>();
            if (generations is not null) {
                services.AddSingleton<IAgentExecutionProfileGenerationSource>(generations);
            }
            services.AddScoped<ProjectAgentNativeMutationService>(provider => {
                var database = provider.GetRequiredService<ICanonicalRuntimeDatabase>();
                var admissions = provider.GetRequiredService<ProjectWriteAdmissionService>();
                IAgentCatalogReadLeaseStore source = new CanonicalAgentCatalogLeaseSource(database, provider.GetRequiredService<IOptions<StorageOptions>>(),
                    provider.GetRequiredService<IHostEnvironment>(), admissions);
                if (catalogProbe is not null) {
                    source = new OrdinaryCatalogStore(source, catalogProbe);
                }
                return new(database, source, provider.GetRequiredService<IAgentExecutionProfileGenerationSource>(),
                    admissions, provider.GetRequiredService<CoordinatedDatabaseTransaction>());
            });
            if (missingPolicy) {
                services.AddScoped(provider => new ProjectStructureMutationScopeFactory(provider.GetRequiredService<ProjectRecordQueryService>(),
                    provider.GetRequiredService<ProjectWriteAdmissionService>(), provider.GetRequiredService<CoordinatedDatabaseTransaction>()));
            } else {
                services.AddScoped<ProjectStructureMutationScopeFactory>();
            }
        } };

    private static ProjectStructureAgentContext OrdinaryOwner(Fixture fixture, ProjectAgentMutationDomain domain = ProjectAgentMutationDomain.NonTaskStructure,
        AgentProjectStructureAccessSettings? captured = null, AgentExecutionGovernanceSnapshot? governance = null)
        => new(fixture.Agent.Id.ToString("D"), "Ordinary source", "test", "test", "", fixture.Agent.Id.ToString("D")) {
            ExpectedProjectAdmission = new(fixture.Project.DatabaseProfileId, fixture.Project.ProjectId, fixture.Project.LifetimeId),
            AgentMutationAdmission = new(fixture.Agent.Id, fixture.Project.DatabaseProfileId,
                captured ?? AgentProjectStructureAccessMetadata.Read(fixture.Agent.ConfigurationJson), governance, domain, null)
        };

    private static Task<ProjectStructureNode> OrdinaryCreateAsync(IServiceProvider services, ProjectStructureAgentContext owner)
        => services.GetRequiredService<ProjectWorkbenchService>().CreateObjectAsync(owner.ExpectedProjectAdmission!.ProjectId,
            new(ProjectObjectType.ProjectBlock, "Native authority", "", "", $"project:{owner.ExpectedProjectAdmission.ProjectId}") {
                ExpectedProjectAdmission = owner.ExpectedProjectAdmission, AgentMutationAdmission = owner.AgentMutationAdmission
            });

    private static Task OrdinaryGraphChangeAsync(IServiceProvider services, ProjectStructureAgentContext owner,
        (string First, string Second) nodes, OrdinaryGraphChange change) {
        var service = services.GetRequiredService<ProjectStructureAgentService>();
        var projectId = owner.ExpectedProjectAdmission!.ProjectId;
        return change switch {
            OrdinaryGraphChange.Move => service.MoveNodeAsync(projectId, new(nodes.First, 50, 50), owner),
            OrdinaryGraphChange.Recompose => service.RecomposeNodeAsync(projectId, new(nodes.First), owner),
            OrdinaryGraphChange.Reparent => service.ReparentNodeAsync(projectId, new(nodes.Second, $"project:{projectId}"), owner),
            OrdinaryGraphChange.Link => service.LinkNodesAsync(projectId, new(nodes.First, nodes.Second), owner),
            OrdinaryGraphChange.Unlink => service.UnlinkNodesAsync(projectId, new(nodes.First, nodes.Second), owner),
            OrdinaryGraphChange.Copy => service.CopyNodesAsync(projectId, new([nodes.First], $"project:{projectId}"), owner, ProjectStructureClipboardCopyTaskPolicy.AllowCanonicalTasks),
            OrdinaryGraphChange.Delete => service.DeleteNodeDetailedAsync(projectId, nodes.First, new(ProjectStructureManagedStorageDisposition.RetainManagedFiles), owner),
            OrdinaryGraphChange.Import => service.ImportAsync(new(projectId, null, ProjectStructureImportSourceKind.JsonOutline, "Import", "[]", LeafWorkItemSubtype: "feature"), owner),
            OrdinaryGraphChange.PromptCommand => service.ExecuteNodeCommandAsync(projectId, nodes.First, new(ProjectStructureCommandKind.Open), owner),
            OrdinaryGraphChange.Approval => service.CreateApprovalRequestAsync(projectId, new("Approval", "", "", "Requested operation"), owner),
            _ => throw new ArgumentOutOfRangeException(nameof(change))
        };
    }

    private static Task OrdinaryTaskChangeAsync(IServiceProvider services, ProjectStructureAgentContext owner,
        (string First, string Second) nodes, OrdinaryTaskChange change) {
        var projectId = owner.ExpectedProjectAdmission!.ProjectId;
        var state = new ProjectStructureTaskEditState(ProjectTaskEstimate.Empty(), ProjectTaskExecutionSnapshot.Unknown, null, 0);
        return change switch {
            OrdinaryTaskChange.Title => services.GetRequiredService<ProjectStructureGanttMutationService>()
                .ApplyTitleAsync(projectId, new(new GanttTaskId(nodes.First), "First", "Edited"), mutationOwner: owner),
            OrdinaryTaskChange.RowOrder => services.GetRequiredService<ProjectStructureGanttRowOrderService>()
                .MoveAsync(projectId, new(nodes.Second, nodes.First, ProjectStructureGanttRowPlacement.Before), owner),
            OrdinaryTaskChange.Pricing => services.GetRequiredService<ProjectStructureTaskPricingPersistenceService>()
                .CommitAsync(new(projectId, nodes.First, new(ProjectStructureTaskResourceKind.Person, Guid.NewGuid()),
                    ProjectTaskExecutionSnapshot.Unknown, ProjectTaskEstimate.Empty(), null, null!, owner),
                    metadata => ProjectStructureTaskEditStatePolicy.WritePricingAndExecution(metadata,
                        new(1m, ProjectWorkItemEffortUnit.Hours, 2m, "USD"), ProjectTaskExecutionSnapshot.Unknown, null), default),
            OrdinaryTaskChange.Compensation => services.GetRequiredService<ProjectStructureTaskEditCompensationService>()
                .RestorePricingAsync(projectId, nodes.First, state, state with { Estimate = new(1m, ProjectWorkItemEffortUnit.Hours, 3m, "USD") }, mutationOwner: owner),
            OrdinaryTaskChange.Assignee => services.GetRequiredService<ProjectStructureWorkItemAssigneeService>()
                .ReplaceAsync(projectId, nodes.First, null, "Ordinary source fixture", mutationOwner: owner),
            _ => throw new ArgumentOutOfRangeException(nameof(change))
        };
    }

    private static async Task<(string First, string Second)> SeedOrdinaryGraphAsync(IServiceProvider services, Guid projectId, bool tasks = false) {
        await using var context = await services.GetRequiredService<IDbContextFactory<WorkbenchDbContext>>().CreateDbContextAsync();
        var first = $"custom:{Guid.NewGuid():N}";
        var second = $"custom:{Guid.NewGuid():N}";
        var start = new DateTimeOffset(2026, 9, 10, 8, 0, 0, TimeSpan.Zero);
        context.Set<ProjectObjectRecord>().AddRange(new[] { first, second }.Select((key, index) => new ProjectObjectRecord {
            Id = Guid.NewGuid(), ProjectId = projectId, NodeKey = key,
            ObjectType = tasks ? ProjectObjectType.WorkItem : ProjectObjectType.ProjectBlock,
            ObjectSubtype = tasks ? "task" : "", Title = index == 0 ? "First" : "Second", ParentNodeKey = index == 0 ? $"project:{projectId}" : first,
            PositionX = 10 + index * 10, PositionY = 10, MetadataJson = "{}", MarkersJson = "[]",
            StartUtc = tasks ? start.AddHours(index) : null, EndUtc = tasks ? start.AddHours(index + 1) : null,
            DurationSeconds = tasks ? 3600 : null, CreatedAtUtc = start, UpdatedAtUtc = start
        }));
        context.Set<ProjectObjectLinkRecord>().Add(new() {
            Id = Guid.NewGuid(), ProjectId = projectId, SourceNodeKey = first, TargetNodeKey = second,
            LinkKind = ProjectObjectLinkKind.DependsOn, CreatedAtUtc = start
        });
        await context.SaveChangesAsync();
        return (first, second);
    }

    private sealed class GanttRowCommitProbe(CatalogCommitProbe catalog) : DbTransactionInterceptor {
        private DbTransaction? rowTransaction;

        public override async ValueTask<InterceptionResult> TransactionCommittingAsync(DbTransaction transaction,
            TransactionEventData eventData, InterceptionResult result, CancellationToken cancellationToken = default) {
            if (eventData.Context is not WorkbenchDbContext context ||
                    !context.ChangeTracker.Entries<ProjectWorkbenchViewStateRecord>().Any()) {
                return result;
            }
            Assert.Null(rowTransaction);
            rowTransaction = transaction;
            return await catalog.TransactionCommittingAsync(transaction, eventData, result, cancellationToken);
        }

        public override Task TransactionCommittedAsync(DbTransaction transaction, TransactionEndEventData eventData,
            CancellationToken cancellationToken = default) => ReferenceEquals(rowTransaction, transaction)
                ? catalog.TransactionCommittedAsync(transaction, eventData, cancellationToken)
                : Task.CompletedTask;
    }

    private sealed class OrdinaryGenerations : IAgentExecutionProfileGenerationSource {
        public DatabaseProfileGeneration Generation { get; set; } = new(9);
        public DatabaseProfileGeneration GetGeneration() => Generation;
    }

    private sealed class OrdinaryCatalogProbe {
        public int Reads { get; set; }
        public Func<Task>? BeforeRead { get; set; }
    }

    private sealed class OrdinaryCatalogStore(IAgentCatalogReadLeaseStore inner, OrdinaryCatalogProbe probe) : IAgentCatalogReadLeaseStore {
        public async Task<IAgentCatalogReadLease> AcquireAgentReadLeaseAsync(Guid agentId, CancellationToken cancellationToken = default) {
            var before = probe.BeforeRead;
            probe.BeforeRead = null;
            probe.Reads++;
            if (before is not null) {
                await before();
            }
            return await inner.AcquireAgentReadLeaseAsync(agentId, cancellationToken);
        }
    }
}
