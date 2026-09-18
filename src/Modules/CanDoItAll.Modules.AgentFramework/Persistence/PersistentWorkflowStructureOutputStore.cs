using System.Data;
using System.Text.Json;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.AgentFramework.Workflows.Abstractions;
using CanDoItAll.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Npgsql;

namespace CanDoItAll.Modules.AgentFramework;

public sealed class PersistentWorkflowStructureOutputStore(
    IDbContextFactory<WorkflowDbContext> factory, TimeProvider clock,
    IWorkflowStructureSourceAuthorityPolicy? sourceAuthority = null,
    CoordinatedDatabaseTransaction? transactions = null,
    DbContextOptions<WorkflowDbContext>? contextOptions = null) : IWorkflowStructureOutputStore {
    private const int MaximumOutputsPerRun = 4096;
    private const string OutputIdentityConstraintName = "PK_AgentFramework_WorkflowStructureOutputs";
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public async Task<WorkflowStructureOutput?> FindAsync(WorkflowStructureOutputIdentity identity, CancellationToken cancellationToken = default) {
        await using var database = await factory.CreateDbContextAsync(cancellationToken);
        var row = await FindAsync(database, identity, cancellationToken);
        return row is null ? null : Map(row);
    }

    public async Task<WorkflowProjectLifetime?> FindProjectLifetimeAsync(WorkflowRunId runId, Guid projectId,
        CancellationToken cancellationToken = default) {
        await using var database = await factory.CreateDbContextAsync(cancellationToken);
        return await FindProjectLifetimeAsync(database, runId, projectId, cancellationToken);
    }

    public async Task<WorkflowStructureOutput> PrepareAsync(WorkflowStructureOutputPlan plan, CancellationToken cancellationToken = default) {
        for (var attempt = 0; ; attempt++) {
            try {
                return await PrepareCoreAsync(plan, cancellationToken);
            } catch (Exception exception) when (attempt < 2 &&
                    (SerializableMutationScope.IsConflict(exception) || IsOutputIdentityConflict(exception))) {
                cancellationToken.ThrowIfCancellationRequested();
            }
        }
    }

    private static bool IsOutputIdentityConflict(Exception exception) => exception is DbUpdateException {
        InnerException: PostgresException {
            SqlState: PostgresErrorCodes.UniqueViolation,
            ConstraintName: OutputIdentityConstraintName
        }
    };

    private async Task<WorkflowStructureOutput> PrepareCoreAsync(WorkflowStructureOutputPlan plan, CancellationToken cancellationToken) {
        Validate(plan);
        var target = plan.ProjectLifetime ?? throw new WorkflowStructureLegacyLineageException();
        await using var database = await factory.CreateDbContextAsync(cancellationToken);
        var initialRun = await database.Set<WorkflowRunRecordEntity>().AsNoTracking()
            .SingleOrDefaultAsync(run => run.RunId == plan.Identity.Occurrence.RunId.Value, cancellationToken)
            ?? throw new InvalidOperationException("A Workflow output requires its persisted run.");
        var authority = RequireAuthority(initialRun, plan);
        await using var source = await (sourceAuthority ?? throw new InvalidOperationException("Workflow output preparation requires its original source policy."))
            .AcquireAsync(authority, Use(plan.Kind), target, cancellationToken);
        var inMemory = WorkflowPersistenceProvider.IsInMemory(database);
        await using var mutation = inMemory ? await SerializableMutationScope.BeginAsync(database, Scope(plan.Identity.Occurrence.RunId), cancellationToken) : null;
        await using var transaction = inMemory ? null : await database.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);
        using var coordination = (transactions ?? throw new InvalidOperationException("Workflow output preparation requires the shared transaction coordinator.")).Enter(database);
        await source.RequireForMutationAsync(cancellationToken);
        if (!inMemory) {
            await SerializableMutationScope.AcquireRelationalScopeLocksAsync(database, [Scope(plan.Identity.Occurrence.RunId)], cancellationToken);
        }
        await RequireRunForMutationAsync(database, plan, cancellationToken);
        var row = await FindAsync(database, plan.Identity, cancellationToken);
        if (row is not null) {
            if (row.PlanJson != JsonSerializer.Serialize(plan, JsonOptions)) {
                throw new WorkflowStructureOutputConflictException();
            }
            return Map(row);
        }
        var existingTarget = await FindProjectLifetimeAsync(database, plan.Identity.Occurrence.RunId, plan.ProjectId, cancellationToken);
        if (existingTarget is not null && existingTarget != target) {
            throw new WorkflowStructureOutputConflictException();
        }
        var runId = plan.Identity.Occurrence.RunId.Value;
        if (await database.Set<WorkflowStructureOutputRecord>().CountAsync(output => output.RunId == runId, cancellationToken) >= MaximumOutputsPerRun) {
            throw new InvalidOperationException("The workflow output manifest has reached its bounded output limit.");
        }
        row = new WorkflowStructureOutputRecord {
            RunId = runId,
            OccurrencePath = plan.Identity.Occurrence.Path,
            Slot = plan.Identity.Slot,
            PlanJson = JsonSerializer.Serialize(plan, JsonOptions),
            DatabaseProfileId = target.DatabaseProfileId,
            ProjectId = target.ProjectId,
            ProjectLifetimeId = target.LifetimeId,
            StoragePlacementIntentId = plan.Kind == WorkflowStructureOutputKind.Asset ? Guid.NewGuid() : null,
            NextInspectionAtUtc = clock.GetUtcNow()
        };
        database.Add(row);
        await database.SaveChangesAsync(cancellationToken);
        await source.RequireForMutationAsync(cancellationToken);
        if (transaction is not null) {
            await transaction.CommitAsync(cancellationToken);
        } else {
            await mutation!.CommitAsync(cancellationToken);
        }
        coordination.Dispose();
        return Map(row);
    }

    public async Task RequireForMutationAsync(WorkflowStructureOutputPlan plan, CancellationToken cancellationToken = default) {
        Validate(plan);
        await using var database = await (transactions ?? throw new InvalidOperationException("Workflow native delivery requires transaction coordination."))
            .CreateEnlistedAsync(contextOptions ?? throw new InvalidOperationException("Workflow native delivery requires its owner context options."),
                static options => new WorkflowDbContext(options), cancellationToken);
        await RequireRunForMutationAsync(database, plan, cancellationToken);
        var row = await FindAsync(database, plan.Identity, cancellationToken)
            ?? throw new InvalidOperationException("The native Workflow effect has no prior owner manifest admission.");
        if (row.PlanJson != JsonSerializer.Serialize(plan, JsonOptions)) {
            throw new WorkflowStructureOutputConflictException();
        }
    }

    private static async Task<WorkflowProjectLifetime?> FindProjectLifetimeAsync(WorkflowDbContext database,
        WorkflowRunId runId, Guid projectId, CancellationToken cancellationToken) {
        var targets = await database.Set<WorkflowStructureOutputRecord>().AsNoTracking()
            .Where(row => row.RunId == runId.Value && row.ProjectId == projectId)
            .Select(row => new { row.DatabaseProfileId, row.ProjectId, row.ProjectLifetimeId }).Distinct().Take(2).ToListAsync(cancellationToken);
        if (targets.Count == 0) {
            return null;
        }
        if (targets.Count != 1 || targets[0] is not { DatabaseProfileId: { } profile, ProjectId: { } project, ProjectLifetimeId: { } lifetime }) {
            throw new InvalidOperationException("The Workflow run has inconsistent or incomplete saved project lifetime bindings.");
        }
        return new(profile, project, lifetime);
    }

    private static async Task RequireRunForMutationAsync(WorkflowDbContext database, WorkflowStructureOutputPlan plan,
        CancellationToken cancellationToken) {
        var runs = database.Database.IsNpgsql() ? database.Set<WorkflowRunRecordEntity>().FromSqlInterpolated(
            $"SELECT * FROM \"AgentFramework_WorkflowRuns\" WHERE \"RunId\" = {plan.Identity.Occurrence.RunId.Value} FOR SHARE")
            : database.Set<WorkflowRunRecordEntity>();
        var run = await runs.AsNoTracking().SingleOrDefaultAsync(row => row.RunId == plan.Identity.Occurrence.RunId.Value, cancellationToken)
            ?? throw new InvalidOperationException("The Workflow output run no longer exists.");
        _ = RequireAuthority(run, plan);
        if (run.State == WorkflowRunState.Cancelled) {
            throw new WorkflowStructureOutputCancelledException();
        }
    }

    private static WorkflowStructureAuthority RequireAuthority(WorkflowRunRecordEntity run, WorkflowStructureOutputPlan plan) {
        var authority = run.ToSnapshot().Origin?.StructureAuthority;
        if (authority?.ProjectScope is null || plan.ProjectLifetime is not { } target) {
            throw new WorkflowStructureLegacyLineageException();
        }
        if (run.VersionId != plan.WorkflowVersionId.Value || target.ProjectId != plan.ProjectId ||
                WorkflowStructureAuthorityFingerprint.Create(authority) != plan.SourceAuthorityFingerprint ||
                authority.ProjectScope.Find(plan.ProjectId) is { } expected && expected != target ||
                authority.ProjectScope.Find(plan.ProjectId) is null && !authority.AllProjects) {
            throw new WorkflowStructureOutputConflictException();
        }
        return authority;
    }

    private static WorkflowStructureAuthorityUse Use(WorkflowStructureOutputKind kind) => kind switch {
        WorkflowStructureOutputKind.Task => WorkflowStructureAuthorityUse.TaskOutput,
        WorkflowStructureOutputKind.Asset => WorkflowStructureAuthorityUse.AssetOutput,
        _ => throw new ArgumentOutOfRangeException(nameof(kind))
    };

    public async Task CompleteAsync(WorkflowStructureOutputReceipt receipt, CancellationToken cancellationToken = default) {
        ArgumentNullException.ThrowIfNull(receipt);
        await using var database = await factory.CreateDbContextAsync(cancellationToken);
        await using var transaction = await SerializableMutationScope.BeginAsync(database,
            Scope(receipt.Identity.Occurrence.RunId), cancellationToken);
        var row = await FindAsync(database, receipt.Identity, cancellationToken)
            ?? throw new InvalidOperationException("The workflow output was not prepared before destination dispatch.");
        var plan = Map(row).Plan;
        var json = JsonSerializer.Serialize(receipt, JsonOptions);
        if (plan.Fingerprint != receipt.Fingerprint || plan.ProjectId != receipt.ProjectId || plan.ProjectLifetime != receipt.ProjectLifetime ||
            row.ReceiptJson.Length > 0 && row.ReceiptJson != json) {
            throw new WorkflowStructureOutputConflictException();
        }

        if (row.ReceiptJson.Length > 0) {
            return;
        }

        row.ReceiptJson = json;
        row.IsComplete = true;
        await database.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<WorkflowStructureOutput>> ListAsync(WorkflowRunId runId, CancellationToken cancellationToken = default) {
        await using var database = await factory.CreateDbContextAsync(cancellationToken);
        var rows = await database.Set<WorkflowStructureOutputRecord>().AsNoTracking()
            .Where(row => row.RunId == runId.Value)
            .OrderBy(row => row.OccurrencePath).ThenBy(row => row.Slot)
            .Take(MaximumOutputsPerRun + 1).ToListAsync(cancellationToken);
        if (rows.Count > MaximumOutputsPerRun) {
            throw new InvalidOperationException("The saved workflow output manifest exceeds its supported limit.");
        }

        return rows.Select(Map).ToArray();
    }

    public async Task<IReadOnlyList<WorkflowStructureOutput>> ListPendingAsync(int take, CancellationToken cancellationToken = default) {
        if (take is < 1 or > 128) {
            throw new ArgumentOutOfRangeException(nameof(take));
        }

        await using var database = await factory.CreateDbContextAsync(cancellationToken);
        var now = clock.GetUtcNow();
        var rows = await database.Set<WorkflowStructureOutputRecord>().AsNoTracking()
            .Where(row => !row.IsComplete && row.NextInspectionAtUtc <= now)
            .OrderBy(row => row.NextInspectionAtUtc).ThenBy(row => row.RunId).ThenBy(row => row.OccurrencePath).ThenBy(row => row.Slot)
            .Take(take).ToListAsync(cancellationToken);
        return rows.Select(Map).ToArray();
    }

    public async Task DeferInspectionAsync(WorkflowStructureOutputIdentity identity, CancellationToken cancellationToken = default) {
        await using var database = await factory.CreateDbContextAsync(cancellationToken);
        await using var transaction = await SerializableMutationScope.BeginAsync(database, Scope(identity.Occurrence.RunId), cancellationToken);
        var row = await FindAsync(database, identity, cancellationToken)
            ?? throw new InvalidOperationException("The workflow output inspection has no prepared intent.");
        row.NextInspectionAtUtc = clock.GetUtcNow().AddSeconds(30);
        await database.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
    }

    public async Task<bool> TryBeginAssetDispatchAsync(WorkflowStructureOutputIdentity identity, CancellationToken cancellationToken = default) {
        await using var database = await factory.CreateDbContextAsync(cancellationToken);
        await using var transaction = await SerializableMutationScope.BeginAsync(database, Scope(identity.Occurrence.RunId), cancellationToken);
        var row = await FindAsync(database, identity, cancellationToken)
            ?? throw new InvalidOperationException("The asset output must be prepared before dispatch.");
        if (Map(row).Plan.Kind != WorkflowStructureOutputKind.Asset) {
            throw new InvalidOperationException("Only asset outputs require a storage dispatch claim.");
        }

        if (row.AssetDispatchStarted) {
            return false;
        }

        row.AssetDispatchStarted = true;
        await database.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return true;
    }

    private static Task<WorkflowStructureOutputRecord?> FindAsync(WorkflowDbContext database,
        WorkflowStructureOutputIdentity identity, CancellationToken cancellationToken)
        => database.Set<WorkflowStructureOutputRecord>().SingleOrDefaultAsync(row =>
            row.RunId == identity.Occurrence.RunId.Value && row.OccurrencePath == identity.Occurrence.Path && row.Slot == identity.Slot,
            cancellationToken);

    private static string Scope(WorkflowRunId runId) => $"workflow-structure-outputs:{runId.Value:N}";

    private static WorkflowStructureOutput Map(WorkflowStructureOutputRecord row) {
        var plan = JsonSerializer.Deserialize<WorkflowStructureOutputPlan>(row.PlanJson, JsonOptions)
            ?? throw new InvalidOperationException("The saved workflow output plan is missing.");
        var receipt = row.ReceiptJson.Length == 0 ? null
            : JsonSerializer.Deserialize<WorkflowStructureOutputReceipt>(row.ReceiptJson, JsonOptions)
                ?? throw new InvalidOperationException("The saved workflow output receipt is missing.");
        if (plan.Identity.Occurrence.RunId.Value != row.RunId || plan.Identity.Occurrence.Path != row.OccurrencePath ||
            plan.Identity.Slot != row.Slot || row.IsComplete != (receipt is not null) ||
            plan.ProjectLifetime is { } target && (target.DatabaseProfileId != row.DatabaseProfileId ||
                target.ProjectId != row.ProjectId || target.LifetimeId != row.ProjectLifetimeId) ||
            plan.ProjectLifetime is null && (row.DatabaseProfileId.HasValue || row.ProjectId.HasValue || row.ProjectLifetimeId.HasValue) ||
            receipt is not null && receipt.ProjectLifetime != plan.ProjectLifetime) {
            throw new InvalidOperationException("The saved workflow manifest identity or completion state is inconsistent.");
        }

        return new(plan, receipt is null ? WorkflowStructureOutputState.Prepared : WorkflowStructureOutputState.Applied, receipt) {
            StoragePlacementIntentId = row.StoragePlacementIntentId
        };
    }

    private static void Validate(WorkflowStructureOutputPlan plan) {
        ArgumentNullException.ThrowIfNull(plan);
        ArgumentNullException.ThrowIfNull(plan.Identity);
        ArgumentNullException.ThrowIfNull(plan.Identity.Occurrence);
        if (plan.ProjectLifetime is null || plan.SourceAuthorityFingerprint is null) {
            throw new WorkflowStructureLegacyLineageException();
        }
        if (plan.Identity.Slot is < 0 or > 4095 || plan.ProjectId == Guid.Empty ||
            plan.WorkflowVersionId.Value == Guid.Empty || string.IsNullOrWhiteSpace(plan.StepId.Value) ||
            string.IsNullOrWhiteSpace(plan.ParentNodeId.Value) || !Enum.IsDefined(plan.Kind) || !Enum.IsDefined(plan.Role) ||
            !IsHash(plan.Fingerprint) || !IsHash(plan.TargetBindingFingerprint) ||
            plan.ProjectLifetime.ProjectId != plan.ProjectId || !IsHash(plan.SourceAuthorityFingerprint)) {
            throw new ArgumentException("The workflow output plan has invalid identity, scope, kind or fingerprints.", nameof(plan));
        }
    }

    private static bool IsHash(string value) => value is { Length: 64 } && value.All(char.IsAsciiHexDigit);
}

