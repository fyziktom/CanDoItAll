using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.Infrastructure.ControlPlane;
using CanDoItAll.Modules.Projects;
using CanDoItAll.Modules.Workbench;
using CanDoItAll.Processes.Application;
using CanDoItAll.Processes.Runtime;

namespace CanDoItAll.Tests.Unit;

public sealed class ProcessPlanningManagerAuthorityTests {
    public enum ProfileChange { Manager, Database, Fingerprint, Generation }

    [Fact]
    public async Task Current_manager_revocation_cannot_reuse_an_earlier_successful_check() {
        var fixture = new Fixture();
        await fixture.RequireAsync();
        fixture.Access.Decision = ProjectDefinitionWriteAccessDecision.Denied;

        await Assert.ThrowsAsync<AgentToolReceiptAccessDeniedException>(() => fixture.RequireAsync());

        Assert.Equal(2, fixture.Access.Checks);
        Assert.Equal(1, fixture.Lease.Checks);
    }

    [Theory]
    [InlineData(ProfileChange.Manager)]
    [InlineData(ProfileChange.Database)]
    [InlineData(ProfileChange.Fingerprint)]
    [InlineData(ProfileChange.Generation)]
    public async Task Different_original_profile_is_refused_before_current_authority_observation(ProfileChange change) {
        var fixture = new Fixture();
        fixture.Change(change);

        await Assert.ThrowsAsync<AgentToolReceiptAccessDeniedException>(() => fixture.RequireAsync());

        Assert.Equal(0, fixture.Access.Checks);
        Assert.Equal(0, fixture.Lease.Checks);
    }

    [Theory]
    [InlineData(ProfileChange.Database)]
    [InlineData(ProfileChange.Fingerprint)]
    [InlineData(ProfileChange.Generation)]
    public async Task Profile_change_during_source_authorization_is_refused_before_disclosure(ProfileChange change) {
        var fixture = new Fixture();
        fixture.Lease.OnCheck = () => fixture.Change(change);

        await Assert.ThrowsAsync<AgentToolReceiptAccessDeniedException>(() => fixture.RequireAsync());

        Assert.Equal(1, fixture.Access.Checks);
        Assert.Equal(1, fixture.Lease.Checks);
    }

    [Theory]
    [InlineData(ProjectDefinitionWriteAccessDecision.CallerChanged)]
    [InlineData(ProjectDefinitionWriteAccessDecision.AuthorityChanged)]
    public async Task Replaced_manager_authority_is_not_accepted(ProjectDefinitionWriteAccessDecision decision) {
        var fixture = new Fixture();
        fixture.Access.Decision = decision;

        await Assert.ThrowsAsync<AgentToolReceiptAccessDeniedException>(() => fixture.RequireAsync());

        Assert.Equal(0, fixture.Lease.Checks);
    }

    [Fact]
    public async Task Unavailable_manager_observation_remains_an_explicit_unavailable_result() {
        var fixture = new Fixture();
        fixture.Access.Decision = ProjectDefinitionWriteAccessDecision.Unavailable;

        var exception = await Assert.ThrowsAsync<AgentToolReceiptReadUnavailableException>(() => fixture.RequireAsync());

        Assert.IsType<IOException>(exception.InnerException);
        Assert.Equal(0, fixture.Lease.Checks);
    }

    [Fact]
    public async Task Source_authority_denial_is_not_disclosed_as_a_success() {
        var fixture = new Fixture();
        fixture.Lease.OnCheck = () => throw new ProcessLaunchAuthorityRejectedException("Source access was revoked.");

        await Assert.ThrowsAsync<AgentToolReceiptAccessDeniedException>(() => fixture.RequireAsync());
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Cancellation_before_or_during_authority_check_is_observed(bool during) {
        var fixture = new Fixture();
        using var cancellation = new CancellationTokenSource();
        if (during) {
            fixture.Lease.OnCheck = cancellation.Cancel;
        } else {
            cancellation.Cancel();
        }

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => fixture.RequireAsync(cancellation.Token));

        Assert.Equal(during ? 1 : 0, fixture.Lease.Checks);
    }

    private sealed class Fixture {
        internal readonly CurrentAccess Access = new();
        internal readonly CurrentDatabase Database = new();
        internal readonly CurrentGeneration Generation = new();
        internal readonly SourceLease Lease = new();
        internal readonly AgentToolProfileBinding Original;
        internal ProcessPlanningManagerAuthority Manager;

        internal Fixture() {
            Original = new(Database.Profile.Profile.Id, Database.Profile.Profile.Runtime.Fingerprint, Generation.GetGeneration());
            Manager = CreateManager(Original.ProfileId);
        }

        internal Task RequireAsync(CancellationToken token = default)
            => Manager.RequireCurrentAsync(Original, Database, Generation, Lease, token);

        internal void Change(ProfileChange change) {
            switch (change) {
                case ProfileChange.Manager:
                    Manager = CreateManager(Guid.NewGuid());
                    break;
                case ProfileChange.Database:
                    Database.Profile.Profile.Id = Guid.NewGuid();
                    break;
                case ProfileChange.Fingerprint:
                    Database.Profile.Profile.Runtime.Fingerprint = "changed-profile";
                    break;
                case ProfileChange.Generation:
                    Generation.Value++;
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(change));
            }
        }

        private ProcessPlanningManagerAuthority CreateManager(Guid profileId)
            => new(new(new ProcessLaunchPrincipal.LocalOperator(ProcessLaunchOperatorSurface.UserInterface),
                profileId, null, true, true, "manager-policy", new(true, true, true, true)), null, Access);
    }

    private sealed class CurrentAccess : IProjectDefinitionCurrentWriteAccess {
        internal ProjectDefinitionWriteAccessDecision Decision = ProjectDefinitionWriteAccessDecision.Allowed;
        internal int Checks;

        public ValueTask<ProjectDefinitionWriteAccessDecision> ObserveAsync(CancellationToken cancellationToken) {
            cancellationToken.ThrowIfCancellationRequested();
            Checks++;
            return ValueTask.FromResult(Decision);
        }
    }

    private sealed class CurrentDatabase : ICanonicalRuntimeDatabase {
        public ResolvedDatabaseProfile Profile { get; } = new(new() {
            Id = Guid.NewGuid(), Runtime = new() { Fingerprint = "original-profile" }
        }, DatabaseProfileResolutionSource.ExplicitOverride, "in-memory:authority-window");
        public long Generation => 0;
    }

    private sealed class CurrentGeneration : IAgentExecutionProfileGenerationSource {
        internal long Value;
        public DatabaseProfileGeneration GetGeneration() => new(Value);
    }

    private sealed class SourceLease : IProcessLaunchAuthorityLease {
        internal Action? OnCheck;
        internal int Checks;

        public Task RequireForMutationAsync(ProcessLaunchLinkTarget? linkTarget, CancellationToken cancellationToken) {
            cancellationToken.ThrowIfCancellationRequested();
            Assert.Null(linkTarget);
            Checks++;
            OnCheck?.Invoke();
            return Task.CompletedTask;
        }

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }
}
