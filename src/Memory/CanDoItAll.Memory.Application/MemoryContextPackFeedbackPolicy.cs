using CanDoItAll.Memory.Abstractions;

namespace CanDoItAll.Memory.Application;

internal static class MemoryContextPackFeedbackPolicy
{
    public static MemoryContextPack Normalize(
        MemoryContextPack contextPack,
        MemoryOperationId operationId,
        MemoryProviderProfile provider,
        bool feedbackDeliveryAvailable)
    {
        if (!feedbackDeliveryAvailable || !SupportsFeedback(provider))
        {
            return contextPack with
            {
                FeedbackHandle = null
            };
        }

        return contextPack with
        {
            FeedbackHandle = MemoryFeedbackHandle.Parse(
                $"memory-feedback:{operationId.Value:D}:{contextPack.ContextPackId.Value:D}")
        };
    }

    private static bool SupportsFeedback(MemoryProviderProfile provider) =>
        provider.Manifest.InteractionSupport.SupportsFeedback &&
        provider.Manifest.Capabilities.Any(capability =>
            capability.Supported &&
            (capability.Id == MemoryCapabilityIds.FeedbackImmediate ||
             capability.Id == MemoryCapabilityIds.FeedbackDelayed));
}
