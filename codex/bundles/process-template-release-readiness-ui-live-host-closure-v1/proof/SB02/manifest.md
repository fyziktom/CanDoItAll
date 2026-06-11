# SB02 Proof Manifest

- Subbundle: SB02
- Status: Completed
- Source references: `repo://tests/CanDoItAll.Tests.Integration/BusinessPlanProcessPostgresIntegrationTests.cs`, `repo://tests/CanDoItAll.Tests.Integration/ProcessRuntimeHostCodeFirstGuardTests.cs`
- Semantic invariant contract: `bundle://proof/SB02/semantic-invariants.md`
- SHA-256 hash: `a778e6cb6d0c8d2d3d953b697c0fbd0ea012b0610cc1a3428b1195dc6d082d93`
- Passing transcript: `bundle://proof/SB02/transcripts/closure.txt`
- Anti-stub audit transcript: `bundle://proof/SB02/transcripts/closure.txt`
- Failing-first: N/A - process classification is covered by adversarial negative proof in the same test command, and no production behavior was added for SB02.
- Test name: `Business_plan_process_SB05_INV_001_completes_on_postgresql_through_automation_dispatch_finalizer_and_readback`
