using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using CanDoItAll.Infrastructure.Storage;
using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.AgentFramework.Workflows.Abstractions;
using CanDoItAll.Infrastructure.Persistence;
using CanDoItAll.SharedKernel;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CanDoItAll.Modules.Workbench;

public sealed record ProjectWorkflowContributionResult(
    ProjectStructureNode Node,
    WorkflowStructureOutputReceipt? Receipt,
    bool WasReplay,
    bool TargetDeleted) {
    [JsonIgnore]
    public Exception? StorageObservationException { get; init; }
}

public sealed partial class ProjectWorkbenchService {
    private static readonly JsonSerializerOptions WorkflowContributionJson = new(JsonSerializerDefaults.Web);

    public async Task<ProjectWorkflowContributionResult> CreateWorkflowContributionAsync(
        WorkflowStructureOutputPlan plan,
        ProjectObjectCreateRequest request,
        CancellationToken cancellationToken = default,
        Guid? storagePlacementIntentId = null) {
        ArgumentNullException.ThrowIfNull(plan);
        ArgumentNullException.ThrowIfNull(request);
        if (request.ExternalBinding is not null || request.NodeReferences is not null ||
            request.TaskPricingInitialization != ProjectObjectTaskPricingInitialization.ClearAuthoritativePricing ||
            request.ParentNodeKey?.Trim() != plan.ParentNodeId.Value) {
            throw new ArgumentException("Workflow outputs require the prepared native parent and no external binding or authoritative pricing.", nameof(request));
        }
        if (plan.Kind == WorkflowStructureOutputKind.Task && request.ObjectType != ProjectObjectType.WorkItem ||
            plan.Kind == WorkflowStructureOutputKind.Asset && request.ObjectType is not (ProjectObjectType.File or ProjectObjectType.ImageAsset or ProjectObjectType.VideoAsset)) {
            throw new ArgumentException("The prepared workflow output kind does not match its native operation.", nameof(request));
        }

        var existing = await FindWorkflowContributionAsync(plan.Identity, cancellationToken);
        if (existing is not null) {
            EnsureContributionMatches(plan, request, existing.Receipt!);
            return existing;
        }

        var prepared = await PrepareWorkflowContributionAsync(plan, request, storagePlacementIntentId, cancellationToken);
        request = prepared.Request;
        SavedMediaDescriptor? media = null;
        Exception? storageObservation = null;
        if (plan.Kind == WorkflowStructureOutputKind.Asset) {
            if (storagePlacementIntentId is not { } preparedId || preparedId == Guid.Empty || request.Media is null) {
                throw new InvalidOperationException("This asset has no durable preallocated Storage intent. Legacy ambiguous dispatch requires reconciliation.");
            }
            var subtype = ProjectObjectSubtypePolicy.Normalize(request.ObjectType, request.ObjectSubtype);
            await using (var database = await dbContextFactory.CreateDbContextAsync(cancellationToken)) {
                await ValidateWorkflowContributionTargetAsync(database, plan.ProjectId, plan.ParentNodeId.Value, plan, request, cancellationToken);
            }
            (media, storageObservation) = await assetStorageService.SaveStableAsync(new(preparedId), plan.ProjectId,
                request.ObjectType, subtype, request.Media, cancellationToken);
        }
        var result = await CreateObjectCoreAsync(plan.ProjectId, request, false, cancellationToken, plan, media);
        return result with { StorageObservationException = storageObservation };
    }

    internal async Task<ProjectWorkflowPreparedContribution?> FindPreparedWorkflowContributionAsync(
        WorkflowStructureOutputIdentity identity, CancellationToken cancellationToken = default) {
        await using var database = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        var row = await database.Set<ProjectWorkflowContributionRecord>().AsNoTracking().SingleOrDefaultAsync(record =>
            record.RunId == identity.Occurrence.RunId.Value && record.OccurrencePath == identity.Occurrence.Path &&
            record.Slot == identity.Slot && record.PlanJson != "", cancellationToken);
        return row is null ? null : ReadPreparedContribution(row);
    }

