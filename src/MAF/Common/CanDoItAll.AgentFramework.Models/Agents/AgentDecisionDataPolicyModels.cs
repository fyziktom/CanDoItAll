using System.Collections.Immutable;
using CanDoItAll.SharedKernel;

namespace CanDoItAll.AgentFramework.Models;

public sealed record AgentDecisionDataPolicyExpectation {
    public AgentDecisionDataPolicyExpectation(AgentExecutionActivityWorkspaceIdentity workspace,
        Guid agentId, CatalogDataRevision catalogRevision) {
        ArgumentNullException.ThrowIfNull(workspace);
        if (agentId == Guid.Empty || !catalogRevision.IsAssigned) {
            throw new ArgumentException("An original Agent identity and assigned catalog revision are required.");
        }
        Workspace = workspace;
        AgentId = agentId;
        CatalogRevision = catalogRevision;
    }

    public AgentExecutionActivityWorkspaceIdentity Workspace { get; }
    public Guid AgentId { get; }
    public CatalogDataRevision CatalogRevision { get; }
}

public sealed record AgentDecisionDataPolicySnapshot {
    public AgentDecisionDataPolicySnapshot(AgentDecisionDataPolicyExpectation expectation, DecisionDataPolicyOverride? policy) {
        ArgumentNullException.ThrowIfNull(expectation);
        if (policy is not null) {
            _ = DecisionDataPolicyOverrideCodec.Serialize(policy);
        }
        Expectation = expectation;
        Policy = policy;
    }
    public AgentDecisionDataPolicyExpectation Expectation { get; }
    public DecisionDataPolicyOverride? Policy { get; }
}

public sealed record ReplaceAgentDecisionDataPolicyCommand {
    public ReplaceAgentDecisionDataPolicyCommand(Guid operationId, AgentDecisionDataPolicyExpectation expectation,
        DecisionDataPolicyOverride policy) {
        if (operationId == Guid.Empty) {
            throw new ArgumentException("An original operation identity is required.", nameof(operationId));
        }
        ArgumentNullException.ThrowIfNull(expectation);
        ArgumentNullException.ThrowIfNull(policy);
        _ = DecisionDataPolicyOverrideCodec.Serialize(policy);
        OperationId = operationId;
        Expectation = expectation;
        Policy = policy;
    }

    public Guid OperationId { get; }
    public AgentDecisionDataPolicyExpectation Expectation { get; }
    public DecisionDataPolicyOverride Policy { get; }
}

public enum AgentDecisionDataPolicyRejection {
    ContextChanged,
    Conflict,
    NotFound,
    InvalidMetadata,
    Unsupported,
    Cancelled,
    Unavailable
}

public enum AgentDecisionDataPolicyFailureStage {
    Publication,
    OwnerDispatch,
    CacheInvalidation,
    Diagnostics
}

public sealed record AgentDecisionDataPolicyFailure {
    private const int MaximumTypes = 16;

    private AgentDecisionDataPolicyFailure(AgentDecisionDataPolicyFailureStage stage, ImmutableArray<string> exceptionTypes, bool truncated) {
        Stage = stage;
        ExceptionTypes = exceptionTypes;
        Truncated = truncated;
    }

    public AgentDecisionDataPolicyFailureStage Stage { get; }
    public ImmutableArray<string> ExceptionTypes { get; }
    public bool Truncated { get; }

    public static AgentDecisionDataPolicyFailure Capture(AgentDecisionDataPolicyFailureStage stage, Exception failure,
        AgentDecisionDataPolicyFailure? previous = null) {
        ArgumentNullException.ThrowIfNull(failure);
        if (!Enum.IsDefined(stage)) {
            throw new ArgumentOutOfRangeException(nameof(stage));
        }
        var types = ImmutableArray.CreateBuilder<string>();
        var truncated = previous?.Truncated ?? false;
        Add(failure);
        if (previous is not null) {
            foreach (var type in previous.ExceptionTypes) {
                if (types.Count == MaximumTypes) {
                    truncated = true;
                    break;
                }
                types.Add(type);
            }
        }
        return new(stage, types.ToImmutable(), truncated);

        void Add(Exception current) {
            if (types.Count == MaximumTypes) {
                truncated = true;
                return;
            }
            var type = current.GetType().FullName ?? current.GetType().Name;
            types.Add(type[..Math.Min(type.Length, 256)]);
            truncated |= type.Length > 256;
            if (current is AggregateException aggregate) {
                for (var index = 0; index < aggregate.InnerExceptions.Count; index++) {
                    Add(aggregate.InnerExceptions[index]);
                    if (types.Count == MaximumTypes) {
                        truncated |= index + 1 < aggregate.InnerExceptions.Count;
                        break;
                    }
                }
            } else if (current.InnerException is { } inner) {
                Add(inner);
            }
        }
    }
}

public abstract record AgentDecisionDataPolicySaveOutcome {
    private AgentDecisionDataPolicySaveOutcome(AgentDecisionDataPolicyFailure? failure = null) {
        Failure = failure;
    }

    public AgentDecisionDataPolicyFailure? Failure { get; }

    public sealed record Accepted : AgentDecisionDataPolicySaveOutcome {
        public Accepted(ReplaceAgentDecisionDataPolicyCommand command, AgentDecisionDataPolicySnapshot snapshot,
            bool followupRequired = false, AgentDecisionDataPolicyFailure? failure = null) : base(failure) {
            ArgumentNullException.ThrowIfNull(command);
            ArgumentNullException.ThrowIfNull(snapshot);
            if (snapshot.Expectation.Workspace != command.Expectation.Workspace ||
                snapshot.Expectation.AgentId != command.Expectation.AgentId ||
                snapshot.Expectation.CatalogRevision != command.Expectation.CatalogRevision.Next() ||
                snapshot.Policy != command.Policy) {
                throw new ArgumentException("An acknowledgement must contain this command's exact accepted policy and next revision.", nameof(snapshot));
            }
            Command = command;
            Snapshot = snapshot;
            FollowupRequired = followupRequired;
        }

        public ReplaceAgentDecisionDataPolicyCommand Command { get; }
        public AgentDecisionDataPolicySnapshot Snapshot { get; }
        public bool FollowupRequired { get; }
    }

    public sealed record Rejected : AgentDecisionDataPolicySaveOutcome {
        public Rejected(AgentDecisionDataPolicyRejection reason, AgentDecisionDataPolicyFailure? failure = null) : base(failure) {
            if (!Enum.IsDefined(reason)) {
                throw new ArgumentOutOfRangeException(nameof(reason));
            }
            Reason = reason;
        }
        public AgentDecisionDataPolicyRejection Reason { get; }
    }

    public sealed record Unconfirmed : AgentDecisionDataPolicySaveOutcome {
        public Unconfirmed(ReplaceAgentDecisionDataPolicyCommand command, AgentDecisionDataPolicyFailure? failure = null) : base(failure) {
            ArgumentNullException.ThrowIfNull(command);
            Command = command;
        }
        public ReplaceAgentDecisionDataPolicyCommand Command { get; }
    }
}

public sealed class AgentDecisionDataPolicyContextChangedException()
    : InvalidOperationException("The original Agent policy workspace is no longer current.");
