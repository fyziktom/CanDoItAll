# Example: Process Driver Extraction

## Problem

Generic process core begins to contain domain-specific switches for software delivery, Blazor, memory, economy simulations, or other domains.

## Target shape

```text
Processes.Contracts
Processes.Abstractions
Processes.Core
Processes.Runtime
Processes.Builder
Processes.Drivers.Abstractions
Processes.Drivers.Standard
DomainSpecific.ProcessDriver
```

## Driver contract

```csharp
public interface IProcessDriver
{
    ProcessDriverId DriverId { get; }

    ValueTask<DriverDecision> DecideAsync(
        ProcessDriverContext context,
        CancellationToken cancellationToken);
}
```

## Generic runtime rule

The runtime may ask drivers for decisions. It must not know domain-specific branch names.

Good:

```csharp
var driver = driverCatalog.GetRequiredDriver(instance.DriverId);
var decision = await driver.DecideAsync(context, cancellationToken);
```

Bad:

```csharp
if (instance.TemplateName.Contains("blazor"))
{
    return DecideBlazorRepairPath(instance);
}

if (instance.TemplateName.Contains("memory"))
{
    return DecideMemoryProviderPath(instance);
}
```

## Tests

- generic runtime invokes fake driver
- unsupported driver id fails clearly
- standard driver returns expected decision for standard context
- domain driver is tested without runtime host
- composition smoke resolves driver catalog

## Proof

The process core should remain domain-agnostic. Domain-specific behavior must live behind driver contracts.
