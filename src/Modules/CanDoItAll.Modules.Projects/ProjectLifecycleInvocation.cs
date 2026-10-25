namespace CanDoItAll.Modules.Projects;

public enum ProjectLifecycleWriteAccessDecision { Denied, Allowed, CallerChanged, AuthorityChanged, Unavailable }

public interface IProjectLifecycleCurrentWriteAccess {
    ValueTask<ProjectLifecycleWriteAccessDecision> ObserveAsync(CancellationToken cancellationToken);
}

public sealed record ProjectLifecycleInvocation {
    public ProjectLifecycleInvocation(ProjectMutationCallerScope caller, IProjectLifecycleReceiptAccess receiptAccess,
        IProjectLifecycleCurrentWriteAccess currentWrite)
        : this(caller, receiptAccess, currentWrite, currentWrite, null, ProjectLifecycleEffectBinding.None) {
    }

    public static ProjectLifecycleInvocation ForGoverned(ProjectMutationCallerScope caller,
        IProjectLifecycleReceiptAccess receiptAccess, ProjectMutationAuthorization authorization,
        ProjectLifecycleEffectBinding effectBinding, IProjectLifecycleCurrentWriteAccess postReleaseWrite)
        => new(caller, receiptAccess, null, postReleaseWrite, authorization, effectBinding);

    private ProjectLifecycleInvocation(ProjectMutationCallerScope caller, IProjectLifecycleReceiptAccess receiptAccess,
        IProjectLifecycleCurrentWriteAccess? currentWrite, IProjectLifecycleCurrentWriteAccess postReleaseWrite,
        ProjectMutationAuthorization? authorization, ProjectLifecycleEffectBinding effect) {
        ArgumentNullException.ThrowIfNull(caller);
        ArgumentNullException.ThrowIfNull(receiptAccess);
        ArgumentNullException.ThrowIfNull(postReleaseWrite);
        ArgumentNullException.ThrowIfNull(effect);
        var governed = caller.Kind == ProjectMutationCallerKind.GovernedExecution;
        if (governed != (authorization is not null) || governed != (effect.Kind != ProjectLifecycleEffectKind.None) ||
                governed != (currentWrite is null)) {
            throw new ArgumentException("Operator lifecycle work requires a current write observer; governed work requires held source authority and a separate post-release observer.");
        }
        Caller = caller;
        ReceiptAccess = receiptAccess;
        CurrentWrite = currentWrite;
        PostReleaseWrite = postReleaseWrite;
        Authorization = authorization;
        EffectBinding = effect;
    }

    public ProjectMutationCallerScope Caller { get; }
    public IProjectLifecycleReceiptAccess ReceiptAccess { get; }
    internal IProjectLifecycleCurrentWriteAccess? CurrentWrite { get; }
    internal IProjectLifecycleCurrentWriteAccess PostReleaseWrite { get; }
    public ProjectMutationAuthorization? Authorization { get; }
    public ProjectLifecycleEffectBinding EffectBinding { get; }
}
