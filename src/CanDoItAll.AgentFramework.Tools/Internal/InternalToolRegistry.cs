using CanDoItAll.AgentFramework.Capabilities.Abstractions;
using CanDoItAll.AgentFramework.Tools.Abstractions;

namespace CanDoItAll.AgentFramework.Tools;

public sealed class InternalToolRegistry : IInternalToolRegistry
{
    private readonly Dictionary<ImplementationKey, IInternalTool> tools = [];

    public void Register(IInternalTool tool)
    {
        ArgumentNullException.ThrowIfNull(tool);

        if (!tools.TryAdd(tool.Descriptor.ImplementationKey, tool))
        {
            throw new InvalidOperationException($"Internal tool implementation key '{tool.Descriptor.ImplementationKey}' is already registered.");
        }
    }

    public IInternalTool Resolve(ImplementationKey implementationKey)
    {
        if (tools.TryGetValue(implementationKey, out var tool))
        {
            return tool;
        }

        throw new KeyNotFoundException($"Internal tool implementation key '{implementationKey}' is not registered.");
    }

    public IReadOnlyList<IInternalTool> List()
        => tools.Values
            .OrderBy(tool => tool.Descriptor.ImplementationKey.Value, StringComparer.Ordinal)
            .ToArray();
}

public sealed class DelegateInternalTool(
    InternalToolDescriptor descriptor,
    Func<ToolInvocationRequest, ToolInvocationResult> invoke) : IInternalTool
{
    public InternalToolDescriptor Descriptor { get; } = descriptor;

    public ValueTask<ToolInvocationResult> InvokeAsync(
        ToolInvocationRequest request,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return ValueTask.FromResult(invoke(request));
    }
}
