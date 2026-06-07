# Core Dependency Guardrails

Add architecture tests that scan:

- project references of `CanDoItAll.Processes.Core`,
- source text in Core,
- source text in module adapters,
- solution project references.

Core must reject these tokens in production source:

- `DbContext`
- `IDbContextFactory`
- `Microsoft.EntityFrameworkCore`
- `IWorkspace`
- `WorkspacePathResolver`
- `IStorage`
- `StoragePlacement`
- `AgentFramework`
- `Maf`
- `ProcessRunAutomationDispatchService`
- `IProcessDriver`
- `DriverPack`
- `DriverRegistry`
- `IServiceProvider`
- `IServiceScopeFactory`
- `ILogger<ProcessRunAutomationDispatchService>`

Core may use simple enums/statuses from Contracts.
