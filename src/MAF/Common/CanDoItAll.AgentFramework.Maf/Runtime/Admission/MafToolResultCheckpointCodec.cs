using System.Text.Json;
using System.Text.Json.Serialization;
using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.AgentFramework.Tooling;
using Microsoft.Extensions.AI;

namespace CanDoItAll.AgentFramework.Maf;

internal static class MafToolResultCheckpointCodec {
    internal static MafToolResultCheckpoint Decode(AgentToolProtocolEnvelope envelope)
        => MafToolProtocolCodec.Decode<MafToolResultCheckpoint>(envelope);

    internal static AgentToolProtocolEnvelope Capture(object? value, AgentToolPreDispatchFailure? failure = null,
        AgentToolCommittedEffect? committedEffect = null) {
        if (failure is not null && committedEffect is not null) {
            throw Denied("A committed owner acknowledgement cannot contain pre-dispatch failure evidence.");
        }
        if (failure is not null) {
            if (value is AgentToolFailureResult hostFailure) {
                RequireHostFailure(hostFailure, failure);
            } else if (value is not string) {
                throw Denied("A pre-dispatch denial requires its original text or typed host failure.");
            }
        }
        var kind = value switch {
            string when failure is not null => MafToolResultKind.PreDispatchDeniedText,
            AgentToolFailureResult when failure is not null => MafToolResultKind.PreDispatchDeniedJson,
            AgentToolFailureResult => MafToolResultKind.TypedFailureJson,
            BrowserMcpToolResult => MafToolResultKind.BrowserMcp,
            null => MafToolResultKind.Null,
            string => MafToolResultKind.Text,
            AIContent => MafToolResultKind.Content,
            IEnumerable<AIContent> => MafToolResultKind.Contents,
            _ => MafToolResultKind.Json
        };
        var element = kind switch {
            MafToolResultKind.BrowserMcp => JsonSerializer.SerializeToElement(new BrowserMcpCheckpoint(
                (BrowserMcpToolResult)value!, ((BrowserMcpToolResult)value!).CanonicalResult), MafToolProtocolCodec.SerializationOptions),
            MafToolResultKind.Content => JsonSerializer.SerializeToElement((AIContent)value!, MafToolProtocolCodec.SerializationOptions),
            MafToolResultKind.Contents => JsonSerializer.SerializeToElement(((IEnumerable<AIContent>)value!).ToArray(), MafToolProtocolCodec.SerializationOptions),
            _ => JsonSerializer.SerializeToElement(value, MafToolProtocolCodec.SerializationOptions)
        };
        return MafToolProtocolCodec.Encode(committedEffect is null
            ? new MafToolResultCheckpoint(kind, element) { PreDispatchFailure = failure }
            : new MafToolResultCheckpoint(MafToolResultKind.CommittedOwnerResult, element) {
                CommittedEffect = committedEffect,
                CommittedResultKind = kind
            });
    }

