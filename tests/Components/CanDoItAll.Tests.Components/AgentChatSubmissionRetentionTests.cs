using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.Modules.AgentFramework;

namespace CanDoItAll.Tests.Components.AgentFramework;

public sealed class AgentChatSubmissionRetentionTests {
    [Fact]
    public void Retention_freezes_editable_content_and_refuses_capacity_without_evicting_originals() {
        var owner = new AgentChatSubmissionRetention();
        var paths = new List<string> { "uploads/original.png" };
        var agent = Guid.NewGuid();
        var session = Guid.NewGuid();
        Assert.True(owner.TryReserve(agent, session, null, new(7), "draft", "composed request", paths, out var original));
        paths[0] = "uploads/changed.png";
        var profile = Guid.NewGuid();
        var stream = new AgentExecutionActivityStreamId(profile, WorkspaceScopeDescriptor.Organization(profile.ToString("N")), new(7), AgentExecutionOperationId.New());
        owner.BindStream(original!.Id, stream);
        owner.RetainFailure(original.Id, definitelyNotSent: false);
        var uncertain = Assert.Single(owner.Snapshot());
        Assert.Equal("uploads/original.png", Assert.Single(uncertain.AttachmentPaths));
        Assert.Equal(stream, uncertain.Stream);
        Assert.False(owner.TryTakeNotSent(uncertain, out _));
        Assert.False(owner.TryReserve(agent, session, null, new(7), "", new string('a', AgentChatSubmissionRetention.MaximumCharacters), [], out _));
        Assert.False(owner.TryReserve(agent, session, null, new(7), "", "request",
            Enumerable.Repeat("a", AgentChatSubmissionRetention.MaximumAttachmentReferences + 1).ToArray(), out _));
        Assert.Equal(uncertain, Assert.Single(owner.Snapshot()));
        owner.Discard(original);
        Assert.Single(owner.Snapshot());
        owner.Discard(uncertain);
        Assert.Empty(owner.Snapshot());
        Assert.True(owner.TryReserve(agent, session, null, new(7), "draft", "request", [], out var refused));
        owner.RetainFailure(refused!.Id, definitelyNotSent: true);
        var retained = Assert.Single(owner.Snapshot());
        Assert.True(owner.TryTakeNotSent(retained, out var restored));
        Assert.Equal("draft", restored!.Draft);
        Assert.Empty(owner.Snapshot());
    }

    [Fact]
    public void Admission_refusal_requires_direct_prepublication_knowledge_and_preserves_original_identity() {
        var conflict = new SandboxWorkspaceConcurrencyException(2, 3);
        var agent = Guid.NewGuid();
        var session = Guid.NewGuid();
        var refusal = new AgentChatAdmissionConflictException(agent, session, conflict);
        Assert.Equal(agent, refusal.AgentId);
        Assert.Equal(session, refusal.ChatSessionId);
        Assert.Same(conflict, refusal.InnerException);
        Assert.Throws<ArgumentException>(() => new AgentChatAdmissionConflictException(agent, session, new AggregateException(conflict)));
        Assert.Throws<ArgumentException>(() => new AgentChatAdmissionConflictException(agent, session, new IOException("storage")));
    }
}
