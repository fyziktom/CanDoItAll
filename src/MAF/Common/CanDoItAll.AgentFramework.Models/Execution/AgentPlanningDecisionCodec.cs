using System.Collections.Immutable;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using CanDoItAll.SharedKernel;

namespace CanDoItAll.AgentFramework.Models;

public static class AgentPlanningDecisionCodec {
    public static string Serialize(AgentPlanningDecisionRecord decision) {
        ArgumentNullException.ThrowIfNull(decision);
        return Encoding.UTF8.GetString(AgentPlanningDecisionJsonConverter.Encode(decision));
    }

    public static AgentPlanningDecisionRecord Deserialize(string json) {
        ArgumentNullException.ThrowIfNull(json);
        if (Encoding.UTF8.GetByteCount(json) > AgentPlanningDecisionRecord.MaximumUtf8Bytes) {
            throw new JsonException("The planning decision exceeds its supported encoding bound.");
        }
        return JsonSerializer.Deserialize<AgentPlanningDecisionRecord>(json)
            ?? throw new JsonException("A planning decision must be an object.");
    }
}

internal sealed class AgentPlanningDecisionJsonConverter : JsonConverter<AgentPlanningDecisionRecord> {
    private static readonly JsonSerializerOptions wireOptions = new() {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = false,
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
        NumberHandling = JsonNumberHandling.Strict,
        MaxDepth = 32,
        Converters = { new OverrideConverter() }
    };

    public override bool HandleNull => true;

