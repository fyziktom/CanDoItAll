using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.SharedKernel;

namespace CanDoItAll.Web.Api;

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
internal sealed record AgentDecisionPolicyApiExpectation(Guid DatabaseProfileId, WorkspaceScopeKind ScopeKind,
    string ScopeKey, string DatabaseProfileGeneration, Guid AgentId, string CatalogRevision) {
    internal AgentDecisionDataPolicyExpectation ToDomain() {
        if (!Enum.IsDefined(ScopeKind)) {
            throw new ArgumentException("The workspace scope kind is invalid.");
        }
        var scope = new WorkspaceScopeDescriptor(ScopeKind, ScopeKey);
        if (scope.Key != ScopeKey) {
            throw new ArgumentException("The original canonical workspace scope is required.");
        }
        return new(new(DatabaseProfileId, scope, new(ParseRevision(DatabaseProfileGeneration))),
            AgentId, new(ParseRevision(CatalogRevision)));
    }

    internal static AgentDecisionPolicyApiExpectation From(AgentDecisionDataPolicyExpectation value)
        => new(value.Workspace.DatabaseProfileId, value.Workspace.WorkspaceScope.Kind, value.Workspace.WorkspaceScope.Key,
            value.Workspace.DatabaseProfileGeneration.Value.ToString(CultureInfo.InvariantCulture), value.AgentId,
            value.CatalogRevision.Value.ToString(CultureInfo.InvariantCulture));

    private static long ParseRevision(string text) {
        if (text is null || !long.TryParse(text, NumberStyles.None, CultureInfo.InvariantCulture, out var value) ||
            text != value.ToString(CultureInfo.InvariantCulture)) {
            throw new ArgumentException("Revisions require canonical nonnegative Int64 decimal strings.");
        }
        return value;
    }
}

internal sealed record AgentDecisionPolicyApiSnapshot(AgentDecisionPolicyApiExpectation Expectation,
    [property: JsonConverter(typeof(AgentDecisionPolicyApiOverrideConverter))] DecisionDataPolicyOverride? Policy) {
    internal static AgentDecisionPolicyApiSnapshot From(AgentDecisionDataPolicySnapshot value)
        => new(AgentDecisionPolicyApiExpectation.From(value.Expectation), value.Policy);
}

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
internal sealed record AgentDecisionPolicyApiSubmission(Guid OperationId, AgentDecisionPolicyApiExpectation Expectation,
    [property: JsonConverter(typeof(AgentDecisionPolicyApiOverrideConverter))] DecisionDataPolicyOverride Policy) {
    internal ReplaceAgentDecisionDataPolicyCommand ToDomain(Guid agentId) {
        if (agentId == Guid.Empty || Expectation.AgentId != agentId) {
            throw new ArgumentException("The route must match the original Agent.");
        }
        return new(OperationId, Expectation.ToDomain(), Policy);
    }
}

[JsonConverter(typeof(JsonStringEnumConverter<AgentDecisionPolicyApiOutcome>))]
internal enum AgentDecisionPolicyApiOutcome { Accepted, Rejected, Unconfirmed }

internal sealed record AgentDecisionPolicyApiResult(AgentDecisionPolicyApiOutcome Outcome, Guid OperationId,
    AgentDecisionPolicyApiSnapshot? Snapshot = null, AgentDecisionDataPolicyRejection? Rejection = null,
    bool FollowupRequired = false);

internal sealed class AgentDecisionPolicyApiOverrideConverter : JsonConverter<DecisionDataPolicyOverride> {
    public override DecisionDataPolicyOverride Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) {
        using var document = JsonDocument.ParseValue(ref reader);
        return DecisionDataPolicyOverrideCodec.Deserialize(document.RootElement.GetRawText());
    }

    public override void Write(Utf8JsonWriter writer, DecisionDataPolicyOverride value, JsonSerializerOptions options)
        => writer.WriteRawValue(DecisionDataPolicyOverrideCodec.Serialize(value));
}
