using CanDoItAll.Infrastructure.Persistence;
using CanDoItAll.Infrastructure.Search;
using CanDoItAll.Infrastructure.Storage;
using CanDoItAll.Modules.AgentFramework;
using CanDoItAll.Modules.Processes;
using CanDoItAll.Modules.Projects;
using CanDoItAll.Modules.SchedulerPlanner;
using CanDoItAll.Modules.Workbench;
using CanDoItAll.Processes.Persistence;
using CanDoItAll.Tests.Support;
using Microsoft.EntityFrameworkCore;

namespace CanDoItAll.Tests.Unit;

public sealed class ProjectTransferTargetStateArchitectureTests
{
    private static readonly SemanticProjectStateRegistration[] SemanticProjectStateRegistry =
    [
        Residue<Project>(
            ProjectTransferTargetStateArea.Projects,
            SemanticProjectStateKind.RootAggregate),
        Residue<SearchDocument>(
            ProjectTransferTargetStateArea.Infrastructure,
            SemanticProjectStateKind.TypedDiscriminator),
        Residue<StorageRoutingRule>(
            ProjectTransferTargetStateArea.Infrastructure,
            SemanticProjectStateKind.TypedScope),
        Residue<WorkflowRunRecordEntity>(
            ProjectTransferTargetStateArea.AgentFramework,
            SemanticProjectStateKind.TypedOrigin),
        Residue<WorkflowLaunchIdempotencyRecordEntity>(
            ProjectTransferTargetStateArea.AgentFramework,
            SemanticProjectStateKind.TypedOrigin),
        Residue<WorkflowUsageObservationRecordEntity>(
            ProjectTransferTargetStateArea.AgentFramework,
            SemanticProjectStateKind.TypedOrigin),
        Residue<ProcessRuntimeStateEntity>(
            ProjectTransferTargetStateArea.Processes,
            SemanticProjectStateKind.ActiveWriter),
        Residue<ProcessRuntimeStepAssignmentEntity>(
            ProjectTransferTargetStateArea.Processes,
            SemanticProjectStateKind.JsonLaunchVariables),
        LockAnchor<ProcessInstancePlanEntity>(
            ProjectTransferTargetStateArea.Processes,
            SemanticProjectStateKind.JsonRuntimeAggregate),
        Residue<SchedulerPlan>(
            ProjectTransferTargetStateArea.SchedulerPlanner,
            SemanticProjectStateKind.DynamicInput),
        Residue<SchedulerPlanRun>(
            ProjectTransferTargetStateArea.SchedulerPlanner,
            SemanticProjectStateKind.DynamicInputChild),
        Residue<ProjectStructureOperationAnalyticsRecord>(
            ProjectTransferTargetStateArea.Workbench,
            SemanticProjectStateKind.TypedScope),
        Residue<ProjectStructureLeaseRecord>(
            ProjectTransferTargetStateArea.Workbench,
            SemanticProjectStateKind.TypedScope)
    ];

    [Fact]
    public void Conventionally_project_related_entities_have_one_lock_owner()
    {
        using var dbContext = CreateDbContext();
        var participants = CreateParticipants();
        _ = new ProjectTransferTargetStateGuard(participants);
        var lockOwners = BuildLockOwners(participants);

        var projectEntities = dbContext.Model.GetEntityTypes()
            .Select(entityType => new ConventionallyProjectRelatedEntity(
                entityType.ClrType,
                entityType.GetProperties()
                    .Select(property => property.Name)
                    .Where(IsProjectIdentityProperty)
                    .Distinct(StringComparer.Ordinal)
                    .Order(StringComparer.Ordinal)
                    .ToArray()))
            .Where(entity => entity.IdentityProperties.Count > 0)
            .GroupBy(entity => entity.EntityType)
            .Select(group => new ConventionallyProjectRelatedEntity(
                group.Key,
                group.SelectMany(entity => entity.IdentityProperties)
                    .Distinct(StringComparer.Ordinal)
                    .Order(StringComparer.Ordinal)
                    .ToArray()))
            .OrderBy(entity => entity.EntityType.FullName, StringComparer.Ordinal)
            .ToArray();

        Assert.NotEmpty(projectEntities);
        foreach (var entity in projectEntities)
        {
            var owners = ResolveOwners(lockOwners, entity.EntityType);
            Assert.True(
                owners.Count == 1,
                $"Mapped entity '{entity.EntityType.FullName}' has project identity properties " +
                $"[{string.Join(", ", entity.IdentityProperties)}] but has {owners.Count} " +
                $"project-transfer lock owners [{DescribeOwners(owners)}]. Expected exactly one.");
        }
    }

    [Fact]
    public void Semantic_project_state_registry_is_mapped_and_lock_owned()
    {
        using var dbContext = CreateDbContext();
        var participants = CreateParticipants();
        var lockOwners = BuildLockOwners(participants);

        var duplicateRegistrations = SemanticProjectStateRegistry
            .GroupBy(registration => registration.EntityType)
            .Where(group => group.Count() > 1)
            .Select(group => group.Key.FullName)
            .ToArray();
        Assert.True(
            duplicateRegistrations.Length == 0,
            $"Semantic project-state entities are registered more than once: {string.Join(", ", duplicateRegistrations)}.");

        foreach (var registration in SemanticProjectStateRegistry)
        {
            Assert.True(
                dbContext.Model.FindEntityType(registration.EntityType) is not null,
                $"Semantic project-state entity '{registration.EntityType.FullName}' is not mapped by AppDbContext.");

            var owners = ResolveOwners(lockOwners, registration.EntityType);
            Assert.True(
                owners.Count == 1 && owners[0] == registration.Area,
                $"Semantic {registration.Participation} entity '{registration.EntityType.FullName}' " +
                $"({registration.Kind}) is registered for {registration.Area} but has lock owners " +
                $"[{DescribeOwners(owners)}]. Expected exactly {registration.Area}.");
        }
    }

