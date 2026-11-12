using System.Collections.Immutable;
using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;

namespace CanDoItAll.AgentFramework.Persistence;

internal sealed class FileAgentPlanningPublicationSnapshotLease : IAgentPlanningPublicationSnapshotLease {
    private readonly object releaseSync = new();
    private Task? release;
    private FileSandboxWorkspacePublicationScope? held;
    private readonly AgentPlanningPublicationExpectation original;
    private readonly CatalogDataRevision revision;
    private readonly AgentDefinition agent;
    private readonly ExecutionRunRecord run;
    private readonly ChatSessionRecord? chat;
    private readonly ImmutableArray<AgentDefinition> agents;
    private readonly ImmutableArray<CapabilityCatalogItem> capabilities;

    internal FileAgentPlanningPublicationSnapshotLease(FileSandboxWorkspacePublicationScope held,
        AgentPlanningPublicationExpectation original, CatalogDataRevision revision, ImmutableArray<AgentDefinition> agents,
        ExecutionRunAdmissionSnapshot saved, IEnumerable<CapabilityCatalogItem> capabilities) {
        this.held = held;
        this.original = original;
        this.revision = revision;
        this.agents = agents.Select(FileSandboxWorkspacePreparedValues.Freeze).ToImmutableArray();
        agent = this.agents.Single(item => item.Id == original.Session.AgentId);
        run = FileSandboxWorkspacePreparedValues.Freeze(saved.Run);
        chat = saved.ChatSession is { } session ? FileSandboxWorkspacePreparedValues.Freeze(session) : null;
        this.capabilities = capabilities.Select(item => item with { Tags = item.Tags.ToImmutableArray() }).ToImmutableArray();
    }

    public WorkspaceScopeDescriptor Scope {
        get {
            RequireActive();
            return original.Scope;
        }
    }
    public CatalogDataRevision Revision {
        get {
            RequireActive();
            return revision;
        }
    }
    public AgentDefinition Agent {
        get {
            RequireActive();
            return agent;
        }
    }
    public ImmutableArray<AgentDefinition> Agents {
        get {
            RequireActive();
            return agents;
        }
    }
    public ImmutableArray<CapabilityCatalogItem> Capabilities {
        get {
            RequireActive();
            return capabilities;
        }
    }
    public ExecutionRunRecord OriginalRun {
        get {
            RequireActive();
            return run;
        }
    }
    public ChatSessionRecord? OriginalChatSession {
        get {
            RequireActive();
            return chat;
        }
    }
    public AgentPlanningPublicationExpectation Original {
        get {
            RequireActive();
            return original;
        }
    }

    public void RequireActive() => ObjectDisposedException.ThrowIf(Volatile.Read(ref held) is null, this);

    public ValueTask DisposeAsync() {
        lock (releaseSync) {
            return new(release ??= ReleaseAsync());
        }
    }

    private async Task ReleaseAsync() {
        var acquired = Interlocked.Exchange(ref held, null);
        if (acquired is not null) {
            await acquired.DisposeAsync().ConfigureAwait(false);
        }
    }

    internal static void RequireOriginal(AgentPlanningPublicationExpectation expected, WorkspaceScopeDescriptor scope,
        ExecutionRunAdmissionSnapshot? saved) {
        var run = saved?.Run;
        var chat = saved?.ChatSession;
        var session = expected.Session;
        var journal = run?.ToolAdmission;
        if (scope != expected.Scope || run is null || journal is null ||
                run.Id != session.Reference.ExecutionRunId || run.AgentId != session.AgentId ||
                run.Revision != expected.RunRevision || journal.Revision != expected.JournalRevision || journal.Session != session) {
            throw new SandboxWorkspacePublicationConflictException(SandboxWorkspacePublicationConflictKind.OriginalBytesChanged);
        }
        journal.Validate();
        if (expected.Origin == AgentPlanningPublicationOrigin.Inherited) {
            if (chat is not null || run.ChatSessionId is not null ||
                journal.SchemaVersion != AgentToolJournalRecord.InheritedPlanningSchemaVersion || journal.PlanningDecision?.InheritedOrigin is null) {
                throw new SandboxWorkspacePublicationConflictException(SandboxWorkspacePublicationConflictKind.OriginalBytesChanged);
            }
            _ = AgentPlanningRunMetadata.Read(run);
            return;
        }
        var authority = AgentTurnContextMetadata.TryReadExecutionGovernanceSnapshot(run.MetadataJson);
        if (chat is null || run.ChatSessionId != session.Reference.ChatSessionId || chat.Id != session.Reference.ChatSessionId ||
                chat.AgentId != session.AgentId || chat.LatestExecutionRunId != run.Id ||
                authority is null || authority.AuthorityId != session.Reference.AuthorityId || authority.AgentId != session.AgentId ||
                authority.DatabaseProfileId != session.Profile.ProfileId || authority.DatabaseProfileGeneration != session.Profile.Generation) {
            throw new SandboxWorkspacePublicationConflictException(SandboxWorkspacePublicationConflictKind.OriginalBytesChanged);
        }
    }
}
