using Microsoft.Extensions.DependencyInjection.Extensions;
using System.Data;
using CanDoItAll.Infrastructure.ControlPlane;
using CanDoItAll.Infrastructure.Persistence;
using CanDoItAll.Modules.CrmHr;
using CanDoItAll.Modules.Projects;
using CanDoItAll.Modules.Workbench;
using CanDoItAll.SharedKernel;
using CanDoItAll.Tests.Support;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;

namespace CanDoItAll.Tests.Integration;

public sealed class WorkAssignmentOwnerPersistenceTests {
    private static readonly DateTimeOffset Now = new(2026, 4, 5, 6, 7, 8, TimeSpan.Zero);

    [Fact]
    public async Task Legacy_work_fields_and_orphan_references_survive_restart_and_profile_isolation() {
        await using var environment = CanDoItAllTestEnvironment.Create("work-owner-history");
        var profile = environment.CreatePostgreSqlProfile("original");
        var other = environment.CreatePostgreSqlProfile("other");
        var row = new ProjectWorkAssignmentRecord {
            ProjectId = Guid.NewGuid(), PartyId = Guid.NewGuid(), NodeKey = "retained:unavailable-task",
            PhaseName = "Legacy phase", OpportunityId = Guid.NewGuid(), AllocationPercent = 37.125m,
            StartsAtUtc = Now.AddTicks(1230), EndsAtUtc = Now.AddDays(3).AddTicks(4560),
            IsPrimary = true, Source = " old source ", Notes = "Original\nwork assignment notes"
        };
        var expected = Fact(row);
        await using (var original = await TestApplication.CreateAsync(new TestHarnessOptions { TestEnvironment = environment, ActiveProfile = profile })) {
            await using var complete = await CanonicalAsync(original);
            complete.Add(row);
            await complete.SaveChangesAsync();
        }
        await using var restarted = await TestApplication.CreateAsync(new TestHarnessOptions { TestEnvironment = environment, ActiveProfile = profile });
        await using var scope = restarted.Services.CreateAsyncScope();
        var queries = scope.ServiceProvider.GetRequiredService<IProjectWorkAssignmentQueries>();
        Assert.Equal(expected, await queries.GetAsync(row.Id));
        Assert.Equal(ProjectPartyAssignmentRole.WorkItemAssignee, Assert.Single(await queries.ListForProjectsAsync([row.ProjectId])).Role);
        await using var isolated = await TestApplication.CreateAsync(new TestHarnessOptions { TestEnvironment = environment, ActiveProfile = other });
        await using var otherScope = isolated.Services.CreateAsyncScope();
        Assert.Null(await otherScope.ServiceProvider.GetRequiredService<IProjectWorkAssignmentQueries>().GetAsync(row.Id));
        Assert.Equal(expected, await queries.GetAsync(row.Id));
    }

    [Fact]
    public async Task Cross_role_edits_keep_the_same_identity_and_noneditor_fields_in_both_directions() {
        await using var app = await TestApplication.CreateAsync();
        var seed = await SeedAsync(app);
        var participation = new ProjectPartyAssignment {
            ProjectId = seed.ProjectId, ProjectLifetimeId = seed.Admission.LifetimeId, PartyId = seed.PartyId, NodeKey = seed.NodeKey,
            AssignmentKind = ProjectPartyAssignmentKind.Manager, PhaseName = "Retained phase", OpportunityId = Guid.NewGuid()
        };
        await using (var complete = await CanonicalAsync(app)) {
            complete.Add(participation);
            await complete.SaveChangesAsync();
        }
        await using var scope = app.Services.CreateAsyncScope();
        var facade = scope.ServiceProvider.GetRequiredService<ProjectPartyIntegrationService>();
        var request = Request(seed, participation.Id);
        request.Original = Assert.Single(await facade.ListAssignmentsDetailedAsync(seed.ProjectId)).Original;
        var work = await facade.SaveAssignmentAsync(request);
        Assert.True(work.IsSuccess);
        Assert.Equal(participation.Id, work.Value);
        await using (var complete = await CanonicalAsync(app)) {
            Assert.False(await complete.Set<ProjectPartyAssignment>().AnyAsync(item => item.Id == participation.Id));
            var saved = await complete.Set<ProjectWorkAssignmentRecord>().SingleAsync(item => item.Id == participation.Id);
            Assert.Equal(participation.PhaseName, saved.PhaseName);
            Assert.Equal(participation.OpportunityId, saved.OpportunityId);
        }
        request.Original = (await scope.ServiceProvider.GetRequiredService<IProjectWorkAssignmentQueries>().GetAsync(participation.Id))!.Original;
        request.Role = ProjectPartyAssignmentRole.Manager;
        request.PrimaryOriginal = await facade.ReadPrimaryObservationAsync(seed.Admission, request.Role, seed.NodeKey);
        Assert.True((await facade.SaveAssignmentAsync(request)).IsSuccess);
        await using var verified = await CanonicalAsync(app);
        Assert.False(await verified.Set<ProjectWorkAssignmentRecord>().AnyAsync(item => item.Id == participation.Id));
        var restored = await verified.Set<ProjectPartyAssignment>().SingleAsync(item => item.Id == participation.Id);
        Assert.Equal(ProjectPartyAssignmentKind.Manager, restored.AssignmentKind);
        Assert.Equal(participation.PhaseName, restored.PhaseName);
        Assert.Equal(participation.OpportunityId, restored.OpportunityId);
        Assert.Equal(2, await RevisionAsync(app, seed));
    }