    private async Task<ProjectWorkflowPreparedContribution> PrepareWorkflowContributionAsync(WorkflowStructureOutputPlan plan,
        ProjectObjectCreateRequest request, Guid? storagePlacementIntentId, CancellationToken cancellationToken) {
        if (plan.Kind == WorkflowStructureOutputKind.Asset && (storagePlacementIntentId is null || storagePlacementIntentId == Guid.Empty)) {
            throw new InvalidOperationException("This legacy asset dispatch has no durable Storage intent and requires explicit reconciliation.");
        }
        if (plan.Fingerprint != ProjectWorkflowContributionFingerprint.Create(plan, request) ||
            plan.Kind == WorkflowStructureOutputKind.Asset && (request.Media is null ||
                request.Media.Base64Data.Length > ProjectStructureAssetUploadLimits.MaximumBase64Characters)) {
            throw new WorkflowStructureOutputConflictException();
        }
        await using var database = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        await using var transaction = await mutationScopes.BeginBindingWriteAsync(database,
            [ProjectStructureSerializableMutationScope.ForProject(plan.ProjectId), WorkflowContributionScope(plan.Identity)], cancellationToken);
        var row = await database.Set<ProjectWorkflowContributionRecord>().SingleOrDefaultAsync(record =>
            record.RunId == plan.Identity.Occurrence.RunId.Value && record.OccurrencePath == plan.Identity.Occurrence.Path &&
            record.Slot == plan.Identity.Slot, cancellationToken);
        if (row is not null) {
            if (row.PlanJson.Length == 0) {
                throw new InvalidOperationException("A legacy receipt has no reconstructable native command; observe its existing receipt.");
            }
            var prepared = ReadPreparedContribution(row);
            if (prepared.Plan != plan || prepared.StoragePlacementIntentId != storagePlacementIntentId ||
                prepared.Plan.Fingerprint != ProjectWorkflowContributionFingerprint.Create(prepared.Plan, request)) {
                throw new WorkflowStructureOutputConflictException();
            }
            return prepared;
        }
        await ValidateWorkflowContributionTargetAsync(database, plan.ProjectId, plan.ParentNodeId.Value, plan, request, cancellationToken);
        row = new() {
            RunId = plan.Identity.Occurrence.RunId.Value,
            OccurrencePath = plan.Identity.Occurrence.Path,
            Slot = plan.Identity.Slot,
            ProjectId = plan.ProjectId,
            PlanJson = JsonSerializer.Serialize(plan, WorkflowContributionJson),
            RequestJson = JsonSerializer.Serialize(request, WorkflowContributionJson),
            StoragePlacementIntentId = storagePlacementIntentId,
            PreparedAtUtc = clock.GetUtcNow()
        };
        database.Add(row);
        await database.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return ReadPreparedContribution(row);
    }

    private static ProjectWorkflowPreparedContribution ReadPreparedContribution(ProjectWorkflowContributionRecord row) {
        var plan = JsonSerializer.Deserialize<WorkflowStructureOutputPlan>(row.PlanJson, WorkflowContributionJson)
            ?? throw new InvalidOperationException("The prepared native output plan is missing.");
        var request = JsonSerializer.Deserialize<ProjectObjectCreateRequest>(row.RequestJson, WorkflowContributionJson)
            ?? throw new InvalidOperationException("The prepared native output command is missing.");
        if (!row.PreparedAtUtc.HasValue || plan.Identity.Occurrence.RunId.Value != row.RunId || plan.Identity.Occurrence.Path != row.OccurrencePath ||
            plan.Identity.Slot != row.Slot || plan.ProjectId != row.ProjectId ||
            plan.Fingerprint != ProjectWorkflowContributionFingerprint.Create(plan, request)) {
            throw new InvalidOperationException("The prepared native output identity or content fingerprint is inconsistent.");
        }
        return new(plan, request, row.StoragePlacementIntentId);
    }

    public async Task<ProjectWorkflowContributionResult?> FindWorkflowContributionAsync(
        WorkflowStructureOutputIdentity identity,
        CancellationToken cancellationToken = default) {
        ArgumentNullException.ThrowIfNull(identity);
        await using var database = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        return await ReadWorkflowContributionAsync(database, identity, cancellationToken);
    }

    public async Task<string> ReadWorkflowTargetBindingAsync(Guid projectId, string parentNodeId,
        CancellationToken cancellationToken = default) {
        await using var database = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        return await ReadWorkflowTargetBindingAsync(database, projectId, parentNodeId, cancellationToken);
    }

