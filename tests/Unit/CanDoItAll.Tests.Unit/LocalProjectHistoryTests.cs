using CanDoItAll.AgentFramework.Core.Execution;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.AgentFramework.ProviderHistory;

namespace CanDoItAll.Tests.Unit;

public sealed class LocalProjectHistoryTests {
    [Fact]
    public void Trusted_project_scope_is_kept_on_child_invocations() {
        var project = Guid.NewGuid();
        var context = AgentHistoryInvocation.Create(Run(), WorkspaceScopeDescriptor.Project(project.ToString("N")));
        Assert.Equal(new HistoryExternalReference(project.ToString("D"), HistoryExternalReference.LocalProjectType), context.ExternalReference);
        var child = context.CreateChild();
        Assert.Equal(context.ExternalReference, child.ExternalReference);
        Assert.NotEqual(context.RequestId, child.RequestId);
        Assert.NotSame(context.Attempts, child.Attempts);
    }

    [Fact]
    public void Organization_or_missing_scope_is_not_a_local_project() {
        Assert.Null(AgentHistoryInvocation.Create(Run(), null).ExternalReference);
        Assert.Null(AgentHistoryInvocation.Create(Run(), WorkspaceScopeDescriptor.Organization(Guid.NewGuid().ToString("D"))).ExternalReference);
    }

    private static ExecutionRunRecord Run() => new(
        Guid.NewGuid(), Guid.NewGuid(), null, "test", "test", "source", "correlation", "", "test", "test",
        "{}", "input", "output", "provider", "model", ExecutionState.Completed, RunOutcome.Succeeded,
        DateTimeOffset.UtcNow, DateTimeOffset.UtcNow, null, null, "", null, []);
}
