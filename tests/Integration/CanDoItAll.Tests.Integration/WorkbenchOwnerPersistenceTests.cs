using CanDoItAll.Modules.CrmHr;
using System.Data;
using System.Data.Common;
using CanDoItAll.Infrastructure.Persistence;
using CanDoItAll.Modules.Processes;
using CanDoItAll.Modules.Projects;
using CanDoItAll.Modules.Prompts;
using CanDoItAll.Modules.Workbench;
using CanDoItAll.Processes.Application;
using CanDoItAll.Processes.Persistence;
using CanDoItAll.Processes.Runtime;
using CanDoItAll.SharedKernel;
using CanDoItAll.Tests.Support;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.DependencyInjection;

namespace CanDoItAll.Tests.Integration;

public sealed class WorkbenchOwnerPersistenceTests {
    private static readonly DateTimeOffset Now = new(2026, 3, 4, 5, 6, 7, TimeSpan.Zero);

    [Fact]
    public async Task Fifteen_owner_mappings_match_the_complete_schema_and_reject_foreign_entities() {
        await using var application = await TestApplication.CreateAsync();
        await using var complete = await application.Services.GetRequiredService<IDbContextFactory<AppDbContext>>().CreateDbContextAsync();
        await using var owner = await application.Services.GetRequiredService<IDbContextFactory<WorkbenchDbContext>>().CreateDbContextAsync();
        Type[] expected = [typeof(ProjectObjectRecord), typeof(ProjectObjectLinkRecord), typeof(ProjectWorkbenchViewStateRecord),
            typeof(ProjectStructureProjectionLayoutRecord), typeof(ProjectStructureOperationAnalyticsRecord), typeof(ProjectStructureLeaseRecord),
            typeof(ProjectNodeBindingRecord), typeof(ProjectNodeReferenceRecord), typeof(ProjectNodeLifecycleEventRecord),
            typeof(ProjectCrossModuleMutationRecord), typeof(ProjectWorkflowContributionRecord), typeof(ProjectWorkflowAdmissionRecord),
            typeof(ProjectWorkAssignmentRecord), typeof(ProjectProcessAssetContributionRecord), typeof(ProjectWorkAssignmentHistoryRecord)];
        var entities = owner.GetService<IDesignTimeModel>().Model.GetEntityTypes().ToArray();
        Assert.Equal(expected.OrderBy(type => type.FullName), entities.Select(entity => entity.ClrType).OrderBy(type => type.FullName));
        foreach (var entity in entities) {
            var canonical = Assert.IsAssignableFrom<IEntityType>(complete.GetService<IDesignTimeModel>().Model.FindEntityType(entity.ClrType));
            if (entity.ClrType == typeof(ProjectWorkAssignmentRecord)) {
                Assert.Equal(canonical.GetProperties().Select(property => (property.Name, property.ClrType, property.IsNullable,
                        property.IsConcurrencyToken, property.ValueGenerated, property.GetColumnType(), property.GetMaxLength())),
                    entity.GetProperties().Select(property => (property.Name, property.ClrType, property.IsNullable,
                        property.IsConcurrencyToken, property.ValueGenerated, property.GetColumnType(), property.GetMaxLength())));
                Assert.Equal(canonical.GetKeys().Select(key => key.ToDebugString()), entity.GetKeys().Select(key => key.ToDebugString()));
                Assert.Equal(canonical.GetIndexes().Select(index => index.ToDebugString()), entity.GetIndexes().Select(index => index.ToDebugString()));
                var affiliationKey = Assert.Single(canonical.GetForeignKeys());
                Assert.Equal(typeof(PartyOrganizationAffiliation), affiliationKey.PrincipalEntityType.ClrType);
                Assert.Equal(DeleteBehavior.Restrict, affiliationKey.DeleteBehavior);
                Assert.Equal(nameof(ProjectWorkAssignmentRecord.PartyOrganizationAffiliationId), Assert.Single(affiliationKey.Properties).Name);
                Assert.Empty(entity.GetForeignKeys());
            } else {
                Assert.Equal(canonical.ToDebugString(MetadataDebugStringOptions.LongDefault), entity.ToDebugString(MetadataDebugStringOptions.LongDefault));
            }
        }
        Assert.Throws<InvalidOperationException>(() => owner.Set<Project>().ToQueryString());
        Assert.Throws<InvalidOperationException>(() => owner.Set<PromptArtifact>().ToQueryString());
        Assert.Throws<InvalidOperationException>(() => owner.Set<ProcessRuntimeStateEntity>().ToQueryString());
    }

