using CanDoItAll.Processes.Application;
using CanDoItAll.Processes.Projections;
using CanDoItAll.Modules.Projects;
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

    Task<ProcessRuntimeRunCancellationResult> RequestRunCancellationAsync(
        ProcessRuntimeRunCancellationCommand command,
        CancellationToken cancellationToken = default);
}

public sealed class ProcessWorkspaceProjectionClient(
    IServiceScopeFactory scopeFactory,
    ProcessDefinitionCanvasEditorProjectionService canvasSessionService,
    ProjectWriteAdmissionService projectAdmissions) : IProcessWorkspaceProjectionClient
{
    public async Task<ProcessWorkspaceShellProjection> GetShellAsync(
        ProcessWorkspaceShellRequest request,
        CancellationToken cancellationToken = default)
    {
        var admission = request.Scope.ProjectId is { } projectId
            ? await projectAdmissions.CaptureAsync(projectId, cancellationToken)
                ?? throw new InvalidOperationException("The Process workspace project no longer exists.") : null;
        var binding = admission is null ? null : new ProcessProjectionProjectBinding(admission.DatabaseProfileId, admission.ProjectId, admission.LifetimeId);
        if (request.ProjectBinding != binding && request.RuntimeQuery is { } runtime) {
            request = request with {
                RuntimeQuery = runtime with { PreviouslyLoadedRuns = null, SelectedRunId = request.Selection.RunId, EventPage = 0 }
            };
        }
        request = request with { ProjectBinding = binding };
        using var scope = scopeFactory.CreateScope();
        var projection = await scope.ServiceProvider
            .GetRequiredService<ProcessWorkspaceShellProjectionService>()
            .GetShellAsync(request, cancellationToken)
            .ConfigureAwait(false);
        if (projection.DefinitionCatalog.SelectedEditor is { Canvas: not null } selectedEditor) {
            var canvas = await canvasSessionService.GetCanvasAsync(request.Scope, selectedEditor.DefinitionKey, cancellationToken, binding)
                .ConfigureAwait(false);
            projection = projection with {
                DefinitionCatalog = projection.DefinitionCatalog with { SelectedEditor = selectedEditor with { Canvas = canvas } }
            };
        }
        if (admission is not null) {
            await projectAdmissions.RequireCurrentAsync(admission, cancellationToken);
        }
        return projection with { ProjectBinding = binding };
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

    public async Task<ProcessDefinitionCanvasCommandResult> ExecuteDefinitionCanvasCommandAsync(
        ProcessDefinitionCanvasCommand command,
        CancellationToken cancellationToken = default)
    {
        var admission = command.Scope.ProjectId is { } projectId
            ? await projectAdmissions.CaptureAsync(projectId, cancellationToken)
                ?? throw new InvalidOperationException("The Process canvas project no longer exists.") : null;
        var binding = admission is null ? null : new ProcessProjectionProjectBinding(admission.DatabaseProfileId, admission.ProjectId, admission.LifetimeId);
        var result = await canvasSessionService.ExecuteCommandAsync(command, cancellationToken, binding);
        if (admission is not null) {
            await projectAdmissions.RequireCurrentAsync(admission, cancellationToken);
        }
        return result;
    }

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

    public async Task<ProcessRuntimeRunCancellationResult> RequestRunCancellationAsync(
        ProcessRuntimeRunCancellationCommand command,
        CancellationToken cancellationToken = default) {
        using var scope = scopeFactory.CreateScope();
        return await scope.ServiceProvider
            .GetRequiredService<ProcessRuntimeOperatorApplicationService>()
            .RequestCancellationAsync(command, cancellationToken)
            .ConfigureAwait(false);
    }
}
