using System.Text.Json;
using System.Text.Json.Serialization;

namespace CanDoItAll.Modules.CognitiveMemory;

[JsonSourceGenerationOptions(
    GenerationMode = JsonSourceGenerationMode.Metadata,
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    UseStringEnumConverter = true,
    WriteIndented = false)]
[JsonSerializable(typeof(CognitiveMemoryDurablePayloadEnvelope))]
[JsonSerializable(typeof(CognitiveMemoryPageRequest))]
[JsonSerializable(typeof(CognitiveMemoryProcessingBudget))]
[JsonSerializable(typeof(CognitiveMemoryBudgetDecision))]
[JsonSerializable(typeof(CognitiveMemoryScoreEvaluationTrace))]
[JsonSerializable(typeof(CognitiveMemoryScoreVectorSnapshot))]
[JsonSerializable(typeof(CognitiveMemoryScoreComponent))]
[JsonSerializable(typeof(CognitiveMemoryScoreShapeSnapshot))]
[JsonSerializable(typeof(CognitiveMemoryAttentionRoutingDecision))]
[JsonSerializable(typeof(CognitiveMemoryRecallTracePayload))]
[JsonSerializable(typeof(CognitiveMemoryRecallTraceStage))]
[JsonSerializable(typeof(CognitiveMemoryRecallContextPack))]
[JsonSerializable(typeof(CognitiveMemoryRecallContextSection))]
[JsonSerializable(typeof(CognitiveMemoryRecallSourceRef))]
[JsonSerializable(typeof(CognitiveMemoryConsolidationCandidatePayload))]
[JsonSerializable(typeof(CognitiveMemoryConsolidationReportPayload))]
[JsonSerializable(typeof(CognitiveMemoryDreamValidationIssue))]
[JsonSerializable(typeof(CognitiveMemoryDreamValidationIssue[]))]
[JsonSerializable(typeof(CognitiveMemoryQualityDiagnosticWarning))]
[JsonSerializable(typeof(CognitiveMemoryQualityDiagnosticWarning[]))]
[JsonSerializable(typeof(CognitiveMemoryEvidenceAnchorQuery))]
[JsonSerializable(typeof(CognitiveMemoryClaimQuery))]
[JsonSerializable(typeof(CognitiveMemoryContextFrameQuery))]
[JsonSerializable(typeof(CognitiveMemoryMutationAuditQuery))]
[JsonSerializable(typeof(CognitiveMemoryClaimProjectionPayload))]
[JsonSerializable(typeof(CognitiveMemoryProjectionPayloadValidationResult))]
[JsonSerializable(typeof(CognitiveMemorySourceItemProvenancePayload))]
[JsonSerializable(typeof(CognitiveMemorySourceReferencePayload))]
[JsonSerializable(typeof(CognitiveMemorySourceLinkPayload))]
[JsonSerializable(typeof(CognitiveMemoryExecutionModelId))]
[JsonSerializable(typeof(CognitiveMemoryModelExecutionProfile[]))]
[JsonSerializable(typeof(Dictionary<string, string>))]
[JsonSerializable(typeof(Guid[]))]
[JsonSerializable(typeof(string[]))]
public sealed partial class CognitiveMemoryJsonSerializerContext : JsonSerializerContext;

public sealed record CognitiveMemoryDurablePayloadEnvelope(
    CognitiveMemoryPayloadSchemaVersion SchemaVersion,
    CognitiveMemoryDurablePayloadKind PayloadKind,
    string PayloadJson,
    Dictionary<string, string> Metadata);

public static class CognitiveMemoryJson
{
    public static readonly JsonSerializerOptions SerializerOptions =
        new(CognitiveMemoryJsonSerializerContext.Default.Options);

    public static string SerializeEnvelope(CognitiveMemoryDurablePayloadEnvelope envelope)
    {
        ArgumentNullException.ThrowIfNull(envelope);
        return JsonSerializer.Serialize(
            envelope,
            CognitiveMemoryJsonSerializerContext.Default.CognitiveMemoryDurablePayloadEnvelope);
    }

    public static CognitiveMemoryDurablePayloadEnvelope DeserializeEnvelope(string json)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(json);
        return JsonSerializer.Deserialize(
            json,
            CognitiveMemoryJsonSerializerContext.Default.CognitiveMemoryDurablePayloadEnvelope)
            ?? throw new JsonException("Cognitive memory durable payload envelope was empty.");
    }
}
