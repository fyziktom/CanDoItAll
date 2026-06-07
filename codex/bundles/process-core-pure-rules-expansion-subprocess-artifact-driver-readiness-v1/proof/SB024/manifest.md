# SB024 Proof Manifest
## Summary
- Subbundle: SB024 - Gate H integration closure.
- Status: Completed.
- Invariant ID: SB024-INV-001
- Hash reference: repo://tests/CanDoItAll.Tests.Integration/ProcessRunAutomationDispatchServiceTests.cs SHA-256 7ae3ecdff28c859d1d4f3ebbd169d99f0365d44899610cbad9d67d9861e60640
- Semantic invariant contract: bundle://proof/SB024/semantic-invariants.md
- Changed file: repo://tests/CanDoItAll.Tests.Integration/ProcessRunAutomationDispatchServiceTests.cs
## Evidence
- Source assertion transcript: bundle://proof/shared/transcripts/source-assertions.txt
- Source proof artifact: repo://tests/CanDoItAll.Tests.Integration/ProcessRunAutomationDispatchServiceTests.cs
- Passing transcript: bundle://proof/shared/transcripts/focused-integration.txt
- Failing-first transcript: bundle://proof/SB024/transcripts/failing-first.txt
- Anti-stub audit transcript: bundle://proof/shared/transcripts/anti-stub-scan.txt
- Dependency scan transcript: bundle://proof/shared/transcripts/core-forbidden-scan.txt
- Build transcript: bundle://proof/shared/transcripts/build.txt
- Driver token scan transcript: bundle://proof/shared/transcripts/driver-token-scan.txt
- No UI/media transcript: bundle://proof/shared/transcripts/no-ui-media-drift-scan.txt
## Closure
- Expected behavior: Focused integration tests cover lifecycle, subprocess artifact mapping, and artifact satisfaction parity.
- Disallowed shallow implementation: Relying only on compile proof without exercising moved deterministic behavior.
- Downstream dependency check: bundle://proof/shared/transcripts/build.txt
