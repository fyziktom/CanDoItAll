using System.Collections.Immutable;
using CanDoItAll.Infrastructure.ControlPlane;
using CanDoItAll.Infrastructure.Persistence;

namespace CanDoItAll.Modules.Projects;

internal sealed class ProjectLifecycleAttempt(ProjectLifecycleSubmission submission, ProjectLifecycleInvocation invocation,
    ResolvedDatabaseProfile profile, long generation, DatasetActivationFileObservation activation,
    ProjectLifecycleDigest digest) {
    internal ProjectLifecycleSubmission Submission { get; } = submission;
    internal ProjectLifecycleInvocation Invocation { get; } = invocation;
    internal ResolvedDatabaseProfile Profile { get; } = profile;
    internal long Generation { get; } = generation;
    internal DatasetActivationFileObservation Activation { get; } = activation;
    internal DatasetActivationBinding Binding => Activation.Record.Plan.Binding;
    internal ProjectLifecycleDigest Digest { get; } = digest;
    internal FiniteOperationEpochSnapshot? Epoch { get; set; }
    internal ProjectLifecycleAccepted? Accepted { get; set; }
    internal bool ReplayObserved { get; set; }
    internal bool AdmissionOwnerReleased { get; set; }

    internal ProjectWriteAdmission Target => Submission.Command switch {
        ProjectLifecycleCommand.Delete command => command.Expected.Project,
        ProjectLifecycleCommand.EnsureLink command => command.Child,
        ProjectLifecycleCommand.RemoveLink command => command.Expected.Child,
        ProjectLifecycleCommand.ReconnectLink command => command.Expected.Child,
        _ => throw new NotSupportedException("The original lifecycle command is unsupported.")
    };

    internal ImmutableArray<ProjectWriteAdmission> ExpectedProjects => Submission.Command switch {
        ProjectLifecycleCommand.Delete command => [command.Expected.Project],
        ProjectLifecycleCommand.EnsureLink command => [command.Parent, command.Child],
        ProjectLifecycleCommand.RemoveLink command => [command.Expected.Parent, command.Expected.Child],
        ProjectLifecycleCommand.ReconnectLink command => [command.Expected.Parent, command.Expected.Child, command.NewParent],
        _ => throw new NotSupportedException("The original lifecycle command is unsupported.")
    };

    internal string OperationKey => ProjectMutationScopeKeys.ForLifecycleOperation(
        Submission.Operation, Invocation.Caller.Kind, Invocation.Caller.Key);
}

internal sealed class ProjectLifecycleRefusedException(ProjectLifecycleRefusal reason)
    : InvalidOperationException($"Project lifecycle attempt refused: {reason}.") {
    internal ProjectLifecycleRefusal Reason { get; } = reason;
}

internal sealed class ProjectLifecycleObservationUnavailableException(ProjectLifecycleRecoveryReason reason)
    : InvalidOperationException($"Project lifecycle outcome cannot be established: {reason}.") {
    internal ProjectLifecycleRecoveryReason Reason { get; } = reason;
}
