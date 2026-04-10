using CanDoItAll.Mcp.Core.Contracts;
using CanDoItAll.Modules.Processes;
using CanDoItAll.Modules.Projects;
using CanDoItAll.SharedKernel;
using Microsoft.Extensions.DependencyInjection;

namespace CanDoItAll.Mcp.Processes;

public interface IProcessesCoordinator
{
    Task<IReadOnlyList<ProcessDefinitionListItem>> ListDefinitionsAsync(Guid? projectId, CancellationToken cancellationToken = default);

    Task<ProcessDefinitionEditorModel> GetDefinitionEditorAsync(Guid? definitionId, Guid? projectId, CancellationToken cancellationToken = default);

    Task<Guid> SaveDefinitionAsync(ProcessDefinitionEditorModel model, CancellationToken cancellationToken = default);

    Task PublishDefinitionAsync(Guid definitionId, CancellationToken cancellationToken = default);

    Task DeleteDefinitionAsync(Guid definitionId, CancellationToken cancellationToken = default);

    Task<ProcessImportExportEnvelope> ExportDefinitionAsync(Guid definitionId, CancellationToken cancellationToken = default);

    Task<Guid> ImportDefinitionAsync(ProcessImportExportEnvelope envelope, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<ProcessRunListItem>> ListRunsAsync(Guid? definitionId, Guid? projectId, CancellationToken cancellationToken = default);

    Task<ProcessRunDetailToolData> GetRunDetailAsync(Guid runId, CancellationToken cancellationToken = default);

    Task<ProcessAnalyticsSummary> GetAnalyticsAsync(Guid? definitionId, Guid? projectId, CancellationToken cancellationToken = default);

    Task<Guid> StartRunAsync(ProcessRunStartRequest request, CancellationToken cancellationToken = default);

    Task TransitionStepAsync(ProcessStepTransitionRequest request, CancellationToken cancellationToken = default);

    Task ResolveAssignmentAsync(ProcessAssignmentResolutionRequest request, CancellationToken cancellationToken = default);

    Task<Guid> RecordArtifactAsync(ProcessArtifactRecordRequest request, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<ProjectPartyOption>> ListPartyOptionsAsync(Guid projectId, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<ProcessExecutorRegistryOption>> ListExecutorOptionsAsync(CancellationToken cancellationToken = default);
}

public sealed class ProcessesCoordinator(IServiceScopeFactory scopeFactory) : IProcessesCoordinator
{
    public Task<IReadOnlyList<ProcessDefinitionListItem>> ListDefinitionsAsync(Guid? projectId, CancellationToken cancellationToken = default)
    {
        return ExecuteAsync(service => service.ListDefinitionsAsync(projectId, cancellationToken));
    }

    public Task<ProcessDefinitionEditorModel> GetDefinitionEditorAsync(Guid? definitionId, Guid? projectId, CancellationToken cancellationToken = default)
    {
        return ExecuteAsync(
            async service =>
            {
                var editor = await service.GetEditorAsync(definitionId, projectId, cancellationToken);
                if (definitionId.HasValue && !editor.Id.HasValue)
                {
                    throw new ToolInvocationException(
                        "ProcessDefinitionNotFound",
                        $"Process definition '{definitionId.Value}' was not found.",
                        new { DefinitionId = definitionId.Value });
                }

                return editor;
            });
    }

    public Task<Guid> SaveDefinitionAsync(ProcessDefinitionEditorModel model, CancellationToken cancellationToken = default)
    {
        return ExecuteAsync(async service => EnsureSuccess(await service.SaveAsync(model, cancellationToken)));
    }

    public Task PublishDefinitionAsync(Guid definitionId, CancellationToken cancellationToken = default)
    {
        return ExecuteAsync(
            async service =>
            {
                EnsureSuccess(await service.PublishAsync(definitionId, cancellationToken));
            });
    }

    public Task DeleteDefinitionAsync(Guid definitionId, CancellationToken cancellationToken = default)
    {
        return ExecuteAsync(
            async service =>
            {
                var definitions = await service.ListDefinitionsAsync(cancellationToken: cancellationToken);
                if (definitions.All(item => item.Id != definitionId))
                {
                    throw new ToolInvocationException(
                        "ProcessDefinitionNotFound",
                        $"Process definition '{definitionId}' was not found.",
                        new { DefinitionId = definitionId });
                }

                await service.DeleteAsync(definitionId, cancellationToken);
                return definitionId;
            });
    }

    public Task<ProcessImportExportEnvelope> ExportDefinitionAsync(Guid definitionId, CancellationToken cancellationToken = default)
    {
        return ExecuteAsync(service => service.ExportAsync(definitionId, cancellationToken));
    }

    public Task<Guid> ImportDefinitionAsync(ProcessImportExportEnvelope envelope, CancellationToken cancellationToken = default)
    {
        return ExecuteAsync(async service => EnsureSuccess(await service.ImportAsync(envelope, cancellationToken)));
    }

    public Task<IReadOnlyList<ProcessRunListItem>> ListRunsAsync(Guid? definitionId, Guid? projectId, CancellationToken cancellationToken = default)
    {
        return ExecuteAsync(service => service.ListRunsAsync(definitionId, projectId, cancellationToken));
    }

    public Task<ProcessRunDetailToolData> GetRunDetailAsync(Guid runId, CancellationToken cancellationToken = default)
    {
        return ExecuteAsync(
            async service =>
            {
                var run = (await service.ListRunsAsync(cancellationToken: cancellationToken))
                    .SingleOrDefault(item => item.Id == runId);
                if (run is null)
                {
                    throw new ToolInvocationException(
                        "ProcessRunNotFound",
                        $"Process run '{runId}' was not found.",
                        new { RunId = runId });
                }

                return new ProcessRunDetailToolData(
                    run,
                    await service.ListStepRunsAsync(runId, cancellationToken),
                    await service.ListDecisionRecordsAsync(runId, cancellationToken),
                    await service.ListArtifactsAsync(runId, cancellationToken),
                    await service.ListAssignmentsAsync(runId, cancellationToken),
                    await service.ListWorkBriefsAsync(runId, cancellationToken),
                    await service.ListConformanceObservationsAsync(runId, cancellationToken),
                    await service.ListImprovementsAsync(run.ProcessDefinitionId, cancellationToken));
            });
    }

    public Task<ProcessAnalyticsSummary> GetAnalyticsAsync(Guid? definitionId, Guid? projectId, CancellationToken cancellationToken = default)
    {
        return ExecuteAsync(service => service.GetAnalyticsAsync(definitionId, projectId, cancellationToken));
    }

    public Task<Guid> StartRunAsync(ProcessRunStartRequest request, CancellationToken cancellationToken = default)
    {
        return ExecuteAsync(async service => EnsureSuccess(await service.StartRunAsync(request, cancellationToken)));
    }

    public Task TransitionStepAsync(ProcessStepTransitionRequest request, CancellationToken cancellationToken = default)
    {
        return ExecuteAsync(
            async service =>
            {
                EnsureSuccess(await service.TransitionStepAsync(request, cancellationToken));
            });
    }

    public Task ResolveAssignmentAsync(ProcessAssignmentResolutionRequest request, CancellationToken cancellationToken = default)
    {
        return ExecuteAsync(
            async service =>
            {
                EnsureSuccess(await service.ResolveAssignmentAsync(request, cancellationToken));
            });
    }

    public Task<Guid> RecordArtifactAsync(ProcessArtifactRecordRequest request, CancellationToken cancellationToken = default)
    {
        return ExecuteAsync(async service => EnsureSuccess(await service.RecordArtifactAsync(request, cancellationToken)));
    }

    public Task<IReadOnlyList<ProjectPartyOption>> ListPartyOptionsAsync(Guid projectId, CancellationToken cancellationToken = default)
    {
        return ExecuteAsync(service => service.ListPartyOptionsAsync(projectId, cancellationToken));
    }

    public Task<IReadOnlyList<ProcessExecutorRegistryOption>> ListExecutorOptionsAsync(CancellationToken cancellationToken = default)
    {
        return ExecuteAsync(service => service.ListExecutorOptionsAsync(cancellationToken));
    }

    private async Task<T> ExecuteAsync<T>(Func<ProcessesService, Task<T>> callback)
    {
        await using var scope = scopeFactory.CreateAsyncScope();
        var service = scope.ServiceProvider.GetRequiredService<ProcessesService>();
        return await callback(service);
    }

    private async Task ExecuteAsync(Func<ProcessesService, Task> callback)
    {
        await using var scope = scopeFactory.CreateAsyncScope();
        var service = scope.ServiceProvider.GetRequiredService<ProcessesService>();
        await callback(service);
    }

    private static Guid EnsureSuccess(Result<Guid> result)
    {
        if (result.IsSuccess)
        {
            return result.Value;
        }

        throw CreateToolInvocationException(result.Errors);
    }

    private static void EnsureSuccess(Result result)
    {
        if (result.IsFailure)
        {
            throw CreateToolInvocationException(result.Errors);
        }
    }

    private static ToolInvocationException CreateToolInvocationException(IReadOnlyList<Error> errors)
    {
        var firstError = errors.FirstOrDefault() ?? Error.Failure("The process operation failed.", "processes.failure");
        var details = errors.Select(
            item => new
            {
                item.Code,
                item.Message,
                Severity = item.Severity.ToString()
            });

        return new ToolInvocationException(firstError.Code, firstError.Message, details);
    }
}
