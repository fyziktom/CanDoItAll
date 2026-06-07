# Core Cutline

## Allowed Core production changes
- Add public deterministic diagnostics/read models under `CanDoItAll.Processes.Core`.
- Add pure transition intent facts, if they contain no execution, EF, claim, or finalizer application behavior.
- Add pure artifact matching/satisfaction result records and reason codes.
- Add API surface tests and dependency scans.
- Add module-local adapters that convert process module data into Core facts.

## Forbidden Core production changes
- `Microsoft.EntityFrameworkCore`, `DbContext`, `AppDbContext`
- `CanDoItAll.Modules.*`
- `CanDoItAll.Infrastructure.*`
- `CanDoItAll.AgentFramework.*`
- `IServiceScopeFactory`, DI registrations, runtime selectors
- `File`, `Directory`, workspace/storage placement
- claim lifecycle, transition execution, finalizer application, process mutation
- production process driver APIs or driver registries

## Driver work cutline
Driver work in this bundle is proposal/test-only:
- docs under `architecture/`
- test fixtures / negative architecture tests
- no production driver project
- no production interface
- no DI registration
- no runtime path
