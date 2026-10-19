using System.Collections.Immutable;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using CanDoItAll.SharedKernel;
using static CanDoItAll.Processes.Runtime.ProcessDecisionPolicyWire;

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
    private static readonly JsonSerializerOptions wireOptions = ProcessDecisionPolicyWire.Options;

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
            var materialShape = wire.ExecutionContract is not null &&
                (wire.MaterialProtocol == ProcessDecisionMaterialProtocol.CanonicalSnapshotV1 && !document.RootElement.TryGetProperty("contentContract", out _) ||
                wire.MaterialProtocol == ProcessDecisionMaterialProtocol.SharedContentV1 && wire.ContentContract is not null);
            var validSchema = wire.SchemaVersion switch {
                ProcessDecisionDataPolicyAdmission.LegacySchemaVersion => !document.RootElement.TryGetProperty("executionContract", out _) &&
                    !document.RootElement.TryGetProperty("materialProtocol", out _) && !document.RootElement.TryGetProperty("contentContract", out _),
                ProcessDecisionDataPolicyAdmission.ExecutionContractSchemaVersion => wire.ExecutionContract is not null &&
                    !document.RootElement.TryGetProperty("materialProtocol", out _) && !document.RootElement.TryGetProperty("contentContract", out _),
                ProcessDecisionDataPolicyAdmission.SchemaVersion => materialShape,
                ProcessDecisionDataPolicyAdmission.FundedSchemaVersion => materialShape && wire.Funding is not null,
                _ => false
            };
            if (!validSchema || wire.SchemaVersion != ProcessDecisionDataPolicyAdmission.FundedSchemaVersion && document.RootElement.TryGetProperty("funding", out _) ||
                wire.Capabilities is null ||
                wire.EnclosingBudgets.IsDefault || wire.EnclosingBudgets.Any(budget => budget is null)) {
                throw new JsonException("The Process decision policy requires its supported schema and original limits.");
            }
            var admission = new ProcessDecisionDataPolicyAdmission(wire.WorkspaceNamespaceId, wire.WorkspaceHeadRevision,
                new(wire.DefinitionVersionId), new(wire.StepInstanceId), wire.WorkspacePolicy, wire.DefinitionOverride,
                wire.StepOverride, wire.AuthorizedInvocationOverride, wire.Capabilities.ToModel(),
                [.. wire.EnclosingBudgets.Select(budget => budget.ToModel())], wire.Policy) {
                ExecutionContract = wire.ExecutionContract, MaterialProtocol = wire.MaterialProtocol, ContentContract = wire.ContentContract,
                Funding = wire.Funding?.ToModel()
            };
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
        var schema = value.Funding is not null ? ProcessDecisionDataPolicyAdmission.FundedSchemaVersion :
            value.MaterialProtocol is not null ? ProcessDecisionDataPolicyAdmission.SchemaVersion :
            value.ExecutionContract is null ? ProcessDecisionDataPolicyAdmission.LegacySchemaVersion : ProcessDecisionDataPolicyAdmission.ExecutionContractSchemaVersion;
        var wire = new AdmissionWire(schema, value.WorkspaceNamespaceId,
            value.WorkspaceHeadRevision, value.DefinitionVersionId.Value, value.StepInstanceId.Value,
            value.WorkspacePolicy, value.DefinitionOverride, value.StepOverride, value.AuthorizedInvocationOverride,
            CapabilitiesWire.From(value.OwnerCapabilities), [.. value.EnclosingBudgets.Select(BudgetWire.From)], value.Policy,
            value.ExecutionContract, value.MaterialProtocol, value.ContentContract,
            value.Funding is { } funding ? new FundingWire(funding.RootRunId.Value, funding.BudgetId.Value) : null);
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
        [property: JsonRequired] DecisionDataPolicy Policy,
        [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] ProcessDecisionExecutionContract? ExecutionContract,
        [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] ProcessDecisionMaterialProtocol? MaterialProtocol,
        [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] ProcessDecisionContentContract? ContentContract,
        [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] FundingWire? Funding);

    private sealed record FundingWire([property: JsonRequired] Guid RootRunId, [property: JsonRequired] Guid BudgetId) {
        internal ProcessDecisionFundingReference ToModel() => new(new(RootRunId), new(BudgetId));
    }
}
