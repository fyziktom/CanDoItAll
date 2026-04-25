# Scope Inventory

## In Scope

- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.AgentFramework`
- `C:\repositories\CanDoItAll\src\CanDoItAll.AgentFramework.Core`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Processes`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.CrmHr`
- `C:\repositories\CanDoItAll\tests\CanDoItAll.Tests.Integration`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Web\appsettings.Development.json`

## Likely New Files

- AgentFramework hosting options for process mock agents.
- AgentFramework hosting catalog seeder for the mock provider and mock role agents.
- AgentFramework hosting runtime decorator for deterministic calculator-process behavior.
- Integration tests for settings gating, runtime output, and process repair progression.

## Out Of Scope

- Real LLM provider changes.
- UI redesign.
- Replacing the process dispatcher or progression planner.
- Broad scenario harness refactoring beyond the minimal runtime chain needed for this feature.
