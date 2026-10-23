using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using CanDoItAll.Processes.Abstractions;
using CanDoItAll.Processes.Application;
using CanDoItAll.Processes.Core;
using CanDoItAll.Processes.Runtime;

namespace CanDoItAll.Processes.Persistence;

internal static class ProcessCancellationRecoveryCodec {
    internal const int MaximumPositionBytes = 1024;
    internal const int MaximumIntentBytes = 8192;
    private static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web) {
        PropertyNameCaseInsensitive = false, UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
        RespectRequiredConstructorParameters = true, DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull, MaxDepth = 16
    };

    internal static string WritePosition(ProcessCancellationRecoveryPosition position) {
        position.Validate();
        return Encode(new PositionDocument(position.CoverageVersion, position.Stage, position.RunAfter?.Value, position.DecisionAfter?.Value,
            position.CurrentDecision?.DecisionId.Value, position.CurrentDecision?.Generation, position.ChildAfter?.Value,
            position.CurrentRun?.Value, position.ExecutionAfter?.Value, position.CycleHadPending), MaximumPositionBytes);
    }

    internal static ProcessCancellationRecoveryPosition ReadPosition(string json)
        => Decode<PositionDocument, ProcessCancellationRecoveryPosition>(json, MaximumPositionBytes, document => {
            if (document.Version is < 1 or > ProcessCancellationRecoveryPosition.CurrentCoverageVersion) {
                throw new InvalidDataException("The recovery position version is unsupported.");
            }
            return new(document.Stage, document.RunAfter is { } run ? new(run) : null,
                document.DecisionAfter is { } after ? new(after) : null,
                document.DecisionId is { } decision ? new(new(decision), document.Generation ?? 0) : null,
                document.ChildAfter is { } child ? new(child) : null,
                document.CurrentRun is { } current ? new(current) : null,
                document.ExecutionAfter is { } execution ? new(execution) : null, document.CycleHadPending, document.Version);
        }, WritePosition);

    internal static string? WriteIntent(ProcessRunId root, ProcessCancellationRecoveryIntent? intent) {
        if (intent is null) {
            return null;
        }
        var (kind, stage) = intent switch {
            ProcessCancellationCancelRun => (IntentKind.CancelRun, ProcessCancellationRecoveryStage.Runtime),
            ProcessCancellationAccountChild => (IntentKind.AccountChild, ProcessCancellationRecoveryStage.Decisions),
            ProcessCancellationFinalizeDecision => (IntentKind.FinalizeDecision, ProcessCancellationRecoveryStage.Decisions),
            ProcessCancellationChangeScope => (IntentKind.ChangeScope, ProcessCancellationRecoveryStage.SharedScope),
            ProcessCancellationFinalizeRoot => (IntentKind.FinalizeRoot, ProcessCancellationRecoveryStage.Quiescence),
            _ => throw new InvalidDataException("The recovery intent kind is unsupported.")
        };
        intent.Validate(root, stage);
        var context = intent.Context;
        var terminal = intent switch {
            ProcessCancellationAccountChild item => TerminalDocument.From(item.Request.Child.StepInstanceId, item.Request.Decision,
                item.Request.Budget, item.Request.StepStatus) with { ChildId = item.Request.Child.ChildId.Value, ChildRootId = item.Request.Child.RootRunId.Value },
            ProcessCancellationFinalizeDecision item => TerminalDocument.From(item.Request.StepInstanceId, item.Request.Decision,
                item.Request.Budget, item.Request.StepStatus),
            _ => null
        };
        var scope = intent is ProcessCancellationChangeScope change
            ? new ScopeDocument(change.Command.Expectation.ScopeId.Value, change.Command.Expectation.Revision, change.Command.Kind) : null;
        return Encode(new IntentDocument(1, kind, intent.RunId.Value, new(context.CommandId.Value, context.Actor.Kind,
            context.Actor.Id.Value, context.CorrelationId.Value, context.OccurredAtUtc), terminal, scope), MaximumIntentBytes);
    }

    internal static ProcessCancellationRecoveryIntent? ReadIntent(ProcessRunId root, string? json)
        => json is null ? null : Decode<IntentDocument, ProcessCancellationRecoveryIntent>(json, MaximumIntentBytes, document => {
            if (document.Version != 1 || document.Context is null) {
                throw new InvalidDataException("The recovery intent has an unsupported version or missing context.");
            }
            ProcessRunId run = new(document.RunId);
            var original = document.Context;
            RuntimeCommandContext context = new(new(original.ActionId), new(original.ActorKind, new(original.ActorId)),
                new(original.CorrelationId), original.OccurredAtUtc);
            var terminal = document.Terminal;
            return document.Kind switch {
                IntentKind.CancelRun => new ProcessCancellationCancelRun(run, context),
                IntentKind.FinalizeRoot => new ProcessCancellationFinalizeRoot(run, context),
                IntentKind.AccountChild when terminal is { ChildId: { } child, ChildRootId: { } childRoot }
                    => new ProcessCancellationAccountChild(run, context, new(new(new(childRoot), run, new(terminal.StepId),
                        terminal.Decision.Reference, new(child)), terminal.Decision, terminal.Budget, terminal.StepStatus, context.CommandId)),
                IntentKind.FinalizeDecision when terminal is not null
                    => new ProcessCancellationFinalizeDecision(run, context, new(new(terminal.StepId), terminal.Decision,
                        terminal.Budget, terminal.StepStatus, context.CommandId)),
                IntentKind.ChangeScope when document.Scope is { } scope
                    => new ProcessCancellationChangeScope(run, context, new(new(new(scope.ScopeId), scope.Revision), scope.Kind)),
                _ => throw new InvalidDataException("The recovery intent has an invalid command shape.")
            };
        }, value => WriteIntent(root, value)!);

    internal static ProcessCancellationRecoveryClaim ReadClaim(ProcessCancellationRecoveryEntity entity) {
        ProcessRunId root = new(entity.RootRunId);
        if (entity.Completed || entity.LeaseToken is null || entity.LeaseExpiresAtUtc is null) {
            throw new InvalidDataException("A recovery claim requires an unfinished owned row.");
        }
        ProcessCancellationRecoveryClaim result = new(root, entity.Origin,
            entity.BarrierCommandId is { } command ? new(command) : null, entity.LeaseToken.Value, entity.LeaseGeneration,
            entity.ProgressRevision, entity.LeaseExpiresAtUtc.Value, entity.Cycle, ReadPosition(entity.PositionJson),
            ReadIntent(root, entity.IntentJson), entity.Pending);
        result.Validate();
        return result;
    }

    private static string Encode<T>(T value, int maximum) {
        var json = JsonSerializer.Serialize(value, Options);
        RequireBound(json, maximum);
        return json;
    }

    private static TResult Decode<TDocument, TResult>(string json, int maximum, Func<TDocument, TResult> read, Func<TResult, string> write) {
        RequireBound(json, maximum);
        try {
            var document = JsonSerializer.Deserialize<TDocument>(json, Options) ?? throw new InvalidDataException("The recovery record is empty.");
            var value = read(document);
            if (!string.Equals(json, write(value), StringComparison.Ordinal)) {
                throw new InvalidDataException("The recovery record is not a supported canonical representation.");
            }
            return value;
        } catch (Exception error) when (error is JsonException or ArgumentException) {
            throw new InvalidDataException("The recovery record is malformed.", error);
        }
    }

    private static void RequireBound(string json, int maximum) {
        if (string.IsNullOrWhiteSpace(json) || Encoding.UTF8.GetByteCount(json) > maximum) {
            throw new InvalidDataException("The recovery record exceeds its bounded wire contract.");
        }
    }

    private enum IntentKind { CancelRun, AccountChild, FinalizeDecision, ChangeScope, FinalizeRoot }
    private sealed record PositionDocument(int Version, ProcessCancellationRecoveryStage Stage, Guid? RunAfter = null,
        Guid? DecisionAfter = null, Guid? DecisionId = null, int? Generation = null, Guid? ChildAfter = null,
        Guid? CurrentRun = null, Guid? ExecutionAfter = null,
        [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)] bool CycleHadPending = false);
    private sealed record ContextDocument(Guid ActionId, ProcessEventActorKind ActorKind, string ActorId, string CorrelationId, DateTimeOffset OccurredAtUtc);
    private sealed record IntentDocument(int Version, IntentKind Kind, Guid RunId, ContextDocument Context,
        TerminalDocument? Terminal = null, ScopeDocument? Scope = null);
    private sealed record ScopeDocument(Guid ScopeId, long Revision, ProcessDecisionRunScopeCommandKind Kind);
    private sealed record TerminalDocument(Guid StepId, Guid DecisionId, int Generation, long DecisionRevision,
        Guid BudgetId, long BudgetRevision, ProcessRuntimeStepStatus StepStatus, Guid? ChildId = null, Guid? ChildRootId = null) {
        [JsonIgnore] public ProcessDecisionExpectation Decision => new(new(new(DecisionId), Generation), DecisionRevision);
        [JsonIgnore] public ProcessDecisionBudgetExpectation Budget => new(new(StepId), new(BudgetId), BudgetRevision);

        internal static TerminalDocument From(ProcessStepInstanceId step, ProcessDecisionExpectation decision,
            ProcessDecisionBudgetExpectation budget, ProcessRuntimeStepStatus status)
            => new(step.Value, decision.Reference.DecisionId.Value, decision.Reference.Generation, decision.Revision,
                budget.BudgetId.Value, budget.Revision, status);
    }
}
