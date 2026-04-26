# Source Artifacts

## Real Run Database

- `C:\Users\lucys\AppData\Local\CanDoItAll\control-plane\database-profiles\managed-sqlite\bf40a76da44f4d0f858dc55f428483c8\db\candoitall.db`

The DB is an external runtime artifact. Do not modify it during bundle execution. Query it read-only when needed.

## DB Evidence Captured During Preparation

Latest failed run:

```text
RunId: 8F1A0E9E-FC8A-405C-A370-57A1A560E9A3
Name: Create main application / Multi-team software delivery and release governance
Status: Failed
DefinitionId: A585EA10-1996-4BEB-9080-33B05091F0EB
ProjectId: 8E29CECB-D07C-49F8-8E29-7164A42C2C7A
UpdatedAtUtc: 2026-04-25 23:29:05.5789203+00:00
```

Failed step:

```text
StepRunId: 1F125B32-04B3-464F-A51C-563EF3DDBEEB
Sequence: 2
Title: Implement feature, tests, and migration notes
Status: Failed
Executor: Programming Workspace Analyst
ExceptionSummary: AgentFramework run failed due repeated identical workspace_write_file invocation. Recovery attempt 5 of 5.
```

Failed step required artifacts:

```text
Implementation change set | Deliverable | required | ReviewRequired
Migration and rollout preparation checklist | Checklist | required | ReviewRequired
```

Artifacts actually recorded for failed step included implementation files and validation transcripts, but no `Migration and rollout preparation checklist` artifact:

```text
Calculator.csproj
Calculator.Tests.csproj
CalculatorEngineTests.cs
dotnet_build stdout/stderr
dotnet_test stdout/stderr
```

## Source Files To Inspect

- `C:\repositories\CanDoItAll\Templates\Processes\processes\software-delivery\definition.json`
- `C:\repositories\CanDoItAll\Templates\Processes\processes\software-delivery\steps\implementation.md`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Processes\ProcessRunAutomationDispatchService.cs`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.AgentFramework\Hosting\ProcessMockAgentRuntime.cs`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.AgentFramework\Hosting\ScenarioHarnessAgentRuntime.cs`
- `C:\repositories\CanDoItAll\src\CanDoItAll.AgentFramework.Persistence\SeedAssets\instructions\agents\programming-workspace-analyst.md`
- `C:\repositories\CanDoItAll\tests\CanDoItAll.Tests.Integration\ProcessMockAgentRuntimeIntegrationTests.cs`
- `C:\repositories\CanDoItAll\tests\CanDoItAll.Tests.Integration\ProcessRunAutomationDispatchServiceTests.cs`
