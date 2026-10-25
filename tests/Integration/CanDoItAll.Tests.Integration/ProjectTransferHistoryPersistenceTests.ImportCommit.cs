using System.Data.Common;
using System.Text;
using CanDoItAll.Infrastructure.ControlPlane;
using CanDoItAll.Infrastructure.Persistence;
using CanDoItAll.Infrastructure.Storage;
using CanDoItAll.Modules.Projects;
using CanDoItAll.Modules.Workbench;
using CanDoItAll.SharedKernel;
using CanDoItAll.Tests.Support;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace CanDoItAll.Tests.Integration;

public sealed partial class ProjectTransferHistoryPersistenceTests {
    public enum ImportFailurePoint { AfterFlushBeforeCommit, AfterServerCommit, ConnectionClose, Publication }

    [Theory]
    [InlineData(ImportFailurePoint.AfterFlushBeforeCommit)]
    [InlineData(ImportFailurePoint.AfterServerCommit)]
    [InlineData(ImportFailurePoint.ConnectionClose)]
    [InlineData(ImportFailurePoint.Publication)]
    public async Task Package_import_compensates_only_before_commit_and_retains_bound_files_after_commit_uncertainty(ImportFailurePoint point) {
        await using var fixture = await TransferFixture.CreateAsync();
        const string contents = "Retained package commit evidence";
        const string coordinationLockSuffix = ".candoitall.lock";
        var projectId = await SeedPackageMediaAsync(fixture, contents);
        var exported = await fixture.PackageService().ExportAllAsync(new() { SourceProfileId = fixture.SourceProfile.Profile.Id });
        Assert.True(exported.IsSuccess, string.Join(" ", exported.Errors.Select(error => error.Message)));
        var package = exported.Value!;
        var probe = new ImportCommitProbe(point, Path.Combine(fixture.TargetProfile.Profile.Storage.WorkspaceRoot,
            "managed-files", "project-media", "imports", package.Manifest.PackageId.ToString("N")));
        await using var source = await fixture.SourceContextAsync();
        await using var target = CreateImportContext(fixture, probe);
        var transfer = new DatabaseTransferOperation(fixture.SourceProfile, fixture.TargetProfile, true);
        var operations = DatabaseTransferTestSupport.For(transfer, source, target,
            inspections: fixture.SourceServices.GetRequiredService<ProjectTransferTargetInspectionRunner>(),
            activation: fixture.SourceServices.GetRequiredService<DatasetActivationService>());
        var guard = new ProjectTransferTargetStateGuard(fixture.SourceServices.GetRequiredService<IEnumerable<IProjectTransferTargetStateParticipant>>(), operations);
        var storage = ActivatorUtilities.CreateInstance<StorageProfileTransferService>(fixture.SourceServices, operations);
        var service = ActivatorUtilities.CreateInstance<ProjectPackageService>(fixture.SourceServices,
            new Profiles(fixture.SourceProfile, fixture.TargetProfile), operations, new ProjectsProfileTransferStore(operations), storage, guard,
            new ImportPublicationLogger(probe));
        var result = await service.ImportAllAsync(new() { PackagePath = package.PackagePath, TargetProfileId = fixture.TargetProfile.Profile.Id });
        Assert.True(probe.ProjectSaveObserved);
        var stagedPath = Assert.Single(probe.StagedFiles, path => !path.EndsWith(coordinationLockSuffix, StringComparison.Ordinal));
        var coordinationPath = stagedPath + coordinationLockSuffix;
        Assert.Equal(new[] { stagedPath, coordinationPath }.Order(StringComparer.Ordinal), probe.StagedFiles.Order(StringComparer.Ordinal));
        await using var verify = await fixture.TargetContextAsync();
        if (point == ImportFailurePoint.AfterFlushBeforeCommit) {
            Assert.True(result.IsFailure);
            Assert.DoesNotContain(result.Errors, error => error.Code == ProjectPackageErrorCodes.ImportOutcomeUnconfirmed);
            Assert.False(await verify.Set<Project>().AnyAsync(row => row.Id == projectId));
            Assert.False(File.Exists(stagedPath));
            Assert.Equal(coordinationPath, Assert.Single(Directory.GetFiles(probe.WorkspaceRoot, "*", SearchOption.AllDirectories)));
            Assert.False(probe.ServerCommitted);
            return;
        }
        Assert.True(probe.ServerCommitted);
        Assert.True(await verify.Set<Project>().AnyAsync(row => row.Id == projectId));
        var binding = await verify.Set<ProjectNodeBindingRecord>().SingleAsync();
        var boundPath = Path.Combine(fixture.TargetProfile.Profile.Storage.WorkspaceRoot,
            binding.MediaRelativePath.Replace('/', Path.DirectorySeparatorChar));
        Assert.Equal(Path.GetFullPath(stagedPath), Path.GetFullPath(boundPath));
        Assert.All(probe.StagedFiles, path => Assert.True(File.Exists(path)));
        Assert.Equal(contents, await File.ReadAllTextAsync(boundPath));
        Assert.True(StorageJson.TryParseReference(binding.StorageObjectReferenceJson, out var reference));
        Assert.True(ProjectManagedStorageProvenancePolicy.HasManagedMarker(reference!));
        Assert.True(ProjectManagedStorageProvenancePolicy.TryValidate(reference!, binding.MediaRelativePath, out var provenanceError), provenanceError);
        if (point == ImportFailurePoint.AfterServerCommit) {
            Assert.True(result.IsFailure);
            var error = Assert.Single(result.Errors);
            Assert.Equal(ProjectPackageErrorCodes.ImportOutcomeUnconfirmed, error.Code);
            Assert.Contains("may have committed", error.Message);
            Assert.Contains("Do not repeat", error.Message);
            Assert.DoesNotContain(ImportCommitFailure.PrivateDiagnostic, error.Message);
        } else {
            Assert.True(result.IsSuccess);
            Assert.Equal(package.Manifest.PackageId, result.Value!.Manifest.PackageId);
            Assert.Equal(package.Manifest.TotalRecordCount, result.Value.RecordsImported);
            Assert.Equal(1, result.Value.StorageFilesImported);
            Assert.Equal(ProjectPackageImportWarning.CleanupIncomplete, result.Value.Warning);
        }
        Assert.All(probe.LoggedExceptions, Assert.Null);
        Assert.DoesNotContain(probe.LogMessages, message => message.Contains(ImportCommitFailure.PrivateDiagnostic, StringComparison.Ordinal));
    }