    public override AgentPlanningDecisionRecord Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) {
        if (reader.TokenType != JsonTokenType.StartObject) {
            throw new JsonException("A planning decision must be an object.");
        }
        var check = reader;
        if (!check.TrySkip() || check.BytesConsumed - reader.TokenStartIndex > AgentPlanningDecisionRecord.MaximumUtf8Bytes) {
            throw new JsonException("The planning decision exceeds its supported encoding bound.");
        }
        using var document = JsonDocument.ParseValue(ref reader);
        RequireDistinctProperties(document.RootElement, 0);
        try {
            var wire = document.RootElement.Deserialize<DecisionWire>(wireOptions)
                ?? throw new JsonException("A planning decision must be an object.");
            if (wire.SchemaVersion != AgentPlanningDecisionRecord.Version || wire.Provenance is null) {
                throw new JsonException("The planning decision requires its supported version and original provenance.");
            }
            var result = new AgentPlanningDecisionRecord(wire.DecisionId, wire.Session, wire.StartedAtUtc, wire.Owner,
                wire.OriginalTarget, wire.Policy, wire.Provenance.ToModel(), wire.CurrentGeneration, wire.AutomaticReplans,
                wire.Generations, wire.Work);
            result.Validate();
            return result;
        } catch (Exception failure) when (failure is ArgumentException or InvalidDataException or OverflowException) {
            throw new JsonException("The saved planning decision is invalid.", failure);
        }
    }

    public override void Write(Utf8JsonWriter writer, AgentPlanningDecisionRecord value, JsonSerializerOptions options)
        => writer.WriteRawValue(Encode(value));

    internal static byte[] Encode(AgentPlanningDecisionRecord value) {
        ArgumentNullException.ThrowIfNull(value);
        value.Validate();
        var wire = new DecisionWire(AgentPlanningDecisionRecord.Version, value.DecisionId, value.Session, value.StartedAtUtc,
            value.Owner, value.OriginalTarget, value.Policy, ProvenanceWire.From(value.Provenance), value.CurrentGeneration,
            value.AutomaticReplans, value.Generations, value.Work);
        using var stream = new BoundedStream();
        using (var writer = new Utf8JsonWriter(stream)) {
            JsonSerializer.Serialize(writer, wire, wireOptions);
        }
        return stream.ToArray();
    }

    private static void RequireDistinctProperties(JsonElement element, int depth) {
        if (depth > wireOptions.MaxDepth) {
            throw new JsonException("The planning decision exceeds its supported nesting bound.");
        }
        if (element.ValueKind == JsonValueKind.Object) {
            var names = new HashSet<string>(StringComparer.Ordinal);
            foreach (var property in element.EnumerateObject()) {
                if (!names.Add(property.Name)) {
                    throw new JsonException("The planning decision cannot repeat a property.");
                }
                RequireDistinctProperties(property.Value, depth + 1);
            }
        } else if (element.ValueKind == JsonValueKind.Array) {
            foreach (var item in element.EnumerateArray()) {
                RequireDistinctProperties(item, depth + 1);
            }
        }
    }

    private sealed record DecisionWire(
        [property: JsonRequired] int SchemaVersion,
        [property: JsonRequired] Guid DecisionId,
        [property: JsonRequired] AgentToolSessionReference Session,
        [property: JsonRequired] DateTimeOffset StartedAtUtc,
        [property: JsonRequired] AgentPlanningOwner Owner,
        [property: JsonRequired] AgentToolProtocolEnvelope OriginalTarget,
        [property: JsonRequired] DecisionDataPolicy Policy,
        [property: JsonRequired] ProvenanceWire Provenance,
        [property: JsonRequired] int CurrentGeneration,
        [property: JsonRequired] int AutomaticReplans,
        [property: JsonRequired] ImmutableArray<AgentPlanningGenerationRecord> Generations,
        [property: JsonRequired] AgentPlanningWorkRecord Work);

    private sealed record ProvenanceWire(
        [property: JsonRequired] Guid WorkspaceNamespaceId,
        [property: JsonRequired] Guid WorkspaceHeadRevision,
        [property: JsonRequired] Guid AgentId,
        [property: JsonRequired] long CatalogDataRevision,
        [property: JsonRequired] DecisionDataPolicy WorkspacePolicy,
        [property: JsonRequired] DecisionDataPolicyOverride? AgentOverride,
        [property: JsonRequired] DecisionDataPolicyOverride? AuthorizedInvocationOverride,
        [property: JsonRequired] CapabilitiesWire OwnerCapabilities,
        [property: JsonRequired] ImmutableArray<BudgetWire> EnclosingBudgets) {
        internal static ProvenanceWire From(AgentPlanningPolicyProvenance value) => new(value.WorkspaceNamespaceId,
            value.WorkspaceHeadRevision, value.AgentId, value.CatalogDataRevision, value.WorkspacePolicy, value.AgentOverride,
            value.AuthorizedInvocationOverride, CapabilitiesWire.From(value.OwnerCapabilities),
            value.EnclosingBudgets.Select(BudgetWire.From).ToImmutableArray());

        internal AgentPlanningPolicyProvenance ToModel() {
            if (OwnerCapabilities is null || EnclosingBudgets.IsDefault || EnclosingBudgets.Length > AgentPlanningDecisionRecord.MaximumSteps ||
                EnclosingBudgets.Any(item => item is null)) {
                throw AgentPlanningDecisionRecord.Invalid();
            }
            return new(WorkspaceNamespaceId, WorkspaceHeadRevision, AgentId, CatalogDataRevision, WorkspacePolicy, AgentOverride,
                AuthorizedInvocationOverride, OwnerCapabilities.ToModel(), EnclosingBudgets.Select(item => item.ToModel()).ToImmutableArray());
        }
    }

    private sealed record BudgetWire(
        [property: JsonRequired] int MaximumReplans,
        [property: JsonRequired] TimeSpan MaximumWorkDuration,
        [property: JsonRequired] TimeSpan MaximumBasisAge) {
        internal static BudgetWire From(DecisionDataPolicyBudget value) => new(value.MaximumReplans, value.MaximumWorkDuration, value.MaximumBasisAge);
        internal DecisionDataPolicyBudget ToModel() => new(MaximumReplans, MaximumWorkDuration, MaximumBasisAge);
    }

    private sealed record CapabilitiesWire(
        [property: JsonRequired] bool SupportsRunSnapshot,
        [property: JsonRequired] bool SupportsStepSnapshot,
        [property: JsonRequired] bool SupportsPinnedObservation,
        [property: JsonRequired] bool SupportsCurrentPublication,
        [property: JsonRequired] bool SupportsPauseForReview,
        [property: JsonRequired] bool SupportsReplan,
        [property: JsonRequired] PublicationFreshness MinimumPublicationFreshness,
        [property: JsonRequired] BudgetWire BudgetCeiling) {
        internal static CapabilitiesWire From(DecisionDataPolicyCapabilities value) => new(value.SupportsRunSnapshot,
            value.SupportsStepSnapshot, value.SupportsPinnedObservation, value.SupportsCurrentPublication, value.SupportsPauseForReview,
            value.SupportsReplan, value.MinimumPublicationFreshness, BudgetWire.From(value.BudgetCeiling));
        internal DecisionDataPolicyCapabilities ToModel() => new(SupportsRunSnapshot, SupportsStepSnapshot, SupportsPinnedObservation,
            SupportsCurrentPublication, SupportsPauseForReview, SupportsReplan, MinimumPublicationFreshness,
            (BudgetCeiling ?? throw AgentPlanningDecisionRecord.Invalid()).ToModel());
    }

    private sealed class OverrideConverter : JsonConverter<DecisionDataPolicyOverride> {
        public override DecisionDataPolicyOverride? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) {
            using var document = JsonDocument.ParseValue(ref reader);
            return DecisionDataPolicyOverrideCodec.Deserialize(document.RootElement.GetRawText());
        }
        public override void Write(Utf8JsonWriter writer, DecisionDataPolicyOverride value, JsonSerializerOptions options)
            => writer.WriteRawValue(DecisionDataPolicyOverrideCodec.Serialize(value));
    }

    private sealed class BoundedStream : MemoryStream {
        public override void Write(ReadOnlySpan<byte> buffer) {
            RequireCapacity(buffer.Length);
            base.Write(buffer);
        }
        public override void Write(byte[] buffer, int offset, int count) {
            RequireCapacity(count);
            base.Write(buffer, offset, count);
        }
        private void RequireCapacity(int count) {
            if (count > AgentPlanningDecisionRecord.MaximumUtf8Bytes - Length) {
                throw new JsonException("The planning decision exceeds its supported encoding bound.");
            }
        }
    }
}
