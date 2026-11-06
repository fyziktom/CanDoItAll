using System.Data.Common;
using System.Text.Json;
using Bunit;
using CanDoItAll.Components.CanvasLib;
using CanDoItAll.Editing;
using CanDoItAll.Modules.CrmHr;
using CanDoItAll.Modules.Workbench;
using CanDoItAll.SharedKernel;
using CanDoItAll.Tests.Support;
using CanDoItAll.Workbench.Operators.UI.Parties;
using Microsoft.AspNetCore.Components;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace CanDoItAll.Tests.Components.ProjectStructure;

public sealed partial class ProjectStructurePartySaveTests {
    public enum PartyAccessBoundary { Allowed, BeforeSubmission, AssignmentSql, AssignmentCommit, PresentationSql, PresentationCommit, DirectorySql, DirectoryCommit }

    [Theory]
    [InlineData(ProjectObjectType.Participant, PartyAccessBoundary.Allowed)]
    [InlineData(ProjectObjectType.Participant, PartyAccessBoundary.BeforeSubmission)]
    [InlineData(ProjectObjectType.Participant, PartyAccessBoundary.AssignmentSql)]
    [InlineData(ProjectObjectType.Participant, PartyAccessBoundary.AssignmentCommit)]
    [InlineData(ProjectObjectType.Participant, PartyAccessBoundary.PresentationSql)]
    [InlineData(ProjectObjectType.Participant, PartyAccessBoundary.PresentationCommit)]
    [InlineData(ProjectObjectType.Meeting, PartyAccessBoundary.Allowed)]
    [InlineData(ProjectObjectType.Meeting, PartyAccessBoundary.BeforeSubmission)]
    [InlineData(ProjectObjectType.Meeting, PartyAccessBoundary.AssignmentSql)]
    [InlineData(ProjectObjectType.Meeting, PartyAccessBoundary.AssignmentCommit)]
    [InlineData(ProjectObjectType.Meeting, PartyAccessBoundary.PresentationSql)]
    [InlineData(ProjectObjectType.Meeting, PartyAccessBoundary.PresentationCommit)]
    public async Task Party_assignment_and_presentation_keep_current_structure_access_at_each_physical_commit(ProjectObjectType kind, PartyAccessBoundary boundary) {
        var access = new EditingUiTestAccess();
        var probe = new PartyAuthorityProbe(access, boundary);
        await using var harness = await ComponentTestHarness.CreateAsync(services => ConfigurePartyAuthority(services, access, probe));
        var target = await SeedAsync(harness, kind);
        var cut = Render(harness, target.ProjectId);
        await SelectAsync(cut, target.Node.Id);
        var editor = cut.FindComponent<PartyEditor>().Instance;
        if (kind == ProjectObjectType.Participant) {
            await PickAsync(cut, target.FirstParty);
        } else {
            var index = editor.State.Choices.ToList().FindIndex(x => x.Id == target.FirstParty);
            Assert.True(index >= 0);
            await cut.InvokeAsync(() => cut.FindAll("[data-testid='project-structure-meeting-party-checkbox']")[index].ChangeAsync(new ChangeEventArgs { Value = true }));
        }
        var state = editor.State;
        var selected = state.Draft.Selection();
        var before = await PartyAuthorityRowsAsync(harness, target.ProjectId);
        var nodeBefore = JsonSerializer.Serialize(await ReadNodeAsync(harness, target));
        var visibleBefore = JsonSerializer.Serialize(cut.FindComponent<CanvasWorkbench>().Instance.Surface.Nodes);
        probe.Project = target.ProjectId;
        probe.Armed = boundary is not (PartyAccessBoundary.Allowed or PartyAccessBoundary.BeforeSubmission);
        access.Allowed = boundary != PartyAccessBoundary.BeforeSubmission;
        await SaveAsync(cut, kind);
        Assert.False(probe.Armed);
        Assert.Equal(JsonSerializer.Serialize(selected), JsonSerializer.Serialize(state.Draft.Selection()));
        var assignmentsCommitted = boundary is not (PartyAccessBoundary.BeforeSubmission or PartyAccessBoundary.AssignmentSql);
        var nodeCommitted = boundary is PartyAccessBoundary.Allowed or PartyAccessBoundary.PresentationCommit;
        var after = await PartyAuthorityRowsAsync(harness, target.ProjectId);
        if (assignmentsCommitted) {
            var assignments = await harness.Context.Services.GetRequiredService<CanDoItAll.Modules.Projects.IProjectPartyIntegrationBridge>().ListAssignmentsDetailedAsync(target.ProjectId);
            var assignment = Assert.Single(assignments, x => x.NodeKey == target.Node.Id);
            Assert.Equal(target.FirstParty, assignment.PartyId);
            Assert.Contains(assignment.Id, state.Receipt!.AssignmentIds);
            Assert.Equal(nodeCommitted ? boundary == PartyAccessBoundary.Allowed ? PartySavePhase.Observed : PartySavePhase.NodeCommitted
                : PartySavePhase.AssignmentsCommitted, state.Receipt.Phase);
        } else {
            Assert.Equal(before, after);
            Assert.Equal(PartySavePhase.Rejected, state.Receipt!.Phase);
        }
        if (!nodeCommitted) {
            Assert.Equal(nodeBefore, JsonSerializer.Serialize(await ReadNodeAsync(harness, target)));
        }
        if (boundary != PartyAccessBoundary.Allowed) {
            Assert.Equal(visibleBefore, JsonSerializer.Serialize(cut.FindComponent<CanvasWorkbench>().Instance.Surface.Nodes));
        }
    }

