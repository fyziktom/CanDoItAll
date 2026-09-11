using System.Text.Json;
using CanDoItAll.Infrastructure.ControlPlane;
using CanDoItAll.Infrastructure.Persistence;
using CanDoItAll.Modules.CrmHr;
using CanDoItAll.Modules.Projects;
using CanDoItAll.SharedKernel;
using CanDoItAll.Tests.Support;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;

namespace CanDoItAll.Tests.Integration.Processes;

public sealed partial class ProcessCatalogAuthorityPersistenceTests {
    public enum OpportunityCompensationEdit { None, Project, Assignment, RecreatedProject }

    [Theory]
    [InlineData(false, OpportunityCompensationEdit.None)]
    [InlineData(false, OpportunityCompensationEdit.Project)]
    [InlineData(false, OpportunityCompensationEdit.Assignment)]
    [InlineData(false, OpportunityCompensationEdit.RecreatedProject)]
    [InlineData(true, OpportunityCompensationEdit.None)]
    [InlineData(true, OpportunityCompensationEdit.Assignment)]
    public async Task Opportunity_compensation_restores_only_its_exact_original_effect(bool existingProject, OpportunityCompensationEdit edit) {
        var probe = new OpportunityCompensationProbe();
        await using var app = await TestApplication.CreateAsync(OpportunityCompensationHarness(probe));
        await using var scope = app.Services.CreateAsyncScope();
        var services = scope.ServiceProvider;
        probe.Options = services.GetRequiredService<DbContextOptions<CrmHrDbContext>>();
        var (request, originalRows) = await PrepareOpportunityCompensationAsync(services, existingProject);
        string? laterRows = null;
        ProjectWriteAdmission? replacement = null;
        probe.BeforeFinalSave = async projectId => {
            var projects = services.GetRequiredService<ProjectsService>();
            if (edit == OpportunityCompensationEdit.Project) {
                var model = await projects.GetAsync(projectId);
                model.Name = "Later human project edit";
                Assert.True((await projects.SaveAsync(model)).IsSuccess);
            } else if (edit == OpportunityCompensationEdit.Assignment) {
                await using var crm = new CrmHrDbContext(probe.Options);
                var assignment = await crm.Set<ProjectPartyAssignment>().SingleAsync(row =>
                    row.ProjectId == projectId && row.AssignmentKind == ProjectPartyAssignmentKind.Manager);
                assignment.Notes = "Later human assignment edit";
                assignment.PhaseName = "A human milestone";
                assignment.AllocationPercent = 41;
                assignment.Source = "Human authority";
                await crm.SaveChangesAsync();
                laterRows = await ReadCompensationAssignmentsAsync(crm, projectId);
            } else if (edit == OpportunityCompensationEdit.RecreatedProject) {
                var original = await services.GetRequiredService<ProjectWriteAdmissionService>().CaptureAsync(projectId);
                await projects.DeleteAsync(projectId, expectedProjectAdmission: original);
                replacement = (await projects.CreateWithReceiptAsync(projectId, new() { Name = "Recreated by a human" })).Value!.Project;
            }
        };
        probe.ArmedOpportunityId = request.OpportunityId;
        if (edit == OpportunityCompensationEdit.None) {
            var result = await services.GetRequiredService<CrmService>().ConvertOpportunityToProjectAsync(request);
            Assert.True(result.IsFailure);
            Assert.Contains(result.Errors, error => error.Code == "crmhr.crm.opportunity-conversion-concurrency-conflict");
        } else {
            var failure = await Assert.ThrowsAsync<CrmOpportunityConversionRequiresObservationException>(() =>
                services.GetRequiredService<CrmService>().ConvertOpportunityToProjectAsync(request));
            Assert.Equal(probe.TargetProjectId, failure.Project.ProjectId);
            if (failure.InnerException is AggregateException aggregate) {
                Assert.Same(probe.Failure, aggregate.InnerExceptions[0]);
            } else {
                Assert.Same(probe.Failure, failure.InnerException);
            }
        }
        Assert.True(probe.Fired);
        Assert.IsType<DbUpdateConcurrencyException>(probe.Failure);
        var projectId = probe.TargetProjectId!.Value;
        await using var owner = await services.GetRequiredService<IDbContextFactory<ProjectsDbContext>>().CreateDbContextAsync();
        await using var read = new CrmHrDbContext(probe.Options);
        var opportunity = await read.Set<Opportunity>().SingleAsync(row => row.Id == request.OpportunityId);
        Assert.Null(opportunity.LinkedProjectId);
        Assert.Equal("Concurrent opportunity edit", opportunity.Notes);
        if (edit == OpportunityCompensationEdit.None && !existingProject) {
            Assert.False(await owner.Set<Project>().AnyAsync(row => row.Id == projectId));
            Assert.Equal("[]", await ReadCompensationAssignmentsAsync(read, projectId));
        } else {
            var project = await owner.Set<Project>().SingleAsync(row => row.Id == projectId);
            if (edit == OpportunityCompensationEdit.Project) {
                Assert.Equal("Later human project edit", project.Name);
            } else if (edit == OpportunityCompensationEdit.RecreatedProject) {
                Assert.Equal(replacement!.LifetimeId, project.LifetimeId);
                Assert.Equal("Recreated by a human", project.Name);
            }
            if (edit == OpportunityCompensationEdit.Assignment) {
                Assert.Equal(laterRows, await ReadCompensationAssignmentsAsync(read, projectId));
            } else if (existingProject) {
                Assert.Equal(originalRows, await ReadCompensationAssignmentsAsync(read, projectId));
            }
        }
    }

