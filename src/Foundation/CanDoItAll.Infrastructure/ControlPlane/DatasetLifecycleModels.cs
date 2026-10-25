using CanDoItAll.SharedKernel;
using System.Text.Json.Serialization;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CanDoItAll.Infrastructure.ControlPlane;

public enum DatasetLifecycleState { Active = 1, Maintenance = 2 }

public enum DatasetGuardProtocol { SharedRows = 1, SharedAdvisoryWithSnapshotValidation = 2 }

public enum DatasetActivationKind { Bootstrap = 1, Attach = 2, Reactivate = 3 }

public enum DatasetActivationFileState { Prepared = 1, Active = 2 }

public sealed class DatasetLifecycleRecord {
    public const int SingletonId = 1;
    public const int CurrentSchemaVersion = 1;
    public const string TableName = "Infrastructure_DatasetLifecycle";

    public int Id { get; set; } = SingletonId;
    public Guid NamespaceId { get; set; }
    public Guid MaintenanceId { get; set; }
    public int SchemaVersion { get; set; } = CurrentSchemaVersion;
    public DatasetGuardProtocol GuardProtocol { get; set; }
    public DatasetLifecycleState State { get; set; }
}

public sealed record DatasetActivationBinding {
    public DatasetActivationBinding(DatasetNamespaceId @namespace, Guid maintenanceId, int schemaVersion,
        DatasetGuardProtocol guardProtocol) {
        ArgumentNullException.ThrowIfNull(@namespace);
        if (maintenanceId == Guid.Empty || schemaVersion != DatasetLifecycleRecord.CurrentSchemaVersion ||
                guardProtocol != DatasetGuardProtocol.SharedRows) {
            throw new ArgumentException("The dataset activation binding is invalid or unsupported.");
        }
        Namespace = @namespace;
        MaintenanceId = maintenanceId;
        SchemaVersion = schemaVersion;
        GuardProtocol = guardProtocol;
    }

    public DatasetNamespaceId Namespace { get; }
    public Guid MaintenanceId { get; }
    public int SchemaVersion { get; }
    public DatasetGuardProtocol GuardProtocol { get; }
}

public sealed record DatasetLifecycleSnapshot(DatasetActivationBinding Binding, DatasetLifecycleState State);

public sealed class DatasetActivationMismatchException(DatasetActivationBinding capturedBinding)
    : InvalidOperationException("The active SQL dataset does not match the captured activation binding.") {
    public DatasetActivationBinding CapturedBinding { get; } = capturedBinding;
}

public sealed record DatasetActivationPlan {
    public DatasetActivationPlan(Guid planId, DatasetActivationKind kind, DatasetActivationBinding binding,
        DatasetActivationBinding? expectedPreviousSqlBinding) {
        ArgumentNullException.ThrowIfNull(binding);
        if (planId == Guid.Empty || !Enum.IsDefined(kind)) {
            throw new ArgumentException("A valid dataset activation plan identity and kind are required.");
        }
        if (kind == DatasetActivationKind.Bootstrap && expectedPreviousSqlBinding is not null) {
            throw new ArgumentException("Dataset bootstrap requires absent prior SQL lifecycle state.");
        }
        if (kind == DatasetActivationKind.Attach && expectedPreviousSqlBinding != binding) {
            throw new ArgumentException("Attaching a profile must preserve the existing SQL activation binding.");
        }
        if (kind == DatasetActivationKind.Reactivate && (expectedPreviousSqlBinding is not { } previous ||
                previous.Namespace == binding.Namespace || previous.MaintenanceId == binding.MaintenanceId)) {
            throw new ArgumentException("Dataset reactivation requires an explicit prior SQL binding and a new namespace and maintenance identity.");
        }
        PlanId = planId;
        Kind = kind;
        Binding = binding;
        ExpectedPreviousSqlBinding = expectedPreviousSqlBinding;
    }

    public Guid PlanId { get; }
    public DatasetActivationKind Kind { get; }
    public DatasetActivationBinding Binding { get; }
    public DatasetActivationBinding? ExpectedPreviousSqlBinding { get; }
}

public sealed record DatasetActivationFileRecord {
    public const int CurrentVersion = 2;
    public const int LegacyVersion = 1;

    public DatasetActivationFileRecord(int version, Guid profileId, DatasetActivationFileState state, DatasetActivationPlan plan,
        DatasetActivationRestoration? restoration = null) {
        ArgumentNullException.ThrowIfNull(plan);
        if (version is not (LegacyVersion or CurrentVersion) || profileId == Guid.Empty || !Enum.IsDefined(state)) {
            throw new ArgumentException("The dataset activation file record is invalid or unsupported.");
        }
        if (restoration is not null && (version != CurrentVersion || plan.Kind != DatasetActivationKind.Attach ||
                plan.Binding != restoration.AbandonedPlan.ExpectedPreviousSqlBinding || plan.PlanId == restoration.AbandonedPlan.PlanId)) {
            throw new ArgumentException("Restoration must retain the exact previous binding and a distinct attachment plan.");
        }
        Version = version;
        ProfileId = profileId;
        State = state;
        Plan = plan;
        Restoration = restoration;
    }

    public int Version { get; }
    public Guid ProfileId { get; }
    public DatasetActivationFileState State { get; }
    public DatasetActivationPlan Plan { get; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public DatasetActivationRestoration? Restoration { get; }
}

internal sealed class DatasetLifecycleRecordConfiguration : IEntityTypeConfiguration<DatasetLifecycleRecord> {
    public void Configure(EntityTypeBuilder<DatasetLifecycleRecord> builder) {
        builder.ToTable(DatasetLifecycleRecord.TableName, table => {
            table.HasCheckConstraint("CK_DatasetLifecycle_Singleton", "\"Id\" = 1");
            table.HasCheckConstraint("CK_DatasetLifecycle_Namespace", "\"NamespaceId\" <> '00000000-0000-0000-0000-000000000000'::uuid");
            table.HasCheckConstraint("CK_DatasetLifecycle_Maintenance", "\"MaintenanceId\" <> '00000000-0000-0000-0000-000000000000'::uuid");
            table.HasCheckConstraint("CK_DatasetLifecycle_Versions", "\"SchemaVersion\" > 0 AND \"GuardProtocol\" > 0");
            table.HasCheckConstraint("CK_DatasetLifecycle_State", "\"State\" IN (1, 2)");
        });
        builder.HasKey(record => record.Id);
        builder.Property(record => record.Id).ValueGeneratedNever();
    }
}
