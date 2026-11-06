using System.Data.Common;
using CanDoItAll.Modules.CrmHr;
using CanDoItAll.Modules.Projects;
using CanDoItAll.SharedKernel;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace CanDoItAll.Tests.Integration.CrmHr;

public sealed class CrmHrOpportunityConversionConcurrencyTests {
    [Fact]
    public async Task Existing_human_assignment_identity_and_complete_legacy_metadata_survive_a_successful_conversion() {
        await using var application = await TestApplication.CreateAsync();
        await using var scope = application.Services.CreateAsyncScope();
        var services = scope.ServiceProvider;
        var crm = services.GetRequiredService<CrmService>();
        var original = await CrmHrOpportunityConcurrentEditingTests.SeedAsync(services);
        var won = CrmHrOpportunityConcurrentEditingTests.Edit(original);
        won.Stage = OpportunityStage.Won;
        Assert.True((await crm.SaveOpportunityAsync(won)).IsSuccess);
        var opportunity = Assert.IsType<CrmOpportunityDetailModel>(await crm.GetOpportunityAsync(original.Id));
        var project = (await services.GetRequiredService<ProjectsService>().CreateWithAdmissionAsync(new() { Name = "Existing historical handoff target" })).Value!;
        var id = Guid.NewGuid();
        var starts = new DateTimeOffset(2031, 1, 2, 3, 4, 5, TimeSpan.Zero).AddTicks(1230);
        await using (var setup = await services.GetRequiredService<IDbContextFactory<CrmHrDbContext>>().CreateDbContextAsync()) {
            setup.Add(new ProjectPartyAssignment {
                Id = id, ProjectId = project.ProjectId, ProjectLifetimeId = project.LifetimeId, PartyId = opportunity.OwnerPartyId,
                AssignmentKind = ProjectPartyAssignmentKind.Manager, PhaseName = "Historical human milestone",
                StartsAtUtc = starts, EndsAtUtc = starts.AddDays(3), AllocationPercent = 27.125m,
                Source = "  Historical source  ", Notes = "  Human notes retained exactly  "
            });
            await setup.SaveChangesAsync();
        }
        var observed = await crm.ObserveOpportunityProjectBasisAsync(opportunity.Id, opportunity.UpdatedAtUtc, project);
        Assert.True(observed.IsSuccess);

        var result = await crm.ConvertOpportunityToProjectAsync(new() {
            OpportunityId = opportunity.Id, ExpectedUpdatedAtUtc = opportunity.UpdatedAtUtc,
            LinkExistingProject = true, ExistingProjectId = project.ProjectId, ExpectedProjectAdmission = project,
            ExpectedProjectBasis = observed.Value
        });

        Assert.True(result.IsSuccess);
        await using var read = await services.GetRequiredService<IDbContextFactory<CrmHrDbContext>>().CreateDbContextAsync();
        var retained = await read.Set<ProjectPartyAssignment>().SingleAsync(row => row.Id == id);
        Assert.Equal("Historical human milestone", retained.PhaseName);
        Assert.Equal(starts, retained.StartsAtUtc);
        Assert.Equal(starts.AddDays(3), retained.EndsAtUtc);
        Assert.Equal(27.125m, retained.AllocationPercent);
        Assert.Equal("  Historical source  ", retained.Source);
        Assert.Equal("  Human notes retained exactly  ", retained.Notes);
        Assert.False(retained.IsPrimary);
        Assert.Equal(2, await read.Set<ProjectPartyAssignment>().CountAsync(row => row.ProjectId == project.ProjectId));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task A_project_retired_after_handoff_cannot_receive_the_final_conversion_link(bool recreate) {
        var gate = new ConversionLifecycleGate();
        await using var application = await TestApplication.CreateAsync(new() {
            ConfigureServices = services => {
                services.RemoveAll<IDbContextFactory<CrmHrDbContext>>();
                services.AddSingleton<IDbContextFactory<CrmHrDbContext>>(provider =>
                    new GatedFactory(provider.GetRequiredService<DbContextOptions<CrmHrDbContext>>(), gate));
            }
        });
        await using var scope = application.Services.CreateAsyncScope();
        var services = scope.ServiceProvider;
        var crm = services.GetRequiredService<CrmService>();
        var original = await CrmHrOpportunityConcurrentEditingTests.SeedAsync(services);
        var won = CrmHrOpportunityConcurrentEditingTests.Edit(original);
        won.Stage = OpportunityStage.Won;
        Assert.True((await crm.SaveOpportunityAsync(won)).IsSuccess);
        var opportunity = Assert.IsType<CrmOpportunityDetailModel>(await crm.GetOpportunityAsync(original.Id));
        ProjectWriteAdmission? replacement = null;
        gate.OpportunityId = opportunity.Id;
        gate.Retire = async projectId => {
            await new ProjectLifecycleTestDriver(services).DeleteAsync(projectId);
            if (recreate) {
                var created = await services.GetRequiredService<ProjectsService>().CreateWithReceiptAsync(projectId,
                    new() { Name = "Human replacement after conversion handoff" });
                Assert.True(created.IsSuccess);
                replacement = created.Value!.Project;
            }
        };
        Result<CrmOpportunityConversionResult>? result = null;

        var failure = await Record.ExceptionAsync(async () => result = await crm.ConvertOpportunityToProjectAsync(new() {
            OpportunityId = opportunity.Id, ExpectedUpdatedAtUtc = opportunity.UpdatedAtUtc,
            ProjectName = "Retired between conversion owners"
        }));

        Assert.True(gate.Fired);
        Assert.True(gate.LifecycleCompleted);
        Assert.True(failure is CrmOpportunityConversionRequiresObservationException || result?.IsFailure == true);
        var after = Assert.IsType<CrmOpportunityDetailModel>(await crm.GetOpportunityAsync(opportunity.Id));
        Assert.Null(after.LinkedProjectId);
        Assert.Equal(opportunity.UpdatedAtUtc, after.UpdatedAtUtc);
        if (replacement is not null) {
            var project = await services.GetRequiredService<IProjectRecordQueryService>().GetAsync(replacement.ProjectId);
            Assert.Equal(replacement.LifetimeId, project!.LifetimeId);
            Assert.Equal("Human replacement after conversion handoff", project.Name);
        }
    }

    [Fact]
    public async Task An_existing_project_membership_changed_after_observation_requires_a_new_explicit_conversion_basis() {
        await using var application = await TestApplication.CreateAsync();
        await using var scope = application.Services.CreateAsyncScope();
        var services = scope.ServiceProvider;
        var crm = services.GetRequiredService<CrmService>();
        var original = await CrmHrOpportunityConcurrentEditingTests.SeedAsync(services);
        var won = CrmHrOpportunityConcurrentEditingTests.Edit(original);
        won.Stage = OpportunityStage.Won;
        Assert.True((await crm.SaveOpportunityAsync(won)).IsSuccess);
        var opportunity = Assert.IsType<CrmOpportunityDetailModel>(await crm.GetOpportunityAsync(original.Id));
        var project = (await services.GetRequiredService<ProjectsService>().CreateWithAdmissionAsync(new() { Name = "Reviewed membership target" })).Value!;
        var observed = await crm.ObserveOpportunityProjectBasisAsync(opportunity.Id, opportunity.UpdatedAtUtc, project);
        Assert.True(observed.IsSuccess);
        Assert.Empty(observed.Value!.Assignments);
        var peer = await services.GetRequiredService<ProjectPartyIntegrationService>().SaveAssignmentAsync(new() {
            ProjectId = project.ProjectId, PartyId = opportunity.OwnerPartyId, Role = ProjectPartyAssignmentRole.Manager,
            Notes = "Peer authority after the conversion basis", ExpectedProjectAdmission = project
        });
        Assert.True(peer.IsSuccess);

        var result = await crm.ConvertOpportunityToProjectAsync(new() {
            OpportunityId = opportunity.Id, ExpectedUpdatedAtUtc = opportunity.UpdatedAtUtc,
            LinkExistingProject = true, ExistingProjectId = project.ProjectId, ExpectedProjectAdmission = project,
            ExpectedProjectBasis = observed.Value
        });

        Assert.True(result.IsFailure);
        Assert.Null((await crm.GetOpportunityAsync(opportunity.Id))!.LinkedProjectId);
        var rows = await services.GetRequiredService<ProjectPartyIntegrationService>().ListAssignmentsDetailedAsync(project.ProjectId);
        var retained = Assert.Single(rows);
        Assert.Equal(peer.Value, retained.Id);
        Assert.Equal("Peer authority after the conversion basis", retained.Notes);
    }

    [Fact]
    public async Task A_conversion_receipt_precedes_a_failed_continuation_and_identifies_the_actual_committed_project() {
        await using var application = await TestApplication.CreateAsync();
        await using var scope = application.Services.CreateAsyncScope();
        var services = scope.ServiceProvider;
        var crm = services.GetRequiredService<CrmService>();
        var original = await CrmHrOpportunityConcurrentEditingTests.SeedAsync(services);
        var won = CrmHrOpportunityConcurrentEditingTests.Edit(original);
        won.Stage = OpportunityStage.Won;
        Assert.True((await crm.SaveOpportunityAsync(won)).IsSuccess);
        var basis = Assert.IsType<CrmOpportunityDetailModel>(await crm.GetOpportunityAsync(original.Id));
        CrmOpportunityConversionResult? accepted = null;

        await Assert.ThrowsAsync<ReceiptContinuationFailure>(() => crm.ConvertOpportunityToProjectAsync(new() {
            OpportunityId = basis.Id, ExpectedUpdatedAtUtc = basis.UpdatedAtUtc, ProjectName = "Exact conversion acceptance"
        }, onCommitted: receipt => {
            accepted = receipt;
            throw new ReceiptContinuationFailure();
        }));

        Assert.NotNull(accepted);
        Assert.True(accepted.CreatedNewProject);
        var after = Assert.IsType<CrmOpportunityDetailModel>(await crm.GetOpportunityAsync(basis.Id));
        Assert.Equal(accepted.ProjectId, after.LinkedProjectId);
        Assert.Equal(accepted.ProjectAdmission, after.LinkedProjectAdmission);
        Assert.Equal(accepted.OpportunityUpdatedAtUtc, after.UpdatedAtUtc);
        Assert.NotEqual(basis.UpdatedAtUtc, after.UpdatedAtUtc);
        Assert.NotNull(await services.GetRequiredService<IProjectRecordQueryService>().GetAsync(accepted.ProjectId));
    }

    [Fact]
    public async Task Revocation_after_conversion_SQL_rolls_back_the_link_and_compensates_its_exact_project_effects() {
        var gate = new ConversionGrantGate();
        await using var application = await TestApplication.CreateAsync(new() {
            ConfigureServices = services => {
                services.RemoveAll<IDbContextFactory<CrmHrDbContext>>();
                services.AddSingleton<IDbContextFactory<CrmHrDbContext>>(provider =>
                    new GatedFactory(provider.GetRequiredService<DbContextOptions<CrmHrDbContext>>(), gate));
            }
        });
        await using var scope = application.Services.CreateAsyncScope();
        var services = scope.ServiceProvider;
        var crm = services.GetRequiredService<CrmService>();
        var original = await CrmHrOpportunityConcurrentEditingTests.SeedAsync(services);
        var won = CrmHrOpportunityConcurrentEditingTests.Edit(original);
        won.Stage = OpportunityStage.Won;
        Assert.True((await crm.SaveOpportunityAsync(won)).IsSuccess);
        var basis = Assert.IsType<CrmOpportunityDetailModel>(await crm.GetOpportunityAsync(original.Id));
        const string projectName = "Revoked conversion exact compensation";
        var published = false;
        using var authority = services.GetRequiredService<CrmHrChanges>().EnterUiWrite(_ => {
            if (gate.Triggered) {
                throw new UnauthorizedAccessException("Conversion grant retired after SQL.");
            }
            return ValueTask.CompletedTask;
        });
        gate.OpportunityId = basis.Id;

        var failure = await Record.ExceptionAsync(() => crm.ConvertOpportunityToProjectAsync(new() {
            OpportunityId = basis.Id, ExpectedUpdatedAtUtc = basis.UpdatedAtUtc, ProjectName = projectName
        }, onCommitted: _ => published = true));

        Assert.True(gate.Triggered);
        Assert.IsType<UnauthorizedAccessException>(failure);
        Assert.True(gate.ObservedTransaction);
        Assert.False(published);
        var after = Assert.IsType<CrmOpportunityDetailModel>(await crm.GetOpportunityAsync(basis.Id));
        Assert.Null(after.LinkedProjectId);
        Assert.Equal(basis.UpdatedAtUtc, after.UpdatedAtUtc);
        var projects = await services.GetRequiredService<IProjectRecordQueryService>().SearchAsync(new(projectName));
        Assert.DoesNotContain(projects.Items, project => project.Name == projectName);
    }

    [Fact]
    public async Task Revoked_conversion_admission_cannot_create_a_project_or_any_linked_effect() {
        await using var application = await TestApplication.CreateAsync();
        await using var scope = application.Services.CreateAsyncScope();
        var services = scope.ServiceProvider;
        var crm = services.GetRequiredService<CrmService>();
        var original = await CrmHrOpportunityConcurrentEditingTests.SeedAsync(services);
        var won = CrmHrOpportunityConcurrentEditingTests.Edit(original);
        won.Stage = OpportunityStage.Won;
        Assert.True((await crm.SaveOpportunityAsync(won)).IsSuccess);
        var basis = Assert.IsType<CrmOpportunityDetailModel>(await crm.GetOpportunityAsync(original.Id));
        const string projectName = "Refused conversion has no project";
        var demands = 0;
        using var authority = services.GetRequiredService<CrmHrChanges>().EnterUiWrite(_ => {
            demands++;
            throw new UnauthorizedAccessException("Conversion grant retired before any effect.");
        });

        var failure = await Record.ExceptionAsync(() => crm.ConvertOpportunityToProjectAsync(new() {
            OpportunityId = basis.Id,
            ExpectedUpdatedAtUtc = basis.UpdatedAtUtc,
            ProjectName = projectName
        }));

        Assert.IsType<UnauthorizedAccessException>(failure);
        Assert.True(demands > 0);
        var after = Assert.IsType<CrmOpportunityDetailModel>(await crm.GetOpportunityAsync(basis.Id));
        Assert.Null(after.LinkedProjectId);
        Assert.Equal(basis.UpdatedAtUtc, after.UpdatedAtUtc);
        var projects = await services.GetRequiredService<IProjectRecordQueryService>().SearchAsync(new(projectName));
        Assert.DoesNotContain(projects.Items, project => project.Name == projectName);
    }

    private sealed class ConversionGrantGate : SaveChangesInterceptor {
        public Guid? OpportunityId { get; set; }
        public bool Triggered { get; private set; }
        public bool ObservedTransaction { get; private set; }
        public override ValueTask<int> SavedChangesAsync(SaveChangesCompletedEventData eventData, int result, CancellationToken cancellationToken = default) {
            if (!Triggered && eventData.Context?.ChangeTracker.Entries<Opportunity>().Any(entry =>
                    entry.Entity.Id == OpportunityId && entry.Entity.LinkedProjectId.HasValue) == true) {
                Triggered = true;
                ObservedTransaction = eventData.Context.Database.CurrentTransaction is not null;
            }
            return ValueTask.FromResult(result);
        }
    }

    private sealed class ReceiptContinuationFailure : Exception;

    private sealed class ConversionLifecycleGate : DbTransactionInterceptor {
        public Guid? OpportunityId { get; set; }
        public Func<Guid, Task> Retire { get; set; } = default!;
        public bool Fired { get; private set; }
        public bool LifecycleCompleted { get; private set; }
        public override async ValueTask<InterceptionResult<DbTransaction>> TransactionStartingAsync(DbConnection connection,
            TransactionStartingEventData eventData, InterceptionResult<DbTransaction> result, CancellationToken cancellationToken = default) {
            var entry = eventData.Context?.ChangeTracker.Entries<Opportunity>().SingleOrDefault(item =>
                item.Entity.Id == OpportunityId && item.State == EntityState.Modified && item.Entity.LinkedProjectId.HasValue);
            if (!Fired && entry is not null) {
                Fired = true;
                await Retire(entry.Entity.LinkedProjectId!.Value);
                LifecycleCompleted = true;
            }
            return result;
        }
    }

    private sealed class GatedFactory(DbContextOptions<CrmHrDbContext> options, IInterceptor gate) : IDbContextFactory<CrmHrDbContext> {
        public CrmHrDbContext CreateDbContext() => new(new DbContextOptionsBuilder<CrmHrDbContext>(options).AddInterceptors(gate).Options);
        public Task<CrmHrDbContext> CreateDbContextAsync(CancellationToken cancellationToken = default) => Task.FromResult(CreateDbContext());
    }
}
