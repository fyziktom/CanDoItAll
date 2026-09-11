using CanDoItAll.AgentFramework.Models;
using CanDoItAll.AgentFramework.Workflows.Abstractions;
using CanDoItAll.Infrastructure.ControlPlane;
using CanDoItAll.Infrastructure.Persistence;
using CanDoItAll.Modules.SchedulerPlanner;
using CanDoItAll.Modules.Workbench;
using CanDoItAll.SharedKernel;
using CanDoItAll.Tests.Support;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;

namespace CanDoItAll.Tests.Integration;

public sealed partial class SchedulerSourceAuthorityPersistenceTests {
    [Fact]
    public async Task Scoped_global_schedule_preserves_source_without_Structure_permissions_and_releases_lease_before_trigger() {
        await using var app = await TestApplication.CreateAsync(Harness());
        await using var scope = app.Services.CreateAsyncScope();
        var services = scope.ServiceProvider;
        var fixture = await Fixture.CreateAsync(services);
        var authorityService = services.GetRequiredService<ProjectStructureWorkflowAuthorityService>();
        var authority = await authorityService.CaptureAgentAsync(fixture.Agent, fixture.Run.Origin!.StructureAuthority!.AgentGovernance!);
        Assert.NotNull(authority.ProjectScope);
        Assert.Empty(authority.ProjectScope.Projects);
        Assert.False(authority.CanCreateTasks);
        Assert.False(authority.CanCreateAssets);
        var options = new DbContextOptionsBuilder<SchedulerPlannerDbContext>();
        AppDbContextOptionsConfigurator.Configure(options, services.GetRequiredService<ICanonicalRuntimeDatabase>().Profile);
        var flushed = new SaveCatalogProbe(fixture.Writer);
        options.AddInterceptors(flushed);
        var trigger = new CatalogEditingTrigger(fixture);
        var planner = new SchedulerPlannerService(new Factory<SchedulerPlannerDbContext>(options.Options, static value => new(value)),
            trigger, new QuartzCronDescriptionService(), fixture.Catalog, new SchedulerWorkflowInputSchemaService(fixture.Catalog),
            services.GetRequiredService<IClock>(), NullLogger<SchedulerPlannerService>.Instance,
            services.GetRequiredService<IWorkflowScheduledSourceAuthorityPolicy>(), authorityService,
            services.GetRequiredService<CoordinatedDatabaseTransaction>());
        var editor = await planner.GetPlanEditorAsync(fixture.Plan.Id);
        editor.StructureAuthority = authority;
        editor.Name = "Scoped source retained";
        var saved = await planner.SavePlanAsync(editor);
        Assert.True(flushed.Observed);
        Assert.True(trigger.Called);
        await using var read = fixture.SchedulerContext();
        var retained = await read.Set<SchedulerPlan>().AsNoTracking().SingleAsync(row => row.Id == saved.Id);
        var restored = SchedulerFireSnapshot.ParseAuthority(retained.StructureAuthorityJson);
        Assert.NotNull(restored);
        Assert.NotNull(restored.ProjectScope);
        Assert.Empty(restored.ProjectScope.Projects);
        Assert.Equal(authority.AgentGovernance!.AuthorityId, restored.AgentGovernance!.AuthorityId);
        Assert.Equal(WorkflowStructureAuthorityFingerprint.Create(authority), WorkflowStructureAuthorityFingerprint.Create(restored));
        editor.Name = "Denied after source revocation";
        await Assert.ThrowsAsync<ProjectStructureAgentException>(() => planner.SavePlanAsync(editor));
        Assert.Equal(saved.Name, (await read.Set<SchedulerPlan>().AsNoTracking().SingleAsync(row => row.Id == saved.Id)).Name);
    }
}
