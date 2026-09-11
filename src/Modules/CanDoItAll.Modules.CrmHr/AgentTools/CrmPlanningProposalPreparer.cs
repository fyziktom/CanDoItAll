using System.Text.Json;
using System.Text.Json.Serialization;
using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;

namespace CanDoItAll.Modules.CrmHr;

internal sealed record CrmPlanningReadSource(AgentToolSessionReference Session, Guid AgentId, Guid ProviderId,
    WorkspaceScopeDescriptor Scope, string CapabilityKey, Guid CapabilityId, AgentToolProtocolEnvelope WorkspaceSource);

internal sealed record CrmPlanningSearchArguments([property: JsonRequired] CrmHrAgentSearchQuery Request);
internal sealed record CrmPlanningSummaryArguments([property: JsonRequired] CrmHrAgentItemReference Request);

internal sealed class CrmPlanningProposalPreparer(CrmPlanningReadSource source) : IAgentToolProposalPreparer {
    internal const string SourceFormat = "crm-planning-read-source";
    internal const int SemanticVersion = 2;
    internal static JsonSerializerOptions Json { get; } = new(JsonSerializerDefaults.Web) {
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
        Converters = { new JsonStringEnumConverter() }
    };

    public bool Supports(string toolName)
        => toolName is CrmPlanningToolPolicy.Search or CrmPlanningToolPolicy.Summary;

    public AgentToolPreparedPayload Prepare(string toolName, JsonElement arguments) {
        if (!Supports(toolName) || source.CapabilityKey != CrmPlanningToolPolicy.CapabilityKey(toolName)) {
            throw new ArgumentException("The CRM planning source does not authorize this operation.", nameof(toolName));
        }
        var canonical = CanonicalArguments(toolName, arguments);
        var envelope = WriteSource(source);
        var digest = AgentToolProtocolEnvelope.ComputeDigest(JsonSerializer.Serialize(new {
            domain = SourceFormat,
            semanticVersion = SemanticVersion,
            toolName,
            argumentsJson = canonical,
            source = envelope,
            effect = AgentToolProposalEffect.Read,
            recovery = AgentToolProposalRecovery.RevalidateAndRead
        }, Json));
        return new(toolName, SemanticVersion, digest, canonical, AgentToolProposalEffect.Read,
            AgentToolProposalRecovery.RevalidateAndRead, envelope);
    }

    internal CrmPlanningReadSource Require(AgentToolPreparedPayload payload) {
        var saved = ReadSource(payload.SourcePreparation);
        if (saved != source || payload.SemanticVersion != SemanticVersion ||
                payload.Effect != AgentToolProposalEffect.Read || payload.Recovery != AgentToolProposalRecovery.RevalidateAndRead) {
            throw Denied();
        }
        using var arguments = JsonDocument.Parse(payload.ArgumentsJson);
        var expected = Prepare(payload.ToolName, arguments.RootElement);
        if (expected.Digest != payload.Digest || expected.ArgumentsJson != payload.ArgumentsJson) {
            throw Denied();
        }
        return saved;
    }

    internal static T Read<T>(JsonElement value) where T : class
        => value.Deserialize<T>(Json) ?? throw new InvalidDataException("The CRM planning request is missing.");

    private static string CanonicalArguments(string toolName, JsonElement arguments) {
        if (toolName == CrmPlanningToolPolicy.Search) {
            var value = Read<CrmPlanningSearchArguments>(arguments);
            ArgumentNullException.ThrowIfNull(value.Request);
            return JsonSerializer.Serialize(value, Json);
        }
        var summary = Read<CrmPlanningSummaryArguments>(arguments);
        ArgumentNullException.ThrowIfNull(summary.Request);
        return JsonSerializer.Serialize(summary, Json);
    }

    internal static CrmPlanningReadSource ReadSource(AgentToolProtocolEnvelope? envelope) {
        if (envelope is not { Format: SourceFormat, Version: 1 }) {
            throw new AgentToolAdmissionException("crm-planning.original-source-required",
                "The CRM planning proposal lacks its original granted source. Start a new authorized turn.");
        }
        var saved = JsonSerializer.Deserialize<CrmPlanningReadSource>(envelope.PayloadJson, Json)
            ?? throw Denied();
        Validate(saved);
        return saved;
    }

    private static AgentToolProtocolEnvelope WriteSource(CrmPlanningReadSource value) {
        Validate(value);
        return AgentToolProtocolEnvelope.Create(SourceFormat, 1, JsonSerializer.Serialize(value, Json));
    }

    private static void Validate(CrmPlanningReadSource value) {
        if (value.Session is null || value.Session.BackgroundSource is not null || value.AgentId == Guid.Empty ||
                value.ProviderId == Guid.Empty || value.CapabilityId == Guid.Empty ||
                value.CapabilityKey is not (CrmPlanningToolPolicy.SearchCapability or CrmPlanningToolPolicy.SummaryCapability) ||
                value.Scope is not { Kind: WorkspaceScopeKind.Project } ||
                !Guid.TryParse(value.Scope.Key, out var projectId) || projectId == Guid.Empty || value.WorkspaceSource is null) {
            throw Denied();
        }
    }

    internal static AgentToolAdmissionException Denied() => new("crm-planning.read-denied",
        "The original CRM planning capability, source, provider or project scope is no longer authorized.");
}
