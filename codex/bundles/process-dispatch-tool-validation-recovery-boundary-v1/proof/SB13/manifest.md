# SB13 Proof Manifest

## Status

- Status: `Completed`

## Evidence

- Semantic invariant contract: `bundle://proof/SB13/semantic-invariants.md`
- Source proof: `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessRecoveryRetryDecisionRules.cs`, `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessRunAutomationDispatchService.RecoveryPackets.cs`
- Passing transcript: `bundle://proof/SB13/transcripts/recovery-retry-parity.txt`
- Failing-first: N/A - process refactor with preserved public behavior; existing retry negative fixtures cover rejected/no-progress cases.
- Anti-stub audit transcript: `bundle://proof/SB16/transcripts/final-source-scans.txt`
- Changed-file SHA-256: `820c6779de2622b6fe4577d78f3198c7d4b7aaf85e1f30541354aef0e5f42124` for `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessRecoveryRetryDecisionRules.cs`
- Changed-file SHA-256: `bdff9fc34b5d75691bb22bac0de9ae8799bb2b763ee9ce8a1fcd84467f955c7d` for `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessRunAutomationDispatchService.RecoveryPackets.cs`
- Hash list: `bundle://proof/SB16/hashes/changed-file-hashes.txt`

## Notes

- Recovery facts moved; recovery journal persistence and packet creation remained in dispatcher code.
