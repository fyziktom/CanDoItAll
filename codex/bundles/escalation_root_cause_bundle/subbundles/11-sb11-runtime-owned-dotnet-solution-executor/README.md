# SB11 - Runtime-Owned .NET Solution Executor

## Status

- `Reopened - generic script-lifecycle extraction in progress`
- Critical foundation: no

## Objective

Move deterministic .NET solution setup from prompt-only execution into a runtime-owned executor after the tool-plan guard and capability contracts are proven. The executor should create, wire, and verify solution membership idempotently using governed workspace tools.

## Reopened Boundary Finding (2026-07-12)

The original executor closed the prompt-only gap, but its implementation still
contains a generic managed-script lifecycle. That lifecycle is now being
extracted into a workspace driver so the .NET driver owns only .NET mechanics.
This is a boundary correction, not a reversal of runtime-owned setup or a move
of .NET semantics into the generic dispatcher.

## Covered Inputs

- GPTPro runtime-owned .NET setup plan.
- REQ-011, REQ-013, REQ-014, REQ-015, REQ-017, REQ-018, REQ-020.
- The incident where the agent scaffolded but did not run the wiring helper.

## Prerequisites

- SB01 launch variable resolution complete.
- SB07 tool-plan guard complete.
- SB10 capability-aware assignment complete.
- SB09 migrated `dotnet-solution-setup` execution metadata available.

## Exact Source References

- `bundle://codex/07-runtime-owned-dotnet-solution-setup-plan.md`
- `repo://src/Modules/CanDoItAll.Modules.Workbench/ProjectStructure/ProjectStructureProcessLaunchVariableContributor.cs`
- `repo://src/Modules/CanDoItAll.Modules.Processes/Services/RuntimeIntegration/ProcessRuntimeToolPreflightService.cs`
- `repo://Templates/Processes/processes/dotnet-solution-setup/definition.json`
- `repo://Templates/Processes/processes/dotnet-solution-setup/steps/create-dotnet-project.md`
- `repo://Templates/Processes/processes/dotnet-solution-setup/steps/add-test-project.md`
- `repo://tests/Unit/CanDoItAll.Tests.Unit/WorkspaceRuntimeProcessToolsTests.cs`
- `repo://tests/Unit/CanDoItAll.Tests.Unit/WorkspaceCommandExecutionServiceTests.cs`
- `repo://tests/Integration/CanDoItAll.Tests.Integration/ProjectStructureAgentIntegrationTests.cs`

## Deliverables

- Runtime-owned executor for deterministic .NET setup operations or a clearly named service in the existing runtime/tool boundary.
- Idempotent create project, add to solution, add test project, and readback verification operations.
- Execution receipts for scaffold, helper script execution, solution membership readback, and side-effect manifest.
- Integration with completion gates so executor success is still validated by product state, not assumed.
- Tests proving existing-project repair runs the missing wire/readback path instead of destructive recreation.

## Dependency Impact

- SB12 uses this as the long-term fix beyond prompt/tool-plan guard.
- Template migration in SB09 must point deterministic .NET setup steps to this executor where applicable.

## Validation Depth

- Unit tests with fake workspace tools plus integration tests using temporary workspace/project roots.
- Semantic proof must show runtime-owned execution produces the required helper receipt and product readback.

## Implementation Steps

1. Reopen SB07 plan records and decide whether they already serve as executor commands.
2. Locate the existing governed workspace tool APIs for file writes, PowerShell script execution, and dotnet commands.
3. Implement executor operations as idempotent commands with explicit inputs and outputs.
4. For create project, skip destructive scaffold when the expected project already exists and record why.
5. For wire solution, run or invoke the generated helper path and capture `workspace_pwsh_run_script` receipt.
6. For readback, verify `dotnet sln list` or equivalent product state includes the expected project.
7. Return structured receipts and diagnostics to the completion gate evaluator.
8. Add tests for clean create, existing project missing solution membership, add-test project path, script failure, and readback failure.
9. Update templates to select runtime-owned execution where the deterministic contract is complete.
10. Ensure logs include project path, solution path, operation, exit code, and masked workspace context.

## Do Not Do

- Do not bypass the SB07 guard.
- Do not delete/recreate existing projects as the default repair.
- Do not assume command success without product readback.
- Do not make the executor Workbench-only if process runtime templates need it generically.

## Acceptance Checklist

- [x] Runtime-owned executor can create a project and wire it into solution.
- [x] Existing project with empty solution runs wire/readback repair.
- [x] `workspace_pwsh_run_script` receipt or runtime-owned equivalent is produced.
- [x] Completion gates still verify solution membership.
- [x] Failures return explicit diagnostics and do not silently fallback to agent prose.
- [x] Tests cover clean and repair scenarios.

## Closure Evidence

- Runtime-owned executor implemented in `repo://src/Modules/CanDoItAll.Modules.Processes/Services/RuntimeIntegration/DotNetSolutionSetupRuntimeExecutor.cs`.
- Adapter integration implemented in `repo://src/Modules/CanDoItAll.Modules.Processes/Services/RuntimeIntegration/AgentFrameworkProcessExecutionAdapter.DotNetSetupRuntime.cs`.
- DI registration added in `repo://src/Modules/CanDoItAll.Modules.Processes/Services/ProcessesModuleServiceCollectionExtensions.cs`.
- Tests added in `repo://tests/Unit/CanDoItAll.Tests.Unit/DotNetSolutionSetupRuntimeExecutorTests.cs` and adjacent adapter regression coverage.
- `proof/SB11/transcripts/01-targeted-runtime-owned-dotnet-tests.txt` records 7 passing focused tests covering clean create, existing-project repair, add-test-project helper execution, helper failure, readback failure, adapter runtime-owned bypass, and SB07 guard regression.
- `proof/SB11/transcripts/02-modules-processes-build.txt` records `CanDoItAll.Modules.Processes` build success with 0 warnings and 0 errors.
- CodeAnalytics snapshot `snap-20260708212205-c7d874cd` reported no scoped dependency cycles.

## Proof Required

- `proof/SB11/manifest.md`
- `proof/SB11/semantic-invariants.md`
- Failing-first prompt-only missing-helper test.
- Passing runtime-owned executor tests.
- Source assertions for idempotent command behavior.
- Production Behavior Artifact Matrix if new command/result records are introduced.

## Browser Validation Logging

- `N/A`; no browser surface is changed.

## Progression Gate

- SB12 may close only after deterministic .NET setup can be runtime-owned or a documented blocker explains why guard-only behavior remains.

## C# Architecture Impact

Adds deterministic runtime execution for a previously prompt-owned workflow.

## Boundary Ownership

Executor belongs in the process runtime/tool boundary. Workbench supplies project structure context but must not own generic deterministic execution policy.

## Dependency Direction

Runtime-owned executor must not depend on UI or agent instruction markdown.

## Pattern Decision

Use PSR-007 command records and executor service.

## Testability Contract

Executor tests use fake workspace tools for unit coverage and temporary workspace roots for integration coverage.

## Partial Class Policy

No adapter partial changes except to consume executor receipts where necessary.

## Architecture Proof Required

- Executor boundary rationale.
- Idempotency proof for existing-project repair.
- Dependency/cycle evidence if new contracts are shared.

## Suggested Agent Prompt

```text
Execute SB11 only. Implement runtime-owned deterministic .NET solution setup on top of the SB07 guard. Prove existing-project empty-solution repair runs the wire/readback path and still passes through completion gates.
```
