# SB05 Facade Foundation Source Assertions

- `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessAutomationExecutionClient.cs` introduces the internal `IProcessAutomationExecutionClient` boundary owned by the Processes module.
- `ProcessAutomationExecutionClient` delegates execution run, run detail, run query, agent catalog, provider catalog, provider health probe, agent editor load, and agent editor save operations to the current `IAgentFrameworkWorkspaceService`.
- Null-sensitive request/model inputs fail explicitly through `ArgumentNullException`; no silent fallback or alternate runtime path was added.
- `repo://src/CanDoItAll.Modules.Processes/Services/ProcessesModuleServiceCollectionExtensions.cs` registers `IProcessAutomationExecutionClient` as scoped next to the existing automation dispatch service.
- SB05 intentionally leaves `ProcessRunAutomationDispatchService` wired to `IAgentFrameworkWorkspaceService`; SB06 owns the dispatcher migration.
- `repo://tests/CanDoItAll.Tests.Integration/ProcessAutomationExecutionClientTests.cs` uses a fake workspace service proxy to prove delegation and DI registration without starting runtime infrastructure.
- Browser validation is N/A because SB05 changed no rendered UI route and produced no screenshots.
