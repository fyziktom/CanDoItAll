using CanDoItAll.SharedKernel;
using System.Text.Json;
using System.Text.Json.Serialization;
using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.Infrastructure.Persistence;
using CanDoItAll.Infrastructure.Storage;
using CanDoItAll.Modules.Projects;
using CanDoItAll.Processes.Application;
using CanDoItAll.Processes.Abstractions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CanDoItAll.Modules.Workbench;

public sealed record ProjectProcessAssetReceipt(AgentToolBusinessIntentId IntentId, Guid NativeObjectId,
    string NodeId, StoragePlacementIntentId StorageIntentId, string Fingerprint, DateTimeOffset CommittedAtUtc);

public sealed record ProjectProcessAssetReceiptObservation(ProjectProcessAssetReceipt Receipt, bool WasReplay,
    bool TargetDeleted, string? StorageObservationWarning = null);

public sealed class ProjectProcessAssetReconciliationRequiredException : ProjectStructureAgentException {
    internal ProjectProcessAssetReconciliationRequiredException(AgentToolBusinessIntentId intentId, StorageStablePlacementPendingException failure)
        : base(409, "ProcessAssetReconciliationRequired",
            $"Asset intent {intentId.Value:D} has Storage intent {failure.Outcome.IntentId.Value:D} in state {failure.Outcome.State}. " +
            "Retain this intent; its native receipt needs observation before starting another effect.",
            null, true, false, AgentToolEffectState.Unknown, failure) {
        IntentId = intentId;
        StorageIntentId = failure.Outcome.IntentId;
        StorageState = failure.Outcome.State;
    }

    public AgentToolBusinessIntentId IntentId { get; }
    public StoragePlacementIntentId StorageIntentId { get; }
    public StorageStablePlacementState StorageState { get; }
}

public sealed record ProjectProcessAssetInvocation {
    internal ProjectProcessAssetInvocation(AgentToolAdmittedInvocation admitted, ProcessExecutionDispatchAuthority execution,
        string parentNodeKey) {
        Admitted = admitted;
        Execution = execution;
        ParentNodeKey = parentNodeKey;
    }

    internal AgentToolAdmittedInvocation Admitted { get; }
    internal ProcessExecutionDispatchAuthority Execution { get; }
    internal string ParentNodeKey { get; }
}

internal sealed record ProjectProcessAssetPlan(int SchemaVersion, AgentToolAdmittedInvocation Producer,
    ProcessExecutionDispatchAuthority Execution, string ParentNodeKey, string TargetBindingFingerprint,
    Guid NativeObjectId, StoragePlacementIntentId StorageIntentId, ProjectObjectCreateRequest Template,
    bool CreateRevisionLink) {
    internal const int CurrentSchemaVersion = 1;

    internal ProjectWriteAdmission ProjectAdmission => Execution.SourceAuthority?.ProjectAdmission is { } project
        ? new(project.DatabaseProfileId, project.ProjectId, project.LifetimeId)
        : throw new InvalidOperationException("The saved Process asset has no original project lifetime.");

    internal void Validate() {
        var input = new ProjectProcessAssetProposalCodec().Read(Producer.Payload);
        var background = Producer.Session.BackgroundSource;
        if (SchemaVersion != CurrentSchemaVersion || Producer.IntentId.Value == Guid.Empty || NativeObjectId == Guid.Empty ||
                Producer.BatchId.Value == Guid.Empty || Producer.ApprovalStatus != ExecutionApprovalStatus.Approved || Producer.ApprovedDigest != Producer.Payload.Digest ||
                StorageIntentId.Value == Guid.Empty || background is null || Execution.SourceAuthority is null ||
                Execution.ProjectReference is null || Execution.Evidence.ExecutionRunId != Producer.Session.ExecutionRunId ||
                background.SourceId != Execution.Evidence.StepKey ||
                background.OwnerFingerprint != AgentToolProtocolEnvelope.ComputeDigest(Execution.OwnerFingerprint) ||
                input.ProjectId != ProjectAdmission.ProjectId || Execution.ProjectId != input.ProjectId ||
                string.IsNullOrWhiteSpace(ParentNodeKey) || Template.ParentNodeKey != ParentNodeKey ||
                TargetBindingFingerprint.Length != 64 || !TargetBindingFingerprint.All(char.IsAsciiHexDigit) ||
                Template.Media is not null || Template.ExternalBinding is not null || Template.NodeReferences is not null ||
                Template.ObjectType is not (ProjectObjectType.File or ProjectObjectType.ImageAsset or ProjectObjectType.VideoAsset) ||
                CreateRevisionLink != (input.Revision is not null)) {
            throw new InvalidOperationException("The saved Process asset contribution has inconsistent source, target or proposal evidence.");
        }
        if (!Execution.SourceAuthority.CanCreateAssets || Execution.ProjectReference.RunId != Execution.Evidence.RunId ||
                Execution.ProjectReference.StepInstanceId != Execution.Evidence.StepInstanceId ||
                Execution.ProjectReference.ReadinessHash != Execution.ReadinessHash) {
            throw new InvalidOperationException("The saved asset proposal has no matching Process assignment and asset authority.");
        }
        Execution.SourceAuthority.Validate();
    }
}

internal sealed record ProjectProcessAssetSnapshot(ProjectProcessAssetPlan Plan, string PlanFingerprint,
    ProjectObjectCreateRequest? MaterializedRequest, string? MaterializedFingerprint,
    ProjectStructureNode? Node, ProjectProcessAssetReceipt? Receipt, bool TargetDeleted) {
    public RetainedEvidenceImport? ImportedHistory { get; init; }
}

