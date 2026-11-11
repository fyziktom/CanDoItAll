using Microsoft.Extensions.DependencyInjection;

namespace CanDoItAll.Modules.AgentFramework;

public interface IAgentChatExecutionScope : IAsyncDisposable {
    IAgentChatOperationExecutor Executor { get; }
}

public interface IAgentChatExecutionScopeFactory {
    ValueTask<IAgentChatExecutionScope> CreateAsync();
}

public sealed class AgentChatExecutionScopeFactory(IServiceScopeFactory scopes) : IAgentChatExecutionScopeFactory {
    public async ValueTask<IAgentChatExecutionScope> CreateAsync() {
        var scope = scopes.CreateAsyncScope();
        try {
            return new AgentChatExecutionScope(scope, scope.ServiceProvider.GetRequiredService<IAgentChatOperationExecutor>());
        } catch (Exception failure) {
            try {
                await scope.DisposeAsync().ConfigureAwait(false);
            } catch (Exception cleanup) {
                throw new AggregateException("Agent execution scope construction and cleanup failed.", failure, cleanup);
            }
            throw;
        }
    }
}

internal sealed class AgentChatExecutionScope(AsyncServiceScope scope, IAgentChatOperationExecutor executor) : IAgentChatExecutionScope {
    public IAgentChatOperationExecutor Executor { get; } = executor;

    public ValueTask DisposeAsync() => scope.DisposeAsync();
}
