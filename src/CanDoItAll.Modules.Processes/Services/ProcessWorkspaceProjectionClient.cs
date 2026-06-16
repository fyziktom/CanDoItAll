using CanDoItAll.Processes.Application;
using CanDoItAll.Processes.Projections;

namespace CanDoItAll.Modules.Processes;

public interface IProcessWorkspaceProjectionClient
{
    Task<ProcessWorkspaceShellProjection> GetShellAsync(
        ProcessWorkspaceShellRequest request,
        CancellationToken cancellationToken = default);

    Task<ProcessDefinitionCatalogCommandReceipt> FeedDefaultDefinitionsAsync(
        ProcessDefinitionFeedDefaultsCommand command,
        CancellationToken cancellationToken = default);
}

public sealed class ProcessWorkspaceProjectionClient(
    ProcessWorkspaceShellProjectionService shellProjectionService) : IProcessWorkspaceProjectionClient
{
    public Task<ProcessWorkspaceShellProjection> GetShellAsync(
        ProcessWorkspaceShellRequest request,
        CancellationToken cancellationToken = default)
        => shellProjectionService.GetShellAsync(request, cancellationToken);

    public Task<ProcessDefinitionCatalogCommandReceipt> FeedDefaultDefinitionsAsync(
        ProcessDefinitionFeedDefaultsCommand command,
        CancellationToken cancellationToken = default)
        => shellProjectionService.FeedDefaultDefinitionsAsync(command, cancellationToken);
}
