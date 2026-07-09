using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using CanDoItAll.Processes.Abstractions;
using CanDoItAll.Processes.Contracts;

namespace CanDoItAll.Processes.Projections;

public sealed class ProcessProjectionJsonCodec
{
    public static ProcessProjectionJsonCodec Default { get; } = new();

    private static readonly ProcessProjectionJsonContext JsonContext = new(CreateSerializerOptions());

    private ProcessProjectionJsonCodec()
    {
    }

    public ProcessProjectionSnapshot CreateSnapshot<TProjection>(
        ProcessProjectorName projectorName,
        ProcessProjectionKey projectionKey,
        TProjection projection,
        DateTimeOffset updatedAtUtc)
    {
        var payloadJson = Serialize(projection);
        return new ProcessProjectionSnapshot(
            projectorName,
            projectionKey,
            ProcessContractVersions.RuntimeProjectionV1,
            payloadJson,
            ComputeHash(payloadJson),
            updatedAtUtc);
    }

    public ProcessProjectionHistoryRecord CreateHistoryRecord<TProjection>(
        ProcessProjectorName projectorName,
        ProcessProjectionKey projectionKey,
        ProcessStoredRuntimeEvent sourceEvent,
        TProjection projection)
    {
        var payloadJson = Serialize(projection);
        return new ProcessProjectionHistoryRecord(
            projectorName,
            projectionKey,
            sourceEvent.GlobalSequence,
            sourceEvent.Envelope.RootRunId,
            sourceEvent.Envelope.RunId,
            sourceEvent.Envelope.OccurredAtUtc,
            sourceEvent.Envelope.EventType.Value,
            ProcessContractVersions.RuntimeProjectionV1,
            payloadJson,
            ComputeHash(payloadJson),
            sourceEvent.Envelope.Sensitivity.ToString());
    }

    public TProjection ReadSnapshot<TProjection>(ProcessProjectionSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        return Deserialize<TProjection>(snapshot.PayloadJson);
    }

    public TProjection ReadHistory<TProjection>(ProcessProjectionHistoryRecord record)
    {
        ArgumentNullException.ThrowIfNull(record);
        return Deserialize<TProjection>(record.PayloadJson);
    }

    private static string Serialize<TProjection>(TProjection projection)
    {
        return projection switch
        {
            ProcessLiveProcessSnapshot value => JsonSerializer.Serialize(value, JsonContext.ProcessLiveProcessSnapshot),
            ProcessRunDetailProjection value => JsonSerializer.Serialize(value, JsonContext.ProcessRunDetailProjection),
            ProcessTimelineEventProjection value => JsonSerializer.Serialize(value, JsonContext.ProcessTimelineEventProjection),
            ProcessRuntimeCanvasProjection value => JsonSerializer.Serialize(value, JsonContext.ProcessRuntimeCanvasProjection),
            ProcessDefinitionCanvasProjection value => JsonSerializer.Serialize(value, JsonContext.ProcessDefinitionCanvasProjection),
            ProcessArtifactMapProjection value => JsonSerializer.Serialize(value, JsonContext.ProcessArtifactMapProjection),
            ProcessIncidentProjection value => JsonSerializer.Serialize(value, JsonContext.ProcessIncidentProjection),
            ProcessManagerMessageProjection value => JsonSerializer.Serialize(value, JsonContext.ProcessManagerMessageProjection),
            LegacyProcessHistoryInventoryReport value => JsonSerializer.Serialize(value, JsonContext.LegacyProcessHistoryInventoryReport),
            LegacyProcessRunProjection value => JsonSerializer.Serialize(value, JsonContext.LegacyProcessRunProjection),
            LegacyProcessHistoryActionDenial value => JsonSerializer.Serialize(value, JsonContext.LegacyProcessHistoryActionDenial),
            _ => throw new NotSupportedException($"Projection payload type '{typeof(TProjection).FullName}' is not supported.")
        };
    }

    private static TProjection Deserialize<TProjection>(string payloadJson)
    {
        object? result =
            typeof(TProjection) == typeof(ProcessLiveProcessSnapshot)
                ? JsonSerializer.Deserialize(payloadJson, JsonContext.ProcessLiveProcessSnapshot)
                : typeof(TProjection) == typeof(ProcessRunDetailProjection)
                    ? JsonSerializer.Deserialize(payloadJson, JsonContext.ProcessRunDetailProjection)
                    : typeof(TProjection) == typeof(ProcessTimelineEventProjection)
                        ? JsonSerializer.Deserialize(payloadJson, JsonContext.ProcessTimelineEventProjection)
                        : typeof(TProjection) == typeof(ProcessRuntimeCanvasProjection)
                            ? JsonSerializer.Deserialize(payloadJson, JsonContext.ProcessRuntimeCanvasProjection)
                            : typeof(TProjection) == typeof(ProcessDefinitionCanvasProjection)
                                ? JsonSerializer.Deserialize(payloadJson, JsonContext.ProcessDefinitionCanvasProjection)
                                : typeof(TProjection) == typeof(ProcessArtifactMapProjection)
                                    ? JsonSerializer.Deserialize(payloadJson, JsonContext.ProcessArtifactMapProjection)
                                    : typeof(TProjection) == typeof(ProcessIncidentProjection)
                                        ? JsonSerializer.Deserialize(payloadJson, JsonContext.ProcessIncidentProjection)
                                        : typeof(TProjection) == typeof(ProcessManagerMessageProjection)
                                        ? JsonSerializer.Deserialize(payloadJson, JsonContext.ProcessManagerMessageProjection)
                                        : typeof(TProjection) == typeof(LegacyProcessHistoryInventoryReport)
                                            ? JsonSerializer.Deserialize(payloadJson, JsonContext.LegacyProcessHistoryInventoryReport)
                                            : typeof(TProjection) == typeof(LegacyProcessRunProjection)
                                                ? JsonSerializer.Deserialize(payloadJson, JsonContext.LegacyProcessRunProjection)
                                                : typeof(TProjection) == typeof(LegacyProcessHistoryActionDenial)
                                                    ? JsonSerializer.Deserialize(payloadJson, JsonContext.LegacyProcessHistoryActionDenial)
                                                    : throw new NotSupportedException($"Projection payload type '{typeof(TProjection).FullName}' is not supported.");

        return result is TProjection projection
            ? projection
            : throw new InvalidOperationException($"Projection payload type '{typeof(TProjection).FullName}' could not be deserialized.");
    }

