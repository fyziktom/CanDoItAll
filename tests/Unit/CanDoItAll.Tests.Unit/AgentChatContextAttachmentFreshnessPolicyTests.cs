using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;

namespace CanDoItAll.Tests.Unit;

public sealed class AgentChatContextAttachmentFreshnessPolicyTests
{
    private static readonly DateTimeOffset CapturedAtUtc =
        new(2026, 7, 27, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Invocation_accepts_attachment_before_its_deadline_for_the_same_profile()
    {
        var context = CreateContext();

        var invocation = AgentChatContextInvocationFactory.Create(
            context,
            Guid.NewGuid(),
            chatSessionId: null,
            "Use the current selection.",
            AgentExecutionOperationId.New(),
            new DatabaseProfileGeneration(7),
            CapturedAtUtc.AddMinutes(4));

        Assert.Single(invocation.Options.TransientContext!.Attachments);
    }

    [Fact]
    public void Invocation_rejects_the_context_at_its_attachment_deadline()
    {
        var context = CreateContext();

        var exception = Assert.Throws<
            AgentChatContextAttachmentUnavailableException>(() =>
                AgentChatContextInvocationFactory.Create(
                    context,
                    Guid.NewGuid(),
                    chatSessionId: null,
                    "Use the current selection.",
                    AgentExecutionOperationId.New(),
                    new DatabaseProfileGeneration(7),
                    CapturedAtUtc.AddMinutes(5)));

        Assert.Equal(
            AgentChatContextAttachmentFreshness.Expired,
            exception.Freshness);
        Assert.Equal(context.Scope.Id, exception.ScopeId);
        Assert.Equal(
            new AgentChatContextContributorId("tests.selection"),
            exception.ContributorId);
    }

    [Fact]
    public void Invocation_rejects_an_attachment_from_another_profile_generation()
    {
        var context = CreateContext();

        var exception = Assert.Throws<
            AgentChatContextAttachmentUnavailableException>(() =>
                AgentChatContextInvocationFactory.Create(
                    context,
                    Guid.NewGuid(),
                    chatSessionId: null,
                    "Use the current selection.",
                    AgentExecutionOperationId.New(),
                    new DatabaseProfileGeneration(8),
                    CapturedAtUtc.AddMinutes(1)));

        Assert.Equal(
            AgentChatContextAttachmentFreshness.ProfileMismatch,
            exception.Freshness);
    }

    private static AgentChatContextSnapshot CreateContext()
    {
        var scopeId = AgentChatContextScopeId.Create();
        var scope = new AgentChatContextScope(
            scopeId,
            new AgentChatContextSource(
                new AgentChatContextSourceKind("tests"),
                new AgentChatContextSourceId("selection")),
            "Test selection",
            accessMode: AgentChatContextScopeAccessMode.Unrestricted);
        var contributorId =
            new AgentChatContextContributorId("tests.selection");
        var attachmentDraft = new AgentChatContextAttachmentDraft(
            new AgentChatContextAttachmentKind("tests.snapshot"),
            new SnapshotContentFingerprint("content-1"),
            new SnapshotCoverageFingerprint("coverage-1"),
            new DatabaseProfileGeneration(7),
            new SnapshotFreshnessFingerprint("freshness-1"),
            CapturedAtUtc,
            CapturedAtUtc.AddMinutes(5),
            new TestAttachment());
        var publication = new AgentChatContextPublication(
            scope,
            [
                new AgentChatContextContributorPublication(
                    new AgentChatContextFragment(
                        contributorId,
                        0,
                        "Selected item: 42"),
                    [attachmentDraft])
            ]);
        var registry = new AgentChatContextRegistry(
            new FixedTimeProvider(CapturedAtUtc));
        using var lease = registry.PublishModuleContext(publication);
        return Assert.IsType<AgentChatContextSnapshot>(registry.Capture());
    }

    private sealed record TestAttachment : IAgentChatContextAttachment;

    private sealed class FixedTimeProvider(DateTimeOffset utcNow) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow()
            => utcNow;
    }
}