    [Fact]
    public async Task Opportunity_unknown_commit_ack_retains_the_actual_committed_conversion_and_assignments() {
        var probe = new OpportunityCompensationProbe { LoseAcknowledgement = true };
        await using var app = await TestApplication.CreateAsync(OpportunityCompensationHarness(probe));
        await using var scope = app.Services.CreateAsyncScope();
        var services = scope.ServiceProvider;
        probe.Options = services.GetRequiredService<DbContextOptions<CrmHrDbContext>>();
        var (request, _) = await PrepareOpportunityCompensationAsync(services, false);
        probe.ArmedOpportunityId = request.OpportunityId;
        var failure = await Assert.ThrowsAsync<CrmOpportunityConversionRequiresObservationException>(() =>
            services.GetRequiredService<CrmService>().ConvertOpportunityToProjectAsync(request));
        Assert.True(probe.Fired);
        Assert.Same(probe.Failure, failure.InnerException);
        await using var owner = await services.GetRequiredService<IDbContextFactory<ProjectsDbContext>>().CreateDbContextAsync();
        Assert.True(await owner.Set<Project>().AnyAsync(row => row.Id == failure.Project.ProjectId && row.LifetimeId == failure.Project.LifetimeId));
        Assert.False(await owner.Set<ProjectRetirementRecord>().AnyAsync(row => row.ProjectId == failure.Project.ProjectId));
        await using var read = new CrmHrDbContext(probe.Options);
        Assert.Equal(failure.Project.ProjectId, (await read.Set<Opportunity>().SingleAsync(row => row.Id == request.OpportunityId)).LinkedProjectId);
        var assignments = await read.Set<ProjectPartyAssignment>().Where(row => row.ProjectId == failure.Project.ProjectId).ToArrayAsync();
        Assert.Equal(2, assignments.Length);
        Assert.All(assignments, row => Assert.Equal(failure.Project.LifetimeId, row.ProjectLifetimeId));
    }

    private static TestHarnessOptions OpportunityCompensationHarness(OpportunityCompensationProbe probe) => new() { ConfigureServices = services => {
        ProjectsHarness().ConfigureServices!(services);
        services.AddSingleton<IDbContextFactory<CrmHrDbContext>>(provider => {
            var builder = new DbContextOptionsBuilder<CrmHrDbContext>();
            AppDbContextOptionsConfigurator.Configure(builder, provider.GetRequiredService<ICanonicalRuntimeDatabase>().Profile);
            builder.AddInterceptors(probe);
            return new Factory<CrmHrDbContext>(builder.Options, static options => new(options));
        });
    } };

