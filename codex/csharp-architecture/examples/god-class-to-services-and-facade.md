# Example: God Class to Services and Thin Facade

## Starting problem

A runtime class owns:

- provider selection
- tool metadata construction
- tool execution
- memory provider creation
- retry policy
- metrics
- persistence mapping
- composition registration

Splitting this into `Runtime.Tools.cs`, `Runtime.Memory.cs`, and `Runtime.Persistence.cs` does not solve the problem because the same type still owns every reason to change.

## Target shape

```text
Runtime
  -> IToolCatalog
  -> IToolExecutor
  -> IMemoryProviderFactory
  -> IRuntimeEventSink
  -> IRetryPolicy

Tools.Abstractions
Tools.Standard
Memory.Abstractions
Memory.ProviderX
Runtime.Events
Composition
```

## Refactoring sequence

1. Add characterization tests around one behavior slice.
2. Extract `IToolCatalog` contract.
3. Create `StandardToolCatalog` implementation.
4. Add tests for tool descriptor generation.
5. Register catalog in composition root.
6. Replace old runtime tool descriptor method with delegation.
7. Delete old descriptor-building code.
8. Add source assertion that runtime no longer contains concrete tool descriptors.
9. Repeat for execution, memory provider factory, and events.

## Thin facade rule

The runtime may remain as a facade only if it delegates to cohesive services and does not own provider-specific branches.

Good facade:

```csharp
public sealed class AgentRuntime
{
    private readonly IToolCatalog toolCatalog;
    private readonly IToolExecutor toolExecutor;

    public AgentRuntime(IToolCatalog toolCatalog, IToolExecutor toolExecutor)
    {
        this.toolCatalog = toolCatalog;
        this.toolExecutor = toolExecutor;
    }

    public IReadOnlyList<ToolDescriptor> GetTools(RuntimeContext context)
    {
        return toolCatalog.GetTools(context);
    }

    public Task<ToolExecutionResult> ExecuteToolAsync(ToolExecutionRequest request, CancellationToken cancellationToken)
    {
        return toolExecutor.ExecuteAsync(request, cancellationToken);
    }
}
```

Bad facade:

```csharp
public sealed class AgentRuntime
{
    public IReadOnlyList<ToolDescriptor> GetTools(RuntimeContext context)
    {
        return new []
        {
            BuildFileTool(),
            BuildMemoryTool(),
            BuildMcpTool(),
            BuildDocumentTool()
        };
    }
}
```

## Proof

- old runtime has fewer methods and no provider-specific descriptor construction
- tool catalog tests instantiate `StandardToolCatalog` directly
- runtime composition smoke proves delegation
- no new partial class was added
