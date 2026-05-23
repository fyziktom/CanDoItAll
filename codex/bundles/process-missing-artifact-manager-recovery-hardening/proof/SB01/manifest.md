# SB01 Proof Manifest

## Scope

Critical subbundle `SB01 01-manager-artifact-recovery`.

## Changed Runtime Files

- `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessRunAutomationDispatchService.CompletionArtifactRecovery.cs`
- `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessRunAutomationDispatchService.Dispatch.cs`
- `repo://tests/CanDoItAll.Tests.Integration/ProcessRunAutomationDispatchServiceTests.cs`

## SHA-256 Changed-File Hashes

- `564BD9CF96EE9C304B0E9E6F2C86A066D701F8090B994E0EA7C6F5765B249EDD` for `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessRunAutomationDispatchService.CompletionArtifactRecovery.cs`
- `6CA1CF94238A7EC42B61B223D8AA1DC7D88DD8BE2BA9268A4CC808EAF0899841` for `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessRunAutomationDispatchService.Dispatch.cs`
- `58F71243197DC496867BF5ED0168C730181B0AAEA8C2ABD52CE8B2E7E6D5A81A` for `repo://tests/CanDoItAll.Tests.Integration/ProcessRunAutomationDispatchServiceTests.cs`

## Command Transcripts

- Passing transcript: `proof/SB01/transcripts/passing-test.txt`.
- Anti-stub audit transcript: `proof/SB01/transcripts/anti-stub-audit.txt`.
- Failing-first transcript: `proof/SB01/transcripts/failing-first-exemption.md`.
- Failing-first N/A process/non-production exemption: the initial local command failed on a locked demo-app assembly, while the live run evidence is the behavioral failing input.

## Semantic Invariant Contract

- Contract: `proof/SB01/semantic-invariants.md`.
- Invariant ID: `SB01-I001`.

## Semantic Adequacy Evidence

- The missing completion-artifact recovery path now resolves a process manager technical agent before launching recovery.
- The path records a manager directive before manager recovery execution.
- The path rejects manager recovery when the resolved manager technical agent is the same as the current step executor.
- The recovery directive instructs the process manager to use prior step history, upstream artifacts, previous execution run id, tool receipts, changed files, and current-run evidence.
- Completion remains gated on existing artifact projection; remaining missing artifacts return `Blocked` with exact artifact titles.
- A stranded in-progress step with a recoverable prior execution and missing required artifacts now enters manager artifact recovery before another executor attempt can start.
- Manager resolution now uses the assigned run manager role before ambiguous fallback manager-like agents, matching the live Tetris run shape.
- Reopened blocked missing-artifact steps can still use the latest prior terminal automation run for manager artifact recovery instead of losing that context when `StartedAtUtc` changes.
- Manager recovery executes with a review/artifact-recovery step view so implementation build/run/test proof requirements do not force a manager retry loop.
