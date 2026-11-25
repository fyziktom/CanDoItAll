using Microsoft.Agents.AI;
using Microsoft.Agents.AI.Compaction;
using Microsoft.Extensions.AI;

namespace CanDoItAll.AgentFramework.Maf;

internal sealed class MafApprovalAwareCompactionProvider(CompactionStrategy strategy) : AIContextProvider {
    private readonly CompactionProvider inner = new(strategy);

    public override IReadOnlyList<string> StateKeys => inner.StateKeys;

    protected override ValueTask<AIContext> InvokingCoreAsync(InvokingContext context, CancellationToken cancellationToken = default) {
        if (context.AIContext.Messages is { } input) {
            var messages = input as IReadOnlyList<ChatMessage> ?? input.ToArray();
            context.AIContext.Messages = messages;
            // A repeated context message can make compaction reuse history that predates this decision.
            if (messages.Any(message => message.GetAgentRequestMessageSourceType() == AgentRequestMessageSourceType.External &&
                    message.Contents.Any(content => content is ToolApprovalResponseContent))) {
                return ValueTask.FromResult(context.AIContext);
            }
        }
        return inner.InvokingAsync(context, cancellationToken);
    }

    protected override ValueTask InvokedCoreAsync(InvokedContext context, CancellationToken cancellationToken = default)
        => inner.InvokedAsync(context, cancellationToken);

    public override object? GetService(Type serviceType, object? serviceKey = null)
        => serviceKey is null && serviceType.IsInstanceOfType(this) ? this : inner.GetService(serviceType, serviceKey);
}