    [Theory]
    [InlineData(PartyAccessBoundary.Allowed)]
    [InlineData(PartyAccessBoundary.BeforeSubmission)]
    [InlineData(PartyAccessBoundary.DirectorySql)]
    [InlineData(PartyAccessBoundary.DirectoryCommit)]
    public async Task Party_quick_create_retains_exact_native_directory_receipt_without_unauthorized_followups(PartyAccessBoundary boundary) {
        var access = new EditingUiTestAccess();
        var probe = new PartyAuthorityProbe(access, boundary);
        await using var harness = await ComponentTestHarness.CreateAsync(services => ConfigurePartyAuthority(services, access, probe));
        var target = await SeedAsync(harness);
        var cut = Render(harness, target.ProjectId);
        await SelectAsync(cut, target.Node.Id);
        await cut.InvokeAsync(() => cut.Find("[data-testid='project-structure-participant-local-only']").ChangeAsync(new ChangeEventArgs { Value = false }));
        var state = cut.FindComponent<PartyEditor>().Instance.State;
        const string name = "Owned directory authority candidate";
        await cut.InvokeAsync(() => cut.Find("[data-testid='project-structure-participant-quick-name']").InputAsync(new() { Value = name }));
        await cut.InvokeAsync(() => cut.Find("[data-testid='project-structure-participant-quick-email']").InputAsync(new() { Value = "owned-authority@example.test" }));
        var before = await PartyAuthorityRowsAsync(harness, target.ProjectId);
        probe.Project = target.ProjectId;
        probe.Armed = boundary is PartyAccessBoundary.DirectorySql or PartyAccessBoundary.DirectoryCommit;
        access.Allowed = boundary != PartyAccessBoundary.BeforeSubmission;
        await cut.InvokeAsync(() => cut.FindComponent<PartyEditor>().Instance.Create(state.Draft.QuickCreate()));
        Assert.False(probe.Armed);
        var committed = boundary is PartyAccessBoundary.Allowed or PartyAccessBoundary.DirectoryCommit;
        if (committed) {
            await using var db = await harness.Context.Services.GetRequiredService<IDbContextFactory<CrmHrDbContext>>().CreateDbContextAsync();
            var accepted = await db.Set<Party>().AsNoTracking().SingleAsync(x => x.DisplayName == name);
            Assert.Equal(accepted.Id, state.Created!.Id);
            Assert.Single(await db.Set<PartyContactPoint>().AsNoTracking().Where(x => x.PartyId == accepted.Id).ToListAsync());
        } else {
            Assert.Null(state.Created);
            Assert.Equal(before, await PartyAuthorityRowsAsync(harness, target.ProjectId));
            Assert.False(state.RequiresObservation);
        }
        if (boundary != PartyAccessBoundary.Allowed) {
            Assert.DoesNotContain(state.Choices, x => x.Name == name);
            Assert.Equal(name, state.Draft.Name);
        }
    }

