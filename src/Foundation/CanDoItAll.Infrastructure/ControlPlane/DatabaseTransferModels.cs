using CanDoItAll.Infrastructure.Persistence;

namespace CanDoItAll.Infrastructure.ControlPlane;

public sealed record DatabaseTransferItemDescriptor(
    string Key,
    string Label,
    string Description,
    int SortOrder = 0,
    bool IsSensitive = false);

public sealed record DatabaseTransferSourceSummary(
    Guid ProfileId,
    string DisplayName,
    DatabaseProviderKind ProviderKind,
    DatabaseProfileSourceKind SourceKind,
    string Descriptor,
    bool IsActive,
    bool IsRuntimeLocked);

public sealed record DatabaseTransferItemPreview(
    DatabaseTransferItemDescriptor Descriptor,
    bool IsAvailable,
    string Summary,
    string? Warning,
    int SourceRecordCount,
    int TargetRecordCount);

public sealed class DatabaseTransferRequest
{
    public Guid SourceProfileId { get; set; }

    public Guid TargetProfileId { get; set; }

    public List<string> ItemKeys { get; set; } = [];

    public bool ReplaceExisting { get; set; } = true;
}

public sealed record DatabaseTransferItemResult(
    string Key,
    string Label,
    bool Success,
    string Message,
    int RecordsCopied);

public sealed record DatabaseTransferResult(
    Guid SourceProfileId,
    Guid TargetProfileId,
    IReadOnlyList<DatabaseTransferItemResult> Items)
{
    public bool IsSuccess => Items.Count > 0 && Items.All(item => item.Success);

    public int RecordsCopied => Items.Sum(item => Math.Max(0, item.RecordsCopied));
}

public sealed record DatabaseTransferContext(
    ResolvedDatabaseProfile SourceProfile,
    ResolvedDatabaseProfile TargetProfile,
    AppDbContext SourceDbContext,
    AppDbContext TargetDbContext,
    bool ReplaceExisting);

public interface IDatabaseTransferHandler
{
    DatabaseTransferItemDescriptor Descriptor { get; }

    Task<DatabaseTransferItemPreview> PreviewAsync(
        DatabaseTransferContext context,
        CancellationToken cancellationToken = default);

    Task<DatabaseTransferItemResult> TransferAsync(
        DatabaseTransferContext context,
        CancellationToken cancellationToken = default);
}

public interface IDatabaseTransferService
{
    Task<IReadOnlyList<DatabaseTransferSourceSummary>> ListSourcesAsync(
        Guid targetProfileId,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<DatabaseTransferItemPreview>> PreviewAsync(
        Guid sourceProfileId,
        Guid targetProfileId,
        CancellationToken cancellationToken = default);

    Task<DatabaseTransferResult> TransferAsync(
        DatabaseTransferRequest request,
        CancellationToken cancellationToken = default);
}
