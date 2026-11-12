namespace CanDoItAll.Modules.Projects;

public interface IProjectLifecycleReceiptAccess {
    Task<bool> CanReadAsync(ProjectLifecycleSubmission original, CancellationToken cancellationToken);
}

public sealed record ProjectLifecycleReceiptInvocation {
    public ProjectLifecycleReceiptInvocation(ProjectMutationCallerScope caller, IProjectLifecycleReceiptAccess access) {
        ArgumentNullException.ThrowIfNull(caller);
        ArgumentNullException.ThrowIfNull(access);
        Caller = caller;
        Access = access;
    }

    public ProjectMutationCallerScope Caller { get; }
    public IProjectLifecycleReceiptAccess Access { get; }
}
