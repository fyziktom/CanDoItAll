namespace CanDoItAll.Workspace.ApiAccess.UiSandbox;

public sealed class ApiScenarioGate {
    private readonly TaskCompletionSource release = new(TaskCreationOptions.RunContinuationsAsynchronously);
    public TaskCompletionSource Entered { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
    public bool IsReleased => release.Task.IsCompleted;
    public async Task WaitAsync(CancellationToken cancellationToken) {
        Entered.TrySetResult();
        await release.Task.WaitAsync(cancellationToken);
    }
    public void Release() => release.TrySetResult();
}

public enum ApiScenario { Representative, Empty, MultiplePages, Denied, StatusUnavailable, AccessUnavailable, ReadUnavailable, AuthorizationOff, MissingSigningKey }
public enum ApiScenarioOperation { Status, Access, TokenSearch, TokenObserve, IssueToken, RevokeToken, DeleteToken, AccountSearch, AccountObserve, CreateAccount, UpdateAccount, ResetPassword, DeleteAccount }
public enum ApiScenarioFault { None, Unavailable, Denied, Invalid, Conflict, Missing, CommittedWarning, UnknownWithoutCommit, UnknownAfterCommit }
