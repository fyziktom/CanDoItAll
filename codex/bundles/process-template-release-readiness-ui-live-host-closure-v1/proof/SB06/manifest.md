# SB06 Proof Manifest

- Subbundle: SB06
- Status: Completed
- Source references: `repo://src/CanDoItAll.Modules.Processes/Runtime/ProcessesService.Runtime.TriggerStart.cs`, `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessReadOnlyVerificationJobRunner.cs`
- Semantic invariant contract: `bundle://proof/SB06/semantic-invariants.md`
- SHA-256 hash: `f593ae88b2e4b9130063d9808c8be725c89352b1566e3fd8643547b8afd5eea3`
- Passing transcript: `bundle://proof/SB06/transcripts/closure.txt`
- Anti-stub audit transcript: `bundle://proof/SB06/transcripts/closure.txt`
- Failing-first: N/A - process-owned trigger tests and source scan cover the adversarial direct-driver case, and no production behavior was added for SB06.
- Test name: `Target_launcher_starts_real_process_run`
