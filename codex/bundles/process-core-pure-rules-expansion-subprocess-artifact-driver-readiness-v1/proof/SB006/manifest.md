# SB006 Proof Manifest
## Summary
- Subbundle: SB006 - Gate B subprocess lifecycle parity.
- Status: Completed.
- Invariant ID: SB006-INV-001
- Hash reference: repo://src/CanDoItAll.Processes.Core/Subprocess/ProcessSubprocessLifecycleRules.cs SHA-256 f7ab443c9b5fa6a9e41bc29b45614e692f3131dba0236d8cd2f9c6ca94cb1f4c
- Semantic invariant contract: bundle://proof/SB006/semantic-invariants.md
- Changed file: repo://src/CanDoItAll.Processes.Core/Subprocess/ProcessSubprocessLifecycleRules.cs
- Changed file: repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessSubprocessLifecycleRules.cs
## Evidence
- Source assertion transcript: bundle://proof/shared/transcripts/source-assertions.txt
- Source proof artifact: repo://src/CanDoItAll.Processes.Core/Subprocess/ProcessSubprocessLifecycleRules.cs
- Passing transcript: bundle://proof/shared/transcripts/focused-integration.txt
- Failing-first transcript: bundle://proof/SB006/transcripts/failing-first.txt
- Anti-stub audit transcript: bundle://proof/shared/transcripts/anti-stub-scan.txt
- Dependency scan transcript: bundle://proof/shared/transcripts/core-forbidden-scan.txt
- Build transcript: bundle://proof/shared/transcripts/build.txt
- Driver token scan transcript: bundle://proof/shared/transcripts/driver-token-scan.txt
- No UI/media transcript: bundle://proof/shared/transcripts/no-ui-media-drift-scan.txt
## Closure
- Expected behavior: Parent subprocess transition facts preserve the module status and reason outputs while Core remains side-effect free.
- Disallowed shallow implementation: Hard-coding happy-path status text or leaving the deterministic status/reason logic in module services.
- Downstream dependency check: bundle://proof/shared/transcripts/build.txt
