# Example: Tool Registration Project Extraction

## Problem

Tool definitions are scattered across runtime partial classes. Adding a new tool requires editing the runtime.

## Target projects

```text
CanDoItAll.AgentFramework.Tools.Abstractions
CanDoItAll.AgentFramework.Tools
CanDoItAll.Tools.Documents
CanDoItAll.AgentFramework.Tooling
```

## Target contracts

```csharp
public interface IToolDefinitionProvider
{
    IReadOnlyList<ToolDefinition> GetDefinitions();
}

public interface IToolExecutor
{
    ValueTask<ToolExecutionResult> ExecuteAsync(ToolExecutionRequest request, CancellationToken cancellationToken);
}
```

## Target implementation

Each tool family owns a provider:

```csharp
public sealed class DocumentToolDefinitionProvider : IToolDefinitionProvider
{
    public IReadOnlyList<ToolDefinition> GetDefinitions()
    {
        return
        [
            ToolDefinition.Create("document.extract_text", "Extract text from a document."),
            ToolDefinition.Create("document.summarize", "Summarize a document.")
        ];
    }
}
```

The runtime consumes an aggregate catalog:

```csharp
public sealed class ToolDefinitionCatalog
{
    private readonly IEnumerable<IToolDefinitionProvider> providers;

    public ToolDefinitionCatalog(IEnumerable<IToolDefinitionProvider> providers)
    {
        this.providers = providers;
    }

    public IReadOnlyList<ToolDefinition> GetDefinitions()
    {
        return providers.SelectMany(provider => provider.GetDefinitions()).ToArray();
    }
}
```

## Tests

- catalog combines definitions from two fake providers
- duplicate tool names are rejected
- document tool provider returns expected metadata
- runtime composition resolves the catalog

## Proof that future tools are modular

Add a test-only fake provider and prove the runtime sees it without editing runtime source.
