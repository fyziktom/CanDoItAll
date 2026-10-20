using CanDoItAll.AgentFramework.Models;
using CanDoItAll.Modules.Projects;
using CanDoItAll.Modules.Workbench;
using CanDoItAll.Processes.Application;
using CanDoItAll.Processes.Persistence;
using CanDoItAll.Processes.Runtime;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using static CanDoItAll.Tests.Integration.Processes.ProcessDecisionPersistenceFixture;

namespace CanDoItAll.Tests.Integration.Processes;

public sealed class ProcessRootMaterialSourceTests {
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Missing_header_is_rejected_by_storage_and_corrupt_header_cannot_start_capture(bool missing) {
        await using var fixture = await ProcessRootMaterialFixture.CreateAsync();
        await using var context = Context(fixture.Services);
        var launch = await context.PreparedLaunches.SingleAsync(item => item.RunId == fixture.State.RunId.Value);
        if (missing) {
            launch.AcceptedSourceHeaderJson = null;
            var failure = await Assert.ThrowsAsync<DbUpdateException>(() => context.SaveChangesAsync());
            var databaseFailure = Assert.IsType<PostgresException>(failure.InnerException);
            Assert.Equal(PostgresErrorCodes.CheckViolation, databaseFailure.SqlState);
            Assert.Equal("CK_process_prepared_launch_source", databaseFailure.ConstraintName);
            Assert.Empty(await context.DecisionBudgetAllocations.AsNoTracking().ToArrayAsync());
            return;
        }
        launch.AcceptedSourceHeaderFingerprint = "sha256:" + new string('0', 64);
        await context.SaveChangesAsync();
        await Assert.ThrowsAsync<InvalidDataException>(() => fixture.Coordinator(context).PrepareAsync(fixture.State));
        Assert.Empty(await context.DecisionBudgetAllocations.ToArrayAsync());
    }

    [Fact]
    public async Task Original_header_read_survives_an_invalid_whole_launch_review_copy() {
        await using var fixture = await ProcessRootMaterialFixture.CreateAsync();
        await using var context = Context(fixture.Services);
        await context.PreparedLaunches.Where(item => item.RunId == fixture.State.RunId.Value)
            .ExecuteUpdateAsync(update => update.SetProperty(item => item.PayloadJson, "invalid review copy"));
        Assert.Equal(ProcessDecisionRootPreparationStatus.Ready, (await fixture.Coordinator(context).PrepareAsync(fixture.State)).Status);
        Assert.Single(await context.DecisionMaterials.ToArrayAsync());
    }

    [Theory]
    [InlineData(ProcessDecisionRootCaptureRefusal.SourceChanged)]
    [InlineData(ProcessDecisionRootCaptureRefusal.LifetimeChanged)]
    [InlineData(ProcessDecisionRootCaptureRefusal.DatasetChanged)]
    public async Task Registered_capturer_checks_original_source_lifetime_and_namespace(ProcessDecisionRootCaptureRefusal expected) {
        await using var fixture = await ProcessRootMaterialFixture.CreateAsync();
        var request = fixture.CaptureRequest();
        request = expected switch {
            ProcessDecisionRootCaptureRefusal.SourceChanged => request with { Source = request.Source with { PolicyFingerprint = "sha256:" + new string('0', 64) } },
            ProcessDecisionRootCaptureRefusal.LifetimeChanged => request with { Source = request.Source with {
                ProjectAdmission = new(fixture.Sql.Project.DatabaseProfileId, fixture.Sql.Project.ProjectId, Guid.NewGuid())
            } },
            ProcessDecisionRootCaptureRefusal.DatasetChanged => request with { Scope = request.Scope with { WorkspaceNamespaceId = Guid.NewGuid() } },
            _ => throw new ArgumentOutOfRangeException(nameof(expected))
        };
        var refused = Assert.IsType<ProcessDecisionRootCaptureResult.Refused>(await fixture.Capturer.CaptureAsync(request));
        Assert.Equal(expected, refused.Reason);
    }

