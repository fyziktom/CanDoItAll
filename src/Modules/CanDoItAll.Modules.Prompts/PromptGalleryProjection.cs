using CanDoItAll.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using System.Runtime.CompilerServices;

namespace CanDoItAll.Modules.Prompts;

public enum PromptGalleryProjectionHealth
{
    Disabled,
    Ready,
    RebuildRequired,
    Failed
}

public enum PromptGalleryProjectionOperationState
{
    Disabled,
    Applied
}

public sealed record PromptGalleryProjectionStatus(
    string DriverName,
    bool Enabled,
    PromptGalleryProjectionHealth Health,
    string Detail);

public sealed record PromptGalleryProjectionOperationResult(
    PromptGalleryProjectionOperationState State,
    int ProcessedCount,
    PromptGalleryProjectionStatus Status);

public sealed record PromptGalleryProjectionDocument(
    Guid PromptArtifactId,
    Guid? ProjectId,
    string Title,
    string Summary,
    string Content,
    PromptGalleryItemKind Kind,
    PromptArtifactStatus Status,
    IReadOnlyList<string> Tags,
    string Route,
    DateTimeOffset UpdatedAtUtc);

public interface IPromptGalleryProjectionDriver
{
    string Name { get; }

    bool Enabled { get; }

    Task<PromptGalleryProjectionStatus> GetStatusAsync(CancellationToken cancellationToken = default);

    Task UpsertAsync(PromptGalleryProjectionDocument document, CancellationToken cancellationToken = default);

    Task RemoveAsync(Guid promptArtifactId, CancellationToken cancellationToken = default);

    Task<int> RebuildAsync(
        IAsyncEnumerable<PromptGalleryProjectionDocument> documents,
        CancellationToken cancellationToken = default);
}

public interface IPromptGalleryProjectionCoordinator
{
    Task<PromptGalleryProjectionStatus> GetStatusAsync(CancellationToken cancellationToken = default);

    Task<PromptGalleryProjectionOperationResult> UpsertAsync(
        Guid promptArtifactId,
        CancellationToken cancellationToken = default);

    Task<PromptGalleryProjectionOperationResult> RemoveAsync(
        Guid promptArtifactId,
        CancellationToken cancellationToken = default);

    Task<PromptGalleryProjectionOperationResult> RebuildAsync(CancellationToken cancellationToken = default);
}

public sealed class DisabledPromptGalleryProjectionDriver : IPromptGalleryProjectionDriver
{
    private static readonly PromptGalleryProjectionStatus DisabledStatus = new(
        nameof(DisabledPromptGalleryProjectionDriver),
        Enabled: false,
        PromptGalleryProjectionHealth.Disabled,
        "Prompt Gallery search projection is disabled.");

    public string Name => DisabledStatus.DriverName;

    public bool Enabled => false;

    public Task<PromptGalleryProjectionStatus> GetStatusAsync(CancellationToken cancellationToken = default)
        => Task.FromResult(DisabledStatus);

    public Task UpsertAsync(
        PromptGalleryProjectionDocument document,
        CancellationToken cancellationToken = default)
        => Task.CompletedTask;

    public Task RemoveAsync(Guid promptArtifactId, CancellationToken cancellationToken = default)
        => Task.CompletedTask;

    public Task<int> RebuildAsync(
        IAsyncEnumerable<PromptGalleryProjectionDocument> documents,
        CancellationToken cancellationToken = default)
        => Task.FromResult(0);
}

