using CanDoItAll.Modules.CrmHr;
using CanDoItAll.Modules.Projects;
using CanDoItAll.Modules.Workbench;
using Microsoft.EntityFrameworkCore;

namespace CanDoItAll.Tests.Unit;

public sealed class ProjectsWorkbenchDbContextTests {
    [Fact]
    public void Projects_model_contains_exactly_the_five_owner_records() {
        using var context = new ProjectsDbContext(new DbContextOptionsBuilder<ProjectsDbContext>()
            .UseInMemoryDatabase($"projects-model-{Guid.NewGuid():N}").Options);
        Assert.Equal(new[] { typeof(Project), typeof(ProjectPhase), typeof(ProjectOptionSelection), typeof(ProjectHierarchyLink), typeof(ProjectRetirementRecord) }
            .OrderBy(type => type.Name), context.Model.GetEntityTypes().Select(entity => entity.ClrType).OrderBy(type => type.Name));
        Assert.DoesNotContain(context.Model.GetEntityTypes().SelectMany(entity => entity.GetProperties()), property => property.IsConcurrencyToken);
        Assert.Throws<InvalidOperationException>(() => context.Set<CrmAccountConnectionProjectLink>().ToList());
        Assert.Throws<InvalidOperationException>(() => context.Set<ProjectObjectRecord>().ToList());
    }

    [Fact]
    public void Workbench_model_keeps_one_canonical_task_table_and_no_foreign_records() {
        using var context = new WorkbenchDbContext(new DbContextOptionsBuilder<WorkbenchDbContext>()
            .UseInMemoryDatabase($"workbench-model-{Guid.NewGuid():N}").Options);
        Assert.Equal(new[] {
            "Workbench_ProjectObjects", "Workbench_ProjectObjectLinks", "Workbench_ViewStates",
            "Workbench_ProjectProjectionLayouts", "Workbench_ProjectStructureOperationAnalytics",
            "Workbench_ProjectStructureLeases", "Workbench_ProjectNodeBindings", "Workbench_ProjectNodeReferences",
            "Workbench_ProjectNodeLifecycleEvents", "Workbench_ProjectCrossModuleMutations"
        }.Order(), context.Model.GetEntityTypes().Select(entity => entity.GetTableName()).Order());
        Assert.Single(context.Model.GetEntityTypes(), entity => entity.ClrType == typeof(ProjectObjectRecord));
        Assert.All(context.Model.GetEntityTypes().SelectMany(entity => entity.GetForeignKeys()),
            relationship => Assert.Equal(typeof(ProjectObjectRecord), relationship.PrincipalEntityType.ClrType));
        Assert.Throws<InvalidOperationException>(() => context.Set<Project>().ToList());
        Assert.Throws<InvalidOperationException>(() => context.Set<ProjectPartyAssignment>().ToList());
        Assert.DoesNotContain(typeof(IProjectWorkItemAssignmentMutationBridge).GetMethods().SelectMany(method => method.GetParameters()),
            parameter => typeof(DbContext).IsAssignableFrom(parameter.ParameterType));
    }
}