    private static string ComputeHash(string payloadJson)
    {
        var bytes = Encoding.UTF8.GetBytes(payloadJson);
        return "sha256:" + Convert.ToHexString(SHA256.HashData(bytes));
    }

    private static JsonSerializerOptions CreateSerializerOptions()
    {
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        options.Converters.Add(new ProcessRunIdJsonConverter());
        options.Converters.Add(new RuntimeEventIdJsonConverter());
        options.Converters.Add(new ArtifactSlotIdJsonConverter());
        options.Converters.Add(new LegacyProcessRunIdJsonConverter());
        return options;
    }
}

internal sealed class ProcessRunIdJsonConverter : JsonConverter<ProcessRunId>
{
    public override ProcessRunId Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        return new ProcessRunId(Guid.Parse(reader.GetString() ?? throw new JsonException("ProcessRunId value is missing.")));
    }

    public override void Write(Utf8JsonWriter writer, ProcessRunId value, JsonSerializerOptions options)
    {
        writer.WriteStringValue(value.ToString());
    }
}

internal sealed class RuntimeEventIdJsonConverter : JsonConverter<RuntimeEventId>
{
    public override RuntimeEventId Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        return new RuntimeEventId(Guid.Parse(reader.GetString() ?? throw new JsonException("RuntimeEventId value is missing.")));
    }

    public override void Write(Utf8JsonWriter writer, RuntimeEventId value, JsonSerializerOptions options)
    {
        writer.WriteStringValue(value.ToString());
    }
}

internal sealed class ArtifactSlotIdJsonConverter : JsonConverter<ArtifactSlotId>
{
    public override ArtifactSlotId Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        return new ArtifactSlotId(Guid.Parse(reader.GetString() ?? throw new JsonException("ArtifactSlotId value is missing.")));
    }

    public override void Write(Utf8JsonWriter writer, ArtifactSlotId value, JsonSerializerOptions options)
    {
        writer.WriteStringValue(value.ToString());
    }
}

internal sealed class LegacyProcessRunIdJsonConverter : JsonConverter<LegacyProcessRunId>
{
    public override LegacyProcessRunId Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        return new LegacyProcessRunId(reader.GetString() ?? throw new JsonException("LegacyProcessRunId value is missing."));
    }

    public override void Write(Utf8JsonWriter writer, LegacyProcessRunId value, JsonSerializerOptions options)
    {
        writer.WriteStringValue(value.ToString());
    }
}

[JsonSerializable(typeof(ProcessLiveProcessSnapshot))]
[JsonSerializable(typeof(ProcessRuntimeCurrentStepProjection))]
[JsonSerializable(typeof(ProcessRuntimeDiagnosticProjection))]
[JsonSerializable(typeof(ProcessRuntimeOperatorDiagnosticDetailsProjection))]
[JsonSerializable(typeof(ProcessRuntimeArtifactLineageProjection))]
[JsonSerializable(typeof(ProcessRuntimeRecoveryDecisionProjection))]
[JsonSerializable(typeof(ProcessRuntimeResultLineageProjection))]
[JsonSerializable(typeof(ProcessRuntimeOperatorActionProjection))]
[JsonSerializable(typeof(ProcessRuntimeChildRunWaitProjection))]
[JsonSerializable(typeof(ProcessRunDetailProjection))]
[JsonSerializable(typeof(ProcessTimelineEventProjection))]
[JsonSerializable(typeof(ProcessRuntimeCanvasProjection))]
[JsonSerializable(typeof(ProcessDefinitionCanvasProjection))]
[JsonSerializable(typeof(ProcessArtifactMapProjection))]
[JsonSerializable(typeof(ProcessIncidentProjection))]
[JsonSerializable(typeof(ProcessManagerMessageProjection))]
[JsonSerializable(typeof(LegacyProcessHistoryInventoryReport))]
[JsonSerializable(typeof(LegacyProcessRunProjection))]
[JsonSerializable(typeof(LegacyProcessHistoryActionDenial))]
internal sealed partial class ProcessProjectionJsonContext : JsonSerializerContext;