    private async Task<string> ReadWorkflowTargetBindingAsync(WorkbenchDbContext database, Guid projectId,
        string parentNodeId, CancellationToken cancellationToken) {
        if (parentNodeId == ProjectWorkbenchGraphConventions.BuildProjectRootNodeKey(projectId)) {
            return ProjectWorkflowContributionFingerprint.Hash(JsonSerializer.Serialize(new { projectId, parentNodeId }));
        }

        var native = await database.Set<ProjectObjectRecord>().AsNoTracking()
            .SingleOrDefaultAsync(node => node.ProjectId == projectId && node.NodeKey == parentNodeId, cancellationToken);
        if (native is null) {
            var assembly = await projectStructureAssemblyService.LoadAsync(database, projectId, cancellationToken);
            native = assembly.Nodes.SingleOrDefault(node => node.NodeKey == parentNodeId)
                ?? throw new ProjectStructureAgentException(404, "ParentNodeNotFound", "The prepared workflow output parent no longer exists.");
        }

        var binding = await database.Set<ProjectNodeBindingRecord>().AsNoTracking()
            .SingleOrDefaultAsync(item => item.ProjectObjectId == native.Id, cancellationToken);
        if (binding is not null) {
            native.Binding = new(binding.Route, binding.ExternalArtifactKind, binding.ExternalArtifactId, binding.MediaRelativePath,
                binding.MediaContentType, binding.MediaOriginalFileName, binding.StorageObjectReferenceJson);
        }

        return ProjectWorkflowContributionFingerprint.Hash(JsonSerializer.Serialize(new {
            Target = ProjectWorkflowContributionFingerprint.Target(native),
            Binding = binding is null ? null : new {
                binding.Id, binding.Route, binding.ExternalArtifactKind, binding.ExternalArtifactId,
                binding.MediaRelativePath, binding.MediaContentType, binding.MediaOriginalFileName, binding.StorageObjectReferenceJson
            }
        }));
    }

    private async Task ValidateWorkflowContributionTargetAsync(WorkbenchDbContext database, Guid projectId, string parentNodeId,
        WorkflowStructureOutputPlan plan, ProjectObjectCreateRequest request, CancellationToken cancellationToken) {
        if (plan.ProjectId != projectId || plan.ParentNodeId.Value != parentNodeId ||
            plan.TargetBindingFingerprint != await ReadWorkflowTargetBindingAsync(database, projectId, parentNodeId, cancellationToken) ||
            plan.Fingerprint != ProjectWorkflowContributionFingerprint.Create(plan, request)) {
            throw new WorkflowStructureOutputConflictException();
        }
    }

    private static void EnsureContributionMatches(WorkflowStructureOutputPlan plan, ProjectObjectCreateRequest request,
        WorkflowStructureOutputReceipt receipt) {
        if (receipt.Identity != plan.Identity || receipt.Fingerprint != plan.Fingerprint ||
            receipt.ProjectId != plan.ProjectId || plan.Fingerprint != ProjectWorkflowContributionFingerprint.Create(plan, request)) {
            throw new WorkflowStructureOutputConflictException();
        }
    }

    private static async Task<ProjectWorkflowContributionResult?> ReadWorkflowContributionAsync(WorkbenchDbContext database,
        WorkflowStructureOutputIdentity identity, CancellationToken cancellationToken) {
        var row = await database.Set<ProjectWorkflowContributionRecord>().AsNoTracking().SingleOrDefaultAsync(record =>
            record.RunId == identity.Occurrence.RunId.Value && record.OccurrencePath == identity.Occurrence.Path && record.Slot == identity.Slot &&
                record.ReceiptJson != "", cancellationToken);
        if (row is null) {
            return null;
        }

        var node = JsonSerializer.Deserialize<ProjectStructureNode>(row.NodeJson, WorkflowContributionJson)
            ?? throw new InvalidOperationException("The saved Structure contribution node receipt is invalid.");
        var receipt = JsonSerializer.Deserialize<WorkflowStructureOutputReceipt>(row.ReceiptJson, WorkflowContributionJson)
            ?? throw new InvalidOperationException("The saved Structure contribution receipt is invalid.");
        var exists = await database.Set<ProjectObjectRecord>().AsNoTracking().AnyAsync(record =>
            record.Id == row.NativeObjectId && record.ProjectId == row.ProjectId && record.NodeKey == receipt.NodeId, cancellationToken);
        return new(node, receipt, true, !exists);
    }

    private static async Task<ProjectWorkflowContributionResult> StageWorkflowContributionAsync(WorkbenchDbContext database,
        ProjectObjectRecord record, WorkflowStructureOutputPlan plan, CancellationToken cancellationToken) {
        var createdAt = record.CreatedAtUtc.ToUniversalTime();
        createdAt = new DateTimeOffset(createdAt.Ticks - createdAt.Ticks % TimeSpan.TicksPerMicrosecond, TimeSpan.Zero);
        var node = ProjectWorkbenchNodeMapper.MapStructureNode(record);
        var receipt = new WorkflowStructureOutputReceipt(plan.Identity, plan.Fingerprint, plan.ProjectId,
            record.NodeKey, plan.Kind == WorkflowStructureOutputKind.Asset
                ? StorageJson.ParseReference(node.StorageObjectReferenceJson)?.PlacementIntentId : node.ArtifactId,
            node.MediaRelativePath, createdAt);
        var row = await database.Set<ProjectWorkflowContributionRecord>().SingleAsync(item =>
            item.RunId == plan.Identity.Occurrence.RunId.Value && item.OccurrencePath == plan.Identity.Occurrence.Path &&
            item.Slot == plan.Identity.Slot, cancellationToken);
        if (ReadPreparedContribution(row).Plan != plan || row.ReceiptJson.Length != 0) {
            throw new WorkflowStructureOutputConflictException();
        }
        row.NativeObjectId = record.Id;
        row.NodeJson = JsonSerializer.Serialize(node, WorkflowContributionJson);
        row.ReceiptJson = JsonSerializer.Serialize(receipt, WorkflowContributionJson);
        return new(node, receipt, false, false);
    }