    [Fact]
    public async Task Explicit_staging_uses_the_held_transaction_and_rolls_back_assignment_and_native_revision() {
        await using var app = await TestApplication.CreateAsync();
        var seed = await SeedAsync(app);
        await using var scope = app.Services.CreateAsyncScope();
        var commands = scope.ServiceProvider.GetRequiredService<IProjectWorkAssignmentCommands>();
        var feed = scope.ServiceProvider.GetRequiredService<ProjectStructureChangeFeed>();
        var cursor = feed.Read(seed.Admission.DatabaseProfileId, [seed.ProjectId]).Cursor;
        var id = Guid.NewGuid();
        await Assert.ThrowsAsync<InvalidOperationException>(() => commands.StageSaveAsync(Request(seed), id));
        await using var owner = await app.Services.GetRequiredService<IDbContextFactory<CrmHrDbContext>>().CreateDbContextAsync();
        await using var transaction = await owner.Database.BeginTransactionAsync(IsolationLevel.Serializable);
        await SerializableMutationScope.AcquireRelationalScopeLocksAsync(owner,
            [ProjectMutationScopeKeys.ForProject(seed.ProjectId), ProjectAssignmentMutationKeys.ForAssignment(id)], CancellationToken.None);
        using (app.Services.GetRequiredService<CoordinatedDatabaseTransaction>().Enter(owner)) {
            var saved = await commands.StageSaveAsync(Request(seed), id);
            Assert.True(saved.IsSuccess);
            Assert.Equal(id, saved.Value);
            Assert.Equal(id, (await commands.GetForMutationAsync(id))!.Id);
            Assert.Null(await commands.GetAsync(id));
            Assert.Empty(feed.Read(seed.Admission.DatabaseProfileId, [seed.ProjectId], cursor).Changes);
            await using var independent = await CanonicalAsync(app);
            Assert.False(await independent.Set<ProjectWorkAssignmentRecord>().AnyAsync(item => item.Id == id));
        }
        await transaction.RollbackAsync();
        Assert.Null(await commands.GetAsync(id));
        Assert.Equal(0, await RevisionAsync(app, seed));
        Assert.Empty(feed.Read(seed.Admission.DatabaseProfileId, [seed.ProjectId], cursor).Changes);
        await Assert.ThrowsAsync<InvalidOperationException>(() => commands.GetForMutationAsync(id));
    }

    [Fact]
    public async Task Caller_supplied_identity_cannot_be_created_in_both_owners_by_concurrent_replacement() {
        await using var app = await TestApplication.CreateAsync();
        var workSeed = await SeedAsync(app);
        var participationSeed = await SeedAsync(app);
        var identity = Guid.NewGuid();
        await using var workScope = app.Services.CreateAsyncScope();
        await using var crmScope = app.Services.CreateAsyncScope();
        var work = workScope.ServiceProvider.GetRequiredService<IProjectWorkAssignmentCommands>();
        var crm = crmScope.ServiceProvider.GetRequiredService<ProjectPartyIntegrationService>();
        var workRequest = Request(workSeed, identity);
        var participationRequest = new ProjectPartyAssignmentUpsertRequest {
            AssignmentId = identity, ProjectId = participationSeed.ProjectId, ExpectedProjectAdmission = participationSeed.Admission, PartyId = participationSeed.PartyId,
            Role = ProjectPartyAssignmentRole.Manager
        };
        var results = await Task.WhenAll(
            AttemptAsync(() => work.ReplaceAsync(workSeed.ProjectId, new(workSeed.NodeKey), [workRequest], expectedProjectAdmission: workSeed.Admission)),
            AttemptAsync(() => crm.ReplaceProjectAssignmentsAsync(participationSeed.ProjectId, [participationRequest], [ProjectPartyAssignmentRole.Manager], expectedProjectAdmission: participationSeed.Admission)));
        Assert.Single(results, item => item);
        Assert.Single(results, item => !item);
        await using var complete = await CanonicalAsync(app);
        var count = await complete.Set<ProjectPartyAssignment>().CountAsync(item => item.Id == identity) +
            await complete.Set<ProjectWorkAssignmentRecord>().CountAsync(item => item.Id == identity);
        Assert.Equal(1, count);
    }

