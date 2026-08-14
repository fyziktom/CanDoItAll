using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using CanDoItAll.Infrastructure;
using CanDoItAll.Infrastructure.ControlPlane;
using CanDoItAll.Infrastructure.Persistence;
using CanDoItAll.SharedKernel;
using Microsoft.EntityFrameworkCore;

namespace CanDoItAll.Modules.Security;

public enum SecretMigrationSourceSelection
{
    LegacyDataProtectionRecords,
    VaultReferences,
    All
}

public enum SecretMigrationRecordState
{
    Prepared,
    DestinationStaged,
    DestinationVerified,
    ReferenceCommitted,
    RestartVerified,
    SourceCleaned,
    RolledBack
}

public enum SecretMigrationAuditStage
{
    DryRunVerified,
    Prepared,
    DestinationStaged,
    DestinationVerified,
    ReferenceCommitted,
    RestartVerified,
    SourceCleaned,
    RolledBack,
    Failed
}

public sealed record SecretMigrationOptions(
    string MigrationRoot,
    SecretMigrationSourceSelection SourceSelection,
    bool DryRun = false);

public sealed record SecretMigrationAuditEvent(
    Guid MigrationId,
    Guid SecretRecordId,
    SecretMigrationAuditStage Stage,
    DateTimeOffset TimestampUtc,
    string? ErrorCode = null);

public interface ISecretMigrationAuditSink
{
    ValueTask RecordAsync(SecretMigrationAuditEvent auditEvent, CancellationToken cancellationToken);
}

public enum SecretMigrationInterruptionPoint
{
    AfterSourceReferenceRestored
}

public interface ISecretMigrationInterruptionObserver
{
    ValueTask ObserveAsync(
        Guid secretRecordId,
        SecretMigrationInterruptionPoint point,
        CancellationToken cancellationToken);
}

public sealed class NullSecretMigrationAuditSink : ISecretMigrationAuditSink
{
    public ValueTask RecordAsync(
        SecretMigrationAuditEvent auditEvent,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return ValueTask.CompletedTask;
    }
}

public sealed class NullSecretMigrationInterruptionObserver : ISecretMigrationInterruptionObserver
{
    public ValueTask ObserveAsync(
        Guid secretRecordId,
        SecretMigrationInterruptionPoint point,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return ValueTask.CompletedTask;
    }
}

public sealed record SecretMigrationReport(
    Guid MigrationId,
    bool DryRun,
    int CandidateCount,
    int CommittedCount,
    int CleanedCount,
    int ControlPlaneProtectedPasswordCount,
    bool RequiresRestartCheckpoint);

public interface ISecretMigrationCoordinatorFactory
{
    SecretMigrationCoordinator CreateLegacyDataProtectionMigration();

    SecretMigrationCoordinator CreateVaultMigration(SecretVaultOptions sourceVaultOptions);
}

public sealed class SecretMigrationCoordinatorFactory(
    IDbContextFactory<AppDbContext> dbContextFactory,
    ISecretProtector legacyDataProtectionProtector,
    ISecretVault destinationVault,
    IControlPlaneSecretContinuityVerifier controlPlaneContinuityVerifier,
    DurableFileWriter durableFileWriter,
    IClock clock,
    ISecretMigrationAuditSink auditSink,
    ISecretMigrationInterruptionObserver interruptionObserver) : ISecretMigrationCoordinatorFactory
{
    public SecretMigrationCoordinator CreateLegacyDataProtectionMigration()
        => Create(new RejectingSourceVault());

    public SecretMigrationCoordinator CreateVaultMigration(SecretVaultOptions sourceVaultOptions)
        => Create(SecretVaultFactory.CreateMigrationSource(sourceVaultOptions, durableFileWriter));

    private SecretMigrationCoordinator Create(ISecretVault sourceVault)
        => new(
            dbContextFactory,
            legacyDataProtectionProtector,
            sourceVault,
            destinationVault,
            controlPlaneContinuityVerifier,
            durableFileWriter,
            clock,
            auditSink,
            interruptionObserver);

    private sealed class RejectingSourceVault : ISecretVault
    {
        public Task SetAsync(string key, string value, CancellationToken ct = default)
            => throw new InvalidOperationException(
                "The legacy Data Protection migration cannot write a source vault.");

        public Task<string?> GetAsync(string key, CancellationToken ct = default)
            => throw new InvalidOperationException(
                "The selected migration source does not include vault references.");

        public Task DeleteAsync(string key, CancellationToken ct = default)
            => throw new InvalidOperationException(
                "The legacy Data Protection migration cannot delete a source vault.");
    }
}

