namespace CanDoItAll.Modules.Projects;

public interface IProjectDefinitionReceiptAccess {
    Task<bool> CanReadAsync(ProjectWriteAdmission target, CancellationToken cancellationToken);
}

public sealed record ProjectDefinitionReceiptInvocation {
    public ProjectDefinitionReceiptInvocation(ProjectMutationCallerScope caller, IProjectDefinitionReceiptAccess receiptAccess) {
        ArgumentNullException.ThrowIfNull(caller);
        ArgumentNullException.ThrowIfNull(receiptAccess);
        Caller = caller;
        ReceiptAccess = receiptAccess;
    }

    public ProjectMutationCallerScope Caller { get; }
    public IProjectDefinitionReceiptAccess ReceiptAccess { get; }
}

public sealed record ProjectDefinitionInvocation {
    public ProjectDefinitionInvocation(ProjectMutationCallerScope caller, IProjectDefinitionReceiptAccess receiptAccess,
        ProjectMutationAuthorization? authorization = null, ProjectDefinitionEffectBinding? effectBinding = null,
        IProjectDefinitionCurrentWriteAccess? currentWrite = null) {
        ArgumentNullException.ThrowIfNull(caller);
        ArgumentNullException.ThrowIfNull(receiptAccess);
        var effect = effectBinding ?? ProjectDefinitionEffectBinding.None;
        if ((caller.Kind is ProjectMutationCallerKind.AuthenticatedSubject or ProjectMutationCallerKind.OpenApi) != (currentWrite is not null)) {
            throw new ArgumentException("HTTP definition invocations require current write observation; local and governed invocations retain their own authority boundary.", nameof(currentWrite));
        }
        if ((authorization is null) != (effect.Kind == ProjectDefinitionEffectKind.None) ||
                caller.Kind == ProjectMutationCallerKind.GovernedExecution && authorization is null) {
            throw new ArgumentException("A governed definition effect requires both its original source binding and current authority boundary.");
        }
        Caller = caller;
        ReceiptAccess = receiptAccess;
        Authorization = authorization;
        EffectBinding = effect;
        CurrentWrite = currentWrite;
    }

    public ProjectMutationCallerScope Caller { get; }
    public IProjectDefinitionReceiptAccess ReceiptAccess { get; }
    public ProjectMutationAuthorization? Authorization { get; }
    public ProjectDefinitionEffectBinding EffectBinding { get; }
    public IProjectDefinitionCurrentWriteAccess? CurrentWrite { get; }
}
