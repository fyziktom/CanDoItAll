using System.Collections.Immutable;
using System.Text.Json;
using CanDoItAll.AgentFramework.Models;

namespace CanDoItAll.AgentFramework.Persistence;

internal sealed class FileSandboxWorkspaceWriteSet {
    private static readonly JsonSerializerOptions SerializerOptions = new FileSandboxWorkspaceJsonStore().SerializerOptions;
    private readonly ImmutableArray<PublicationKey> keys;

    private FileSandboxWorkspaceWriteSet(IEnumerable<PublicationKey> keys) {
        this.keys = keys.Distinct().OrderBy(key => key.Kind)
            .ThenBy(key => key.Id.ToString("N"), StringComparer.Ordinal).ToImmutableArray();
        if (this.keys.Any(key => key.Id == Guid.Empty)) {
            throw new InvalidDataException("A workspace publication key requires an owner identity.");
        }
    }

    internal static FileSandboxWorkspaceWriteSet Catalog(SandboxWorkspaceCatalog before, SandboxWorkspaceCatalog after) {
        var prior = before.Agents.GroupBy(agent => agent.Id).ToDictionary(group => group.Key, group => group.ToImmutableArray());
        var target = after.Agents.GroupBy(agent => agent.Id).ToDictionary(group => group.Key, group => group.ToImmutableArray());
        var sharedChanged = !Same(before with { Agents = [], CatalogDataRevision = CatalogDataRevision.Unassigned, AgentDecisionDataPolicyReceipts = null },
            after with { Agents = [], CatalogDataRevision = CatalogDataRevision.Unassigned, AgentDecisionDataPolicyReceipts = null });
        return new(prior.Keys.Union(target.Keys).Where(id => sharedChanged || !prior.TryGetValue(id, out var left) ||
            !target.TryGetValue(id, out var right) || !Same(left, right)).Select(Agent));
    }

    internal static FileSandboxWorkspaceWriteSet Run(ExecutionRunRecord? before, ExecutionRunRecord after,
        bool readsAgent) {
        var keys = new List<PublicationKey> { Execution(after.Id) };
        Add(before);
        Add(after);
        return new(keys);

        void Add(ExecutionRunRecord? run) {
            if (run is null) {
                return;
            }
            keys.Add(Execution(run.Id));
            if (readsAgent) {
                keys.Add(Agent(run.AgentId));
            }
            if (run.ChatSessionId is { } sessionId) {
                keys.Add(Chat(sessionId));
            }
        }
    }

    internal static FileSandboxWorkspaceWriteSet Session(ChatSessionRecord? before, ChatSessionRecord after) {
        var keys = new List<PublicationKey> { Chat(after.Id), Agent(after.AgentId) };
        if (before is not null) {
            keys.Add(Chat(before.Id));
            keys.Add(Agent(before.AgentId));
        }
        return new(keys);
    }

    internal static FileSandboxWorkspaceWriteSet Planning(ExecutionRunRecord run, Guid? additionalAgentId) {
        var selected = Run(run, run, readsAgent: true);
        return additionalAgentId is { } id ? new(selected.keys.Append(Agent(id))) : selected;
    }

    internal static FileSandboxWorkspaceWriteSet Agents(IEnumerable<Guid> agentIds) => new(agentIds.Select(Agent));

    internal static FileSandboxWorkspaceWriteSet Workspace(SandboxWorkspaceDocument before, SandboxWorkspaceDocument after) {
        var keys = Catalog(before.ToCatalog(), after.ToCatalog()).keys.ToList();
        foreach (var run in before.ExecutionRuns.Concat(after.ExecutionRuns)) {
            keys.Add(Execution(run.Id));
            keys.Add(Agent(run.AgentId));
            if (run.ChatSessionId is { } chatId) {
                keys.Add(Chat(chatId));
            }
        }
        foreach (var chat in before.ChatSessions.Concat(after.ChatSessions)) {
            keys.Add(Chat(chat.Id));
            keys.Add(Agent(chat.AgentId));
        }
        return new(keys);
    }

    internal static FileSandboxWorkspaceWriteSet Deletion(AgentDeletionCommitJournal journal) =>
        new(Catalog(journal.SourceCatalog, journal.TargetCatalog).keys
            .Append(Agent(journal.AgentId))
            .Concat(journal.ExecutionPlan.RunIds.Select(Execution))
            .Concat(journal.ExecutionPlan.SessionIds.Select(Chat)));

    internal bool Contains(FileSandboxWorkspaceWriteSet other) => other.keys.All(keys.Contains);

    internal ImmutableArray<string> LockPaths(FileSandboxWorkspaceStorageLayout layout) => keys.Select(key =>
        Path.Combine(layout.PublicationCoordinationRoot, key.Kind switch {
            PublicationKeyKind.Agent => "agent",
            PublicationKeyKind.Run => "run",
            PublicationKeyKind.Chat => "chat",
            _ => throw new InvalidOperationException("The workspace publication key kind is unsupported.")
        }, $"{key.Id:N}.lock")).ToImmutableArray();

    private static bool Same<T>(T left, T right) => JsonSerializer.SerializeToUtf8Bytes(left,
        SerializerOptions).AsSpan().SequenceEqual(JsonSerializer.SerializeToUtf8Bytes(right, SerializerOptions));

    private static PublicationKey Agent(Guid id) => new(PublicationKeyKind.Agent, id);

    private static PublicationKey Execution(Guid id) => new(PublicationKeyKind.Run, id);

    private static PublicationKey Chat(Guid id) => new(PublicationKeyKind.Chat, id);

    private readonly record struct PublicationKey(PublicationKeyKind Kind, Guid Id);

    private enum PublicationKeyKind {
        Agent,
        Run,
        Chat
    }
}
