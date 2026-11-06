using CanDoItAll.Editing;
using CanDoItAll.Infrastructure.Editing;
using CanDoItAll.Modules.Projects;
using CanDoItAll.Modules.Projects.Pages;
using CanDoItAll.Modules.Projects.Pages.Components;
using Microsoft.Extensions.Logging;

namespace CanDoItAll.Modules.Workbench;

internal sealed class ProjectRootCreationAccess(EditingChangeFeed changes, IEditingUiAccess access,
    ProjectChanges projects, ILogger<EditingObservation> logger) : IAsyncDisposable {
    private readonly EditingObservation observation = new(changes, access, new(projects.Scope, EditingOwner.Project), logger);

    public Task OpenAsync() => observation.StartAsync(_ => Task.CompletedTask, () => Task.CompletedTask);

    public async ValueTask AuthorizeReadAsync(CancellationToken cancellationToken) {
        try {
            await observation.AuthorizeAsync(write: false, cancellationToken);
        } catch (UnauthorizedAccessException) {
            Stop();
            throw;
        }
    }

    public async ValueTask AuthorizeWriteAsync(CancellationToken cancellationToken) {
        try {
            await observation.AuthorizeAsync(write: true, cancellationToken);
        } catch (UnauthorizedAccessException) {
            Stop();
            throw;
        }
    }

    public async Task<T> ReadAsync<T>(Func<CancellationToken, Task<T>> read, CancellationToken cancellationToken) {
        await AuthorizeReadAsync(cancellationToken);
        var result = await read(cancellationToken);
        await AuthorizeReadAsync(cancellationToken);
        return result;
    }

    public async Task<ProjectPendingDispatchOutcome> DispatchAsync(ProjectNativeDefinitionOperations definitions,
        Guid editorId, ProjectDefinitionSubmission submitted, CancellationToken cancellationToken) {
        await AuthorizeWriteAsync(cancellationToken);
        using var admission = projects.EnterUiWrite(AuthorizeWriteAsync);
        using var identity = projects.Enter(new(editorId, submitted.Operation.Id));
        return await definitions.DispatchAsync(submitted, cancellationToken);
    }

    public void Stop() => observation.Stop();
    public ValueTask DisposeAsync() => observation.DisposeAsync();
}
