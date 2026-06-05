# SB12 Proof Manifest

## Status

- Status: `Completed`

## Evidence

- Semantic invariant contract: `bundle://proof/SB12/semantic-invariants.md`
- Source proof: `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessCriticalToolFailureRules.cs`, `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessCompletionBlockerRules.cs`, `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessCompletionDecisionRules.cs`
- Passing transcript: `bundle://proof/SB12/transcripts/completion-critical-parity.txt`
- Failing-first: N/A - process parity gate; existing integration negative fixtures cover rejected completion and critical-failure cases.
- Anti-stub audit transcript: `bundle://proof/SB16/transcripts/final-source-scans.txt`
- Changed-file SHA-256: `1d7e8a6a1a84b9484726d2d016abbe89cc76c5cba3cbc72e92f7509dcb6dda0d` for `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessCriticalToolFailureRules.cs`
- Changed-file SHA-256: `6a9a8d7df074babc6e0b6766c74c298d36124606c193c1e826d535fe4607cdd0` for `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessCompletionBlockerRules.cs`
- Hash list: `bundle://proof/SB16/hashes/changed-file-hashes.txt`

## Notes

- Gate C passed completion and critical-failure parity before recovery retry facts moved.
