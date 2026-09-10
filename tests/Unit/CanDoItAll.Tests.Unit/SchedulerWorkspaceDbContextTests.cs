using CanDoItAll.Modules.SchedulerPlanner;
using CanDoItAll.Modules.Workspace;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;

namespace CanDoItAll.Tests.Unit;

public sealed class SchedulerWorkspaceDbContextTests {
    [Fact]
    public void Scheduler_model_contains_only_plans_and_runs_with_the_canonical_cascade_and_legacy_columns() {
        using var context = new SchedulerPlannerDbContext(new DbContextOptionsBuilder<SchedulerPlannerDbContext>()
            .UseInMemoryDatabase($"scheduler-owner-model-{Guid.NewGuid():N}").Options);
        Assert.Equal(new[] { typeof(SchedulerPlan), typeof(SchedulerPlanRun) }.OrderBy(type => type.Name),
            context.Model.GetEntityTypes().Select(entity => entity.ClrType).OrderBy(type => type.Name));
        var plan = context.Model.FindEntityType(typeof(SchedulerPlan))!;
        var run = context.Model.FindEntityType(typeof(SchedulerPlanRun))!;
        var planTable = StoreObjectIdentifier.Table(plan.GetTableName()!, plan.GetSchema());
        var runTable = StoreObjectIdentifier.Table(run.GetTableName()!, run.GetSchema());
        Assert.Equal("AutomationTriggerId", plan.FindProperty(nameof(SchedulerPlan.SchedulerTriggerId))!.GetColumnName(planTable));
        Assert.Equal("AutomationTriggerKey", plan.FindProperty(nameof(SchedulerPlan.SchedulerTriggerKey))!.GetColumnName(planTable));
        Assert.Equal("AutomationEnvelopeId", run.FindProperty(nameof(SchedulerPlanRun.SchedulerFireId))!.GetColumnName(runTable));
        var relationship = Assert.Single(run.GetForeignKeys());
        Assert.Equal(plan, relationship.PrincipalEntityType);
        Assert.Equal(DeleteBehavior.Cascade, relationship.DeleteBehavior);
        Assert.DoesNotContain(context.Model.GetEntityTypes().SelectMany(entity => entity.GetProperties()), property => property.IsConcurrencyToken);
        Assert.Throws<InvalidOperationException>(() => context.Set<ConnectorCommandRecord>().ToList());
    }

    [Fact]
    public void Connector_command_model_keeps_audit_ownership_without_workspace_preferences_or_scheduler_entities() {
        using var context = new WorkspaceConnectorCommandDbContext(new DbContextOptionsBuilder<WorkspaceConnectorCommandDbContext>()
            .UseInMemoryDatabase($"connector-owner-model-{Guid.NewGuid():N}").Options);
        Assert.Equal(new[] { typeof(ConnectorCommandRecord), typeof(ConnectorCommandAuditRecord) }.OrderBy(type => type.Name),
            context.Model.GetEntityTypes().Select(entity => entity.ClrType).OrderBy(type => type.Name));
        var command = context.Model.FindEntityType(typeof(ConnectorCommandRecord))!;
        var audit = context.Model.FindEntityType(typeof(ConnectorCommandAuditRecord))!;
        var relationship = Assert.Single(audit.GetForeignKeys());
        Assert.Equal(command, relationship.PrincipalEntityType);
        Assert.Equal(DeleteBehavior.Cascade, relationship.DeleteBehavior);
        Assert.DoesNotContain(context.Model.GetEntityTypes().SelectMany(entity => entity.GetProperties()), property => property.IsConcurrencyToken);
        Assert.Throws<InvalidOperationException>(() => context.Set<WorkspaceSettings>().ToList());
        Assert.Throws<InvalidOperationException>(() => context.Set<SchedulerPlan>().ToList());
    }
}
