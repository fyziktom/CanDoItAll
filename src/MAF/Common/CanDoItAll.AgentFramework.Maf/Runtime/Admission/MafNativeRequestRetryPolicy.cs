using System.ClientModel.Primitives;

namespace CanDoItAll.AgentFramework.Maf;

internal sealed class MafNativeRequestRetryPolicy : ClientRetryPolicy {
    protected override bool ShouldRetry(PipelineMessage message, Exception? exception)
        => MafToolRunContext.Current?.HasActiveProviderDispatch != true && base.ShouldRetry(message, exception);

    protected override ValueTask<bool> ShouldRetryAsync(PipelineMessage message, Exception? exception)
        => MafToolRunContext.Current?.HasActiveProviderDispatch == true
            ? ValueTask.FromResult(false)
            : base.ShouldRetryAsync(message, exception);
}
