using CanDoItAll.Memory.Abstractions;
using CanDoItAll.Memory.Application;

namespace CanDoItAll.Memory.Tests.Security;

public sealed class MemoryOperationAccessAuthorizerTests
{
    [Fact]
    public void Exact_owner_identity_is_allowed()
    {
        var requester = CreateRequester();
        var authorizer = new ExactMemoryOperationAccessAuthorizer();

        var result = authorizer.Authorize(requester, requester);

        Assert.True(result.IsAllowed);
    }

    [Theory]
    [MemberData(nameof(ForeignCallers))]
    public void Any_changed_ownership_dimension_is_denied(MemoryLedgerRequester caller)
    {
        var authorizer = new ExactMemoryOperationAccessAuthorizer();

        var result = authorizer.Authorize(CreateRequester(), caller);

        Assert.False(result.IsAllowed);
    }

    [Fact]
    public void Missing_requester_identity_is_denied_even_when_optional_context_matches()
    {
        var owner = CreateRequester() with
        {
            RequesterId = string.Empty
        };
        var caller = owner;
        var authorizer = new ExactMemoryOperationAccessAuthorizer();

        var result = authorizer.Authorize(owner, caller);

        Assert.False(result.IsAllowed);
    }

    public static IEnumerable<object[]> ForeignCallers()
    {
        var owner = CreateRequester();
        yield return [owner with { RequesterId = "user-foreign" }];
        yield return [owner with { AgentId = "agent-foreign" }];
        yield return [owner with { AgentRole = "reviewer" }];
        yield return [owner with { SessionId = "session-foreign" }];
        yield return [owner with { WorkflowId = "workflow-foreign" }];
        yield return [owner with { WorkflowNodeId = "node-foreign" }];
        yield return [owner with { ProcessId = "process-foreign" }];
        yield return [owner with { ProcessStepId = "step-foreign" }];
        yield return [owner with { SessionId = null }];
    }

    private static MemoryLedgerRequester CreateRequester()
    {
        return new MemoryLedgerRequester(
            RequesterId: "user-42",
            AgentId: "agent-dev",
            AgentRole: "developer",
            SessionId: "session-1",
            WorkflowId: "workflow-1",
            WorkflowNodeId: "node-1",
            ProcessId: "process-1",
            ProcessStepId: "step-1");
    }
}