    [Fact]
    public void Participant_lock_declarations_are_unique_and_mapped()
    {
        using var dbContext = CreateDbContext();
        var participants = CreateParticipants();
        var declarations = participants
            .SelectMany(participant => participant.EntityTypesToLock.Select(entityType => new
            {
                participant.Area,
                EntityType = entityType
            }))
            .ToArray();

        foreach (var declaration in declarations)
        {
            Assert.True(
                dbContext.Model.FindEntityType(declaration.EntityType) is not null,
                $"Participant '{declaration.Area}' declares unmapped lock entity " +
                $"'{declaration.EntityType.FullName}'.");
        }

        var duplicateOwners = declarations
            .GroupBy(declaration => declaration.EntityType)
            .Where(group => group.Count() > 1)
            .Select(group =>
                $"{group.Key.FullName}: {string.Join(", ", group.Select(item => item.Area))}")
            .ToArray();
        Assert.True(
            duplicateOwners.Length == 0,
            $"Project-transfer lock entities must have one owner. Duplicates: {string.Join("; ", duplicateOwners)}.");
    }

    private static AppDbContext CreateDbContext()
    {
        AppDbContextModelRegistry.ConfigureAssemblies(
            TestApplicationBootstrap.ModuleAssemblies);
        var optionsBuilder = AppDbContextTestOptionsBuilder.Create()
            .UseInMemoryDatabase(
                $"project-transfer-target-state-architecture-{Guid.NewGuid():N}");
        return new AppDbContext(optionsBuilder.Options);
    }

    private static IProjectTransferTargetStateParticipant[] CreateParticipants()
    {
        var assemblies = TestApplicationBootstrap.ModuleAssemblies
            .Append(typeof(IProjectTransferTargetStateParticipant).Assembly)
            .Distinct()
            .ToArray();
        return assemblies
            .SelectMany(assembly => assembly.GetTypes())
            .Where(type =>
                !type.IsAbstract &&
                !type.IsInterface &&
                typeof(IProjectTransferTargetStateParticipant).IsAssignableFrom(type))
            .Select(type => Activator.CreateInstance(type, nonPublic: true))
            .Select(instance => Assert.IsAssignableFrom<IProjectTransferTargetStateParticipant>(instance))
            .OrderBy(participant => participant.Area)
            .ToArray();
    }

    private static Dictionary<Type, ProjectTransferTargetStateArea[]> BuildLockOwners(
        IReadOnlyCollection<IProjectTransferTargetStateParticipant> participants)
        => participants
            .SelectMany(participant => participant.EntityTypesToLock.Select(entityType => new
            {
                participant.Area,
                EntityType = entityType
            }))
            .GroupBy(declaration => declaration.EntityType)
            .ToDictionary(
                group => group.Key,
                group => group.Select(declaration => declaration.Area).ToArray());

    private static IReadOnlyList<ProjectTransferTargetStateArea> ResolveOwners(
        IReadOnlyDictionary<Type, ProjectTransferTargetStateArea[]> lockOwners,
        Type entityType)
        => lockOwners.TryGetValue(entityType, out var owners)
            ? owners
            : [];

    private static bool IsProjectIdentityProperty(string propertyName)
        => propertyName.EndsWith("ProjectId", StringComparison.Ordinal) ||
           propertyName.EndsWith("ProjectObjectId", StringComparison.Ordinal) ||
           propertyName.EndsWith("ProjectNodeId", StringComparison.Ordinal);

    private static string DescribeOwners(
        IReadOnlyCollection<ProjectTransferTargetStateArea> owners)
        => owners.Count == 0
            ? "none"
            : string.Join(", ", owners);

    private static SemanticProjectStateRegistration Residue<TEntity>(
        ProjectTransferTargetStateArea area,
        SemanticProjectStateKind kind)
        => new(
            typeof(TEntity),
            area,
            kind,
            SemanticProjectStateParticipation.ResidueSource);

    private static SemanticProjectStateRegistration LockAnchor<TEntity>(
        ProjectTransferTargetStateArea area,
        SemanticProjectStateKind kind)
        => new(
            typeof(TEntity),
            area,
            kind,
            SemanticProjectStateParticipation.ConcurrencyAnchor);

    private sealed record ConventionallyProjectRelatedEntity(
        Type EntityType,
        IReadOnlyList<string> IdentityProperties);

    private sealed record SemanticProjectStateRegistration(
        Type EntityType,
        ProjectTransferTargetStateArea Area,
        SemanticProjectStateKind Kind,
        SemanticProjectStateParticipation Participation);

    private enum SemanticProjectStateKind
    {
        RootAggregate,
        TypedDiscriminator,
        TypedScope,
        TypedOrigin,
        JsonLaunchVariables,
        JsonRuntimeAggregate,
        DynamicInput,
        DynamicInputChild,
        ActiveWriter
    }

    private enum SemanticProjectStateParticipation
    {
        ResidueSource,
        ConcurrencyAnchor
    }
}
