# SB20-SB28 Runtime Completion Semantic Invariants

## Invariants

1. Process core/runtime/dispatch remains domain-generic.
   - The `workspace_dotnet_stop` implementation lives in AgentFramework workspace command tooling and policy, not in Process runtime core.
   - Runtime stop scans found no Tetris or product-specific vocabulary in the generic stop/policy/seed C# implementation.

2. Kept-alive runtime cleanup is governed by receipt, not by arbitrary script execution.
   - `workspace_dotnet_stop` accepts a `startup.json` receipt path and writes `cleanup.json` beside it.
   - New startup receipts advertise `stopTool` and `stopToolStartupReceiptPath`; raw `stopCommand` output is removed from new receipts.
   - Tool policy classifies stop as a validation/runtime-launch side-effect and requires `LaunchRuntime` or `CaptureRuntimeProof`.

3. Process validation agents can use the cleanup path without broad PowerShell fallback.
   - Seeded capabilities include `workspace-dotnet-stop` for programming, QA, Blazor, dotnet, and screenshot validation agents.
   - Inline delivery instructions and Blazor process validation steps instruct agents to call `workspace_dotnet_stop` and cite `cleanup.json`.
   - Stale managed catalog metadata refreshes to the seeded stop capability description/configuration.

4. Runtime launch and dispatch can execute a real project-structure process end to end.
   - Project-structure API launch returned `stage: Completed` for run `06f0c5bd-f425-44b9-9985-0a11e0a72a6f`.
   - Runtime status query shows the run status `Completed`, validation/revalidation steps `Completed`, skipped branch alternatives, and final result recording `Completed`.
   - The process handled a real QA finding by entering repair, then accepted the repaired result after revalidation.

5. Cleanup proof is durable and negative evidence is explicit.
   - Both validation cleanup receipts have `succeeded: true` and empty `stillRunningProcessIds`.
   - Host process inspection after completion found no `TetrisGame` processes.
   - The final startup receipt records `cleanupAttempted: true`, `cleanupSucceeded: true`, and the cleanup receipt path.

## Production Behavior Artifact Matrix

| Behavior | Producer | Consumer | Negative/positive proof |
| --- | --- | --- | --- |
| Stop tool rejects non-receipt/no-path usage and builds bounded cleanup plans | `WorkspaceCommandPlanBuilder.BuildDotnetStop` | `WorkspaceCommandExecutionService.DotnetStop` and MAF workspace plugin | Unit tests in `test-unit-workspace-dotnet-stop-focused.txt` |
| Stop tool is available to validation-capable agents | Seed builder, seed normalizer, template skill lists | Agent workspace catalog and runtime capability mapping | Integration tests in `test-integration-seed-workspace-dotnet-stop.txt` |
| Process validation must stop runtime before outcome | Blazor process templates and inline delivery skill text | QA/revalidation agents | E2E summaries and cleanup receipts under `e2e/` |
| Project-structure process execution completes | Process API/runtime/dispatch/application layers | User-facing Live Processes route and result artifacts | Launch response and runtime status query under `e2e/` and `transcripts/` |

## Residual Risk

The e2e run is one real Blazor/Tetris project scenario from the dev DB. It proves the repaired runtime path, receipt cleanup, repair/revalidation branch, and project-structure launch API. Broader non-Blazor scenario coverage remains a normal future regression expansion, not a blocker for the requested Tetris process validation.