    [Fact]
    public async Task Legacy_native_identity_bindings_and_view_state_survive_restart_owner_edits_and_profile_isolation() {
        await using var environment = CanDoItAllTestEnvironment.Create("workbench-owner-restart");
        var original = environment.CreatePostgreSqlProfile("original");
        var other = environment.CreatePostgreSqlProfile("other");
        var options = new TestHarnessOptions { TestEnvironment = environment, ActiveProfile = original };
        var project = Project("Legacy native project");
        var node = new ProjectObjectRecord { ProjectId = project.Id, NodeKey = $"node:{Guid.NewGuid():N}",
            ObjectType = ProjectObjectType.ProjectBlock, Title = "Original native title", Notes = "Human notes", MetadataJson = "{\"legacy\":true}",
            MarkersJson = "[]", CreatedAtUtc = Now, UpdatedAtUtc = Now };
        var binding = new ProjectNodeBindingRecord { ProjectObjectId = node.Id, Route = "/legacy/native", ExternalArtifactKind = "legacy",
            ExternalArtifactId = Guid.NewGuid(), CreatedAtUtc = Now, UpdatedAtUtc = Now };
        var view = new ProjectWorkbenchViewStateRecord { ProjectId = project.Id, SurfaceKind = "structure", StateJson = "{\"zoom\":1.25}", UpdatedAtUtc = Now };
        await using (var initial = await TestApplication.CreateAsync(options)) {
            await using var complete = await initial.Services.GetRequiredService<IDbContextFactory<AppDbContext>>().CreateDbContextAsync();
            complete.AddRange(project, node, binding, view);
            await complete.SaveChangesAsync();
        }
        await using var restarted = await TestApplication.CreateAsync(options);
        await using var scope = restarted.Services.CreateAsyncScope();
        var service = scope.ServiceProvider.GetRequiredService<ProjectWorkbenchService>();
        var loaded = await service.GetStructureAsync(project.Id);
        var loadedNode = Assert.Single(loaded.Nodes, item => item.Id == node.NodeKey);
        Assert.Equal(node.Title, loadedNode.Title);
        await using (var owner = await restarted.Services.GetRequiredService<IDbContextFactory<WorkbenchDbContext>>().CreateDbContextAsync()) {
            var saved = await owner.Set<ProjectObjectRecord>().SingleAsync(item => item.Id == node.Id);
            Assert.Equal(node.Notes, saved.Notes);
            Assert.Equal(node.MetadataJson, saved.MetadataJson);
            Assert.Equal(binding.Id, (await owner.Set<ProjectNodeBindingRecord>().SingleAsync(item => item.ProjectObjectId == node.Id)).Id);
            saved.Title = "Owner edit";
            saved.UpdatedAtUtc = Now.AddMinutes(1);
            owner.SaveChanges();
            saved.Notes = "Second owner edit";
            await owner.SaveChangesAsync();
        }
        await using (var complete = await restarted.Services.GetRequiredService<IDbContextFactory<AppDbContext>>().CreateDbContextAsync()) {
            var saved = await complete.Set<ProjectObjectRecord>().SingleAsync(item => item.Id == node.Id);
            Assert.Equal("Owner edit", saved.Title);
            Assert.Equal("Second owner edit", saved.Notes);
            Assert.Equal(node.CreatedAtUtc, saved.CreatedAtUtc);
            Assert.Equal(binding.Route, (await complete.Set<ProjectNodeBindingRecord>().SingleAsync(item => item.Id == binding.Id)).Route);
            Assert.Equal(view.StateJson, (await complete.Set<ProjectWorkbenchViewStateRecord>().SingleAsync(item => item.Id == view.Id)).StateJson);
        }
        await using var isolated = await TestApplication.CreateAsync(new TestHarnessOptions { TestEnvironment = environment, ActiveProfile = other });
        await using var otherOwner = await isolated.Services.GetRequiredService<IDbContextFactory<WorkbenchDbContext>>().CreateDbContextAsync();
        Assert.False(await otherOwner.Set<ProjectObjectRecord>().AnyAsync(item => item.Id == node.Id));
        Assert.Equal("Owner edit", Assert.Single((await service.GetStructureAsync(project.Id)).Nodes, item => item.Id == node.NodeKey).Title);
    }

