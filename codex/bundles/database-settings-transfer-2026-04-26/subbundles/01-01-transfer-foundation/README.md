# 01-transfer-foundation

## Status

- `Completed`

## Objective

- Add generic database transfer contracts and orchestration in Infrastructure so UI and modules do not manually copy records.

## Covered Inputs

- Generic transfer system requirement.
- Runtime DB switch problem.
- Maintainability/testability requirement.

## Prerequisites

- none

## Exact Source References

- `C:\repositories\CanDoItAll\src\CanDoItAll.Infrastructure\ControlPlane\DatabaseProfileModels.cs`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Infrastructure\Persistence\SwitchableAppDbContextFactory.cs`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Infrastructure\DependencyInjection\InfrastructureServiceCollectionExtensions.cs`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Infrastructure\Persistence\DatabaseRuntimeSwitching.cs`

## Deliverables

- Transfer models for descriptors, previews, requests, results, and context.
- `IDatabaseTransferHandler` and `IDatabaseTransferService`.
- Service implementation that resolves source/target profiles and opens explicit source/target contexts.
- DI registration.

## Dependency Impact

- All handlers and UI depend on this. Weak proof here can make later transfer proof meaningless because records could copy from/to the wrong DB.

## Validation Depth

- Critical foundation.

## Implementation Steps

1. Add transfer model and service files under Infrastructure control-plane.
2. Register the service in Infrastructure DI.
3. Keep handler discovery via `IEnumerable<IDatabaseTransferHandler>`.
4. Ensure preview and execution both use explicit source/target profile resolution.

## Scope Exceptions

- This subbundle does not copy any settings records by itself.

## Do Not Do

- Do not place module-specific copy logic in Infrastructure.
- Do not use the ambient active `AppDbContext` as a source or target.

## Acceptance Checklist

- Contracts expose enough metadata for a checkbox UI.
- Service can list source profiles excluding target.
- Service returns deterministic result rows per selected item.
- Code compiles with no module reference cycle.

## Proof Required

- `dotnet build C:\repositories\CanDoItAll\CanDoItAll.slnx` or a narrower successful build if full build is blocked by unrelated issues.

## Browser Validation Logging

- N/A. This subbundle has no browser-visible UI.

## Progression Gate

- Proceed only when the transfer service compiles and downstream handlers can depend on Infrastructure abstractions.

## Suggested Agent Prompt

```text
Implement only the Infrastructure database transfer foundation. Do not add module copy logic or UI.
```
