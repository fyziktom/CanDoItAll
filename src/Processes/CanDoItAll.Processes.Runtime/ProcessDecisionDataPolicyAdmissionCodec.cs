using System.Collections.Immutable;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using CanDoItAll.SharedKernel;

namespace CanDoItAll.Processes.Runtime;

public static class ProcessDecisionDataPolicyAdmissionCodec {
    public static string Serialize(ProcessDecisionDataPolicyAdmission admission) {
        ArgumentNullException.ThrowIfNull(admission);
        return Encoding.UTF8.GetString(ProcessDecisionDataPolicyAdmissionJsonConverter.Encode(admission));
    }

    public static ProcessDecisionDataPolicyAdmission Deserialize(string json) {
        ArgumentNullException.ThrowIfNull(json);
        if (Encoding.UTF8.GetByteCount(json) > ProcessDecisionDataPolicyAdmission.MaximumUtf8Bytes) {
            throw new JsonException("The Process decision policy exceeds its encoding bound.");
        }
        return JsonSerializer.Deserialize<ProcessDecisionDataPolicyAdmission>(json)
            ?? throw new JsonException("The Process decision policy must be an object.");
    }
}

internal sealed class ProcessDecisionDataPolicyAdmissionJsonConverter : JsonConverter<ProcessDecisionDataPolicyAdmission> {
    private static readonly JsonSerializerOptions wireOptions = new() {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = false,
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
        NumberHandling = JsonNumberHandling.Strict,
        MaxDepth = 16,
        Converters = { new OverrideConverter() }
    };

    public override bool HandleNull => true;

