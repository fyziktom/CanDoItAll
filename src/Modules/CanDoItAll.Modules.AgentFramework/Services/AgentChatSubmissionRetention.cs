using System.Collections.Immutable;
using CanDoItAll.AgentFramework.Models;

namespace CanDoItAll.Modules.AgentFramework;

public enum AgentChatSubmissionState {
    Pending,
    NotSent,
    Unconfirmed
}

public sealed record AgentChatSubmission(
    Guid Id,
    Guid AgentId,
    Guid? SessionId,
    AgentChatHandleId? HandleId,
    DatabaseProfileGeneration ProfileGeneration,
    string Draft,
    string RequestPrompt,
    ImmutableArray<string> AttachmentPaths,
    AgentChatSubmissionState State,
    AgentExecutionActivityStreamId? Stream = null);

public sealed class AgentChatSubmissionRetention {
    public const int MaximumSubmissions = 16;
    public const int MaximumCharacters = 1_048_576;
    public const int MaximumAttachmentReferences = 256;
    private readonly object gate = new();
    private readonly Dictionary<Guid, AgentChatSubmission> submissions = [];
    private int characters;

    public event Action? Changed;

    public ImmutableArray<AgentChatSubmission> Snapshot() {
        lock (gate) {
            return submissions.Values.ToImmutableArray();
        }
    }

    public bool TryReserve(Guid agentId, Guid? sessionId, AgentChatHandleId? handleId,
        DatabaseProfileGeneration profileGeneration, string draft, string requestPrompt,
        IReadOnlyList<string> attachmentPaths, out AgentChatSubmission? submission) {
        ArgumentNullException.ThrowIfNull(draft);
        ArgumentException.ThrowIfNullOrWhiteSpace(requestPrompt);
        ArgumentNullException.ThrowIfNull(attachmentPaths);
        if (agentId == Guid.Empty || sessionId == Guid.Empty) {
            throw new ArgumentException("The submitted chat target must be valid.");
        }
        if (attachmentPaths.Count > MaximumAttachmentReferences) {
            submission = null;
            return false;
        }
        var size = (long)draft.Length + requestPrompt.Length;
        foreach (var path in attachmentPaths) {
            ArgumentException.ThrowIfNullOrWhiteSpace(path);
            size += path.Length;
        }
        lock (gate) {
            if (submissions.Count >= MaximumSubmissions || size > MaximumCharacters - characters) {
                submission = null;
                return false;
            }
            submission = new(Guid.NewGuid(), agentId, sessionId, handleId, profileGeneration, draft,
                requestPrompt, attachmentPaths.ToImmutableArray(), AgentChatSubmissionState.Pending);
            submissions.Add(submission.Id, submission);
            characters += (int)size;
        }
        Changed?.Invoke();
        return true;
    }

    public void BindStream(Guid id, AgentExecutionActivityStreamId stream) {
        ArgumentNullException.ThrowIfNull(stream);
        lock (gate) {
            var current = submissions[id];
            if (current.State != AgentChatSubmissionState.Pending || current.Stream is not null) {
                throw new InvalidOperationException("The submitted chat already has an operation.");
            }
            submissions[id] = current with { Stream = stream };
        }
        Changed?.Invoke();
    }

    public void RetainFailure(Guid id, bool definitelyNotSent) {
        lock (gate) {
            var current = submissions[id];
            submissions[id] = current with {
                State = definitelyNotSent ? AgentChatSubmissionState.NotSent : AgentChatSubmissionState.Unconfirmed
            };
        }
        Changed?.Invoke();
    }

    public bool TryTakeNotSent(AgentChatSubmission expected, out AgentChatSubmission? retained) {
        lock (gate) {
            if (!submissions.TryGetValue(expected.Id, out var current) || current != expected ||
                    current.State != AgentChatSubmissionState.NotSent) {
                retained = null;
                return false;
            }
            retained = current;
            RemoveCore(current);
        }
        Changed?.Invoke();
        return true;
    }

    public void Complete(Guid id) {
        lock (gate) {
            if (submissions.TryGetValue(id, out var current)) {
                RemoveCore(current);
            }
        }
        Changed?.Invoke();
    }

    public void Discard(AgentChatSubmission expected) {
        lock (gate) {
            if (submissions.TryGetValue(expected.Id, out var current) && current == expected &&
                    current.State != AgentChatSubmissionState.Pending) {
                RemoveCore(current);
            }
        }
        Changed?.Invoke();
    }

    private void RemoveCore(AgentChatSubmission current) {
        submissions.Remove(current.Id);
        characters -= current.Draft.Length + current.RequestPrompt.Length + current.AttachmentPaths.Sum(path => path.Length);
    }
}
