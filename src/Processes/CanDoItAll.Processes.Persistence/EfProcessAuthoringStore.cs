using System.Data;
using CanDoItAll.Infrastructure.Persistence;
using CanDoItAll.Processes.Application;
using Microsoft.EntityFrameworkCore;

namespace CanDoItAll.Processes.Persistence;

public sealed class EfProcessAuthoringStore(IDbContextFactory<ProcessPersistenceDbContext> factory,
    CoordinatedDatabaseTransaction transactions, IProcessAuthoringAdmissionPolicy admissions, TimeProvider clock) : IProcessAuthoringStore {
    public async Task<ProcessAuthoringSnapshot?> ReadAsync(ProcessAuthoringAddress address, CancellationToken cancellationToken = default) {
        address.Validate();
        await admissions.RequireAsync(address, null, false, cancellationToken);
        await using var context = await factory.CreateDbContextAsync(cancellationToken);
        var head = await FindHead(context, address).AsNoTracking().SingleOrDefaultAsync(cancellationToken);
        return head is null ? null : Snapshot(head);
    }

    public async Task<IReadOnlyList<ProcessAuthoringCatalogEntry>> ListAsync(Guid databaseProfileId, Guid projectId, Guid projectLifetimeId, CancellationToken cancellationToken = default) {
        await admissions.RequireAsync(new(databaseProfileId, projectId, projectLifetimeId, "catalog"), null, false, cancellationToken);
        await using var context = await factory.CreateDbContextAsync(cancellationToken);
        return await context.AuthoringHeads.AsNoTracking()
            .Where(item => item.DatabaseProfileId == databaseProfileId &&
                (item.ProjectId == Guid.Empty || item.ProjectId == projectId && item.ProjectLifetimeId == projectLifetimeId))
            .Select(item => new ProcessAuthoringCatalogEntry(new(item.DatabaseProfileId, item.ProjectId, item.ProjectLifetimeId, item.DefinitionKey),
                item.Revision, item.Lifecycle, item.PublishedId, item.Name, item.Summary, item.Criticality, item.OperatingMode, item.UpdatedAtUtc))
            .ToArrayAsync(cancellationToken);
    }

    public async Task<ProcessAuthoringPublication?> ReadPublicationAsync(Guid publicationId, CancellationToken cancellationToken = default) {
        await using var context = await factory.CreateDbContextAsync(cancellationToken);
        var entity = await context.AuthoringPublications.AsNoTracking().SingleOrDefaultAsync(item => item.Id == publicationId, cancellationToken);
        if (entity is null) {
            return null;
        }
        ProcessAuthoringAddress address = new(entity.DatabaseProfileId, entity.ProjectId, entity.ProjectLifetimeId, entity.DefinitionKey);
        await admissions.RequireAsync(address, null, false, cancellationToken);
        if (ProcessAuthoringCodec.Hash(entity.ContentJson) != entity.ContentHash) {
            throw new InvalidOperationException("The immutable authoring publication failed its content integrity check.");
        }
        return new(entity.Id, address, entity.Revision, entity.ContentHash, ProcessAuthoringCodec.Read(entity.ContentJson), entity.PublishedAtUtc);
    }

    public async Task<ProcessAuthoringReceipt?> RecoverAsync(ProcessAuthoringAddress address, string callerId, Guid operationId, string requestFingerprint, CancellationToken cancellationToken = default) {
        address.Validate();
        await admissions.RequireAsync(address, callerId, false, cancellationToken);
        await using var context = await factory.CreateDbContextAsync(cancellationToken);
        return await FindReceipt(context, address, callerId, operationId, requestFingerprint, cancellationToken);
    }

    public async Task<ProcessAuthoringReceipt> CommitAsync(ProcessAuthoringCommit command, CancellationToken cancellationToken = default) {
        command.Address.Validate();
        if (command.OperationId == Guid.Empty || string.IsNullOrWhiteSpace(command.CallerId) || command.CallerId.Length > 200 ||
                command.RequestFingerprint.Length != 71 || command.ExpectedRevision < 0 ||
                command.Content.Definition.Key != command.Address.DefinitionKey || command.Publish && command.Lifecycle != ProcessAuthoringLifecycle.Published) {
            throw new ArgumentException("The authoring operation has an invalid identity, revision or publication.");
        }
        var json = ProcessAuthoringCodec.Write(command.Content);
        var content = ProcessAuthoringCodec.Read(json);
        await admissions.RequireAsync(command.Address, command.CallerId, false, cancellationToken);
        await using var context = await factory.CreateDbContextAsync(cancellationToken);
        if (!context.Database.IsNpgsql()) {
            throw new NotSupportedException("Durable authoring requires PostgreSQL transaction semantics.");
        }
        transactions.RequireOwnerProfile(context);
        await using var transaction = await context.Database.BeginTransactionAsync(IsolationLevel.ReadCommitted, cancellationToken);
        using var participation = transactions.Enter(context);
        await admissions.RequireAsync(command.Address, command.CallerId, true, cancellationToken);
        var operationLock = $"process-authoring-operation:{command.Address.DatabaseProfileId:N}:{command.CallerId}:{command.OperationId:N}";
        await context.Database.ExecuteSqlInterpolatedAsync($"SELECT pg_advisory_xact_lock(hashtextextended({operationLock}, 0))", cancellationToken);
        var replay = await FindReceipt(context, command.Address, command.CallerId, command.OperationId, command.RequestFingerprint, cancellationToken);
        if (replay is not null) {
            return replay;
        }
        var aggregateLock = $"process-authoring:{command.Address.DatabaseProfileId:N}:{command.Address.ProjectId:N}:{command.Address.ProjectLifetimeId:N}:{command.Address.DefinitionKey}";
        await context.Database.ExecuteSqlInterpolatedAsync($"SELECT pg_advisory_xact_lock(hashtextextended({aggregateLock}, 0))", cancellationToken);
        var head = await FindHead(context, command.Address).SingleOrDefaultAsync(cancellationToken);
        var outcome = ProcessAuthoringOutcome.Conflict;
        if ((head?.Revision ?? 0) == command.ExpectedRevision) {
            outcome = ProcessAuthoringOutcome.Accepted;
            if (head is null) {
                head = new() { DatabaseProfileId = command.Address.DatabaseProfileId, ProjectId = command.Address.ProjectId,
                    ProjectLifetimeId = command.Address.ProjectLifetimeId, DefinitionKey = command.Address.DefinitionKey };
                context.AuthoringHeads.Add(head);
            }
            head.Revision++;
            head.Lifecycle = command.Lifecycle;
            head.ContentJson = json;
            head.Name = content.Definition.DisplayName;
            head.Summary = content.Definition.Summary;
            head.Criticality = content.Definition.Criticality;
            head.OperatingMode = content.Definition.OperatingMode;
            head.UpdatedAtUtc = clock.GetUtcNow();
            if (command.Publish) {
                var publicationId = Guid.NewGuid();
                context.AuthoringPublications.Add(new() { Id = publicationId, DatabaseProfileId = head.DatabaseProfileId,
                    ProjectId = head.ProjectId, ProjectLifetimeId = head.ProjectLifetimeId, DefinitionKey = head.DefinitionKey,
                    Revision = head.Revision, ContentHash = ProcessAuthoringCodec.Hash(json), ContentJson = json, PublishedAtUtc = head.UpdatedAtUtc });
                head.PublishedId = publicationId;
            } else if (command.Lifecycle == ProcessAuthoringLifecycle.Deleted) {
                head.PublishedId = null;
            }
        }
        ProcessAuthoringReceipt receipt = new(command.OperationId, outcome, head is null ? null : Snapshot(head));
        context.AuthoringReceipts.Add(new() { DatabaseProfileId = command.Address.DatabaseProfileId, CallerId = command.CallerId,
            OperationId = command.OperationId, ProjectId = command.Address.ProjectId, ProjectLifetimeId = command.Address.ProjectLifetimeId,
            DefinitionKey = command.Address.DefinitionKey, RequestFingerprint = command.RequestFingerprint,
            ReceiptJson = ProcessAuthoringCodec.WriteReceipt(receipt) });
        await context.SaveChangesAsync(cancellationToken);
        await admissions.RequireAsync(command.Address, command.CallerId, true, cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return receipt;
    }

    private static IQueryable<ProcessAuthoringHeadEntity> FindHead(ProcessPersistenceDbContext context, ProcessAuthoringAddress address)
        => context.AuthoringHeads.Where(item => item.DatabaseProfileId == address.DatabaseProfileId && item.ProjectId == address.ProjectId &&
            item.ProjectLifetimeId == address.ProjectLifetimeId && item.DefinitionKey == address.DefinitionKey);

    private static ProcessAuthoringSnapshot Snapshot(ProcessAuthoringHeadEntity head)
        => new(new(head.DatabaseProfileId, head.ProjectId, head.ProjectLifetimeId, head.DefinitionKey), head.Revision,
            head.Lifecycle, ProcessAuthoringCodec.Read(head.ContentJson), head.PublishedId, head.UpdatedAtUtc);

    private static async Task<ProcessAuthoringReceipt?> FindReceipt(ProcessPersistenceDbContext context, ProcessAuthoringAddress address,
        string callerId, Guid operationId, string fingerprint, CancellationToken cancellationToken) {
        var entity = await context.AuthoringReceipts.AsNoTracking().SingleOrDefaultAsync(item => item.DatabaseProfileId == address.DatabaseProfileId &&
            item.CallerId == callerId && item.OperationId == operationId, cancellationToken);
        if (entity is null) {
            return null;
        }
        if (entity.ProjectId != address.ProjectId || entity.ProjectLifetimeId != address.ProjectLifetimeId ||
                entity.DefinitionKey != address.DefinitionKey || entity.RequestFingerprint != fingerprint) {
            throw new InvalidOperationException("The authoring operation identity was reused for different submitted content or ownership.");
        }
        return ProcessAuthoringCodec.ReadReceipt(entity.ReceiptJson);
    }
}
