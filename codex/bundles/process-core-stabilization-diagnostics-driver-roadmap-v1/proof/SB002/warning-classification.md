# SB002 CA1416 Warning Classification

## Finding
- Baseline build transcript: `bundle://proof/SB001/transcripts/baseline-build.txt`
- The process cleanup warnings were `CA1416` platform analyzer warnings in `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessRunAutomationDispatchService.DotnetRunCleanup.cs`.
- They came from host process APIs and `subst` cleanup helpers that are unsupported on browser.

## Decision
- Do not add blanket suppression.
- Keep desktop/server cleanup behavior intact.
- Add explicit browser entry short-circuit before process-tree cleanup.
- Mark non-browser process-tree cleanup and Windows-only `subst` helpers with targeted platform annotations.

## Proof
- Failing-first warning assertion: `bundle://proof/SB003/transcripts/failing-first-process-ca1416-scan.txt`
- Passing warning assertion: `bundle://proof/SB003/transcripts/passing-process-ca1416-scan.txt`
- Passing build: `bundle://proof/SB003/transcripts/post-browser-entry-guard-build.txt`
