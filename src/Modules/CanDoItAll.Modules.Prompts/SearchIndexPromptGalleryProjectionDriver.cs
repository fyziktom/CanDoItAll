using System.Runtime.CompilerServices;
using CanDoItAll.Infrastructure.Search;

namespace CanDoItAll.Modules.Prompts;

public sealed class SearchIndexPromptGalleryProjectionDriver(
    SearchProjectionStore store,
    IPromptArtifactProjectionQueryService projections) : IPromptGalleryProjectionDriver {
    public const string SourceType = "prompt";
    private const string Category = "Prompts";
    private static readonly SearchProjectionPartition Partition = new(SourceType, 7_142_033_841_991_137_043);
    private static readonly PromptGalleryProjectionStatus ReadyStatus = new(
        nameof(SearchIndexPromptGalleryProjectionDriver), Enabled: true, PromptGalleryProjectionHealth.Ready,
        "Prompt Gallery items are projected into the relational search index.");

    public string Name => ReadyStatus.DriverName;
    public bool Enabled => true;

    public Task<PromptGalleryProjectionStatus> GetStatusAsync(CancellationToken cancellationToken = default)
        => Task.FromResult(ReadyStatus);

    public Task UpsertAsync(PromptGalleryProjectionDocument document, CancellationToken cancellationToken = default)
        => store.UpsertAsync(Partition, ToEntry(document), async token => {
            var current = await projections.GetSearchStateForMutationAsync(document.PromptArtifactId, token);
            return current is { IsProjectable: true } && current.UpdatedAtUtc == document.UpdatedAtUtc;
        }, cancellationToken);

    public Task RemoveAsync(Guid promptArtifactId, DateTimeOffset? expectedUpdatedAtUtc,
        CancellationToken cancellationToken = default)
        => store.RemoveAsync(Partition, promptArtifactId.ToString(), async token =>
            !expectedUpdatedAtUtc.HasValue ||
            await projections.GetSearchStateForMutationAsync(promptArtifactId, token) is not { IsProjectable: true }, cancellationToken);

    public Task<int> RebuildAsync(IAsyncEnumerable<PromptGalleryProjectionDocument> documents,
        CancellationToken cancellationToken = default)
        => store.RebuildAsync(Partition, ToEntries(documents, cancellationToken), cancellationToken);

    private static async IAsyncEnumerable<SearchProjectionEntry> ToEntries(
        IAsyncEnumerable<PromptGalleryProjectionDocument> documents, [EnumeratorCancellation] CancellationToken cancellationToken) {
        await foreach (var document in documents.WithCancellation(cancellationToken)) {
            yield return ToEntry(document);
        }
    }

    private static SearchProjectionEntry ToEntry(PromptGalleryProjectionDocument document)
        => new(new(SourceType, document.PromptArtifactId.ToString(), Category, document.Title, document.Summary,
            document.Content, document.Route, document.ProjectId), document.UpdatedAtUtc);
}
