using CanDoItAll.Processes.Application;
using CanDoItAll.Processes.Projections;
using Microsoft.Extensions.DependencyInjection;

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

    Task<ProcessRuntimeOperatorActionResult> ExecuteRuntimeOperatorActionAsync(
        ProcessRuntimeOperatorActionCommand command,
        CancellationToken cancellationToken = default);
}

public sealed class ProcessWorkspaceProjectionClient(
    IServiceScopeFactory scopeFactory,
    ProcessDefinitionCanvasEditorProjectionService canvasSessionService) : IProcessWorkspaceProjectionClient
{
    public async Task<ProcessWorkspaceShellProjection> GetShellAsync(
        ProcessWorkspaceShellRequest request,
        CancellationToken cancellationToken = default)
    {
        using var scope = scopeFactory.CreateScope();
        var projection = await scope.ServiceProvider
            .GetRequiredService<ProcessWorkspaceShellProjectionService>()
            .GetShellAsync(request, cancellationToken)
            .ConfigureAwait(false);
        if (projection.DefinitionCatalog.SelectedEditor is not { Canvas: not null } selectedEditor)
        {
            return projection;
        }

        var canvas = await canvasSessionService
            .GetCanvasAsync(request.Scope, selectedEditor.DefinitionKey, cancellationToken)
            .ConfigureAwait(false);
        return projection with
        {
            DefinitionCatalog = projection.DefinitionCatalog with
            {
                SelectedEditor = selectedEditor with { Canvas = canvas }
            }
        };
    }

    public async Task<ProcessDefinitionCatalogCommandReceipt> FeedDefaultDefinitionsAsync(
        ProcessDefinitionFeedDefaultsCommand command,
        CancellationToken cancellationToken = default)
    {
        using var scope = scopeFactory.CreateScope();
        return await scope.ServiceProvider
            .GetRequiredService<ProcessWorkspaceShellProjectionService>()
            .FeedDefaultDefinitionsAsync(command, cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task<ProcessDefinitionEditorCommandResult> ExecuteDefinitionEditorCommandAsync(
        ProcessDefinitionEditorCommand command,
        CancellationToken cancellationToken = default)
    {
        using var scope = scopeFactory.CreateScope();
        return await scope.ServiceProvider
            .GetRequiredService<ProcessWorkspaceShellProjectionService>()
            .ExecuteDefinitionEditorCommandAsync(command, cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task<ProcessDefinitionRoleEditorCommandResult> ExecuteDefinitionRoleEditorCommandAsync(
        ProcessDefinitionRoleEditorCommand command,
        CancellationToken cancellationToken = default)
    {
        using var scope = scopeFactory.CreateScope();
        return await scope.ServiceProvider
            .GetRequiredService<ProcessWorkspaceShellProjectionService>()
            .ExecuteDefinitionRoleEditorCommandAsync(command, cancellationToken)
            .ConfigureAwait(false);
    }

    public Task<ProcessDefinitionCanvasCommandResult> ExecuteDefinitionCanvasCommandAsync(
        ProcessDefinitionCanvasCommand command,
        CancellationToken cancellationToken = default)
        => canvasSessionService.ExecuteCommandAsync(command, cancellationToken);

    public async Task<ProcessDefinitionStepEditorCommandResult> ExecuteDefinitionStepEditorCommandAsync(
        ProcessDefinitionStepEditorCommand command,
        CancellationToken cancellationToken = default)
    {
        using var scope = scopeFactory.CreateScope();
        return await scope.ServiceProvider
            .GetRequiredService<ProcessWorkspaceShellProjectionService>()
            .ExecuteDefinitionStepEditorCommandAsync(command, cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task<ProcessTemplateImportCommandResult> ExecuteTemplateImportCommandAsync(
        ProcessTemplateImportCommand command,
        CancellationToken cancellationToken = default)
    {
        using var scope = scopeFactory.CreateScope();
        return await scope.ServiceProvider
            .GetRequiredService<ProcessWorkspaceShellProjectionService>()
            .ExecuteTemplateImportCommandAsync(command, cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task<ProcessRuntimeOperatorActionResult> ExecuteRuntimeOperatorActionAsync(
        ProcessRuntimeOperatorActionCommand command,
        CancellationToken cancellationToken = default)
    {
        using var scope = scopeFactory.CreateScope();
        return await scope.ServiceProvider
            .GetRequiredService<ProcessRuntimeOperatorApplicationService>()
            .ExecuteAsync(command, cancellationToken)
            .ConfigureAwait(false);
    }
}
