using System.Data;
using CanDoItAll.Infrastructure.Persistence;
using CanDoItAll.Infrastructure.Storage;
using CanDoItAll.Modules.CrmHr;
using CanDoItAll.Modules.Projects;
using CanDoItAll.SharedKernel;
using CanDoItAll.Tests.Support;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace CanDoItAll.Tests.Integration.Processes;

public sealed partial class ProcessCatalogAuthorityPersistenceTests {
    public enum CompensationForeignEdit { Participation, AccountConnection, StorageRouting }

    [Theory]
    [InlineData(CompensationForeignEdit.Participation)]
    [InlineData(CompensationForeignEdit.AccountConnection)]
    [InlineData(CompensationForeignEdit.StorageRouting)]
    public async Task Creation_compensation_retains_later_authoritative_foreign_owner_state(CompensationForeignEdit edit) {
        await using var app = await TestApplication.CreateAsync(ProjectsHarness());
        await using var scope = app.Services.CreateAsyncScope();
        var services = scope.ServiceProvider;
        var projects = services.GetRequiredService<ProjectsService>();
        var receipt = (await projects.CreateWithReceiptAsync(Guid.NewGuid(), new() { Name = "Foreign edit target" })).Value!;
        var rowId = Guid.NewGuid();
        if (edit == CompensationForeignEdit.StorageRouting) {
            await services.GetRequiredService<StorageCatalogService>().SaveRuleAsync(new() {
                Id = rowId, Name = "Later exact routing", ScopeKind = StorageRoutingScopeKind.Project,
                ProjectId = receipt.Project.ProjectId, Reason = "A human routing decision", Priority = 17,
                PreferredStorageId = Guid.NewGuid()
            });
            rowId = (await services.GetRequiredService<StorageCatalogService>().ListRulesAsync())
                .Single(row => row.ProjectId == receipt.Project.ProjectId).Id;
        } else {
            await using var crm = await services.GetRequiredService<IDbContextFactory<CrmHrDbContext>>().CreateDbContextAsync();
            var party = new Party {
                PartyType = PartyType.Person, DisplayName = "Later manager", LifecycleStatus = PartyLifecycleStatus.Active,
                CreatedAtUtc = DateTimeOffset.UtcNow, UpdatedAtUtc = DateTimeOffset.UtcNow
            };
            crm.Add(party);
            if (edit == CompensationForeignEdit.Participation) {
                crm.Add(new ProjectPartyAssignment {
                    Id = rowId, ProjectId = receipt.Project.ProjectId, ProjectLifetimeId = receipt.Project.LifetimeId,
                    PartyId = party.Id, AssignmentKind = ProjectPartyAssignmentKind.Manager,
                    Notes = "Later human participation", PhaseName = "Delivery", AllocationPercent = 27, IsPrimary = true
                });
            } else {
                var connection = new CrmAccountConnection {
                    AccountPartyId = Guid.NewGuid(), RelatedPartyId = party.Id, Notes = "Later human connection"
                };
                crm.Add(connection);
                crm.Add(new CrmAccountConnectionProjectLink { Id = rowId, AccountConnectionId = connection.Id, ProjectId = receipt.Project.ProjectId });
            }
            await crm.SaveChangesAsync();
        }
        Assert.False(await projects.TryCompensateCreationAsync(receipt));
        Assert.Equal(receipt.Project, await services.GetRequiredService<ProjectWriteAdmissionService>().CaptureAsync(receipt.Project.ProjectId));
        await using var read = await services.GetRequiredService<IDbContextFactory<CrmHrDbContext>>().CreateDbContextAsync();
        if (edit == CompensationForeignEdit.Participation) {
            var row = await read.Set<ProjectPartyAssignment>().SingleAsync(row => row.Id == rowId);
            Assert.Equal("Later human participation", row.Notes);
            Assert.Equal("Delivery", row.PhaseName);
            Assert.Equal(27m, row.AllocationPercent);
            Assert.Equal(receipt.Project.LifetimeId, row.ProjectLifetimeId);
        } else if (edit == CompensationForeignEdit.AccountConnection) {
            Assert.Equal(receipt.Project.ProjectId, (await read.Set<CrmAccountConnectionProjectLink>().SingleAsync(row => row.Id == rowId)).ProjectId);
        } else {
            var row = (await services.GetRequiredService<StorageCatalogService>().ListRulesAsync()).Single(row => row.Id == rowId);
            Assert.Equal("A human routing decision", row.Reason);
            Assert.Equal(17, row.Priority);
        }
        await using var owned = await services.GetRequiredService<IDbContextFactory<ProjectsDbContext>>().CreateDbContextAsync();
        Assert.False(await owned.Set<ProjectRetirementRecord>().AnyAsync(row => row.ProjectId == receipt.Project.ProjectId));
    }

