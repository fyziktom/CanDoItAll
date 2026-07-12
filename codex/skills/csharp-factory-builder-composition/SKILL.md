---
name: csharp-factory-builder-composition
description: "Design C# factories, builders, catalogs, and composition-root wiring for complex runtime, workflow, process, tool, provider, and capability construction without service-locator or god-runtime patterns."
---

# C# Factory, Builder, and Composition Skill

Use this skill when a runtime or module must construct complex objects, select implementations, assemble tool/provider catalogs, or build process/workflow definitions.

## Goal

Move construction complexity out of large runtime classes while keeping domain logic out of the composition root.

## Pattern choice

Use a builder when:

- an object has many required and optional parts
- order matters
- validation must happen before runtime execution
- a process/workflow/tool definition is assembled step by step
- the same construction process produces variants

Use a factory when:

- runtime input selects an implementation
- callers should not know concrete types
- a provider/tool/driver family is extensible
- object lifetime or disposal must be controlled explicitly

Use a catalog provider when:

- a module contributes a set of tools, capabilities, skills, prompts, workflow executors, or drivers
- future additions should not edit the runtime class
- metadata and implementation registration need to stay together

Use a composition-root extension method when:

- the app host needs to register a module
- implementation packages should be wired in one place
- the extension method stays declarative and does not own runtime behavior

## Required flow

1. Identify whether the problem is construction, selection, contribution, or registration.
2. Keep immutable definitions separate from runtime instances.
3. Keep builders free of external SDK calls and side effects unless the builder is explicitly infrastructure-specific.
4. Keep factories narrow. A factory should create or select, not orchestrate full business behavior.
5. Avoid injecting `IServiceProvider` into domain/core objects.
6. Use typed factory interfaces when runtime selection is required.
7. Add validation to builders so invalid definitions fail before runtime execution.
8. Add tests for:
   - valid construction
   - missing required fields
   - selection of expected implementation
   - unknown key or unsupported provider
   - registration smoke when DI wiring changed

## Example shape

```csharp
public interface IMemoryProviderFactory
{
    IMemoryProvider Create(MemoryProviderId providerId);
}

public sealed class MemoryProviderFactory : IMemoryProviderFactory
{
    private readonly IReadOnlyDictionary<MemoryProviderId, IMemoryProvider> providers;

    public MemoryProviderFactory(IEnumerable<IMemoryProvider> providers)
    {
        this.providers = providers.ToDictionary(provider => provider.ProviderId);
    }

    public IMemoryProvider Create(MemoryProviderId providerId)
    {
        if (providers.TryGetValue(providerId, out var provider))
        {
            return provider;
        }

        throw new UnsupportedMemoryProviderException(providerId);
    }
}
```

## Avoid this shape

```csharp
public sealed class Runtime
{
    public object CreateEverything(string kind)
    {
        if (kind == "mem0") return new Mem0Client();
        if (kind == "qdrant") return new QdrantClient();
        if (kind == "filesystem") return new FileStore();
        throw new NotSupportedException(kind);
    }
}
```

The second example leaks every concrete implementation into the runtime and forces future extension by editing the runtime again.

## Composition root rule

The composition root may know implementations. Runtime, core, contracts, and builders should usually know abstractions.

Good:

```csharp
services.AddMemoryProviderContracts();
services.AddMem0MemoryProvider();
services.AddQdrantMemoryProvider();
services.AddSingleton<IMemoryProviderFactory, MemoryProviderFactory>();
```

Bad:

```csharp
services.AddSingleton(provider =>
{
    var runtime = provider.GetRequiredService<MafAgentRuntime>();
    runtime.RegisterAllToolsAndMemoryProviders(provider);
    return runtime;
});
```

## Proof required

- pattern selection record
- builder/factory tests
- invalid construction tests
- registration smoke
- source assertion that runtime no longer constructs concrete provider/tool classes directly
- no service-locator shortcut in extracted domain logic

## Exit condition

Exit only when construction and selection are cohesive, testable, and extensible without adding another partial runtime file.
