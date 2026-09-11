using System.Data.Common;
using System.Text;
using CanDoItAll.Infrastructure;
using CanDoItAll.Infrastructure.ControlPlane;
using CanDoItAll.Infrastructure.Persistence;
using CanDoItAll.Infrastructure.Storage;
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
        await driver.DeleteAsync(storage, reference);
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
        Id = Guid.NewGuid(), Name = "Isolated placement fixture", ProviderKind = kind, IsEnabled = true,
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
        public Task<StorageConnectionTestResult> TestConnectionAsync(StorageCatalogRecord storage, string? secretValue, CancellationToken cancellationToken = default)
            => inner.TestConnectionAsync(storage, secretValue, cancellationToken);
        public Task<StorageWriteResult> SaveAsync(StorageCatalogRecord storage, StorageWriteRequest request, CancellationToken cancellationToken = default)
            => throw new InvalidOperationException("Stable placement called ordinary Save.");
        public Task<Stream> OpenReadAsync(StorageCatalogRecord storage, StorageObjectReference reference, CancellationToken cancellationToken = default)
            => inner.OpenReadAsync(storage, reference, cancellationToken);
        public async Task DeleteAsync(StorageCatalogRecord storage, StorageObjectReference reference, CancellationToken cancellationToken = default) {
            Deletes++;
            await inner.DeleteAsync(storage, reference, cancellationToken);
        }
        public Task<StorageObjectReference> PrepareStableTargetAsync(StorageCatalogRecord storage, StoragePlacementIntentId id,
            StorageWriteRequest request, CancellationToken cancellationToken)
            => ((IStorageStablePlacementDriver)inner).PrepareStableTargetAsync(storage, id, request, cancellationToken);
        public async Task<StorageWriteResult> WriteStableTargetAsync(StorageCatalogRecord storage, StorageObjectReference target,
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
        public Task CompleteStableTargetAsync(StorageCatalogRecord storage, StorageObjectReference target, CancellationToken cancellationToken)
            => Task.CompletedTask;
    }

    private sealed class FtpTransport : IFtpStorageTransport {
        public bool FailAfterUpload { get; init; }
        public int Uploads { get; private set; }
        public int Reads { get; private set; }
        public byte[] Bytes { get; set; } = [];
        public Task<string?> TestConnectionAsync(StorageCatalogRecord storage, string? password, CancellationToken cancellationToken) => Task.FromResult<string?>(null);
        public Task UploadAsync(StorageCatalogRecord storage, string? password, string path, ReadOnlyMemory<byte> content, CancellationToken cancellationToken) {
            Uploads++;
            Bytes = content.ToArray();
            if (FailAfterUpload) {
                throw new IOException("Injected FTP acknowledgment loss.");
            }
            return Task.CompletedTask;
        }
        public Task<Stream> OpenReadAsync(StorageCatalogRecord storage, string? password, string path, CancellationToken cancellationToken) {
            Reads++;
            return Task.FromResult<Stream>(new MemoryStream(Bytes, writable: false));
        }
        public Task DeleteAsync(StorageCatalogRecord storage, string? password, string path, CancellationToken cancellationToken) => Task.CompletedTask;
        public Task<RemoteBrowseTransportPage> BrowseAsync(StorageCatalogRecord storage, string? password, string path,
            RemoteBrowseTransportRequest request, CancellationToken cancellationToken) => throw new NotSupportedException();
    }

    private sealed class IpfsTransport : IIpfsStorageTransport, IIpfsStableStorageTransport {
        public ArgumentException Failure { get; } = new("Injected IPFS add acknowledgment loss.");
        public List<IpfsStableAddMode> Adds { get; } = [];
        public int Pins { get; private set; }
        private byte[] bytes = [];
        public Task TestConnectionAsync(StorageCatalogRecord storage, string? token, CancellationToken cancellationToken) => Task.CompletedTask;
        public Task<IpfsAddResult> AddAsync(StorageCatalogRecord storage, string? token, string name, ReadOnlyMemory<byte> content,
            CancellationToken cancellationToken) => throw new InvalidOperationException("Stable placement used ordinary IPFS add.");
        public Task<IpfsAddResult> AddStableAsync(StorageCatalogRecord storage, string? token, string name, ReadOnlyMemory<byte> content,
            IpfsStableAddMode mode, CancellationToken cancellationToken) {
            Adds.Add(mode);
            if (mode == IpfsStableAddMode.Store) {
                bytes = content.ToArray();
                throw Failure;
            }
            return Task.FromResult(new IpfsAddResult("bafy-fixed-test-cid"));
        }
        public Task PinAsync(StorageCatalogRecord storage, string? token, string id, CancellationToken cancellationToken) {
            Assert.Equal("bafy-fixed-test-cid", id);
            Pins++;
            return Task.CompletedTask;
        }
        public Task<Stream> OpenReadAsync(StorageCatalogRecord storage, string? token, string locator, string route, CancellationToken cancellationToken) {
            Assert.Equal("bafy-fixed-test-cid", locator);
            return Task.FromResult<Stream>(new MemoryStream(bytes, writable: false));
        }
        public Task<RemoteBrowseTransportPage> BrowseAsync(StorageCatalogRecord storage, string? token, IpfsBrowseAddress address,
            RemoteBrowseTransportRequest request, CancellationToken cancellationToken) => throw new NotSupportedException();
    }

    private sealed class NoSecrets : IStorageSecretResolver {
        public Task<string?> ResolveCredentialAsync(Guid? secretId, CancellationToken cancellationToken = default) {
            Assert.Null(secretId);
            return Task.FromResult<string?>(null);
        }
    }
    private sealed class Catalog(StorageCatalogRecord storage) : IStorageCatalogService {
        public Task<IReadOnlyList<StorageCatalogRecord>> ListAsync(CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<StorageCatalogRecord>>([storage]);
        public Task<StorageCatalogRecord?> GetAsync(Guid id, CancellationToken cancellationToken = default) => Task.FromResult(id == storage.Id ? storage : null);
        public Task<StorageCatalogRecord> EnsureBootstrapFileSystemStorageAsync(CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<StorageCatalogRecord> SaveAsync(StorageCatalogRecord record, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task DeleteAsync(Guid id, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<IReadOnlyList<StorageRoutingRule>> ListRulesAsync(CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<StorageRoutingRule>>([]);
        public Task<StorageRoutingRule> SaveRuleAsync(StorageRoutingRule rule, CancellationToken cancellationToken = default) => throw new NotSupportedException();
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
