using CanDoItAll.Infrastructure.Editing;
using CanDoItAll.Modules.Projects;
using CanDoItAll.Workbench.Planning.UI;

namespace CanDoItAll.Modules.Workbench;

internal sealed class ProjectStructureTaskEditorAccess(EditingObservation observation, ProjectWriteAdmissionService admissions,
    ProjectWriteAdmission project, Func<bool> isCurrent) : IAsyncDisposable {
    private bool retired;
    public bool IsActive => !retired && isCurrent() && !observation.Lifetime.IsCancellationRequested;
    public PlanningTaskUnavailableReason? UnavailableReason { get; private set; }
    public event Action? Retired;

    public async Task OpenAsync(CancellationToken cancellationToken) {
        await observation.StartAsync(_ => Task.CompletedTask, () => {
            Retire(PlanningTaskUnavailableReason.AccessChanged, stopObservation: false);
            return Task.CompletedTask;
        });
        await AuthorizeReadAsync(cancellationToken);
    }

    public ValueTask AuthorizeReadAsync(CancellationToken cancellationToken) => AuthorizeAsync(false, cancellationToken);
    public ValueTask AuthorizeWriteAsync(CancellationToken cancellationToken) => AuthorizeAsync(true, cancellationToken);

    public async Task<T> ReadPrivateAsync<T>(Func<CancellationToken, Task<T>> read, CancellationToken cancellationToken) {
        await AuthorizeReadAsync(cancellationToken);
        var result = await read(cancellationToken);
        await AuthorizeReadAsync(cancellationToken);
        return result;
    }

    private async ValueTask AuthorizeAsync(bool write, CancellationToken cancellationToken) {
        try {
            if (!IsActive) {
                Retire(isCurrent() ? PlanningTaskUnavailableReason.AccessChanged : PlanningTaskUnavailableReason.OpeningRetired);
                throw new UnauthorizedAccessException("The original Task editor is no longer current.");
            }
            await observation.AuthorizeAsync(write, cancellationToken);
            await admissions.RequireCurrentAsync(project, cancellationToken);
            if (!IsActive) {
                throw new UnauthorizedAccessException("Structure editing access changed. Reopen with current access.");
            }
        } catch (ProjectWriteAdmissionRejectedException) {
            Retire(PlanningTaskUnavailableReason.ProjectLifetimeChanged);
            throw;
        } catch (UnauthorizedAccessException) {
            Retire(PlanningTaskUnavailableReason.AccessChanged);
            throw;
        }
    }

    public void Retire(PlanningTaskUnavailableReason reason, bool stopObservation = true) {
        if (retired) {
            return;
        }
        retired = true;
        UnavailableReason = reason;
        if (stopObservation) {
            observation.Stop();
        }
        Retired?.Invoke();
    }

    public async ValueTask DisposeAsync() {
        Retire(PlanningTaskUnavailableReason.OpeningRetired);
        await observation.DisposeAsync();
    }
}