    [Fact]
    public async Task Creation_compensation_queries_share_the_supplied_uncommitted_owner_transaction() {
        await using var app = await TestApplication.CreateAsync(ProjectsHarness());
        await using var scope = app.Services.CreateAsyncScope();
        var services = scope.ServiceProvider;
        var project = (await services.GetRequiredService<ProjectsService>().CreateWithAdmissionAsync(new() { Name = "Enlisted compensation facts" })).Value!;
        await using var crm = await services.GetRequiredService<IDbContextFactory<CrmHrDbContext>>().CreateDbContextAsync();
        var party = new Party { DisplayName = "Uncommitted manager", PartyType = PartyType.Person, LifecycleStatus = PartyLifecycleStatus.Active };
        crm.Add(party);
        await crm.SaveChangesAsync();
        await using var transaction = await crm.Database.BeginTransactionAsync(IsolationLevel.Serializable);
        crm.Add(new ProjectPartyAssignment { ProjectId = project.ProjectId, ProjectLifetimeId = project.LifetimeId,
            PartyId = party.Id, AssignmentKind = ProjectPartyAssignmentKind.Manager });
        await crm.SaveChangesAsync();
        using (services.GetRequiredService<CoordinatedDatabaseTransaction>().Enter(crm)) {
            Assert.False(await services.GetRequiredService<IProjectCreationCompensationGuard>().CanCompensateForMutationAsync(project));
        }
        await transaction.RollbackAsync();
        await using var independent = await services.GetRequiredService<IDbContextFactory<CrmHrDbContext>>().CreateDbContextAsync();
        Assert.False(await independent.Set<ProjectPartyAssignment>().AnyAsync(row => row.ProjectId == project.ProjectId));
    }

    [Fact]
    public async Task Ordinary_project_deletion_keeps_consumed_reservation_history() {
        await using var app = await TestApplication.CreateAsync(ProjectsHarness());
        await using var scope = app.Services.CreateAsyncScope();
        var services = scope.ServiceProvider;
        var fixture = await Fixture.CreateAsync(services);
        var owner = await CreationOwnerAsync(fixture, services);
        var (reservation, authority) = await ReserveChildAsync(fixture, owner, services);
        var projects = services.GetRequiredService<ProjectsService>();
        var receipt = (await projects.CreateWithReceiptAsync(reservation, new() { Name = "Ordinary deletion target" }, authorization: authority)).Value!;
        await projects.DeleteAsync(receipt.Project.ProjectId, expectedProjectAdmission: receipt.Project);
        await using var read = await services.GetRequiredService<IDbContextFactory<ProjectsDbContext>>().CreateDbContextAsync();
        var retained = await read.Set<ProjectCreationReservationRecord>().SingleAsync(row => row.Id == reservation.Id);
        Assert.Equal(ProjectCreationReservationState.Consumed, retained.State);
        Assert.NotNull(retained.ConsumedAtUtc);
        Assert.Null(retained.CancelledAtUtc);
    }

    [Fact]
    public async Task Public_project_deletion_rejects_the_original_lifetime_after_recreation() {
        await using var app = await TestApplication.CreateAsync(ProjectsHarness());
        await using var scope = app.Services.CreateAsyncScope();
        var services = scope.ServiceProvider;
        var projects = services.GetRequiredService<ProjectsService>();
        var original = (await projects.CreateWithAdmissionAsync(new() { Name = "Original public deletion" })).Value!;
        await projects.DeleteAsync(original.ProjectId, expectedProjectAdmission: original);
        var replacement = (await projects.CreateWithReceiptAsync(original.ProjectId, new() { Name = "Human replacement" })).Value!;
        await Assert.ThrowsAsync<ProjectWriteAdmissionRejectedException>(() => projects.DeleteAsync(original.ProjectId, expectedProjectAdmission: original));
        Assert.Equal(replacement.Project, await services.GetRequiredService<ProjectWriteAdmissionService>().CaptureAsync(original.ProjectId));
        Assert.Equal("Human replacement", (await projects.GetAsync(original.ProjectId)).Name);
    }

    [Fact]
    public async Task Create_with_admission_returns_the_original_commit_when_a_postcommit_callback_recreates_the_id() {
        var callback = new ProjectAdmissionCallback();
        await using var app = await TestApplication.CreateAsync(new() { ConfigureServices = services => {
            ProjectsHarness().ConfigureServices!(services);
            services.AddSingleton<IActivityStream>(callback);
        } });
        callback.Services = app.Services;
        await using var scope = app.Services.CreateAsyncScope();
        var projects = scope.ServiceProvider.GetRequiredService<ProjectsService>();
        var result = await projects.CreateWithAdmissionAsync(new() { Name = "Original callback creation" });
        Assert.True(result.IsSuccess);
        Assert.True(callback.Invoked);
        var original = Assert.IsType<ProjectWriteAdmission>(result.Value);
        Assert.Equal(callback.Original, original);
        Assert.NotNull(callback.Replacement);
        Assert.NotEqual(callback.Replacement.Project.LifetimeId, original.LifetimeId);
        Assert.Equal("Replacement during callback", (await projects.GetAsync(original.ProjectId)).Name);
    }

    private sealed class ProjectAdmissionCallback : IActivityStream {
        internal IServiceProvider Services { get; set; } = null!;
        internal bool Invoked { get; private set; }
        internal ProjectWriteAdmission? Original { get; private set; }
        internal ProjectCreationReceipt? Replacement { get; private set; }
        public async Task RecordAsync(ActivityWriteRequest request, CancellationToken cancellationToken = default) {
            if (Invoked || request.Category != "projects" || request.Action != "create") {
                return;
            }
            Invoked = true;
            await using var scope = Services.CreateAsyncScope();
            var projects = scope.ServiceProvider.GetRequiredService<ProjectsService>();
            Original = await scope.ServiceProvider.GetRequiredService<ProjectWriteAdmissionService>().CaptureAsync(request.ProjectId!.Value, cancellationToken);
            await projects.DeleteAsync(Original!.ProjectId, cancellationToken, Original);
            Replacement = (await projects.CreateWithReceiptAsync(Original.ProjectId, new() { Name = "Replacement during callback" }, cancellationToken: cancellationToken)).Value!;
        }
    }
}