    [Fact]
    public async Task Mixed_move_replay_preserves_work_payload_and_work_only_cleanup_does_not_return_early() {
        await using var app = await TestApplication.CreateAsync();
        var seed = await SeedAsync(app);
        var target = await SeedAsync(app);
        var work = new ProjectWorkAssignmentRecord {
            ProjectId = seed.ProjectId, ProjectLifetimeId = seed.Admission.LifetimeId, PartyId = seed.PartyId, NodeKey = seed.NodeKey, PhaseName = "Kept phase",
            OpportunityId = Guid.NewGuid(), AllocationPercent = 41.25m, StartsAtUtc = Now, EndsAtUtc = Now.AddDays(2),
            IsPrimary = true, Source = "legacy move", Notes = "Keep every work field"
        };
        var participation = new ProjectPartyAssignment {
            ProjectId = seed.ProjectId, ProjectLifetimeId = seed.Admission.LifetimeId, PartyId = seed.PartyId, NodeKey = seed.NodeKey, AssignmentKind = ProjectPartyAssignmentKind.Manager
        };
        var expected = Fact(work) with { ProjectId = target.ProjectId, ProjectLifetimeId = target.Admission.LifetimeId };
        await using (var complete = await CanonicalAsync(app)) {
            complete.AddRange(work, participation);
            await complete.SaveChangesAsync();
        }
        await using var scope = app.Services.CreateAsyncScope();
        var facade = scope.ServiceProvider.GetRequiredService<ProjectPartyIntegrationService>();
        var move = new ProjectPartyAssignmentMoveOperationId(Guid.NewGuid());
        await facade.MoveAssignmentsToProjectAsync(move, seed.ProjectId, [new(seed.NodeKey)], target.ProjectId, sourceReference: ProjectAssignmentReference.From(seed.Admission), expectedTargetAdmission: target.Admission);
        await facade.MoveAssignmentsToProjectAsync(move, seed.ProjectId, [new(seed.NodeKey)], target.ProjectId, sourceReference: ProjectAssignmentReference.From(seed.Admission), expectedTargetAdmission: target.Admission);
        Assert.Equal(expected, await scope.ServiceProvider.GetRequiredService<IProjectWorkAssignmentQueries>().GetAsync(work.Id));
        await using (var complete = await CanonicalAsync(app)) {
            Assert.Equal(target.ProjectId, (await complete.Set<ProjectPartyAssignment>().SingleAsync(item => item.Id == participation.Id)).ProjectId);
            Assert.Single(await complete.Set<ProjectPartyAssignmentMoveReceipt>().Where(item => item.OperationId == move.Value).ToListAsync());
            complete.Remove(await complete.Set<ProjectPartyAssignment>().SingleAsync(item => item.Id == participation.Id));
            await complete.SaveChangesAsync();
        }
        await facade.DeleteAssignmentsForNodesAsync(target.ProjectId, [new(seed.NodeKey)], expectedReference: ProjectAssignmentReference.From(target.Admission));
        Assert.Null(await scope.ServiceProvider.GetRequiredService<IProjectWorkAssignmentQueries>().GetAsync(work.Id));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Party_merge_stages_work_before_intermediate_crm_saves_and_rolls_every_owner_back_on_failure(bool withAffiliations) {
        var failure = new MergeSaveFailure();
        await using var app = await TestApplication.CreateAsync(new TestHarnessOptions {
            ConfigureServices = services => services.AddSingleton<DbContextOptions<CrmHrDbContext>>(provider => {
                var options = new DbContextOptionsBuilder<CrmHrDbContext>();
                AppDbContextOptionsConfigurator.Configure(options, provider.GetRequiredService<ICanonicalRuntimeDatabase>().Profile);
                return options.AddInterceptors(failure).Options;
            })
        });
        var seed = await SeedAsync(app);
        var merged = new Party { PartyType = PartyType.Person, DisplayName = "Duplicate person" };
        var organization = new Party { PartyType = PartyType.Organization, DisplayName = "Employer" };
        var retainedAffiliation = new PartyOrganizationAffiliation {
            PersonPartyId = seed.PartyId, OrganizationPartyId = organization.Id, AffiliationKind = PartyOrganizationAffiliationKind.Employee,
            IsPrimary = true, CreatedAtUtc = Now.AddDays(-2), UpdatedAtUtc = Now
        };
        var mergedAffiliation = new PartyOrganizationAffiliation {
            PersonPartyId = merged.Id, OrganizationPartyId = organization.Id, AffiliationKind = PartyOrganizationAffiliationKind.Employee,
            IsPrimary = true, CreatedAtUtc = Now.AddDays(-1), UpdatedAtUtc = Now
        };
        var work = new ProjectWorkAssignmentRecord {
            ProjectId = seed.ProjectId, ProjectLifetimeId = seed.Admission.LifetimeId, PartyId = merged.Id, NodeKey = seed.NodeKey, PhaseName = "Retained phase",
            PartyOrganizationAffiliationId = withAffiliations ? mergedAffiliation.Id : null,
            OpportunityId = Guid.NewGuid(), AllocationPercent = 37m, StartsAtUtc = Now, EndsAtUtc = Now.AddDays(2),
            IsPrimary = true, Source = "Imported", Notes = "Untouched work notes"
        };
        await using (var complete = await CanonicalAsync(app)) {
            complete.AddRange(merged, organization, work);
            if (withAffiliations) {
                complete.AddRange(retainedAffiliation, mergedAffiliation);
            }
            await complete.SaveChangesAsync();
        }
        await using var scope = app.Services.CreateAsyncScope();
        var manager = scope.ServiceProvider.GetRequiredService<PartyDirectoryManagementService>();
        var queries = scope.ServiceProvider.GetRequiredService<IProjectWorkAssignmentQueries>();
        var mergePreview = await manager.PreviewMergeAsync(seed.PartyId, merged.Id);
        Assert.True(mergePreview.IsSuccess);
        var mergeOriginal = mergePreview.Value!.Original;
        var feed = scope.ServiceProvider.GetRequiredService<ProjectStructureChangeFeed>();
        var cursor = feed.Read(seed.Admission.DatabaseProfileId, [seed.ProjectId]).Cursor;
        failure.Enabled = true;
        await Assert.ThrowsAsync<InjectedMergeFailure>(() => manager.MergePartyAsync(seed.PartyId, merged.Id, "test", "Merge duplicate", original: mergeOriginal));
        failure.Enabled = false;
        Assert.Equal(Fact(work), await queries.GetAsync(work.Id));
        Assert.Equal(0, await RevisionAsync(app, seed));
        Assert.Empty(feed.Read(seed.Admission.DatabaseProfileId, [seed.ProjectId], cursor).Changes);
        Assert.True((await manager.MergePartyAsync(seed.PartyId, merged.Id, "test", "Merge duplicate", original: mergeOriginal)).IsSuccess);
        Assert.Equal(Fact(work) with {
            PartyId = seed.PartyId,
            PartyOrganizationAffiliationId = withAffiliations ? retainedAffiliation.Id : null
        }, await queries.GetAsync(work.Id));
        Assert.Equal(1, await RevisionAsync(app, seed));
        Assert.Equal(seed.NodeKey, Assert.Single(Assert.Single(feed.Read(seed.Admission.DatabaseProfileId, [seed.ProjectId], cursor).Changes).Nodes).NodeId);
        await using var verified = await CanonicalAsync(app);
        Assert.False(await verified.Set<Party>().AnyAsync(item => item.Id == merged.Id));
        Assert.Single(await verified.Set<CrmHrAuditEntry>().Where(item => item.EntityId == seed.PartyId && item.Action == "MergedDuplicate").ToListAsync());
        if (withAffiliations) {
            Assert.False(await verified.Set<PartyOrganizationAffiliation>().AnyAsync(item => item.Id == mergedAffiliation.Id));
            verified.Remove(await verified.Set<PartyOrganizationAffiliation>().SingleAsync(item => item.Id == retainedAffiliation.Id));
            await Assert.ThrowsAsync<DbUpdateException>(() => verified.SaveChangesAsync());
        }
    }

    [Fact]
    public async Task Party_merge_rejects_a_native_work_assignment_change_after_preview() {
        await using var app = await TestApplication.CreateAsync();
        var seed = await SeedAsync(app);
        await using var scope = app.Services.CreateAsyncScope();
        var services = scope.ServiceProvider;
        var parties = services.GetRequiredService<PartyDirectoryService>();
        var created = await parties.SavePartyAsync(new PartyEditorModel { PartyType = PartyType.Person, DisplayName = "Observed work duplicate" });
        Assert.True(created.IsSuccess);
        var facade = services.GetRequiredService<ProjectPartyIntegrationService>();
        var assignment = await facade.SaveAssignmentAsync(Request(seed));
        Assert.True(assignment.IsSuccess);
        var manager = services.GetRequiredService<PartyDirectoryManagementService>();
        var original = (await manager.PreviewMergeAsync(seed.PartyId, created.Value)).Value!.Original;
        await using var peer = app.Services.CreateAsyncScope();
        var peerFacade = peer.ServiceProvider.GetRequiredService<ProjectPartyIntegrationService>();
        var request = Request(seed, assignment.Value);
        request.Original = (await peer.ServiceProvider.GetRequiredService<IProjectWorkAssignmentQueries>().GetAsync(assignment.Value))!.Original;
        request.PrimaryOriginal = await peerFacade.ReadPrimaryObservationAsync(seed.Admission, request.Role, seed.NodeKey);
        request.Notes = "Independent native Work change";
        Assert.True((await peerFacade.SaveAssignmentAsync(request)).IsSuccess);
        var revision = await RevisionAsync(app, seed);

        var result = await manager.MergePartyAsync(seed.PartyId, created.Value, "work-preflight-test", "Must reject", original: original);

        Assert.True(result.IsFailure);
        Assert.Contains(result.Errors, error => error.Code == CrmHrEditingErrorCodes.PartyMergeConflict);
        Assert.NotNull(await parties.GetPartyAsync(created.Value));
        Assert.Empty((await parties.GetPartyAsync(seed.PartyId))!.Notes);
        Assert.Equal(revision, await RevisionAsync(app, seed));
        Assert.Equal(request.Notes, (await services.GetRequiredService<IProjectWorkAssignmentQueries>().GetAsync(assignment.Value))!.Notes);
    }

    [Fact]
    public async Task Staffing_filters_and_pages_using_work_allocations_before_the_limit() {
        await using var app = await TestApplication.CreateAsync();
        var allocated = await SeedAsync(app);
        var bench = await SeedAsync(app);
        await using (var complete = await CanonicalAsync(app)) {
            complete.AddRange(new WorkforceProfile { PartyId = allocated.PartyId, JobTitle = "Work resource" },
                new WorkforceProfile { PartyId = bench.PartyId, JobTitle = "Bench resource" },
                new ProjectWorkAssignmentRecord { ProjectId = allocated.ProjectId, ProjectLifetimeId = allocated.Admission.LifetimeId, PartyId = allocated.PartyId,
                    NodeKey = allocated.NodeKey, AllocationPercent = 80m },
                new ProjectPartyAssignment { ProjectId = allocated.ProjectId, ProjectLifetimeId = allocated.Admission.LifetimeId, PartyId = allocated.PartyId,
                    AssignmentKind = ProjectPartyAssignmentKind.TeamMember, AllocationPercent = 30m });
            await complete.SaveChangesAsync();
        }
        await using var scope = app.Services.CreateAsyncScope();
        var hr = scope.ServiceProvider.GetRequiredService<HrService>();
        var page = await hr.SearchStaffingCandidatesAsync(new(AvailabilityState: WorkforceAvailabilityState.Overallocated, PageSize: 1));
        Assert.Equal(1, page.TotalCount);
        Assert.Equal(allocated.PartyId, Assert.Single(page.Items).PartyId);
        var benchPage = await hr.SearchStaffingCandidatesAsync(new(AvailabilityState: WorkforceAvailabilityState.Bench, PageSize: 1));
        Assert.Equal(1, benchPage.TotalCount);
        Assert.Equal(bench.PartyId, Assert.Single(benchPage.Items).PartyId);
        var assignments = scope.ServiceProvider.GetRequiredService<ProjectPartyIntegrationService>();
        var count = await assignments.GetAssignmentCountsAsync(allocated.ProjectId, Now, Now.AddDays(1));
        Assert.Equal(2, count.TotalCount);
        Assert.Equal(2, count.AllocationCount);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Native_work_assignment_commit_publishes_the_affected_node_to_structure_polling(bool throughCrm) {
        await using var app = await TestApplication.CreateAsync();
        var seed = await SeedAsync(app);
        await using var scope = app.Services.CreateAsyncScope();
        var services = scope.ServiceProvider;
        var feed = services.GetRequiredService<ProjectStructureChangeFeed>();
        var cursor = feed.Read(seed.Admission.DatabaseProfileId, [seed.ProjectId]).Cursor;
        var result = throughCrm
            ? await services.GetRequiredService<ProjectPartyIntegrationService>().SaveAssignmentAsync(Request(seed))
            : await services.GetRequiredService<IProjectWorkAssignmentCommands>().SaveAsync(Request(seed));

        Assert.True(result.IsSuccess);
        Assert.Equal(1, await RevisionAsync(app, seed));
        var batch = feed.Read(seed.Admission.DatabaseProfileId, [seed.ProjectId], cursor);
        Assert.False(batch.RequiresRefresh);
        var committed = Assert.Single(batch.Changes);
        Assert.Equal(services.GetRequiredService<ProjectStructureChangeSession>().Id, committed.SessionId);
        Assert.Equal(seed.ProjectId, committed.ProjectId);
        var node = Assert.Single(committed.Nodes);
        Assert.Equal(seed.NodeKey, node.NodeId);
        Assert.Equal("Canonical task", node.Title);
        Assert.Equal(ProjectStructureChangeKind.Updated, node.Kind);
        Assert.Empty(feed.Read(Guid.NewGuid(), [seed.ProjectId], cursor).Changes);
        Assert.Empty(feed.Read(seed.Admission.DatabaseProfileId, [Guid.NewGuid()], cursor).Changes);
        var commands = services.GetRequiredService<IProjectWorkAssignmentCommands>();
        var update = Request(seed, result.Value);
        update.Original = (await commands.GetAsync(result.Value))!.Original;
        update.PrimaryOriginal = await commands.ReadPrimaryObservationAsync(seed.Admission, seed.NodeKey);
        update.Notes = "Independent current Work update";
        cursor = batch.Cursor;
        var updated = throughCrm
            ? await services.GetRequiredService<ProjectPartyIntegrationService>().SaveAssignmentAsync(update)
            : await commands.SaveAsync(update);
        Assert.True(updated.IsSuccess);
        Assert.Equal(2, await RevisionAsync(app, seed));
        batch = feed.Read(seed.Admission.DatabaseProfileId, [seed.ProjectId], cursor);
        Assert.Equal(seed.NodeKey, Assert.Single(Assert.Single(batch.Changes).Nodes).NodeId);
        var original = (await commands.GetAsync(result.Value))!.Original;
        cursor = batch.Cursor;
        if (throughCrm) {
            await services.GetRequiredService<ProjectPartyIntegrationService>().DeleteAssignmentAsync(result.Value,
                expectedReference: ProjectAssignmentReference.From(seed.Admission), original: original);
        } else {
            await commands.DeleteAsync(result.Value, expectedReference: ProjectAssignmentReference.From(seed.Admission), original: original);
        }
        Assert.Null(await commands.GetAsync(result.Value));
        Assert.Equal(3, await RevisionAsync(app, seed));
        var deletedAssignmentNode = Assert.Single(Assert.Single(feed.Read(seed.Admission.DatabaseProfileId, [seed.ProjectId], cursor).Changes).Nodes);
        Assert.Equal(seed.NodeKey, deletedAssignmentNode.NodeId);
        Assert.Equal(ProjectStructureChangeKind.Updated, deletedAssignmentNode.Kind);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task A_failing_work_receipt_receiver_cannot_suppress_the_committed_node_change(bool throughCrm) {
        await using var app = await TestApplication.CreateAsync();
        var seed = await SeedAsync(app);
        await using var scope = app.Services.CreateAsyncScope();
        var services = scope.ServiceProvider;
        var feed = services.GetRequiredService<ProjectStructureChangeFeed>();
        var cursor = feed.Read(seed.Admission.DatabaseProfileId, [seed.ProjectId]).Cursor;
        ProjectAssignmentSaveReceipt? receipt = null;
        void Accepted(ProjectAssignmentSaveReceipt value) {
            receipt = value;
            throw new InvalidOperationException("Owned receipt receiver failure after commit.");
        }

        await Assert.ThrowsAsync<InvalidOperationException>(() => throughCrm
            ? services.GetRequiredService<ProjectPartyIntegrationService>().SaveAssignmentAsync(Request(seed), onCommitted: Accepted)
            : services.GetRequiredService<IProjectWorkAssignmentCommands>().SaveAsync(Request(seed), onCommitted: Accepted));

        Assert.NotNull(receipt);
        Assert.NotNull(await services.GetRequiredService<IProjectWorkAssignmentQueries>().GetAsync(receipt.Id));
        var node = Assert.Single(Assert.Single(feed.Read(seed.Admission.DatabaseProfileId, [seed.ProjectId], cursor).Changes).Nodes);
        Assert.Equal(seed.NodeKey, node.NodeId);
        Assert.Equal("Canonical task", node.Title);
    }

    [Fact]
    public async Task Work_node_capture_requires_a_held_owner_and_rejects_unbounded_or_foreign_batches() {
        await using var app = await TestApplication.CreateAsync();
        var seed = await SeedAsync(app);
        await using var scope = app.Services.CreateAsyncScope();
        var commands = scope.ServiceProvider.GetRequiredService<IProjectWorkAssignmentCommands>();
        var feed = scope.ServiceProvider.GetRequiredService<ProjectStructureChangeFeed>();
        var cursor = feed.Read(seed.Admission.DatabaseProfileId, [seed.ProjectId]).Cursor;

        await Assert.ThrowsAsync<InvalidOperationException>(() => commands.ReadExistingNodeChangesForMutationAsync([(seed.ProjectId, seed.NodeKey)]));
        await Assert.ThrowsAsync<ArgumentException>(() => commands.ReadExistingNodeChangesForMutationAsync(
            Enumerable.Range(0, 5001).Select(_ => (seed.ProjectId, seed.NodeKey)).ToArray()));
        Assert.Throws<InvalidOperationException>(() => commands.PublishCommittedNodeChanges([
            new(Guid.NewGuid(), seed.ProjectId, seed.NodeKey, "Foreign profile label") ]));
        Assert.Empty(feed.Read(seed.Admission.DatabaseProfileId, [seed.ProjectId], cursor).Changes);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Native_work_replacement_publishes_one_node_and_stale_replacement_publishes_nothing(bool throughCrm) {
        await using var app = await TestApplication.CreateAsync();
        var seed = await SeedAsync(app);
        await using var scope = app.Services.CreateAsyncScope();
        var services = scope.ServiceProvider;
        var commands = services.GetRequiredService<IProjectWorkAssignmentCommands>();
        var facade = services.GetRequiredService<ProjectPartyIntegrationService>();
        var feed = services.GetRequiredService<ProjectStructureChangeFeed>();
        var cursor = feed.Read(seed.Admission.DatabaseProfileId, [seed.ProjectId]).Cursor;
        Task<Result> Replace() => throughCrm
            ? facade.ReplaceNodeAssignmentsIfCurrentAsync(seed.ProjectId, new(seed.NodeKey), [Request(seed)],
                [ProjectPartyAssignmentRole.WorkItemAssignee], [], new ProjectWorkItemDirectAssignmentRevision(0), expectedProjectAdmission: seed.Admission)
            : commands.ReplaceAsync(seed.ProjectId, new(seed.NodeKey), [Request(seed)], [], new(0), expectedProjectAdmission: seed.Admission);

        Assert.True((await Replace()).IsSuccess);
        Assert.Equal(1, await RevisionAsync(app, seed));
        var batch = feed.Read(seed.Admission.DatabaseProfileId, [seed.ProjectId], cursor);
        Assert.Equal(seed.NodeKey, Assert.Single(Assert.Single(batch.Changes).Nodes).NodeId);
        cursor = batch.Cursor;
        Assert.True((await Replace()).IsFailure);
        Assert.Equal(1, await RevisionAsync(app, seed));
        Assert.Single(await commands.ListForProjectsAsync([seed.ProjectId]));
        Assert.Empty(feed.Read(seed.Admission.DatabaseProfileId, [seed.ProjectId], cursor).Changes);
    }

    [Fact]
    public async Task Native_work_replacement_rechecks_current_authority_after_SQL_and_emits_no_node_on_rollback() {
        var revoked = false;
        var gate = new WorkRevocationGate(() => revoked = true);
        await using var app = await TestApplication.CreateAsync(new() {
            ConfigureServices = services => {
                services.RemoveAll<IDbContextFactory<WorkbenchDbContext>>();
                services.AddSingleton<IDbContextFactory<WorkbenchDbContext>>(provider =>
                    new WorkRevocationFactory(provider.GetRequiredService<DbContextOptions<WorkbenchDbContext>>(), gate));
            }
        });
        var seed = await SeedAsync(app);
        await using var scope = app.Services.CreateAsyncScope();
        var services = scope.ServiceProvider;
        var commands = services.GetRequiredService<IProjectWorkAssignmentCommands>();
        var feed = services.GetRequiredService<ProjectStructureChangeFeed>();
        var cursor = feed.Read(seed.Admission.DatabaseProfileId, [seed.ProjectId]).Cursor;
        using var authority = services.GetRequiredService<ProjectAssignmentChanges>().EnterUiWrite(_ => revoked
            ? ValueTask.FromException(new UnauthorizedAccessException("Owned Work grant revoked after SQL.")) : ValueTask.CompletedTask);
        gate.Armed = true;

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => commands.ReplaceAsync(seed.ProjectId, new(seed.NodeKey),
            [Request(seed)], [], new(0), expectedProjectAdmission: seed.Admission));

        Assert.True(gate.Triggered);
        Assert.True(gate.ObservedTransaction);
        Assert.Empty(await commands.ListForProjectsAsync([seed.ProjectId]));
        Assert.Equal(0, await RevisionAsync(app, seed));
        Assert.Empty(feed.Read(seed.Admission.DatabaseProfileId, [seed.ProjectId], cursor).Changes);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Cleaning_an_orphan_work_assignment_does_not_invent_a_changed_structure_node(bool throughCrm) {
        await using var app = await TestApplication.CreateAsync();
        var seed = await SeedAsync(app);
        var orphan = new ProjectWorkAssignmentRecord {
            ProjectId = seed.ProjectId, ProjectLifetimeId = seed.Admission.LifetimeId,
            PartyId = seed.PartyId, NodeKey = $"task:{Guid.NewGuid():N}"
        };
        await using (var fixture = await CanonicalAsync(app)) {
            fixture.Add(orphan);
            await fixture.SaveChangesAsync();
        }
        await using var scope = app.Services.CreateAsyncScope();
        var services = scope.ServiceProvider;
        var commands = services.GetRequiredService<IProjectWorkAssignmentCommands>();
        var original = (await commands.GetAsync(orphan.Id))!.Original;
        var feed = services.GetRequiredService<ProjectStructureChangeFeed>();
        var cursor = feed.Read(seed.Admission.DatabaseProfileId, [seed.ProjectId]).Cursor;

        if (throughCrm) {
            await services.GetRequiredService<ProjectPartyIntegrationService>().DeleteAssignmentAsync(orphan.Id,
                expectedReference: ProjectAssignmentReference.From(seed.Admission), original: original);
        } else {
            await commands.DeleteAsync(orphan.Id, expectedReference: ProjectAssignmentReference.From(seed.Admission), original: original);
        }

        Assert.Null(await commands.GetAsync(orphan.Id));
        Assert.Equal(0, await RevisionAsync(app, seed));
        Assert.Empty(feed.Read(seed.Admission.DatabaseProfileId, [seed.ProjectId], cursor).Changes);
    }

    private static async Task<Seed> SeedAsync(TestApplication app) {
        var project = new Project { Name = "Work owner project", Slug = Guid.NewGuid().ToString("N") };
        var party = new Party { PartyType = PartyType.Person, DisplayName = "Retained person" };
        var node = new ProjectObjectRecord {
            ProjectId = project.Id, NodeKey = $"task:{Guid.NewGuid():N}", ObjectType = ProjectObjectType.WorkItem,
            ObjectSubtype = "task", Title = "Canonical task", CreatedAtUtc = Now, UpdatedAtUtc = Now,
            MetadataJson = ProjectObjectMetadataSerializer.Serialize(new ProjectObjectMetadataEnvelope {
                WorkItem = new ProjectWorkItemMetadata { WorkItemKind = ProjectWorkItemKind.Task, ExecutionState = ProjectTaskExecutionState.NotStarted }
            })
        };
        await using var complete = await CanonicalAsync(app);
        complete.AddRange(project, party, node);
        await complete.SaveChangesAsync();
        return new(project.Id, party.Id, node.Id, node.NodeKey, new(app.Services.GetRequiredService<ICanonicalRuntimeDatabase>().Profile.Profile.Id, project.Id, project.LifetimeId));
    }

    private static ProjectPartyAssignmentUpsertRequest Request(Seed seed, Guid? id = null) => new() {
        AssignmentId = id, ProjectId = seed.ProjectId, ExpectedProjectAdmission = seed.Admission, PartyId = seed.PartyId, Role = ProjectPartyAssignmentRole.WorkItemAssignee,
        NodeKey = seed.NodeKey, IsPrimary = true,
        PrimaryOriginal = new(seed.ProjectId, seed.Admission.LifetimeId, ProjectPartyAssignmentRole.WorkItemAssignee, seed.NodeKey, System.Collections.Immutable.ImmutableArray<Guid>.Empty), AllocationPercent = 37m, StartsOn = new(2026, 4, 5), EndsOn = new(2026, 4, 7), Source = "Work owner proof"
    };

    private static Task<AppDbContext> CanonicalAsync(TestApplication app) =>
        app.Services.GetRequiredService<IDbContextFactory<AppDbContext>>().CreateDbContextAsync();

    private static async Task<bool> AttemptAsync(Func<Task<Result>> command) {
        try {
            return (await command()).IsSuccess;
        } catch (PostgresException exception) when (exception.SqlState == PostgresErrorCodes.SerializationFailure) {
            return false;
        } catch (DbUpdateException exception) when (exception.InnerException is PostgresException { SqlState: PostgresErrorCodes.SerializationFailure }) {
            return false;
        }
    }

    private static async Task<long> RevisionAsync(TestApplication app, Seed seed) {
        await using var complete = await CanonicalAsync(app);
        var record = await complete.Set<ProjectObjectRecord>().SingleAsync(item => item.Id == seed.NodeId);
        return ProjectObjectMetadataSerializer.Parse(record.MetadataJson).WorkItem!.DirectAssignmentRevision;
    }

    private static ProjectWorkAssignmentFact Fact(ProjectWorkAssignmentRecord row) => new(row.Id, row.ProjectId, row.PartyId,
        row.PartyOrganizationAffiliationId, row.NodeKey, row.PhaseName, row.OpportunityId, row.AllocationPercent,
        row.StartsAtUtc, row.EndsAtUtc, row.IsPrimary, row.Source, row.Notes, row.ProjectLifetimeId);

    private sealed record Seed(Guid ProjectId, Guid PartyId, Guid NodeId, string NodeKey, ProjectWriteAdmission Admission);

    private sealed class WorkRevocationGate(Action revoke) : SaveChangesInterceptor {
        public bool Armed { get; set; }
        public bool Triggered { get; private set; }
        public bool ObservedTransaction { get; private set; }
        public override ValueTask<int> SavedChangesAsync(SaveChangesCompletedEventData eventData, int result, CancellationToken cancellationToken = default) {
            if (Armed) {
                Armed = false;
                Triggered = true;
                ObservedTransaction = eventData.Context?.Database.CurrentTransaction is not null;
                revoke();
            }
            return ValueTask.FromResult(result);
        }
    }

    private sealed class WorkRevocationFactory(DbContextOptions<WorkbenchDbContext> options, IInterceptor gate) : IDbContextFactory<WorkbenchDbContext> {
        public WorkbenchDbContext CreateDbContext() => new(new DbContextOptionsBuilder<WorkbenchDbContext>(options).AddInterceptors(gate).Options);
        public Task<WorkbenchDbContext> CreateDbContextAsync(CancellationToken cancellationToken = default) => Task.FromResult(CreateDbContext());
    }

    private sealed class InjectedMergeFailure() : Exception("Injected CRM save failure after Work staging.");

    private sealed class MergeSaveFailure : SaveChangesInterceptor {
        public bool Enabled { get; set; }

        public override ValueTask<InterceptionResult<int>> SavingChangesAsync(DbContextEventData eventData,
            InterceptionResult<int> result, CancellationToken cancellationToken = default) {
            if (Enabled && eventData.Context is CrmHrDbContext) {
                throw new InjectedMergeFailure();
            }
            return ValueTask.FromResult(result);
        }
    }
}
