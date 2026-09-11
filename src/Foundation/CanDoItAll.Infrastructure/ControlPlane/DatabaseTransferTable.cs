using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;

namespace CanDoItAll.Infrastructure.ControlPlane;

public sealed record DatabaseTransferTable(string? Schema, string Name) {
    public static DatabaseTransferTable From(IEntityType mapping)
        => new(mapping.GetSchema(), mapping.GetTableName()
            ?? throw new InvalidOperationException("The transfer owner mapping has no physical table."));
}
