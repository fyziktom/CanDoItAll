using CanDoItAll.Modules.Projects;
using Microsoft.Extensions.DependencyInjection;

namespace CanDoItAll.Tests.Integration.ProjectStructure;

public sealed partial class ProjectLifecycleOperationTests {
    [Fact]
    public Task Governed_lifecycle_checks_held_source_at_commit_and_fresh_authority_only_after_release() {
        var faults = new OwnerFaults();
        return WithFixtureAsync(async fixture => {
            var parent = await fixture.SeedAsync("Governed parent");
            var child = await fixture.SeedAsync("Governed child");
            var probe = new GovernedSourceProbe(faults);
            var caller = ProjectMutationCallerScope.FromAdmittedExecution(new(Enumerable.Repeat((byte)1, 32).ToArray()));
            var invocation = ProjectLifecycleInvocation.ForGoverned(caller, new ReceiptAccess(),
                new(probe, [parent.Project, child.Project]), new(ProjectLifecycleEffectKind.Agent, 1, Enumerable.Repeat((byte)2, 32).ToArray()), probe);
            var context = await fixture.Services.GetRequiredService<ProjectLifecycleOperationProtocol>().GetWriteContextAsync(caller);
            var original = new ProjectLifecycleSubmission(new(context.DatabaseProfileId, context.Namespace, context.Epoch,
                context.CallerBinding, Guid.NewGuid()), ProjectLifecycleInterpretation.VersionOne,
                new ProjectLifecycleCommand.EnsureLink(parent.Project, child.Project));
            faults.Armed = true;

            var accepted = Assert.IsType<ProjectLifecycleOutcome.Accepted>(await fixture.Writer.SubmitAsync(original, invocation));
            Assert.Equal(ProjectLifecycleCompletionIssue.PostCommitProcessing, accepted.CompletionIssue);
            Assert.True(probe.FinalCheckAfterReceipt);
            Assert.Equal(1, probe.Acquisitions);
            Assert.Equal(1, probe.Releases);
            Assert.Equal(1, probe.PostReleaseObservations);
            Assert.False(probe.Held);
            Assert.Single((await fixture.StateAsync()).Edges);
            var replay = Assert.IsType<ProjectLifecycleOutcome.Accepted>(await fixture.Writer.SubmitAsync(original, invocation));
            Assert.True(replay.Replayed);
            Assert.Equal(accepted.Result, replay.Result);
            Assert.Equal(1, probe.Acquisitions);
            Assert.Equal(1, probe.PostReleaseObservations);
        }, faults);
    }

    [Fact]
    public Task Governed_source_refusal_after_receipt_staging_rolls_back_hierarchy_and_receipt_without_post_release_work() {
        var faults = new OwnerFaults();
        return WithFixtureAsync(async fixture => {
            var parent = await fixture.SeedAsync("Guarded parent");
            var child = await fixture.SeedAsync("Guarded child");
            var before = await fixture.StateAsync();
            var probe = new GovernedSourceProbe(faults) { RefuseAfterReceipt = true };
            var caller = ProjectMutationCallerScope.FromAdmittedExecution(new(Enumerable.Repeat((byte)3, 32).ToArray()));
            var invocation = ProjectLifecycleInvocation.ForGoverned(caller, new ReceiptAccess(),
                new(probe, [parent.Project, child.Project]), new(ProjectLifecycleEffectKind.Process, 1, Enumerable.Repeat((byte)4, 32).ToArray()), probe);
            var context = await fixture.Services.GetRequiredService<ProjectLifecycleOperationProtocol>().GetWriteContextAsync(caller);
            var original = new ProjectLifecycleSubmission(new(context.DatabaseProfileId, context.Namespace, context.Epoch,
                context.CallerBinding, Guid.NewGuid()), ProjectLifecycleInterpretation.VersionOne,
                new ProjectLifecycleCommand.EnsureLink(parent.Project, child.Project));
            faults.Armed = true;

            var refused = Assert.IsType<ProjectLifecycleOutcome.Refused>(await fixture.Writer.SubmitAsync(original, invocation));
            Assert.Equal(ProjectLifecycleRefusal.AuthorityChanged, refused.Reason);
            Assert.True(probe.FinalCheckAfterReceipt);
            Assert.Equal(1, probe.Releases);
            Assert.Equal(0, probe.PostReleaseObservations);
            before.RequireEqual(await fixture.StateAsync());
        }, faults);
    }

    private sealed class GovernedSourceProbe(OwnerFaults faults) : IProjectMutationSourceAuthority, IProjectLifecycleCurrentWriteAccess {
        internal OwnerFaults Faults { get; } = faults;
        internal bool Held;
        internal bool RefuseAfterReceipt;
        internal bool FinalCheckAfterReceipt;
        internal int Acquisitions;
        internal int Releases;
        internal int PostReleaseObservations;

        public Task<IProjectMutationSourceLease> AcquireAsync(ProjectMutationTarget target, CancellationToken cancellationToken = default) {
            Assert.False(Held);
            Assert.Equal(ProjectMutationPurpose.HierarchyWrite, target.Purpose);
            Held = true;
            Acquisitions++;
            return Task.FromResult<IProjectMutationSourceLease>(new HeldSource(this));
        }

        public ValueTask<ProjectLifecycleWriteAccessDecision> ObserveAsync(CancellationToken cancellationToken) {
            Assert.False(Held);
            Assert.True(faults.OwnerDisposed);
            PostReleaseObservations++;
            return ValueTask.FromResult(ProjectLifecycleWriteAccessDecision.Denied);
        }

        private sealed class HeldSource(GovernedSourceProbe owner) : IProjectMutationSourceLease {
            public IReadOnlyCollection<ProjectWriteAdmission> SourceProjects => [];
            public Task RequireForMutationAsync(CancellationToken cancellationToken = default) {
                Assert.True(owner.Held);
                if (owner.Faults.ReceiptReached.Task.IsCompleted) {
                    owner.FinalCheckAfterReceipt = true;
                    if (owner.RefuseAfterReceipt) {
                        throw new ProjectLifecycleRefusedException(ProjectLifecycleRefusal.AuthorityChanged);
                    }
                }
                return Task.CompletedTask;
            }
            public ValueTask DisposeAsync() {
                Assert.True(owner.Held);
                owner.Held = false;
                owner.Releases++;
                return ValueTask.CompletedTask;
            }
        }
    }
}