internal sealed record ProjectProcessAssetResult(ProjectStructureNode Node, ProjectProcessAssetReceiptObservation Observation) {
    internal Exception? StorageObservationException { get; init; }
}

public sealed class ProjectProcessAssetContributionRecord {
    public Guid IntentId { get; set; }
    public Guid DatabaseProfileId { get; set; }
    public Guid ProjectId { get; set; }
    public Guid ProjectLifetimeId { get; set; }
    public Guid SourceExecutionRunId { get; set; }
    public Guid NativeObjectId { get; set; }
    public Guid StorageIntentId { get; set; }
    public string PlanJson { get; set; } = string.Empty;
    public string PlanFingerprint { get; set; } = string.Empty;
    public string MaterializedRequestJson { get; set; } = string.Empty;
    public string MaterializedFingerprint { get; set; } = string.Empty;
    public string NodeJson { get; set; } = string.Empty;
    public string ReceiptJson { get; set; } = string.Empty;
    public DateTimeOffset PreparedAtUtc { get; set; }
    public RetainedEvidenceImport? ImportedHistory { get; set; }
}

internal sealed class ProjectProcessAssetContributionRecordConfiguration : IEntityTypeConfiguration<ProjectProcessAssetContributionRecord> {
    public void Configure(EntityTypeBuilder<ProjectProcessAssetContributionRecord> builder) {
        builder.ToTable("Workbench_ProcessAssetContributions");
        builder.HasKey(row => row.IntentId);
        builder.Property(row => row.ImportedHistory).HasRetainedEvidenceImportConversion();
        builder.Property(row => row.PlanJson).HasColumnType("TEXT").IsRequired();
        builder.Property(row => row.PlanFingerprint).HasMaxLength(64).IsRequired();
        builder.Property(row => row.MaterializedRequestJson).HasColumnType("TEXT").IsRequired();
        builder.Property(row => row.MaterializedFingerprint).HasMaxLength(64).IsRequired();
        builder.Property(row => row.NodeJson).HasColumnType("TEXT").IsRequired();
        builder.Property(row => row.ReceiptJson).HasColumnType("TEXT").IsRequired();
        builder.HasIndex(row => row.NativeObjectId).IsUnique();
        builder.HasIndex(row => row.StorageIntentId).IsUnique();
        builder.HasIndex(row => new { row.DatabaseProfileId, row.ProjectId, row.ProjectLifetimeId });
        builder.HasIndex(row => row.SourceExecutionRunId);
    }
}

internal static class ProjectProcessAssetPersistence {
    internal static JsonSerializerOptions Json { get; } = new(JsonSerializerDefaults.Web) {
        Converters = {
            new GuidValueConverter<ProcessRunId>(static id => new(id), static id => id.Value),
            new GuidValueConverter<ProcessStepInstanceId>(static id => new(id), static id => id.Value)
        }
    };

    internal static string Hash(string value) => ProjectWorkflowContributionFingerprint.Hash(value);

    internal static ProjectProcessAssetPlan ReadPlan(ProjectProcessAssetContributionRecord row) {
        var plan = JsonSerializer.Deserialize<ProjectProcessAssetPlan>(row.PlanJson, Json)
            ?? throw new InvalidOperationException("The saved Process asset preparation is missing.");
        plan.Validate();
        if (row.PlanFingerprint != Hash(row.PlanJson) || plan.Producer.IntentId.Value != row.IntentId ||
                plan.ProjectAdmission != new ProjectWriteAdmission(row.DatabaseProfileId, row.ProjectId, row.ProjectLifetimeId) ||
                plan.Execution.Evidence.ExecutionRunId != row.SourceExecutionRunId || plan.NativeObjectId != row.NativeObjectId ||
                plan.StorageIntentId.Value != row.StorageIntentId) {
            throw new InvalidOperationException("The Process asset preparation does not match its retained immutable identity.");
        }
        return plan;
    }

    internal static ProjectObjectCreateRequest? ReadMaterialized(ProjectProcessAssetContributionRecord row) {
        if (row.MaterializedRequestJson.Length == 0) {
            if (row.MaterializedFingerprint.Length != 0 || row.ReceiptJson.Length != 0 || row.NodeJson.Length != 0) {
                throw new InvalidOperationException("A Process asset has an outcome without its retained media preparation.");
            }
            return null;
        }
        if (Hash(row.MaterializedRequestJson) != row.MaterializedFingerprint) {
            throw new InvalidOperationException("The Process asset media preparation fingerprint is inconsistent.");
        }
        var request = JsonSerializer.Deserialize<ProjectObjectCreateRequest>(row.MaterializedRequestJson, Json)
            ?? throw new InvalidOperationException("The Process asset media preparation is missing.");
        var plan = ReadPlan(row);
        if (request.Media is null || request.Media.Base64Data.Length > ProjectStructureAssetUploadLimits.MaximumBase64Characters ||
                JsonSerializer.Serialize(request with { Media = null }, Json) != JsonSerializer.Serialize(plan.Template, Json)) {
            throw new InvalidOperationException("The Process asset media preparation changed its original native target or command.");
        }
        return request;
    }

    private sealed class GuidValueConverter<T>(Func<Guid, T> create, Func<T, Guid> value) : JsonConverter<T> where T : struct {
        public override T Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) => create(reader.GetGuid());
        public override void Write(Utf8JsonWriter writer, T item, JsonSerializerOptions options) => writer.WriteStringValue(value(item));
    }
}
