using CanDoItAll.Infrastructure.FileSystem;
using System.Data.Common;
using System.Text;
using System.Security.Cryptography;
using System.Text.Json;
using CanDoItAll.Infrastructure;
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
using Microsoft.Extensions.Logging.Abstractions;

namespace CanDoItAll.Tests.Integration;

public sealed class StorageStablePlacementPersistenceTests {
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Exact_filesystem_receipt_recovers_driver_or_commit_lost_acknowledgement(bool failReceiptCommit) {
        await using var app = await TestApplication.CreateAsync();
        var storage = FileStorage(app.RootPath);
        var paths = new FileSystemStoragePathPolicy(new Paths(app.RootPath));
        var failure = new ArgumentException("Injected exact placement acknowledgment failure.");
        var driver = new ControlledDriver(new FileSystemStorageDriver(paths)) {
            AfterWriteFailure = failReceiptCommit ? null : failure
        };
        var fault = new ReceiptCommitFault(failure);
        var owner = Create(app, storage, driver, failReceiptCommit ? fault : null);
        var id = new StoragePlacementIntentId(Guid.NewGuid());
        var request = Request(storage, id);
        var first = await owner.PlaceAsync(id, request);
        Assert.Equal(StorageStablePlacementState.Completed, first.State);
        Assert.Same(failure, first.ObservationException);
        Assert.Equal(failReceiptCommit, fault.Fired);
        Assert.NotNull(first.Receipt);
        Assert.Equal(id.Value, first.Receipt.WriteResult.Reference.PlacementIntentId);
        Assert.Equal("original content", await File.ReadAllTextAsync(first.Receipt.Location));
        var replay = await Create(app, storage, driver).PlaceAsync(id, request);
        Assert.Equal(first.Receipt, replay.Receipt);
        Assert.Equal(1, driver.Writes);
        await using var db = await Factory(app).CreateDbContextAsync();
        Assert.Single(await db.Set<StoragePlacementIntentRecord>().Where(row => row.Id == id.Value).ToListAsync());
    }

    [Fact]
    public async Task Concurrent_same_intent_does_not_resubmit_an_inflight_write_and_changed_content_conflicts() {
        await using var app = await TestApplication.CreateAsync();
        var storage = FileStorage(app.RootPath);
        var driver = new ControlledDriver(new FileSystemStorageDriver(new(new Paths(app.RootPath)))) { BlockWrite = true };
        var owner = Create(app, storage, driver);
        var id = new StoragePlacementIntentId(Guid.NewGuid());
        var request = Request(storage, id);
        var first = owner.PlaceAsync(id, request);
        await driver.Entered.Task.WaitAsync(TimeSpan.FromSeconds(10));
        try {
            var second = await Create(app, storage, driver).PlaceAsync(id, request);
            Assert.Equal(StorageStablePlacementState.Uncertain, second.State);
            Assert.Null(second.Receipt);
            Assert.Equal(1, driver.Writes);
            var error = await Assert.ThrowsAsync<StorageStablePlacementConflictException>(() => owner.PlaceAsync(id,
                request with { Content = "changed"u8.ToArray() }));
            Assert.Equal(id, error.IntentId);
        } finally {
            driver.Release.TrySetResult();
        }
        Assert.Equal(StorageStablePlacementState.Completed, (await first).State);
        Assert.Equal(StorageStablePlacementState.Completed, (await owner.PlaceAsync(id, request)).State);
        Assert.Equal(1, driver.Writes);
    }

