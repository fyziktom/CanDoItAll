# SB003 Semantic Invariants

## Invariants

- Invariant ID: `SB003-INV-001`
- Source raw note: `Preserve all original functionality; do not rush Process Core; keep future helper-driver preparation aligned but do not create production driver APIs; no small/medium/mobile/browser proof for runtime/service-only changes.`
- Expected behavior: Before production refactoring starts, the branch builds, focused process architecture guards pass, current-bundle gate rows remain individually accountable, Process Core projects are absent, production process-driver APIs are absent, and UI/mobile changed paths outside bundle docs are absent.
- Disallowed shallow implementation: A build-only gate that ignores collapsed proof rows, stale previous-bundle assertions, documentation-only forbidden-token mentions, or UI/mobile drift outside the process runtime surface.
- Failing-first test: `N/A - no production behavior changed in Gate A; the adversarial architecture guard is the first active negative proof for downstream movement.`
- Passing test: `bundle://proof/SB003/transcripts/focused-architecture-tests.txt`
- Changed source files: `repo://tests/CanDoItAll.Tests.Unit/ProcessAgentExecutionBoundaryArchitectureTests.cs` with SHA-256 `694ed8880c9bacfa9d378e18ac514520f3f52121bf752d81a9e0fbfd568c415b`
- Production assertions: `bundle://proof/SB003/transcripts/source-assertions-and-scans.txt`
- Red-team negative case: A future change that adds `CanDoItAll.Processes.Core`, `IProcessDriverRegistry`, a `.razor` changed file outside bundle docs, or `| SB001-SB036 |` in the execution report fails the guard.
- Downstream dependency check: `SB004` may start only while `bundle://proof/SB003/transcripts/critical-build.txt`, `bundle://proof/SB003/transcripts/focused-architecture-tests.txt`, and `bundle://proof/SB003/transcripts/source-assertions-and-scans.txt` remain passing and current.

## Raw Note Closure

- Preserve existing behavior: `Partially solved by build and architecture guard; route/finalizer/subprocess/projection parity remains owned by later critical gates.`
- Do not rush Process Core: `Partially solved by Gate A no-Core proof; final Core decision remains owned by SB036.`
- No production driver API: `Partially solved by Gate A no-driver proof; final driver readiness remains owned by SB033/SB036.`
- No UI/mobile/browser proof for runtime-only changes: `Partially solved by Gate A changed-file scan; final no-UI scan remains owned by SB034/SB036.`
