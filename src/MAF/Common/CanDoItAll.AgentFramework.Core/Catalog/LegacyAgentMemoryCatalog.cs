using CanDoItAll.AgentFramework.Models;

namespace CanDoItAll.AgentFramework.Core;

internal sealed class LegacyAgentMemoryCatalog(
    ISandboxWorkspaceStore store,
    TimeProvider timeProvider)
{
    public async Task<IReadOnlyList<AgentMemoryRecord>> ListAsync(
        Guid agentId,
        CancellationToken cancellationToken)
    {
        var catalog = await store.LoadCatalogAsync(cancellationToken);
        return catalog.Memory
            .Where(item => item.AgentId == agentId)
            .OrderByDescending(item => item.CreatedAtUtc)
            .ToList();
    }

    public async Task<Guid> SaveAsync(
        MemoryEditorModel model,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(model);
        var recordId = model.Id ?? Guid.NewGuid();
        await store.UpdateCatalogAsync(catalog =>
        {
            var record = new AgentMemoryRecord(
                recordId,
                model.AgentId,
                model.Kind,
                model.Title.Trim(),
                model.Content.Trim(),
                model.Source.Trim(),
                model.Importance,
                model.MetadataJson.Trim(),
                catalog.Memory.FirstOrDefault(item => item.Id == model.Id)?.CreatedAtUtc ??
                timeProvider.GetUtcNow());
            return catalog with
            {
                Memory = catalog.Memory
                    .Where(item => item.Id != recordId)
                    .Append(record)
                    .OrderByDescending(item => item.CreatedAtUtc)
                    .ToList()
            };
        }, cancellationToken);
        return recordId;
    }

    public Task DeleteAsync(Guid memoryId, CancellationToken cancellationToken) =>
        store.UpdateCatalogAsync(catalog => catalog with
        {
            Memory = catalog.Memory.Where(item => item.Id != memoryId).ToList()
        }, cancellationToken);
}
