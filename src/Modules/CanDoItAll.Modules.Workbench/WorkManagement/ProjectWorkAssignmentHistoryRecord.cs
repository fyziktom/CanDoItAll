using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using CanDoItAll.Infrastructure.Persistence;
using CanDoItAll.Modules.Projects;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CanDoItAll.Modules.Workbench;

public sealed record ProjectWorkAssignmentHistoryPayload(Guid DatabaseProfileId, Guid Id, Guid ProjectId, Guid? ProjectLifetimeId,
    Guid PartyId, Guid? PartyOrganizationAffiliationId, string NodeKey, string PhaseName, Guid? OpportunityId,
    decimal? AllocationPercent, DateTimeOffset? StartsAtUtc, DateTimeOffset? EndsAtUtc, bool IsPrimary, string Source, string Notes,
    ProjectPartyAssignmentRole Role = ProjectPartyAssignmentRole.WorkItemAssignee);

public sealed record ProjectWorkAssignmentTransferItem(Guid EvidenceId, string PayloadJson, RetainedEvidenceImport? ImportedHistory);

public sealed class ProjectWorkAssignmentHistoryRecord {
    private static readonly JsonSerializerOptions SerializerOptions = CreateSerializerOptions();

    private ProjectWorkAssignmentHistoryRecord() {
    }

    internal ProjectWorkAssignmentHistoryRecord(ProjectWorkAssignmentTransferItem item) {
        var payload = Validate(item);
        ImportedHistory = item.ImportedHistory
            ?? throw new InvalidDataException("An imported Work assignment requires an explicit history disposition.");
        Id = item.EvidenceId;
        ProjectId = payload.ProjectId;
        AssignmentId = payload.Id;
        NodeKey = payload.NodeKey;
        ProjectLifetimeId = payload.ProjectLifetimeId;
        PayloadJson = item.PayloadJson;
    }

    public Guid Id { get; private set; }
    public Guid ProjectId { get; private set; }
    public Guid AssignmentId { get; private set; }
    public string NodeKey { get; private set; } = string.Empty;
    public Guid? ProjectLifetimeId { get; private set; }
    public string PayloadJson { get; private set; } = string.Empty;
    public RetainedEvidenceImport ImportedHistory { get; private set; } = null!;

    internal ProjectWorkAssignmentTransferItem ToTransfer() => new(Id, PayloadJson, ImportedHistory);

    internal static ProjectWorkAssignmentTransferItem Capture(ProjectWorkAssignmentRecord row, Guid sourceProfileId) {
        var payload = JsonSerializer.Serialize(new ProjectWorkAssignmentHistoryPayload(sourceProfileId, row.Id, row.ProjectId,
            row.ProjectLifetimeId, row.PartyId, row.PartyOrganizationAffiliationId, row.NodeKey, row.PhaseName, row.OpportunityId,
            row.AllocationPercent, row.StartsAtUtc, row.EndsAtUtc, row.IsPrimary, row.Source, row.Notes), SerializerOptions);
        var fingerprint = SHA256.HashData(Encoding.UTF8.GetBytes(payload));
        return new(new Guid(fingerprint.AsSpan(0, 16)), payload, null);
    }

    internal static ProjectWorkAssignmentHistoryPayload Validate(ProjectWorkAssignmentTransferItem item) {
        if (item.EvidenceId == Guid.Empty || string.IsNullOrWhiteSpace(item.PayloadJson)) {
            throw new InvalidDataException("The Work assignment history evidence identity and original payload are required.");
        }
        ProjectWorkAssignmentHistoryPayload payload;
        try {
            payload = JsonSerializer.Deserialize<ProjectWorkAssignmentHistoryPayload>(item.PayloadJson, SerializerOptions)
                ?? throw new InvalidDataException("The Work assignment history payload is missing.");
        } catch (JsonException exception) {
            throw new InvalidDataException("The Work assignment history payload is invalid.", exception);
        }
        if (payload.DatabaseProfileId == Guid.Empty || payload.Id == Guid.Empty || payload.ProjectId == Guid.Empty ||
            payload.PartyId == Guid.Empty || payload.ProjectLifetimeId == Guid.Empty ||
            payload.Role != ProjectPartyAssignmentRole.WorkItemAssignee || string.IsNullOrWhiteSpace(payload.NodeKey) ||
            payload.NodeKey.Length > 160 || payload.PhaseName is null || payload.Source is null || payload.Notes is null) {
            throw new InvalidDataException("The Work assignment history payload has invalid identity or role values.");
        }
        return payload;
    }

    private static JsonSerializerOptions CreateSerializerOptions() {
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        options.Converters.Add(new JsonStringEnumConverter());
        return options;
    }
}

public sealed class ProjectWorkAssignmentHistoryRecordConfiguration : IEntityTypeConfiguration<ProjectWorkAssignmentHistoryRecord> {
    public void Configure(EntityTypeBuilder<ProjectWorkAssignmentHistoryRecord> builder) {
        builder.ToTable("Workbench_WorkAssignmentHistory");
        builder.HasKey(row => row.Id);
        builder.Property(row => row.NodeKey).HasMaxLength(160);
        builder.Property(row => row.PayloadJson).HasColumnType("text");
        builder.Property<RetainedEvidenceImport?>(row => row.ImportedHistory).HasRetainedEvidenceImportConversion().IsRequired();
        builder.HasIndex(row => new { row.ProjectId, row.NodeKey });
        builder.HasIndex(row => row.AssignmentId);
    }
}