public sealed class SecretMigrationCoordinator(
    IDbContextFactory<AppDbContext> dbContextFactory,
    ISecretProtector legacyDataProtectionProtector,
    ISecretVault sourceVault,
    ISecretVault destinationVault,
    IControlPlaneSecretContinuityVerifier controlPlaneContinuityVerifier,
    DurableFileWriter durableFileWriter,
    IClock clock,
    ISecretMigrationAuditSink auditSink,
    ISecretMigrationInterruptionObserver? interruptionObserver = null)
{
    private const int CurrentSchemaVersion = 1;
    private const string JournalFileName = "secret-migration.v1.json";
    private const string CoordinationFileName = "secret-migration.v1.lock";
    private static readonly JsonSerializerOptions SerializerOptions = CreateSerializerOptions();

    public async Task<SecretMigrationReport> RunAsync(
        SecretMigrationOptions options,
        CancellationToken cancellationToken = default)
    {
        ValidateOptions(options);
        if (options.DryRun)
        {
            return await RunDryRunAsync(options, cancellationToken).ConfigureAwait(false);
        }

        using IDisposable coordination = AcquireCoordination(options, cancellationToken);
        SecretMigrationJournal journal = await LoadOrCreateJournalAsync(options, cancellationToken)
            .ConfigureAwait(false);
        if (journal.Records.Count > 0 &&
            journal.Records.All(static record => record.State == SecretMigrationRecordState.RolledBack))
        {
            throw new InvalidOperationException(
                "The secret migration journal was rolled back. Start a new migration in a new migration root.");
        }

        ControlPlaneSecretContinuityReport continuity = await controlPlaneContinuityVerifier
            .VerifyAsync(cancellationToken)
            .ConfigureAwait(false);
        foreach (SecretMigrationJournalRecord record in journal.Records)
        {
            await MigrateRecordAsync(options, journal, record, cancellationToken).ConfigureAwait(false);
        }

        return CreateReport(journal, options.DryRun, continuity.ProtectedPasswordCount);
    }

    public async Task<SecretMigrationReport> CompleteRestartCheckpointAsync(
        SecretMigrationOptions options,
        CancellationToken cancellationToken = default)
    {
        ValidateOptions(options);
        using IDisposable coordination = AcquireCoordination(options, cancellationToken);
        SecretMigrationJournal journal = LoadRequiredJournal(options);
        ControlPlaneSecretContinuityReport continuity = await controlPlaneContinuityVerifier
            .VerifyAsync(cancellationToken)
            .ConfigureAwait(false);
        foreach (SecretMigrationJournalRecord record in journal.Records)
        {
            if (record.State == SecretMigrationRecordState.ReferenceCommitted)
            {
                await VerifyCommittedDestinationAsync(record, cancellationToken).ConfigureAwait(false);
                record.State = SecretMigrationRecordState.RestartVerified;
                WriteJournal(options, journal);
                await AuditAsync(journal, record, SecretMigrationAuditStage.RestartVerified, cancellationToken)
                    .ConfigureAwait(false);
            }

            if (record.State == SecretMigrationRecordState.RestartVerified)
            {
                if (record.SourceVaultKey is not null)
                {
                    await sourceVault.DeleteAsync(record.SourceVaultKey, cancellationToken).ConfigureAwait(false);
                }

                record.State = SecretMigrationRecordState.SourceCleaned;
                WriteJournal(options, journal);
                await AuditAsync(journal, record, SecretMigrationAuditStage.SourceCleaned, cancellationToken)
                    .ConfigureAwait(false);
            }
        }

        return CreateReport(journal, DryRun: false, continuity.ProtectedPasswordCount);
    }

    public async Task<SecretMigrationReport> RollbackAsync(
        SecretMigrationOptions options,
        CancellationToken cancellationToken = default)
    {
        ValidateOptions(options);
        using IDisposable coordination = AcquireCoordination(options, cancellationToken);
        SecretMigrationJournal journal = LoadRequiredJournal(options);
        foreach (SecretMigrationJournalRecord record in journal.Records.AsEnumerable().Reverse())
        {
            try
            {
                if (record.State is SecretMigrationRecordState.SourceCleaned && record.SourceVaultKey is not null)
                {
                    throw new InvalidOperationException(
                        "A migrated vault source cannot be rolled back after the restart checkpoint deleted it.");
                }

                if (record.State is SecretMigrationRecordState.ReferenceCommitted or
                        SecretMigrationRecordState.RestartVerified ||
                    (record.State == SecretMigrationRecordState.SourceCleaned && record.SourceVaultKey is null))
                {
                    bool sourceReferenceRestored = await RestoreSourceReferenceAsync(record, cancellationToken)
                        .ConfigureAwait(false);
                    if (sourceReferenceRestored && interruptionObserver is not null)
                    {
                        await interruptionObserver.ObserveAsync(
                            record.SecretRecordId,
                            SecretMigrationInterruptionPoint.AfterSourceReferenceRestored,
                            cancellationToken).ConfigureAwait(false);
                    }
                }

                if (record.State != SecretMigrationRecordState.RolledBack)
                {
                    await destinationVault.DeleteAsync(record.DestinationVaultKey, cancellationToken)
                        .ConfigureAwait(false);
                }

                record.State = SecretMigrationRecordState.RolledBack;
                record.LastErrorCode = null;
                WriteJournal(options, journal);
                await AuditAsync(journal, record, SecretMigrationAuditStage.RolledBack, cancellationToken)
                    .ConfigureAwait(false);
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                string errorCode = exception is SecretMigrationException migrationException
                    ? migrationException.ErrorCode
                    : "rollback-operation-failed";
                record.LastErrorCode = errorCode;
                WriteJournal(options, journal);
                await AuditAsync(
                    journal,
                    record,
                    SecretMigrationAuditStage.Failed,
                    cancellationToken,
                    errorCode).ConfigureAwait(false);
                throw new SecretMigrationException(errorCode);
            }
        }

        ControlPlaneSecretContinuityReport continuity = await controlPlaneContinuityVerifier
            .VerifyAsync(cancellationToken)
            .ConfigureAwait(false);
        return CreateReport(journal, DryRun: false, continuity.ProtectedPasswordCount);
    }

    private async Task<SecretMigrationReport> RunDryRunAsync(
        SecretMigrationOptions options,
        CancellationToken cancellationToken)
    {
        Guid migrationId = Guid.NewGuid();
        List<SecretMigrationJournalRecord> records = await DiscoverCandidatesAsync(
            migrationId,
            options.SourceSelection,
            cancellationToken).ConfigureAwait(false);
        foreach (SecretMigrationJournalRecord record in records)
        {
            _ = await ResolveSourceValueAsync(record, cancellationToken).ConfigureAwait(false);
            await auditSink.RecordAsync(new SecretMigrationAuditEvent(
                migrationId,
                record.SecretRecordId,
                SecretMigrationAuditStage.DryRunVerified,
                clock.GetUtcNow()), cancellationToken).ConfigureAwait(false);
        }

        ControlPlaneSecretContinuityReport continuity = await controlPlaneContinuityVerifier
            .VerifyAsync(cancellationToken)
            .ConfigureAwait(false);
        return new SecretMigrationReport(
            migrationId,
            DryRun: true,
            records.Count,
            CommittedCount: 0,
            CleanedCount: 0,
            continuity.ProtectedPasswordCount,
            RequiresRestartCheckpoint: false);
    }

    private async Task<SecretMigrationJournal> LoadOrCreateJournalAsync(
        SecretMigrationOptions options,
        CancellationToken cancellationToken)
    {
        string journalPath = ResolveJournalPath(options);
        if (File.Exists(journalPath))
        {
            return ReadJournal(journalPath, options.SourceSelection);
        }

        var journal = new SecretMigrationJournal
        {
            SchemaVersion = CurrentSchemaVersion,
            MigrationId = Guid.NewGuid(),
            SourceSelection = options.SourceSelection,
            CreatedAtUtc = clock.GetUtcNow()
        };
        journal.Records.AddRange(await DiscoverCandidatesAsync(
            journal.MigrationId,
            options.SourceSelection,
            cancellationToken).ConfigureAwait(false));
        WriteJournal(options, journal);
        foreach (SecretMigrationJournalRecord record in journal.Records)
        {
            await AuditAsync(journal, record, SecretMigrationAuditStage.Prepared, cancellationToken)
                .ConfigureAwait(false);
        }

        return journal;
    }

    private async Task<List<SecretMigrationJournalRecord>> DiscoverCandidatesAsync(
        Guid migrationId,
        SecretMigrationSourceSelection selection,
        CancellationToken cancellationToken)
    {
        await using AppDbContext dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken)
            .ConfigureAwait(false);
        List<SecretRecord> records = await dbContext.Set<SecretRecord>()
            .AsNoTracking()
            .OrderBy(static record => record.Id)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
        return records
            .Select(record => CreateJournalRecord(migrationId, record, selection))
            .Where(static record => record is not null)
            .Cast<SecretMigrationJournalRecord>()
            .ToList();
    }

    private static SecretMigrationJournalRecord? CreateJournalRecord(
        Guid migrationId,
        SecretRecord record,
        SecretMigrationSourceSelection selection)
    {
        bool isVaultReference = SecretVaultRecordReference.TryParse(
            record.EncryptedPayload,
            out string sourceVaultKey);
        bool selected = selection switch
        {
            SecretMigrationSourceSelection.LegacyDataProtectionRecords => !isVaultReference,
            SecretMigrationSourceSelection.VaultReferences => isVaultReference,
            SecretMigrationSourceSelection.All => true,
            _ => false
        };
        if (!selected)
        {
            return null;
        }

        return new SecretMigrationJournalRecord
        {
            SecretRecordId = record.Id,
            SourcePayload = record.EncryptedPayload,
            SourceVaultKey = isVaultReference ? sourceVaultKey : null,
            DestinationVaultKey = $"migration/{migrationId:N}/{record.Id:N}",
            State = SecretMigrationRecordState.Prepared
        };
    }

    private async Task MigrateRecordAsync(
        SecretMigrationOptions options,
        SecretMigrationJournal journal,
        SecretMigrationJournalRecord record,
        CancellationToken cancellationToken)
    {
        try
        {
            if (record.State == SecretMigrationRecordState.Prepared)
            {
                string value = await ResolveSourceValueAsync(record, cancellationToken).ConfigureAwait(false);
                await destinationVault.SetAsync(record.DestinationVaultKey, value, cancellationToken)
                    .ConfigureAwait(false);
                record.State = SecretMigrationRecordState.DestinationStaged;
                record.LastErrorCode = null;
                WriteJournal(options, journal);
                await AuditAsync(journal, record, SecretMigrationAuditStage.DestinationStaged, cancellationToken)
                    .ConfigureAwait(false);
            }

            if (record.State == SecretMigrationRecordState.DestinationStaged)
            {
                string sourceValue = await ResolveSourceValueAsync(record, cancellationToken).ConfigureAwait(false);
                string? destinationValue = await destinationVault
                    .GetAsync(record.DestinationVaultKey, cancellationToken)
                    .ConfigureAwait(false);
                if (!ValuesEqual(sourceValue, destinationValue))
                {
                    throw new SecretMigrationException("destination-verification-failed");
                }

                record.State = SecretMigrationRecordState.DestinationVerified;
                WriteJournal(options, journal);
                await AuditAsync(journal, record, SecretMigrationAuditStage.DestinationVerified, cancellationToken)
                    .ConfigureAwait(false);
            }

            if (record.State == SecretMigrationRecordState.DestinationVerified)
            {
                await CommitReferenceAsync(record, cancellationToken).ConfigureAwait(false);
                record.State = SecretMigrationRecordState.ReferenceCommitted;
                WriteJournal(options, journal);
                await AuditAsync(journal, record, SecretMigrationAuditStage.ReferenceCommitted, cancellationToken)
                    .ConfigureAwait(false);
            }
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            string errorCode = exception is SecretMigrationException migrationException
                ? migrationException.ErrorCode
                : "migration-operation-failed";
            record.LastErrorCode = errorCode;
            WriteJournal(options, journal);
            await AuditAsync(
                journal,
                record,
                SecretMigrationAuditStage.Failed,
                cancellationToken,
                errorCode).ConfigureAwait(false);
            throw new SecretMigrationException(errorCode);
        }
    }

    private async Task<string> ResolveSourceValueAsync(
        SecretMigrationJournalRecord record,
        CancellationToken cancellationToken)
    {
        if (record.SourceVaultKey is null)
        {
            try
            {
                return legacyDataProtectionProtector.Unprotect(record.SourcePayload);
            }
            catch (Exception)
            {
                throw new SecretMigrationException("legacy-data-protection-unreadable");
            }
        }

        try
        {
            return await sourceVault.GetAsync(record.SourceVaultKey, cancellationToken).ConfigureAwait(false)
                ?? throw new SecretMigrationException("source-vault-payload-missing");
        }
        catch (SecretMigrationException)
        {
            throw;
        }
        catch (Exception)
        {
            throw new SecretMigrationException("source-vault-unreadable");
        }
    }

    private async Task CommitReferenceAsync(
        SecretMigrationJournalRecord record,
        CancellationToken cancellationToken)
    {
        await using AppDbContext dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken)
            .ConfigureAwait(false);
        SecretRecord entity = await dbContext.Set<SecretRecord>()
            .SingleOrDefaultAsync(item => item.Id == record.SecretRecordId, cancellationToken)
            .ConfigureAwait(false)
            ?? throw new SecretMigrationException("source-record-missing");
        string destinationReference = SecretVaultRecordReference.Create(record.DestinationVaultKey);
        if (string.Equals(entity.EncryptedPayload, destinationReference, StringComparison.Ordinal))
        {
            return;
        }

        if (!string.Equals(entity.EncryptedPayload, record.SourcePayload, StringComparison.Ordinal))
        {
            throw new SecretMigrationException("source-record-changed");
        }

        entity.EncryptedPayload = destinationReference;
        await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    private async Task VerifyCommittedDestinationAsync(
        SecretMigrationJournalRecord record,
        CancellationToken cancellationToken)
    {
        await using AppDbContext dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken)
            .ConfigureAwait(false);
        string? payload = await dbContext.Set<SecretRecord>()
            .Where(item => item.Id == record.SecretRecordId)
            .Select(static item => item.EncryptedPayload)
            .SingleOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);
        if (!string.Equals(
                payload,
                SecretVaultRecordReference.Create(record.DestinationVaultKey),
                StringComparison.Ordinal))
        {
            throw new SecretMigrationException("committed-reference-mismatch");
        }

        string sourceValue = await ResolveSourceValueAsync(record, cancellationToken).ConfigureAwait(false);
        string? destinationValue = await destinationVault
            .GetAsync(record.DestinationVaultKey, cancellationToken)
            .ConfigureAwait(false);
        if (!ValuesEqual(sourceValue, destinationValue))
        {
            throw new SecretMigrationException("committed-destination-verification-failed");
        }
    }

    private async Task<bool> RestoreSourceReferenceAsync(
        SecretMigrationJournalRecord record,
        CancellationToken cancellationToken)
    {
        _ = await ResolveSourceValueAsync(record, cancellationToken).ConfigureAwait(false);
        await using AppDbContext dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken)
            .ConfigureAwait(false);
        SecretRecord entity = await dbContext.Set<SecretRecord>()
            .SingleOrDefaultAsync(item => item.Id == record.SecretRecordId, cancellationToken)
            .ConfigureAwait(false)
            ?? throw new SecretMigrationException("source-record-missing");
        if (string.Equals(entity.EncryptedPayload, record.SourcePayload, StringComparison.Ordinal))
        {
            return false;
        }

        string destinationReference = SecretVaultRecordReference.Create(record.DestinationVaultKey);
        if (!string.Equals(entity.EncryptedPayload, destinationReference, StringComparison.Ordinal))
        {
            throw new SecretMigrationException("rollback-reference-mismatch");
        }

        entity.EncryptedPayload = record.SourcePayload;
        await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return true;
    }

    private static bool ValuesEqual(string source, string? destination)
    {
        if (destination is null)
        {
            return false;
        }

        byte[] sourceBytes = Encoding.UTF8.GetBytes(source);
        byte[] destinationBytes = Encoding.UTF8.GetBytes(destination);
        try
        {
            return CryptographicOperations.FixedTimeEquals(sourceBytes, destinationBytes);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(sourceBytes);
            CryptographicOperations.ZeroMemory(destinationBytes);
        }
    }

    private IDisposable AcquireCoordination(
        SecretMigrationOptions options,
        CancellationToken cancellationToken)
    {
        durableFileWriter.EnsureDirectory(
            options.MigrationRoot,
            options.MigrationRoot,
            requirePrivateUnixMode: true);
        return durableFileWriter.AcquireCoordination(
            options.MigrationRoot,
            Path.Combine(options.MigrationRoot, CoordinationFileName),
            DurableFileWriteOptions.Private.LockTimeout,
            requirePrivateUnixMode: true,
            cancellationToken);
    }

    private SecretMigrationJournal LoadRequiredJournal(SecretMigrationOptions options)
    {
        string path = ResolveJournalPath(options);
        if (!File.Exists(path))
        {
            throw new InvalidOperationException("No secret migration journal is available for this operation.");
        }

        return ReadJournal(path, options.SourceSelection);
    }

    private static SecretMigrationJournal ReadJournal(
        string path,
        SecretMigrationSourceSelection expectedSourceSelection)
    {
        SecretMigrationJournal journal;
        try
        {
            journal = JsonSerializer.Deserialize<SecretMigrationJournal>(File.ReadAllText(path), SerializerOptions)
                ?? throw new InvalidDataException("The secret migration journal is empty.");
        }
        catch (JsonException exception)
        {
            throw new InvalidDataException("The secret migration journal is invalid.", exception);
        }

        if (journal.SchemaVersion != CurrentSchemaVersion ||
            journal.MigrationId == Guid.Empty ||
            journal.CreatedAtUtc == default ||
            !Enum.IsDefined(journal.SourceSelection) ||
            journal.SourceSelection != expectedSourceSelection ||
            journal.Records is null)
        {
            throw new InvalidDataException("The secret migration journal schema is unsupported.");
        }

        var recordIds = new HashSet<Guid>();
        var destinationKeys = new HashSet<string>(StringComparer.Ordinal);
        foreach (SecretMigrationJournalRecord record in journal.Records)
        {
            bool sourceIsVaultReference = SecretVaultRecordReference.TryParse(
                record.SourcePayload,
                out string sourceVaultKey);
            bool sourceMatchesSelection = journal.SourceSelection switch
            {
                SecretMigrationSourceSelection.LegacyDataProtectionRecords => !sourceIsVaultReference,
                SecretMigrationSourceSelection.VaultReferences => sourceIsVaultReference,
                SecretMigrationSourceSelection.All => true,
                _ => false
            };
            string expectedDestinationKey = $"migration/{journal.MigrationId:N}/{record.SecretRecordId:N}";
            if (record.SecretRecordId == Guid.Empty ||
                string.IsNullOrEmpty(record.SourcePayload) ||
                string.IsNullOrWhiteSpace(record.DestinationVaultKey) ||
                !Enum.IsDefined(record.State) ||
                !recordIds.Add(record.SecretRecordId) ||
                !destinationKeys.Add(record.DestinationVaultKey) ||
                !string.Equals(record.DestinationVaultKey, expectedDestinationKey, StringComparison.Ordinal) ||
                !sourceMatchesSelection ||
                sourceIsVaultReference != (record.SourceVaultKey is not null) ||
                (sourceIsVaultReference &&
                    !string.Equals(sourceVaultKey, record.SourceVaultKey, StringComparison.Ordinal)))
            {
                throw new InvalidDataException("The secret migration journal contains invalid authority state.");
            }
        }

        return journal;
    }

    private void WriteJournal(SecretMigrationOptions options, SecretMigrationJournal journal)
    {
        durableFileWriter.WriteText(
            options.MigrationRoot,
            ResolveJournalPath(options),
            JsonSerializer.Serialize(journal, SerializerOptions),
            DurableFileWriteOptions.Private with
            {
                CreateBackup = true
            });
    }

    private static string ResolveJournalPath(SecretMigrationOptions options)
        => Path.Combine(Path.GetFullPath(options.MigrationRoot), JournalFileName);

    private ValueTask AuditAsync(
        SecretMigrationJournal journal,
        SecretMigrationJournalRecord record,
        SecretMigrationAuditStage stage,
        CancellationToken cancellationToken,
        string? errorCode = null)
        => auditSink.RecordAsync(new SecretMigrationAuditEvent(
            journal.MigrationId,
            record.SecretRecordId,
            stage,
            clock.GetUtcNow(),
            errorCode), cancellationToken);

    private static SecretMigrationReport CreateReport(
        SecretMigrationJournal journal,
        bool DryRun,
        int controlPlaneProtectedPasswordCount)
        => new(
            journal.MigrationId,
            DryRun,
            journal.Records.Count,
            journal.Records.Count(static record => record.State is
                SecretMigrationRecordState.ReferenceCommitted or
                SecretMigrationRecordState.RestartVerified or
                SecretMigrationRecordState.SourceCleaned),
            journal.Records.Count(static record => record.State == SecretMigrationRecordState.SourceCleaned),
            controlPlaneProtectedPasswordCount,
            journal.Records.Any(static record => record.State == SecretMigrationRecordState.ReferenceCommitted));

    private static void ValidateOptions(SecretMigrationOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentException.ThrowIfNullOrWhiteSpace(options.MigrationRoot);
        PhysicalPathSyntaxPolicy.EnsureNativeOrRelative(options.MigrationRoot, "secret migration root");
        if (!Path.IsPathRooted(options.MigrationRoot))
        {
            throw new ArgumentException(
                "The secret migration root must be an absolute native-host path.",
                nameof(options));
        }
    }

    private static JsonSerializerOptions CreateSerializerOptions()
    {
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web)
        {
            WriteIndented = true
        };
        options.Converters.Add(new JsonStringEnumConverter());
        return options;
    }

    private sealed class SecretMigrationJournal
    {
        public int SchemaVersion { get; set; }

        public Guid MigrationId { get; set; }

        public SecretMigrationSourceSelection SourceSelection { get; set; }

        public DateTimeOffset CreatedAtUtc { get; set; }

        public List<SecretMigrationJournalRecord> Records { get; set; } = [];
    }

    private sealed class SecretMigrationJournalRecord
    {
        public Guid SecretRecordId { get; set; }

        public string SourcePayload { get; set; } = string.Empty;

        public string? SourceVaultKey { get; set; }

        public string DestinationVaultKey { get; set; } = string.Empty;

        public SecretMigrationRecordState State { get; set; }

        public string? LastErrorCode { get; set; }
    }
}

public sealed class SecretMigrationException : InvalidOperationException
{
    public SecretMigrationException(string errorCode)
        : base($"Secret migration failed ({errorCode}).")
    {
        ErrorCode = errorCode;
    }

    public string ErrorCode { get; }
}
