using CanDoItAll.Infrastructure.ControlPlane;
using CanDoItAll.Modules.Projects;

namespace CanDoItAll.Tests.Unit;

public sealed class ProjectDefinitionCurrentWriteContractTests {
    [Fact]
    public void Only_HTTP_write_invocations_require_the_explicit_current_observer() {
        var receipt = new ReceiptRead();
        var access = new DecisionAccess(ProjectDefinitionWriteAccessDecision.Allowed);
        foreach (var caller in new[] { ProjectMutationCallerScope.OpenApi, ProjectMutationCallerScope.FromVerifiedSubject("issuer", "subject") }) {
            Assert.Throws<ArgumentException>(() => new ProjectDefinitionInvocation(caller, receipt));
            Assert.Same(access, new ProjectDefinitionInvocation(caller, receipt, currentWrite: access).CurrentWrite);
            Assert.Equal(caller, new ProjectDefinitionReceiptInvocation(caller, receipt).Caller);
        }
        Assert.Null(new ProjectDefinitionInvocation(ProjectMutationCallerScope.LocalWorkspace, receipt).CurrentWrite);
        Assert.Throws<ArgumentException>(() => new ProjectDefinitionInvocation(ProjectMutationCallerScope.LocalWorkspace, receipt, currentWrite: access));
    }

    [Fact]
    public async Task Default_unknown_and_typed_refusals_are_retained_before_commit_with_closed_error_mapping() {
        foreach (var (decision, code) in new[] {
            (ProjectDefinitionWriteAccessDecision.Denied, ProjectErrorCodes.DefinitionWriteDenied),
            ((ProjectDefinitionWriteAccessDecision)999, ProjectErrorCodes.DefinitionWriteDenied),
            (ProjectDefinitionWriteAccessDecision.CallerChanged, ProjectErrorCodes.DefinitionCallerChanged),
            (ProjectDefinitionWriteAccessDecision.AuthorityChanged, ProjectErrorCodes.DefinitionAuthorityChanged),
            (ProjectDefinitionWriteAccessDecision.Unavailable, ProjectErrorCodes.DefinitionAuthorityUnavailable)
        }) {
            var attempt = Attempt(new DecisionAccess(decision));
            var error = await Assert.ThrowsAsync<ProjectDefinitionProtocolRejectedException>(
                () => ProjectDefinitionCurrentWriteGuard.RequireAsync(attempt, CancellationToken.None).AsTask());
            Assert.Equal(code, error.Error.Code);
            Assert.Equal(decision, attempt.CurrentWriteRefusal);
            Assert.False(attempt.CommitAttempted);
            Assert.False(attempt.Committed);
        }
    }

    [Fact]
    public async Task Allowed_observation_does_not_mark_commit_and_late_caller_cancellation_cannot_admit() {
        var attempt = Attempt(new DecisionAccess(ProjectDefinitionWriteAccessDecision.Allowed));
        await ProjectDefinitionCurrentWriteGuard.RequireAsync(attempt, CancellationToken.None);
        Assert.Null(attempt.CurrentWriteRefusal);
        Assert.False(attempt.CommitAttempted);
        using var cancellation = new CancellationTokenSource();
        var canceled = Attempt(new DecisionAccess(ProjectDefinitionWriteAccessDecision.Allowed, cancellation.Cancel));
        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => ProjectDefinitionCurrentWriteGuard.RequireAsync(canceled, cancellation.Token).AsTask());
        Assert.Null(canceled.CurrentWriteRefusal);
        Assert.False(canceled.CommitAttempted);
    }

    [Fact]
    public void Governed_invocation_keeps_its_real_source_and_effect_binding_without_HTTP_substitution() {
        var source = new UnusedSource();
        var authorization = new ProjectMutationAuthorization(source, []);
        var bytes = Enumerable.Repeat((byte)1, 32).ToArray();
        var caller = ProjectMutationCallerScope.FromAdmittedExecution(new(bytes));
        var effect = new ProjectDefinitionEffectBinding(ProjectDefinitionEffectKind.Process, 1, bytes);
        var receipt = new ReceiptRead();
        Assert.Throws<ArgumentException>(() => new ProjectDefinitionInvocation(caller, receipt));
        var invocation = new ProjectDefinitionInvocation(caller, receipt, authorization, effect);
        Assert.Same(authorization, invocation.Authorization);
        Assert.Same(effect, invocation.EffectBinding);
        Assert.Null(invocation.CurrentWrite);
        Assert.Throws<ArgumentException>(() => new ProjectDefinitionInvocation(caller, receipt, authorization, effect,
            new DecisionAccess(ProjectDefinitionWriteAccessDecision.Allowed)));
    }

    private static ProjectDefinitionOperationAttempt Attempt(IProjectDefinitionCurrentWriteAccess access) {
        var binding = new DatasetActivationBinding(new(Guid.NewGuid()), Guid.NewGuid(), 1, DatasetGuardProtocol.SharedRows);
        var caller = ProjectMutationCallerScope.OpenApi;
        var operation = new ProjectDefinitionOperation(binding.Namespace, new(1), Guid.NewGuid(), caller.Bind(binding.Namespace));
        var intent = new ProjectDefinitionNormalizedIntent(ProjectDefinitionRequestVersion.VersionOne, ProjectDefinitionCommandKind.Create,
            null, new("Project", "", "", ProjectStatus.Draft, "", null), [], [], null, new(null, null, null),
            ProjectDefinitionEffectBinding.None, ProjectDefinitionLimits.VersionOneMaximum);
        var digest = ProjectDefinitionOperationCodec.ComputeDigest(intent, operation, caller, ProjectDefinitionLimits.VersionOneMaximum, out var length);
        return new(binding, operation, new(caller, new ReceiptRead(), currentWrite: access), intent, digest, length);
    }

    private sealed class ReceiptRead : IProjectDefinitionReceiptAccess {
        public Task<bool> CanReadAsync(ProjectWriteAdmission target, CancellationToken cancellationToken) => throw new NotSupportedException();
    }

    private sealed class DecisionAccess(ProjectDefinitionWriteAccessDecision decision, Action? beforeReturn = null) : IProjectDefinitionCurrentWriteAccess {
        public ValueTask<ProjectDefinitionWriteAccessDecision> ObserveAsync(CancellationToken cancellationToken) {
            beforeReturn?.Invoke();
            return ValueTask.FromResult(decision);
        }
    }

    private sealed class UnusedSource : IProjectMutationSourceAuthority {
        public Task<IProjectMutationSourceLease> AcquireAsync(ProjectMutationTarget target, CancellationToken cancellationToken) => throw new NotSupportedException();
    }
}
