# Example: MAF Runtime Capability Extraction

This is a repository-shaped example for a runtime capability file that concentrates skills, tools, plugins, MCP, RAG, memory, and compaction assembly.

## Smell

A single runtime capability partial file owns:

- skill discovery
- tool registration
- plugin registration
- MCP connector assembly
- memory provider assembly
- RAG dependency wiring
- compaction configuration

This creates a change hotspot. Adding one tool or provider requires editing the runtime.

## Target ownership

| Responsibility | Target owner |
|---|---|
| capability contracts | Capabilities.Abstractions |
| capability access policies | Capabilities.Access |
| skill catalog | Skills implementation project |
| tool catalog | Tools implementation project |
| MCP connector catalog | MCP implementation project |
| memory provider catalog | Memory provider project |
| runtime composition | Composition root extension |
| runtime execution | runtime class, thin orchestration only |

## Extension seam

Create a contribution interface:

```csharp
public interface IRuntimeCapabilityContributor
{
    ValueTask ContributeAsync(RuntimeCapabilityBuilder builder, CancellationToken cancellationToken);
}
```

Each module contributes through its own implementation:

```csharp
public sealed class StandardToolsCapabilityContributor : IRuntimeCapabilityContributor
{
    private readonly IToolCatalog toolCatalog;

    public StandardToolsCapabilityContributor(IToolCatalog toolCatalog)
    {
        this.toolCatalog = toolCatalog;
    }

    public ValueTask ContributeAsync(RuntimeCapabilityBuilder builder, CancellationToken cancellationToken)
    {
        builder.AddTools(toolCatalog.GetToolDescriptors());
        return ValueTask.CompletedTask;
    }
}
```

The runtime depends on `IEnumerable<IRuntimeCapabilityContributor>`, not every concrete provider.

## Test seam

- `RuntimeCapabilityBuilder` validates duplicate tool names.
- `StandardToolsCapabilityContributor` contributes descriptors from a fake catalog.
- runtime assembly test proves all contributors are invoked.
- negative test proves duplicate descriptor collision is rejected.

## Gate

The refactor is not complete if the runtime still constructs every tool/provider descriptor manually.
