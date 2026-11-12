using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.AgentFramework.UI.Catalog;
using CanDoItAll.Web.Api;

namespace CanDoItAll.Web.Infrastructure;

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
internal sealed record BrowserAgentDecisionPolicyPendingScope([property: JsonRequired] string Caller, [property: JsonRequired] string ProfileId,
    [property: JsonRequired] int ScopeKind, [property: JsonRequired] string ScopeKey);

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
internal sealed record BrowserAgentDecisionPolicyPendingKey([property: JsonRequired] string Caller, [property: JsonRequired] string ProfileId,
    [property: JsonRequired] int ScopeKind, [property: JsonRequired] string ScopeKey, [property: JsonRequired] string AgentId);
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
internal sealed record BrowserAgentDecisionPolicyPendingWire([property: JsonRequired] int Schema, [property: JsonRequired] BrowserAgentDecisionPolicyPendingKey Key,
    [property: JsonRequired] string OperationId, [property: JsonRequired] string Payload, [property: JsonRequired] AgentDecisionPolicyPendingStage Stage,
    [property: JsonRequired] string? Outcome);

internal static class BrowserAgentDecisionPolicyPendingCodec {
    private static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web) {
        PropertyNameCaseInsensitive = false, UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
        RespectRequiredConstructorParameters = true, RespectNullableAnnotations = true, MaxDepth = 8
    };

    internal static string Encode(ReplaceAgentDecisionDataPolicyCommand command)
        => EncodeBounded(new AgentDecisionPolicyApiSubmission(command.OperationId,
            AgentDecisionPolicyApiExpectation.From(command.Expectation), command.Policy), BrowserAgentDecisionPolicyPendingStore.MaximumRequestBytes);

    internal static string EncodeOutcome(ReplaceAgentDecisionDataPolicyCommand command, AgentDecisionDataPolicySaveOutcome outcome) {
        var wire = outcome switch {
            AgentDecisionDataPolicySaveOutcome.Accepted accepted when accepted.Command == command => new AgentDecisionPolicyApiResult(
                AgentDecisionPolicyApiOutcome.Accepted, command.OperationId, AgentDecisionPolicyApiSnapshot.From(accepted.Snapshot), FollowupRequired: accepted.FollowupRequired),
            AgentDecisionDataPolicySaveOutcome.Rejected rejected when Enum.IsDefined(rejected.Reason) => new AgentDecisionPolicyApiResult(
                AgentDecisionPolicyApiOutcome.Rejected, command.OperationId, Rejection: rejected.Reason),
            _ => throw new ArgumentException("Only an exact known policy outcome can resolve the retained submission.", nameof(outcome))
        };
        return EncodeBounded(wire, BrowserAgentDecisionPolicyPendingStore.MaximumOutcomeBytes);
    }

    internal static AgentDecisionPolicyPendingRecord? Decode(BrowserAgentDecisionPolicyPendingWire? wire, BrowserAgentDecisionPolicyPendingKey expected) {
        if (wire is null) {
            return null;
        }
        if (wire.Schema != 1 || wire.Key != expected || !Enum.IsDefined(wire.Stage) || !Guid.TryParseExact(expected.AgentId, "D", out var agentId)) {
            throw new InvalidOperationException("The retained policy record is invalid or belongs to another caller/workspace.");
        }
        var command = DecodeBounded<AgentDecisionPolicyApiSubmission>(wire.Payload, BrowserAgentDecisionPolicyPendingStore.MaximumRequestBytes).ToDomain(agentId);
        var workspace = command.Expectation.Workspace;
        if (wire.OperationId != command.OperationId.ToString("D") || expected.ProfileId != workspace.DatabaseProfileId.ToString("D") ||
            expected.ScopeKind != (int)workspace.WorkspaceScope.Kind || expected.ScopeKey != workspace.WorkspaceScope.Key) {
            throw new InvalidOperationException("The retained policy address does not match its original command.");
        }
        AgentDecisionDataPolicySaveOutcome? outcome = null;
        if (wire.Outcome is not null) {
            var result = DecodeBounded<AgentDecisionPolicyApiResult>(wire.Outcome, BrowserAgentDecisionPolicyPendingStore.MaximumOutcomeBytes);
            if (result.OperationId != command.OperationId) {
                throw new InvalidOperationException("The retained policy outcome belongs to another operation.");
            }
            outcome = result.Outcome switch {
                AgentDecisionPolicyApiOutcome.Accepted when result.Snapshot is not null && result.Rejection is null =>
                    new AgentDecisionDataPolicySaveOutcome.Accepted(command,
                        new(result.Snapshot.Expectation.ToDomain(), result.Snapshot.Policy), result.FollowupRequired),
                AgentDecisionPolicyApiOutcome.Rejected when result.Snapshot is null && result.Rejection is { } rejection &&
                    Enum.IsDefined(rejection) && !result.FollowupRequired => new AgentDecisionDataPolicySaveOutcome.Rejected(rejection),
                _ => throw new InvalidOperationException("The retained policy outcome is not a known result.")
            };
        }
        return new(command, wire.Stage, outcome);
    }

    private static string EncodeBounded<T>(T value, int maximum) {
        var bytes = JsonSerializer.SerializeToUtf8Bytes(value, Options);
        if (bytes.Length > maximum) {
            throw new InvalidOperationException("The retained policy payload exceeds its bound.");
        }
        return Encoding.UTF8.GetString(bytes);
    }

    private static T DecodeBounded<T>(string text, int maximum) {
        if (string.IsNullOrEmpty(text) || text.Length > maximum || Encoding.UTF8.GetByteCount(text) > maximum) {
            throw new InvalidOperationException("The retained policy payload exceeds its bound.");
        }
        using var document = JsonDocument.Parse(text, new() { MaxDepth = 8 });
        AgentApiRequestValidation.RequireUniqueMembers(document.RootElement);
        return document.Deserialize<T>(Options) ?? throw new JsonException("A retained policy payload is required.");
    }
}
