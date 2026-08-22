using CanDoItAll.AgentFramework.Core;
using CanDoItAll.Infrastructure.Persistence;
using CanDoItAll.Modules.AgentFramework;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;

namespace CanDoItAll.Tests.Unit.AgentFramework;

public sealed class WorkflowHitlProductionConstructorGuardTests
{
    [Theory]
    [InlineData(ConstructorDependency.DeduplicatingInner, "inner")]
    [InlineData(ConstructorDependency.DeduplicatingCatalog, "catalog")]
    [InlineData(ConstructorDependency.DeduplicatingStore, "store")]
    [InlineData(ConstructorDependency.CheckpointFactory, "dbContextFactory")]
    [InlineData(ConstructorDependency.CheckpointProtectionProvider, "dataProtectionProvider")]
    [InlineData(ConstructorDependency.CheckpointTimeProvider, "timeProvider")]
    [InlineData(ConstructorDependency.RequestBoundaryFactory, "dbContextFactory")]
    [InlineData(ConstructorDependency.ResponseOperationFactory, "dbContextFactory")]
    [InlineData(ConstructorDependency.ResponseOperationProtectionProvider, "dataProtectionProvider")]
    [InlineData(ConstructorDependency.ResumeBoundaryFactory, "dbContextFactory")]
    [InlineData(ConstructorDependency.ResumeBoundaryProtectionProvider, "dataProtectionProvider")]
    public void ProductionSeamsRejectNullDependencies(
        ConstructorDependency dependency,
        string expectedParameterName)
    {
        var exception = Assert.Throws<ArgumentNullException>(CreateConstruction(dependency));

        Assert.Equal(expectedParameterName, exception.ParamName);
    }

    private static Action CreateConstruction(ConstructorDependency dependency)
    {
        IDbContextFactory<AppDbContext> dbContextFactory = new ThrowingDbContextFactory();
        IDataProtectionProvider dataProtectionProvider = new EphemeralDataProtectionProvider();
        var catalog = new WorkflowExecutorCatalog([]);
        var inner = new WorkflowExecutorInvoker(catalog, []);
        var store = new PersistentWorkflowExecutorInvocationDeduplicationStore(
            dbContextFactory,
            dataProtectionProvider);

        return dependency switch
        {
            ConstructorDependency.DeduplicatingInner =>
                () => _ = new DeduplicatingWorkflowExecutorInvoker(null!, catalog, store),
            ConstructorDependency.DeduplicatingCatalog =>
                () => _ = new DeduplicatingWorkflowExecutorInvoker(inner, null!, store),
            ConstructorDependency.DeduplicatingStore =>
                () => _ = new DeduplicatingWorkflowExecutorInvoker(inner, catalog, null!),
            ConstructorDependency.CheckpointFactory =>
                () => _ = new PersistentWorkflowBackendCheckpointPayloadStore(
                    null!,
                    dataProtectionProvider,
                    TimeProvider.System),
            ConstructorDependency.CheckpointProtectionProvider =>
                () => _ = new PersistentWorkflowBackendCheckpointPayloadStore(
                    dbContextFactory,
                    null!,
                    TimeProvider.System),
            ConstructorDependency.CheckpointTimeProvider =>
                () => _ = new PersistentWorkflowBackendCheckpointPayloadStore(
                    dbContextFactory,
                    dataProtectionProvider,
                    null!),
            ConstructorDependency.RequestBoundaryFactory =>
                () => _ = new PersistentWorkflowExternalRequestBoundaryStore(null!),
            ConstructorDependency.ResponseOperationFactory =>
                () => _ = new PersistentWorkflowExternalResponseOperationStore(
                    null!,
                    dataProtectionProvider),
            ConstructorDependency.ResponseOperationProtectionProvider =>
                () => _ = new PersistentWorkflowExternalResponseOperationStore(
                    dbContextFactory,
                    null!),
            ConstructorDependency.ResumeBoundaryFactory =>
                () => _ = new PersistentWorkflowResumeBoundaryStore(
                    null!,
                    dataProtectionProvider),
            ConstructorDependency.ResumeBoundaryProtectionProvider =>
                () => _ = new PersistentWorkflowResumeBoundaryStore(
                    dbContextFactory,
                    null!),
            _ => throw new ArgumentOutOfRangeException(nameof(dependency), dependency, null)
        };
    }

    public enum ConstructorDependency
    {
        DeduplicatingInner,
        DeduplicatingCatalog,
        DeduplicatingStore,
        CheckpointFactory,
        CheckpointProtectionProvider,
        CheckpointTimeProvider,
        RequestBoundaryFactory,
        ResponseOperationFactory,
        ResponseOperationProtectionProvider,
        ResumeBoundaryFactory,
        ResumeBoundaryProtectionProvider
    }

    private sealed class ThrowingDbContextFactory : IDbContextFactory<AppDbContext>
    {
        public AppDbContext CreateDbContext()
            => throw new InvalidOperationException("The constructor test must not create a database context.");
    }
}
