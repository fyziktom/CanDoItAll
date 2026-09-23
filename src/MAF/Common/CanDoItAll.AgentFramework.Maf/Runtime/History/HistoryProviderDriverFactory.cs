using System.Collections.Concurrent;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.AgentFramework.ProviderHistory;
using CanDoItAll.AgentFramework.Providers;

namespace CanDoItAll.AgentFramework.Maf;

internal sealed class HistoryProviderDriverFactory(IAgentProviderFactory inner,
    IProviderHistoryRecorder recorder, TimeProvider clock) : IAgentProviderFactory {
    private readonly ConcurrentDictionary<(ProviderKind, Type), IAgentProviderDriver> drivers = new();

    public IReadOnlyList<ProviderCapabilityDescriptor> ListCapabilities(ProviderKind kind) => inner.ListCapabilities(kind);
    public bool Supports(ProviderKind kind, AgentProviderCapabilityKind capability) => inner.Supports(kind, capability);
    public ProviderDispatchLimits GetDispatchLimits(ProviderDispatchQuery query) => inner.GetDispatchLimits(query);

    public TDriver Resolve<TDriver>(ProviderKind kind) where TDriver : class, IAgentProviderDriver {
        var driver = inner.Resolve<TDriver>(kind);
        return (TDriver)drivers.GetOrAdd((kind, typeof(TDriver)), _ => Decorate(driver));
    }

    public bool TryResolve<TDriver>(ProviderKind kind, out TDriver driver) where TDriver : class, IAgentProviderDriver {
        if (!inner.TryResolve<TDriver>(kind, out var source)) {
            driver = null!;
            return false;
        }
        driver = (TDriver)drivers.GetOrAdd((kind, typeof(TDriver)), _ => Decorate(source));
        return true;
    }

    private IAgentProviderDriver Decorate<TDriver>(TDriver driver) where TDriver : class, IAgentProviderDriver =>
        typeof(TDriver) switch {
            var type when type == typeof(IProviderChatCompletionDriver) => new HistoryChatDriver((IProviderChatCompletionDriver)driver, recorder, clock),
            var type when type == typeof(IProviderImageGenerationDriver) => new HistoryImageDriver((IProviderImageGenerationDriver)driver, recorder, clock),
            var type when type == typeof(IProviderSpeechToTextDriver) => new HistoryTranscriptionDriver((IProviderSpeechToTextDriver)driver, recorder, clock),
            var type when type == typeof(IProviderTextToSpeechDriver) => new HistorySynthesisDriver((IProviderTextToSpeechDriver)driver, recorder, clock),
            var type when type == typeof(IProviderModelCatalogDriver) => new HistoryCatalogDriver((IProviderModelCatalogDriver)driver, recorder, clock),
            var type when type == typeof(IProviderModelMaintenanceDriver) => new HistoryModelMaintenanceDriver((IProviderModelMaintenanceDriver)driver, recorder, clock),
            var type when type == typeof(IProviderStreamingChatCompletionDriver) =>
                new HistoryStreamingChatDriver((IProviderStreamingChatCompletionDriver)driver, recorder, clock),
            var type when type == typeof(IProviderHealthDriver) => new HistoryHealthDriver((IProviderHealthDriver)driver, recorder, clock),
            var type when type == typeof(IProviderInferenceRelayDriver) => driver,
            _ => throw new InvalidOperationException("The provider capability has no history adapter.")
        };
}