    private static async Task<(CrmOpportunityConversionEditorModel, string)> PrepareOpportunityCompensationAsync(IServiceProvider services, bool existingProject) {
        var accountId = Guid.NewGuid();
        var ownerId = Guid.NewGuid();
        await using var context = await services.GetRequiredService<IDbContextFactory<CrmHrDbContext>>().CreateDbContextAsync();
        context.AddRange(new Party { Id = accountId, PartyType = PartyType.Organization, LifecycleStatus = PartyLifecycleStatus.Active, DisplayName = "Compensation account" },
            new Party { Id = ownerId, PartyType = PartyType.Person, LifecycleStatus = PartyLifecycleStatus.Active, DisplayName = "Compensation owner" });
        await context.SaveChangesAsync();
        var crm = services.GetRequiredService<CrmService>();
        var saved = await crm.SaveOpportunityAsync(new() { AccountPartyId = accountId, OwnerPartyId = ownerId,
            Title = "Owner compensation boundary", Stage = OpportunityStage.Won, CurrencyCode = "USD", Amount = 100,
            ProbabilityPercent = 100, LastChangedBy = "integration-tests" });
        Assert.True(saved.IsSuccess);
        var opportunity = await crm.GetOpportunityAsync(saved.Value);
        Assert.NotNull(opportunity);
        ProjectWriteAdmission? admission = null;
        var before = "[]";
        if (existingProject) {
            admission = (await services.GetRequiredService<ProjectsService>().CreateWithAdmissionAsync(new() { Name = "Existing conversion target" })).Value!;
            context.Add(new ProjectPartyAssignment {
                ProjectId = admission.ProjectId, ProjectLifetimeId = admission.LifetimeId, PartyId = ownerId,
                AssignmentKind = ProjectPartyAssignmentKind.Manager, PhaseName = "Original phase", OpportunityId = saved.Value,
                AllocationPercent = 19.25m, StartsAtUtc = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero),
                EndsAtUtc = new DateTimeOffset(2026, 12, 1, 0, 0, 0, TimeSpan.Zero), IsPrimary = true,
                Source = "  Exact original source  ", Notes = "Exact original notes"
            });
            await context.SaveChangesAsync();
            before = await ReadCompensationAssignmentsAsync(context, admission.ProjectId);
        }
        return (new() { OpportunityId = saved.Value, ExpectedUpdatedAtUtc = opportunity.UpdatedAtUtc,
            LinkExistingProject = existingProject, ExistingProjectId = admission?.ProjectId, ExpectedProjectAdmission = admission,
            ProjectName = "Created conversion target", LastChangedBy = "integration-tests" }, before);
    }

    private static async Task<string> ReadCompensationAssignmentsAsync(CrmHrDbContext context, Guid projectId) =>
        JsonSerializer.Serialize(await context.Set<ProjectPartyAssignment>().AsNoTracking().Where(row => row.ProjectId == projectId)
            .OrderBy(row => row.Id).ToArrayAsync());

    private sealed class OpportunityCompensationProbe : SaveChangesInterceptor {
        internal DbContextOptions<CrmHrDbContext> Options { get; set; } = null!;
        internal Guid? ArmedOpportunityId { get; set; }
        internal Guid? TargetProjectId { get; private set; }
        internal Func<Guid, Task>? BeforeFinalSave { get; set; }
        internal bool LoseAcknowledgement { get; init; }
        internal bool Fired { get; private set; }
        internal Exception? Failure { get; private set; }
        private bool throwAfterCommit;

        public override async ValueTask<InterceptionResult<int>> SavingChangesAsync(DbContextEventData eventData,
            InterceptionResult<int> result, CancellationToken cancellationToken = default) {
            var entry = eventData.Context?.ChangeTracker.Entries<Opportunity>().SingleOrDefault(item =>
                item.Entity.Id == ArmedOpportunityId && item.State == EntityState.Modified && item.Entity.LinkedProjectId.HasValue);
            if (entry is null) {
                return result;
            }
            ArmedOpportunityId = null;
            TargetProjectId = entry.Entity.LinkedProjectId;
            Fired = true;
            if (BeforeFinalSave is not null) {
                await BeforeFinalSave(TargetProjectId!.Value);
            }
            if (LoseAcknowledgement) {
                throwAfterCommit = true;
            } else {
                await using var concurrent = new CrmHrDbContext(Options);
                await concurrent.Set<Opportunity>().Where(row => row.Id == entry.Entity.Id).ExecuteUpdateAsync(setters => setters
                    .SetProperty(row => row.UpdatedAtUtc, DateTimeOffset.UtcNow.AddMinutes(1))
                    .SetProperty(row => row.Notes, "Concurrent opportunity edit"), cancellationToken);
            }
            return result;
        }

        public override ValueTask<int> SavedChangesAsync(SaveChangesCompletedEventData eventData, int result, CancellationToken cancellationToken = default) {
            if (throwAfterCommit) {
                throwAfterCommit = false;
                Failure = new IOException("Lost actual CRM commit acknowledgement");
                throw Failure;
            }
            return ValueTask.FromResult(result);
        }

        public override Task SaveChangesFailedAsync(DbContextErrorEventData eventData, CancellationToken cancellationToken = default) {
            Failure ??= eventData.Exception;
            return Task.CompletedTask;
        }

        public override ValueTask<InterceptionResult> ThrowingConcurrencyExceptionAsync(ConcurrencyExceptionEventData eventData,
            InterceptionResult result, CancellationToken cancellationToken = default) {
            Failure ??= eventData.Exception;
            return ValueTask.FromResult(result);
        }
    }
}