    [Fact]
    public async Task Hierarchy_projection_reads_only_the_descendant_closure_and_its_external_parents() {
        await using var application = await TestApplication.CreateAsync();
        var root = Project("Root");
        var first = Project("A child");
        var second = Project("B child");
        var external = Project("External parent");
        var unrelated = Enumerable.Range(0, ProjectStructureProjectionQueryLimits.MaximumProjects + 1)
            .Select(index => Project($"Unrelated {index:D4}")).ToArray();
        var phase = new ProjectPhase { ProjectId = root.Id, Name = "Execution", Goal = "Keep phase dates", Status = ProjectPhaseStatus.Active,
            OrderIndex = 1, StartDateUtc = Now.UtcDateTime, EndDateUtc = Now.UtcDateTime.AddDays(1) };
        await using (var complete = await application.Services.GetRequiredService<IDbContextFactory<AppDbContext>>().CreateDbContextAsync()) {
            complete.AddRange(root, first, second, external, phase);
            complete.AddRange(unrelated);
            complete.AddRange(Link(root, first), Link(first, second), Link(external, root), Link(external, second), Link(second, root));
            await complete.SaveChangesAsync();
        }
        await using var scope = application.Services.CreateAsyncScope();
        var facts = Assert.IsType<ProjectStructureProjectFacts>(await scope.ServiceProvider.GetRequiredService<ProjectStructureProjectionQueryService>().GetAsync(root.Id));
        Assert.Equal(new[] { root.Id, first.Id, second.Id, external.Id }.OrderBy(id => id), facts.Projects.Select(item => item.Id).OrderBy(id => id));
        Assert.Equal(5, facts.HierarchyLinks.Count);
        Assert.Equal(phase.Id, Assert.Single(facts.Phases).Id);
        Assert.Equal(phase.StartDateUtc, facts.Phases[0].StartDateUtc);
        var context = new ProjectStructureProjectionContext(root.Id, Now, new Dictionary<string, ProjectStructureProjectionLayoutRecord>());
        await new ProjectHierarchyProjectionContributor(new FixedClock(), scope.ServiceProvider.GetRequiredService<ProjectStructureProjectionQueryService>())
            .ContributeAsync(context, CancellationToken.None);
        Assert.Contains(context.Nodes, item => item.NodeKey == $"project-child:{second.Id}" && item.Title == second.Name);
        Assert.Contains(context.Nodes, item => item.NodeKey == $"project-related-parent:{external.Id}" && item.Title == external.Name);
        Assert.DoesNotContain(context.Nodes, item => unrelated.Any(project => item.Title == project.Name));
    }