    private static void ConfigurePartyAuthority(IServiceCollection services, EditingUiTestAccess access, PartyAuthorityProbe probe) {
        services.Replace(ServiceDescriptor.Singleton<IEditingUiAccess>(access));
        services.AddSingleton<IDbContextFactory<WorkbenchDbContext>>(provider => new PooledDbContextFactory<WorkbenchDbContext>(
            new DbContextOptionsBuilder<WorkbenchDbContext>(provider.GetRequiredService<DbContextOptions<WorkbenchDbContext>>()).AddInterceptors(probe).Options));
        services.AddSingleton<IDbContextFactory<CrmHrDbContext>>(provider => new PooledDbContextFactory<CrmHrDbContext>(
            new DbContextOptionsBuilder<CrmHrDbContext>(provider.GetRequiredService<DbContextOptions<CrmHrDbContext>>()).AddInterceptors(probe).Options));
    }

    private static async Task<string> PartyAuthorityRowsAsync(ComponentTestHarness harness, Guid project) {
        await using var crm = await harness.Context.Services.GetRequiredService<IDbContextFactory<CrmHrDbContext>>().CreateDbContextAsync();
        await using var wb = await harness.Context.Services.GetRequiredService<IDbContextFactory<WorkbenchDbContext>>().CreateDbContextAsync();
        var parties = await crm.Set<Party>().AsNoTracking().OrderBy(x => x.Id).ToListAsync();
        var contacts = await crm.Set<PartyContactPoint>().AsNoTracking().OrderBy(x => x.Id).ToListAsync();
        var assignments = await crm.Set<ProjectPartyAssignment>().AsNoTracking().Where(x => x.ProjectId == project).OrderBy(x => x.Id).ToListAsync();
        var nodes = await wb.Set<ProjectObjectRecord>().AsNoTracking().Where(x => x.ProjectId == project && !x.IsSystemManaged).OrderBy(x => x.Id).ToListAsync();
        return JsonSerializer.Serialize(new { Parties = parties, Contacts = contacts, Assignments = assignments, Nodes = nodes });
    }

    private sealed class PartyAuthorityProbe(EditingUiTestAccess access, PartyAccessBoundary boundary) : SaveChangesInterceptor, IDbTransactionInterceptor {
        public Guid Project { get; set; }
        public bool Armed { get; set; }
        private DbContext? matched;

        public override ValueTask<int> SavedChangesAsync(SaveChangesCompletedEventData eventData, int result, CancellationToken cancellationToken = default) {
            if (Armed && result > 0 && eventData.Context is { } db && Matches(db)) {
                Assert.NotNull(db.Database.CurrentTransaction);
                matched = db;
                if (boundary is PartyAccessBoundary.AssignmentSql or PartyAccessBoundary.PresentationSql or PartyAccessBoundary.DirectorySql) {
                    Revoke();
                }
            }
            return ValueTask.FromResult(result);
        }

        public Task TransactionCommittedAsync(DbTransaction transaction, TransactionEndEventData eventData, CancellationToken cancellationToken = default) {
            if (Armed && ReferenceEquals(matched, eventData.Context)) {
                Revoke();
            }
            return Task.CompletedTask;
        }

        private bool Matches(DbContext db) => boundary switch {
            PartyAccessBoundary.AssignmentSql or PartyAccessBoundary.AssignmentCommit => db is CrmHrDbContext && db.ChangeTracker.Entries<ProjectPartyAssignment>().Any(x => x.Entity.ProjectId == Project),
            PartyAccessBoundary.PresentationSql or PartyAccessBoundary.PresentationCommit => db is WorkbenchDbContext && db.ChangeTracker.Entries<ProjectObjectRecord>().Any(x => x.Entity.ProjectId == Project && x.Entity.ObjectType is ProjectObjectType.Participant or ProjectObjectType.Meeting),
            PartyAccessBoundary.DirectorySql or PartyAccessBoundary.DirectoryCommit => db is CrmHrDbContext && db.ChangeTracker.Entries<Party>().Any(x => x.Entity.DisplayName == "Owned directory authority candidate"),
            _ => false
        };

        private void Revoke() {
            Armed = false;
            access.Allowed = false;
        }
    }
}