    [Fact]
    public async Task Deleting_and_recreating_the_project_cannot_activate_the_original_root_material() {
        await using var fixture = await ProcessRootMaterialFixture.CreateAsync();
        await using (var projects = await fixture.Services.GetRequiredService<IDbContextFactory<ProjectsDbContext>>().CreateDbContextAsync()) {
            await projects.Set<Project>().Where(item => item.Id == fixture.Sql.Project.ProjectId).ExecuteDeleteAsync();
            projects.Add(new Project { Id = fixture.Sql.Project.ProjectId, Name = "Replacement project", Slug = Guid.NewGuid().ToString("N") });
            await projects.SaveChangesAsync();
        }
        await using var context = Context(fixture.Services);
        var result = await fixture.Coordinator(context).PrepareAsync(fixture.State);
        Assert.Equal(ProcessDecisionRootPreparationStatus.ReviewRequired, result.Status);
        Assert.Empty(await context.DecisionMaterials.ToArrayAsync());
        Assert.NotNull(Assert.Single(await context.DecisionBudgetAllocations.ToArrayAsync()).OwnerWorkRevision);
    }

    [Fact]
    public async Task Unsupported_content_contract_is_rejected_before_capture() {
        await using var fixture = await ProcessRootMaterialFixture.CreateAsync();
        var request = fixture.CaptureRequest();
        await Assert.ThrowsAsync<InvalidDataException>(() => fixture.Capturer.CaptureAsync(request with {
            Scope = request.Scope with { ContentContract = new(Guid.NewGuid(), 1) }
        }));
    }

    [Theory]
    [InlineData(AgentSourceChange.Healthy)]
    [InlineData(AgentSourceChange.ToolsRevoked)]
    [InlineData(AgentSourceChange.ReadRevoked)]
    [InlineData(AgentSourceChange.ProfileChanged)]
    public async Task Registered_capture_requires_the_original_Agent_source_and_releases_its_catalog_lease(AgentSourceChange change) {
        await using var fixture = await ProjectTaskPlanProcessFixture.CreateAsync(registeredSource: true);
        var source = fixture.Process.Source.SourceAuthority!;
        var before = await fixture.ReadAsync();
        await using var context = Context(fixture.Sql.Services);
        var root = (await new EfProcessDecisionBudgetStore(context).ReadAsync(fixture.Process.State.RootRunId, null))!;
        ProcessDecisionRunScopeAdmission scope = new(new(Guid.NewGuid()), new(fixture.Process.State.RootRunId, root.BudgetId),
            fixture.Sql.Project.DatabaseProfileId, fixture.Sql.Binding.Namespace.Value, ProjectTaskPlanProcessContract.Content);
        var request = new ProcessDecisionRootCaptureRequest(source, scope, new(scope.ScopeId, 1, new(Guid.NewGuid())));
        var capturer = fixture.Sql.Services.GetServices<IProcessDecisionRootCapturer>().Single();
        using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        if (change == AgentSourceChange.ProfileChanged) {
            fixture.Generations.Advance();
        } else if (change != AgentSourceChange.Healthy) {
            await RevokeAsync();
        }
        var result = await capturer.CaptureAsync(request, deadline.Token);
        if (change == AgentSourceChange.Healthy) {
            Assert.Equal(scope.MaterialScope, Assert.IsType<ProcessDecisionRootCaptureResult.Captured>(result).Material.Scope);
            await RevokeAsync();
            result = await capturer.CaptureAsync(request, deadline.Token);
        }
        Assert.Equal(ProcessDecisionRootCaptureRefusal.SourceChanged,
            Assert.IsType<ProcessDecisionRootCaptureResult.Refused>(result).Reason);
        Assert.Equal(before.Revision, (await fixture.ReadAsync()).Revision);

        Task RevokeAsync() => fixture.Store.UpdateCatalogAsync(catalog => catalog with {
            Agents = catalog.Agents.Select(agent => agent.Id != fixture.Launcher!.Id ? agent :
                change == AgentSourceChange.ReadRevoked ? agent with {
                    ConfigurationJson = AgentProjectStructureAccessMetadata.Write(agent.ConfigurationJson, new AgentProjectStructureAccessSettings())
                } : agent with { Permissions = agent.Permissions with { CanUseTools = false } }).ToArray()
        }, deadline.Token);
    }

    public enum AgentSourceChange { Healthy, ToolsRevoked, ReadRevoked, ProfileChanged }
}
