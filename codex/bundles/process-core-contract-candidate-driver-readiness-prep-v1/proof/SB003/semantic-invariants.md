# SB003 Semantic Invariants

## Invariant SB003-INV-001
- Invariant ID: `SB003-INV-001`
- Source raw note: Do not rush `Process Core`; avoid production driver API; preserve separate SB001-SB033 execution rows; no small/medium/mobile/browser proof for runtime-only work.
- Expected behavior: The active bundle remains a runtime/service refactor with no Core project, no driver API, no UI proof drift, and exactly one subbundle gate row for each SB001 through SB033.
- Disallowed shallow implementation: A bundle can pass by adding a collapsed `SB001-SB033` gate row, by checking only old bundle folders, or by scanning documentation instead of production source.
- Failing-first test: `bundle://proof/SB003/transcripts/unit-architecture-test-after-build.txt`
- Passing test: `bundle://proof/SB003/transcripts/unit-architecture-test-passing.txt`
- Changed source files: `repo://tests/CanDoItAll.Tests.Unit/ProcessAgentExecutionBoundaryArchitectureTests.cs`
- Production assertions: `bundle://proof/SB003/transcripts/source-assertions-and-scans.txt` proves dispatch production source has no Process Core or driver API names and no dispatch stub markers.
- Red-team negative case: `bundle://proof/SB003/transcripts/unit-architecture-test-after-build.txt` shows the guard rejects an aggregate-row false positive when the parser accidentally included the browser analytics table.
- Downstream dependency check: SB004-SB030 depend on this guard before refactoring route, finalizer, hydration, subprocess, direct-agent, projection, validation, rule, and driver-readiness boundaries.