    [Fact]
    public async Task Completed_replay_preserves_human_bytes_and_deletion_tombstone() {
        await using var app = await TestApplication.CreateAsync();
        var storage = FileStorage(app.RootPath);
        var paths = new FileSystemStoragePathPolicy(new Paths(app.RootPath));
        var driver = new ControlledDriver(new FileSystemStorageDriver(paths));
        var owner = Create(app, storage, driver);
        var id = new StoragePlacementIntentId(Guid.NewGuid());
        var request = Request(storage, id);
        var first = await owner.PlaceAsync(id, request);
        Assert.NotNull(first.Receipt);
        await File.WriteAllTextAsync(first.Receipt.Location, "human edited bytes of a different length");
        Assert.Equal(first.Receipt, (await owner.PlaceAsync(id, request)).Receipt);
        Assert.Equal("human edited bytes of a different length", await File.ReadAllTextAsync(first.Receipt.Location));
        var reference = first.Receipt.WriteResult.Reference with { ContentLength = 39 };
        Assert.True(await owner.MarkDeletionAsync(reference));
        await driver.DeleteAsync(storage.ToDriverInput(), reference);
        var replay = await Create(app, storage, driver).PlaceAsync(id, request);
        Assert.Equal(StorageStablePlacementState.Deleted, replay.State);
        Assert.Equal(first.Receipt, replay.Receipt);
        Assert.False(File.Exists(first.Receipt.Location));
        Assert.Equal(1, driver.Writes);
        Assert.Equal(1, driver.Deletes);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Ftp_lost_acknowledgement_requires_external_termination_verification_and_exact_readback(bool contentChanged) {
        await using var app = await TestApplication.CreateAsync();
        var storage = RemoteStorage(StorageProviderKind.Ftp);
        var transport = new FtpTransport { FailAfterUpload = true };
        var driver = new FtpStorageDriver(new NoSecrets(), transport, NullLogger<FtpStorageDriver>.Instance);
        var owner = Create(app, storage, driver);
        var id = new StoragePlacementIntentId(Guid.NewGuid());
        var request = Request(storage, id);
        var pending = await owner.PlaceAsync(id, request);
        Assert.Equal(StorageStablePlacementState.Uncertain, pending.State);
        Assert.NotNull(pending.ObservationException);
        Assert.Equal(StorageStablePlacementState.Uncertain, (await owner.PlaceAsync(id, request)).State);
        Assert.Equal(1, transport.Uploads);
        Assert.Equal(0, transport.Reads);
        if (contentChanged) {
            transport.Bytes = "human content"u8.ToArray();
        }
        var recovered = await owner.RecordOperatorVerifiedExternalDispatchTerminationAsync(id);
        Assert.Equal(contentChanged ? StorageStablePlacementState.Conflict : StorageStablePlacementState.Completed, recovered.State);
        Assert.Equal(contentChanged ? "human content" : "original content", Encoding.UTF8.GetString(transport.Bytes));
        Assert.Equal(1, transport.Uploads);
        Assert.Equal(1, transport.Reads);
        Assert.Equal(recovered.State, (await owner.PlaceAsync(id, request)).State);
        Assert.Equal(1, transport.Uploads);
    }

    [Fact]
    public async Task Ftp_acknowledged_first_execution_and_ipfs_fixed_cid_recovery_preserve_supported_writes() {
        await using var app = await TestApplication.CreateAsync();
        var ftp = RemoteStorage(StorageProviderKind.Ftp);
        var ftpTransport = new FtpTransport();
        var ftpDriver = new FtpStorageDriver(new NoSecrets(), ftpTransport, NullLogger<FtpStorageDriver>.Instance);
        var ftpId = new StoragePlacementIntentId(Guid.NewGuid());
        Assert.Equal(StorageStablePlacementState.Completed, (await Create(app, ftp, ftpDriver).PlaceAsync(ftpId, Request(ftp, ftpId))).State);
        Assert.Equal(1, ftpTransport.Uploads);
        Assert.Equal(1, ftpTransport.Reads);

        var ipfs = RemoteStorage(StorageProviderKind.Ipfs);
        ipfs.ConfigJson = "{\"pinOnUpload\":true}";
        var ipfsTransport = new IpfsTransport();
        var ipfsDriver = new IpfsStorageDriver(NullLogger<IpfsStorageDriver>.Instance, new NoSecrets(), ipfsTransport);
        var id = new StoragePlacementIntentId(Guid.NewGuid());
        var request = Request(ipfs, id);
        var owner = Create(app, ipfs, ipfsDriver);
        var first = await owner.PlaceAsync(id, request);
        Assert.Equal(StorageStablePlacementState.Completed, first.State);
        Assert.Same(ipfsTransport.Failure, first.ObservationException);
        Assert.Equal("bafy-fixed-test-cid", first.Receipt!.WriteResult.Reference.Locator);
        Assert.Equal(first.Receipt, (await owner.PlaceAsync(id, request)).Receipt);
        Assert.Equal(new[] { IpfsStableAddMode.ComputeOnly, IpfsStableAddMode.Store }, ipfsTransport.Adds);
        Assert.Equal(1, ipfsTransport.Pins);
    }

    [Fact]
    public async Task Restart_and_other_profile_keep_original_identity_and_receipt_observers_run_after_commit() {
        await using var environment = CanDoItAllTestEnvironment.Create("stable-storage-restart");
        var original = environment.CreatePostgreSqlProfile("original");
        var other = environment.CreatePostgreSqlProfile("other");
        StorageStablePlacementReceipt receipt;
        StorageCatalogRecord storage;
        StoragePlacementRequest request;
        var id = new StoragePlacementIntentId(Guid.NewGuid());
        var observerFailure = new InvalidOperationException("Injected post-receipt observer failure.");
        await using (var app = await TestApplication.CreateAsync(new() { TestEnvironment = environment, ActiveProfile = original })) {
            storage = FileStorage(app.RootPath);
            request = Request(storage, id);
            var observer = new ObserveCommittedReceipt(Factory(app), observerFailure);
            var owner = Create(app, storage, new FileSystemStorageDriver(new(new Paths(app.RootPath))), observer: observer);
            var outcome = await owner.PlaceAsync(id, request);
            Assert.Equal(StorageStablePlacementState.Completed, outcome.State);
            Assert.Same(observerFailure, outcome.ObservationException);
            Assert.Equal(1, observer.Calls);
            receipt = Assert.IsType<StorageStablePlacementReceipt>(outcome.Receipt);
        }
        await using var restarted = await TestApplication.CreateAsync(new() { TestEnvironment = environment, ActiveProfile = original });
        var driver = new ControlledDriver(new FileSystemStorageDriver(new(new Paths(restarted.RootPath))));
        Assert.Equal(receipt, (await Create(restarted, storage, driver).PlaceAsync(id, request)).Receipt);
        Assert.Equal(0, driver.Writes);
        await using var otherApp = await TestApplication.CreateAsync(new() { TestEnvironment = environment, ActiveProfile = other });
        Assert.Null(await Create(otherApp, storage, driver).FindAsync(id));
        Assert.Equal(receipt, (await Create(restarted, storage, driver).FindAsync(id))!.Receipt);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Package_ipfs_adoption_preserves_source_receipt_and_unrelated_target_intent_across_commit_or_rollback(bool commit) {
        await using var environment = CanDoItAllTestEnvironment.Create("ipfs-package-adoption");
        var sourceProfile = environment.CreatePostgreSqlProfile("source");
        var targetProfile = environment.CreatePostgreSqlProfile("target");
        await using var source = await TestApplication.CreateAsync(new() { TestEnvironment = environment, ActiveProfile = sourceProfile });
        var sourceStorage = RemoteStorage(StorageProviderKind.Ipfs);
        sourceStorage.ConfigJson = "{\"pinOnUpload\":true}";
        var transport = new IpfsTransport();
        var driver = new IpfsStorageDriver(NullLogger<IpfsStorageDriver>.Instance, new NoSecrets(), transport);
        var intent = new StoragePlacementIntentId(Guid.NewGuid());
        var sourceOwner = Create(source, sourceStorage, driver);
        var placed = await sourceOwner.PlaceAsync(intent, Request(sourceStorage, intent));
        Assert.Equal(StorageStablePlacementState.Completed, placed.State);
        var reference = placed.Receipt!.WriteResult.Reference;
        string sourceEvidence;
        await using (var context = await Factory(source).CreateDbContextAsync()) {
            sourceEvidence = JsonSerializer.Serialize(await context.Set<StoragePlacementIntentRecord>().AsNoTracking().SingleAsync());
        }
        var targetStorage = RemoteStorage(StorageProviderKind.Ipfs);
        var project = new Project { Name = "Adopted immutable asset", CurrentPhase = "Execution" };
        var node = new ProjectObjectRecord { ProjectId = project.Id, NodeKey = "node:immutable-import", Title = "Imported asset" };
        var binding = new ProjectNodeBindingRecord { ProjectObjectId = node.Id, MediaContentType = reference.ContentType,
            MediaOriginalFileName = reference.DisplayName, StorageObjectReferenceJson = StorageJson.SerializeReference(reference) };
        var dataSet = new ProjectTransferDataSet { Projects = [new ProjectTransferProject { Id = project.Id, Name = project.Name, CurrentPhase = project.CurrentPhase }], Objects = [node], NodeBindings = [binding] };
        var manifest = new ProjectPackageManifest {
            PackageId = Guid.NewGuid(), SourceProfileId = source.Services.GetRequiredService<ICanonicalRuntimeDatabase>().Profile.Profile.Id,
            ImmutableStorageReferences = [new() {
                SourceStorageId = reference.StorageId, ProviderKind = reference.ProviderKind, LocatorKind = reference.LocatorKind,
                Locator = reference.Locator, ContentType = reference.ContentType, OriginalFileName = reference.DisplayName,
                Length = reference.ContentLength!.Value, Sha256 = Convert.ToHexStringLower(SHA256.HashData("original content"u8))
            }]
        };
        string targetEvidence;
        await using (var target = await TestApplication.CreateAsync(new() { TestEnvironment = environment, ActiveProfile = targetProfile })) {
            await using (var context = await Factory(target).CreateDbContextAsync()) {
                context.Add(new StoragePlacementIntentRecord { Id = intent.Value, StorageId = targetStorage.Id,
                    State = StorageStablePlacementState.Prepared, RequestFingerprint = new string('A', 64),
                    PlanJson = "{\"unrelated\":true}", CreatedAtUtc = DateTimeOffset.UtcNow, UpdatedAtUtc = DateTimeOffset.UtcNow });
                await context.SaveChangesAsync();
                targetEvidence = JsonSerializer.Serialize(await context.Set<StoragePlacementIntentRecord>().AsNoTracking().SingleAsync());
            }
            var identity = new ProjectManagedStoragePhysicalIdentityPolicy(new(new Paths(target.RootPath)), target.Services.GetRequiredService<IPhysicalFileSystemPathPolicyFactory>());
            var filesystem = FileStorage(target.RootPath);
            await using (var catalog = await Factory(target).CreateDbContextAsync()) {
                catalog.AddRange(targetStorage, filesystem);
                await catalog.SaveChangesAsync();
            }
            var storageTransfers = new StorageProfileTransferService(DatabaseTransferTestSupport.Create(),
                new StorageDriverRegistry([driver, new FileSystemStorageDriver(new(new Paths(target.RootPath)))]), new ProjectStorageTransferProvenancePolicy(identity),
                new FileSystemStoragePathPolicy(new Paths(target.RootPath)), new SystemClock(), NullLogger<StoragePlacementService>.Instance);
            var importer = new ProjectPackageStorageImporter(storageTransfers, target.Services.GetRequiredService<IPhysicalFileSystemPathPolicyFactory>(), NullLogger<ProjectPackageService>.Instance);
            var preflight = await importer.PreflightImportAsync(target.RootPath, manifest, dataSet, default);
            var staged = importer.CreateStagingJournal();
            await storageTransfers.WithTargetAsync(target.Services.GetRequiredService<ICanonicalRuntimeDatabase>().Profile, async (session, token) => {
                Assert.Equal(0, await importer.RewriteStorageBindingsAsync(target.RootPath, manifest, dataSet, preflight, session, staged, token));
                return true;
            });
            Assert.Empty(staged);
            var adopted = Assert.IsType<StorageObjectReference>(StorageJson.ParseReference(binding.StorageObjectReferenceJson));
            Assert.True(ProjectManagedStorageProvenancePolicy.TryValidate(adopted, null, out var error), error);
            Assert.Null(adopted.PlacementIntentId);
            Assert.Equal(reference, adopted.ImportedHistory!.SourceReference);
            Assert.True(await Create(target, targetStorage, driver).MarkDeletionAsync(adopted));
            await using var database = await target.Services.GetRequiredService<IDbContextFactory<AppDbContext>>().CreateDbContextAsync();
            await using var transaction = await database.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable);
            database.AddRange(project, node, binding);
            await database.SaveChangesAsync();
            if (commit) {
                await transaction.CommitAsync();
            } else {
                await transaction.RollbackAsync();
            }
        }
        await using var restarted = await TestApplication.CreateAsync(new() { TestEnvironment = environment, ActiveProfile = targetProfile });
        await using (var database = await restarted.Services.GetRequiredService<IDbContextFactory<AppDbContext>>().CreateDbContextAsync()) {
            var saved = await database.Set<ProjectNodeBindingRecord>().AsNoTracking().SingleOrDefaultAsync(row => row.Id == binding.Id);
            if (commit) {
                Assert.NotNull(saved);
                var adopted = Assert.IsType<StorageObjectReference>(StorageJson.ParseReference(saved.StorageObjectReferenceJson));
                Assert.Equal(targetStorage.Id, adopted.StorageId);
                Assert.Null(adopted.PlacementIntentId);
                Assert.Equal(reference, adopted.ImportedHistory!.SourceReference);
                Assert.True(await Create(restarted, targetStorage, driver).MarkDeletionAsync(adopted));
                await using var bytes = await driver.OpenReadAsync(targetStorage.ToDriverInput(), adopted);
                using var reader = new StreamReader(bytes);
                Assert.Equal("original content", await reader.ReadToEndAsync());
            } else {
                Assert.Null(saved);
                Assert.False(await database.Set<Project>().AnyAsync(row => row.Id == project.Id));
            }
        }
        await using (var context = await Factory(restarted).CreateDbContextAsync()) {
            Assert.Equal(targetEvidence, JsonSerializer.Serialize(await context.Set<StoragePlacementIntentRecord>().AsNoTracking().SingleAsync()));
        }
        await using (var context = await Factory(source).CreateDbContextAsync()) {
            Assert.Equal(sourceEvidence, JsonSerializer.Serialize(await context.Set<StoragePlacementIntentRecord>().AsNoTracking().SingleAsync()));
        }
        Assert.Equal(new[] { IpfsStableAddMode.ComputeOnly, IpfsStableAddMode.Store }, transport.Adds);
        Assert.Equal(1, transport.Pins);
    }

    private static StorageStablePlacementService Create(TestApplication app, StorageCatalogRecord storage, IStorageDriver driver,
        IInterceptor? interceptor = null, IStoragePlacementReceiptObserver? observer = null)
        => new(Factory(app, interceptor), new Catalog(storage), new NoRouting(), new Registry(driver), new Access(),
            new(new Paths(app.RootPath)), TimeProvider.System, observer is null ? [] : [observer]);

    private static OwnerFactory Factory(TestApplication app, IInterceptor? interceptor = null) {
        var options = new DbContextOptionsBuilder<StorageDbContext>();
        AppDbContextOptionsConfigurator.Configure(options, app.Services.GetRequiredService<ICanonicalRuntimeDatabase>().Profile);
        if (interceptor is not null) {
            options.AddInterceptors(interceptor);
        }
        return new(options.Options);
    }

    private static StorageCatalogRecord FileStorage(string root) {
        var storage = RemoteStorage(StorageProviderKind.FileSystem);
        storage.EndpointOrRoot = root;
        StorageCatalogHostBindingPolicy.BindCurrent(storage, root, DateTimeOffset.UtcNow);
        return storage;
    }

    private static StorageCatalogRecord RemoteStorage(StorageProviderKind kind) => new() {
        Id = Guid.NewGuid(), Name = $"Isolated {kind} placement fixture", ProviderKind = kind, IsEnabled = true,
        EndpointOrRoot = "https://storage.example.test/api/v0/", HealthStatus = StorageHealthStatus.Healthy,
        CapabilityMask = StorageCapability.Read | StorageCapability.Write | StorageCapability.Delete | StorageCapability.Download | StorageCapability.InlinePreview
    };

    private static StoragePlacementRequest Request(StorageCatalogRecord storage, StoragePlacementIntentId id)
        => new("artifact.txt", "text/plain", "original content"u8.ToArray(), StorageUsagePurpose.ProjectAsset,
            StorageContentKind.Text, Guid.NewGuid(), RelativePathHint: $"managed-files/project-media/files/{id.Value:N}/artifact.txt",
            PreferredStorageId: storage.Id);

    private sealed class OwnerFactory(DbContextOptions<StorageDbContext> options) : IDbContextFactory<StorageDbContext> {
        public StorageDbContext CreateDbContext() => new(options);
        public Task<StorageDbContext> CreateDbContextAsync(CancellationToken cancellationToken = default) => Task.FromResult(CreateDbContext());
    }

    private sealed class ReceiptCommitFault(Exception failure) : DbTransactionInterceptor {
        public bool Fired { get; private set; }
        public override Task TransactionCommittedAsync(DbTransaction transaction, TransactionEndEventData eventData,
            CancellationToken cancellationToken = default) {
            if (!Fired && eventData.Context?.ChangeTracker.Entries<StoragePlacementIntentRecord>()
                .Any(entry => entry.Entity.ReceiptJson.Length > 0) == true) {
                Fired = true;
                throw failure;
            }
            return Task.CompletedTask;
        }
    }

    private sealed class ObserveCommittedReceipt(OwnerFactory factory, Exception failure) : IStoragePlacementReceiptObserver {
        public int Calls { get; private set; }
        public async Task ObserveAsync(StorageStablePlacementReceipt receipt, CancellationToken cancellationToken) {
            await using var db = await factory.CreateDbContextAsync(cancellationToken);
            await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
            Assert.Equal(StorageStablePlacementState.Completed,
                (await db.Set<StoragePlacementIntentRecord>().SingleAsync(row => row.Id == receipt.IntentId.Value, cancellationToken)).State);
            await transaction.RollbackAsync(cancellationToken);
            Calls++;
            throw failure;
        }
    }

    private sealed class ControlledDriver(FileSystemStorageDriver inner) : IStorageDriver, IStorageStablePlacementDriver {
        public int Writes { get; private set; }
        public int Deletes { get; private set; }
        public bool BlockWrite { get; init; }
        public Exception? AfterWriteFailure { get; init; }
        public TaskCompletionSource Entered { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource Release { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public StorageProviderKind ProviderKind => inner.ProviderKind;
        public StorageCapability SupportedCapabilities => inner.SupportedCapabilities;
        public bool CanRecoverWithoutWriteAcknowledgement => true;
        public Task<StorageConnectionTestResult> TestConnectionAsync(StorageDriverInput storage, string? secretValue, CancellationToken cancellationToken = default)
            => inner.TestConnectionAsync(storage, secretValue, cancellationToken);
        public Task<StorageWriteResult> SaveAsync(StorageDriverInput storage, StorageWriteRequest request, CancellationToken cancellationToken = default)
            => throw new InvalidOperationException("Stable placement called ordinary Save.");
        public Task<Stream> OpenReadAsync(StorageDriverInput storage, StorageObjectReference reference, CancellationToken cancellationToken = default)
            => inner.OpenReadAsync(storage, reference, cancellationToken);
        public async Task DeleteAsync(StorageDriverInput storage, StorageObjectReference reference, CancellationToken cancellationToken = default) {
            Deletes++;
            await inner.DeleteAsync(storage, reference, cancellationToken);
        }
        public Task<StorageObjectReference> PrepareStableTargetAsync(StorageDriverInput storage, StoragePlacementIntentId id,
            StorageWriteRequest request, CancellationToken cancellationToken)
            => ((IStorageStablePlacementDriver)inner).PrepareStableTargetAsync(storage, id, request, cancellationToken);
        public async Task<StorageWriteResult> WriteStableTargetAsync(StorageDriverInput storage, StorageObjectReference target,
            StorageWriteRequest request, CancellationToken cancellationToken) {
            Writes++;
            Entered.TrySetResult();
            if (BlockWrite) {
                await Release.Task.WaitAsync(cancellationToken);
            }
            var result = await ((IStorageStablePlacementDriver)inner).WriteStableTargetAsync(storage, target, request, cancellationToken);
            if (AfterWriteFailure is not null) {
                throw AfterWriteFailure;
            }
            return result;
        }
        public Task CompleteStableTargetAsync(StorageDriverInput storage, StorageObjectReference target, CancellationToken cancellationToken)
            => Task.CompletedTask;
    }

    private sealed class FtpTransport : IFtpStorageTransport {
        public bool FailAfterUpload { get; init; }
        public int Uploads { get; private set; }
        public int Reads { get; private set; }
        public byte[] Bytes { get; set; } = [];
        public Task<string?> TestConnectionAsync(StorageDriverInput storage, string? password, CancellationToken cancellationToken) => Task.FromResult<string?>(null);
        public Task UploadAsync(StorageDriverInput storage, string? password, string path, ReadOnlyMemory<byte> content, CancellationToken cancellationToken) {
            Uploads++;
            Bytes = content.ToArray();
            if (FailAfterUpload) {
                throw new IOException("Injected FTP acknowledgment loss.");
            }
            return Task.CompletedTask;
        }
        public Task<Stream> OpenReadAsync(StorageDriverInput storage, string? password, string path, CancellationToken cancellationToken) {
            Reads++;
            return Task.FromResult<Stream>(new MemoryStream(Bytes, writable: false));
        }
        public Task DeleteAsync(StorageDriverInput storage, string? password, string path, CancellationToken cancellationToken) => Task.CompletedTask;
        public Task<RemoteBrowseTransportPage> BrowseAsync(StorageDriverInput storage, string? password, string path,
            RemoteBrowseTransportRequest request, CancellationToken cancellationToken) => throw new NotSupportedException();
    }

    private sealed class IpfsTransport : IIpfsStorageTransport, IIpfsStableStorageTransport {
        public ArgumentException Failure { get; } = new("Injected IPFS add acknowledgment loss.");
        public List<IpfsStableAddMode> Adds { get; } = [];
        public int Pins { get; private set; }
        private byte[] bytes = [];
        public Task TestConnectionAsync(StorageDriverInput storage, string? token, CancellationToken cancellationToken) => Task.CompletedTask;
        public Task<IpfsAddResult> AddAsync(StorageDriverInput storage, string? token, string name, ReadOnlyMemory<byte> content,
            CancellationToken cancellationToken) => throw new InvalidOperationException("Stable placement used ordinary IPFS add.");
        public Task<IpfsAddResult> AddStableAsync(StorageDriverInput storage, string? token, string name, ReadOnlyMemory<byte> content,
            IpfsStableAddMode mode, CancellationToken cancellationToken) {
            Adds.Add(mode);
            if (mode == IpfsStableAddMode.Store) {
                bytes = content.ToArray();
                throw Failure;
            }
            return Task.FromResult(new IpfsAddResult("bafy-fixed-test-cid"));
        }
        public Task PinAsync(StorageDriverInput storage, string? token, string id, CancellationToken cancellationToken) {
            Assert.Equal("bafy-fixed-test-cid", id);
            Pins++;
            return Task.CompletedTask;
        }
        public Task<Stream> OpenReadAsync(StorageDriverInput storage, string? token, string locator, string route, CancellationToken cancellationToken) {
            Assert.Equal("bafy-fixed-test-cid", locator);
            return Task.FromResult<Stream>(new MemoryStream(bytes, writable: false));
        }
        public Task<RemoteBrowseTransportPage> BrowseAsync(StorageDriverInput storage, string? token, IpfsBrowseAddress address,
            RemoteBrowseTransportRequest request, CancellationToken cancellationToken) => throw new NotSupportedException();
    }

    private sealed class NoSecrets : IStorageSecretResolver {
        public Task<string?> ResolveCredentialAsync(Guid? secretId, CancellationToken cancellationToken = default) {
            Assert.Null(secretId);
            return Task.FromResult<string?>(null);
        }
    }
    private sealed class Catalog(StorageCatalogRecord storage) : IStorageCatalogService {
        private Task<IReadOnlyList<StorageCatalogRecord>> ReadRecordsAsync(CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<StorageCatalogRecord>>([storage]);
        private Task<StorageCatalogRecord?> ReadRecordAsync(Guid id, CancellationToken cancellationToken = default) => Task.FromResult(id == storage.Id ? storage : null);
        private Task<StorageCatalogRecord> ReadBootstrapRecordAsync(CancellationToken cancellationToken = default) => throw new NotSupportedException();
        private Task<StorageCatalogRecord> SaveRecordAsync(StorageCatalogRecord record, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task DeleteAsync(Guid id, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        internal Task<IReadOnlyList<StorageRoutingRule>> ReadRoutingRecordsAsync(CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<StorageRoutingRule>>([]);
        private Task<StorageRoutingRule> SaveRoutingRecordAsync(StorageRoutingRule rule, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public async Task<IReadOnlyList<StorageCatalogSnapshot>> ListAsync(CancellationToken cancellationToken = default) =>
            (await ReadRecordsAsync(cancellationToken)).Select(StorageCatalogMapping.ToSnapshot).ToArray();

        public async Task<StorageCatalogSnapshot?> GetAsync(Guid id, CancellationToken cancellationToken = default) =>
            (await ReadRecordAsync(id, cancellationToken))?.ToSnapshot();

        public async Task<StorageDriverInput?> GetDriverAsync(Guid id, CancellationToken cancellationToken = default) =>
            (await ReadRecordAsync(id, cancellationToken))?.ToDriverInput();

        public async Task<StorageCatalogEditorSnapshot?> GetEditorAsync(Guid id, CancellationToken cancellationToken = default) {
            var row = await ReadRecordAsync(id, cancellationToken);
            return row is null ? null : new(row.ToSnapshot(), StorageJson.ParseProviderConfiguration(row.ConfigJson));
        }

        public async Task<StorageDriverInput> EnsureBootstrapFileSystemStorageAsync(CancellationToken cancellationToken = default) =>
            (await ReadBootstrapRecordAsync(cancellationToken)).ToDriverInput();

        public async Task<StorageCatalogSnapshot> SaveAsync(StorageCatalogSaveRequest request, CancellationToken cancellationToken = default) =>
            (await SaveRecordAsync(StorageCatalogMapping.CreateDraft(request), cancellationToken)).ToSnapshot();

        public async Task<IReadOnlyList<StorageRoutingRuleSnapshot>> ListRulesAsync(CancellationToken cancellationToken = default) =>
            (await ReadRoutingRecordsAsync(cancellationToken)).Select(StorageCatalogMapping.ToSnapshot).ToArray();

        public async Task<StorageRoutingRuleSnapshot> SaveRuleAsync(StorageRoutingRuleSaveRequest request, CancellationToken cancellationToken = default) =>
            (await SaveRoutingRecordAsync(StorageCatalogMapping.CreateDraft(request), cancellationToken)).ToSnapshot();

        public Task ApplyDefaultPurposesAsync(Guid storageId, IReadOnlyCollection<StorageUsagePurpose> defaultPurposes,
            CancellationToken cancellationToken = default) => throw new NotSupportedException();

    }
    private sealed class NoRouting : IStorageRoutingService {
        public Task<StorageRecommendation> RecommendAsync(StorageSelectionContext context, CancellationToken cancellationToken = default) => throw new InvalidOperationException("The fixture must use its explicit isolated storage.");
    }
    private sealed class Registry(IStorageDriver driver) : IStorageDriverRegistry {
        public IReadOnlyCollection<StorageProviderKind> RegisteredKinds => [driver.ProviderKind];
        public IStorageDriver Resolve(StorageProviderKind kind) => kind == driver.ProviderKind ? driver : throw new InvalidOperationException();
        public bool TryResolve(StorageProviderKind kind, out IStorageDriver resolved) {
            resolved = driver;
            return kind == driver.ProviderKind;
        }
    }
    private sealed class Access : IStorageAccessService {
        public Task<StorageAccessDescriptor> DescribeAsync(StorageObjectReference reference, CancellationToken cancellationToken = default)
            => Task.FromResult(new StorageAccessDescriptor(StorageJson.BuildPreviewUrl(reference), StorageJson.BuildDownloadUrl(reference),
                reference.Route, true, true, false, reference.DisplayName, reference.ContentType, reference.ContentLength, string.Empty));
    }
    private sealed class Paths(string root) : IWorkspacePathResolver {
        public string ResolveWorkspaceRoot() => root;
        public string ResolveManagedFilesRoot() => Path.Combine(root, "managed-files");
        public string ResolveExportsRoot() => Path.Combine(root, "exports");
        public string ResolveEvidenceRoot() => Path.Combine(root, "evidence");
        public string ResolveManagerArtifactsRoot() => Path.Combine(root, "manager-artifacts");
    }
}
