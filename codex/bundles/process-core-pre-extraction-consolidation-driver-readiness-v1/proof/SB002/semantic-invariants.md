# SB002 Semantic Invariants

## Invariants

- Invariant ID: `SB002-INV-001`
- Source raw note: `Force refactoring / proof gates every few subbundles; no production driver API; no UI/mobile proof for runtime/service-only changes.`
- Expected behavior: A dedicated architecture test guards the active bundle against collapsed gate rows, Process Core projects, production process-driver APIs, and UI/mobile changed paths outside bundle documentation.
- Disallowed shallow implementation: Adding only prose instructions or relying on the previous bundle's `SB001-SB033` guard.
- Failing-first test: `N/A - no production movement preceded this test; the test is the first guardrail for later subbundles.`
- Passing test: `bundle://proof/SB002/transcripts/architecture-guard-test.txt`
- Changed source files: `repo://tests/CanDoItAll.Tests.Unit/ProcessAgentExecutionBoundaryArchitectureTests.cs`
- Production assertions: `bundle://proof/SB002/transcripts/source-assertions-and-scans.txt`
- Red-team negative case: A collapsed `| SB001-SB036 |` gate row or changed `.razor/.css/.js/.ts/.png` path outside bundle docs fails the new guard.
- Downstream dependency check: `SB003` can use this guard as the phase gate test before production movement starts.

## Raw Note Closure

- Preserve existing functionality: `Partially solved by test-only guardrail; runtime parity remains owned by later gates.`
- No production driver API: `Partially solved by architecture test and scan; final closure remains owned by SB033/SB036.`
- No UI/mobile proof: `Partially solved by changed-file guard; final closure remains owned by SB034/SB036.`
- Fewer broader subbundles with proof gates: `Partially solved by 36-row gate assertion; final closure remains owned by SB036.`
