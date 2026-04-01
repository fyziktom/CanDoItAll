# 03 Dynamic Runtime DB and Bootstrap

## Status

- `Ready`

## Objective

- Replace startup-bound provider selection with runtime-resolved database/profile selection and add the switch coordinator plus provider drivers needed for runtime activation.

## Covered Inputs

- `RQ-002` startup precedence rules
- `RQ-004` runtime switch without restart
- `RQ-005` switch coordinator
- `RQ-011` PostgreSQL runtime activation
- `RQ-012` empty database creation foundations
- `RQ-018` explicit override compatibility
- Raw notes `N-01`, `N-02`, `N-11`, `N-14`, `N-15`

## Prerequisites

- `subbundles/01-foundation-baseline-and-guardrails` completed or blocked with usable fixtures.
- `subbundles/02-control-plane-and-profile-catalog` completed with proven catalog and active-profile resolution.

## Exact Source References

- `/mnt/data/work/CanDoItAll-toolbox-repair/src/CanDoItAll.Infrastructure/DependencyInjection/InfrastructureServiceCollectionExtensions.cs`
- `/mnt/data/work/CanDoItAll-toolbox-repair/src/CanDoItAll.Infrastructure/Persistence/AppDbContext.cs`
- `/mnt/data/work/CanDoItAll-toolbox-repair/src/CanDoItAll.Infrastructure/Persistence/AppDbContextFactory.cs`
- `/mnt/data/work/CanDoItAll-toolbox-repair/src/CanDoItAll.Web/Program.cs`
- `/mnt/data/work/CanDoItAll-toolbox-repair/src/CanDoItAll.Web/Composition/ModuleAssemblies.cs`
- `/mnt/data/work/CanDoItAll-toolbox-repair/src/CanDoItAll.Infrastructure/BackgroundJobs/BackgroundJobs.cs`
- `/mnt/data/work/CanDoItAll-toolbox-repair/src/CanDoItAll.Infrastructure/Search/SearchIndexing.cs`
- `/mnt/data/work/CanDoItAll-toolbox-repair/src/CanDoItAll.Modules.Workbench/ProjectWorkbenchModels.cs`
- `/mnt/data/work/CanDoItAll-toolbox-repair/tests/CanDoItAll.Tests.Unit/DatabaseConfigurationTests.cs`

## Deliverables

- Runtime-resolved active-profile accessor/resolver.
- `SwitchableAppDbContextFactory` or equivalent implementation that resolves the active profile per context creation.
- Database driver abstractions and concrete SQLite/PostgreSQL driver implementations.
- `DatabaseSwitchCoordinator` with switch lock, lease/drain logic, failure rollback, active-generation update, and change notification.
- Startup bootstrap that uses the active-profile resolver rather than startup-only provider config.
- Empty database creation primitives in the drivers so later UI and clone flows can call them.
- Tests that prove runtime switching changes the active data source without restarting the process.

## Dependency Impact

- Subbundles 04–08 depend on this phase to make runtime switching real rather than just a UI/config illusion.
- If this subbundle is weak, every later test can accidentally hit the old database and still appear to pass.
- The switch coordinator here defines the safety contract for subbundle 06 route reload behavior.

## Validation Depth

- `Critical foundation`

## Implementation Steps

1. Introduce runtime abstractions for the active database profile, switch generation, and switch notifications.
2. Replace the current `AddDbContextFactory<AppDbContext>` provider binding with a switchable factory that resolves the active profile at `CreateDbContextAsync` time.
3. Modify `AppDbContext` as needed so active-context leases can be tracked and released when contexts are disposed.
4. Implement SQLite and PostgreSQL database drivers with connection normalization, connection testing, empty-create support, and schema-bootstrap entry points.
5. Implement the switch coordinator that blocks new context creation, drains active operations, initializes the target DB, persists the new profile, and publishes the change generation.
6. Update startup/bootstrap code so the app resolves the initial profile from the control plane or explicit override instead of only from startup config.
7. Add unit and integration tests for switch coordination, dynamic factory behavior, override-locked mode, and process-alive runtime switching.

## Scope Exceptions

- This subbundle may still call the current schema bootstrap service until subbundle 04 replaces it with migrations.
- This subbundle does **not** yet expose the switcher to end users.
- Clone/snapshot flows remain for subbundle 08, but empty-create support should be in place now.

## Do Not Do

- Do not keep provider selection effectively cached at startup while only renaming the service.
- Do not claim runtime switching works if active contexts are not tracked or if the switch can race new context creation.
- Do not expose a UI path in this phase; the contract must be proven through tests first.

## Acceptance Checklist

- The active database can be changed through service/runtime APIs without restarting the process.
- New `AppDbContext` instances opened after the switch use the target profile/provider.
- Existing operations either drain safely or the switch fails honestly with a clear error.
- The runtime override path still works and clearly locks selection semantics for later UI.
- Empty-create primitives exist for both SQLite and PostgreSQL drivers.

## Proof Required

- `dotnet test tests/CanDoItAll.Tests.Unit/CanDoItAll.Tests.Unit.csproj --filter "FullyQualifiedName~Switch|FullyQualifiedName~Driver|FullyQualifiedName~AppDbContext|FullyQualifiedName~RuntimeOverride"`
- `dotnet test tests/CanDoItAll.Tests.Integration/CanDoItAll.Tests.Integration.csproj --filter "FullyQualifiedName~Switch|FullyQualifiedName~Driver|FullyQualifiedName~Bootstrap"`
- If PostgreSQL driver logic is exercised here, run the PostgreSQL-backed integration subset as well and record whether Docker/local PostgreSQL was used.
- Record in the execution report that the process stayed alive while data switched.

## Browser Validation Logging

- `N/A` — this subbundle is backend/runtime only.
- Do not convert backend integration tests into fake browser proof; browser-visible switch behavior belongs to subbundle 06 and subbundle 07.

## Progression Gate

- Dynamic factory behavior and switch coordination must be proven before subbundle 04 or 06 continues.
- The execution report must show that the active provider actually changes after the switch without a process restart.

## Suggested Agent Prompt

```text
Implement subbundle 03 only.

Make runtime database switching real:
- active-profile runtime resolution
- switchable DbContext factory
- SQLite/PostgreSQL drivers
- switch coordinator with drain/rollback/notification
- startup bootstrap that uses the control plane

Do not expose UI yet.
Run the required unit/integration tests and record whether PostgreSQL proof was available.
```
