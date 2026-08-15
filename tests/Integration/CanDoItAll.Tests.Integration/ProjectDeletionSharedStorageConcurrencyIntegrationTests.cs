using CanDoItAll.Infrastructure.Persistence;
using CanDoItAll.Infrastructure.Storage;
using CanDoItAll.Modules.Projects;
using CanDoItAll.Modules.Workbench;
using CanDoItAll.SharedKernel;
using CanDoItAll.Tests.Support;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace CanDoItAll.Tests.Integration.ProjectStructure;

[Trait("Category", "FileSystemPortability")]
public sealed class ProjectDeletionSharedStorageConcurrencyIntegrationTests
{
    [Fact]
    public async Task Concurrent_project_deletions_hold_shared_binding_scope_and_delete_last_shared_object_once()
    {
        await using var application = await TestApplication.CreateAsync(new TestHarnessOptions
        {
            ConfigureServices = services =>
            {
                services.RemoveAll<IStorageDriverRegistry>();
                services.AddSingleton<CountingStorageDriverRegistry>();
                services.AddSingleton<IStorageDriverRegistry>(serviceProvider =>
                    serviceProvider.GetRequiredService<CountingStorageDriverRegistry>());
            }
        });
        await using var setupScope = application.Services.CreateAsyncScope();
        var projects = setupScope.ServiceProvider.GetRequiredService<ProjectsService>();
        var workbench = setupScope.ServiceProvider
            .GetRequiredService<ProjectWorkbenchService>();
        var dbContextFactory = setupScope.ServiceProvider
            .GetRequiredService<IDbContextFactory<AppDbContext>>();
        var workspacePathResolver = setupScope.ServiceProvider
            .GetRequiredService<IWorkspacePathResolver>();
        var firstProjectId = await CreateProjectAsync(projects, "Shared delete first");
        var secondProjectId = await CreateProjectAsync(projects, "Shared delete second");
        var sourceAsset = await CreateImageAsync(
            workbench,
            firstProjectId,
            "Shared deletion asset");
        var secondNode = await workbench.CreateObjectAsync(
            secondProjectId,
            new ProjectObjectCreateRequest(
                ProjectObjectType.Note,
                "Shared asset consumer",
                string.Empty,
                "This binding deliberately shares the managed object.",
                $"project:{secondProjectId:D}",
                200,
                200));
        await ShareBindingAsync(
            dbContextFactory,
            firstProjectId,
            sourceAsset.Id,
            secondProjectId,
            secondNode.Id);
        var physicalPath = Path.Combine(
            workspacePathResolver.ResolveWorkspaceRoot(),
            sourceAsset.MediaRelativePath.Replace('/', Path.DirectorySeparatorChar));
        Assert.True(File.Exists(physicalPath));

        Task<ProjectDeletionResult> firstDeletion;
        Task<ProjectDeletionResult> secondDeletion;
        await using var firstScope = application.Services.CreateAsyncScope();
        await using var secondScope = application.Services.CreateAsyncScope();
        await using (var lockContext = await dbContextFactory.CreateDbContextAsync())
        await using (var bindingScope = await SerializableMutationScope.BeginAsync(
                         lockContext,
                         ProjectStructureSerializableMutationScope
                             .ManagedStorageBindingScopeKey,
                         CancellationToken.None))
        {
            firstDeletion = firstScope.ServiceProvider
                .GetRequiredService<ProjectsService>()
                .DeleteAsync(firstProjectId);
            secondDeletion = secondScope.ServiceProvider
                .GetRequiredService<ProjectsService>()
                .DeleteAsync(secondProjectId);

            await AssertRemainBlockedAsync(
                [firstDeletion, secondDeletion],
                TimeSpan.FromMilliseconds(300));
            await bindingScope.CommitAsync(CancellationToken.None);
        }

        await Task.WhenAll(firstDeletion, secondDeletion)
            .WaitAsync(TimeSpan.FromSeconds(20));

        Assert.False(File.Exists(physicalPath));
        Assert.Equal(
            1,
            setupScope.ServiceProvider
                .GetRequiredService<CountingStorageDriverRegistry>()
                .FileSystemDeleteCalls);
        await using var verificationContext = await dbContextFactory.CreateDbContextAsync();
        Assert.False(await verificationContext.Set<Project>()
            .AnyAsync(project =>
                project.Id == firstProjectId ||
                project.Id == secondProjectId));
        Assert.Empty(await verificationContext.Set<ProjectNodeBindingRecord>()
            .Where(binding =>
                binding.MediaRelativePath == sourceAsset.MediaRelativePath)
            .ToListAsync());
    }

