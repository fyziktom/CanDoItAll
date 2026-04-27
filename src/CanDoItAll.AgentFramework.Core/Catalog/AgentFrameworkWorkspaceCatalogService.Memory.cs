using CanDoItAll.AgentFramework.Models;

namespace CanDoItAll.AgentFramework.Core;

internal sealed partial class AgentFrameworkWorkspaceCatalogService
{
    public async Task<IReadOnlyList<AgentMemoryRecord>> ListMemoryAsync(
        Guid agentId,
        CancellationToken cancellationToken = default)
    {
        var catalog = await store.LoadCatalogAsync(cancellationToken);
        return catalog.Memory
            .Where(item => item.AgentId == agentId)
            .OrderByDescending(item => item.CreatedAtUtc)
            .ToList();
    }

    public async Task<Guid> SaveMemoryAsync(
        MemoryEditorModel model,
        CancellationToken cancellationToken = default)
    {
        var recordId = model.Id ?? Guid.NewGuid();
        await UpdateCatalogAsync(catalog =>
        {
            var record = new AgentMemoryRecord(
                Id: recordId,
                AgentId: model.AgentId,
                Kind: model.Kind,
                Title: model.Title.Trim(),
                Content: model.Content.Trim(),
                Source: model.Source.Trim(),
                Importance: model.Importance,
                MetadataJson: model.MetadataJson.Trim(),
                CreatedAtUtc: catalog.Memory.FirstOrDefault(item => item.Id == model.Id)?.CreatedAtUtc ?? DateTimeOffset.UtcNow);

            return catalog with
            {
                Memory = catalog.Memory
                    .Where(item => item.Id != record.Id)
                    .Append(record)
                    .OrderByDescending(item => item.CreatedAtUtc)
                    .ToList()
            };
        }, cancellationToken);

        return recordId;
    }

    public async Task DeleteMemoryAsync(Guid memoryId, CancellationToken cancellationToken = default)
    {
        await UpdateCatalogAsync(catalog => catalog with
        {
            Memory = catalog.Memory.Where(item => item.Id != memoryId).ToList()
        }, cancellationToken);
    }
}
