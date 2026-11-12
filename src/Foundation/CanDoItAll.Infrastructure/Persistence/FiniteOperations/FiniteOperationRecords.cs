using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CanDoItAll.Infrastructure.Persistence;

public sealed class FiniteOperationStateRecord {
    public const string TableName = "Infrastructure_FiniteOperationState";
    public Guid NamespaceId { get; set; }
    public Guid PoolId { get; set; }
    public int RetentionProtocolVersion { get; set; }
    public long CurrentEpoch { get; set; }
    public long RetiredThroughEpoch { get; set; }
}

public sealed class FiniteOperationEpochRecord {
    public const string TableName = "Infrastructure_FiniteOperationEpochs";
    public Guid NamespaceId { get; set; }
    public Guid PoolId { get; set; }
    public long Epoch { get; set; }
    public FiniteOperationEpochState State { get; set; }
    public DateTimeOffset OpenedAtUtc { get; set; }
    public DateTimeOffset AdmissionClosesAtUtc { get; set; }
    public DateTimeOffset? ClosedAtUtc { get; set; }
    public int MutationDeadlineMilliseconds { get; set; }
    public long ReplayRetentionSeconds { get; set; }
}

internal sealed class FiniteOperationStateRecordConfiguration : IEntityTypeConfiguration<FiniteOperationStateRecord> {
    public void Configure(EntityTypeBuilder<FiniteOperationStateRecord> builder) {
        builder.ToTable(FiniteOperationStateRecord.TableName, table => {
            table.HasCheckConstraint("CK_Infrastructure_FiniteOperationState_Namespace", "\"NamespaceId\" <> '00000000-0000-0000-0000-000000000000'::uuid");
            table.HasCheckConstraint("CK_Infrastructure_FiniteOperationState_Pool", "\"PoolId\" <> '00000000-0000-0000-0000-000000000000'::uuid");
            table.HasCheckConstraint("CK_Infrastructure_FiniteOperationState_Protocol", "\"RetentionProtocolVersion\" > 0");
            table.HasCheckConstraint("CK_Infrastructure_FiniteOperationState_Epoch", "\"CurrentEpoch\" > 0 AND \"RetiredThroughEpoch\" >= 0 AND \"RetiredThroughEpoch\" < \"CurrentEpoch\"");
        });
        builder.HasKey(record => new { record.NamespaceId, record.PoolId });
        builder.Property(record => record.NamespaceId).ValueGeneratedNever();
        builder.Property(record => record.PoolId).ValueGeneratedNever();
    }
}

internal sealed class FiniteOperationEpochRecordConfiguration : IEntityTypeConfiguration<FiniteOperationEpochRecord> {
    public void Configure(EntityTypeBuilder<FiniteOperationEpochRecord> builder) {
        builder.ToTable(FiniteOperationEpochRecord.TableName, table => {
            table.HasCheckConstraint("CK_Infrastructure_FiniteOperationEpoch_Namespace", "\"NamespaceId\" <> '00000000-0000-0000-0000-000000000000'::uuid");
            table.HasCheckConstraint("CK_Infrastructure_FiniteOperationEpoch_Pool", "\"PoolId\" <> '00000000-0000-0000-0000-000000000000'::uuid");
            table.HasCheckConstraint("CK_Infrastructure_FiniteOperationEpoch_Number", "\"Epoch\" > 0");
            table.HasCheckConstraint("CK_Infrastructure_FiniteOperationEpoch_Deadline", "\"MutationDeadlineMilliseconds\" > 0 AND \"ReplayRetentionSeconds\" > 0 AND \"AdmissionClosesAtUtc\" > \"OpenedAtUtc\"");
            table.HasCheckConstraint("CK_Infrastructure_FiniteOperationEpoch_State", "(\"State\" = 1 AND \"ClosedAtUtc\" IS NULL) OR (\"State\" = 2 AND \"ClosedAtUtc\" IS NOT NULL AND \"ClosedAtUtc\" >= \"OpenedAtUtc\")");
        });
        builder.HasKey(record => new { record.NamespaceId, record.PoolId, record.Epoch });
        builder.Property(record => record.State).HasConversion<int>();
        builder.HasIndex(record => new { record.NamespaceId, record.PoolId }).IsUnique()
            .HasFilter($"\"State\" = {(int)FiniteOperationEpochState.Open}");
    }
}
