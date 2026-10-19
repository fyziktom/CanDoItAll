using System.Collections.Immutable;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using CanDoItAll.SharedKernel;
using static CanDoItAll.Processes.Runtime.ProcessDecisionPolicyWire;

namespace CanDoItAll.Processes.Runtime;

public static class ProcessDecisionRunAdmissionCodec {
    public static string Serialize(ProcessDecisionRunAdmission admission)
        => Encoding.UTF8.GetString(ProcessDecisionRunAdmissionJsonConverter.Encode(admission));

    public static ProcessDecisionRunAdmission Deserialize(string json) {
        ArgumentNullException.ThrowIfNull(json);
        if (Encoding.UTF8.GetByteCount(json) > ProcessDecisionRunAdmission.MaximumUtf8Bytes) {
            throw new JsonException("The Process root admission exceeds its encoding bound.");
        }
        return JsonSerializer.Deserialize<ProcessDecisionRunAdmission>(json)
            ?? throw new JsonException("The Process root admission must be an object.");
    }
}

internal sealed class ProcessDecisionRunAdmissionJsonConverter : JsonConverter<ProcessDecisionRunAdmission> {
    public override bool HandleNull => true;

    public override ProcessDecisionRunAdmission Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) {
        var check = reader;
        if (reader.TokenType != JsonTokenType.StartObject || !check.TrySkip() ||
            check.BytesConsumed - reader.TokenStartIndex > ProcessDecisionRunAdmission.MaximumUtf8Bytes) {
            throw new JsonException("The Process root admission requires a bounded object.");
        }
        using var document = JsonDocument.ParseValue(ref reader);
        try {
            var wire = document.RootElement.Deserialize<AdmissionWire>(Options)
                ?? throw new JsonException("The Process root admission must be an object.");
            if (wire.SchemaVersion != ProcessDecisionRunAdmission.SchemaVersion || wire.Capabilities is null ||
                wire.EnclosingBudgets.IsDefault || wire.EnclosingBudgets.Any(item => item is null)) {
                throw new JsonException("The Process root admission requires its supported schema and original limits.");
            }
            ProcessDecisionRunAdmission result = new(new(wire.RunId), new(wire.BudgetId), wire.DatabaseProfileId,
                wire.WorkspaceNamespaceId, wire.WorkspaceHeadRevision, new(wire.DefinitionVersionId), wire.WorkspacePolicy,
                wire.DefinitionOverride, wire.AuthorizedInvocationOverride, wire.Capabilities.ToModel(),
                [.. wire.EnclosingBudgets.Select(item => item.ToModel())], wire.Policy);
            if (document.RootElement.GetRawText() != Encoding.UTF8.GetString(Encode(result))) {
                throw new JsonException("The retained Process root admission is not its canonical original encoding.");
            }
            return result;
        } catch (Exception failure) when (failure is ArgumentException or InvalidDataException or OverflowException) {
            throw new JsonException("The saved Process root admission is invalid.", failure);
        }
    }

    public override void Write(Utf8JsonWriter writer, ProcessDecisionRunAdmission value, JsonSerializerOptions options)
        => writer.WriteRawValue(Encode(value));

    internal static byte[] Encode(ProcessDecisionRunAdmission value) {
        ArgumentNullException.ThrowIfNull(value);
        value.Validate();
        AdmissionWire wire = new(ProcessDecisionRunAdmission.SchemaVersion, value.RunId.Value, value.BudgetId.Value,
            value.DatabaseProfileId, value.WorkspaceNamespaceId, value.WorkspaceHeadRevision, value.DefinitionVersionId.Value,
            value.WorkspacePolicy, value.DefinitionOverride, value.AuthorizedInvocationOverride,
            CapabilitiesWire.From(value.OwnerCapabilities), [.. value.EnclosingBudgets.Select(BudgetWire.From)], value.Policy);
        var bytes = JsonSerializer.SerializeToUtf8Bytes(wire, Options);
        if (bytes.Length > ProcessDecisionRunAdmission.MaximumUtf8Bytes) {
            throw new JsonException("The Process root admission exceeds its encoding bound.");
        }
        return bytes;
    }

    private sealed record AdmissionWire(
        [property: JsonRequired] int SchemaVersion,
        [property: JsonRequired] Guid RunId,
        [property: JsonRequired] Guid BudgetId,
        [property: JsonRequired] Guid DatabaseProfileId,
        [property: JsonRequired] Guid WorkspaceNamespaceId,
        [property: JsonRequired] Guid WorkspaceHeadRevision,
        [property: JsonRequired] Guid DefinitionVersionId,
        [property: JsonRequired] DecisionDataPolicy WorkspacePolicy,
        [property: JsonRequired] DecisionDataPolicyOverride? DefinitionOverride,
        [property: JsonRequired] DecisionDataPolicyOverride? AuthorizedInvocationOverride,
        [property: JsonRequired] CapabilitiesWire Capabilities,
        [property: JsonRequired] ImmutableArray<BudgetWire> EnclosingBudgets,
        [property: JsonRequired] DecisionDataPolicy Policy);
}
