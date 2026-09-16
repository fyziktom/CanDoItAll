using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;

namespace CanDoItAll.Tests.Unit.AgentFramework;

public sealed class AgentToolSessionObservationTests {
    [Fact]
    public async Task Observation_requires_a_live_owner_lease() {
        var denied = await Assert.ThrowsAsync<AgentToolAdmissionException>(() => new AgentToolAdmissionVerifier()
            .RequireSessionObservationAsync(new(Guid.NewGuid(), Guid.NewGuid(), AgentExecutionAuthorityId.Create())).AsTask());
        Assert.Equal("tool-admission.no-active-lease", denied.Code);
    }

    [Fact]
    public async Task An_admission_implementation_without_saved_source_observation_fails_explicitly() {
        IAgentToolAdmissionVerifier verifier = new UnsupportedVerifier();
        var denied = await Assert.ThrowsAsync<AgentToolAdmissionException>(() => verifier
            .RequireSessionObservationAsync(new(Guid.NewGuid(), Guid.NewGuid(), AgentExecutionAuthorityId.Create())).AsTask());
        Assert.Equal("tool-admission.session-observation-unavailable", denied.Code);
    }

    private sealed class UnsupportedVerifier : IAgentToolAdmissionVerifier {
        public ValueTask<AgentToolSessionAdmission> RequireSessionAsync(AgentToolSessionReference reference, CancellationToken cancellationToken = default)
            => throw new InvalidOperationException("No substitute session lookup is permitted.");

        public ValueTask<AgentToolAdmittedInvocation> RequireInvocationAsync(AgentToolSessionReference session, string toolName,
            AgentToolSemanticDigest digest, CancellationToken cancellationToken = default)
            => throw new InvalidOperationException("No proposal dispatch is permitted.");
    }
}
