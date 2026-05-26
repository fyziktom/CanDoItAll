using System.Text.Json;
using System.Text.Json.Serialization;

namespace CanDoItAll.Modules.Processes;

internal static class ProcessStepRunBlockState
{
    private static readonly JsonSerializerOptions SerializerOptions = CreateSerializerOptions();

    public static void Clear(ProcessStepRun stepRun)
    {
        ArgumentNullException.ThrowIfNull(stepRun);

        stepRun.BlockReasonCode = ProcessStepBlockReasonCode.None;
        stepRun.RecoveryOptionsJson = "[]";
        stepRun.NextRecoveryAction = ProcessStepRecoveryOption.None;
    }

    public static ProcessRecoveryRoutingDecision Apply(ProcessStepRun stepRun, string reason, ProcessStepBlockCause? cause = null)
    {
        ArgumentNullException.ThrowIfNull(stepRun);

        var classification = ProcessBlockStateClassifier.Classify(reason, cause);
        var code = classification.ReasonCode;
        var blockCause = classification.BlockCause;
        var recoveryOptions = classification.RecoveryOptions;
        var routingDecision = ProcessRecoveryRouter.Route(new ProcessRecoveryRoutingRequest(
            code,
            blockCause,
            reason,
            recoveryOptions,
            [],
            ProcessRecoveryRouter.BuildEvidenceFingerprint(code, blockCause, reason),
            HasNewEvidence: true));
        stepRun.BlockReasonCode = code;
        stepRun.RecoveryOptionsJson = SerializeRecoveryOptions(recoveryOptions);
        stepRun.NextRecoveryAction = routingDecision.NextAction;
        return routingDecision;
    }

    public static IReadOnlyList<ProcessStepRecoveryOption> ResolveRecoveryOptions(ProcessStepRun stepRun)
    {
        ArgumentNullException.ThrowIfNull(stepRun);

        if (string.IsNullOrWhiteSpace(stepRun.RecoveryOptionsJson))
        {
            return [];
        }

        try
        {
            return JsonSerializer.Deserialize<List<ProcessStepRecoveryOption>>(
                    stepRun.RecoveryOptionsJson,
                    SerializerOptions) ?? [];
        }
        catch (JsonException)
        {
            return [];
        }
    }

    public static bool IsMissingUpstreamArtifactBlock(ProcessStepRun stepRun)
    {
        return ProcessBlockStateClassifier.IsMissingUpstreamArtifactBlock(stepRun);
    }

    internal static ProcessStepBlockReasonCode InferBlockReasonCode(string reason)
    {
        return ProcessBlockStateClassifier.InferBlockReasonCode(reason);
    }

    internal static ProcessStepBlockCause? InferBlockCause(string reason)
    {
        return ProcessBlockStateClassifier.InferBlockCause(reason);
    }

    internal static ProcessStepBlockReasonCode ResolveBlockReasonCode(ProcessStepBlockCause cause)
    {
        return ProcessBlockStateClassifier.ResolveBlockReasonCode(cause);
    }

    internal static IReadOnlyList<ProcessStepRecoveryOption> ResolveRecoveryOptions(ProcessStepBlockReasonCode code)
    {
        return ProcessBlockStateClassifier.ResolveRecoveryOptions(code);
    }

    private static string SerializeRecoveryOptions(IReadOnlyList<ProcessStepRecoveryOption> options)
    {
        return JsonSerializer.Serialize(options, SerializerOptions);
    }

    private static JsonSerializerOptions CreateSerializerOptions()
    {
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        options.Converters.Add(new JsonStringEnumConverter());
        return options;
    }
}