    [Theory]
    [InlineData(ImportFailurePoint.AfterServerCommit)]
    [InlineData(ImportFailurePoint.ConnectionClose)]
    public async Task Row_import_preserves_acknowledged_counts_and_never_claims_an_unconfirmed_commit(ImportFailurePoint point) {
        await using var fixture = await TransferFixture.CreateAsync();
        var projectId = await SeedImportProjectAsync(fixture);
        var probe = new ImportCommitProbe(point, fixture.TargetProfile.Profile.Storage.WorkspaceRoot);
        await using var source = await fixture.SourceContextAsync();
        await using var target = CreateImportContext(fixture, probe);
        var transfer = new DatabaseTransferOperation(fixture.SourceProfile, fixture.TargetProfile, true);
        var operations = DatabaseTransferTestSupport.For(transfer, source, target,
            inspections: fixture.SourceServices.GetRequiredService<ProjectTransferTargetInspectionRunner>(),
            activation: fixture.SourceServices.GetRequiredService<DatasetActivationService>());
        var guard = new ProjectTransferTargetStateGuard(fixture.SourceServices.GetRequiredService<IEnumerable<IProjectTransferTargetStateParticipant>>(), operations);
        var handler = new ProjectsDatabaseTransferHandler(new Profiles(fixture.SourceProfile, fixture.TargetProfile), guard, operations, new(operations));
        if (point == ImportFailurePoint.ConnectionClose) {
            var exception = await Assert.ThrowsAsync<DatabaseTransferCommittedException>(() => handler.TransferAsync(transfer));
            Assert.Equal(fixture.SourceProfile.Profile.Id, exception.SourceProfileId);
            Assert.Equal(fixture.TargetProfile.Profile.Id, exception.TargetProfileId);
            Assert.True(exception.CommittedResult.Success);
            Assert.Equal(1, exception.CommittedResult.RecordsCopied);
            var import = Assert.IsType<DatabaseImportCommitException>(exception.InnerException);
            Assert.Equal(DatabaseImportCommitKnowledge.Acknowledged, import.Knowledge);
        } else {
            var result = await handler.TransferAsync(transfer);
            Assert.False(result.Success);
            Assert.Equal(0, result.RecordsCopied);
            Assert.Contains("unconfirmed", result.Message);
            Assert.Contains("do not repeat", result.Message);
            Assert.DoesNotContain(ImportCommitFailure.PrivateDiagnostic, result.Message);
        }
        Assert.True(probe.ServerCommitted);
        await using var verify = await fixture.TargetContextAsync();
        Assert.True(await verify.Set<Project>().AnyAsync(row => row.Id == projectId));
    }

    private static AppDbContext CreateImportContext(TransferFixture fixture, ImportCommitProbe probe) {
        var options = new DbContextOptionsBuilder<AppDbContext>(fixture.TargetServices.GetRequiredService<DbContextOptions<AppDbContext>>());
        options.AddInterceptors(new ImportSaveProbe(probe), new ImportTransactionProbe(probe), new ImportCloseProbe(probe));
        return new(options.Options);
    }

