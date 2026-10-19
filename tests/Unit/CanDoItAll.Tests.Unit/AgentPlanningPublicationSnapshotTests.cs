using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;

namespace CanDoItAll.Tests.Unit;

public sealed class AgentPlanningPublicationSnapshotTests {
    [Fact]
    public void Expectation_retains_the_exact_original_scope_session_profile_and_revisions() {
        var session = Session();
        var scope = WorkspaceScopeDescriptor.Organization(session.Profile.ProfileId.ToString("N"));
        var originalScope = scope;
        var originalSession = session;
        var expected = new AgentPlanningPublicationExpectation(scope, session, 23, 41);

        scope = scope with { Key = Guid.NewGuid().ToString("N") };
        session = session with {
            AgentId = Guid.NewGuid(),
            Reference = new(Guid.NewGuid(), Guid.NewGuid(), new(Guid.NewGuid())),
            Profile = new(Guid.NewGuid(), "Changed profile", new(9))
        };

        Assert.Equal(originalScope, expected.Scope);
        Assert.NotSame(originalScope, expected.Scope);
        Assert.Equal(originalSession, expected.Session);
        Assert.NotSame(originalSession, expected.Session);
        Assert.Equal(originalSession.Reference, expected.Session.Reference);
        Assert.Equal(originalSession.Profile, expected.Session.Profile);
        Assert.Equal(23, expected.RunRevision);
        Assert.Equal(41, expected.JournalRevision);
        Assert.NotEqual(scope, expected.Scope);
        Assert.NotEqual(session, expected.Session);
        Assert.Equal(AgentRuntimeContextPurpose.InteractiveChat, expected.Session.Purpose);
        Assert.Null(expected.Session.Reference.BackgroundSource);
    }

    [Fact]
    public void Expectation_refuses_missing_identity_foreign_scope_background_and_nonpositive_revisions() {
        var session = Session();
        var scope = WorkspaceScopeDescriptor.Organization(session.Profile.ProfileId.ToString("N"));
        Assert.Throws<ArgumentNullException>(() => new AgentPlanningPublicationExpectation(null!, session, 1, 1));
        Assert.Throws<ArgumentNullException>(() => new AgentPlanningPublicationExpectation(scope, null!, 1, 1));
        Assert.Throws<ArgumentNullException>(() => new AgentPlanningPublicationExpectation(scope, session with { Reference = null! }, 1, 1));
        Assert.Throws<ArgumentNullException>(() => new AgentPlanningPublicationExpectation(scope, session with { Profile = null! }, 1, 1));
        Assert.Throws<ArgumentException>(() => new AgentPlanningPublicationExpectation(scope, session with { AgentId = Guid.Empty }, 1, 1));
        Assert.Throws<ArgumentException>(() => new AgentPlanningPublicationExpectation(scope,
            session with { Purpose = AgentRuntimeContextPurpose.GovernedProcessAutomation }, 1, 1));
        Assert.Throws<ArgumentException>(() => new AgentPlanningPublicationExpectation(scope,
            session with { Purpose = (AgentRuntimeContextPurpose)int.MaxValue }, 1, 1));
        var background = new AgentToolBackgroundSourceBinding("process-step", "original-step",
            new(new string('a', 64)), new(new string('b', 64)));
        Assert.Throws<ArgumentException>(() => new AgentPlanningPublicationExpectation(scope,
            session with { Reference = new(Guid.NewGuid(), Guid.Empty, default, background) }, 1, 1));
        Assert.Throws<ArgumentException>(() => new AgentPlanningPublicationExpectation(WorkspaceScopeDescriptor.Sandbox, session, 1, 1));
        Assert.Throws<ArgumentException>(() => new AgentPlanningPublicationExpectation(
            WorkspaceScopeDescriptor.Organization(Guid.NewGuid().ToString("N")), session, 1, 1));
        Assert.Throws<ArgumentException>(() => new AgentPlanningPublicationExpectation(
            WorkspaceScopeDescriptor.Project(session.Profile.ProfileId.ToString("N")), session, 1, 1));
        Assert.Throws<ArgumentException>(() => new AgentPlanningPublicationExpectation(
            WorkspaceScopeDescriptor.Organization(session.Profile.ProfileId.ToString("D")), session, 1, 1));
        Assert.Throws<ArgumentException>(() => new AgentPlanningPublicationExpectation(scope, session, 0, 1));
        Assert.Throws<ArgumentException>(() => new AgentPlanningPublicationExpectation(scope, session, -1, 1));
        Assert.Throws<ArgumentException>(() => new AgentPlanningPublicationExpectation(scope, session, 1, 0));
        Assert.Throws<ArgumentException>(() => new AgentPlanningPublicationExpectation(scope, session, 1, -1));
    }

    [Fact]
    public async Task An_unsupported_store_refuses_selected_authority_without_calling_its_coarse_lease() {
        var legacy = new LegacyStore();
        IAgentCatalogReadLeaseStore store = legacy;
        var session = Session();
        var expected = new AgentPlanningPublicationExpectation(
            WorkspaceScopeDescriptor.Organization(session.Profile.ProfileId.ToString("N")), session, 1, 1);
        await Assert.ThrowsAsync<NotSupportedException>(() => store.AcquirePlanningAuthoritySnapshotAsync(expected));
        Assert.Equal(0, legacy.CoarseCalls);
    }

    private static AgentToolSessionAdmission Session() => new(
        new(Guid.NewGuid(), Guid.NewGuid(), new(Guid.NewGuid())), Guid.NewGuid(), AgentRuntimeContextPurpose.InteractiveChat,
        new(Guid.NewGuid(), "Original profile fingerprint", new(7)));

    private sealed class LegacyStore : IAgentCatalogReadLeaseStore {
        public int CoarseCalls { get; private set; }
        public Task<IAgentCatalogReadLease> AcquireAgentReadLeaseAsync(Guid agentId, CancellationToken cancellationToken = default) {
            CoarseCalls++;
            throw new InvalidOperationException("Unsupported selected authority must not enter the coarse lease.");
        }
    }
}
