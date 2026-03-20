# Findings

## Summary verdict

The new dotnet watch MCP server is partially implemented, but it does not yet satisfy the CodexPack contract or its validation matrix.

Key proof points:

- The CodexPack exposes `ContinueIfSafe`, but the implementation throws `UnsupportedPolicy`.
- The CodexPack requires safe stale-process ownership verification, but cleanup currently kills by PID only.
- The CodexPack requires cross-platform graceful stop behavior, but the implementation uses a single hard-kill terminator.
- The CodexPack defines more wait, workspace, and test contract behavior than the implementation currently supports.
- The CodexPack validation matrix defines 20 P0 scenarios, while the current automated coverage is 6 unit tests and 9 integration tests.

## Detailed findings

### F-001 [P0] `ContinueIfSafe` is advertised but not implemented

Specification:

- `CanDoItAll.Mcp.DotNetWatch.CodexPack/08-user-stories-and-acceptance.md`, Build acceptance criteria
- `CanDoItAll.Mcp.DotNetWatch.CodexPack/06-tool-contracts.md`, `whenAppRunning`

Implementation proof:

- `src/CanDoItAll.Mcp.DotNetWatch/Contracts/ToolContracts.cs`: `WhenAppRunningPolicy` includes `ContinueIfSafe`
- `src/CanDoItAll.Mcp.DotNetWatch/Runtime/SessionCoordinator.cs:371-374`: explicit `UnsupportedPolicy` throw

Live proof:

- Calling `candoitall_solution_build` with `whenAppRunning=ContinueIfSafe` returned `UnsupportedPolicy`.

Impact:

- The public contract and the real server disagree.
- Clients can select a policy that the server advertises but cannot execute.

### F-002 [P0] `app_start(waitFor=...)` ignores wait failure outcomes

Specification:

- `CanDoItAll.Mcp.DotNetWatch.CodexPack/06-tool-contracts.md`, `candoitall_app_start`

Implementation proof:

- `src/CanDoItAll.Mcp.DotNetWatch/Runtime/SessionCoordinator.cs:73-76` calls `WaitForAppAsync(...)`
- The returned `AppWaitData` is discarded
- `src/CanDoItAll.Mcp.DotNetWatch/Runtime/SessionCoordinator.cs:78-88` always returns `AppStartData`

Impact:

- `app_start(waitFor=Healthy)` can still return success metadata even if the wait timed out.
- The contract says the tool may internally wait and return the final result; the implementation does not propagate that outcome.

### F-003 [P0] stale process cleanup can kill the wrong process

Specification:

- `CanDoItAll.Mcp.DotNetWatch.CodexPack/08-user-stories-and-acceptance.md:208-212`
- `CanDoItAll.Mcp.DotNetWatch.CodexPack/17-qa-review/04-known-risks-and-open-questions.md`

Implementation proof:

- `src/CanDoItAll.Mcp.DotNetWatch/Persistence/StaleProcessRegistry.cs:75-90`
- Cleanup resolves by PID only, checks only whether the process exists, then terminates it
- No verification of current workspace, command, arguments, process ancestry, or live server ownership

Impact:

- PID reuse or stale registry corruption can terminate an unrelated process.
- This is explicitly called out as a blocker in the QA pack.

### F-004 [P0] cross-platform graceful stop contract is not implemented

Specification:

- `CanDoItAll.Mcp.DotNetWatch.CodexPack/08-user-stories-and-acceptance.md:268-272`

Implementation proof:

- `src/CanDoItAll.Mcp.DotNetWatch/Processes/ProcessServices.cs:26-39`: single `ProcessTreeTerminator`
- `src/CanDoItAll.Mcp.DotNetWatch/Processes/ProcessServices.cs:73-85`: graceful stop depends on `CloseMainWindow()` and then falls back to kill-tree

Gaps:

- No Windows-specific vs Unix-specific implementation split
- No signal-based graceful shutdown path for CLI child processes
- No evidence of child-process enumeration beyond `Kill(entireProcessTree: true)`

Impact:

- The implementation does not satisfy the stated cross-platform behavior contract.

### F-005 [P1] `workspace_info` contract is incomplete and config snapshot is not redacted

Specification:

- `CanDoItAll.Mcp.DotNetWatch.CodexPack/06-tool-contracts.md`, `candoitall_workspace_info`
- `CanDoItAll.Mcp.DotNetWatch.CodexPack/08-user-stories-and-acceptance.md`, Workspace acceptance criteria

Implementation proof:

- `src/CanDoItAll.Mcp.DotNetWatch/Tools/CanDoItAllTools.cs:13-23`: accepts `includeHistory`, but does not use it
- `src/CanDoItAll.Mcp.DotNetWatch/Runtime/SessionCoordinator.cs:26-39`: no history is returned
- `src/CanDoItAll.Mcp.DotNetWatch/Contracts/ToolContracts.cs:110-118`: no relative paths and no history model
- `src/CanDoItAll.Mcp.DotNetWatch/Configuration/RuntimeConfiguration.cs:160-170`: returns raw `environmentOverlay`

Live proof:

