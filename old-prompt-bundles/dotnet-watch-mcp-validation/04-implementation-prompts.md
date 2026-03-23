# Implementation Prompts

Status on 2026-03-20:

- Prompt 1 executed and validated.
- Prompt 2 executed and validated.
- Prompt 3 executed and validated.

## Prompt 1: Safety blockers

```text
Repair the core safety blockers in CanDoItAll.Mcp.DotNetWatch.

Scope:
- src/CanDoItAll.Mcp.DotNetWatch/Runtime/SessionCoordinator.cs
- src/CanDoItAll.Mcp.DotNetWatch/Persistence/StaleProcessRegistry.cs
- src/CanDoItAll.Mcp.DotNetWatch/Processes/ProcessServices.cs
- related tests under tests/CanDoItAll.Mcp.DotNetWatch.Tests and tests/CanDoItAll.Mcp.DotNetWatch.IntegrationTests

Required outcomes:
- Implement ContinueIfSafe or remove it from the public surface.
- Make app_start(waitFor=...) surface wait failure/timeout instead of always returning AppStartData success.
- Verify stale cleanup ownership before killing a PID.
- Implement graceful-then-force process tree termination with Windows and Unix-aware behavior.

Constraints:
- Keep MCP tool names stable unless the contract is intentionally narrowed everywhere.
- Add regression tests for each repaired blocker.
- Do not revert unrelated user changes.
```

## Prompt 2: Contract parity

```text
Bring the DotNetWatch MCP server into parity with the CodexPack contract.

Scope:
- src/CanDoItAll.Mcp.DotNetWatch/Tools/CanDoItAllTools.cs
- src/CanDoItAll.Mcp.DotNetWatch/Contracts/ToolContracts.cs
- src/CanDoItAll.Mcp.DotNetWatch/Configuration/RuntimeConfiguration.cs
- src/CanDoItAll.Mcp.DotNetWatch/Health/HealthServices.cs
- src/CanDoItAll.Mcp.DotNetWatch/Operations/OperationModels.cs
- related tests

Required outcomes:
- Implement includeHistory or remove it from the tool surface.
- Add actual config redaction for workspace_info snapshots.
- Honor StableHealthSuccessCount.
- Fix QuietSinceCursor baseline semantics.
- Add missing tests_run functionality: environmentOverlay, Auto runner detection, artifact reporting.
- Ensure build operations do not report a test runner.

Deliverable:
- Code changes plus tests proving each repaired contract gap.
```

## Prompt 3: Validation matrix closure

```text
Close the validation gap between the CodexPack matrix and the current automated suite.

Scope:
- tests/CanDoItAll.Mcp.DotNetWatch.Tests
- tests/CanDoItAll.Mcp.DotNetWatch.IntegrationTests
- any lightweight fixtures needed for deterministic coverage

Required matrix coverage:
- VAL-001
- VAL-002
- VAL-006
- VAL-007
- VAL-008
- VAL-011
- VAL-015
- VAL-016
- VAL-020
- VAL-024
- VAL-025
- VAL-029
- VAL-030
- VAL-031

Constraints:
- Prefer focused fixtures over the full repo where possible.
- Treat stdout-discipline and stale-cleanup tests as release-blocking.
- Keep tests deterministic and CI-friendly.
```
