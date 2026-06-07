# SB003 Semantic Invariants

## Invariant SB003-INV-001
- Invariant ID: `SB003-INV-001`
- Source raw note: `Prepare next phases toward complete stable Process Core` and `Preserve functionality`.
- Expected behavior: process cleanup platform-warning policy is explicit; browser hosts do not execute process cleanup APIs, Windows-only `subst` cleanup remains Windows-only, and non-browser process cleanup behavior remains unchanged.
- Disallowed shallow implementation: suppress `CA1416` project-wide, remove cleanup code, or hide analyzer warnings without proving the host guards.
- Failing-first test: `bundle://proof/SB003/transcripts/failing-first-process-ca1416-scan.txt` proves the baseline build exposed process cleanup `CA1416` warnings.
- Passing test: `bundle://proof/SB003/transcripts/passing-process-ca1416-scan.txt`, `bundle://proof/SB003/transcripts/architecture-tests.txt`, and `bundle://proof/SB003/transcripts/process-dispatch-integration-tests.txt`.
- Changed source files: `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessRunAutomationDispatchService.DotnetRunCleanup.cs`.
- Production assertions: `bundle://proof/SB003/transcripts/source-assertions.txt` proves the browser entry guard and platform annotations are present in the production cleanup source.
- Red-team negative case: `bundle://proof/SB003/transcripts/anti-stub-audit.txt` proves the change did not add TODO, NotImplemented, stub, or fixture-specific production paths.
- Downstream dependency check: SB004-SB006 may proceed because SB003 removed process cleanup `CA1416` warning drift while the focused architecture and dispatch integration tests remain green.

## Production Behavior Artifact Matrix
- N/A: this subbundle introduces no production signal, persisted state, durable record, or domain event.
