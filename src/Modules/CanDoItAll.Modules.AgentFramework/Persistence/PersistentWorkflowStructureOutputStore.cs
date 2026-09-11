using System.Text.Json;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.AgentFramework.Workflows.Abstractions;
using CanDoItAll.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CanDoItAll.Modules.AgentFramework;

public sealed class PersistentWorkflowStructureOutputStore(
    IDbContextFactory<WorkflowDbContext> factory, TimeProvider clock) : IWorkflowStructureOutputStore {
    private const int MaximumOutputsPerRun = 4096;
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public async Task<WorkflowStructureOutput?> FindAsync(WorkflowStructureOutputIdentity identity, CancellationToken cancellationToken = default) {
        await using var database = await factory.CreateDbContextAsync(cancellationToken);
        var row = await FindAsync(database, identity, cancellationToken);
        return row is null ? null : Map(row);
    }

    public async Task<WorkflowStructureOutput> PrepareAsync(WorkflowStructureOutputPlan plan, CancellationToken cancellationToken = default) {
        Validate(plan);
        await using var database = await factory.CreateDbContextAsync(cancellationToken);
        await using var transaction = await SerializableMutationScope.BeginAsync(database,
            Scope(plan.Identity.Occurrence.RunId), cancellationToken);
        var row = await FindAsync(database, plan.Identity, cancellationToken);
        if (row is not null) {
            if (row.PlanJson != JsonSerializer.Serialize(plan, JsonOptions)) {
                throw new WorkflowStructureOutputConflictException();
            }

            return Map(row);
        }

        var runId = plan.Identity.Occurrence.RunId.Value;
        if (!await database.Set<WorkflowRunRecordEntity>().AsNoTracking()
                .AnyAsync(run => run.RunId == runId && run.VersionId == plan.WorkflowVersionId.Value, cancellationToken)) {
            throw new InvalidOperationException("A workflow output requires its persisted run and exact definition version.");
        }

        if (await database.Set<WorkflowStructureOutputRecord>().CountAsync(output => output.RunId == runId, cancellationToken)
            >= MaximumOutputsPerRun) {
            throw new InvalidOperationException("The workflow output manifest has reached its bounded output limit.");
        }

        row = new WorkflowStructureOutputRecord {
            RunId = runId,
            OccurrencePath = plan.Identity.Occurrence.Path,
            Slot = plan.Identity.Slot,
            PlanJson = JsonSerializer.Serialize(plan, JsonOptions),
            StoragePlacementIntentId = plan.Kind == WorkflowStructureOutputKind.Asset ? Guid.NewGuid() : null,
            NextInspectionAtUtc = clock.GetUtcNow()
        };
        database.Add(row);
        await database.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return Map(row);
    }

    public async Task CompleteAsync(WorkflowStructureOutputReceipt receipt, CancellationToken cancellationToken = default) {
        ArgumentNullException.ThrowIfNull(receipt);
        await using var database = await factory.CreateDbContextAsync(cancellationToken);
        await using var transaction = await SerializableMutationScope.BeginAsync(database,
            Scope(receipt.Identity.Occurrence.RunId), cancellationToken);
        var row = await FindAsync(database, receipt.Identity, cancellationToken)
            ?? throw new InvalidOperationException("The workflow output was not prepared before destination dispatch.");
        var plan = Map(row).Plan;
        var json = JsonSerializer.Serialize(receipt, JsonOptions);
        if (plan.Fingerprint != receipt.Fingerprint || plan.ProjectId != receipt.ProjectId ||
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
            plan.Identity.Slot != row.Slot || row.IsComplete != (receipt is not null)) {
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
        if (plan.Identity.Slot is < 0 or > 4095 || plan.ProjectId == Guid.Empty ||
            plan.WorkflowVersionId.Value == Guid.Empty || string.IsNullOrWhiteSpace(plan.StepId.Value) ||
            string.IsNullOrWhiteSpace(plan.ParentNodeId.Value) || !Enum.IsDefined(plan.Kind) || !Enum.IsDefined(plan.Role) ||
            !IsHash(plan.Fingerprint) || !IsHash(plan.TargetBindingFingerprint)) {
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
}

internal sealed class WorkflowStructureOutputRecordConfiguration : IEntityTypeConfiguration<WorkflowStructureOutputRecord> {
    public void Configure(EntityTypeBuilder<WorkflowStructureOutputRecord> builder) {
        builder.ToTable("AgentFramework_WorkflowStructureOutputs");
        builder.HasKey(row => new { row.RunId, row.OccurrencePath, row.Slot });
        builder.Property(row => row.OccurrencePath).HasMaxLength(64).IsRequired();
        builder.Property(row => row.PlanJson).HasColumnType("TEXT").IsRequired();
        builder.Property(row => row.ReceiptJson).HasColumnType("TEXT").IsRequired();
        builder.HasIndex(row => new { row.IsComplete, row.NextInspectionAtUtc, row.RunId, row.OccurrencePath, row.Slot });
    }
}
