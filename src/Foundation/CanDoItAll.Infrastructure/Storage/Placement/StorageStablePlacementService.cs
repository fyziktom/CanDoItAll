using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using CanDoItAll.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CanDoItAll.Infrastructure.Storage;

public sealed class StorageStablePlacementService(IDbContextFactory<StorageDbContext> factory,
    IStorageCatalogService catalog, IStorageRoutingService routing, IStorageDriverRegistry drivers,
    IStorageAccessService access, FileSystemStoragePathPolicy paths, TimeProvider clock,
    IEnumerable<IStoragePlacementReceiptObserver> receiptObservers) {
    private const int MaximumContentBytes = 256 * 1024 * 1024;
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public async Task<StorageStablePlacementOutcome> PlaceAsync(StoragePlacementIntentId intentId,
        StoragePlacementRequest request, CancellationToken cancellationToken = default) {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(request.Content);
        if (intentId.Value == Guid.Empty || request.Content.Length > MaximumContentBytes || string.IsNullOrWhiteSpace(request.FileName)) {
            throw new ArgumentException("The stable storage placement has an invalid intent, filename or content size.");
        }

        request = request with { Content = request.Content.ToArray() };
        var fingerprint = Fingerprint(request);
        var saved = await FindRecordAsync(intentId, cancellationToken);
        if (saved is null) {
            saved = await PrepareAsync(intentId, fingerprint, request, cancellationToken);
        }
        if (saved.RequestFingerprint != fingerprint) {
            throw new StorageStablePlacementConflictException(intentId);
        }
        if (saved.State is StorageStablePlacementState.Completed or StorageStablePlacementState.Deleted or StorageStablePlacementState.Conflict) {
            return await ObserveAsync(Outcome(saved), cancellationToken);
        }

        var claimed = await ClaimDispatchAsync(intentId, cancellationToken);
        if (!claimed) {
            return await ReconcileAsync(intentId, cancellationToken);
        }

        Exception? observation = null;
        try {
            var plan = ReadPlan(saved);
            var storage = await LoadCurrentStorageAsync(plan, cancellationToken);
            var driver = RequireStableDriver(storage.ProviderKind);
            var written = await driver.WriteStableTargetAsync(storage, plan.Reference, WriteRequest(request), cancellationToken);
            if (!SameTarget(written.Reference, plan.Reference) || written.Reference.ContentType != plan.Reference.ContentType ||
                written.Reference.ContentLength != plan.Reference.ContentLength) {
                return await RecordUncertainAsync(intentId, StorageStablePlacementState.Conflict,
                    "The provider returned a different placement target. No replacement write was attempted.", null, CancellationToken.None);
            }
            await RecordDispatchAcknowledgementAsync(intentId, cancellationToken);
        } catch (Exception exception) {
            observation = exception;
        }

        using var bounded = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        var result = await ReconcileAsync(intentId, bounded.Token);
        return result with { ObservationException = observation ?? result.ObservationException };
    }

    public async Task<StorageStablePlacementOutcome?> FindAsync(StoragePlacementIntentId intentId,
        CancellationToken cancellationToken = default) {
        var row = await FindRecordAsync(intentId, cancellationToken);
        return row is null ? null : Outcome(row);
    }

    public async Task<StorageStablePlacementOutcome> ReconcileAsync(StoragePlacementIntentId intentId,
        CancellationToken cancellationToken = default) {
        var saved = await FindRecordAsync(intentId, cancellationToken)
            ?? throw new InvalidOperationException("The storage placement was not prepared.");
        if (saved.State is StorageStablePlacementState.Completed or StorageStablePlacementState.Deleted or StorageStablePlacementState.Conflict) {
            return await ObserveAsync(Outcome(saved), cancellationToken);
        }
        if (saved.State == StorageStablePlacementState.Prepared) {
            return Outcome(saved);
        }

        var plan = ReadPlan(saved);
        try {
            var storage = await LoadCurrentStorageAsync(plan, cancellationToken);
            var driver = RequireStableDriver(storage.ProviderKind);
            if (!driver.CanRecoverWithoutWriteAcknowledgement && !saved.WriteAcknowledged && !saved.ExternalDispatchConfirmedStopped) {
                return await RecordUncertainAsync(intentId, StorageStablePlacementState.Uncertain,
                    "The FTP write has no completion acknowledgment. An operator must verify that its server-side dispatch stopped before exact readback can complete recovery.",
                    null, cancellationToken);
            }
            await using var content = await drivers.Resolve(storage.ProviderKind).OpenReadAsync(storage, plan.Reference, cancellationToken);
            using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
            var buffer = new byte[81920];
            long length = 0;
            int read;
            while ((read = await content.ReadAsync(buffer, cancellationToken)) != 0) {
                length += read;
                if (length > plan.ContentLength) {
                    return await RecordUncertainAsync(intentId, StorageStablePlacementState.Conflict,
                        "The prepared storage target contains different content. It was preserved.", null, cancellationToken);
                }
                hash.AppendData(buffer, 0, read);
            }
            if (length != plan.ContentLength || Convert.ToHexString(hash.GetHashAndReset()) != plan.ContentSha256) {
                return await RecordUncertainAsync(intentId, StorageStablePlacementState.Conflict,
                    "The prepared storage target contains different content. It was preserved.", null, cancellationToken);
            }

            await driver.CompleteStableTargetAsync(storage, plan.Reference, cancellationToken);
            var descriptor = await access.DescribeAsync(plan.Reference, cancellationToken);
            var write = new StorageWriteResult(plan.Reference, descriptor);
            var route = descriptor.SupportsInlinePreview ? descriptor.PreviewUrl : descriptor.SupportsDownload ? descriptor.DownloadUrl
                : descriptor.DirectUrl ?? plan.Reference.Route;
            var relativePath = plan.Reference.ProviderKind == StorageProviderKind.FileSystem && plan.Reference.LocatorKind == StorageLocatorKind.RelativePath
                ? plan.Reference.Locator : string.Empty;
            var location = storage.ProviderKind == StorageProviderKind.FileSystem ? paths.ResolveFullPath(storage, plan.Reference.Locator)
                : storage.ProviderKind == StorageProviderKind.Ipfs ? plan.Reference.Route : $"{storage.EndpointOrRoot.TrimEnd('/')}/{plan.Reference.Locator.TrimStart('/')}";
            var appliedAt = clock.GetUtcNow().ToUniversalTime();
            appliedAt = new(appliedAt.Ticks - appliedAt.Ticks % TimeSpan.TicksPerMicrosecond, TimeSpan.Zero);
            var receipt = new StorageStablePlacementReceipt(intentId, saved.RequestFingerprint,
                StorageCatalogPlanningFact.FromCatalogRecord(storage, includeFtpAddressing: true), write, route, location, relativePath, appliedAt);
            var completed = await CompleteAsync(intentId, receipt, cancellationToken);
            return await ObserveAsync(completed, cancellationToken);
        } catch (Exception exception) {
            return await RecordUncertainAsync(intentId, StorageStablePlacementState.Uncertain,
                "The previous placement could not be verified. Its exact target is retained; no second write or filename was allocated.", exception, CancellationToken.None);
        }
    }

    public async Task<IReadOnlyList<StorageStablePlacementOutcome>> ListPendingAsync(Guid? projectId, int take,
        CancellationToken cancellationToken = default) {
        if (take is < 1 or > 128) {
            throw new ArgumentOutOfRangeException(nameof(take));
        }
        await using var database = await factory.CreateDbContextAsync(cancellationToken);
        var query = database.Set<StoragePlacementIntentRecord>().AsNoTracking()
            .Where(row => row.State == StorageStablePlacementState.Prepared || row.State == StorageStablePlacementState.Dispatching ||
                row.State == StorageStablePlacementState.Uncertain || row.State == StorageStablePlacementState.Conflict);
        if (projectId.HasValue) {
            query = query.Where(row => row.ProjectId == projectId.Value);
        }
        var rows = await query.OrderBy(row => row.UpdatedAtUtc).ThenBy(row => row.Id).Take(take).ToListAsync(cancellationToken);
        return rows.Select(Outcome).ToArray();
    }

    public async Task<StorageStablePlacementOutcome> RecordOperatorVerifiedExternalDispatchTerminationAsync(
        StoragePlacementIntentId intentId, CancellationToken cancellationToken = default) {
        await using (var database = await factory.CreateDbContextAsync(cancellationToken)) {
            await using var transaction = await SerializableMutationScope.BeginAsync(database, Scope(intentId.Value), cancellationToken);
            var row = await database.Set<StoragePlacementIntentRecord>().SingleAsync(row => row.Id == intentId.Value, cancellationToken);
            if (ReadPlan(row).Reference.ProviderKind != StorageProviderKind.Ftp ||
                row.State is not (StorageStablePlacementState.Dispatching or StorageStablePlacementState.Uncertain)) {
                throw new InvalidOperationException("Only an unresolved FTP dispatch accepts explicit external termination verification.");
            }
            row.ExternalDispatchConfirmedStopped = true;
            row.UpdatedAtUtc = clock.GetUtcNow();
            await database.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }
        return await ReconcileAsync(intentId, cancellationToken);
    }

    public async Task<bool> MarkDeletionAsync(StorageObjectReference reference, CancellationToken cancellationToken = default) {
        if (reference.PlacementIntentId is not { } id) {
            return true;
        }
        await using var database = await factory.CreateDbContextAsync(cancellationToken);
        await using var transaction = await SerializableMutationScope.BeginAsync(database, Scope(id), cancellationToken);
        var row = await database.Set<StoragePlacementIntentRecord>().SingleAsync(row => row.Id == id, cancellationToken);
        if (!SameTarget(ReadPlan(row).Reference, reference)) {
            throw new StorageStablePlacementConflictException(new(id));
        }
        row.DeletionRequested = true;
        var mayDelete = row.State is StorageStablePlacementState.Prepared or StorageStablePlacementState.Completed or StorageStablePlacementState.Deleted;
        if (mayDelete) {
            row.State = StorageStablePlacementState.Deleted;
        }
        row.UpdatedAtUtc = clock.GetUtcNow();
        await database.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return mayDelete;
    }

    private async Task<StorageStablePlacementOutcome> ObserveAsync(StorageStablePlacementOutcome outcome, CancellationToken cancellationToken) {
        if (outcome.State != StorageStablePlacementState.Completed || outcome.Receipt is null) {
            return outcome;
        }
        foreach (var observer in receiptObservers) {
            try {
                await observer.ObserveAsync(outcome.Receipt, cancellationToken);
            } catch (Exception exception) {
                outcome = outcome with { ObservationException = outcome.ObservationException ?? exception };
            }
        }
        return outcome;
    }

    private async Task<StoragePlacementIntentRecord> PrepareAsync(StoragePlacementIntentId id, string fingerprint,
        StoragePlacementRequest request, CancellationToken cancellationToken) {
        var selectedId = request.PreferredStorageId;
        if (!selectedId.HasValue) {
            var recommendation = await routing.RecommendAsync(new(request.FileName, request.ContentType, request.UsagePurpose,
                request.ContentKind, request.ProjectId, request.NodeKey, request.Content.LongLength,
                PreviewRequired: request.PreviewRequired, PublishIntent: request.PublishIntent), cancellationToken);
            selectedId = recommendation.PrimaryCandidate?.StorageId
                ?? throw new InvalidOperationException(recommendation.Reason);
        }
        var storage = await catalog.GetAsync(selectedId.Value, cancellationToken)
            ?? throw new InvalidOperationException("The selected storage no longer exists.");
        ValidateCurrentCapabilities(storage, request.PreviewRequired);
        var target = await RequireStableDriver(storage.ProviderKind).PrepareStableTargetAsync(storage, id, WriteRequest(request), cancellationToken);
        target = target with { PlacementIntentId = id.Value, FormatVersion = StorageObjectReference.StablePlacementFormatVersion };
        var plan = new PlacementPlan(target, TargetFingerprint(storage), Convert.ToHexString(SHA256.HashData(request.Content)),
            request.Content.LongLength, request.PreviewRequired);
        await using var database = await factory.CreateDbContextAsync(cancellationToken);
        await using var transaction = await SerializableMutationScope.BeginAsync(database, Scope(id.Value), cancellationToken);
        var existing = await database.Set<StoragePlacementIntentRecord>().AsNoTracking().SingleOrDefaultAsync(row => row.Id == id.Value, cancellationToken);
        if (existing is not null) {
            return existing;
        }
        var row = new StoragePlacementIntentRecord {
            Id = id.Value, StorageId = storage.Id, ProjectId = request.ProjectId, RequestFingerprint = fingerprint,
            PlanJson = JsonSerializer.Serialize(plan, JsonOptions), State = StorageStablePlacementState.Prepared,
            CreatedAtUtc = clock.GetUtcNow(), UpdatedAtUtc = clock.GetUtcNow()
        };
        database.Add(row);
        await database.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return row;
    }

    private async Task<bool> ClaimDispatchAsync(StoragePlacementIntentId id, CancellationToken cancellationToken) {
        await using var database = await factory.CreateDbContextAsync(cancellationToken);
        await using var transaction = await SerializableMutationScope.BeginAsync(database, Scope(id.Value), cancellationToken);
        var row = await database.Set<StoragePlacementIntentRecord>().SingleAsync(row => row.Id == id.Value, cancellationToken);
        if (row.State != StorageStablePlacementState.Prepared || row.DeletionRequested) {
            return false;
        }
        row.State = StorageStablePlacementState.Dispatching;
        row.UpdatedAtUtc = clock.GetUtcNow();
        await database.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return true;
    }

    private async Task RecordDispatchAcknowledgementAsync(StoragePlacementIntentId id, CancellationToken cancellationToken) {
        await using var database = await factory.CreateDbContextAsync(cancellationToken);
        await using var transaction = await SerializableMutationScope.BeginAsync(database, Scope(id.Value), cancellationToken);
        var row = await database.Set<StoragePlacementIntentRecord>().SingleAsync(row => row.Id == id.Value, cancellationToken);
        row.WriteAcknowledged = true;
        row.UpdatedAtUtc = clock.GetUtcNow();
        await database.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
    }

    private async Task<StorageStablePlacementOutcome> CompleteAsync(StoragePlacementIntentId id,
        StorageStablePlacementReceipt receipt, CancellationToken cancellationToken) {
        await using var database = await factory.CreateDbContextAsync(cancellationToken);
        await using var transaction = await SerializableMutationScope.BeginAsync(database, Scope(id.Value), cancellationToken);
        var row = await database.Set<StoragePlacementIntentRecord>().SingleAsync(row => row.Id == id.Value, cancellationToken);
        if (row.ReceiptJson.Length > 0 || row.State is StorageStablePlacementState.Deleted or StorageStablePlacementState.Conflict) {
            return Outcome(row);
        }
        row.ReceiptJson = JsonSerializer.Serialize(receipt, JsonOptions);
        row.State = StorageStablePlacementState.Completed;
        row.UpdatedAtUtc = clock.GetUtcNow();
        await database.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return Outcome(row);
    }

    private async Task<StorageStablePlacementOutcome> RecordUncertainAsync(StoragePlacementIntentId id,
        StorageStablePlacementState state, string message, Exception? exception, CancellationToken cancellationToken) {
        await using var database = await factory.CreateDbContextAsync(cancellationToken);
        await using var transaction = await SerializableMutationScope.BeginAsync(database, Scope(id.Value), cancellationToken);
        var row = await database.Set<StoragePlacementIntentRecord>().SingleAsync(row => row.Id == id.Value, cancellationToken);
        if (row.State is not (StorageStablePlacementState.Completed or StorageStablePlacementState.Deleted or StorageStablePlacementState.Conflict)) {
            row.State = state;
            row.UpdatedAtUtc = clock.GetUtcNow();
            await database.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }
        var outcome = Outcome(row);
        return outcome with {
            Message = row.State is StorageStablePlacementState.Completed or StorageStablePlacementState.Deleted ? outcome.Message : message,
            ObservationException = exception
        };
    }

    private async Task<StorageCatalogRecord> LoadCurrentStorageAsync(PlacementPlan plan, CancellationToken cancellationToken) {
        var storage = await catalog.GetAsync(plan.Reference.StorageId!.Value, cancellationToken)
            ?? throw new InvalidOperationException("The prepared storage is no longer available.");
        if (TargetFingerprint(storage) != plan.TargetFingerprint) {
            throw new InvalidOperationException("The prepared storage endpoint or host binding changed. Rebinding is required before recovery.");
        }
        ValidateCurrentCapabilities(storage, plan.PreviewRequired);
        return storage;
    }

    private static void ValidateCurrentCapabilities(StorageCatalogRecord storage, bool preview) {
        var required = StorageCapability.Read | StorageCapability.Write | (preview ? StorageCapability.InlinePreview : StorageCapability.None);
        if (!storage.IsEnabled || storage.IsReadOnly || storage.HealthStatus == StorageHealthStatus.Unavailable ||
            (storage.CapabilityMask & required) != required) {
            throw new InvalidOperationException("The selected storage does not currently permit this placement and readback.");
        }
    }

    private async Task<StoragePlacementIntentRecord?> FindRecordAsync(StoragePlacementIntentId id, CancellationToken cancellationToken) {
        await using var database = await factory.CreateDbContextAsync(cancellationToken);
        return await database.Set<StoragePlacementIntentRecord>().AsNoTracking().SingleOrDefaultAsync(row => row.Id == id.Value, cancellationToken);
    }

    private IStorageStablePlacementDriver RequireStableDriver(StorageProviderKind kind)
        => drivers.Resolve(kind) as IStorageStablePlacementDriver
            ?? throw new InvalidOperationException($"Storage provider '{kind}' has no explicit stable-placement contract.");
    private static string Scope(Guid id) => $"storage-placement-intent:{id:N}";
    private static PlacementPlan ReadPlan(StoragePlacementIntentRecord row) {
        var plan = JsonSerializer.Deserialize<PlacementPlan>(row.PlanJson, JsonOptions)
            ?? throw new InvalidOperationException("The saved storage placement plan is invalid.");
        if (plan.Reference.StorageId != row.StorageId || plan.Reference.PlacementIntentId != row.Id ||
            plan.ContentLength < 0 || plan.ContentLength > MaximumContentBytes || plan.Reference.ContentLength != plan.ContentLength ||
            !IsHash(plan.ContentSha256) || !IsHash(plan.TargetFingerprint) || !IsHash(row.RequestFingerprint)) {
            throw new InvalidOperationException("The saved storage placement identity, content or target binding is inconsistent.");
        }
        return plan;
    }

    private static bool IsHash(string value) => value.Length == 64 && value.All(char.IsAsciiHexDigit);
    private static StorageStablePlacementOutcome Outcome(StoragePlacementIntentRecord row) {
        var plan = ReadPlan(row);
        var receipt = row.ReceiptJson.Length == 0 ? null
            : JsonSerializer.Deserialize<StorageStablePlacementReceipt>(row.ReceiptJson, JsonOptions)
                ?? throw new InvalidOperationException("The saved storage placement receipt is invalid.");
        if (row.State == StorageStablePlacementState.Completed && receipt is null ||
            receipt is not null && (receipt.IntentId.Value != row.Id || receipt.RequestFingerprint != row.RequestFingerprint ||
                receipt.Storage.Id != row.StorageId || !SameTarget(receipt.WriteResult.Reference, plan.Reference) ||
                row.State is not (StorageStablePlacementState.Completed or StorageStablePlacementState.Deleted))) {
            throw new InvalidOperationException("The saved storage receipt identity or completion state is inconsistent.");
        }
        return new(new(row.Id), row.State, receipt, row.State switch {
            StorageStablePlacementState.Completed => "The placement is retained with its original receipt.",
            StorageStablePlacementState.Deleted => "This placement was deleted; replay will not recreate it.",
            StorageStablePlacementState.Conflict => "The prepared target differs from its expected content; it was preserved.",
            _ => "Storage placement remains pending exact reconciliation."
        });
    }
    private static StorageWriteRequest WriteRequest(StoragePlacementRequest request)
        => new(request.FileName, request.ContentType, request.Content, request.UsagePurpose, request.ContentKind,
            request.ProjectId, request.NodeKey, request.RelativePathHint, request.PreviewRequired, request.PublishIntent);
    private static string Fingerprint(StoragePlacementRequest request) => Hash(JsonSerializer.Serialize(new {
        Version = 1, request.FileName, request.ContentType, request.UsagePurpose, request.ContentKind, request.ProjectId,
        request.NodeKey, request.RelativePathHint, request.PreviewRequired, request.PublishIntent, request.PreferredStorageId,
        ContentLength = request.Content.LongLength, ContentSha256 = Convert.ToHexString(SHA256.HashData(request.Content))
    }));
    private static string TargetFingerprint(StorageCatalogRecord storage) => Hash(JsonSerializer.Serialize(new {
        storage.Id, storage.ProviderKind, storage.ConnectionMode, storage.EndpointOrRoot, storage.RootBindingFormatVersion,
        storage.RootPlatformFamily, storage.RootPathSyntax, storage.RootHostBindingId, storage.RootPathState, storage.ConfigJson
    }));
    private static string Hash(string value) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value)));
    private static bool SameTarget(StorageObjectReference left, StorageObjectReference right)
        => left.StorageId == right.StorageId && left.ProviderKind == right.ProviderKind && left.LocatorKind == right.LocatorKind &&
            left.Locator == right.Locator && left.PlacementIntentId == right.PlacementIntentId;
    private sealed record PlacementPlan(StorageObjectReference Reference, string TargetFingerprint, string ContentSha256,
        long ContentLength, bool PreviewRequired);
}
