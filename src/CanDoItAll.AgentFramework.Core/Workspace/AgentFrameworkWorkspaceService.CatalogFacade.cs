using CanDoItAll.AgentFramework.Models;

namespace CanDoItAll.AgentFramework.Core;

public sealed partial class AgentFrameworkWorkspaceService
{
    public Task<IReadOnlyList<AgentDefinition>> ListAgentsAsync(
        bool includeTemplates = true,
        CancellationToken cancellationToken = default)
        => catalogService.ListAgentsAsync(includeTemplates, cancellationToken);

    public Task<AgentEditorModel> GetAgentEditorAsync(
        Guid? agentId = null,
        CancellationToken cancellationToken = default)
        => catalogService.GetAgentEditorAsync(agentId, cancellationToken);

    public Task<Guid> SaveAgentAsync(AgentEditorModel model, CancellationToken cancellationToken = default)
        => catalogService.SaveAgentAsync(model, cancellationToken);

    public Task DeleteAgentAsync(Guid agentId, CancellationToken cancellationToken = default)
        => catalogService.DeleteAgentAsync(agentId, cancellationToken);

    public Task<Guid> CloneAgentAsync(Guid agentId, string cloneName, CancellationToken cancellationToken = default)
        => catalogService.CloneAgentAsync(agentId, cloneName, cancellationToken);

    public Task<Guid> ConvertToTemplateAsync(Guid agentId, string templateKey, CancellationToken cancellationToken = default)
        => catalogService.ConvertToTemplateAsync(agentId, templateKey, cancellationToken);

    public Task<AgentExportResult> ExportAgentAsync(Guid agentId, CancellationToken cancellationToken = default)
        => catalogService.ExportAgentAsync(agentId, cancellationToken);

    public Task<Guid> ImportAgentAsync(string packagePath, CancellationToken cancellationToken = default)
        => catalogService.ImportAgentAsync(packagePath, cancellationToken);

    public Task<IReadOnlyList<ProviderProfile>> ListProvidersAsync(CancellationToken cancellationToken = default)
        => catalogService.ListProvidersAsync(cancellationToken);

    public Task<ProviderProfileEditorModel> GetProviderEditorAsync(
        Guid? providerId = null,
        CancellationToken cancellationToken = default)
        => catalogService.GetProviderEditorAsync(providerId, cancellationToken);

    public Task<Guid> SaveProviderAsync(
        ProviderProfileEditorModel model,
        CancellationToken cancellationToken = default)
        => catalogService.SaveProviderAsync(model, cancellationToken);

    public Task DeleteProviderAsync(Guid providerId, CancellationToken cancellationToken = default)
        => catalogService.DeleteProviderAsync(providerId, cancellationToken);

    public Task<ProviderHealthResult> TestProviderAsync(Guid providerId, CancellationToken cancellationToken = default)
        => catalogService.TestProviderAsync(providerId, cancellationToken);

    public Task<ProviderTestChatResult> RunProviderTestChatAsync(
        Guid providerId,
        ProviderTestChatRequest request,
        CancellationToken cancellationToken = default)
        => catalogService.RunProviderTestChatAsync(providerId, request, cancellationToken);

    public Task<OllamaModelfileResult> CreateOrUpdateOllamaModelAsync(
        Guid providerId,
        OllamaModelfileRequest request,
        CancellationToken cancellationToken = default)
        => catalogService.CreateOrUpdateOllamaModelAsync(providerId, request, cancellationToken);

    public Task<IReadOnlyList<CapabilityCatalogItem>> ListCapabilitiesAsync(CancellationToken cancellationToken = default)
        => catalogService.ListCapabilitiesAsync(cancellationToken);

    public Task<CapabilityEditorModel> GetCapabilityEditorAsync(
        Guid? capabilityId = null,
        CancellationToken cancellationToken = default)
        => catalogService.GetCapabilityEditorAsync(capabilityId, cancellationToken);

    public Task<Guid> SaveCapabilityAsync(
        CapabilityEditorModel model,
        CancellationToken cancellationToken = default)
        => catalogService.SaveCapabilityAsync(model, cancellationToken);

    public Task DeleteCapabilityAsync(Guid capabilityId, CancellationToken cancellationToken = default)
        => catalogService.DeleteCapabilityAsync(capabilityId, cancellationToken);

    public Task VerifyCapabilityAsync(Guid agentId, Guid capabilityId, CancellationToken cancellationToken = default)
        => catalogService.VerifyCapabilityAsync(agentId, capabilityId, cancellationToken);

    public Task<IReadOnlyList<AgentMemoryRecord>> ListMemoryAsync(
        Guid agentId,
        CancellationToken cancellationToken = default)
        => catalogService.ListMemoryAsync(agentId, cancellationToken);

    public Task<Guid> SaveMemoryAsync(
        MemoryEditorModel model,
        CancellationToken cancellationToken = default)
        => catalogService.SaveMemoryAsync(model, cancellationToken);

    public Task DeleteMemoryAsync(Guid memoryId, CancellationToken cancellationToken = default)
        => catalogService.DeleteMemoryAsync(memoryId, cancellationToken);
}