public sealed class WorkflowStructureOutputRecord {
    public Guid RunId { get; set; }
    public string OccurrencePath { get; set; } = string.Empty;
    public int Slot { get; set; }
    public string PlanJson { get; set; } = string.Empty;
    public string ReceiptJson { get; set; } = string.Empty;
    public DateTimeOffset NextInspectionAtUtc { get; set; }
    public bool AssetDispatchStarted { get; set; }
    public Guid? StoragePlacementIntentId { get; set; }
    public bool IsComplete { get; set; }
    public Guid? DatabaseProfileId { get; set; }
    public Guid? ProjectId { get; set; }
    public Guid? ProjectLifetimeId { get; set; }
}

internal sealed class WorkflowStructureOutputRecordConfiguration : IEntityTypeConfiguration<WorkflowStructureOutputRecord> {
    public void Configure(EntityTypeBuilder<WorkflowStructureOutputRecord> builder) {
        builder.ToTable("AgentFramework_WorkflowStructureOutputs");
        builder.HasKey(row => new { row.RunId, row.OccurrencePath, row.Slot });
        builder.HasIndex(row => new { row.RunId, row.ProjectId });
        builder.Property(row => row.OccurrencePath).HasMaxLength(64).IsRequired();
        builder.Property(row => row.PlanJson).HasColumnType("TEXT").IsRequired();
        builder.Property(row => row.ReceiptJson).HasColumnType("TEXT").IsRequired();
        builder.HasIndex(row => new { row.IsComplete, row.NextInspectionAtUtc, row.RunId, row.OccurrencePath, row.Slot });
    }
}
