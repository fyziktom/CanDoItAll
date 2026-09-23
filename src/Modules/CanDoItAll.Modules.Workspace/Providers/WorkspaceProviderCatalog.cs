namespace CanDoItAll.Modules.Workspace;

public sealed record WorkspaceProviderOption(
    Guid Id,
    string Name,
    bool IsEnabled);

public interface IWorkspaceProviderCatalog
{
    Task<IReadOnlyList<WorkspaceProviderOption>> ListAsync(
        CancellationToken cancellationToken = default);
}