- `candoitall_workspace_info(includeHistory=true, includeConfigSnapshot=true)` returned no history payload.
- The snapshot returned raw development environment values instead of a redacted form.

Impact:

- The tool surface does not match the pack.
- The redaction claim is overstated.

### F-006 [P1] wait contract is still incomplete

Specification:

- `CanDoItAll.Mcp.DotNetWatch.CodexPack/08-user-stories-and-acceptance.md:97-113`
- `CanDoItAll.Mcp.DotNetWatch.CodexPack/06-tool-contracts.md`, `candoitall_app_wait`

Implementation proof:

- `src/CanDoItAll.Mcp.DotNetWatch/Contracts/ToolContracts.cs:28-36`: no `Ready`, no `RestartCompleted`
- `src/CanDoItAll.Mcp.DotNetWatch/Configuration/RuntimeConfiguration.cs:30`: `StableHealthSuccessCount` is captured
- `src/CanDoItAll.Mcp.DotNetWatch/Health/HealthServices.cs:24-73`: stable success count is never used
- `src/CanDoItAll.Mcp.DotNetWatch/Runtime/SessionCoordinator.cs:183-199`: `QuietSinceCursor` uses `CurrentSequence` when `cursor` is null, which makes the initial baseline depend on call timing

Impact:

- The wait engine does not yet meet the promised semantics.
- Quiet wait and health stability are weaker than specified.

### F-007 [P1] `tests_run` is only partially implemented

Specification:

- `CanDoItAll.Mcp.DotNetWatch.CodexPack/06-tool-contracts.md:386-430`
- `CanDoItAll.Mcp.DotNetWatch.CodexPack/08-user-stories-and-acceptance.md:145-169`

Implementation proof:

- `src/CanDoItAll.Mcp.DotNetWatch/Tools/CanDoItAllTools.cs:142-173`: no `environmentOverlay` parameter
- `src/CanDoItAll.Mcp.DotNetWatch/Runtime/SessionCoordinator.cs:281`: runner is only selected from provided/default text, not auto-detected
- `src/CanDoItAll.Mcp.DotNetWatch/Operations/OperationModels.cs:165-186`: no artifact model at all
- `src/CanDoItAll.Mcp.DotNetWatch/Runtime/SessionCoordinator.cs:242`: build operations inherit `configuration.TestRunnerPreference`

Impact:

- The tool surface is missing request fields promised by the pack.
- Auto runner detection is not implemented.
- Artifact output is not implemented.
- Build responses can incorrectly contain a test runner value.

### F-008 [P1] configuration fields exist but are not enforced by behavior

Implementation proof:

- `src/CanDoItAll.Mcp.DotNetWatch/Configuration/McpServerOptions.cs`: includes `MaxFileSizeMb`, default build/test policies, and wait defaults
- `src/CanDoItAll.Mcp.DotNetWatch/Logging/LoggingModels.cs:93-111`: file logging has no size cap or rotation logic
- `src/CanDoItAll.Mcp.DotNetWatch/Runtime/SessionCoordinator.cs:223-245` and `248-284`: call sites accept explicit `whenAppRunning` but do not apply config defaults when callers omit values because tool defaults are baked into method signatures

Impact:

- Parts of the configuration model are present only on paper.

### F-009 [P1] validation coverage is far below the pack's release gate

Specification:

- `CanDoItAll.Mcp.DotNetWatch.CodexPack/11-validation-strategy.md`
- `CanDoItAll.Mcp.DotNetWatch.CodexPack/12-validation-matrix.csv`

Current coverage:

- `tests/CanDoItAll.Mcp.DotNetWatch.Tests/InfrastructureTests.cs`: 6 tests
- `tests/CanDoItAll.Mcp.DotNetWatch.IntegrationTests/McpServerIntegrationTests.cs`: 9 tests
- Validation matrix P0 count: 20 scenarios

Missing or not evidenced by tests:

- stdout cleanliness
- invalid bootstrap configuration
- session reuse/conflict behavior
- quiet wait after change
- build failure path
- proof that `tests_run` invokes `dotnet test`
- Windows and Unix kill-tree coverage
- correlation consistency
- actionable busy-workspace errors

Impact:

- The server is not validated to the pack's own release standard.

### F-010 [P2] test execution itself is still heavy and unstable

Evidence:

- Unit tests passed locally.
- Running the integration test project with `dotnet test ... --filter FullyQualifiedName~McpServerIntegrationTests` timed out during this review after approximately 124 seconds.

Impact:

- Even where coverage exists, execution cost and stability still need attention.

## Missing P0 matrix coverage summary

The following P0 matrix items are not fully evidenced by the current automated suite:

- VAL-001 stdio cleanliness
- VAL-002 invalid startup config fail-fast
- VAL-006 compatible start reuse
- VAL-007 incompatible start conflict
- VAL-008 full process-tree stop
- VAL-011 quiet wait after change
- VAL-015 build failure diagnostics path
- VAL-016 proof of `dotnet test`
- VAL-020 startup cleanup safety and logging
- VAL-024 no stdout contamination during runtime
- VAL-029 Windows kill tree
- VAL-030 Unix kill tree