    [Fact]
    public async Task Project_prompt_and_process_reads_share_the_native_snapshot_while_normal_queries_remain_independent() {
        await using var application = await TestApplication.CreateAsync();
        await using var scope = application.Services.CreateAsyncScope();
        var project = Project("Snapshot project");
        var prompt = new PromptArtifact { ProjectId = project.Id, Title = "Original prompt", Kind = PromptGalleryItemKind.FullPrompt,
            CreatedAtUtc = Now, UpdatedAtUtc = Now };
        var run = new ProcessRuntimeStateEntity { RunId = Guid.NewGuid(), PlanId = Guid.NewGuid(), Status = ProcessRuntimeStatus.Active, UpdatedAtUtc = Now };
        run.RootRunId = run.RunId;
        await using (var complete = await application.Services.GetRequiredService<IDbContextFactory<AppDbContext>>().CreateDbContextAsync()) {
            complete.AddRange(project, prompt);
            await complete.SaveChangesAsync();
        }
        await using (var process = await scope.ServiceProvider.GetRequiredService<IDbContextFactory<ProcessPersistenceDbContext>>().CreateDbContextAsync()) {
            process.Add(run);
            await process.SaveChangesAsync();
        }
        var projects = scope.ServiceProvider.GetRequiredService<ProjectStructureProjectionQueryService>();
        var prompts = scope.ServiceProvider.GetRequiredService<IPromptArtifactProjectionQueryService>();
        var processes = scope.ServiceProvider.GetRequiredService<IProcessStructureProjectionQueryService>();
        var transactions = scope.ServiceProvider.GetRequiredService<CoordinatedDatabaseTransaction>();
        await using var native = await application.Services.GetRequiredService<IDbContextFactory<WorkbenchDbContext>>().CreateDbContextAsync();
        await using var transaction = await native.Database.BeginTransactionAsync(IsolationLevel.Serializable);
        using (transactions.Enter(native)) {
            Assert.Equal(project.Name, (await projects.GetForMutationAsync(project.Id))!.Project.Name);
            Assert.Equal(prompt.Title, Assert.Single(await prompts.ListProjectFactsForMutationAsync(project.Id)).Title);
            Assert.Equal(ProcessRuntimeStatus.Active, Assert.Single((await processes.GetRuntimeFactsForMutationAsync([run.RunId])).RootRuns).Status);
            await using (var independent = await application.Services.GetRequiredService<IDbContextFactory<ProjectsDbContext>>().CreateDbContextAsync()) {
                (await independent.Set<Project>().SingleAsync(item => item.Id == project.Id)).Name = "Committed project";
                await independent.SaveChangesAsync();
            }
            await using (var independent = await application.Services.GetRequiredService<IDbContextFactory<PromptsDbContext>>().CreateDbContextAsync()) {
                var changed = await independent.Set<PromptArtifact>().SingleAsync(item => item.Id == prompt.Id);
                changed.Title = "Committed prompt";
                changed.IsArchived = true;
                await independent.SaveChangesAsync();
            }
            await using (var independent = await scope.ServiceProvider.GetRequiredService<IDbContextFactory<ProcessPersistenceDbContext>>().CreateDbContextAsync()) {
                (await independent.RuntimeStates.SingleAsync(item => item.RunId == run.RunId)).Status = ProcessRuntimeStatus.Completed;
                await independent.SaveChangesAsync();
            }
            Assert.Equal("Committed project", (await projects.GetAsync(project.Id))!.Project.Name);
            Assert.Empty(await prompts.ListProjectFactsAsync(project.Id));
            Assert.Equal(prompt.Id, (await prompts.GetBindingFactAsync(prompt.Id))!.Id);
            var nodeScope = scope.ServiceProvider.GetRequiredService<IProjectNodeScopeBridge>();
            Assert.Equal(new ProjectNodeScopeResolution(true, false, false, ProjectObjectType.PromptFlow, string.Empty),
                await nodeScope.ResolveAsync(project.Id, new($"prompt:{prompt.Id}")));
            Assert.Equal(new ProjectNodeScopeResolution(false, true, false, ProjectObjectType.PromptFlow, string.Empty),
                await nodeScope.ResolveAsync(Guid.NewGuid(), new($"prompt:{prompt.Id}")));
            Assert.Equal(ProcessRuntimeStatus.Completed, Assert.Single((await processes.GetRuntimeFactsAsync([run.RunId])).RootRuns).Status);
            Assert.Equal(project.Name, (await projects.GetForMutationAsync(project.Id))!.Project.Name);
            Assert.Equal(prompt.Title, Assert.Single(await prompts.ListProjectFactsForMutationAsync(project.Id)).Title);
            Assert.Equal(ProcessRuntimeStatus.Active, Assert.Single((await processes.GetRuntimeFactsForMutationAsync([run.RunId])).RootRuns).Status);
            Assert.Same(transaction, native.Database.CurrentTransaction);
        }
        await Assert.ThrowsAsync<InvalidOperationException>(() => projects.GetForMutationAsync(project.Id));
        await Assert.ThrowsAsync<InvalidOperationException>(() => prompts.GetBindingFactForMutationAsync(prompt.Id));
        await Assert.ThrowsAsync<InvalidOperationException>(() => processes.GetRuntimeFactsForMutationAsync([run.RunId]));
        await transaction.CommitAsync();
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Prompt_and_native_binding_commit_together_before_a_lost_commit_acknowledgement(bool afterCommit) {
        var fault = new PromptCommitFault(afterCommit);
        var writes = new PromptWriteProbe(fault);
        await using var application = await TestApplication.CreateAsync(new TestHarnessOptions {
            ConfigureServices = services => {
                services.ConfigureDbContext<WorkbenchDbContext>(options => options.AddInterceptors(fault, writes), ServiceLifetime.Singleton);
                services.ConfigureDbContext<PromptsDbContext>(options => options.AddInterceptors(writes), ServiceLifetime.Singleton);
            }
        });
        var project = await SeedProjectAsync(application);
        await using var scope = application.Services.CreateAsyncScope();
        var service = scope.ServiceProvider.GetRequiredService<ProjectWorkbenchService>();
        fault.Enabled = true;
        var failure = await Assert.ThrowsAsync<ArgumentException>(() => service.CreateObjectAsync(project.Id, PromptRequest(project.Id)));
        Assert.Same(fault.Failure, failure);
        Assert.True(fault.BothFlushed);
        Assert.Same(writes.NativeTransaction, writes.PromptTransaction);
        await using var complete = await application.Services.GetRequiredService<IDbContextFactory<AppDbContext>>().CreateDbContextAsync();
        var nodes = await complete.Set<ProjectObjectRecord>().Where(item => item.ProjectId == project.Id).ToListAsync();
        var prompts = await complete.Set<PromptArtifact>().Where(item => item.ProjectId == project.Id).ToListAsync();
        Assert.Equal(afterCommit ? 1 : 0, nodes.Count);
        Assert.Equal(afterCommit ? 1 : 0, prompts.Count);
        if (afterCommit) {
            var binding = await complete.Set<ProjectNodeBindingRecord>().SingleAsync(item => item.ProjectObjectId == nodes[0].Id);
            Assert.Equal(prompts[0].Id, binding.ExternalArtifactId);
            Assert.Equal($"/prompt-gallery?promptId={prompts[0].Id}", binding.Route);
        }
    }

    [Fact]
    public async Task Prompt_follow_up_observes_committed_records_and_can_enter_a_new_owner_transaction() {
        var callback = new CompletionObservation();
        await using var application = await TestApplication.CreateAsync(new TestHarnessOptions {
            ConfigureServices = services => services.AddScoped<IPromptGalleryMutationService>(provider =>
                new ObservedPromptMutation(ActivatorUtilities.CreateInstance<PromptGalleryMutationService>(provider), provider, callback))
        });
        var project = await SeedProjectAsync(application);
        await using var scope = application.Services.CreateAsyncScope();
        var service = scope.ServiceProvider.GetRequiredService<ProjectWorkbenchService>();
        using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(15));
        var node = await service.CreateObjectAsync(project.Id, PromptRequest(project.Id), deadline.Token);
        Assert.Equal(1, callback.Calls);
        Assert.Equal(project.Id, callback.ProjectId);
        Assert.NotEqual(Guid.Empty, callback.PromptId);
        await using var owner = await application.Services.GetRequiredService<IDbContextFactory<WorkbenchDbContext>>().CreateDbContextAsync();
        Assert.Equal(node.Id, (await owner.Set<ProjectObjectRecord>().SingleAsync(item => item.ProjectId == project.Id)).NodeKey);
    }