public sealed class PromptGalleryProjectionCoordinator(
    IDbContextFactory<AppDbContext> dbContextFactory,
    IPromptGalleryProjectionDriver driver) : IPromptGalleryProjectionCoordinator
{
    public Task<PromptGalleryProjectionStatus> GetStatusAsync(CancellationToken cancellationToken = default)
        => driver.GetStatusAsync(cancellationToken);

    public async Task<PromptGalleryProjectionOperationResult> UpsertAsync(
        Guid promptArtifactId,
        CancellationToken cancellationToken = default)
    {
        var status = await driver.GetStatusAsync(cancellationToken);
        if (!driver.Enabled || !status.Enabled)
        {
            return Disabled(status);
        }

        var document = await LoadDocumentAsync(promptArtifactId, cancellationToken);
        if (document is null)
        {
            throw new KeyNotFoundException($"Prompt Gallery item '{promptArtifactId}' was not found for projection.");
        }

        if (document.IsArchived)
        {
            await driver.RemoveAsync(promptArtifactId, cancellationToken);
        }
        else
        {
            await driver.UpsertAsync(document.Document, cancellationToken);
        }

        return Applied(1, await driver.GetStatusAsync(cancellationToken));
    }

    public async Task<PromptGalleryProjectionOperationResult> RemoveAsync(
        Guid promptArtifactId,
        CancellationToken cancellationToken = default)
    {
        var status = await driver.GetStatusAsync(cancellationToken);
        if (!driver.Enabled || !status.Enabled)
        {
            return Disabled(status);
        }

        await driver.RemoveAsync(promptArtifactId, cancellationToken);
        return Applied(1, await driver.GetStatusAsync(cancellationToken));
    }

    public async Task<PromptGalleryProjectionOperationResult> RebuildAsync(
        CancellationToken cancellationToken = default)
    {
        var status = await driver.GetStatusAsync(cancellationToken);
        if (!driver.Enabled || !status.Enabled)
        {
            return Disabled(status);
        }

        var processedCount = await driver.RebuildAsync(LoadDocumentsAsync(cancellationToken), cancellationToken);
        return Applied(processedCount, await driver.GetStatusAsync(cancellationToken));
    }

    private async Task<ProjectionDocumentState?> LoadDocumentAsync(
        Guid promptArtifactId,
        CancellationToken cancellationToken)
    {
        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        var artifact = await dbContext.Set<PromptArtifact>()
            .AsNoTracking()
            .FirstOrDefaultAsync(item => item.Id == promptArtifactId, cancellationToken);
        if (artifact is null)
        {
            return null;
        }

        var tags = await LoadTagsAsync(dbContext, [promptArtifactId], cancellationToken);
        return new ProjectionDocumentState(
            artifact.IsArchived,
            CreateDocument(artifact, tags.GetValueOrDefault(promptArtifactId, [])));
    }

    private async IAsyncEnumerable<PromptGalleryProjectionDocument> LoadDocumentsAsync(
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        const int batchSize = 250;
        await using var idDbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        var artifactIds = idDbContext.Set<PromptArtifact>()
            .AsNoTracking()
            .Where(artifact => !artifact.IsArchived)
            .OrderBy(artifact => artifact.Id)
            .Select(artifact => artifact.Id)
            .AsAsyncEnumerable();
        var batch = new List<Guid>(batchSize);
        await foreach (var promptArtifactId in artifactIds.WithCancellation(cancellationToken))
        {
            batch.Add(promptArtifactId);
            if (batch.Count < batchSize)
            {
                continue;
            }

            var documents = await LoadDocumentBatchAsync(batch, cancellationToken);
            foreach (var document in documents)
            {
                yield return document;
            }

            batch.Clear();
        }

        if (batch.Count == 0)
        {
            yield break;
        }

        var finalDocuments = await LoadDocumentBatchAsync(batch, cancellationToken);
        foreach (var document in finalDocuments)
        {
            yield return document;
        }
    }

    private async Task<IReadOnlyList<PromptGalleryProjectionDocument>> LoadDocumentBatchAsync(
        IReadOnlyCollection<Guid> promptArtifactIds,
        CancellationToken cancellationToken)
    {
        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        var artifacts = await dbContext.Set<PromptArtifact>()
            .AsNoTracking()
            .Where(artifact => !artifact.IsArchived && promptArtifactIds.Contains(artifact.Id))
            .OrderBy(artifact => artifact.Id)
            .ToListAsync(cancellationToken);
        var tags = await LoadTagsAsync(dbContext, promptArtifactIds, cancellationToken);
        return artifacts
            .Select(artifact => CreateDocument(artifact, tags.GetValueOrDefault(artifact.Id, [])))
            .ToArray();
    }

    private static async Task<Dictionary<Guid, IReadOnlyList<string>>> LoadTagsAsync(
        AppDbContext dbContext,
        IReadOnlyCollection<Guid> promptArtifactIds,
        CancellationToken cancellationToken)
    {
        if (promptArtifactIds.Count == 0)
        {
            return [];
        }

        var rows = await (
                from link in dbContext.Set<PromptArtifactTag>().AsNoTracking()
                join tag in dbContext.Set<PromptTag>().AsNoTracking() on link.PromptTagId equals tag.Id
                where promptArtifactIds.Contains(link.PromptArtifactId)
                orderby tag.Name
                select new { link.PromptArtifactId, tag.Name })
            .ToListAsync(cancellationToken);

        return rows
            .GroupBy(row => row.PromptArtifactId)
            .ToDictionary(
                group => group.Key,
                group => (IReadOnlyList<string>)group.Select(row => row.Name).ToList());
    }

    private static PromptGalleryProjectionDocument CreateDocument(
        PromptArtifact artifact,
        IReadOnlyList<string> tags)
        => new(
            artifact.Id,
            artifact.ProjectId,
            artifact.Title,
            artifact.Summary,
            artifact.CurrentDraftText,
            artifact.Kind,
            artifact.Status,
            tags,
            $"/prompt-gallery?promptId={artifact.Id}",
            artifact.UpdatedAtUtc);

    private static PromptGalleryProjectionOperationResult Disabled(PromptGalleryProjectionStatus status)
        => new(PromptGalleryProjectionOperationState.Disabled, 0, status);

    private static PromptGalleryProjectionOperationResult Applied(
        int processedCount,
        PromptGalleryProjectionStatus status)
        => new(PromptGalleryProjectionOperationState.Applied, processedCount, status);

    private sealed record ProjectionDocumentState(bool IsArchived, PromptGalleryProjectionDocument Document);
}
