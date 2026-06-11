# SB07 Proof Manifest

- Subbundle: SB07
- Status: Completed
- Source references: `repo://tests/CanDoItAll.Tests.Integration/ProcessTemplateExecutionE2ETests.cs`, `repo://tests/CanDoItAll.Tests.Playwright/AppSmokeTests.ProcessStartSmoke.cs`
- Semantic invariant contract: `bundle://proof/SB07/semantic-invariants.md`
- SHA-256 hash: `1b015fc87d1e252d8be309e484fc60de19ac354fd1d7df06cd05576d3436b722`
- Passing transcript: `bundle://proof/SB07/transcripts/closure.txt`
- Anti-stub audit transcript: `bundle://proof/SB07/transcripts/closure.txt`
- Failing-first: N/A - process regression matrix uses existing representative commands plus adversarial classification guards, and no production behavior was added solely for SB07.
- Test name: `Process_runtime_host_codefirst_SB01_INV_008_manual_contract_tests_are_not_counted_as_automation_proofs`
