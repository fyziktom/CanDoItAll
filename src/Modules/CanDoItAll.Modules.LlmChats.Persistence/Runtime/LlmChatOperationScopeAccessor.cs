using CanDoItAll.Modules.LlmChats.Ports;

namespace CanDoItAll.Modules.LlmChats.Persistence;

public sealed class LlmChatOperationScopeAccessor : ILlmChatOperationScopeAccessor
{
    private readonly AsyncLocal<Scope?> current = new();

    public LlmChatOperationExecutionContext? Current => current.Value?.Context;

    public IDisposable Push(LlmChatOperationExecutionContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        var previous = current.Value;
        var scope = new Scope(this, context, previous);
        current.Value = scope;
        return scope;
    }

    private sealed class Scope(
        LlmChatOperationScopeAccessor owner,
        LlmChatOperationExecutionContext context,
        Scope? previous) : IDisposable
    {
        private int disposed;

        public LlmChatOperationExecutionContext Context { get; } = context;

        public void Dispose()
        {
            if (Interlocked.Exchange(ref disposed, 1) != 0)
            {
                return;
            }

            if (ReferenceEquals(owner.current.Value, this))
            {
                owner.current.Value = previous;
            }
        }
    }
}
