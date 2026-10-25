using CanDoItAll.Infrastructure.ControlPlane;
using CanDoItAll.Infrastructure.Persistence;
using CanDoItAll.Modules.Projects.Pages.Components;

namespace CanDoItAll.Modules.Projects.Pages;

public sealed class ProjectNativeLifecycleOperations(ProjectLifecycleOperationProtocol protocol,
    ProjectLifecycleWriter writer, ProjectLifecycleReceiptReader receipts, ProjectLifecycleHierarchyReader hierarchy,
    IProjectLifecycleRecoveryCodec codec, ICanonicalRuntimeDatabase database, DatasetActivationService activation) {
    public async Task<ProjectLifecyclePendingScope> GetScopeAsync(CancellationToken cancellationToken = default) {
        var profileId = database.Profile.Profile.Id;
        var generation = database.Generation;
        var active = await activation.ObserveActiveBindingAsync(profileId, cancellationToken);
        if (database.Profile.Profile.Id != profileId || database.Generation != generation) {
            throw new InvalidOperationException("The original workspace changed during its dataset observation.");
        }
        var originalNamespace = active.Record.Plan.Binding.Namespace;
        return new(profileId, originalNamespace, ProjectMutationCallerScope.LocalWorkspace.Bind(originalNamespace));
    }

    public Task<ProjectLifecycleWriteContext> GetContextAsync(CancellationToken cancellationToken = default)
        => protocol.GetWriteContextAsync(ProjectMutationCallerScope.LocalWorkspace, cancellationToken);

    public Task<ProjectHierarchyEdgeExpectation> ReadEdgeAsync(ProjectLifecycleWriteContext original,
        ProjectWriteAdmission parent, ProjectWriteAdmission child, CancellationToken cancellationToken = default)
        => hierarchy.ReadEdgeAsync(original, parent, child, cancellationToken);

    public ProjectLifecycleFrozenSubmission Freeze(ProjectLifecycleCommand command, ProjectLifecyclePendingScope originalScope,
        ProjectLifecycleWriteContext context, Guid operationId) {
        ArgumentNullException.ThrowIfNull(command);
        ArgumentNullException.ThrowIfNull(originalScope);
        ArgumentNullException.ThrowIfNull(context);
        if (originalScope != Scope(context) || command.DatabaseProfileId != context.DatabaseProfileId ||
                context.CallerBinding != ProjectMutationCallerScope.LocalWorkspace.Bind(context.Namespace)) {
            throw new InvalidOperationException("The original lifecycle draft cannot be rebound to another profile, dataset or caller.");
        }
        return codec.Freeze(new(new(context.DatabaseProfileId, context.Namespace, context.Epoch, context.CallerBinding, operationId),
            ProjectLifecycleInterpretation.VersionOne, command));
    }

    public Task<ProjectLifecycleOutcome> DispatchAsync(ProjectLifecycleSubmission original, CancellationToken cancellationToken = default) {
        ArgumentNullException.ThrowIfNull(original);
        var access = new NativeAccess(database, activation, original.Operation, database.Generation);
        return writer.SubmitAsync(original, new(ProjectMutationCallerScope.LocalWorkspace, access, access), cancellationToken);
    }

    public Task<ProjectLifecycleLookup> LookupAsync(ProjectLifecycleOperation original, CancellationToken cancellationToken = default) {
        ArgumentNullException.ThrowIfNull(original);
        return receipts.LookupAsync(original, new(ProjectMutationCallerScope.LocalWorkspace,
            new NativeAccess(database, activation, original, database.Generation)), cancellationToken);
    }

    public static ProjectLifecyclePendingScope Scope(ProjectLifecycleWriteContext context)
        => new(context.DatabaseProfileId, context.Namespace, context.CallerBinding);

    private sealed class NativeAccess(ICanonicalRuntimeDatabase database, DatasetActivationService activation,
        ProjectLifecycleOperation original, long generation) : IProjectLifecycleReceiptAccess, IProjectLifecycleCurrentWriteAccess {
        public async Task<bool> CanReadAsync(ProjectLifecycleSubmission submission, CancellationToken cancellationToken)
            => submission.Operation == original && await IsOriginalActiveAsync(cancellationToken);

        public async ValueTask<ProjectLifecycleWriteAccessDecision> ObserveAsync(CancellationToken cancellationToken)
            => await IsOriginalActiveAsync(cancellationToken)
                ? ProjectLifecycleWriteAccessDecision.Allowed : ProjectLifecycleWriteAccessDecision.CallerChanged;

        private async Task<bool> IsOriginalActiveAsync(CancellationToken cancellationToken) {
            if (!Matches()) {
                return false;
            }
            var active = await activation.ObserveActiveBindingAsync(original.DatabaseProfileId, cancellationToken);
            return Matches() && active.Record.Plan.Binding.Namespace == original.Namespace;
        }

        private bool Matches() => database.Profile.Profile.Id == original.DatabaseProfileId && database.Generation == generation &&
            ProjectMutationCallerScope.LocalWorkspace.Bind(original.Namespace) == original.CallerBinding;
    }
}
