using CanDoItAll.AgentFramework.Models;

namespace CanDoItAll.AgentFramework.Core;

internal static class RequiredFinalizerStructuredOutputRecoveryPolicy
{
    internal static bool CanRecover(
        bool recoveryEnabled,
        AgentFinalizerMode finalizerMode,
        AgentFinalizerValidationResult validation)
    {
        ArgumentNullException.ThrowIfNull(validation);

        return recoveryEnabled &&
               finalizerMode == AgentFinalizerMode.Required &&
               !validation.Succeeded &&
               validation.MatchingInvocationCount == 0 &&
               validation.Errors.Count == 1 &&
               string.Equals(
                   validation.Errors[0].Code,
                   "agent.finalizer.missing",
                   StringComparison.Ordinal);
    }
}