    private static string WorkflowContributionScope(WorkflowStructureOutputIdentity identity)
        => $"workbench:workflow-output:{identity.Occurrence.RunId.Value:N}:{identity.Occurrence.Path}:{identity.Slot}";
}

internal static class ProjectWorkflowContributionFingerprint {
    public static string Create(WorkflowStructureOutputPlan plan, ProjectObjectCreateRequest request) {
        var contentHash = request.Media is null ? null : Convert.ToHexString(SHA256.HashData(Convert.FromBase64String(request.Media.Base64Data)));
        var metadata = string.IsNullOrWhiteSpace(request.MetadataJson) ? "{}"
            : WorkflowLaunchIdempotencyRequestFactory.CanonicalizeInputJson(request.MetadataJson);
        return Hash(JsonSerializer.Serialize(new {
            Schema = 1,
            plan.ProjectId,
            ParentNodeId = plan.ParentNodeId.Value,
            plan.TargetBindingFingerprint,
            plan.Kind,
            plan.Role,
            plan.WorkflowVersionId,
            plan.StepId,
            request.ObjectType,
            ObjectSubtype = ProjectObjectSubtypePolicy.Normalize(request.ObjectType, request.ObjectSubtype),
            Title = request.Title?.Trim() ?? string.Empty,
            Subtitle = request.Subtitle?.Trim() ?? string.Empty,
            Notes = request.Notes?.Trim() ?? string.Empty,
            request.X,
            request.Y,
            StartUtc = CanonicalTimestamp(request.StartUtc),
            EndUtc = CanonicalTimestamp(request.EndUtc),
            request.DurationSeconds,
            request.Status,
            request.PlacementIntent,
            Metadata = metadata,
            MediaFileName = request.Media?.FileName,
            MediaContentType = request.Media?.ContentType,
            ContentHash = contentHash
        }));
    }

    private static DateTimeOffset? CanonicalTimestamp(DateTimeOffset? value) {
        if (!value.HasValue) {
            return null;
        }

        var utc = value.Value.ToUniversalTime();
        return new DateTimeOffset(utc.Ticks - utc.Ticks % TimeSpan.TicksPerMicrosecond, TimeSpan.Zero);
    }

    public static string Target(ProjectObjectRecord node) => Hash(JsonSerializer.Serialize(new {
        node.Id,
        node.ProjectId,
        node.NodeKey,
        node.ParentNodeKey,
        node.ObjectType,
        node.ObjectSubtype,
        node.IsSystemManaged,
        node.Binding
    }));

    public static string Hash(string value) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value)));
}

internal sealed record ProjectWorkflowPreparedContribution(WorkflowStructureOutputPlan Plan,
    ProjectObjectCreateRequest Request, Guid? StoragePlacementIntentId);

public sealed class ProjectWorkflowContributionRecord {
    public Guid RunId { get; set; }
    public string OccurrencePath { get; set; } = string.Empty;
    public int Slot { get; set; }
    public Guid ProjectId { get; set; }
    public Guid NativeObjectId { get; set; }
    public string PlanJson { get; set; } = string.Empty;
    public string RequestJson { get; set; } = string.Empty;
    public Guid? StoragePlacementIntentId { get; set; }
    public DateTimeOffset? PreparedAtUtc { get; set; }
    public string NodeJson { get; set; } = string.Empty;
    public string ReceiptJson { get; set; } = string.Empty;
}

internal sealed class ProjectWorkflowContributionRecordConfiguration : IEntityTypeConfiguration<ProjectWorkflowContributionRecord> {
    public void Configure(EntityTypeBuilder<ProjectWorkflowContributionRecord> builder) {
        builder.ToTable("Workbench_WorkflowContributionReceipts");
        builder.Property(row => row.PlanJson).HasColumnType("TEXT").IsRequired();
        builder.Property(row => row.RequestJson).HasColumnType("TEXT").IsRequired();
        builder.HasKey(row => new { row.RunId, row.OccurrencePath, row.Slot });
        builder.Property(row => row.OccurrencePath).HasMaxLength(64).IsRequired();
        builder.Property(row => row.NodeJson).HasColumnType("TEXT").IsRequired();
        builder.Property(row => row.ReceiptJson).HasColumnType("TEXT").IsRequired();
        builder.HasIndex(row => new { row.ProjectId, row.NativeObjectId });
    }
}
