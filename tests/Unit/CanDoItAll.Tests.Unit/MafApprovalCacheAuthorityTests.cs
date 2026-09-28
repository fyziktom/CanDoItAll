using CanDoItAll.AgentFramework.Maf;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.AgentFramework.Runtime.Abstractions;
using Microsoft.Extensions.AI;

namespace CanDoItAll.Tests.Unit.AgentFramework;

public sealed class MafApprovalCacheAuthorityTests {
    [Theory]
    [InlineData("arguments")]
    [InlineData("name")]
    [InlineData("kind")]
    [InlineData("server")]
    [InlineData("call")]
    public void Same_approval_identity_with_changed_intent_is_rejected(string changedField) {
        var driver = new MafApprovalContinuationDriver();
        var request = CreateRequest();
        var pending = driver.MapPendingApproval(request);
        pending = changedField switch {
            "arguments" => pending with { ArgumentsJson = """{"path":"other.txt"}""" },
            "name" => pending with { ToolName = "delete_file" },
            "kind" => pending with { ToolKind = "function" },
            "server" => pending with { Details = "other-server" },
            "call" => pending with { CallId = "other-call" },
            _ => throw new ArgumentOutOfRangeException(nameof(changedField))
        };
        var session = CreateSession([pending]);
        driver.StorePendingApprovals(session.Id, [request]);

        Assert.Throws<InvalidOperationException>(() => driver.CreateApprovalInputMessages(
            session, [new(request.RequestId, Approved: true)]).ToList());
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Cache_cannot_authorize_after_durable_approvals_are_cleared(bool missingCompatibility) {
        var driver = new MafApprovalContinuationDriver();
        var request = CreateRequest();
        var session = CreateSession([]);
        if (missingCompatibility) {
            session = session with { Compatibility = null };
        }
        driver.StorePendingApprovals(session.Id, [request]);

        Assert.Throws<InvalidOperationException>(() => driver.CreateApprovalInputMessages(
            session, [new(request.RequestId, Approved: true)]).ToList());
    }

    [Fact]
    public void Unchanged_intent_has_identical_responses_with_and_without_cache() {
        var driver = new MafApprovalContinuationDriver();
        var request = CreateRequest();
        var session = CreateSession([driver.MapPendingApproval(request)]);
        driver.StorePendingApprovals(session.Id, [request]);

        var cached = CreateResponse(driver, session);
        driver.ClearPendingApprovals(session.Id);
        var restored = CreateResponse(driver, session);

        Assert.Equal(cached.RequestId, restored.RequestId);
        Assert.Equal(cached.ToolCall.CallId, restored.ToolCall.CallId);
        Assert.Equal(driver.MapPendingApproval(request), driver.MapPendingApproval(
            new ToolApprovalRequestContent(restored.RequestId, restored.ToolCall)));
        Assert.True(cached.Approved);
        Assert.True(restored.Approved);
    }

    private static ToolApprovalResponseContent CreateResponse(MafApprovalContinuationDriver driver, ChatSessionRecord session) {
        var message = Assert.Single(driver.CreateApprovalInputMessages(session, [new("approval", Approved: true)]));
        return Assert.IsType<ToolApprovalResponseContent>(Assert.Single(message.Contents));
    }

    private static ToolApprovalRequestContent CreateRequest() => new("approval",
        new McpServerToolCallContent("call", "write_file", "workspace") {
            Arguments = new Dictionary<string, object?> { ["path"] = "approved.txt" }
        });

    private static ChatSessionRecord CreateSession(IReadOnlyList<PendingToolApprovalRecord> pending) => new(
        Id: Guid.NewGuid(),
        AgentId: Guid.NewGuid(),
        Title: "Approval authority fixture",
        CreatedAtUtc: DateTimeOffset.UtcNow,
        UpdatedAtUtc: DateTimeOffset.UtcNow,
        Messages: [],
        Compatibility: new ChatSessionRuntimeCompatibilityRecord(
            runtimeSessionKey: "fixture-session",
            serializedSessionStateJson: "{}",
            pendingApprovals: pending));
}
