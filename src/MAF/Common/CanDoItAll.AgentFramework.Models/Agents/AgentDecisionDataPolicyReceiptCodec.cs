using System.Collections.Immutable;
using System.Text.Json;
using System.Text.Json.Serialization;
using CanDoItAll.SharedKernel;

namespace CanDoItAll.AgentFramework.Models;

internal sealed class AgentDecisionDataPolicyReceiptJsonConverter : JsonConverter<AgentDecisionDataPolicyReceipt> {
    private static readonly JsonSerializerOptions Options = new() {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
        MaxDepth = 8
    };
    public override bool HandleNull => true;

    public override AgentDecisionDataPolicyReceipt Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) {
        var check = reader;
        if (reader.TokenType != JsonTokenType.StartObject || !check.TrySkip() ||
            check.BytesConsumed - reader.TokenStartIndex > AgentDecisionDataPolicyReceipt.MaximumUtf8Bytes) {
            throw new JsonException("An Agent policy receipt requires a bounded object.");
        }
        using var document = JsonDocument.ParseValue(ref reader);
        var names = new HashSet<string>(StringComparer.Ordinal);
        foreach (var property in document.RootElement.EnumerateObject()) {
            if (!names.Add(property.Name)) {
                throw new JsonException("An Agent policy receipt cannot repeat a field.");
            }
        }
        try {
            var wire = document.RootElement.Deserialize<ReceiptWire>(Options)!;
            if (wire.SchemaVersion != AgentDecisionDataPolicyReceipt.SchemaVersion || !Enum.IsDefined(wire.ScopeKind) || wire.ScopeKey is null) {
                throw new JsonException("The Agent policy receipt schema or scope is invalid.");
            }
            var scope = new WorkspaceScopeDescriptor(wire.ScopeKind, wire.ScopeKey);
            if (scope.Key != wire.ScopeKey) {
                throw new JsonException("The Agent policy receipt scope must be canonical.");
            }
            var workspace = new AgentExecutionActivityWorkspaceIdentity(wire.DatabaseProfileId, scope, new(wire.DatabaseProfileGeneration));
            return new(new(wire.OperationId, new(workspace, wire.AgentId, new(wire.CatalogRevision)),
                DecisionDataPolicyOverrideCodec.Deserialize(wire.Policy.GetRawText())), wire.AcceptedAtUtc);
        } catch (Exception failure) when (failure is ArgumentException or OverflowException or DecisionDataPolicyException) {
            throw new JsonException("The Agent policy receipt is invalid.", failure);
        }
    }

    public override void Write(Utf8JsonWriter writer, AgentDecisionDataPolicyReceipt value, JsonSerializerOptions options) {
        ArgumentNullException.ThrowIfNull(value);
        var command = value.Command;
        var workspace = command.Expectation.Workspace;
        using var policy = JsonDocument.Parse(DecisionDataPolicyOverrideCodec.Serialize(command.Policy));
        var wire = new ReceiptWire(AgentDecisionDataPolicyReceipt.SchemaVersion, command.OperationId, workspace.DatabaseProfileId,
            workspace.DatabaseProfileGeneration.Value, workspace.WorkspaceScope.Kind, workspace.WorkspaceScope.Key,
            command.Expectation.AgentId, command.Expectation.CatalogRevision.Value, policy.RootElement, value.AcceptedAtUtc);
        var bytes = JsonSerializer.SerializeToUtf8Bytes(wire, Options);
        if (bytes.Length > AgentDecisionDataPolicyReceipt.MaximumUtf8Bytes) {
            throw new JsonException("The Agent policy receipt exceeds its encoding bound.");
        }
        writer.WriteRawValue(bytes);
    }

    private sealed record ReceiptWire(
        [property: JsonRequired] int SchemaVersion,
        [property: JsonRequired] Guid OperationId,
        [property: JsonRequired] Guid DatabaseProfileId,
        [property: JsonRequired] long DatabaseProfileGeneration,
        [property: JsonRequired] WorkspaceScopeKind ScopeKind,
        [property: JsonRequired] string ScopeKey,
        [property: JsonRequired] Guid AgentId,
        [property: JsonRequired] long CatalogRevision,
        [property: JsonRequired] JsonElement Policy,
        [property: JsonRequired] DateTimeOffset AcceptedAtUtc);
}

internal sealed class AgentDecisionDataPolicyReceiptLogJsonConverter : JsonConverter<AgentDecisionDataPolicyReceiptLog> {
    private const int MaximumUtf8Bytes = AgentDecisionDataPolicyReceiptLog.MaximumReceipts * (AgentDecisionDataPolicyReceipt.MaximumUtf8Bytes + 1) + 2;
    public override bool HandleNull => true;

    public override AgentDecisionDataPolicyReceiptLog Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) {
        if (reader.TokenType != JsonTokenType.StartArray) {
            throw new JsonException("The Agent policy receipt log must be an array.");
        }
        var start = reader.TokenStartIndex;
        var receipts = ImmutableArray.CreateBuilder<AgentDecisionDataPolicyReceipt>();
        while (reader.Read() && reader.TokenType != JsonTokenType.EndArray) {
            if (receipts.Count == AgentDecisionDataPolicyReceiptLog.MaximumReceipts || reader.BytesConsumed - start > MaximumUtf8Bytes) {
                throw new JsonException("The Agent policy receipt log exceeds its bound.");
            }
            receipts.Add(JsonSerializer.Deserialize<AgentDecisionDataPolicyReceipt>(ref reader, options)!);
        }
        if (reader.TokenType != JsonTokenType.EndArray || reader.BytesConsumed - start > MaximumUtf8Bytes) {
            throw new JsonException("The Agent policy receipt log is incomplete or exceeds its bound.");
        }
        try {
            return new(receipts.ToImmutable());
        } catch (ArgumentException failure) {
            throw new JsonException("The Agent policy receipt log is invalid.", failure);
        }
    }

    public override void Write(Utf8JsonWriter writer, AgentDecisionDataPolicyReceiptLog value, JsonSerializerOptions options) {
        ArgumentNullException.ThrowIfNull(value);
        writer.WriteStartArray();
        foreach (var receipt in value.Receipts) {
            JsonSerializer.Serialize(writer, receipt, options);
        }
        writer.WriteEndArray();
    }
}
