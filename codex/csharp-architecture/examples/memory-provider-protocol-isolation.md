# Example: Memory Provider Protocol Isolation

## Problem

A memory protocol must support multiple providers over time. If provider-specific logic is placed in the runtime or application core, every provider addition creates more coupling.

## Target shape

```text
Memory.Abstractions
  IMemoryProvider
  MemoryQuery
  MemoryRecord
  MemoryWriteRequest
  MemoryProviderId

Memory.Application
  MemoryRouter
  MemoryIngestionService

Memory.ProviderX
  ProviderXMemoryProvider
  ProviderXClientAdapter
  ProviderXOptions

Memory.Mcp
  MCP-facing adapter, if needed

Memory.Persistence
  storage-specific persistence
```

## Contract rules

Contracts must not expose provider SDK types.

Good:

```csharp
public interface IMemoryProvider
{
    MemoryProviderId ProviderId { get; }

    ValueTask<IReadOnlyList<MemoryRecord>> SearchAsync(
        MemoryQuery query,
        CancellationToken cancellationToken);
}
```

Bad:

```csharp
public interface IMemoryProvider
{
    Task<ProviderXSearchResponse> SearchAsync(ProviderXSearchRequest request);
}
```

## Factory

```csharp
public interface IMemoryProviderFactory
{
    IMemoryProvider GetRequiredProvider(MemoryProviderId providerId);
}
```

## Tests

- protocol records serialize predictably
- router selects the requested provider
- unknown provider id fails with a domain-specific exception
- provider adapter maps fake SDK response into repository-owned records
- application service can run against fake in-memory provider

## Proof

The runtime should depend on `IMemoryProviderFactory` or `IMemoryRouter`, not on concrete provider SDK clients.
