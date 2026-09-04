using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;

namespace CanDoItAll.AgentFramework.Maf;

internal static class MafAgentToolFailureMapper
{
    public static bool TryMap(Exception exception, out AgentToolFailureResult result)
    {
        ArgumentNullException.ThrowIfNull(exception);

        if (exception is IAgentToolFailure { IsSafeToExpose: true } failure)
        {
            result = new AgentToolFailureResult(
                Succeeded: false,
                failure.ErrorCode,
                failure.SafeMessage,
                failure.CanRetryWithCorrectedInput)
            {
                EffectState = failure is IAgentToolFailureEffectEvidence effectEvidence
                    ? effectEvidence.EffectState
                    : AgentToolEffectState.Unknown
            };
            return true;
        }

        result = default!;
        return false;
    }
}