    private static async Task ShareBindingAsync(
        IDbContextFactory<AppDbContext> dbContextFactory,
        Guid sourceProjectId,
        string sourceNodeKey,
        Guid targetProjectId,
        string targetNodeKey)
    {
        await using var dbContext = await dbContextFactory.CreateDbContextAsync();
        await using var bindingMutationScope = await SerializableMutationScope.BeginAsync(
            dbContext,
            ProjectStructureSerializableMutationScope.ManagedStorageBindingScopeKey,
            CancellationToken.None);
        var sourceObjectId = await dbContext.Set<ProjectObjectRecord>()
            .Where(item =>
                item.ProjectId == sourceProjectId &&
                item.NodeKey == sourceNodeKey)
            .Select(item => item.Id)
            .SingleAsync();
        var targetObjectId = await dbContext.Set<ProjectObjectRecord>()
            .Where(item =>
                item.ProjectId == targetProjectId &&
                item.NodeKey == targetNodeKey)
            .Select(item => item.Id)
            .SingleAsync();
        var source = await dbContext.Set<ProjectNodeBindingRecord>()
            .AsNoTracking()
            .SingleAsync(binding => binding.ProjectObjectId == sourceObjectId);
        var target = await dbContext.Set<ProjectNodeBindingRecord>()
            .SingleAsync(binding => binding.ProjectObjectId == targetObjectId);
        target.Route = source.Route;
        target.ExternalArtifactKind = source.ExternalArtifactKind;
        target.ExternalArtifactId = source.ExternalArtifactId;
        target.MediaRelativePath = source.MediaRelativePath;
        target.MediaContentType = source.MediaContentType;
        target.MediaOriginalFileName = source.MediaOriginalFileName;
        target.StorageObjectReferenceJson = source.StorageObjectReferenceJson;
        target.UpdatedAtUtc = DateTimeOffset.UtcNow;
        await dbContext.SaveChangesAsync();
        await bindingMutationScope.CommitAsync(CancellationToken.None);
    }

    private static async Task<Guid> CreateProjectAsync(
        ProjectsService projects,
        string name)
    {
        var result = await projects.SaveAsync(new ProjectEditorModel
        {
            Name = name,
            Objective = "Validate shared object deletion coordination.",
            CurrentPhase = "Validation"
        });
        Assert.True(result.IsSuccess);
        return result.Value;
    }

    private static Task<ProjectStructureNode> CreateImageAsync(
        ProjectWorkbenchService workbench,
        Guid projectId,
        string title)
        => workbench.CreateObjectAsync(
            projectId,
            new ProjectObjectCreateRequest(
                ProjectObjectType.ImageAsset,
                title,
                string.Empty,
                "Managed image for shared deletion coverage.",
                $"project:{projectId:D}",
                240,
                180,
                Media: new ProjectObjectMediaPayload(
                    "pixel.png",
                    "image/png",
                    "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mNk+A8AAQUBAScY42YAAAAASUVORK5CYII=")));

    private static async Task AssertRemainBlockedAsync(
        IReadOnlyCollection<Task> tasks,
        TimeSpan observationWindow)
    {
        var deadline = TimeProvider.System.GetTimestamp() +
            (long)(observationWindow.TotalSeconds *
                   TimeProvider.System.TimestampFrequency);
        do
        {
            await Task.Delay(TimeSpan.FromMilliseconds(25));
            Assert.All(tasks, task => Assert.False(task.IsCompleted));
        }
        while (TimeProvider.System.GetTimestamp() < deadline);
    }

    private sealed class CountingStorageDriverRegistry : IStorageDriverRegistry
    {
        private readonly IReadOnlyDictionary<StorageProviderKind, IStorageDriver> drivers;
        private int fileSystemDeleteCalls;

        public CountingStorageDriverRegistry(
            IEnumerable<IStorageDriver> registeredDrivers)
        {
            drivers = registeredDrivers.ToDictionary(
                driver => driver.ProviderKind,
                driver => driver.ProviderKind == StorageProviderKind.FileSystem
                    ? new CountingDeleteStorageDriver(
                        driver,
                        () => Interlocked.Increment(ref fileSystemDeleteCalls))
                    : driver);
        }

        public int FileSystemDeleteCalls => Volatile.Read(ref fileSystemDeleteCalls);

        public IReadOnlyCollection<StorageProviderKind> RegisteredKinds
            => drivers.Keys.ToArray();

        public bool TryResolve(
            StorageProviderKind providerKind,
            out IStorageDriver driver)
            => drivers.TryGetValue(providerKind, out driver!);

        public IStorageDriver Resolve(StorageProviderKind providerKind)
            => drivers.TryGetValue(providerKind, out var driver)
                ? driver
                : throw new InvalidOperationException(
                    $"Storage provider '{providerKind}' is not registered.");
    }

    private sealed class CountingDeleteStorageDriver(
        IStorageDriver inner,
        Action onDelete) : IStorageDriver
    {
        public StorageProviderKind ProviderKind => inner.ProviderKind;

        public StorageCapability SupportedCapabilities
            => inner.SupportedCapabilities;

        public Task<StorageConnectionTestResult> TestConnectionAsync(
            StorageCatalogRecord storage,
            string? secretValue,
            CancellationToken cancellationToken = default)
            => inner.TestConnectionAsync(storage, secretValue, cancellationToken);

        public Task<StorageWriteResult> SaveAsync(
            StorageCatalogRecord storage,
            StorageWriteRequest request,
            CancellationToken cancellationToken = default)
            => inner.SaveAsync(storage, request, cancellationToken);

        public Task<Stream> OpenReadAsync(
            StorageCatalogRecord storage,
            StorageObjectReference reference,
            CancellationToken cancellationToken = default)
            => inner.OpenReadAsync(storage, reference, cancellationToken);

        public async Task DeleteAsync(
            StorageCatalogRecord storage,
            StorageObjectReference reference,
            CancellationToken cancellationToken = default)
        {
            onDelete();
            await inner.DeleteAsync(storage, reference, cancellationToken);
        }
    }
}