    internal static object? Restore(AgentToolProtocolEnvelope saved, AgentToolEffectState savedEffect) {
        var result = MafToolProtocolCodec.Decode<MafToolResultCheckpoint>(saved);
        var hasCommittedEvidence = result.CommittedEffect is not null || result.CommittedResultKind is not null;
        if ((result.Kind == MafToolResultKind.CommittedOwnerResult) != hasCommittedEvidence) {
            throw Denied("The saved owner acknowledgement has incompatible result evidence.");
        }
        if (result.Kind == MafToolResultKind.CommittedOwnerResult) {
            if (savedEffect != AgentToolEffectState.Committed || result.PreDispatchFailure is not null ||
                    result.CommittedEffect is not { } committed ||
                    string.IsNullOrWhiteSpace(committed.SourceKind) || committed.SourceKind != committed.SourceKind.Trim() ||
                    string.IsNullOrWhiteSpace(committed.SourceId) || committed.SourceId != committed.SourceId.Trim() ||
                    result.CommittedResultKind is not (MafToolResultKind.Null or MafToolResultKind.Text or MafToolResultKind.Content or
                        MafToolResultKind.Contents or MafToolResultKind.Json or MafToolResultKind.TypedFailureJson)) {
                throw Denied("The saved owner acknowledgement does not match its committed journal outcome.");
            }
            result = result with { Kind = result.CommittedResultKind.Value };
        }
        if ((result.Kind is MafToolResultKind.PreDispatchDeniedText or MafToolResultKind.PreDispatchDeniedJson) != (result.PreDispatchFailure is not null)) {
            throw Denied("The saved pre-dispatch denial has incompatible outcome evidence.");
        }
        if (result.PreDispatchFailure is { } failure) {
            if (savedEffect != AgentToolEffectState.NotCommitted) {
                throw Denied("The saved pre-dispatch denial does not match its journal outcome.");
            }
            if (result.Kind == MafToolResultKind.PreDispatchDeniedJson) {
                var hostFailure = result.Value.Deserialize<AgentToolFailureResult>(MafToolProtocolCodec.SerializationOptions)
                    ?? throw Denied("The saved pre-dispatch host failure is empty.");
                RequireHostFailure(hostFailure, failure);
            }
            AgentToolInvocationEffectScope.RecordPreDispatchFailure(failure);
        }
        object? value = result.Kind switch {
            MafToolResultKind.BrowserMcp => RestoreBrowserMcp(result.Value),
            MafToolResultKind.Null => null,
            MafToolResultKind.Text or MafToolResultKind.PreDispatchDeniedText => result.Value.GetString(),
            MafToolResultKind.Content => result.Value.Deserialize<AIContent>(MafToolProtocolCodec.SerializationOptions),
            MafToolResultKind.Contents => result.Value.Deserialize<AIContent[]>(MafToolProtocolCodec.SerializationOptions),
            MafToolResultKind.Json or MafToolResultKind.PreDispatchDeniedJson => result.Value,
            MafToolResultKind.TypedFailureJson => result.Value.Deserialize<AgentToolFailureResult>(MafToolProtocolCodec.SerializationOptions)
                ?? throw Denied("The saved typed tool failure is empty."),
            _ => throw Denied("The saved tool result shape is unsupported.")
        };
        if (result.CommittedEffect is { } acknowledgement) {
            AgentToolInvocationEffectScope.RecordCommitted(acknowledgement.SourceKind, acknowledgement.SourceId);
        }
        return value;
    }

    private static BrowserMcpToolResult RestoreBrowserMcp(JsonElement value) {
        var saved = value.Deserialize<BrowserMcpCheckpoint>(MafToolProtocolCodec.SerializationOptions)
            ?? throw Denied("The saved browser MCP evidence is empty.");
        return saved.Projection with { CanonicalResult = saved.CanonicalResult };
    }

    private sealed record BrowserMcpCheckpoint(BrowserMcpToolResult Projection, JsonElement CanonicalResult);

    private static void RequireHostFailure(AgentToolFailureResult hostFailure, AgentToolPreDispatchFailure captured) {
        if (hostFailure.Succeeded || hostFailure.EffectState != AgentToolEffectState.NotCommitted ||
                hostFailure.ErrorCode != captured.FailureCode || hostFailure.Message != captured.SafeMessage ||
                hostFailure.CanRetryWithCorrectedInput != captured.CanRetryWithCorrectedInput) {
            throw Denied("The saved pre-dispatch host failure differs from its trusted outcome evidence.");
        }
    }

    private static AgentToolAdmissionException Denied(string message) => new("tool-admission.runtime-denied", message);
}

internal enum MafToolResultKind { Null = 0, Text = 1, Content = 2, Contents = 3, Json = 4, PreDispatchDeniedText = 5, PreDispatchDeniedJson = 6, TypedFailureJson = 7, CommittedOwnerResult = 8, BrowserMcp = 9 }
internal sealed record MafToolResultCheckpoint(MafToolResultKind Kind, JsonElement Value) {
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public AgentToolPreDispatchFailure? PreDispatchFailure { get; init; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public AgentToolCommittedEffect? CommittedEffect { get; init; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public MafToolResultKind? CommittedResultKind { get; init; }
}
