# SB01 Proof Manifest

## Status

- `Completed`

## Required Artifacts

| Artifact | Required path or rule | Status |
| --- | --- | --- |
| Passing transcript | `bundle://proof/SB01/evidence/passing-provider-native-browser-evidence.txt` | Passed, 9 targeted tests |
| Changed-file hashes | `bundle://proof/SB01/evidence/changed-file-hashes.txt` | Captured |
| Source assertions | `bundle://proof/SB01/evidence/source-assertions.txt` | Captured |
| Process artifact record assertion | `ProcessRunAutomationDispatchServiceTests.ResolveSuccessfulBrowserToolOutputFiles_reads_playwright_mcp_outputs_from_structured_evidence_refs` | Passed in transcript |

## Production Behavior Artifact Matrix

| Production artifact or signal | Producer | Consumer | Lifecycle proof required | Negative-test citation |
| --- | --- | --- | --- | --- |
| Browser proof artifact record | Production artifact projection from provider-native MCP output discovery | Process validation and artifact views | Test must exercise production projection, not manually seed rows | Missing managed screenshot must fail |
| Browser proof conformance observation | Production validation path when required evidence cannot be imported | Process run diagnostics | Test must exercise real validation path | Detached `.playwright-mcp` reference must produce observation or repair |

## Completion Rule

This manifest is complete for code-level closure. The provider-native evidence path is covered by integration tests and source assertions; live process artifact rows are deferred to the user-owned clean-DB retest recorded in `bundle://proof/SB04/evidence/fresh-process-run-summary.txt`.
