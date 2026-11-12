using CanDoItAll.SharedKernel;

namespace CanDoItAll.Modules.Projects;

public enum ProjectDefinitionWriteAccessDecision { Denied, Allowed, CallerChanged, AuthorityChanged, Unavailable }

public interface IProjectDefinitionCurrentWriteAccess {
    ValueTask<ProjectDefinitionWriteAccessDecision> ObserveAsync(CancellationToken cancellationToken);
}

internal static class ProjectDefinitionCurrentWriteGuard {
    internal static async ValueTask RequireAsync(ProjectDefinitionOperationAttempt attempt, CancellationToken cancellationToken) {
        cancellationToken.ThrowIfCancellationRequested();
        if (attempt.Invocation.CurrentWrite is not { } access) {
            return;
        }
        var decision = await access.ObserveAsync(cancellationToken);
        cancellationToken.ThrowIfCancellationRequested();
        if (decision == ProjectDefinitionWriteAccessDecision.Allowed) {
            return;
        }
        attempt.CurrentWriteRefusal = decision;
        throw new ProjectDefinitionProtocolRejectedException(ToError(decision));
    }

    internal static Error ToError(ProjectDefinitionWriteAccessDecision decision) => decision switch {
        ProjectDefinitionWriteAccessDecision.CallerChanged => Error.Failure(
            "The current caller no longer matches the original authenticated request.", ProjectErrorCodes.DefinitionCallerChanged),
        ProjectDefinitionWriteAccessDecision.AuthorityChanged => Error.Failure(
            "Account authority changed. Revalidate access before resubmitting the complete original request.", ProjectErrorCodes.DefinitionAuthorityChanged),
        ProjectDefinitionWriteAccessDecision.Unavailable => Error.Failure(
            "Current Projects write authority could not be observed. This attempt did not commit.", ProjectErrorCodes.DefinitionAuthorityUnavailable),
        _ => Error.Failure("Current Projects read and write access is required.", ProjectErrorCodes.DefinitionWriteDenied)
    };
}