    private static Project Project(string name) => new() { Name = name, Slug = Guid.NewGuid().ToString("N"), CreatedAtUtc = Now, UpdatedAtUtc = Now };
    private static ProjectHierarchyLink Link(Project parent, Project child) => new() { ParentProjectId = parent.Id, ChildProjectId = child.Id, CreatedAtUtc = Now };
    private static ProjectObjectCreateRequest PromptRequest(Guid projectId) => new(ProjectObjectType.PromptFlow,
        "Native Gallery prompt", "", "Keep atomic binding", $"project:{projectId}", X: 400, Y: 200, PlacementIntent: ProjectObjectPlacementIntent.CallerControlled);
    private static async Task<Project> SeedProjectAsync(TestApplication application) {
        var project = Project("Prompt owner project");
        await using var complete = await application.Services.GetRequiredService<IDbContextFactory<AppDbContext>>().CreateDbContextAsync();
        complete.Add(project);
        await complete.SaveChangesAsync();
        return project;
    }
    private sealed class FixedClock : IClock {
        public DateTimeOffset GetUtcNow() => Now;
    }
    private sealed class CompletionObservation {
        public int Calls;
        public Guid ProjectId;
        public Guid PromptId;
    }
    private sealed class ObservedPromptMutation(IPromptGalleryMutationService inner, IServiceProvider services, CompletionObservation observation) : IPromptGalleryMutationService {
        public Task<Result<PromptDraftCreationPreparation>> StageCreateDraftAsync(PromptGalleryDraft draft, CancellationToken cancellationToken = default) => inner.StageCreateDraftAsync(draft, cancellationToken);
        public async Task CompleteDraftCreationAsync(PromptDraftCreationPreparation preparation, CancellationToken cancellationToken = default) {
            var projectId = preparation.ProjectId!.Value;
            await using var owner = await services.GetRequiredService<IDbContextFactory<WorkbenchDbContext>>().CreateDbContextAsync(cancellationToken);
            await using var mutation = await services.GetRequiredService<ProjectStructureMutationScopeFactory>()
                .BeginAsync(owner, ProjectStructureSerializableMutationScope.ForProject(projectId), cancellationToken);
            Assert.True(await owner.Set<ProjectNodeBindingRecord>().AnyAsync(item => item.ExternalArtifactId == preparation.Receipt.PromptArtifactId, cancellationToken));
            await mutation.CommitAsync(cancellationToken);
            await mutation.DisposeAsync();
            observation.Calls++;
            observation.ProjectId = projectId;
            observation.PromptId = preparation.Receipt.PromptArtifactId;
            await inner.CompleteDraftCreationAsync(preparation, cancellationToken);
        }
    }
    private sealed class PromptCommitFault(bool afterCommit) : DbTransactionInterceptor {
        public ArgumentException Failure { get; } = new("Injected failure after both owner flushes.");
        public bool BothFlushed { get; set; }
        public bool Enabled { get; set; }
        private bool thrown;
        public override ValueTask<InterceptionResult> TransactionCommittingAsync(DbTransaction transaction, TransactionEventData eventData,
            InterceptionResult result, CancellationToken cancellationToken = default) {
            if (!afterCommit && BothFlushed && !thrown) {
                thrown = true;
                throw Failure;
            }
            return ValueTask.FromResult(result);
        }
        public override Task TransactionCommittedAsync(DbTransaction transaction, TransactionEndEventData eventData, CancellationToken cancellationToken = default) {
            if (afterCommit && BothFlushed && !thrown) {
                thrown = true;
                throw Failure;
            }
            return Task.CompletedTask;
        }
    }
    private sealed class PromptWriteProbe(PromptCommitFault fault) : DbCommandInterceptor {
        public DbTransaction? NativeTransaction { get; private set; }
        public DbTransaction? PromptTransaction { get; private set; }
        public override ValueTask<DbDataReader> ReaderExecutedAsync(DbCommand command, CommandExecutedEventData eventData, DbDataReader result,
            CancellationToken cancellationToken = default) {
            if (!fault.Enabled) {
                return ValueTask.FromResult(result);
            }
            if (command.CommandText.Contains("INSERT INTO \"Workbench_ProjectObjects\"", StringComparison.Ordinal)) {
                NativeTransaction = command.Transaction;
            }
            if (command.CommandText.Contains("INSERT INTO \"Prompts_PromptArtifacts\"", StringComparison.Ordinal)) {
                PromptTransaction = command.Transaction;
            }
            fault.BothFlushed = NativeTransaction is not null && PromptTransaction is not null;
            return ValueTask.FromResult(result);
        }
    }
}