    public override ProcessDecisionDataPolicyAdmission Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) {
        var check = reader;
        if (reader.TokenType != JsonTokenType.StartObject || !check.TrySkip() ||
            check.BytesConsumed - reader.TokenStartIndex > ProcessDecisionDataPolicyAdmission.MaximumUtf8Bytes) {
            throw new JsonException("The Process decision policy requires a bounded object.");
        }
        using var document = JsonDocument.ParseValue(ref reader);
        RequireDistinctProperties(document.RootElement, 0);
        try {
            var wire = document.RootElement.Deserialize<AdmissionWire>(wireOptions)
                ?? throw new JsonException("The Process decision policy must be an object.");
            if (wire.SchemaVersion != ProcessDecisionDataPolicyAdmission.SchemaVersion || wire.Capabilities is null ||
                wire.EnclosingBudgets.IsDefault || wire.EnclosingBudgets.Any(budget => budget is null)) {
                throw new JsonException("The Process decision policy requires its supported schema and original limits.");
            }
            var admission = new ProcessDecisionDataPolicyAdmission(wire.WorkspaceNamespaceId, wire.WorkspaceHeadRevision,
                new(wire.DefinitionVersionId), new(wire.StepInstanceId), wire.WorkspacePolicy, wire.DefinitionOverride,
                wire.StepOverride, wire.AuthorizedInvocationOverride, wire.Capabilities.ToModel(),
                [.. wire.EnclosingBudgets.Select(budget => budget.ToModel())], wire.Policy);
            admission.Validate();
            return admission;
        } catch (Exception failure) when (failure is ArgumentException or InvalidDataException or OverflowException or DecisionDataPolicyException) {
            throw new JsonException("The saved Process decision policy is invalid.", failure);
        }
    }

    public override void Write(Utf8JsonWriter writer, ProcessDecisionDataPolicyAdmission value, JsonSerializerOptions options)
        => writer.WriteRawValue(Encode(value));

    internal static byte[] Encode(ProcessDecisionDataPolicyAdmission value) {
        ArgumentNullException.ThrowIfNull(value);
        value.Validate();
        var wire = new AdmissionWire(ProcessDecisionDataPolicyAdmission.SchemaVersion, value.WorkspaceNamespaceId,
            value.WorkspaceHeadRevision, value.DefinitionVersionId.Value, value.StepInstanceId.Value,
            value.WorkspacePolicy, value.DefinitionOverride, value.StepOverride, value.AuthorizedInvocationOverride,
            CapabilitiesWire.From(value.OwnerCapabilities), [.. value.EnclosingBudgets.Select(BudgetWire.From)], value.Policy);
        var bytes = JsonSerializer.SerializeToUtf8Bytes(wire, wireOptions);
        if (bytes.Length > ProcessDecisionDataPolicyAdmission.MaximumUtf8Bytes) {
            throw new JsonException("The Process decision policy exceeds its encoding bound.");
        }
        return bytes;
    }

    private static void RequireDistinctProperties(JsonElement element, int depth) {
        if (depth > wireOptions.MaxDepth) {
            throw new JsonException("The Process decision policy exceeds its nesting bound.");
        }
        if (element.ValueKind == JsonValueKind.Object) {
            HashSet<string> names = new(StringComparer.Ordinal);
            foreach (var property in element.EnumerateObject()) {
                if (!names.Add(property.Name)) {
                    throw new JsonException("The Process decision policy contains duplicate fields.");
                }
                RequireDistinctProperties(property.Value, depth + 1);
            }
        } else if (element.ValueKind == JsonValueKind.Array) {
            foreach (var item in element.EnumerateArray()) {
                RequireDistinctProperties(item, depth + 1);
            }
        }
    }

    private sealed record AdmissionWire(
        [property: JsonRequired] int SchemaVersion,
        [property: JsonRequired] Guid WorkspaceNamespaceId,
        [property: JsonRequired] Guid WorkspaceHeadRevision,
        [property: JsonRequired] Guid DefinitionVersionId,
        [property: JsonRequired] Guid StepInstanceId,
        [property: JsonRequired] DecisionDataPolicy WorkspacePolicy,
        [property: JsonRequired] DecisionDataPolicyOverride? DefinitionOverride,
        [property: JsonRequired] DecisionDataPolicyOverride? StepOverride,
        [property: JsonRequired] DecisionDataPolicyOverride? AuthorizedInvocationOverride,
        [property: JsonRequired] CapabilitiesWire Capabilities,
        [property: JsonRequired] ImmutableArray<BudgetWire> EnclosingBudgets,
        [property: JsonRequired] DecisionDataPolicy Policy);

    private sealed record BudgetWire(
        [property: JsonRequired] int MaximumReplans,
        [property: JsonRequired] TimeSpan MaximumWorkDuration,
        [property: JsonRequired] TimeSpan MaximumBasisAge) {
        internal static BudgetWire From(DecisionDataPolicyBudget value)
            => new(value.MaximumReplans, value.MaximumWorkDuration, value.MaximumBasisAge);
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
        internal static CapabilitiesWire From(DecisionDataPolicyCapabilities value)
            => new(value.SupportsRunSnapshot, value.SupportsStepSnapshot, value.SupportsPinnedObservation,
                value.SupportsCurrentPublication, value.SupportsPauseForReview, value.SupportsReplan,
                value.MinimumPublicationFreshness, BudgetWire.From(value.BudgetCeiling));
        internal DecisionDataPolicyCapabilities ToModel() {
            if (BudgetCeiling is null) {
                throw new InvalidDataException("The Process decision policy requires its owner budget ceiling.");
            }
            return new(SupportsRunSnapshot, SupportsStepSnapshot, SupportsPinnedObservation,
                SupportsCurrentPublication, SupportsPauseForReview, SupportsReplan, MinimumPublicationFreshness, BudgetCeiling.ToModel());
        }
    }

    private sealed class OverrideConverter : JsonConverter<DecisionDataPolicyOverride> {
        public override DecisionDataPolicyOverride? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) {
            using var document = JsonDocument.ParseValue(ref reader);
            return DecisionDataPolicyOverrideCodec.Deserialize(document.RootElement.GetRawText());
        }
        public override void Write(Utf8JsonWriter writer, DecisionDataPolicyOverride value, JsonSerializerOptions options)
            => writer.WriteRawValue(DecisionDataPolicyOverrideCodec.Serialize(value));
    }
}
