# Example: Builder for Runtime Composition

## Problem

Runtime capabilities are assembled through a long method that mutates collections, reads options, creates tools, registers providers, and validates duplicate names.

## Target builder

```csharp
public sealed class RuntimeCapabilityBuilder
{
    private readonly List<ToolDefinition> tools = [];
    private readonly List<MemoryProviderDescriptor> memoryProviders = [];

    public RuntimeCapabilityBuilder AddTool(ToolDefinition tool)
    {
        tools.Add(tool);
        return this;
    }

    public RuntimeCapabilityBuilder AddMemoryProvider(MemoryProviderDescriptor provider)
    {
        memoryProviders.Add(provider);
        return this;
    }

    public RuntimeCapabilitySet Build()
    {
        EnsureUniqueToolNames();
        EnsureUniqueProviderIds();

        return new RuntimeCapabilitySet(
            tools.ToArray(),
            memoryProviders.ToArray());
    }

    private void EnsureUniqueToolNames()
    {
        var duplicates = tools
            .GroupBy(tool => tool.Name)
            .Where(group => group.Count() > 1)
            .Select(group => group.Key)
            .ToArray();

        if (duplicates.Length > 0)
        {
            throw new DuplicateToolDefinitionException(duplicates);
        }
    }

    private void EnsureUniqueProviderIds()
    {
        var duplicates = memoryProviders
            .GroupBy(provider => provider.ProviderId)
            .Where(group => group.Count() > 1)
            .Select(group => group.Key)
            .ToArray();

        if (duplicates.Length > 0)
        {
            throw new DuplicateMemoryProviderException(duplicates);
        }
    }
}
```

## Contributor model

```csharp
public interface IRuntimeCapabilityContributor
{
    ValueTask ContributeAsync(RuntimeCapabilityBuilder builder, CancellationToken cancellationToken);
}
```

## Tests

- empty builder returns empty capability set if allowed
- duplicate tool names fail
- duplicate provider ids fail
- two contributors produce one combined capability set
- failed contributor stops build and reports source

## Boundary

The builder validates definitions. It should not call external providers, execute tools, or resolve services dynamically.
