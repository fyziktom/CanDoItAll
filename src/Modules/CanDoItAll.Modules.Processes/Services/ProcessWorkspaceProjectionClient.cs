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
        using var scope = scopeFactory.CreateScope();
        return await scope.ServiceProvider.GetRequiredService<ProcessWorkspaceShellProjectionService>()
            .ExecuteDefinitionCanvasCommandAsync(command, cancellationToken).ConfigureAwait(false);
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