    private static async Task<Guid> SeedPackageMediaAsync(TransferFixture fixture, string contents) {
        var projectId = await SeedImportProjectAsync(fixture);
        await fixture.SourceServices.GetRequiredService<ProjectWorkbenchService>().SeedProjectObjectsAsync(projectId,
            [new ProjectObjectSeedRequest(ProjectObjectType.Note, "Package asset", "", "")]);
        var media = await fixture.SourceServices.GetRequiredService<ProjectAssetStorageService>().SaveAsync(projectId,
            ProjectObjectType.Note, string.Empty, new("commit.txt", "text/plain", Convert.ToBase64String(Encoding.UTF8.GetBytes(contents))));
        Assert.NotNull(media);
        await using var source = await fixture.SourceContextAsync();
        var node = await source.Set<ProjectObjectRecord>().SingleAsync(row => row.ProjectId == projectId);
        var binding = await source.Set<ProjectNodeBindingRecord>().SingleAsync(row => row.ProjectObjectId == node.Id);
        binding.MediaRelativePath = media.RelativePath;
        binding.Route = media.Route;
        binding.MediaContentType = media.ContentType;
        binding.MediaOriginalFileName = media.OriginalFileName;
        binding.StorageObjectReferenceJson = media.StorageObjectReferenceJson;
        await source.SaveChangesAsync();
        return projectId;
    }

    private sealed class ImportCommitProbe(ImportFailurePoint point, string workspaceRoot) {
        public ImportFailurePoint Point { get; } = point;
        public string WorkspaceRoot { get; } = workspaceRoot;
        public bool ProjectSaveObserved { get; set; }
        public bool ServerCommitted { get; set; }
        public bool FailureThrown { get; set; }
        public string[] StagedFiles { get; set; } = [];
        public List<Exception?> LoggedExceptions { get; } = [];
        public List<string> LogMessages { get; } = [];
    }

    private sealed class ImportCommitFailure() : IOException(PrivateDiagnostic) {
        public const string PrivateDiagnostic = "Private import diagnostic must not escape.";
    }

    private sealed class ImportSaveProbe(ImportCommitProbe probe) : SaveChangesInterceptor {
        public override ValueTask<InterceptionResult<int>> SavingChangesAsync(DbContextEventData eventData,
            InterceptionResult<int> result, CancellationToken cancellationToken = default) {
            if (eventData.Context!.ChangeTracker.Entries<Project>().Any(entry => entry.State == EntityState.Added)) {
                probe.ProjectSaveObserved = true;
                probe.StagedFiles = Directory.Exists(probe.WorkspaceRoot)
                    ? Directory.GetFiles(probe.WorkspaceRoot, "*", SearchOption.AllDirectories) : [];
            }
            return ValueTask.FromResult(result);
        }

        public override ValueTask<int> SavedChangesAsync(SaveChangesCompletedEventData eventData, int result,
            CancellationToken cancellationToken = default) {
            if (probe.Point == ImportFailurePoint.AfterFlushBeforeCommit && eventData.Context is ProjectsDbContext && probe.ProjectSaveObserved) {
                throw new ImportCommitFailure();
            }
            return ValueTask.FromResult(result);
        }
    }

    private sealed class ImportTransactionProbe(ImportCommitProbe probe) : DbTransactionInterceptor {
        public override Task TransactionCommittedAsync(DbTransaction transaction, TransactionEndEventData eventData,
            CancellationToken cancellationToken = default) {
            if (probe.ProjectSaveObserved) {
                probe.ServerCommitted = true;
                if (probe.Point == ImportFailurePoint.AfterServerCommit && !probe.FailureThrown) {
                    probe.FailureThrown = true;
                    throw new ImportCommitFailure();
                }
            }
            return Task.CompletedTask;
        }
    }

    private sealed class ImportCloseProbe(ImportCommitProbe probe) : DbConnectionInterceptor {
        public override Task ConnectionClosedAsync(DbConnection connection, ConnectionEndEventData eventData) {
            if (probe.ServerCommitted && probe.Point == ImportFailurePoint.ConnectionClose && !probe.FailureThrown) {
                probe.FailureThrown = true;
                throw new ImportCommitFailure();
            }
            return Task.CompletedTask;
        }
    }

    private sealed class ImportPublicationLogger(ImportCommitProbe probe) : ILogger<ProjectPackageService> {
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
        public bool IsEnabled(LogLevel logLevel) => true;
        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter) {
            probe.LoggedExceptions.Add(exception);
            var message = formatter(state, exception);
            probe.LogMessages.Add(message);
            if (probe.Point == ImportFailurePoint.Publication && message.StartsWith("Imported project package ", StringComparison.Ordinal)) {
                throw new ImportCommitFailure();
            }
        }
    }
}
