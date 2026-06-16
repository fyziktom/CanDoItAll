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

    Task<ProcessDefinitionEditorCommandResult> ExecuteDefinitionEditorCommandAsync(
        ProcessDefinitionEditorCommand command,
        CancellationToken cancellationToken = default);

    Task<ProcessDefinitionRoleEditorCommandResult> ExecuteDefinitionRoleEditorCommandAsync(
        ProcessDefinitionRoleEditorCommand command,
        CancellationToken cancellationToken = default);

    Task<ProcessDefinitionCanvasCommandResult> ExecuteDefinitionCanvasCommandAsync(
        ProcessDefinitionCanvasCommand command,
        CancellationToken cancellationToken = default);

    Task<ProcessDefinitionStepEditorCommandResult> ExecuteDefinitionStepEditorCommandAsync(
        ProcessDefinitionStepEditorCommand command,
        CancellationToken cancellationToken = default);

    Task<ProcessTemplateImportCommandResult> ExecuteTemplateImportCommandAsync(
        ProcessTemplateImportCommand command,
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

    public Task<ProcessDefinitionEditorCommandResult> ExecuteDefinitionEditorCommandAsync(
        ProcessDefinitionEditorCommand command,
        CancellationToken cancellationToken = default)
        => shellProjectionService.ExecuteDefinitionEditorCommandAsync(command, cancellationToken);

    public Task<ProcessDefinitionRoleEditorCommandResult> ExecuteDefinitionRoleEditorCommandAsync(
        ProcessDefinitionRoleEditorCommand command,
        CancellationToken cancellationToken = default)
        => shellProjectionService.ExecuteDefinitionRoleEditorCommandAsync(command, cancellationToken);

    public Task<ProcessDefinitionCanvasCommandResult> ExecuteDefinitionCanvasCommandAsync(
        ProcessDefinitionCanvasCommand command,
        CancellationToken cancellationToken = default)
        => shellProjectionService.ExecuteDefinitionCanvasCommandAsync(command, cancellationToken);

    public Task<ProcessDefinitionStepEditorCommandResult> ExecuteDefinitionStepEditorCommandAsync(
        ProcessDefinitionStepEditorCommand command,
        CancellationToken cancellationToken = default)
        => shellProjectionService.ExecuteDefinitionStepEditorCommandAsync(command, cancellationToken);

    public Task<ProcessTemplateImportCommandResult> ExecuteTemplateImportCommandAsync(
        ProcessTemplateImportCommand command,
        CancellationToken cancellationToken = default)
        => shellProjectionService.ExecuteTemplateImportCommandAsync(command, cancellationToken);
}
