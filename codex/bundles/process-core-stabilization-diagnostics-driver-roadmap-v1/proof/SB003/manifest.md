# SB003 Proof Manifest

## Scope
- Subbundle: `SB003 - Gate A - clean baseline and warning policy`
- Invariant IDs: `SB003-INV-001`
- Changed production source: `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessRunAutomationDispatchService.DotnetRunCleanup.cs`

## Changed-File Hashes
- `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessRunAutomationDispatchService.DotnetRunCleanup.cs`
  - SHA-256: `46C87612B298B5AB49550243B2BE91EBB30CB9C036D4539EAFA796275612D9D7`
  - Hash transcript: `bundle://proof/SB003/transcripts/changed-file-hashes.txt`

## Command Transcripts
- Baseline build with process cleanup warnings: `bundle://proof/SB001/transcripts/baseline-build.txt`
- Failing-first warning assertion: `bundle://proof/SB003/transcripts/failing-first-process-ca1416-scan.txt`
- Passing build after warning-policy fix: `bundle://proof/SB003/transcripts/post-browser-entry-guard-build.txt`
- Passing warning assertion: `bundle://proof/SB003/transcripts/passing-process-ca1416-scan.txt`
- Architecture guard tests: `bundle://proof/SB003/transcripts/architecture-tests.txt`
- Focused process dispatch integration tests: `bundle://proof/SB003/transcripts/process-dispatch-integration-tests.txt`
- Source assertions: `bundle://proof/SB003/transcripts/source-assertions.txt`
- Anti-stub audit: `bundle://proof/SB003/transcripts/anti-stub-audit.txt`
- UI/media drift scan: `bundle://proof/SB003/transcripts/changed-files-ui-media-scan.txt`

## Source Assertions
- `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessRunAutomationDispatchService.DotnetRunCleanup.cs` now has an explicit browser return before process-tree cleanup dispatch.
- `StopRecordedProcessTree` is explicitly marked unsupported on browser and remains a no-op on browser.
- `ResolveCurrentSubstDriveTarget` and `TryDismountSubstDrive` are explicitly Windows-only helper paths.
- The cleanup path still preserves the existing runtime behavior: process tree cleanup remains attempted for recorded process IDs on non-browser hosts, and static web assets alias cleanup remains Windows-only.

## Failing-First And Passing Proof
- Failing-first: `bundle://proof/SB003/transcripts/failing-first-process-ca1416-scan.txt` exits non-zero because the baseline build contained `ProcessRunAutomationDispatchService.DotnetRunCleanup.cs` `CA1416` warnings.
- Passing: `bundle://proof/SB003/transcripts/passing-process-ca1416-scan.txt` exits zero because the post-fix build contains no process cleanup `CA1416` warnings.

## Anti-Stub Audit
- `bundle://proof/SB003/transcripts/anti-stub-audit.txt` scans the changed production source for `TODO`, `NotImplemented`, `throw new NotImplementedException`, `stub`, and `fixture-specific`; no matches were found.

## Semantic Contract
- Semantic invariants: `bundle://proof/SB003/semantic-invariants.md`
- No production behavior artifact matrix is required for this subbundle because the change adds platform guard metadata and an entry guard only; it does not add a production signal, persisted state, durable record, or domain event.
