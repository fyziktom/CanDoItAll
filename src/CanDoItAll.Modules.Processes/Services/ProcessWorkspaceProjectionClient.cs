using CanDoItAll.Processes.Application;
using CanDoItAll.Processes.Projections;

namespace CanDoItAll.Modules.Processes;

public interface IProcessWorkspaceProjectionClient
{
    Task<ProcessWorkspaceShellProjection> GetShellAsync(
        ProcessWorkspaceShellRequest request,
        CancellationToken cancellationToken = default);
}

public sealed class ProcessWorkspaceProjectionClient(
    ProcessWorkspaceShellProjectionService shellProjectionService) : IProcessWorkspaceProjectionClient
{
    public Task<ProcessWorkspaceShellProjection> GetShellAsync(
        ProcessWorkspaceShellRequest request,
        CancellationToken cancellationToken = default)
        => shellProjectionService.GetShellAsync(request, cancellationToken);
}
