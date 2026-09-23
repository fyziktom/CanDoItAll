using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CanDoItAll.Infrastructure.Persistence;

public sealed record RetainedEvidenceImport {
    public RetainedEvidenceImport(Guid sourceProfileId, Guid transferId, RetainedEvidenceImport? previous = null) {
        if (sourceProfileId == Guid.Empty || transferId == Guid.Empty) {
            throw new ArgumentException("Imported history requires its source profile and transfer identity.");
        }
        SourceProfileId = sourceProfileId;
        TransferId = transferId;
        Previous = previous;
    }

    public Guid SourceProfileId { get; }
    public Guid TransferId { get; }
    public RetainedEvidenceImport? Previous { get; }

    public static void RequireNative(RetainedEvidenceImport? importedHistory) {
        if (importedHistory is not null) {
            throw new InvalidOperationException("Imported execution evidence is historical. Prepare a new admission in this database profile.");
        }
    }
}

public static class RetainedEvidenceImportMapping {
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public static PropertyBuilder<RetainedEvidenceImport?> HasRetainedEvidenceImportConversion(
        this PropertyBuilder<RetainedEvidenceImport?> property) => property.HasConversion(
            value => Serialize(value!), value => Deserialize(value)).HasColumnType("TEXT");

    private static string Serialize(RetainedEvidenceImport value) => JsonSerializer.Serialize(value, JsonOptions);

    private static RetainedEvidenceImport Deserialize(string value) => JsonSerializer.Deserialize<RetainedEvidenceImport>(value, JsonOptions)
        ?? throw new InvalidOperationException("The retained evidence import provenance is invalid.");
}
